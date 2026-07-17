using UnityEngine;
using System;

// Entity 확장 유틸 — 타겟 유효성/거리 판정 (구 IInteractable 확장의 Entity 버전)
public static class EntityExtensions
{
    // 순수 null 체크만으로는 Destroy된 MonoBehaviour(가짜 null)를 걸러내지 못하므로
    // UnityEngine.Object의 == 오버로드를 활용. SetActive(false)로 내려간 엔티티도 무효 처리.
    public static bool IsValidTarget(this Entity target)
    {
        if (target == null) return false;
        if (!target.gameObject.activeInHierarchy) return false;
        return !target.IsDead;
    }

    // 멀티타일 건물처럼 부피가 있는 대상은 중심점 대신 콜라이더 표면까지의 거리를 사용
    public static float DistanceTo(this Entity target, Vector3 from)
    {
        var col = target.GetComponentInChildren<Collider>();
        if (col != null) return Vector3.Distance(from, col.ClosestPoint(from));
        return Vector3.Distance(from, target.GetPosition());
    }
}

// 모든 게임 개체(몬스터/플레이어/건물)의 공통 베이스.
// HP/피격/사망은 전 엔티티 공통이고, 이동·전투·감지 컴포넌트(순수 C#)는
// 하위 클래스가 보유한 것만 virtual 프로퍼티로 노출한다.
public class Entity : MonoBehaviour
{
    [Header("Entity Settings (Compatibility)")]
    [Tooltip("PlayerController 등 기존 코드 호환용. 몬스터 이동 속도는 MovementComponent 쪽 값을 사용한다.")]
    public float moveSpeed = 5f;

    [SerializeField] private HealthComponent health = new HealthComponent();

    public HealthComponent Health => health;

    // 하위 클래스가 보유한 컴포넌트만 노출 (없으면 null)
    public virtual MovementComponent Movement => null;
    public virtual CombatComponent Combat => null;
    public virtual SensorComponent Sensor => null;

    public virtual bool IsDead => health.IsDead;

    public event Action<float, float> OnHealthChanged
    {
        add => health.OnHealthChanged += value;
        remove => health.OnHealthChanged -= value;
    }

    public event Action OnDeath
    {
        add => health.OnDeath += value;
        remove => health.OnDeath -= value;
    }

    public event Action OnAttackAction
    {
        add { if (Combat != null) Combat.OnAttackAction += value; }
        remove { if (Combat != null) Combat.OnAttackAction -= value; }
    }

    protected virtual void Awake()
    {
        health.Initialize();
        health.OnDeath += HandleDeath;
    }

    protected virtual void Start() { }

    protected virtual void Update() { }

    public virtual void TakeDamage(float damageAmount) => health.TakeDamage(damageAmount);

    public Vector3 GetPosition() => transform.position;

    // 기본 사망 처리: 연출(2초) 후 비활성화. 즉시 소멸이 필요한 엔티티(건물)는 override.
    protected virtual void HandleDeath()
    {
        Invoke(nameof(DeactivateEntity), 2f);
    }

    private void DeactivateEntity() => gameObject.SetActive(false);
}
