// Yarl2 - A roguelike computer RPG
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

internal class Wilderness(Rng rng, int length)
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

      var nextSegment = Util.Bresenham(row, col, nextRow, nextCol);
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
    var opts = new List<int>() { 0, 1, 2 };
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


    var f = Fuzz();
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

  static void Dump(Map map, int length, string filename)
  {
    using TextWriter tw = new StreamWriter(filename);
    for (int r = 0; r < length; r++)
    {
      for (int c = 0; c < length; c++)
      {
        var t = map.TileAt(r, c);
        char ch = t.Type switch
        {
          TileType.PermWall => '#',
          TileType.DungeonWall => '#',
          TileType.DungeonFloor or TileType.Sand => '.',
          TileType.ClosedDoor => '+',
          TileType.Mountain or TileType.SnowPeak => '^',
          TileType.Grass => ',',
          TileType.OrangeTree => 'T',
          TileType.GreenTree => 'T',
          TileType.RedTree => 'T',
          TileType.YellowTree => 'T',
          TileType.DeepWater or TileType.Water => '~',
          _ => '!'
        };

        tw.Write(ch);
      }
      tw.WriteLine();
    }
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

    return map;
  }

  public static void PlaceStoneRing(Map map, Town town, GameObjectDB objDb, FactDb factDb, Rng rng)
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
    SetColumn(row, col + 3);
    SetColumn(row + 1, col + 1);
    SetColumn(row + 1, col + 5);
    SetColumn(row + 3, col);
    SetColumn(row + 3, col + 6);
    SetColumn(row + 5, col + 1);
    SetColumn(row + 5, col + 5);
    SetColumn(row + 6, col + 3);
    map.SetTile(row + 3, col + 3, TileFactory.Get(TileType.Dirt));
    factDb.Add(new LocationFact() { Desc = "Stone ring centre", Loc = new(0, 0, row + 3, col + 3) });
    
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