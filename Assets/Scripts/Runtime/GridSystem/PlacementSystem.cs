using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// 그리드 기반 배치/철거 컨트롤러 (Input System)
///
/// 모드:
///  - None        : 대기. 핫키(B=배치, X=철거) 또는 외부 UI가 API로 진입시킨다.
///  - Placing     : SelectBuilding()으로 진입. 좌클릭 설치, R 회전, T 벨트 모양 순환.
///  - Demolishing : EnterDemolishMode()로 진입. 조준 건물 하이라이트, 좌클릭 철거.
///  두 모드 모두 우클릭/ESC로 종료 (핫키 재입력도 토글로 종료).
///
/// 입력: Keyboard.current / Mouse.current 직접 폴링.
///   PlayerControls.inputactions를 수정하지 않기 위한 선택 (팀 병합 충돌 방지).
///   액션 에셋으로 옮기게 되면 이 파일의 폴링부만 교체하면 된다.
///
/// 조준: 커서가 잠겨 있으면(FPS 플레이 중) 화면 중앙 크로스헤어, 아니면 마우스 커서.
///
/// 외부(배치 UI) 연동 표면:
///   SelectBuilding(so) / SelectBuildingByIndex(i) / EnterDemolishMode() / ExitMode()
///   Mode / CurrentBuilding / Buildings
/// </summary>
public class PlacementSystem : MonoBehaviour
{
    public enum BuildMode { None, Placing, Demolishing }

    [Header("References")]
    [SerializeField] private Camera cam;
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private BuildingDataSO[] buildingDataList;

    [Header("Grid")]
    [SerializeField] private float cellSize = 1f;
    [SerializeField] private Vector3 gridOrigin = Vector3.zero;

    // GridManager가 그리드 설정 정합성을 검사할 때 사용
    public float CellSize => cellSize;
    public Vector3 GridOrigin => gridOrigin;

    [Header("Terrain Height")]
    [SerializeField] private float raycastStartHeight = 100f;
    [SerializeField] private float maxSlopeHeightDiff = 0.5f;

    [Header("Preview Materials")]
    [SerializeField] private Material validMat;
    [SerializeField] private Material invalidMat;
    [Tooltip("철거 모드에서 대상 건물에 입힐 하이라이트 머티리얼 (빨강 반투명 추천).")]
    [SerializeField] private Material demolishHighlightMat;

    [Header("Hotkeys")]
    [Tooltip("배치 모드 토글 — 마지막으로 선택했던 건물로 진입.")]
    [SerializeField] private Key buildKey = Key.B;
    [Tooltip("철거 모드 토글.")]
    [SerializeField] private Key demolishKey = Key.X;

    private GridSystem grid;
    private BuildMode mode = BuildMode.None;

    // 배치 모드 상태
    private BuildingDataSO current;
    private int lastIndex;                  // 배치 핫키 토글용 — 마지막 선택 건물 인덱스
    private GameObject preview;
    private List<Renderer> previewRenderers = new();
    private int rotation;
    private BeltShape beltShape;

    // 철거 모드 상태
    private Building hovered;                                          // 지금 하이라이트 중인 건물
    private readonly Dictionary<Renderer, Material[]> savedMats = new(); // 원본 머티리얼 백업

    // ── 외부(UI) 조회용
    public BuildMode Mode => mode;
    public BuildingDataSO CurrentBuilding => current;
    public IReadOnlyList<BuildingDataSO> Buildings => buildingDataList;

    void Awake()
    {
        grid = new GridSystem(cellSize, gridOrigin);
        if (cam == null) cam = Camera.main;
    }

    void Update()
    {
        HandleModeHotkeys();

        switch (mode)
        {
            case BuildMode.Placing: UpdatePlacing(); break;
            case BuildMode.Demolishing: UpdateDemolishing(); break;
        }
    }

    // ===================== 입력 헬퍼 =====================

    static bool KeyDown(Key key)
    {
        var kb = Keyboard.current;
        return kb != null && kb[key].wasPressedThisFrame;
    }

    static bool ClickDown =>
        Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && !IsPointerOverUI();

    static bool CancelDown =>
        (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame) || KeyDown(Key.Escape);

    /// <summary>UI 위 클릭이 지형/건물 조작으로 새는 것 방지.</summary>
    static bool IsPointerOverUI() =>
        EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

    /// <summary>조준 레이 — 커서 잠금(FPS)이면 화면 중앙, 아니면 마우스 커서.</summary>
    Ray AimRay()
    {
        if (Cursor.lockState == CursorLockMode.Locked || Mouse.current == null)
            return cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        return cam.ScreenPointToRay(Mouse.current.position.ReadValue());
    }

    void HandleModeHotkeys()
    {
        if (KeyDown(buildKey))
        {
            if (mode == BuildMode.Placing) ExitMode();
            else SelectBuildingByIndex(lastIndex);
        }
        if (KeyDown(demolishKey))
        {
            if (mode == BuildMode.Demolishing) ExitMode();
            else EnterDemolishMode();
        }
    }

    // ===================== 모드 진입/종료 (UI 연동 표면) =====================

    public void SelectBuilding(BuildingDataSO data)
    {
        if (data == null) return;
        ExitMode();
        mode = BuildMode.Placing;
        current = data;
        rotation = 0;
        beltShape = BeltShape.Straight;
        SpawnPreview();
    }

    /// <summary>buildingDataList 인덱스로 선택 — 배치 UI의 버튼/단축키용.</summary>
    public void SelectBuildingByIndex(int index)
    {
        if (buildingDataList == null || index < 0 || index >= buildingDataList.Length) return;
        lastIndex = index;
        SelectBuilding(buildingDataList[index]);
    }

    public void EnterDemolishMode()
    {
        ExitMode();
        mode = BuildMode.Demolishing;
    }

    /// <summary>현재 모드를 빠져나오며 프리뷰/하이라이트를 정리한다.</summary>
    public void ExitMode()
    {
        if (preview != null) Destroy(preview);
        previewRenderers.Clear();
        current = null;

        ClearHovered();

        mode = BuildMode.None;
    }

    // ===================== 배치 모드 =====================

    private void UpdatePlacing()
    {
        if (KeyDown(Key.R))
            rotation = (rotation + 1) % 4;

        // T: 벨트 모양 순환 (직선 → L → R)
        if (KeyDown(Key.T) && current is BeltDataSO)
        {
            beltShape = (BeltShape)(((int)beltShape + 1) % 3);
            SpawnPreview();   // 모양이 바뀌면 프리뷰 메시 교체
        }

        if (CancelDown)
        {
            ExitMode();
            return;
        }

        // 지형을 조준하지 못하면 프리뷰를 숨긴다 (허공에 떠 있지 않게)
        if (!TryGetGroundPoint(out Vector3 cursorPoint))
        {
            if (preview != null) preview.SetActive(false);
            return;
        }
        if (preview != null && !preview.activeSelf) preview.SetActive(true);

        Vector2Int origin = grid.WorldToGrid(cursorPoint);
        Vector2Int size = current.GetRotatedSize(rotation);

        bool heightOk = TryGetFootprintHeight(origin, size, out float groundY);

        Vector3 pos = grid.GetFootprintCenter(origin, size);
        pos.y = groundY;
        preview.transform.position = pos;
        preview.transform.rotation = Quaternion.Euler(0, rotation * 90, 0);

        bool canPlace = heightOk && CanPlace(origin, size);
        SetPreviewColor(canPlace);

        if (canPlace && ClickDown)
            Place(origin, pos);
    }

    private void Place(Vector2Int origin, Vector3 pos)
    {
        if (current is BeltDataSO belt)
            PlacementBridge.Place(current, origin, pos, rotation,
                BeltDataSO.BuildPorts(beltShape, rotation), belt.PrefabFor(beltShape));
        else
            PlacementBridge.Place(current, origin, pos, rotation);
    }

    // ===================== 철거 모드 =====================

    private void UpdateDemolishing()
    {
        if (CancelDown)
        {
            ExitMode();
            return;
        }

        // 조준하는 칸 → 그 칸의 건물 찾기
        Building target = null;
        if (TryGetGroundPoint(out Vector3 cursorPoint))
        {
            Vector2Int cell = grid.WorldToGrid(cursorPoint);
            target = FactoryBootstrap.Instance.Sim.Grid.GetAt(cell);
        }

        // 대상이 바뀌면 하이라이트 갱신
        SetHovered(target);

        // 좌클릭으로 철거 (모드는 유지 → 연속 철거 가능)
        if (target != null && ClickDown)
            Demolish(target);
    }

    /// <summary>특정 건물을 철거한다. 점유 칸 모두 해제 + 인스턴스 파괴.</summary>
    public void Demolish(Building b)
    {
        if (b == null) return;

        // 하이라이트 대상이면 복원 절차 없이 참조만 비운다 (어차피 곧 파괴됨)
        if (hovered == b)
        {
            hovered = null;
            savedMats.Clear();
        }

        PlacementBridge.Remove(b);
    }

    /// <summary>칸 좌표로 철거 (외부 호출용 편의 오버로드).</summary>
    public void Demolish(Vector2Int cell)
        => Demolish(FactoryBootstrap.Instance.Sim.Grid.GetAt(cell));

    // ---- 하이라이트 적용/복원 ----
    private void SetHovered(Building b)
    {
        if (hovered == b) return;   // 변화 없으면 그대로
        ClearHovered();             // 이전 대상 원복

        hovered = b;
        if (b == null || demolishHighlightMat == null) return;

        var view = FactoryBootstrap.Instance.GetView(b);
        if (view == null) return;

        foreach (var r in view.GetComponentsInChildren<Renderer>())
        {
            savedMats[r] = r.sharedMaterials;               // 원본 백업
            var arr = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < arr.Length; i++) arr[i] = demolishHighlightMat;
            r.sharedMaterials = arr;                        // 하이라이트 입히기
        }
    }

    private void ClearHovered()
    {
        foreach (var kv in savedMats)
            if (kv.Key != null) kv.Key.sharedMaterials = kv.Value; // 원본 복원
        savedMats.Clear();
        hovered = null;
    }

    // ===================== 공용 헬퍼 =====================

    private bool TryGetGroundPoint(out Vector3 point)
    {
        if (Physics.Raycast(AimRay(), out RaycastHit hit, 1000f, groundMask))
        {
            point = hit.point;
            return true;
        }
        point = default;
        return false;
    }

    private bool SampleCellHeight(Vector2Int cell, out float height)
    {
        Vector3 center = grid.GridToWorldCenter(cell);
        Vector3 start = new Vector3(center.x, gridOrigin.y + raycastStartHeight, center.z);
        if (Physics.Raycast(start, Vector3.down, out RaycastHit hit,
                            raycastStartHeight * 2f, groundMask))
        {
            height = hit.point.y;
            return true;
        }
        height = 0f;
        return false;
    }

    private bool TryGetFootprintHeight(Vector2Int origin, Vector2Int size, out float y)
    {
        float min = float.MaxValue, max = float.MinValue;
        foreach (var cell in GetCells(origin, size))
        {
            if (!SampleCellHeight(cell, out float h)) { y = 0f; return false; }
            if (h < min) min = h;
            if (h > max) max = h;
        }
        y = max;
        return (max - min) <= maxSlopeHeightDiff;
    }

    private static IEnumerable<Vector2Int> GetCells(Vector2Int origin, Vector2Int size)
    {
        for (int x = 0; x < size.x; x++)
            for (int z = 0; z < size.y; z++)
                yield return origin + new Vector2Int(x, z);
    }

    private static bool CanPlace(Vector2Int origin, Vector2Int size)
        => GetCells(origin, size).All(c => !FactoryBootstrap.Instance.Sim.Grid.IsOccupied(c));

    private void SpawnPreview()
    {
        if (preview != null) Destroy(preview);
        previewRenderers.Clear();

        var prefab = current is BeltDataSO belt ? belt.PrefabFor(beltShape) : current.prefab;
        if (prefab == null)
        {
            preview = new GameObject("Preview (프리팹 없음)");
            return;
        }

        preview = Instantiate(prefab);
        foreach (var col in preview.GetComponentsInChildren<Collider>())
            col.enabled = false;
        previewRenderers = preview.GetComponentsInChildren<Renderer>().ToList();
    }

    private void SetPreviewColor(bool valid)
    {
        Material mat = valid ? validMat : invalidMat;
        if (mat == null) return;
        foreach (var r in previewRenderers)
            r.sharedMaterial = mat;
    }
}