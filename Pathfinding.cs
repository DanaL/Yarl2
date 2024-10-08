﻿// Yarl2 - A roguelike computer RPG
// Written in 2024 by Dana Larose <ywg.dana@gmail.com>
//
// To the extent possible under law, the author(s) have dedicated all copyright
// and related and neighboring rights to this software to the public domain
// worldwide. This software is distributed without any warranty.
//
// You should have received a copy of the CC0 Public Domain Dedication along 
// with this software. If not, 
// see <http://creativecommons.org/publicdomain/zero/1.0/>.

using Yarl2;

// My implementation of Djisktra Maps, as defined at RogueBasin. Bsaically
// a flood fill that'll find the shortest paths from a given goal(s)
class DjikstraMap(Map map, int height, int width)
{
  Map Map { get; set; } = map;
  int Height { get; set; } = height;
  int Width { get; set; } = width;
  int[,]? _djikstraMap { get; set; }

  // Passable defines the squares to be used in the pathfinding and their weight
  // (Ie., a floor might be passable with score 1 but a door is 2 because it's 
  // slightly more expensive)
  // I'm going to make life easy on myself for now and just work with a 
  // single goal.
  public void Generate(Dictionary<TileType, int> passable, (int Row, int Col) goal, int maxRange)
  {
    _djikstraMap = new int[Height, Width];

    for (int r = 0; r < Height; r++)
    {
      for (int c = 0; c < Width; c++)
      {
        _djikstraMap[r, c] = int.MaxValue;
      }
    }

    _djikstraMap[goal.Row, goal.Col] = 0;

    var q = new Queue<(int, int)>();
    foreach (var sq in Util.Adj4Sqs(goal.Row, goal.Col))
    {
      if (sq.Item1 >= 0 && sq.Item2 >= 0 && sq.Item1 < Height && sq.Item2 < Width)
        q.Enqueue(sq);
    }
    HashSet<(int, int)> visited = [(goal.Row, goal.Col)];

    while (q.Count > 0)
    {
      var sq = q.Dequeue();
      if (Util.Distance(sq.Item1, sq.Item2, goal.Row, goal.Col) >= maxRange)
      {
        visited.Add(sq);
        continue;
      }

      if (visited.Contains(sq))
        continue;
      var tile = Map.TileAt(sq.Item1, sq.Item2);

      if (!passable.TryGetValue(tile.Type, out int cost))
        continue;

      int cheapestNeighbour = int.MaxValue;
      foreach (var n in Util.Adj4Sqs(sq.Item1, sq.Item2))
      {
        if (n.Item1 < 0 || n.Item2 < 0 || n.Item1 >= Height || n.Item2 >= Width)
          continue;
        if (_djikstraMap[n.Item1, n.Item2] < cheapestNeighbour)
          cheapestNeighbour = _djikstraMap[n.Item1, n.Item2];
        if (!visited.Contains(n))
          q.Enqueue(n);
      }
      _djikstraMap[sq.Item1, sq.Item2] = cheapestNeighbour + cost;
      visited.Add(sq);
    }
  }

  public List<(int, int)> ShortestPath(int row, int col)
  {
    List<(int, int)> path = [(row, col)];
    int currRow = row;
    int currCol = col;

    if (currRow < 0 || currCol < 0)
    {
      return [];
    }

    int score = _djikstraMap[currRow, currCol];

    while (score != 0)
    {
      int cost = int.MaxValue;
      (int, int) next = (-1, -1);
      foreach (var adj in Util.Adj4Sqs(currRow, currCol))
      {
        if (adj.Item1 < 0 || adj.Item2 < 0 || adj.Item1 >= Height || adj.Item2 >= Width)
          continue;
        if (_djikstraMap[adj.Item1, adj.Item2] < cost)
        {
          next = (adj.Item1, adj.Item2);
          cost = _djikstraMap[adj.Item1, adj.Item2];
        }
      }

      if (cost == int.MaxValue)
        break;

      path.Add((next.Item1, next.Item2));
      score = cost;
      (currRow, currCol) = next;
    }

    return path;
  }

  public List<(int, int, int)> Neighbours(int row, int col)
  {
    List<(int, int, int)> adj = [];

    foreach (var (nr, nc) in Util.Adj8Sqs(row, col))
    {
      if (_djikstraMap[nr, nc] < int.MaxValue)
        adj.Add((nr, nc, _djikstraMap[nr, nc]));
    }

    return [.. adj.OrderBy(v => v.Item3)];
  }
}

// Redblob is SUCH a treasure of a website
// https://www.redblobgames.com/pathfinding/a-star/introduction.html
class AStar
{
  static public Stack<Loc> FindPath(Map map, Loc start, Loc goal, Dictionary<TileType, int> travelCost)
  {
    var q = new PriorityQueue<Loc, int>();
    q.Enqueue(start, 0);
    Dictionary<Loc, Loc> cameFrom = [];
    cameFrom[start] = start;
    Dictionary<Loc, int> costs = [];
    costs[start] = 0;

    while (q.Count > 0)
    {
      var curr = q.Dequeue();

      if (curr == goal)
        break;

      foreach (var adj in Util.Adj8Locs(curr))
      {
        var tileType = map.TileAt(adj.Row, adj.Col).Type;
        if (!travelCost.TryGetValue(tileType, out int travel))          
          continue;
        
        int newCost = costs[curr] + travel;
        if (!costs.TryGetValue(adj, out int value) || newCost < value)
        {
          costs[adj] = newCost;
          int priority = newCost + Util.Manhattan(goal, adj);
          q.Enqueue(adj, priority);
          cameFrom[adj] = curr;
        }
      }
    }

    Stack<Loc> path = [];
    Loc loc = goal;
    while (loc != start)
    {
      path.Push(loc);
      loc = cameFrom[loc];
    }

    return path;
  }
}