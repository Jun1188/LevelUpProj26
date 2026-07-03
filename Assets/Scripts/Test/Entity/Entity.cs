using UnityEngine;
using System;
using System.Collections.Generic;

// 공통 행동 인터페이스 정의
// 추적하고 데미지를 받을 수 있는 모든 객체가 공유
public interface IInteractable
{
    void TakeDamage(float damageAmount);
    Vector3 GetPosition();
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
