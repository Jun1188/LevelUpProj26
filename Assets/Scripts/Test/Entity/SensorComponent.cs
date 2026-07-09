using UnityEngine;

public class SensorComponent : MonoBehaviour
{
    [SerializeField] private float detectionRange = 10f;
    [Tooltip("추적 대상이 위치한 레이어 이름. Awake에서 마스크로 변환된다. 타워디펜스에서 몬스터의 추적 대상은 플레이어뿐.")]
    [SerializeField] private string targetLayerName = "Player";
    [SerializeField] private float scanInterval = 0.2f; // 매 프레임 OverlapSphere 방지

    public float DetectionRange => detectionRange;

    private LayerMask targetLayer;
    private float lastScanTime = float.MinValue;
    private IInteractable cachedTarget;

    private void Awake()
    {
        // 레이어 마스크를 이름으로 해석 — 인스펙터 오설정 방지
        targetLayer = LayerMask.GetMask(targetLayerName);
        if (targetLayer == 0)
            Debug.LogWarning($"[SensorComponent] '{targetLayerName}' 레이어를 찾지 못했습니다. " +
                $"대상 감지가 동작하지 않습니다. Project Settings > Tags and Layers에서 레이어를 확인하세요.");
    }

    // 지정된 반경 내에서 가장 가까운 IInteractable 대상을 찾는 메서드
    public IInteractable GetClosestTarget()
    {
        if (Time.time < lastScanTime + scanInterval)
        {
            return cachedTarget.IsValidTarget() ? cachedTarget : null;
        }
        lastScanTime = Time.time;

        Collider[] colliders = Physics.OverlapSphere(transform.position, detectionRange, targetLayer);
        IInteractable self = GetComponent<IInteractable>();
        IInteractable closest = null;
        float minDistance = float.MaxValue;

        foreach (var col in colliders)
        {
            // 콜라이더가 자식 모델에 붙어 있는 프리팹 구조를 지원하기 위해 부모까지 탐색
            IInteractable interactable = col.GetComponentInParent<IInteractable>();

            // 나 자신과 죽은 대상(엔티티/건물 공통)은 제외
            if (interactable == null || interactable == self) continue;
            // 타워 디펜스: 몬스터는 서로를 추적/공격하지 않는다. 센서로는 플레이어만 획득하고,
            // 건물은 길이 막혔을 때 PathFinder.FindBlockingBuilding으로 별도 타겟이 된다.
            // (레이어 설정 실수로 다른 몬스터가 타겟 레이어에 올라와도 방어적으로 걸러낸다)
            if (interactable is Monster) continue;
            if (!interactable.IsValidTarget()) continue;

            float dist = Vector3.Distance(transform.position, col.transform.position);
            if (dist < minDistance)
            {
                minDistance = dist;
                closest = interactable;
            }
        }

        cachedTarget = closest;
        return closest;
    }
}
