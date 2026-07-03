using UnityEngine;

[RequireComponent(typeof(Animator))]
public class EntityAnimator : MonoBehaviour
{
    [SerializeField] private Entity entity;
    private Animator animator;

    private readonly int isMovingHash = Animator.StringToHash("IsMoving");
    private readonly int attackHash = Animator.StringToHash("Attack");
    private readonly int dieHash = Animator.StringToHash("Die");

    private MovementComponent movementComponent;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (entity == null)
            entity = GetComponentInParent<Entity>();
            
        movementComponent = entity != null ? entity.GetComponent<MovementComponent>() : GetComponentInParent<MovementComponent>();
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

        // 이동 관련 상태일 경우에 걷기/뛰기 애니메이션 적용
        bool isMoving = movementComponent != null && movementComponent.IsMoving;
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
