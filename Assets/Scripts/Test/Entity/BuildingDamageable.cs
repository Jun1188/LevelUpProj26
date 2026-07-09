using UnityEngine;

// 건물을 몬스터의 공격 대상(IInteractable)으로 만드는 어댑터.
// 심 엔티티 Building은 plain C# 객체라 컴포넌트를 직접 붙일 수 없으므로,
// 그 씬 표현(BuildingView)의 GameObject에 이 컴포넌트를 지연 부착해 Building과 연결한다.
// Runtime/Factory는 수정하지 않고 Entity 쪽 호출 시점(GetOrAttach)에 연결하는 방식.
// 파괴 시 PlacementBridge.Remove(Building)로 심 제거/GridIndex 해제/뷰 파괴까지 일괄 처리.
public class BuildingDamageable : MonoBehaviour, IInteractable
{
    [SerializeField] private float maxHealth = 100f;
    private float currentHealth;

    // 이 피격체가 대변하는 심 엔티티
    public Building Building { get; private set; }

    public bool IsDead => currentHealth <= 0f || (Building != null && Building.IsRemoved);

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    // Building(POCO) → 뷰 GameObject를 찾아 피격 컴포넌트를 얻거나 새로 붙인다.
    // 뷰가 없는 심 전용 건물(테스트 등)은 공격 대상이 될 수 없어 null을 반환한다.
    public static BuildingDamageable GetOrAttach(Building building)
    {
        if (building == null || building.IsRemoved) return null;

        var boot = FactoryBootstrap.Instance;
        if (boot == null) return null;

        BuildingView view = boot.GetView(building);
        if (view == null) return null;

        var damageable = view.GetComponent<BuildingDamageable>();
        if (damageable == null) damageable = view.gameObject.AddComponent<BuildingDamageable>();
        damageable.Building = building;
        return damageable;
    }

    public void TakeDamage(float damageAmount)
    {
        if (IsDead) return;

        currentHealth -= damageAmount;
        if (currentHealth <= 0f)
        {
            if (Building != null && !Building.IsRemoved)
            {
                PlacementBridge.Remove(Building);
            }
            else
            {
                // Building 연결이 없는 예외적 상황(수동 부착 등)에서도 뷰는 정리한다
                Destroy(gameObject);
            }
        }
    }

    public Vector3 GetPosition()
    {
        return transform.position;
    }
}
