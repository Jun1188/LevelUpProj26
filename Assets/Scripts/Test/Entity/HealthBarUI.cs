using UnityEngine;
using UnityEngine.UI;

public class HealthBarUI : MonoBehaviour
{
    [SerializeField] private HealthComponent healthComponent;
    [SerializeField] private Entity entity; // Fallback
    [SerializeField] private Image fillImage; // UI의 전경 이미지 (Image Type: Filled)

    private void Awake()
    {
        if (healthComponent == null)
        {
            if (entity == null)
                entity = GetComponentInParent<Entity>();
            
            if (entity != null)
                healthComponent = entity.GetComponent<HealthComponent>();
            else
                healthComponent = GetComponentInParent<HealthComponent>();
        }
    }

    private void OnEnable()
    {
        if (healthComponent != null)
        {
            healthComponent.OnHealthChanged += UpdateHealthBar;
        }
    }

    private void OnDisable()
    {
        if (healthComponent != null)
        {
            healthComponent.OnHealthChanged -= UpdateHealthBar;
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
