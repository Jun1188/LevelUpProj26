using UnityEngine;
using UnityEngine.Pool;

public class Bullet : MonoBehaviour
{
    private IObjectPool<GameObject> managedPool;
    private float speed;
    private float lifetime;

    public void SetPool(IObjectPool<GameObject> pool)
    {
        managedPool = pool;
    }

    public void Setup(float speed, float lifetime)
    {
        this.speed = speed;
        this.lifetime = lifetime;
        
        // 이전 예치된 Invoke 취소 후 재등록
        CancelInvoke(nameof(ReleaseToPool));
        Invoke(nameof(ReleaseToPool), lifetime);
    }

    private void Update()
    {
        // 전방으로 비행
        transform.Translate(Vector3.forward * speed * Time.deltaTime);
    }

    private void OnCollisionEnter(Collision collision)
    {
        // 무언가와 부딪히면 풀로 반환
        ReleaseToPool();
    }

    private void OnTriggerEnter(Collider other)
    {
        // 트리거 충돌 시에도 풀로 반환 (벽/적 판정용)
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