using UnityEngine;
using System;
using System.Collections.Generic;

// 공통 행동 인터페이스 정의
// 추적하고 데미지를 받을 수 있는 모든 객체가 공유
public interface IInteractable
{
    void TakeDamage(float damageAmount);
    Vector3 GetPosition();
    bool IsDead { get; }
}

public static class InteractableExtensions
{
    // 순수 null 체크만으로는 Destroy된 MonoBehaviour(가짜 null)를 걸러내지 못하므로
    // UnityEngine.Object 캐스팅 비교까지 포함한 유효성 검사
    public static bool IsValidTarget(this IInteractable target)
    {
        if (target == null) return false;
        if (target is UnityEngine.Object obj && obj == null) return false;
        return !target.IsDead;
    }

    // 멀티타일 건물처럼 부피가 있는 대상은 중심점 대신 콜라이더 표면까지의 거리를 사용
    public static float DistanceTo(this IInteractable target, Vector3 from)
    {
        if (target is Component c)
        {
            var col = c.GetComponentInChildren<Collider>();
            if (col != null) return Vector3.Distance(from, col.ClosestPoint(from));
        }
        return Vector3.Distance(from, target.GetPosition());
    }
}

public class Entity : MonoBehaviour, IInteractable 
{
    [Header("Entity Settings (Compatibility)")]
    public float moveSpeed = 5f;

    private HealthComponent healthComponent;
    private StateMachineComponent stateMachine;

    public bool IsDead => healthComponent != null && healthComponent.IsDead;
    
    public event Action<float, float> OnHealthChanged
    {
        add { if (healthComponent != null) healthComponent.OnHealthChanged += value; }
        remove { if (healthComponent != null) healthComponent.OnHealthChanged -= value; }
    }
    
    public event Action OnDeath
    {
        add { if (healthComponent != null) healthComponent.OnDeath += value; }
        remove { if (healthComponent != null) healthComponent.OnDeath -= value; }
    }

    public event Action OnAttackAction
    {
        add { var combat = GetComponent<CombatComponent>(); if (combat != null) combat.OnAttackAction += value; }
        remove { var combat = GetComponent<CombatComponent>(); if (combat != null) combat.OnAttackAction -= value; }
    }

    protected virtual void Awake()
    {
        healthComponent = GetComponent<HealthComponent>();
        stateMachine = GetComponent<StateMachineComponent>();

        if (healthComponent != null && stateMachine != null)
        {
            healthComponent.OnDeath += () => stateMachine.SetState(new DeadState());
        }
    }

    protected virtual void Start()
    {
        if (stateMachine != null)
        {
            stateMachine.SetState(new IdleState());
        }
    }

    protected virtual void Update()
    {
    }

    public void TakeDamage(float damageAmount)
    {
        if (healthComponent != null)
        {
            healthComponent.TakeDamage(damageAmount);
        }
    }

    public Vector3 GetPosition()
    {
        return transform.position;
    }

    public IEntityState CurrentState => stateMachine?.CurrentState;
}
