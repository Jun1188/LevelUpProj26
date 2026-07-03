using UnityEngine;

public class StateMachineComponent : MonoBehaviour
{
    private IEntityState currentState;

    public IEntityState CurrentState => currentState;

    public void SetState(IEntityState newState)
    {
        currentState?.Exit(this);
        currentState = newState;
        currentState?.Enter(this);
    }

    private void Update()
    {
        currentState?.Update(this);
    }
}
