using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

public class MovementComponent : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;

    private List<Node> currentPath = null;
    private Coroutine moveCoroutine;

    // 이동 중인지 여부를 반환
    public bool IsMoving => currentPath != null && currentPath.Count > 0;

    public event Action OnDestinationReached;
    public event Action OnPathBlocked;

    public void StartMoving(List<Node> path)
    {
        currentPath = path;
        if (moveCoroutine != null) StopCoroutine(moveCoroutine);
        
        if (path != null && path.Count > 0)
        {
            moveCoroutine = StartCoroutine(FollowPath());
        }
    }

    public void StopMoving()
    {
        if (moveCoroutine != null)
        {
            StopCoroutine(moveCoroutine);
            moveCoroutine = null;
        }
        currentPath = null;
    }

    private IEnumerator FollowPath()
    {
        if (currentPath == null || currentPath.Count == 0) yield break;
        int targetIndex = 0;
        Vector3 currentWaypoint = currentPath[0].worldPosition;
        currentWaypoint.y = transform.position.y; // Y축 높이 보정

        while (true)
        {
            // 이동 도중 다음 웨이포인트에 건물이 설치되면 즉시 멈추고 재탐색 요청
            if (GridManager.Instance != null && !GridManager.Instance.IsWalkable(currentPath[targetIndex]))
            {
                StopMoving();
                OnPathBlocked?.Invoke();
                yield break;
            }

            Vector3 flatPosition = new Vector3(transform.position.x, 0, transform.position.z);
            Vector3 flatWaypoint = new Vector3(currentWaypoint.x, 0, currentWaypoint.z);

            if (Vector3.Distance(flatPosition, flatWaypoint) < 0.1f)
            {
                targetIndex++;
                if (targetIndex >= currentPath.Count)
                {
                    // 목적지 도착 완료
                    StopMoving();
                    OnDestinationReached?.Invoke();
                    yield break;
                }
                currentWaypoint = currentPath[targetIndex].worldPosition;
                currentWaypoint.y = transform.position.y;
            }
            
            transform.position = Vector3.MoveTowards(transform.position, currentWaypoint, moveSpeed * Time.deltaTime);
            yield return null;
        }
    }
}
