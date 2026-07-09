using UnityEngine;

public class IdleState : IEntityState
{
    public void Enter(StateMachineComponent stateMachine)
    {
        stateMachine.Movement?.StopMoving();
    }

    public void Update(StateMachineComponent stateMachine)
    {
        if (stateMachine.Sensor == null) return;

        IInteractable target = stateMachine.Sensor.GetClosestTarget();
        if (!target.IsValidTarget()) return;

        float distance = target.DistanceTo(stateMachine.transform.position);

        if (stateMachine.Combat != null && distance <= stateMachine.Combat.AttackRange)
        {
            stateMachine.SetState(new AttackState(target));
        }
        else
        {
            stateMachine.SetState(new ChaseState(target));
        }
    }

    public void Exit(StateMachineComponent stateMachine) {}
}
