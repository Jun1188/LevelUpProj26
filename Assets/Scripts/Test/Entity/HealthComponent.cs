using System;
using UnityEngine;

// 체력/사망 상태 — 순수 C# 클래스. 소유 Entity의 인스펙터에 인라인으로 직렬화된다.
// 사망 후 오브젝트 처리(비활성화/파괴)는 Entity.HandleDeath가 담당한다.
[Serializable]
public class HealthComponent
{
    [SerializeField] private float maxHealth = 100f;

    private float currentHealth;

    public bool IsDead { get; private set; }

    public event Action<float, float> OnHealthChanged;
    public event Action OnDeath;

    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;

    public void Initialize()
    {
        currentHealth = maxHealth;
        IsDead = false;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void TakeDamage(float damageAmount)
    {
        if (IsDead) return;

        currentHealth = Mathf.Clamp(currentHealth - damageAmount, 0, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void Heal(float healAmount)
    {
        if (IsDead) return;

        currentHealth = Mathf.Clamp(currentHealth + healAmount, 0, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private void Die()
    {
        IsDead = true;
        OnDeath?.Invoke();
    }
}
