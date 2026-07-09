using UnityEngine;

public class StateMachineComponent : MonoBehaviour
{
    private IEntityState currentState;

    public IEntityState CurrentState => currentState;

    // 각 상태가 Enter마다 GetComponent를 반복하지 않도록 1회 캐싱
    public MovementComponent Movement { get; private set; }
    public CombatComponent Combat { get; private set; }
    public SensorComponent Sensor { get; private set; }

    private void Awake()
    {
        Movement = GetComponent<MovementComponent>();
        Combat = GetComponent<CombatComponent>();
        Sensor = GetComponent<SensorComponent>();
    }

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
