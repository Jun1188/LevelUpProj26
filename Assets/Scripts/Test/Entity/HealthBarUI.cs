using UnityEngine;
using UnityEngine.UI;

public class HealthBarUI : MonoBehaviour
{
    [SerializeField] private Entity entity;
    [SerializeField] private Image fillImage; // UI의 전경 이미지 (Image Type: Filled)

    private void Awake()
    {
        if (entity == null)
            entity = GetComponentInParent<Entity>();
    }

    private void OnEnable()
    {
        if (entity != null)
        {
            entity.OnHealthChanged += UpdateHealthBar;
            // 활성화 시점의 현재 체력 즉시 반영
            UpdateHealthBar(entity.Health.CurrentHealth, entity.Health.MaxHealth);
        }
    }

    private void OnDisable()
    {
        if (entity != null)
        {
            entity.OnHealthChanged -= UpdateHealthBar;
        }
    }

    private void UpdateHealthBar(float currentHealth, float maxHealth)
    {
        if (fillImage != null && maxHealth > 0)
        {
            fillImage.fillAmount = currentHealth / maxHealth;
        }
    }
}
