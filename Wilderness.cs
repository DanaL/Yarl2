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

namespace Yarl2;

class Wilderness(Rng rng, int length)
{
  readonly Rng Rng = rng;
  readonly int Length = length;
  readonly int ConiferousAmount = rng.NextDouble() < 0.5 ? 10 : 30;

  int Fuzz() => Rng.Next(-50, 51);

  // Guess I should deal with the southern hemisphere if possible :P
  // And/or randomize the latitude of the dungeon if I want to be silly
  // I don't yet have winter trees
  public static TileType PickTree(Rng rng, int coniferChance)
  {
    if (rng.Next(100) < coniferChance)
      return TileType.Conifer;

    bool fallColours = false;
    int day = DateTime.UtcNow.DayOfYear;
    if (day > 320)
    {
      fallColours = true;
    }
    else if (day > 240)
    {
      int chance = int.Min(10, 365 - day);
      if (rng.Next(100) < chance)
        fallColours = true;
    }

    if (fallColours)
    {
      return rng.Next(3) switch
      {
        0 => TileType.OrangeTree,
        1 => TileType.RedTree,
        _ => TileType.YellowTree
      };
    }
    else
    {
      return TileType.GreenTree;
    }
  }

  void DrawARiver(Map map, (int, int) start)
  {
    int row = start.Item1;
    int col = start.Item2;
    var pts = new List<(int, int)>();

    do
    {
      int d = Rng.Next(2, 5);
      int columnBoop = Rng.Next(-5, 5);

      int nextRow = row - d;
      int nextCol = col + columnBoop;

      if (!map.InBounds(nextRow, nextCol))
        break;

      List<(int, int)> nextSegment = Util.LerpLine(row, col, nextRow, nextCol);
      bool riverCrossing = false;
      foreach (var pt in nextSegment)
      {
        pts.Add(pt);
        if (map.TileAt(pt).Type == TileType.DeepWater || map.TileAt(pt).Type == TileType.Water)
        {
          riverCrossing = true;
        }
      }

      if ((map.TileAt(nextRow, nextCol).Type == TileType.DeepWater || map.TileAt(nextRow, nextCol).Type == TileType.Water) && riverCrossing)
        break;

      row = nextRow;
      col = nextCol;

      // smooth river
      // bresenham draws lines that can look like:
      //     ~
      //   ~~
      //  ~@
      // I don't want those points where the player could walk
      // say NW and avoid stepping on the river
      List<(int, int)> extraPts = [];
      for (int j = 0; j < pts.Count - 1; j++)
      {
        var a = pts[j];
        var b = pts[j + 1];
        if (a.Item1 != b.Item1 && a.Item2 != b.Item2)
          extraPts.Add((a.Item1 - 1, a.Item2));

        map.SetTile(pts[j], TileFactory.Get(TileType.Water));
      }
      map.SetTile(pts.Last(), TileFactory.Get(TileType.Water));

      foreach (var pt in extraPts)
        map.SetTile(pt, TileFactory.Get(TileType.Water));
    }
    while (true);
  }

  List<(int, int)> FindRiverStarts(Map map, int colLo, int colHi)
  {
    List<(int, int)> candidates = [];
    int x = Length / 3;
    for (int r = Length - x; r < Length - 2; r++)
    {
      for (int c = colLo; c < colHi; c++)
      {
        int mountains = Util.CountAdjTileType(map, r, c, TileType.Mountain);
        if (mountains > 3)
          candidates.Add((r, c));
      }
    }

    return candidates;
  }

  // Try to draw up to three rivers on the map
  void DrawRivers(Map map)
  {
    List<int> opts = [0, 1, 2];
    opts.Shuffle(Rng);

    int third = Length / 3;

    foreach (int o in opts)
    {
      if (o == 0)
      {
        var startCandidates = FindRiverStarts(map, 2, third);
        if (startCandidates.Count > 0)
        {
          var startLoc = startCandidates[Rng.Next(startCandidates.Count)];
          DrawARiver(map, startLoc);
        }
      }
      else if (o == 1)
      {
        var startCandidates = FindRiverStarts(map, third, third * 2);
        if (startCandidates.Count > 0)
        {
          var startLoc = startCandidates[Rng.Next(startCandidates.Count)];
          DrawARiver(map, startLoc);
        }
      }
      else
      {
        var startCandidates = FindRiverStarts(map, third * 2, Length - 2);
        if (startCandidates.Count > 0)
        {
          var startLoc = startCandidates[Rng.Next(startCandidates.Count)];
          DrawARiver(map, startLoc);
        }
      }
    }
  }

  static (int, int) CountAdjTreesAndGrass(Map map, int r, int c)
  {
    int tree = 0;
    int grass = 0;

    foreach (var loc in Util.Adj8Sqs(r, c))
    {
      Tile tile = map.TileAt(loc);
      if (tile.IsTree())
        ++tree;
      else if (tile.Type == TileType.Grass)
        ++grass;
    }

    return (tree, grass);
  }

  Map CAizeTerrain(Map map, Rng rng)
  {
    var next = (Map)map.Clone();
    for (int r = 1; r < Length - 1; r++)
    {
      for (int c = 1; c < Length - 1; c++)
      {
        var (trees, _) = CountAdjTreesAndGrass(map, r, c);
        if (map.TileAt(r, c).Type == TileType.Grass && trees >= 5 && trees <= 8)
          next.SetTile(r, c, TileFactory.Get(PickTree(rng, ConiferousAmount)));
        else if (map.TileAt(r, c).IsTree() && trees < 4)
          next.SetTile(r, c, TileFactory.Get(TileType.Grass));
      }
    }

    return next;
  }

  // Run a sort of cellular automata ule over the trees
  // and grass to clump them together.
  // Two generations seems to make a nice mix .
  Map TweakTreesAndGrass(Map map, Rng rng)
  {
    map = CAizeTerrain(map, rng);
    map = CAizeTerrain(map, rng);

    return map;
  }

  // Average each point with its neighbours to smooth things out
  void SmoothGrid(int[,] grid)
  {
    for (int r = 0; r < Length; r++)
    {
      for (int c = 0; c < Length; c++)
      {
        int avg = grid[r, +c];
        int count = 1;

        if (r >= 1)
        {
          if (c >= 1)
          {
            avg += grid[(r - 1), +c - 1];
            count += 1;
          }
          avg += grid[(r - 1), +c];
          count += 1;
          if (c + 1 < Length)
          {
            avg += grid[(r - 1), +c + 1];
            count += 1;
          }
        }

        if (r > 1 && c >= 1)
        {
          avg += grid[(r - 1), c - 1];
          count += 1;
        }

        if (r > 1 && c + 1 < Length)
        {
          avg += grid[(r - 1), c + 1];
          count += 1;
        }

        if (r > 1 && r + 1 < Length)
        {
          if (c >= 1)
          {
            avg += grid[(r - 1), c - 1];
            count += 1;
          }
          avg += grid[(r - 1), c];
          count += 1;
          if (c + 1 < Length)
          {
            avg += grid[(r - 1), c + 1];
            count += 1;
          }
        }

        grid[r, c] = avg / count;
      }
    }
  }

  void DiamondStep(int[,] grid, int r, int c, int width)
  {
    int avg = (grid[r, c]
                    + grid[r, c + width - 1]
                    + grid[r + width - 1, c]
                    + grid[(r + width - 1), c + width - 1]) / 4;


    int f = Fuzz();
    grid[r + width / 2, +c + width / 2] = avg + f;
  }

  void DiamondAverage(int[,] grid, int r, int c, int width)
  {
    int count = 0;
    double avg = 0.0;

    if (width <= c)
    {
      avg += grid[r, +c - width];
      count += 1;
    }
    if (c + width < Length)
    {
      avg += grid[r, c + width];
      count += 1;
    }
    if (width <= r)
    {
      avg += grid[(r - width), +c];
      count += 1;
    }
    if (r + width < Length)
    {
      avg += grid[r + width, c];
      count += 1;
    }

    grid[r, c] = (int)(avg / count) + Fuzz();
  }

  void SquareStep(int[,] grid, int r, int c, int width)
  {
    var halfWidth = width / 2;

    DiamondAverage(grid, r - halfWidth, c, halfWidth);
    DiamondAverage(grid, r + halfWidth, c, halfWidth);
    DiamondAverage(grid, r, c - halfWidth, halfWidth);
    DiamondAverage(grid, r, c + halfWidth, halfWidth);
  }

  void MidpointDisplacement(int[,] grid, int r, int c, int width)
  {
    DiamondStep(grid, r, c, width);
    var halfWidth = width / 2;
    SquareStep(grid, r + halfWidth, c + halfWidth, width);

    if (halfWidth == 1)
      return;

    MidpointDisplacement(grid, r, c, halfWidth + 1);
    MidpointDisplacement(grid, r, c + halfWidth, halfWidth + 1);
    MidpointDisplacement(grid, r + halfWidth, c, halfWidth + 1);
    MidpointDisplacement(grid, r + halfWidth, c + halfWidth, halfWidth + 1);
  }

  Map ToMap(int[,] grid)
  {
    var map = new Map(Length, Length);

    for (int r = 0; r < Length; r++)
    {
      for (int c = 0; c < Length; c++)
      {
        var v = grid[r, c];
        TileType tt;
        if (v < 25)
        {
          tt = TileType.DeepWater;
        }
        else if (v < 40)
        {
          tt = TileType.Sand;
        }
        else if (v < 165)
        {
          tt = v % 2 == 0 ? tt = TileType.Grass : PickTree(Rng, ConiferousAmount);
        }
        else if (Rng.NextDouble() < 0.9)
        {
          tt = TileType.Mountain;
        }
        else
        {
          tt = TileType.Mountain;
        }

        map.SetTile(r, c, TileFactory.Get(tt));
      }
    }
    return map;
  }

  static void SetBorderingWater(Map map, int length)
  {
    int center = length / 2;
    int radius = center - 1;

    for (int r = 0; r < length; r++)
    {
      for (int c = 0; c < length; c++)
      {
        if (Util.Distance(r, c, center, center) > radius)
          map.SetTile(r, c, TileFactory.Get(TileType.DeepWater));
      }
    }
  }

  public Map DrawLevel()
  {
    int[,] grid = new int[Length, Length];

    if (Rng.NextDouble() < 0.5)
    {
      grid[0, 0] = Rng.Next(-10, 25);
      grid[0, Length - 1] = Rng.Next(0, 100);
    }
    else
    {
      grid[0, Length - 1] = Rng.Next(-10, 25);
      grid[0, 0] = Rng.Next(0, 100);
    }
    grid[Length - 1, 0] = Rng.Next(250, 300);
    grid[Length - 1, Length - 1] = Rng.Next(200, 350);

    MidpointDisplacement(grid, 0, 0, Length);
    SmoothGrid(grid);

    var map = ToMap(grid);
    map = TweakTreesAndGrass(map, Rng);

    DrawRivers(map);

    // I want the outer perimeter to be deep water/ocean
    SetBorderingWater(map, Length);

    // set the border around the world
    for (int c = 0; c < Length; c++)
    {
      map.SetTile(0, c, TileFactory.Get(TileType.WorldBorder));
      map.SetTile(Length - 1, c, TileFactory.Get(TileType.WorldBorder));
    }
    for (int r = 1; r < Length - 1; r++)
    {
      map.SetTile(r, 0, TileFactory.Get(TileType.WorldBorder));
      map.SetTile(r, Length - 1, TileFactory.Get(TileType.WorldBorder));
    }

    int rotation = Rng.Next(4);
    Map rotatedMap = new(Length, Length);
    for (int r = 0; r < Length; r++)
    {
      for (int c = 0; c < Length; c++)
      {
        var (rotatedR, rotatedC) = rotation switch
        {
          0 => (r, c),
          1 => (c, Length - 1 - r),
          2 => (Length - 1 - r, Length - 1 - c),
          _ => (Length - 1 - c, r)
        };
        rotatedMap.SetTile(rotatedR, rotatedC, map.TileAt(r, c));
      }
    }
    
    return rotatedMap;
  }

  static List<(int, int)> AddMontainRange(Map map, Town town, Rng rng)
  {
    // Find candidate spots for the mountain range
    List<(int, int)> candidates = [];
    for (int r = 3; r < map.Height - 10; r++)
    {
      for (int c = 3; c < map.Width - 10; c++)
      {
        if (ValidSpot(r, c))
          candidates.Add((r, c));
      }
    }

    bool[,] template = {
      { false, false, false, false, false, false, false, false, false, false},
      { false, false, false, false, false, false, false, false, false, false},
      { false, true, true, true, true, true, true, true, true, false},
      { false, true, true, true, true, true, true, true, true, false},
      { false, true, true, true, false, false, true, true, true, false},
      { false, true, true, true, false, false, true, true, true, false},
      { false, true, true, true, true, true, true, true, true, false},
      { false, true, true, true, true, true, true, true, true, false},
      { false, false, false, false, false, false, false, false, false, false},
      { false, false, false, false, false, false, false, false, false, false},
    };

    for (int c = 0; c < 10; c++)
    {
      if (rng.NextDouble() < 0.25)
        template[0, c] = true;
      if (rng.NextDouble() < 0.25)
        template[1, c] = true;
      if (rng.NextDouble() < 0.25)
        template[8, c] = true;
      if (rng.NextDouble() < 0.25)
        template[9, c] = true;
    }
    for (int r = 2; r <= 7; r++)
    {
      if (rng.NextDouble() < 0.25)
        template[r, 0] = true;
      if (rng.NextDouble() < 0.25)
        template[r, 9] = true;
    }

    (int row, int col) = candidates[rng.Next(candidates.Count)];
    for (int r = 0; r < 10; r++)
    {
      for (int c = 0; c < 10; c++)
      {
        if (template[r, c])
        {
          map.SetTile(row + r, col + c, TileFactory.Get(TileType.Mountain));
        }
      }
    }

    List<(int, int)> valley = [ (row + 4, col + 4), (row + 4, col + 5), (row + 5, col + 4), (row + 5, col + 5)];
    
    return valley;

    bool ValidSpot(int row, int col)
    {
      for (int r = 0; r < 8; r++)
      {
        for (int c = 0; c < 8; c++)
        {
          Loc loc = new(0, 0, row + r, col + c);
          if (town.WitchesYard.Contains(loc) || town.WitchesGarden.Contains(loc))
            return false;
          if (town.InTown(loc))
            return false;

          Tile tile = map.TileAt(loc.Row, loc.Col);
          switch (tile.Type)
          {
            case TileType.StoneRoad:
            case TileType.Portcullis:
            case TileType.StoneWall:
            case TileType.Dirt:
            case TileType.DeepWater:
              return false;
          }
        }
      }
      return true;
    }
  }

  static List<(int, int)> CreateValleyInRange(Map map, HashSet<(int, int)> range)
  {
    foreach (var sq in range)
    {
      if (OnlyMountainsAdj(sq.Item1, sq.Item2))
      {
        List<(int, int)> valley = [sq];
        foreach (var adj in Util.Adj4Sqs(sq.Item1, sq.Item2))
        {
          if (OnlyMountainsAdj(adj.Item1, adj.Item2))
            valley.Add(adj);
        }

        return valley;
      }
    }

    return [];

    bool OnlyMountainsAdj(int r, int c)
    {
      foreach (var sq in Util.Adj8Sqs(r, c))
      {
        TileType tt = map.TileAt(sq.Item1, sq.Item2).Type;
        if (!(tt == TileType.Mountain || tt == TileType.SnowPeak))
          return false;
      }

      return true;
    }
  }

  static List<(int, int)> FixMountainsForValley(Map map, Town town, Rng rng)
  {
    ConfigurablePassable passable = new();
    passable.Passable.Add(TileType.Mountain);
    passable.Passable.Add(TileType.SnowPeak);

    RegionFinder regionFinder = new(passable);
    Dictionary<int, HashSet<(int, int)>> regions = regionFinder.Find(map, false, 0, TileType.Unknown);
    List<HashSet<(int, int)>> mountainous = [.. regions.Values.Where(r => r.Count > 25)];

    if (mountainous.Count == 0)
    {
      return AddMontainRange(map, town, rng);
    }

    foreach (HashSet<(int, int)> range in mountainous)
    {
      List<(int, int)> valley = CreateValleyInRange(map, mountainous[0]);
      if (valley.Count > 0)
        return valley;
    }

    map.Dump();
    throw new WildernessCreationException("No valleys at all!");
  }

  static int CostsToCarveValley(Tile tile) => tile.Type switch
  {
    TileType.Grass => 2,
    TileType.Sand => 2,
    TileType.Water => 6,
    TileType.GreenTree => 2,
    TileType.YellowTree => 2,
    TileType.RedTree => 2,
    TileType.OrangeTree => 2,
    TileType.Conifer => 2,
    TileType.Dirt => 2,
    TileType.ClosedDoor => 3,
    TileType.Bridge => 3,
    TileType.Mountain => 5,
    TileType.SnowPeak => 5,
    _ => int.MaxValue
  };

  public static void CarveBurriedValley(Map map, HashSet<(int, int)>[] regions, HashSet<(int, int)> mainRegion, Town town, GameObjectDB objDb, FactDb factDb, Rng rng)
  {
    List<HashSet<(int, int)>> valleys = [.. regions.Where(r => IsValley(r))];

    List<(int, int)> valley;

    if (valleys.Count == 0)
    {
      // The wilderness wasn't generated with any pocket valleys. 
      valley = FixMountainsForValley(map, town, rng);
    }
    else
    {
      valley = [.. valleys[rng.Next(valleys.Count)]];
    }
    (int sr, int sc) = valley[rng.Next(valley.Count)];
    factDb.Add(new LocationFact() { Desc = "RLEntrance", Loc = new(0, 0, sr, sc) });

    Loc start = new(0, 0, sr, sc);
    // Pick a target loc in town
    List<Loc> townSqs = [];
    foreach (var sq in town.TownSquare)
    {
      switch (map.TileAt(sq.Row, sq.Col).Type)
      {
        case TileType.Grass:
        case TileType.Dirt:
        case TileType.Well:
          townSqs.Add(sq);
          break;
      }
    }
    Loc goal = townSqs[rng.Next(townSqs.Count)];

    List<Loc> carved = [];
    Stack<Loc> path = AStar.FindPath(objDb, map, start, goal, CostsToCarveValley, false);
    while (path.Count > 0)
    {
      Loc loc = path.Pop();
      if (valley.Contains((loc.Row, loc.Col)))
        continue;

      Tile tile = map.TileAt(loc.Row, loc.Col);
      if (tile.Type == TileType.Water)
      {
        continue;
      }
      else if (tile.Type == TileType.Mountain || tile.Type == TileType.SnowPeak)
      {
        map.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.Dirt));
        carved.Add(loc);
      }
      else if (mainRegion.Contains((loc.Row, loc.Col)))
      {
        break;
      }
    }

    for (int j = 0; j < int.Min(3, carved.Count); j++)
    {
      Item rubble = ItemFactory.Get(ItemNames.RUBBLE, objDb);
      objDb.SetToLoc(carved[j], rubble);
    }

    if (carved.Count > 6)
    {
      List<int> indexes = [.. Enumerable.Range(3, carved.Count - 3)];
      indexes.Shuffle(rng);

      int numOfRubble = int.Min(rng.Next(indexes.Count), 6);
      for (int j = 0; j < numOfRubble; j++)
      {
        Loc loc = carved[indexes[j]];
        Item rubble = ItemFactory.Get(ItemNames.RUBBLE, objDb);
        objDb.SetToLoc(loc, rubble);
      }
    }
    
    // Valleys can be bordered by deep water, but we need to have at least
    // one mountain
    bool IsValley(HashSet<(int, int)> potential)
    {
      bool mountain = false;
      foreach ((int r, int c) in potential)
      {
        foreach (var sq in Util.Adj8Sqs(r, c))
        {
          if (potential.Contains(sq))
            continue;

          TileType tile = map.TileAt(sq).Type;
          switch (tile)
          {
            case TileType.Mountain:
            case TileType.SnowPeak:
              mountain = true;
              break;
            case TileType.DeepWater:
              break;
            default:
              return false;
          }
        }
      }

      return mountain;
    }
  }

  public static Loc PlaceStoneRing(Map map, Town town, GameObjectDB objDb, FactDb factDb, Rng rng)
  {
    // We need a 7 x 7 spot that is not in town (or witch's cottage) and all 
    // passable tiles and I think no water tiles.
    List<(int, int)> options = [];
    for (int r = 5; r < map.Height - 7; r++)
    {
      for (int c = 5; c < map.Width - 7; c++)
      {
        if (IsValidSpotForRing(r, c))
        {
          options.Add((r, c));
        }
      }
    }

    (int row, int col) = options[rng.Next(options.Count)];

    for (int r = 0; r < 7; r++)
    {
      for (int c = 0; c < 7; c++)
      {
        if (rng.NextDouble() < 0.6)
          map.SetTile(row + r, col + c, TileFactory.Get(TileType.StoneRoad));
      }
    }

    SetColumn(row, col + 3);
    SetColumn(row + 1, col + 1);
    SetColumn(row + 1, col + 5);
    SetColumn(row + 3, col);
    SetColumn(row + 3, col + 6);
    SetColumn(row + 5, col + 1);
    SetColumn(row + 5, col + 5);
    SetColumn(row + 6, col + 3);
    map.SetTile(row + 3, col + 3, TileFactory.Get(TileType.StoneFloor));
    Loc centreLoc = new(0, 0, row + 3, col + 3);
    factDb.Add(new LocationFact() { Desc = "Stone ring centre", Loc = centreLoc });

    return centreLoc;
    
    void SetColumn(int row, int col)
    {
      Item column = ItemFactory.Get(ItemNames.COLUMN, objDb);
      column.Traits.Add(new DescriptionTrait("an ancient, weathered column"));
      objDb.SetToLoc(new Loc(0, 0, row, col), column);
    }

    bool IsValidSpotForRing(int row, int col)
    {
      for (int r = 0; r < 7; r++)
      {
        for (int c = 0; c < 7; c++)
        {
          Tile tile = map.TileAt(row + r, col + c);
          if (!tile.Passable())
            return false;
          if (tile.Type == TileType.DeepWater || tile.Type == TileType.Water)
            return false;

          Loc loc = new(0, 0, row + r, col + c);
          if (town.WitchesYard.Contains(loc) || town.WitchesGarden.Contains(loc))
            return false;

          if (town.InTown(loc))
            return false;
        }
      }

      return true;
    }
  }
}