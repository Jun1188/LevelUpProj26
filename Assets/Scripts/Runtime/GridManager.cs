using UnityEngine;
using System.Collections.Generic;

// 길찾기를 위한 Node class
public class Node
{
    public bool walkable;         // 장애물이 없어 지나갈 수 있는지 여부
    public Vector3 worldPosition; // 유니티 월드 상의 실제 위치 좌표
    public int gridX;             // 그리드 배열의 X 인덱스
    public int gridY;             // 그리드 배열의 Y 인덱스

    public int gCost;             // 시작 지점부터 이 노드까지의 거리
    public int hCost;             // 목적지까지의 예상(휴리스틱) 거리
    public Node parent;           // 경로를 재구성하기 위한 부모 노드 추적

    public int FCost => gCost + hCost;

    public Node(bool _walkable, Vector3 _worldPos, int _gridX, int _gridY)
    {
        walkable = _walkable;
        worldPosition = _worldPos;
        gridX = _gridX;
        gridY = _gridY;
    }
}

public class GridManager : MonoBehaviour
{
    // A* 알고리즘 등 어디서든 접근할 수 있도록 싱글톤 패턴 적용
    public static GridManager Instance { get; private set; }

    [Header("Grid Settings")]
    public LayerMask unwalkableMask;   // 벽이나 장애물로 인식할 레이어 지정
    public Vector2 gridWorldSize;      // 전체 그리드가 덮을 월드 공간의 크기(가로, 세로)
    public float nodeRadius;           // 각 노드 하나가 차지할 반지름(크기)

    private Node[,] grid;              // 전체 맵을 담을 2차원 배열
    private float nodeDiameter;
    private int gridSizeX, gridSizeY;

    void Awake()
    {
        // 싱글톤 초기화
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // 노드의 지름을 계산하고 가로/세로에 노드가 몇 개 들어갈지 계산
        nodeDiameter = nodeRadius * 2;
        gridSizeX = Mathf.RoundToInt(gridWorldSize.x / nodeDiameter);
        gridSizeY = Mathf.RoundToInt(gridWorldSize.y / nodeDiameter);
        
        CreateGrid();
    }

    // 맵 생성: 씬이 시작될 때 한 번 물리 충돌체를 검사해 장애물을 판단
    void CreateGrid()
    {
        grid = new Node[gridSizeX, gridSizeY];
        
        // 그리드의 가장 왼쪽 아래 점(Bottom-Left)을 기준점으로 Y축 대신 Z축 기준)
        Vector3 worldBottomLeft = transform.position - Vector3.right * gridWorldSize.x / 2 - Vector3.forward * gridWorldSize.y / 2;

        for (int x = 0; x < gridSizeX; x++)
        {
            for (int y = 0; y < gridSizeY; y++)
            {
                // 각 그리드 칸의 실제 월드 좌표를 계산
                Vector3 worldPoint = worldBottomLeft + Vector3.right * (x * nodeDiameter + nodeRadius) + Vector3.forward * (y * nodeDiameter + nodeRadius);
                
                // 지정한 레이어(unwalkableMask)와 겹치는 물리 객체가 없는지 확인 (없으면 walkable = true)
                bool walkable = !(Physics.CheckSphere(worldPoint, nodeRadius, unwalkableMask));
                
                grid[x, y] = new Node(walkable, worldPoint, x, y);
            }
        }
    }

    // 특정 노드의 주변 8방향 이웃 노드들을 get
    public List<Node> GetNeighbours(Node node)
    {
        List<Node> neighbours = new List<Node>();

        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                // 자기 자신은 제외
                if (x == 0 && y == 0) continue;

                int checkX = node.gridX + x;
                int checkY = node.gridY + y;

                // 맵을 벗어나지 않는 안전한 범위 내에 있는지 체크
                if (checkX >= 0 && checkX < gridSizeX && checkY >= 0 && checkY < gridSizeY)
                {
                    neighbours.Add(grid[checkX, checkY]);
                }
            }
        }
        return neighbours;
    }

    // 목적지의 월드 좌표를 넣으면 어느 노드에 위치하는지 반환
    public Node NodeFromWorldPoint(Vector3 worldPosition)
    {
        // 기준 위치 대비 얼마나 떨어져 있는지 퍼센티지로 계산
        float percentX = (worldPosition.x + gridWorldSize.x / 2) / gridWorldSize.x;
        float percentY = (worldPosition.z + gridWorldSize.y / 2) / gridWorldSize.y;
        
        // 범위를 0 ~ 1 사이로 강제 고정하여 배열 인덱스 에러 방지
        percentX = Mathf.Clamp01(percentX);
        percentY = Mathf.Clamp01(percentY);

        int x = Mathf.RoundToInt((gridSizeX - 1) * percentX);
        int y = Mathf.RoundToInt((gridSizeY - 1) * percentY);
        
        return grid[x, y];
    }

    // 유니티 에디터 화면에 그리드 모양과 통행 가능 여부를 visualize
    void OnDrawGizmos()
    {
        Gizmos.DrawWireCube(transform.position, new Vector3(gridWorldSize.x, 1, gridWorldSize.y));

        if (grid != null)
        {
            foreach (Node n in grid)
            {
                // 이동 가능하면 흰색, 장애물이면 빨간색으로 visualize
                Gizmos.color = (n.walkable) ? new Color(1, 1, 1, 0.3f) : new Color(1, 0, 0, 0.3f);
                Gizmos.DrawCube(n.worldPosition, Vector3.one * (nodeDiameter - 0.1f));
            }
        }
    }
}
