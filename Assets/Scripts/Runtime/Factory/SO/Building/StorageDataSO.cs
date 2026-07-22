using UnityEngine;

/// <summary>저장소. 받은 아이템을 보관하고 연결된 하류로 내보낸다. 용량은 버퍼 크기로 설정.</summary>
[CreateAssetMenu(fileName = "NewStorage", menuName = "Factory/Buildings/Storage")]
public class StorageDataSO : BuildingDataSO
{
    public override IBuildingBehavior CreateBehavior(Building building)
        => new StorageBehavior(building);
}

// ─── 행동 ──────────────────────────────────────────────────────

/// <summary>
/// 큰 버퍼를 가진 저장소.
/// 입력 버퍼로 받은 아이템을 출력 버퍼로 옮긴 뒤, 연결된 하류로 Push 시도.
/// E 상호작용: 보관함(출력 버퍼)을 인벤 화면과 함께 연다.
/// </summary>
public class StorageBehavior : IBuildingBehavior, IInteractiveBehavior
{
    readonly Building _b;
    public StorageBehavior(Building b) => _b = b;
    public void OnAfterPlaced() { }

    public string InteractPrompt => "보관함 열기";

    public void Interact(PlayerController player)
    {
        // 실질 보관 = 출력 버퍼 (입력 버퍼는 경유지 — Tick이 곧바로 출력으로 옮긴다)
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OpenContainerScreen(_b.Output);
    }

    public void Tick(float dt)
    {
        // 입력 버퍼 → 출력 버퍼 이동 (출력 여유만큼만)
        foreach (var (item, count) in _b.Input.Snapshot())
        {
            int move = Mathf.Min(count, _b.Output.RoomFor(item));
            if (move <= 0) continue;
            _b.Output.TryAdd(item, move);
            _b.Input.TryConsume(item, move);
            _b.NotifyUpstream(); // 입력 버퍼에 자리 생김 → 막혀 있던 상류 깨움
        }

        _b.FlushOutputs();
    }
}
