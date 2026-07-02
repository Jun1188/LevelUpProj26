using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public static class PathFinder
{
    // A* 알고리즘을 수행하고 결과 경로(List<Node>)를 반환
    public static List<Node> FindPath(Vector3 startPos, Vector3 targetPos)
    {
        if (GridManager.Instance == null)
        {
            Debug.LogWarning("GridManager.Instance is null. Please ensure a GridManager exists in the scene.");
            return null;
        }

        Node startNode = GridManager.Instance.NodeFromWorldPoint(startPos);
        Node targetNode = GridManager.Instance.NodeFromWorldPoint(targetPos);
        if (startNode == null || targetNode == null) return null;
        Dictionary<Node, pathNode> pathNodes = new Dictionary<Node, pathNode>();
        pathNode startPathNode = new pathNode(startNode) { gCost = 0 };
        pathNodes.Add(startNode, startPathNode);
        List<pathNode> openSet = new List<pathNode>();
        HashSet<Node> closedSet = new HashSet<Node>();
        openSet.Add(startPathNode);
        while (openSet.Count > 0)
        {
            pathNode currentPathNode = openSet[0];
            for (int i = 1; i < openSet.Count; i++)
            {
                if (openSet[i].FCost < currentPathNode.FCost ||
                   (openSet[i].FCost == currentPathNode.FCost && openSet[i].hCost < currentPathNode.hCost))
                {
                    currentPathNode = openSet[i];
                }
            }
            openSet.Remove(currentPathNode);
            closedSet.Add(currentPathNode.targetNode);
            // 목적지에 도착하면 경로를 역추적하여 리스트로 반환
            if (currentPathNode.targetNode == targetNode)
            {
                return RetracePath(startPathNode, currentPathNode);
            }
            foreach (Node neighbour in GridManager.Instance.GetNeighbours(currentPathNode.targetNode))
            {
                if (!neighbour.walkable || closedSet.Contains(neighbour)) continue;
                if (!pathNodes.TryGetValue(neighbour, out pathNode neighbourPathNode))
                {
                    neighbourPathNode = new pathNode(neighbour);
                    pathNodes.Add(neighbour, neighbourPathNode);
                }
                int newCostToNeighbour = currentPathNode.gCost + GetDistance(currentPathNode.targetNode, neighbour);
                if (newCostToNeighbour < neighbourPathNode.gCost || !openSet.Contains(neighbourPathNode))
                {
                    neighbourPathNode.gCost = newCostToNeighbour;
                    neighbourPathNode.hCost = GetDistance(neighbour, targetNode);
                    neighbourPathNode.parent = currentPathNode;
                    if (!openSet.Contains(neighbourPathNode))
                    {
                        openSet.Add(neighbourPathNode);
                    }
                }
            }
        }

        // 길을 찾지 못한 경우
        return null;
    }
    // 경로 역추적 메서드
    private static List<Node> RetracePath(pathNode startNode, pathNode endNode)
    {
        List<Node> path = new List<Node>();
        pathNode currentNode = endNode;
        while (currentNode != startNode)
        {
            path.Add(currentNode.targetNode);
            currentNode = currentNode.parent;
        }
        path.Reverse();
        return path;
    }
    // 거리 계산 메서드
    private static int GetDistance(Node nodeA, Node nodeB)
    {
        int dstX = Mathf.Abs(nodeA.gridCoord.x - nodeB.gridCoord.x);
        int dstY = Mathf.Abs(nodeA.gridCoord.y - nodeB.gridCoord.y);
        if (dstX > dstY) return 14 * dstY + 10 * (dstX - dstY);
        return 14 * dstX + 10 * (dstY - dstX);
    }
}