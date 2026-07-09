using UnityEngine;
using System.Collections.Generic;

public class ChaseState : IEntityState
{
    private IInteractable target;
    private float pathUpdateInterval = 0.5f;
    private float lastPathUpdateTime;
    private float maxChaseRange = float.MaxValue;
    private StateMachineComponent owner;

    public ChaseState(IInteractable target)
    {
        this.target = target;
    }

    public void Enter(StateMachineComponent stateMachine)
    {
        owner = stateMachine;
        if (stateMachine.Sensor != null)
        {
            maxChaseRange = stateMachine.Sensor.DetectionRange * 1.5f;
        }
        if (stateMachine.Movement != null)
        {
            stateMachine.Movement.OnPathBlocked += HandlePathBlocked;
        }
        UpdatePath(stateMachine);
    }

    public void Update(StateMachineComponent stateMachine)
    {
        if (!target.IsValidTarget())
        {
            stateMachine.SetState(new IdleState());
            return;
        }

        float distance = target.DistanceTo(stateMachine.transform.position);

        if (stateMachine.Combat != null && distance <= stateMachine.Combat.AttackRange)
        {
            stateMachine.SetState(new AttackState(target));
            return;
        }

        // 감지 범위를 한참 벗어나면 추적 포기
        // 단, 길을 막는 건물 타겟은 감지 범위 밖에 있을 수 있으므로 거리로 포기하지 않는다
        if (!(target is BuildingDamageable) && distance > maxChaseRange)
        {
            stateMachine.SetState(new IdleState());
            return;
        }

        if (Time.time >= lastPathUpdateTime + pathUpdateInterval)
        {
            UpdatePath(stateMachine);
        }
    }

    public void Exit(StateMachineComponent stateMachine)
    {
        if (stateMachine.Movement != null)
        {
            stateMachine.Movement.OnPathBlocked -= HandlePathBlocked;
            stateMachine.Movement.StopMoving();
        }
    }

    private void HandlePathBlocked()
    {
        if (owner != null) UpdatePath(owner);
    }

    private void UpdatePath(StateMachineComponent stateMachine)
    {
        if (!target.IsValidTarget()) return;

        lastPathUpdateTime = Time.time;
        List<Node> path = PathFinder.FindPath(stateMachine.transform.position, target.GetPosition());

        if (path != null)
        {
            if (path.Count > 0)
            {
                stateMachine.Movement?.StartMoving(path);
            }
            else
            {
                // 시작 셀과 목표(보정) 셀이 같으면 빈 경로가 반환된다 — 막힘이 아니라 이미 도착.
                // 사거리 진입 여부는 Update의 거리 판정에 맡긴다
                stateMachine.Movement?.StopMoving();
            }
            return;
        }

        // 길이 완전히 막힘 → 경로를 막는 건물을 새 타겟으로 삼아 부순다
        // Building은 POCO이므로 그 뷰 GameObject에 피격 컴포넌트를 지연 부착해 타겟으로 쓴다
        Building blocker = PathFinder.FindBlockingBuilding(stateMachine.transform.position, target.GetPosition());
        BuildingDamageable damageable = BuildingDamageable.GetOrAttach(blocker);

        if (damageable != null && !ReferenceEquals(damageable, target))
        {
            stateMachine.SetState(new ChaseState(damageable));
            return;
        }

        // 부술 건물조차 없으면(지형 막힘 등) Chase에 머물며 pathUpdateInterval 주기로 재시도.
        // Idle로 보내면 Idle이 다음 프레임 바로 재추적을 시작해 매 프레임 A*가 도는 진동이 생긴다
        stateMachine.Movement?.StopMoving();
    }
}
