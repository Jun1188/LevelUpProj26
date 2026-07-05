using UnityEngine;

/// <summary>저장소. 받은 아이템을 보관하고 연결된 하류로 내보낸다. 용량은 버퍼 크기로 설정.</summary>
[CreateAssetMenu(fileName = "NewStorage", menuName = "Factory/Storage")]
public class StorageDataSO : BuildingDataSO
{
    public override IBuildingBehavior CreateBehavior(BuildingInstance instance)
        => new StorageBehavior(instance);
}

// ─── 행동 ──────────────────────────────────────────────────────

/// <summary>
/// 큰 버퍼를 가진 저장소.
/// 입력 버퍼로 받은 아이템을 출력 버퍼로 옮긴 뒤, 연결된 하류로 Push 시도.
/// </summary>
public class StorageBehavior : IBuildingBehavior
{
    readonly BuildingInstance _b;
    public StorageBehavior(BuildingInstance b) => _b = b;
    public void OnAfterPlaced() { }

    public void Tick(float dt)
    {
        // 입력 버퍼 → 출력 버퍼 이동 (출력 여유만큼만)
        foreach (var (item, count) in _b.Inventory.InputSnapshot)
        {
            int moved = 0;
            while (moved < count && _b.Inventory.TryAddOutput(item)) moved++;
            if (moved > 0)
            {
                _b.Inventory.TryConsumeInput(item, moved);
                _b.NotifyUpstream(); // 입력 버퍼에 자리 생김 → 막혀 있던 상류 깨움
            }
        }

        foreach (var (item, count) in _b.Inventory.OutputSnapshot)
            for (int k = 0; k < count && _b.TryPushOutput(item); k++)
                _b.Inventory.TryConsumeOutput(item);
    }
}
