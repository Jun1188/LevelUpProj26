using UnityEngine;

public class IdleState : IEntityState
{
    private SensorComponent sensor;
    private CombatComponent combat;

    public void Enter(StateMachineComponent stateMachine)
    {
        sensor = stateMachine.GetComponent<SensorComponent>();
        combat = stateMachine.GetComponent<CombatComponent>();
        
        var movement = stateMachine.GetComponent<MovementComponent>();
        if (movement != null)
        {
            movement.StopMoving();
        }
    }

    public void Update(StateMachineComponent stateMachine)
    {
        if (sensor != null)
        {
            IInteractable target = sensor.GetClosestTarget();
            if (target != null)
            {
                float distance = Vector3.Distance(stateMachine.transform.position, target.GetPosition());
                
                if (combat != null && distance <= combat.AttackRange)
                {
                    stateMachine.SetState(new AttackState());
                }
                else
                {
                    stateMachine.SetState(new ChaseState(target));
                }
            }
        }
    }

    public void Exit(StateMachineComponent stateMachine) {}
}
