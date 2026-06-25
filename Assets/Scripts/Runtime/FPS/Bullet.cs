using UnityEngine;
using UnityEngine.Pool;

public class Bullet : MonoBehaviour
{
    private IObjectPool<GameObject> managedPool;
    public float lifeTime = 2f;

    public void SetPool(IObjectPool<GameObject> pool)
    {
        managedPool = pool;
    }

    private void OnEnable()
    {
        // 총알이 활성화되면 지정된 시간 후 반환하는 함수 예약
        Invoke(nameof(ReleaseToPool), lifeTime);
    }

    private void OnDisable()
    {
        // 비활성화 시 혹시 남아있을 Invoke 취소
        CancelInvoke();
    }

    private void OnCollisionEnter(Collision collision)
    {
        // 벽이나 적에 부딪히면 즉시 풀로 반환
        ReleaseToPool();
    }

    private void ReleaseToPool()
    {
        if (gameObject.activeSelf && managedPool != null)
        {
            managedPool.Release(gameObject);
        }
    }
}