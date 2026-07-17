using System;
using System.Collections.Generic;
using UnityEngine;

// 이동 — 순수 C# 클래스. 소유 Entity가 매 프레임 Tick(dt)으로 구동한다.
// 두 가지 모드: 경로 추종(런타임 A*, StartMoving)과 방향 이동(플로우필드, SetDirection).
[Serializable]
public class MovementComponent
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotateSpeed = 720f; // 도/초 — 이동 방향으로 몸 돌리는 속도

    private Transform transform;
    private List<Node> currentPath;
    private int targetIndex;
    private Vector3 flowDirection; // 방향 이동 모드 (플로우필드)

    public bool IsMoving => (currentPath != null && currentPath.Count > 0) || flowDirection != Vector3.zero;
    public float MoveSpeed => moveSpeed;

    public event Action OnDestinationReached;
    public event Action OnPathBlocked;

    public void Initialize(Transform ownerTransform) => transform = ownerTransform;

    public void StartMoving(List<Node> path)
    {
        flowDirection = Vector3.zero;
        if (path == null || path.Count == 0)
        {
            currentPath = null;
            return;
        }
        currentPath = path;
        targetIndex = 0;
    }

    // 플로우필드 방향 이동. 매 프레임 갱신 호출을 전제로 한다 (zero면 정지).
    public void SetDirection(Vector3 direction)
    {
        currentPath = null;
        direction.y = 0f;
        flowDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
    }

    public void StopMoving()
    {
        currentPath = null;
        flowDirection = Vector3.zero;
    }

    public void Tick(float deltaTime)
    {
        if (transform == null) return;

        if (currentPath != null) TickPath(deltaTime);
        else if (flowDirection != Vector3.zero) TickDirection(deltaTime);
    }

    private void TickPath(float deltaTime)
    {
        // 이동 도중 다음 웨이포인트에 건물이 설치되면 즉시 멈추고 재탐색 요청
        if (GridManager.Instance != null && !GridManager.Instance.IsWalkable(currentPath[targetIndex]))
        {
            StopMoving();
            OnPathBlocked?.Invoke();
            return;
        }

        Vector3 waypoint = currentPath[targetIndex].worldPosition;
        waypoint.y = transform.position.y; // Y축 높이 보정

        Vector3 flatPosition = new Vector3(transform.position.x, 0, transform.position.z);
        Vector3 flatWaypoint = new Vector3(waypoint.x, 0, waypoint.z);

        if (Vector3.Distance(flatPosition, flatWaypoint) < 0.1f)
        {
            targetIndex++;
            if (targetIndex >= currentPath.Count)
            {
                // 목적지 도착 완료
                StopMoving();
                OnDestinationReached?.Invoke();
                return;
            }
            waypoint = currentPath[targetIndex].worldPosition;
            waypoint.y = transform.position.y;
        }

        Vector3 moveDir = waypoint - transform.position;
        transform.position = Vector3.MoveTowards(transform.position, waypoint, moveSpeed * deltaTime);
        Face(moveDir, deltaTime);
    }

    private void TickDirection(float deltaTime)
    {
        Vector3 next = transform.position + flowDirection * (moveSpeed * deltaTime);

        // 건물/장애물 셀로는 진입하지 않는다 (플로우필드가 목표 건물 셀을 가리킬 수 있음 —
        // 그 앞에서 멈추면 FlowFieldState의 사거리 판정이 공격으로 전환시킨다)
        if (GridManager.Instance != null)
        {
            Node nextNode = GridManager.Instance.NodeFromWorldPoint(next);
            if (nextNode == null || !GridManager.Instance.IsWalkable(nextNode))
            {
                Face(flowDirection, deltaTime);
                return;
            }
        }

        transform.position = next;
        Face(flowDirection, deltaTime);
    }

    private void Face(Vector3 direction, float deltaTime)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;
        Quaternion look = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, look, rotateSpeed * deltaTime);
    }
}
