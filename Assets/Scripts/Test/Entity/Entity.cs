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
public class PathfindingState : IEntityState
{
    public void FindPath(Entity entity, IInteractable target)
    {
        
        // 실제 A* 길찾기 호출
        entity.FindByAstar(target);
    }
}







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


    // A* 알고리즘 길찾기 제공 함수
    public virtual void FindByAstar(IInteractable target)
    {
        if (target == null) return;

        Vector3 startPos = GetPosition();
        Vector3 targetPos = target.GetPosition();

        //Debug.Log($"A* Algorithm: Calculating path from {startPos} to {targetPos}");

        
        Node startNode = GridManager.Instance.NodeFromWorldPoint(startPos);
        Node targetNode = GridManager.Instance.NodeFromWorldPoint(targetPos);

        List<Node> openSet = new List<Node>();
        HashSet<Node> closedSet = new HashSet<Node>();
        openSet.Add(startNode);

        while (openSet.Count > 0)
        {
            // 1. OpenList에서 F비용이 가장 낮은 노드를 찾음 (F = G + H)
            Node currentNode = openSet[0];
            for (int i = 1; i < openSet.Count; i++)
            {
                if (openSet[i].FCost < currentNode.FCost || 
                   (openSet[i].FCost == currentNode.FCost && openSet[i].hCost < currentNode.hCost))
                {
                    currentNode = openSet[i];
                }
            }

            openSet.Remove(currentNode);
            closedSet.Add(currentNode);

            // 2. 목적지에 도착했는지 확인
            if (currentNode == targetNode)
            {
                RetracePath(startNode, targetNode);
                return;
            }

            // 3. 인접한 이웃 노드들 탐색
            foreach (Node neighbour in GridManager.Instance.GetNeighbours(currentNode))
            {
                // 벽이거나 이미 탐색한 노드면 무시
                if (!neighbour.walkable || closedSet.Contains(neighbour))
                {
                    continue;
                }

                // 시작점에서 이웃 노드까지의 새로운 G비용 계산
                int newCostToNeighbour = currentNode.gCost + GetDistance(currentNode, neighbour);
                
                if (newCostToNeighbour < neighbour.gCost || !openSet.Contains(neighbour))
                {
                    neighbour.gCost = newCostToNeighbour;
                    neighbour.hCost = GetDistance(neighbour, targetNode);
                    neighbour.parent = currentNode;

                    if (!openSet.Contains(neighbour))
                    {
                        openSet.Add(neighbour);
                    }
                }
            }
        }
        
    }



    // 도착 후 부모 노드를 역추적하여 실제 경로를 리스트로 만드는 함수
    
    protected virtual void RetracePath(Node startNode, Node endNode)
    {
        List<Node> path = new List<Node>();
        Node currentNode = endNode;

        while (currentNode != startNode)
        {
            path.Add(currentNode);
            currentNode = currentNode.parent;
        }

        path.Reverse(); // 역추적했으므로 순서를 뒤집음
        
        currentPath = path;

        // 찾은 경로를 바탕으로 이동 코루틴 시작
        if (moveCoroutine != null)
        {
            StopCoroutine(moveCoroutine);
        }
        moveCoroutine = StartCoroutine(FollowPath());
    }




    // 찾은 경로를 따라 실제 오브젝트를 이동시키는 코루틴
    protected virtual IEnumerator FollowPath()
    {
        if (currentPath == null || currentPath.Count == 0)
            yield break;

        int targetIndex = 0;
        // 경로의 첫 번째 노드(다음 이동 지점)를 목표로 설정
        Vector3 currentWaypoint = currentPath[0].worldPosition;

        while (true)
        {
            // 현재 웨이포인트와의 거리가 충분히 가까워지면 다음 웨이포인트로 타겟 갱신
            if (Vector3.Distance(transform.position, currentWaypoint) < 0.1f)
            {
                targetIndex++;
                if (targetIndex >= currentPath.Count)
                {
                    // 최종 목적지에 도착함
                    currentPath = null;
                    // 도착 후 다시 대기 상태로 전환 (필요시 공격이나 다른 상태로 전환 가능)
                    SetState(new IdleState());
                    yield break;
                }
                currentWaypoint = currentPath[targetIndex].worldPosition;
            }

            // 등속으로 목표 지점을 향해 이동
            transform.position = Vector3.MoveTowards(transform.position, currentWaypoint, moveSpeed * Time.deltaTime);
            // 한 프레임 대기
            yield return null;
        }
    }

    // 휴리스틱 거리 계산 (대각선 이동 허용 시 통상적인 계산법)
    protected int GetDistance(Node nodeA, Node nodeB)
    {
        int dstX = Mathf.Abs(nodeA.gridX - nodeB.gridX);
        int dstY = Mathf.Abs(nodeA.gridY - nodeB.gridY);

        if (dstX > dstY)
            return 14 * dstY + 10 * (dstX - dstY);
        return 14 * dstX + 10 * (dstY - dstX);
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
