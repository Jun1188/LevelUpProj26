using UnityEngine;
using System;

public class HealthComponent : MonoBehaviour
{
    [SerializeField] private float maxHealth = 100f;
    private float currentHealth;

    public bool IsDead { get; private set; }

    public event Action<float, float> OnHealthChanged;
    public event Action OnDeath;

    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;

    private void Awake()
    {
        Initialize();
    }

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
        
        // 2초 지연 후 게임 오브젝트 비활성화 (기존 로직 유지)
        Invoke(nameof(DeactivateEntity), 2f);
    }

    private void DeactivateEntity()
    {
        gameObject.SetActive(false);
    }
}
