using UnityEngine;

public class AttackState : IEntityState
{
    private float stateTimer;
    private float attackDuration;

    public void Enter(StateMachineComponent stateMachine)
    {
        stateTimer = 0f;
        
        var combat = stateMachine.GetComponent<CombatComponent>();
        if (combat != null)
        {
            attackDuration = combat.AttackCooldown;
            var sensor = stateMachine.GetComponent<SensorComponent>();
            if (sensor != null)
            {
                combat.TryAttack(sensor.GetClosestTarget());
            }
        }
        else
        {
            attackDuration = 1f;
        }

        var movement = stateMachine.GetComponent<MovementComponent>();
        if (movement != null)
        {
            movement.StopMoving();
        }
    }

    public void Update(StateMachineComponent stateMachine)
    {
        stateTimer += Time.deltaTime;
        
        if (stateTimer >= attackDuration)
        {
            stateMachine.SetState(new IdleState());
        }
    }

    public void Exit(StateMachineComponent stateMachine) 
    {
    }
}
