using System.Collections.Generic;
using UnityEngine;

namespace Entities
{
    // 건물 엔티티 — 구 IInteractable + BuildingDamageable + BuildingView의 기능 통합.
    // 이동은 없고, 몬스터에게 피격되는 HP와 (canAttack 건물 한정) 자동 공격을 가진다.
    // HP 0 → die: 심 제거(PlacementBridge.Remove) + GameObject 소멸 + 플로우필드 갱신.
    //
    // 이름 충돌 주의: 팩토리 심의 plain C# Building(전역 네임스페이스)과 구분하기 위해
    // Entities 네임스페이스에 있다. 심 건물 참조는 global::Building(Sim 프로퍼티).
    // 코어처럼 심 없이 씬에 직접 배치하는 건물은 Sim이 null이어도 된다.
    public class Building : Entity
    {
        [Header("Building Settings")]
        [Tooltip("맵 중앙의 코어인지 여부. 플로우필드에서 타워보다 우선하는 최종 목표가 된다.")]
        [SerializeField] private bool isCore;

        [Tooltip("공격 가능한 건물(타워)인지 여부. 켜면 사거리 내 몬스터를 자동 공격한다.")]
        [SerializeField] private bool canAttack;
        [SerializeField] private CombatComponent combat = new CombatComponent();
        [SerializeField] private SensorComponent sensor = new SensorComponent();

        // 이 엔티티가 대변하는 팩토리 심 건물(plain C#). PlacementBridge가 배치 시 연결한다.
        public global::Building Sim { get; set; }

        public bool IsCore => isCore;
        public override CombatComponent Combat => canAttack ? combat : null;
        public override SensorComponent Sensor => sensor;
        public override bool IsDead => base.IsDead || (Sim != null && Sim.IsRemoved);

        // 살아있는 건물 레지스트리 — 플로우필드 목표 수집과 몬스터의 사거리 검색용
        private static readonly List<Building> all = new List<Building>();
        public static IReadOnlyList<Building> All => all;

        protected override void Awake()
        {
            base.Awake();
            sensor.Initialize(this);
        }

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

        protected override void Update()
        {
            base.Update();
            if (!canAttack || IsDead) return;

            // 쿨다운이 준비됐을 때만 스캔해 OverlapSphere 낭비를 줄인다 (타워 자동 공격)
            if (!combat.CanAttack()) return;
            Entity target = sensor.GetClosestTarget(combat.AttackRange);
            if (target.IsValidTarget())
            {
                combat.TryAttack(target);
            }
        }

        // HP 0 → 몬스터의 2초 사망 연출 없이 즉시 소멸
        protected override void HandleDeath()
        {
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
        // PlacementBridge가 모든 뷰에 BuildingView(: Entities.Building)를 붙이므로
        // 뷰가 있으면 항상 건물 엔티티를 얻을 수 있다.
        public static Building GetOrAttach(global::Building sim)
        {
            if (sim == null || sim.IsRemoved) return null;

            var boot = FactoryBootstrap.Instance;
            if (boot == null) return null;

            var view = boot.GetView(sim);
            if (view == null) return null; // 뷰 없는 심 전용 건물(테스트 등)은 공격 대상이 될 수 없음

            Building entity = view; // BuildingView : Entities.Building
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
