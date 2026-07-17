using UnityEngine;

[RequireComponent(typeof(Animator))]
public class EntityAnimator : MonoBehaviour
{
    [SerializeField] private Entity entity;
    private Animator animator;

    private readonly int isMovingHash = Animator.StringToHash("IsMoving");
    private readonly int attackHash = Animator.StringToHash("Attack");
    private readonly int dieHash = Animator.StringToHash("Die");

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (entity == null)
            entity = GetComponentInParent<Entity>();
    }

    private void OnEnable()
    {
        if (entity != null)
        {
            entity.OnAttackAction += PlayAttackAnimation;
            entity.OnDeath += PlayDeathAnimation;
        }
    }

    private void OnDisable()
    {
        if (entity != null)
        {
            entity.OnAttackAction -= PlayAttackAnimation;
            entity.OnDeath -= PlayDeathAnimation;
        }
    }

    private void Update()
    {
        if (entity == null || entity.IsDead) return;

        // 이동 컴포넌트(순수 C#)는 Entity가 노출한다 — 없는 엔티티(건물 등)는 항상 false
        bool isMoving = entity.Movement != null && entity.Movement.IsMoving;
        animator.SetBool(isMovingHash, isMoving);
    }

    private void PlayAttackAnimation()
    {
        animator.SetTrigger(attackHash);
    }

    private void PlayDeathAnimation()
    {
        animator.SetTrigger(dieHash);
    }
}
