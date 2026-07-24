using System.Collections.Generic;
using UnityEngine;

namespace Entities
{
    // 건물 엔티티 — 심 건물의 씬 표현이자 피격 주체 (구 BuildingDamageable/BuildingView 통합·대체).
    // 이동은 없고, 몬스터에게 피격되는 HP를 가진다. 자동 공격은 BattleTower(상속)가 담당.
    // HP 0 → die: 심 제거(PlacementBridge.Remove) + GameObject 소멸 + 플로우필드 갱신.
    //
    // 이름 충돌 주의: 팩토리 심의 plain C# Building(전역 네임스페이스)과 구분하기 위해
    // Entities 네임스페이스에 있다. 심 건물 참조는 global::Building(Sim 프로퍼티).
    // 코어처럼 심 없이 씬에 직접 배치하는 건물은 Sim이 null이어도 된다.
    public class Building : Entity, IInteractable
    {
        [Header("Building Settings")]
        [Tooltip("맵 중앙의 코어인지 여부. 플로우필드에서 타워보다 우선하는 최종 목표가 된다.")]
        [SerializeField] private bool isCore;

        // 이 엔티티가 대변하는 팩토리 심 건물(plain C#). PlacementBridge가 배치 시 연결한다.
        public global::Building Sim { get; set; }

        public bool IsCore => isCore;
        public override bool IsDead => base.IsDead || (Sim != null && Sim.IsRemoved);

        // ── 플레이어 상호작용(E) — 행동이 IInteractiveBehavior를 구현한 건물만 반응 (opt-in)
        public string Prompt => Sim?.Behavior is IInteractiveBehavior i ? i.InteractPrompt : null;

        public void Interact(PlayerController player)
        {
            if (Sim?.Behavior is IInteractiveBehavior i) i.Interact(player);
        }

        // 살아있는 건물 레지스트리 — 플로우필드 목표 수집과 몬스터의 사거리 검색용
        private static readonly List<Building> all = new List<Building>();
        public static IReadOnlyList<Building> All => all;

        // 코어 파괴 = 게임오버 조건. BattleManager가 구독한다.
        public static event System.Action<Building> CoreDestroyed;

        private void OnEnable()
        {
            all.Add(this);
            // 건물 배치/파괴는 몬스터 경로에 영향을 주므로 플로우필드 갱신 예약
            if (FlowFieldManager.Instance != null) FlowFieldManager.Instance.MarkDirty();
        }

        private void OnDisable()
        {
            all.Remove(this);
            if (FlowFieldManager.Instance != null) FlowFieldManager.Instance.MarkDirty();
        }

        // HP 0 → 몬스터의 사망 연출 지연 없이 즉시 소멸
        protected override void HandleDeath()
        {
            if (isCore) CoreDestroyed?.Invoke(this); // 소멸 전에 게임오버 통지

            if (Sim != null && !Sim.IsRemoved)
            {
                PlacementBridge.Remove(Sim); // 심 제거 + GridIndex 해제 + 뷰(GO) 파괴 일괄 처리
            }
            else
            {
                // 심 연결이 없는 건물(코어 등 씬 직접 배치)은 뷰만 정리
                Destroy(gameObject);
            }
        }

        // 심 건물(POCO) → 건물 엔티티 (구 BuildingDamageable.GetOrAttach).
        // PlacementBridge가 배치 시 모든 뷰 GO에 이 컴포넌트를 붙이고 매핑을 등록한다.
        public static Building GetOrAttach(global::Building sim)
        {
            if (sim == null || sim.IsRemoved) return null;

            var boot = FactoryBootstrap.Instance;
            if (boot == null) return null;

            var entity = boot.GetView(sim);
            if (entity == null) return null; // 뷰 없는 심 전용 건물(테스트 등)은 공격 대상이 될 수 없음

            if (entity.Sim == null) entity.Sim = sim;
            return entity;
        }

        // 사거리 내 가장 가까운 살아있는 건물 — 몬스터(FlowFieldState)의 도착/공격 판정용.
        // 멀티타일 건물을 고려해 콜라이더 표면 거리(DistanceTo)를 사용한다.
        public static Building FindClosestInRange(Vector3 from, float range)
        {
            Building closest = null;
            float minDistance = float.MaxValue;
            foreach (var building in all)
            {
                if (!building.IsValidTarget()) continue;
                float dist = building.DistanceTo(from);
                if (dist <= range && dist < minDistance)
                {
                    minDistance = dist;
                    closest = building;
                }
            }
            return closest;
        }
    }
}
