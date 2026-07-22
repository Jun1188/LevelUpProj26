using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 심의 ItemContainer를 기존 인벤 UI(Inventory 기반)로 보여주는 프록시 브리지.
/// 건물 보관함 화면(E)의 뒷단 — InventoryManager가 생성·소유한다.
///
/// 동작: 열려 있는 동안 매 프레임(LateUpdate, 심 Advance 이후)
///   ① 플레이어가 프록시에 가한 변화(종류별 개수 차이)를 컨테이너에 반영
///   ② 컨테이너 현재 상태를 프록시 슬롯으로 재미러링 (공장 유입/유출 실시간 표시)
///
/// 한계(v1, LOG 참조): 같은 프레임에 플레이어와 공장이 동시에 같은 아이템을 옮기면
/// 개수 차이 기반 조정이 드물게 어긋날 수 있다. 넣기 실패분은 플레이어 가방으로 반환.
/// </summary>
public class ContainerInventoryBridge : MonoBehaviour
{
    private ItemContainer source;
    private Inventory proxy;
    private InventoryUI screenUI;   // 변화 시 새로고침할 UI (chestInventoryUI)

    private readonly Dictionary<ItemDataSO, int> lastMirror = new();
    private readonly Dictionary<ItemDataSO, int> scratch = new();

    public bool IsOpen => source != null;

    /// <summary>컨테이너를 열고, UI에 꽂을 프록시 Inventory를 반환한다.</summary>
    public Inventory Open(ItemContainer container, InventoryUI ui)
    {
        source = container;
        screenUI = ui;

        if (proxy == null) proxy = gameObject.AddComponent<Inventory>();
        proxy.slotCount = container.SlotCount;
        proxy.slots = new ItemStack[container.SlotCount];

        MirrorToProxy();
        return proxy;
    }

    /// <summary>닫기 — 마지막 플레이어 변화를 반영하고 연결 해제.</summary>
    public void Close()
    {
        if (source == null) return;
        ApplyPlayerChanges();
        source = null;
        screenUI = null;
    }

    private void LateUpdate()   // 심 Advance(Update) 이후 — 한 프레임 안에서 일관된 조정
    {
        if (source == null) return;

        bool changed = ApplyPlayerChanges();
        changed |= MirrorToProxy();

        if (changed && screenUI != null) screenUI.RefreshAllUI();
    }

    /// <summary>프록시(UI 조작 결과)와 마지막 미러의 종류별 개수 차이를 컨테이너에 반영.</summary>
    private bool ApplyPlayerChanges()
    {
        Aggregate(proxy, scratch);

        bool changed = false;
        foreach (var (item, was) in EnumerateUnion(scratch, lastMirror))
        {
            int now = scratch.TryGetValue(item, out var n) ? n : 0;
            int delta = now - was;
            if (delta > 0)
            {
                int add = Mathf.Min(delta, source.RoomFor(item));
                if (add > 0) { source.TryAdd(item, add); changed = true; }

                // 컨테이너가 다 못 받은 잔여분 — 유실 방지: 플레이어 가방으로 반환
                int leftover = delta - add;
                if (leftover > 0 && InventoryManager.Instance != null &&
                    InventoryManager.Instance.playerController != null &&
                    InventoryManager.Instance.playerController.playerInventory != null)
                {
                    var bag = InventoryManager.Instance.playerController.playerInventory;
                    bag.AddItem(item, leftover);
                    InventoryManager.Instance.RefreshAllGameUIs(bag);
                    changed = true;
                }
            }
            else if (delta < 0)
            {
                int take = Mathf.Min(-delta, source.CountOf(item));
                if (take > 0) { source.TryConsume(item, take); changed = true; }
            }
        }
        return changed;
    }

    /// <summary>컨테이너 상태를 프록시 슬롯으로 재구성. 내용이 달라졌으면 true.</summary>
    private bool MirrorToProxy()
    {
        var snapshot = source.Snapshot();

        // 변화 감지 — 종류별 개수가 같으면 슬롯 재구성 생략 (UI 리프레시 낭비 방지)
        bool same = snapshot.Count == lastMirror.Count;
        if (same)
            foreach (var (item, n) in snapshot)
                if (!lastMirror.TryGetValue(item, out var m) || m != n) { same = false; break; }
        if (same) return false;

        lastMirror.Clear();
        for (int i = 0; i < proxy.slots.Length; i++) proxy.slots[i] = null;

        int slot = 0;
        foreach (var (item, total) in snapshot)
        {
            lastMirror[item] = total;
            int remain = total;
            while (remain > 0 && slot < proxy.slots.Length)
            {
                var stack = new ItemStack(item, Mathf.Min(remain, 64));
                remain -= stack.amount;
                proxy.slots[slot++] = stack;
            }
        }
        return true;
    }

    private static void Aggregate(Inventory inv, Dictionary<ItemDataSO, int> into)
    {
        into.Clear();
        foreach (var s in inv.slots)
            if (s != null && s.item != null && s.amount > 0)
                into[s.item] = into.TryGetValue(s.item, out var c) ? c + s.amount : s.amount;
    }

    /// <summary>두 딕셔너리 키 합집합을 (item, lastMirror 개수)로 순회.</summary>
    private IEnumerable<(ItemDataSO item, int was)> EnumerateUnion(
        Dictionary<ItemDataSO, int> now, Dictionary<ItemDataSO, int> was)
    {
        foreach (var kv in was) yield return (kv.Key, kv.Value);
        foreach (var kv in now)
            if (!was.ContainsKey(kv.Key)) yield return (kv.Key, 0);
    }
}
