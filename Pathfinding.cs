// Delve - A roguelike computer RPG
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

delegate int TravelCostFunction(Tile tile);
delegate IEnumerable<(int, int)> AdjSqs(int r, int c);

// My implementation of Djisktra Maps, as defined at RogueBasin. Bsaically
// a flood fill that'll find the shortest paths from a given goal(s)
class DijkstraMap(Map map, Dictionary<(int, int), int> extraCosts, int height, int width, bool cardinalMovesOnly)
{
  public const int IMPASSABLE = 9999;
  Map Map { get; set; } = map;
  public int Height { get; init; } = height;
  public int Width { get; init; } = width;
  public readonly int[,] Sqrs = new int[height, width];
  Dictionary<(int, int), int> ExtraCosts { get; set; } = extraCosts;

  public GameState? GS { get; set; }
  public ulong DungeonId { get; set; }
  public ulong Level { get; set; }

  // For monster pathfinding we do 8-dir movement but in places where we're 
  // drawing roads or bridges we want to use 4-dir movement
  bool CardinalMovesOnly { get; set; } = cardinalMovesOnly;

  public static int Cost(Tile tile)
  {
    if (!tile.Passable())
      return int.MaxValue;

    // A monster will step on a magic mouth but will avoid them if reasonable
    if (tile.Type == TileType.MagicMouth)
      return 2;
      
    if (tile.IsVisibleTrap())
      return int.MaxValue;

    if (tile is JetTrigger trigger && trigger.Visible)
      return int.MaxValue;

    return 1;
  }

  public static int CostByFlight(Tile tile)
  {
    if (!tile.PassableByFlight())
      return int.MaxValue;

    // JetTriggers aren't triggered by flight but they aren't covered
    // in IsVisibleTrap(). I can't remember if that's intentional or an
    // an oversight...
    //
    // Monsters also won't necessarily avoid Magic Mouths
    TileType t = tile.Type;

    if (t == TileType.MagicMouth)
      return 2;

    if (tile.IsVisibleTrap() && !(t == TileType.Pit || t == TileType.TrapDoor))
      return int.MaxValue;

    return 1;
  }

  public static int CostWithDoors(Tile tile)
  {
    if (tile.Type == TileType.ClosedDoor)
      return 2;

    // A monster will step on a magic mouth but will avoid them if reasonable
    if (tile.Type == TileType.MagicMouth)
      return 2;
      
    if (!tile.Passable())
      return int.MaxValue;

    if (tile.IsVisibleTrap())
      return int.MaxValue;

    if (tile is JetTrigger trigger && trigger.Visible)
      return int.MaxValue;

    return 1;
  }

  public static int CostForSwimming(Tile tile) => tile.Type switch
  {
    TileType.Water or TileType.DeepWater or TileType.Underwater or TileType.Lake => 1,
    _ => int.MaxValue,
  };

  public static int CostForAmphibians(Tile tile)
  {
    if (tile.Passable())    
      return 1;
    else if (tile.IsWater())
      return 1;
    else
      return int.MaxValue;
  }

  public static TravelCostFunction WrapCostFunction(Dictionary<TileType, int> extraCosts)
  {
    return (tile) =>
    {
      if (extraCosts.TryGetValue(tile.Type, out int cost))
        return cost;

      return Cost(tile);
    };
  }

  // Passable defines the squares to be used in the pathfinding and their weight
  // (Ie., a floor might be passable with score 1 but a door is 2 because it's 
  // slightly more expensive)
  // I'm going to make life easy on myself for now and just work with a 
  // single goal.
  public void Generate(TravelCostFunction calcCost, (int Row, int Col) goal, int maxRange)
  {
    AdjSqs calcAdjSqs = CardinalMovesOnly ? Util.Adj4Sqs : Util.Adj8Sqs;

    for (int r = 0; r < Height; r++)
    {
      for (int c = 0; c < Width; c++)
      {
        Sqrs[r, c] = int.MaxValue;
      }
    }

    Sqrs[goal.Row, goal.Col] = 0;

    var q = new Queue<(int, int)>();
    foreach (var sq in calcAdjSqs(goal.Row, goal.Col))
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
      Tile tile = Map.TileAt(sq.Item1, sq.Item2);

      int cost = calcCost(tile);
      if (ExtraCosts.TryGetValue(sq, out int extraCost))
        cost += extraCost;
      if (cost > IMPASSABLE)
        continue;

      int cheapestNeighbour = int.MaxValue;
      foreach (var n in calcAdjSqs(sq.Item1, sq.Item2))
      {
        if (n.Item1 < 0 || n.Item2 < 0 || n.Item1 >= Height || n.Item2 >= Width)
          continue;
        if (Sqrs[n.Item1, n.Item2] < cheapestNeighbour)
          cheapestNeighbour = Sqrs[n.Item1, n.Item2];
        if (!visited.Contains(n))
          q.Enqueue(n);
      }
      Sqrs[sq.Item1, sq.Item2] = cheapestNeighbour + cost;
      visited.Add(sq);
    }
  }

  public List<(int, int)> ShortestPath(int row, int col)
  {
    if (Sqrs is null)
      throw new Exception("No dijkstra map found");

    AdjSqs calcAdjSqs = CardinalMovesOnly ? Util.Adj4Sqs : Util.Adj8Sqs;

    List<(int, int)> path = [(row, col)];
    int currRow = row;
    int currCol = col;

    if (currRow < 0 || currCol < 0)
    {
      return [];
    }

    int score = Sqrs[currRow, currCol];

    while (score != 0)
    {
      int cost = int.MaxValue;
      (int, int) next = (-1, -1);
      foreach (var adj in calcAdjSqs(currRow, currCol))
      {
        if (adj.Item1 < 0 || adj.Item2 < 0 || adj.Item1 >= Height || adj.Item2 >= Width)
          continue;
        if (Sqrs[adj.Item1, adj.Item2] < cost)
        {
          next = (adj.Item1, adj.Item2);
          cost = Sqrs[adj.Item1, adj.Item2];
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
    if (Sqrs is null)
      throw new Exception("No dijkstra map found");

    List<(int, int, int)> adj = [];

    foreach (var (nr, nc) in Util.Adj8Sqs(row, col))
    {
      if (Sqrs[nr, nc] < int.MaxValue)
        adj.Add((nr, nc, Sqrs[nr, nc]));
    }

    return [.. adj.OrderBy(v => v.Item3)];
  }
}

delegate bool AtGoalFunc(Loc loc);
delegate int HeuristicFunc(Loc a);
delegate IEnumerable<Loc> AdjancencyFunc(Loc a);

// Redblob is SUCH a treasure of a website
// https://www.redblobgames.com/pathfinding/a-star/introduction.html
class AStar
{
  static public Stack<Loc> AStarSearch(GameObjectDB objDb, Map map, Loc start, AtGoalFunc goalFunc, HeuristicFunc heuristic, TravelCostFunction calcCost, AdjancencyFunc adjLocs)
  {
    PriorityQueue<Loc, int> q = new();
    q.Enqueue(start, 0);
    Dictionary<Loc, Loc> cameFrom = [];
    cameFrom[start] = start;
    Dictionary<Loc, int> costs = [];
    costs[start] = 0;
    Loc goal = Loc.Nowhere;

    while (q.Count > 0)
    {
      Loc curr = q.Dequeue();

      if (goalFunc(curr))
      {
        goal = curr;
        break;
      }

      foreach (Loc adj in adjLocs(curr))
      {
        int travel = calcCost(map.TileAt(adj.Row, adj.Col));
        if (travel == int.MaxValue)
          continue;
        else if (objDb.AreBlockersAtLoc(adj))
          continue;
        else if (objDb.Occupied(adj))
          travel *= 2;

        int newCost = costs[curr] + travel;
        if (!costs.TryGetValue(adj, out int value) || newCost < value)
        {
          costs[adj] = newCost;
          int priority = newCost + heuristic(adj);
          q.Enqueue(adj, priority);
          cameFrom[adj] = curr;
        }
      }
    }

    if (!cameFrom.ContainsKey(goal))
      return [];

    Stack<Loc> path = [];
    Loc loc = goal;
    while (loc != start)
    {
      path.Push(loc);
      loc = cameFrom[loc];
    }

    return path;
  }

  public static Stack<Loc> FindPath(GameObjectDB objDb, Map map, Loc start, Loc goal, TravelCostFunction calcCost, bool allowDiagonal = true)
  {
    bool AtSingleGoal(Loc loc) => loc == goal;
    int Heuristic(Loc loc) => Util.Manhattan(loc, goal);
    AdjancencyFunc af = allowDiagonal ? Util.Adj8Locs : Util.Adj4Locs;

    return AStarSearch(objDb, map, start, AtSingleGoal, Heuristic, calcCost, af);
  }

  public static Stack<Loc> FindPathToArea(GameObjectDB objDb, Map map, Loc start, HashSet<Loc> goal, TravelCostFunction calcCost, bool allowDiagonal = true)
  {
    bool InGoalArea(Loc loc) => goal.Contains(loc);
    int Heuristic(Loc loc) => Util.Manhattan(start, loc);
    AdjancencyFunc af = allowDiagonal ? Util.Adj8Locs : Util.Adj4Locs;

    return AStarSearch(objDb, map, start, InGoalArea, Heuristic, calcCost, af);
  }
}