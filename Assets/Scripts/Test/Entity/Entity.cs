using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;



// 공통 행동 인터페이스 정의
// 추적하고 데미지를 받을 수 있는 모든 객체가 공유
public interface IInteractable
{
    void TakeDamage(float damageAmount);
    void FindPath(IInteractable target);
    Vector3 GetPosition();
}


public class PathfindingState : IEntityState
{
    public void FindPath(Entity entity, IInteractable target)
    {
        if (target == null) return;

        // 분리된 PathFinder를 이용해 순수하게 '경로 데이터'만 가져옴
        List<Node> path = PathFinder.FindPath(entity.GetPosition(), target.GetPosition());

        if (path != null && path.Count > 0)
        {
            // 찾은 경로를 따라가도록 Entity에게 명령
            entity.StartMoving(path);
        }
        else
        {
            // 길을 못 찾았을 경우 다시 대기 상태로 복귀
            entity.SetState(new IdleState());
        }
    }
}



// 상태 패턴을 위한 인터페이스 정의
public interface IEntityState
{
    void FindPath(Entity entity, IInteractable target);
}

// 기본 대기 상태
public class IdleState : IEntityState
{
    public void FindPath(Entity entity, IInteractable target)
    {
        // 길찾기 명령을 받으면 Pathfinding 상태로 전환
        entity.SetState(new PathfindingState());
        entity.CurrentState.FindPath(entity, target);
    }
}

// 길찾기 상태








public abstract class Entity : MonoBehaviour, IInteractable //abstract class로 선언하여 직접 인스턴스화 방지, 상속 구현
{
    [Header("Entity Settings")]
    
    public float moveSpeed = 5f;

    // 길찾기 및 이동을 위한 변수
    protected List<Node> currentPath = null;
    protected Coroutine moveCoroutine;

    [SerializeField] protected float maxHealth = 100f;
    protected float currentHealth;

    // 외부에서는 get만 가능, 내부 및 상속 클래스에서만 set 가능한 프로퍼티
    public bool IsDead { get; protected set; }

    // UI 업데이트나 사운드 재생 등을 결합도를 낮춘 상태로 Action 델리게이트 사용
    public event Action<float, float> OnHealthChanged; // 현재 체력, 최대 체력
    public event Action OnDeath;

    // 현재 상태 관리를 위한 프로퍼티
    protected IEntityState currentState;
    public IEntityState CurrentState => currentState;


    [SerializeField] private IInteractable target = null;



    // 생명주기 메서드 virtual로 선언, 하위 클래스에서 확장 가능
    protected virtual void Awake()
    {
        Initialize();
    }

    // 초기화 메서드
    protected virtual void Initialize()
    {
        currentHealth = maxHealth;
        IsDead = false;
        
        // 초기 상태 설정
        SetState(new IdleState());
    }

    // 상태 전환 메서드
    public void SetState(IEntityState newState)
    {
        currentState = newState;
    }

    // 인터페이스 구현 및 로직 처리
    public virtual void TakeDamage(float damageAmount)
    {
        if (IsDead) return;

        // 데미지 적용 및 하한선 고정
        currentHealth = Mathf.Clamp(currentHealth - damageAmount, 0, maxHealth);

        // 체력 변경 이벤트 호출 (UI 업데이트 등에 활용)
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
    }





    // 상태 패턴을 위임하는 FindPath 인터페이스
    public virtual void FindPath(IInteractable target) 
    { 
        if(this.target != null) target = this.target;

        if (currentState != null)
        {
            currentState.FindPath(this, target);
        }
    }

    // 외부(또는 상태 클래스)에서 찾은 경로를 주입받아 이동을 시작하는 메서드
    public void StartMoving(List<Node> path)
    {
        currentPath = path;
        if (moveCoroutine != null) StopCoroutine(moveCoroutine);
        moveCoroutine = StartCoroutine(FollowPath());
    }
    // 실제 오브젝트를 움직이는 로직 (Entity의 고유 역할)
    protected virtual IEnumerator FollowPath()
    {
        if (currentPath == null || currentPath.Count == 0) yield break;
        int targetIndex = 0;
        Vector3 currentWaypoint = currentPath[0].worldPosition;
        currentWaypoint.y = transform.position.y; // Y축 높이 보정
        while (true)
        {
            Vector3 flatPosition = new Vector3(transform.position.x, 0, transform.position.z);
            Vector3 flatWaypoint = new Vector3(currentWaypoint.x, 0, currentWaypoint.z);
            if (Vector3.Distance(flatPosition, flatWaypoint) < 0.1f)
            {
                targetIndex++;
                if (targetIndex >= currentPath.Count)
                {
                    // 목적지 도착 완료
                    currentPath = null;
                    SetState(new IdleState());
                    yield break;
                }
                currentWaypoint = currentPath[targetIndex].worldPosition;
                currentWaypoint.y = transform.position.y;
            }
            transform.position = Vector3.MoveTowards(transform.position, currentWaypoint, moveSpeed * Time.deltaTime);
            yield return null;
        }
    }




    // 인터페이스에 정의된 자신의 위치 반환
    public Vector3 GetPosition()
    {
        return transform.position;
    }

    public virtual void Heal(float healAmount)
    {
        if (IsDead) return;

        currentHealth = Mathf.Clamp(currentHealth + healAmount, 0, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    // 사망 처리
    protected virtual void Die()
    {
        IsDead = true;
        OnDeath?.Invoke();

        // 기본적으로는 오브젝트를 비활성화하거나 파괴하지만,
        // 하위 클래스에서 오버라이드하여
        // 애니메이션 재생이나 오브젝트 풀링 반환 등으로 커스텀
        gameObject.SetActive(false);
    }

    protected virtual void Start()
    {
        // Initialize entity settings here
    }
    protected virtual void Update()
    {
        // Handle entity behavior here
    }
}
