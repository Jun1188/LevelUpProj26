using UnityEngine;
using System.Collections.Generic;

// 길찾기를 위한 Node class

public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }
    [Header("Grid Settings")]
    public LayerMask unwalkableMask;
    public float cellSize = 1f;       // GridSystem의 CellSize와 동일한 역할
    public Vector2Int gridSize;       // 가로, 세로 셀 개수
    public Vector3 originPosition;    // 그리드의 시작점 (왼쪽 아래)
    private Node[,] grid;

    // GridSystem 로직을 래핑할 변수
    private GridSystem gridSystem;
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        // GridSystem 초기화
        gridSystem = new GridSystem(cellSize, originPosition);
        CreateGrid();
    }
    void CreateGrid()
    {
        grid = new Node[gridSize.x, gridSize.y];
        for (int x = 0; x < gridSize.x; x++)
        {
            for (int y = 0; y < gridSize.y; y++)
            {
                // GridSystem을 이용해 각 셀의 중앙 좌표를 쉽게 구함
                Vector2Int cell = new Vector2Int(x, y);
                Vector3 worldCenter = gridSystem.GridToWorldCenter(cell);

                // 장애물 체크 (반지름은 cellSize의 절반)
                bool walkable = !Physics.CheckSphere(worldCenter, cellSize * 0.5f, unwalkableMask);

                grid[x, y] = new Node(walkable, worldCenter, cell);
            }
        }
    }
    // GridSystem을 사용하여 월드 좌표를 노드로 변환
    public Node NodeFromWorldPoint(Vector3 worldPosition)
    {
        Vector2Int gridPos = gridSystem.WorldToGrid(worldPosition);

        // 배열 범위를 벗어나지 않도록 방어 코드
        if (gridPos.x >= 0 && gridPos.x < gridSize.x && gridPos.y >= 0 && gridPos.y < gridSize.y)
        {
            return grid[gridPos.x, gridPos.y];
        }
        return null; // 맵 밖일 경우
    }

    // GridManager.cs 내부에 추가할 메서드
    public List<Node> GetNeighbours(Node node)
    {
        List<Node> neighbours = new List<Node>();

        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0) continue;

                // 새로 바뀐 gridCoord를 사용
                int checkX = node.gridCoord.x + x;
                int checkY = node.gridCoord.y + y;

                if (checkX >= 0 && checkX < gridSize.x && checkY >= 0 && checkY < gridSize.y)
                {
                    neighbours.Add(grid[checkX, checkY]);
                }
            }
        }
        return neighbours;
    }


    // GetNeighbours 메서드 등은 그대로 유지하거나 gridPos.x, gridPos.y 대신 Vector2Int를 사용하도록 개선 가능
}
