using System;
using System.Collections.Generic;
using UnityEngine;

// 和面包迷宫原型一样，每局生成一张带环路的 19×19 地图。
internal sealed class BreadMazeLayout
{
    internal const int Size = 19;
    internal const float CellSize = 1.5f;
    internal const int BreadCount = 6;
    internal static readonly Vector2Int Start = Vector2Int.one;
    internal static readonly Vector2Int Exit = new Vector2Int(Size - 2, Size - 2);
    private static readonly Vector2Int[] Directions =
        { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };

    private readonly bool[,] open = new bool[Size, Size];
    private readonly System.Random random;
    private readonly List<Vector2Int> corridors = new List<Vector2Int>();

    internal Vector2Int MonsterStart { get; }
    internal List<Vector2Int> BreadCells { get; }
    internal Vector2 InitialFacing { get; }

    internal BreadMazeLayout(int seed)
    {
        random = new System.Random(seed);
        CarveMaze();
        for (int y = 1; y < Size - 1; y++)
        {
            for (int x = 1; x < Size - 1; x++)
            {
                if (open[x, y])
                    corridors.Add(new Vector2Int(x, y));
            }
        }

        int[,] distance = DistancesFrom(Start);
        var farCells = corridors.FindAll(cell => distance[cell.x, cell.y] >= 24 && cell != Exit);
        MonsterStart = farCells[random.Next(farCells.Count)];
        InitialFacing = IsOpen(Start + Vector2Int.right) ? Vector2.right : Vector2.up;

        var candidates = corridors.FindAll(cell => cell != Start && cell != Exit && cell != MonsterStart);
        BreadCells = new List<Vector2Int>(BreadCount);
        for (int i = 0; i < BreadCount; i++)
        {
            int index = random.Next(candidates.Count);
            BreadCells.Add(candidates[index]);
            candidates.RemoveAt(index);
        }
    }

    internal static Vector2 Center(Vector2Int cell) => (Vector2)cell * CellSize;

    internal static Vector2Int CellAt(Vector2 position) =>
        new Vector2Int(Mathf.FloorToInt(position.x / CellSize + .5f), Mathf.FloorToInt(position.y / CellSize + .5f));

    internal bool IsOpen(Vector2Int cell) =>
        cell.x > 0 && cell.x < Size - 1 && cell.y > 0 && cell.y < Size - 1 && open[cell.x, cell.y];

    internal Vector2Int ChoosePatrolCell() => corridors[random.Next(corridors.Count)];

    // 广度优先生成距离图，供影子追逐和 HUD 的出口距离共用。
    internal int[,] DistancesFrom(Vector2Int origin)
    {
        var distances = new int[Size, Size];
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(origin);
        distances[origin.x, origin.y] = 1;

        while (queue.Count > 0)
        {
            Vector2Int cell = queue.Dequeue();
            foreach (Vector2Int direction in Directions)
            {
                Vector2Int next = cell + direction;
                if (IsOpen(next) && distances[next.x, next.y] == 0)
                {
                    distances[next.x, next.y] = distances[cell.x, cell.y] + 1;
                    queue.Enqueue(next);
                }
            }
        }

        return distances;
    }

    internal Vector2Int StepTowards(Vector2Int cell, int[,] distances)
    {
        int distance = distances[cell.x, cell.y];
        foreach (Vector2Int direction in Directions)
        {
            Vector2Int next = cell + direction;
            if (IsOpen(next) && distances[next.x, next.y] == distance - 1)
                return next;
        }
        return cell;
    }

    private void CarveMaze()
    {
        var path = new Stack<Vector2Int>();
        var candidates = new Vector2Int[Directions.Length];
        path.Push(Start);
        open[Start.x, Start.y] = true;

        while (path.Count > 0)
        {
            Vector2Int cell = path.Peek();
            int count = 0;
            foreach (Vector2Int direction in Directions)
            {
                Vector2Int next = cell + direction * 2;
                if (next.x > 0 && next.x < Size - 1 && next.y > 0 && next.y < Size - 1 && !IsOpen(next))
                    candidates[count++] = next;
            }

            if (count == 0)
            {
                path.Pop();
                continue;
            }

            Vector2Int room = candidates[random.Next(count)];
            Vector2Int passage = (room + cell) / 2;
            open[passage.x, passage.y] = open[room.x, room.y] = true;
            path.Push(room);
        }

        for (int i = 0; i < Size * Size / 4; i++)
        {
            int x = random.Next(1, Size - 1);
            int y = random.Next(1, Size - 1);
            bool across = open[x - 1, y] && open[x + 1, y] && !open[x, y - 1] && !open[x, y + 1];
            bool along = open[x, y - 1] && open[x, y + 1] && !open[x - 1, y] && !open[x + 1, y];
            if (across || along)
                open[x, y] = true;
        }
    }
}
