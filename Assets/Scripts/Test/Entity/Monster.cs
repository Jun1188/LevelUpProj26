using UnityEngine;

[RequireComponent(typeof(HealthComponent))]
[RequireComponent(typeof(MovementComponent))]
[RequireComponent(typeof(CombatComponent))]
[RequireComponent(typeof(SensorComponent))]
[RequireComponent(typeof(StateMachineComponent))]
public class Monster : Entity
{
    protected override void Awake()
    {
        base.Awake();
        // 초기 셋업 로직 (예: 스탯 초기화 등) 이 필요하면 여기에 작성
    }

    protected override void Start()
    {
        base.Start();
    }
}
