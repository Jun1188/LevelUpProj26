using UnityEngine;

public class DeadState : IEntityState
{
    public void Enter(StateMachineComponent stateMachine)
    {
        var movement = stateMachine.GetComponent<MovementComponent>();
        if (movement != null)
        {
            movement.StopMoving();
        }
    }

    public void Update(StateMachineComponent stateMachine) 
    {
    }

    public void Exit(StateMachineComponent stateMachine) 
    {
    }
}
