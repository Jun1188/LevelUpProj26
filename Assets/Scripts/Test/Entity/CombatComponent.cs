using UnityEngine;
using System;

public class CombatComponent : MonoBehaviour
{
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float attackCooldown = 2f;

    public float AttackRange => attackRange;
    public float AttackCooldown => attackCooldown;

    private float lastAttackTime;

    public event Action OnAttackAction;

    public bool CanAttack()
    {
        return Time.time >= lastAttackTime + attackCooldown;
    }

    public void TryAttack(IInteractable target)
    {
        if (target == null) return;

        if (CanAttack())
        {
            lastAttackTime = Time.time;
            
            // 데미지 전달
            target.TakeDamage(attackDamage);
            
            // 이벤트 발생 (애니메이션, 사운드 등에서 구독)
            OnAttackAction?.Invoke();
        }
    }
}
