using UnityEngine;
using System.Collections.Generic;

public class ChaseState : IEntityState
{
    private IInteractable target;
    private float pathUpdateInterval = 0.5f;
    private float lastPathUpdateTime;

    public ChaseState(IInteractable target)
    {
        this.target = target;
    }

    public void Enter(StateMachineComponent stateMachine) 
    {
        UpdatePath(stateMachine);
    }

    public void Update(StateMachineComponent stateMachine) 
    {
        if (target == null)
        {
            stateMachine.SetState(new IdleState());
            return;
        }

        var combat = stateMachine.GetComponent<CombatComponent>();
        if (combat != null)
        {
            float distance = Vector3.Distance(stateMachine.transform.position, target.GetPosition());
            if (distance <= combat.AttackRange)
            {
                stateMachine.SetState(new AttackState());
                return;
            }
        }

        if (Time.time >= lastPathUpdateTime + pathUpdateInterval)
        {
            UpdatePath(stateMachine);
        }
    }

    public void Exit(StateMachineComponent stateMachine) 
    {
        var movement = stateMachine.GetComponent<MovementComponent>();
        if (movement != null)
        {
            movement.StopMoving();
        }
    }

    private void UpdatePath(StateMachineComponent stateMachine)
    {
        if (target == null) return;
        
        lastPathUpdateTime = Time.time;
        List<Node> path = PathFinder.FindPath(stateMachine.transform.position, target.GetPosition());
        var movement = stateMachine.GetComponent<MovementComponent>();
        
        if (movement != null && path != null && path.Count > 0)
        {
            movement.StartMoving(path);
        }
        else
        {
            stateMachine.SetState(new IdleState());
        }
    }
}
