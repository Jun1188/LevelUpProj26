using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 플레이어 아바타 조작 — 입력 파이프라인의 Player 리시버 (최하위 우선순위).
/// 이동/카메라(연속)는 폴링(§7-1), 점프/상호작용/인벤 열기(이산)는 OnInput 라우팅.
///
/// ④단계에서 PlayerInput(Send Messages) 제거·파이프라인 이관:
///  - 핫바 선택/드롭 → HotbarController, 사격/재장전 → WeaponManager (각자 리시버)
///  - isInventoryOpen 입력 가드 삭제 — 팝업이 UI 맵을 Push하면 Gameplay 신호가 원천 차단되고,
///    폴링도 비활성 맵에서 0을 반환하므로 이동/시점이 저절로 멎는다
///  - 커서/크로스헤어 부수효과 → InventoryPopup의 Enter/Exit (PausePopup과 같은 패턴)
/// </summary>
public class PlayerController : Entity, IInputReceiver
{
    #region [1. Variables - Inspector Settings]

    [Header("Core Components")]
    public Rigidbody rb;

    [Header("Camera & Mouse Settings")]
    public Transform playerCamera;
    public float mouseSensitivity = 1f;
    public float MAX_CAMERA_ROTATION_X = 90f;
    private float cameraRotationX = 0f;

    [Header("Movement Settings")]
    public float jumpForce = 10f;

    [Header("Gun & Combat Settings")]
    public WeaponManager weaponManager;

    [Header("Inventory Backend")]
    public Inventory playerInventory;
    private bool isInventoryOpen = false;
    private Inventory currentOpenedInventory = null;

    [Header("Inventory & HUD UI")]
    public GameObject inventoryUIPanel;
    public InventoryUI inventoryUI;
    public InventoryUI chestInventoryUI;
    public GameObject crosshairUI;      // 표시/숨김은 InventoryPopup의 Enter/Exit가 수행

    [Header("Debug / Cheat Tools")]
    public ItemDataSO debugTestItem;

    private Vector2 moveInput;
    private Vector2 mouseInput;
    private bool isJumpPressed;

    #endregion

    #region [2. Input Pipeline - IInputReceiver]

    public int Priority => InputPriority.Player;   // 항상 최하위 — 위에서 아무도 안 받은 입력만 도달
    public bool IsInputActive => isActiveAndEnabled;

    public bool OnInput(in InputEvent e)
    {
        if (e.Phase != InputActionPhase.Performed)
        {
            // 점프는 눌림/뗌 상태가 필요 — Canceled에서 해제
            if (e.Id == InputActionId.Jump && e.Phase == InputActionPhase.Canceled)
                isJumpPressed = false;
            return false;
        }

        switch (e.Id)
        {
            case InputActionId.Jump:
                isJumpPressed = true;
                return true;

            case InputActionId.Interact:
                TryInteract();
                return true;

            // 열기만 담당 — 인벤이 열려 있으면 InventoryPopup(상위 우선순위)이 먼저 가로채 닫는다
            case InputActionId.ToggleInventory:
                OpenPlayerInventory();
                return true;
        }
        return false;
    }

    #endregion

    #region [3. Unity Lifecycle]

    protected override void Start()
    {
        base.Start();

        CloseInventory();
        Cursor.lockState = CursorLockMode.Locked;   // 시작 시 패널이 이미 닫혀 있으면 팝업 Exit이 안 불리므로 직접 잠금
        moveSpeed = 5f;

        if (InputManager.Instance != null) InputManager.Instance.Register(this);
        else Debug.LogError("[PlayerController] 씬에 InputManager가 없습니다.", this);

        if (InventoryManager.Instance != null && playerInventory != null)
        {
            InventoryManager.Instance.CheckWeaponEquip(playerInventory);
        }
    }

    private void OnDisable()
    {
        if (InputManager.Instance != null) InputManager.Instance.Unregister(this);
    }

    protected override void Update()
    {
        base.Update();

        // 연속 입력 폴링 — 소속 맵이 비활성(팝업 열림)이면 0이 읽힌다
        if (InputManager.Instance != null)
        {
            moveInput = InputManager.Instance.ReadValue<Vector2>(InputActionId.Move);
            mouseInput = InputManager.Instance.ReadValue<Vector2>(InputActionId.Look);
        }

        HandleCameraRotation();
    }

    private void FixedUpdate()
    {
        HandleMovement();
        HandleJump();
    }

    #endregion

    #region [4. Core Mechanics - Movement & Camera]

    private void HandleCameraRotation()
    {
        float mouseX = mouseInput.x * mouseSensitivity * 0.1f;
        float mouseY = mouseInput.y * mouseSensitivity * 0.1f;

        cameraRotationX -= mouseY;
        cameraRotationX = Mathf.Clamp(cameraRotationX, -MAX_CAMERA_ROTATION_X, MAX_CAMERA_ROTATION_X);

        playerCamera.localRotation = Quaternion.Euler(cameraRotationX, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }

    private void HandleMovement()
    {
        Vector3 moveDir = transform.forward * moveInput.y + transform.right * moveInput.x;
        Vector3 targetVelocity = moveDir * moveSpeed;
        targetVelocity.y = rb.linearVelocity.y;

        rb.linearVelocity = targetVelocity;
    }

    private void HandleJump()
    {
        if (isJumpPressed && Mathf.Abs(rb.linearVelocity.y) < 0.001f)
        {
            rb.AddForce(new Vector3(0f, jumpForce, 0f), ForceMode.Impulse);
            isJumpPressed = false;
        }
    }

    // AddRecoil 삭제 — 반동은 ProceduralRecoil/WeaponKickback 모듈로 대체됨 (Entity 개편 병합)

    #endregion

    #region [5. Core Mechanics - Interaction]

    /// <summary>조준 중인 Interactable에 상호작용 (E). 인벤 열림 중 E로 닫기는 InventoryPopup이 처리.</summary>
    private void TryInteract()
    {
        Ray ray = new(playerCamera.position, playerCamera.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, 4f, LayerMask.GetMask("Interactable")))
        {
            Interactable interactable = hit.collider.GetComponentInParent<Interactable>();
            if (interactable != null)
            {
                interactable.OnInteract(this);
            }
        }
    }

    #endregion

    #region [6. Core Mechanics - Inventory System Management]

    public void ToggleInventory()
    {
        if (isInventoryOpen) CloseInventory();
        else OpenPlayerInventory();
    }

    public void OpenPlayerInventory()
    {
        if (isInventoryOpen) return;
        isInventoryOpen = true;
        HaltMomentum();

        if (inventoryUIPanel != null) inventoryUIPanel.SetActive(true);   // → InventoryPopup.OnEnable (UI 맵 Push + 커서/크로스헤어)
        if (inventoryUI != null) inventoryUI.RefreshAllUI();
    }

    public void OpenTargetInventory(Inventory targetInventory)
    {
        isInventoryOpen = true;
        currentOpenedInventory = targetInventory;
        HaltMomentum();

        if (chestInventoryUI != null)
        {
            chestInventoryUI.inventory = targetInventory;
            chestInventoryUI.gameObject.SetActive(true);
            chestInventoryUI.RefreshAllUI();
        }

        if (inventoryUIPanel != null) inventoryUIPanel.SetActive(true);
        if (inventoryUI != null) inventoryUI.RefreshAllUI();
    }

    public void CloseInventory()
    {
        // 인벤토리를 닫을 때 손에 든 아이템이 있다면 월드로 드롭!
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.DropMouseCarriageItem();
        }

        isInventoryOpen = false;
        currentOpenedInventory = null;

        if (inventoryUIPanel != null) inventoryUIPanel.SetActive(false);  // → InventoryPopup.OnDisable (UI 맵 Pop + 커서/크로스헤어)
        if (chestInventoryUI != null) chestInventoryUI.gameObject.SetActive(false);
    }

    /// <summary>인벤을 여는 순간 수평 관성 제거 — 열림 중 이동 입력은 맵 비활성으로 이미 0</summary>
    private void HaltMomentum()
    {
        if (rb != null) rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
    }

    #endregion
}