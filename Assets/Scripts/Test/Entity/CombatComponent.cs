using System;
using UnityEngine;

// 공격 — 순수 C# 클래스. 쿨다운 관리와 데미지 전달만 담당한다.
[Serializable]
public class CombatComponent
{
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float attackCooldown = 2f;

    private float lastAttackTime = float.MinValue;

    public float AttackRange => attackRange;
    public float AttackCooldown => attackCooldown;

    public event Action OnAttackAction;

    public bool CanAttack() => Time.time >= lastAttackTime + attackCooldown;

    public void TryAttack(Entity target)
    {
        if (!target.IsValidTarget()) return;

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
