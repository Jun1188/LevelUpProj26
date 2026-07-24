using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 발사체 — 데미지 전달의 주체.
/// 피격 판정은 맞은 쪽(Monster 등)이 아니라 총알이 수행한다:
/// 충돌 상대가 대상 레이어(targetMask)의 Entity면 TakeDamage(damage) 후 소멸.
/// 플레이어 총(ProjectileGun 풀)과 타워(BattleTower, 풀 없이 Instantiate) 모두 이 클래스를 쓴다.
/// </summary>
public class Bullet : MonoBehaviour
{
    private IObjectPool<GameObject> managedPool;
    private float speed;
    private float lifetime;
    private float damage;
    private int targetMask; // 데미지를 적용할 레이어 마스크 (0이면 데미지 없음, 소멸만)
    private float range;
    private Vector3 start;

    public void SetPool(IObjectPool<GameObject> pool)
    {
        managedPool = pool;
    }

    public void Setup(float speed, float lifetime, float damage = 0f, int targetMask = 0, float range = 10)
    {
        this.speed = speed;
        this.lifetime = lifetime;
        this.damage = damage;
        this.targetMask = targetMask;
        this.range = range;
        start = transform.position;

        // 이전 예치된 Invoke 취소 후 재등록
        CancelInvoke(nameof(ReleaseToPool));
        Invoke(nameof(ReleaseToPool), lifetime);
    }

    private void Update()
    {
        // 전방으로 비행
        transform.Translate(Vector3.forward * speed * Time.deltaTime);
        if(Vector3.Distance(start, transform.position) >= range)
        {
            ReleaseToPool();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryApplyDamage(collision.collider);
        ReleaseToPool();
    }

    private void OnTriggerEnter(Collider other)
    {
        TryApplyDamage(other);
        ReleaseToPool();
    }

    // 대상 레이어의 Entity에게만 데미지 적용 (콜라이더가 자식 모델에 있는 구조 지원)
    private void TryApplyDamage(Collider hit)
    {
        if (damage <= 0f || targetMask == 0) return;
        if ((targetMask & (1 << hit.gameObject.layer)) == 0) return;

        Entity entity = hit.GetComponentInParent<Entity>();
        if (entity != null && !entity.IsDead)
        {
            entity.TakeDamage(damage);
        }
    }

    private void ReleaseToPool()
    {
        if (!gameObject.activeSelf) return;

        if (managedPool != null) managedPool.Release(gameObject);
        else Destroy(gameObject); // 풀 없이 발사된 총알(타워 등)은 직접 소멸
    }
}
