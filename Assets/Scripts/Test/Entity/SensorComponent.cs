using UnityEngine;

public class SensorComponent : MonoBehaviour
{
    [SerializeField] private float detectionRange = 10f;
    [SerializeField] private LayerMask targetLayer;

    public float DetectionRange => detectionRange;

    // 지정된 반경 내에서 가장 가까운 IInteractable 대상을 찾는 메서드
    public IInteractable GetClosestTarget()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, detectionRange, targetLayer);
        IInteractable closest = null;
        float minDistance = float.MaxValue;

        foreach (var col in colliders)
        {
            IInteractable interactable = col.GetComponent<IInteractable>();
            
            // 나 자신은 대상에서 제외
            if (interactable != null && interactable != (IInteractable)GetComponent<IInteractable>())
            {
                // 대상이 Entity(이제는 HealthComponent로 확인)라면 사망 상태인지 확인
                var health = col.GetComponent<HealthComponent>();
                if (health != null && health.IsDead) continue;

                float dist = Vector3.Distance(transform.position, col.transform.position);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    closest = interactable;
                }
            }
        }
        return closest;
    }
}
