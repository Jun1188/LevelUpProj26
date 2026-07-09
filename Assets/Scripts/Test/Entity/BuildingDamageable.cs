using UnityEngine;

// 건물을 몬스터의 공격 대상(IInteractable)으로 만드는 컴포넌트.
// PlacementBridge.Place()에서 자동으로 부착된다.
// 파괴 시 PlacementBridge.Remove를 통해 GridRegistry 셀 해제/그래프 정리까지 일괄 처리.
public class BuildingDamageable : MonoBehaviour, IInteractable
{
    [SerializeField] private float maxHealth = 100f;
    private float currentHealth;

    public bool IsDead => currentHealth <= 0f;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float damageAmount)
    {
        if (IsDead) return;

        currentHealth -= damageAmount;
        if (currentHealth <= 0f)
        {
            var instance = GetComponent<BuildingInstance>();
            if (instance != null)
            {
                PlacementBridge.Remove(instance);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }

    public Vector3 GetPosition()
    {
        return transform.position;
    }
}
