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

// Eventually this will addd NPCs, special features, etc. For now let's
// just get the town buildings drawn on the map

enum BuildingType
{
  Shrine,
  Home,
  Tavern,
  Market,
  Smithy,
  WitchesCottage
}

class Town
{
  public string Name { get; set; } = "";
  public HashSet<Loc> Shrine { get; set; } = [];
  public HashSet<Loc> Tavern { get; set; } = [];
  public HashSet<Loc> Market { get; set; } = [];
  public HashSet<Loc> Smithy { get; set; } = [];
  public List<HashSet<Loc>> Homes { get; set; } = [];
  public HashSet<int> TakenHomes { get; set; } = [];
  public HashSet<Loc> TownSquare { get; set; } = [];
  public HashSet<Loc> WitchesCottage { get; set; } = [];
  public HashSet<Loc> WitchesGarden { get; set; } = [];
  public HashSet<Loc> WitchesYard { get; set; } = [];

  public HashSet<Loc> Roofs { get; set; } = [];

  public int Row { get; set; }
  public int Col { get; set; }
  public int Height { get; set; }
  public int Width { get; set; }

  public bool InTown(Loc loc)
  {
    if (loc.Row >= Row && loc.Row <= Row + Height && loc.Col >= Col && loc.Col <= Col + Width)
      return true;
    return false;
  }
}

class TownBuilder
{
  const int TOWN_HEIGHT = 37;
  const int TOWN_WIDTH = 63;

  public (int, int) TownCentre { get; set; }
  public Town Town { get; set; }
  Dictionary<string, Template> Templates { get; set; } = [];

  public TownBuilder() => Town = new Town();

  // This requires the templates to be squares and while I was writing this code I
  // was too dumb to figure out how to rotate a rectangle so for now I'm going to 
  // stick with square building templates :P
  static char[] Rotate(char[] sqs, int width)
  {
    var indices = ListUtils.Filled(0, width * width);
    var rotated = ListUtils.Filled('`', width * width);

    for (int i = 0; i < width * width; i++)
    {
      if (i < width)
        indices[i] = i * width + width - 1;
      else
        indices[i] = indices[i - width] - 1;
    }

    foreach (var i in indices)
    {
      char c;
      if (sqs[i] == '|')
        c = '-';
      else if (sqs[i] == '-')
        c = '|';
      else
        c = sqs[i];
      rotated[indices[i]] = c;
    }

    return [.. rotated];
  }

  void DrawBuilding(Map map, int row, int col, int townRow, int townCol, Template t, BuildingType building, Rng rng)
  {
    bool isWood = rng.NextDouble() < 0.7;
    HashSet<(int, int)> sqs = [];
    char[] buildingSqs = t.Sqs.Select(sq => sq).ToArray();

    // rotate would go here
    if (!t.NoRotate)
    {
      int centreRow = row + t.Height / 2;
      int centreCol = col + t.Width / 2;
      int quarter = TOWN_HEIGHT / 4;
      int northQuarter = townRow + quarter;
      int southQuarter = townRow + quarter + quarter;
      int mid = townCol + TOWN_WIDTH / 2;

      if (centreRow >= southQuarter)
      {
        // rotate doors to face north
        buildingSqs = Rotate(buildingSqs, t.Width);
        buildingSqs = Rotate(buildingSqs, t.Width);
      }
      else if (centreRow > northQuarter && centreCol < mid)
      {
        // rotate doors to face east
        buildingSqs = Rotate(buildingSqs, t.Width);
        buildingSqs = Rotate(buildingSqs, t.Width);
        buildingSqs = Rotate(buildingSqs, t.Width);
      }
      else if (centreRow > northQuarter && centreCol > mid)
      {
        // rotate doors to face west
        buildingSqs = Rotate(buildingSqs, t.Width);
      }
    }

    for (int r = 0; r < t.Height; r++)
    {
      for (int c = 0; c < t.Width; c++)
      {
        int currRow = row + r;
        int currCol = col + c;
        var tileType = buildingSqs[r * t.Width + c] switch
        {
          '#' => isWood ? TileType.WoodWall : TileType.StoneWall,
          '`' => TileType.Grass,
          '+' => TileType.ClosedDoor,
          '|' => TileType.VWindow,
          '-' => TileType.HWindow,
          'T' => Wilderness.PickTree(rng, 20),
          '.' => building == BuildingType.Smithy ? TileType.StoneFloor : TileType.WoodFloor,
          _ => throw new Exception("Invalid character in building template!")
        };

        sqs.Add((currRow, currCol));
        // if the tile is grass or a tree and the underlying tile is a 
        // river, we don't want to overwrite it        
        if (map.TileAt(currRow, currCol).Type == TileType.Water)
        {          
          switch (tileType)
          {
            case TileType.Grass:
            case TileType.Conifer:
            case TileType.GreenTree:
            case TileType.OrangeTree:
            case TileType.YellowTree:
            case TileType.RedTree:
              continue;
          }
        }
        map.SetTile(currRow, currCol, TileFactory.Get(tileType));
      }
    }

    switch (building)
    {
      case BuildingType.Shrine:
        Town.Shrine = sqs.Select(sq => new Loc(0, 0, sq.Item1, sq.Item2)).ToHashSet();        
        break;
      case BuildingType.Tavern:
        Town.Tavern = sqs.Select(sq => new Loc(0, 0, sq.Item1, sq.Item2)).ToHashSet();
        InstallSign(map, building, sqs, rng);
        break;
      case BuildingType.Market:
        Town.Market = sqs.Select(sq => new Loc(0, 0, sq.Item1, sq.Item2)).ToHashSet();
        InstallSign(map, building, sqs, rng);
        break;
      case BuildingType.Smithy:
        Town.Smithy = sqs.Select(sq => new Loc(0, 0, sq.Item1, sq.Item2)).ToHashSet();
        InstallSign(map, building, sqs, rng);
        break;
      case BuildingType.WitchesCottage:
        // Witches' cottage is set up outside the main town building functions
        break;
      default:
        Town.Homes.Add(sqs.Select(sq => new Loc(0, 0, sq.Item1, sq.Item2)).ToHashSet());
        break;
    }
  }

  static void InstallSign(Map map, BuildingType building, HashSet<(int, int)> sqs, Rng rng)
  {
    foreach (var (r, c) in sqs)
    {
      if (map.TileAt(r, c).Type != TileType.ClosedDoor)
        continue;

        string sign = building switch
        {
          BuildingType.Smithy => "Ye Olde Smithy",
          BuildingType.Market => "Dry Goods, Salves, & More",
          BuildingType.Tavern => "Tavern & Common House",
          _ => ""
        };
        
        List<(int, int)> options = [];
        TileType type = map.TileAt(r-1, c-1).Type;
        if ((type == TileType.Grass || type == TileType.Dirt || map.TileAt(r-1, c-1).IsTree())
                && !Util.Adj4Sqs(r-1, c-1).Any(sq => map.TileAt(sq).Type == TileType.ClosedDoor))
          options.Add((r-1, c-1));
        type = map.TileAt(r-1, c+1).Type;
        if ((type == TileType.Grass || type == TileType.Dirt || map.TileAt(r-1, c+1).IsTree())
                && !Util.Adj4Sqs(r-1, c+1).Any(sq => map.TileAt(sq).Type == TileType.ClosedDoor))
          options.Add((r-1, c+1));
        type = map.TileAt(r+1, c-1).Type;
        if ((type == TileType.Grass || type == TileType.Dirt || map.TileAt(r+1, c-1).IsTree())
                && !Util.Adj4Sqs(r+1, c-1).Any(sq => map.TileAt(sq).Type == TileType.ClosedDoor))
          options.Add((r+1, c-1));
        type = map.TileAt(r+1, c+1).Type;
        if ((type == TileType.Grass || type == TileType.Dirt || map.TileAt(r+1, c+1).IsTree())
                && !Util.Adj4Sqs(r+1, c+1).Any(sq => map.TileAt(sq).Type == TileType.ClosedDoor))
          options.Add((r+1, c+1));

        if (options.Count > 0)
        {
          var (signR, signC) = options[rng.Next(options.Count)];
          map.SetTile(signR, signC, new BusinessSign(sign));
          break;
        }
      }
  }

  bool BuildingFits(Map map, int nwRow, int nwCol, Template t)
  {
    for (int r = 0; r < t.Height; r++)
    {
      for (int c = 0; c < t.Width; c++)
      {
        switch (map.TileAt(nwRow + r, nwCol + c).Type)
        {
          case TileType.DeepWater:
          case TileType.Water:
          case TileType.StoneWall:
          case TileType.WoodWall:
          case TileType.DungeonFloor:
          case TileType.WoodFloor:
          case TileType.ClosedDoor:
          case TileType.HWindow:
            return false;
          default:
            continue;
        }
      }
    }

    // We also want to ensure a little space between buildings
    for (int c = 0; c < t.Width; c++)
    {
      var tile = map.TileAt(nwRow - 1, nwCol + c);
      if (tile.Type == TileType.StoneWall || tile.Type == TileType.WoodWall)
        return false;
      tile = map.TileAt(nwRow + t.Height, nwCol + c);
      if (tile.Type == TileType.StoneWall || tile.Type == TileType.WoodWall)
        return false;
    }

    for (int r = 0; r < t.Height; r++)
    {
      var tile = map.TileAt(nwRow + r, nwCol - 1);
      if (tile.Type == TileType.StoneWall || tile.Type == TileType.WoodWall)
        return false;
      tile = map.TileAt(nwRow + r, nwCol + t.Width);
      if (tile.Type == TileType.StoneWall || tile.Type == TileType.WoodWall)
        return false;
    }

    return true;
  }

  bool CheckAlongCol(Map map, int startRow, int startCol, int townRow, int townCol, int delta, Template t, BuildingType building, Rng rng)
  {
    int height = t.Height;

    if (delta > 0)
    {
      int row = startRow;
      while (row + height < townRow + TOWN_HEIGHT)
      {
        if (BuildingFits(map, row, startCol, t))
        {
          DrawBuilding(map, row, startCol, townRow, townCol, t, building, rng);
          return true;
        }
        row += delta;
      }
    }
    else
    {
      int row = startRow - height;
      while (row > townRow)
      {
        if (BuildingFits(map, row, startCol, t))
        {
          DrawBuilding(map, row, startCol, townRow, townCol, t, building, rng);
          return true;
        }
        row += delta;
      }
    }

    return false;
  }

  bool CheckAlongRow(Map map, int startRow, int startCol, int townRow, int townCol, int delta, Template t, BuildingType building, Rng rng)
  {
    int width = t.Width;

    if (delta > 0)
    {
      int col = startCol;
      while (col + width < townCol + TOWN_WIDTH)
      {
        if (BuildingFits(map, startRow, col, t))
        {
          DrawBuilding(map, startRow, col, townRow, townCol, t, building, rng);
          return true;
        }
        col += delta;
      }
    }
    else
    {
      int col = townCol + TOWN_WIDTH - width - 1;
      while (col > townCol)
      {
        if (BuildingFits(map, startRow, col, t))
        {
          DrawBuilding(map, startRow, col, townRow, townCol, t, building, rng);
          return true;
        }
        col += delta;
      }
    }

    return false;
  }

  // This code (and the general building placement code) is very
  // cut-and-paste-y. But I'll likely eventually rewrite the 
  // town generation and building placement. This is just a 
  // "get something working" first pass at it"
  void PlaceTavern(Map map, int townRow, int townCol, Rng rng)
  {
    List<int> options = [1, 2, 3, 4];
    options.Shuffle(rng);

    int o = 0;
    while (o < options.Count)
    {
      int choice = options[o++];

      if (choice == 1)
      {
        // east facing tavern
        var template = Templates["tavern 1"];
        int startRow, delta;
        if (rng.NextDouble() < 0.5)
        {
          startRow = townRow;
          delta = 1;
        }
        else
        {
          startRow = townRow + TOWN_HEIGHT - 1;
          delta = -1;
        }

        delta = -1;
        if (CheckAlongCol(map, startRow, townCol, townRow, townCol, delta, template, BuildingType.Tavern, rng))
          break;
      }
      else if (choice == 2)
      {
        // south facing tavern
        var template = Templates["tavern 2"];
        int startCol, delta;
        if (rng.NextDouble() < 0.5)
        {
          startCol = townCol;
          delta = 1;
        }
        else
        {
          startCol = townCol + TOWN_HEIGHT - template.Height;
          delta = -1;
        }
        if (CheckAlongRow(map, townRow, startCol, townRow, townCol, delta, template, BuildingType.Tavern, rng))
          break;
      }
      else if (choice == 3)
      {
        // north facing tavern
        var template = Templates["tavern 3"];
        int startCol, delta;
        if (rng.NextDouble() < 0.5)
        {
          startCol = townCol;
          delta = 1;
        }
        else
        {
          startCol = townCol + TOWN_WIDTH - template.Width;
          delta = -1;
        }
        if (CheckAlongRow(map, townRow + TOWN_HEIGHT - template.Height - 1, startCol, townRow, townCol, delta, template, BuildingType.Tavern, rng))
          break;
      }
      else if (choice == 4)
      {
        // west facing tavern
        var template = Templates["tavern 4"];
        int startRow, delta;
        if (rng.NextDouble() < 0.5)
        {
          startRow = townRow;
          delta = 1;
        }
        else
        {
          startRow = townRow + TOWN_HEIGHT;
          delta = -1;
        }
        if (CheckAlongCol(map, startRow, townCol + TOWN_WIDTH - template.Width - 1, townRow, townCol, delta, template, BuildingType.Tavern, rng))
          break;
      }
    }
  }

  bool PlaceBuilding(Map map, int townRow, int townCol, Template t, BuildingType building, Rng rng)
  {
    List<int> options = [1, 2, 3, 4];
    options.Shuffle(rng);

    int o = 0;
    while (o < options.Count)
    {
      int choice = options[o++];
      if (choice == 1)
      {
        // start at top left, stagger the buildings
        // so the town doesn't look too neat and orderly
        int row = townRow + rng.Next(0, 6);
        int col = townCol + rng.Next(0, 6);
        int deltaRow = 2;
        int deltaCol = 2;

        while (true)
        {
          if (CheckAlongRow(map, row, col, townRow, townCol, deltaCol, t, building, rng))
            return true;
          row += deltaRow;
          col += deltaCol;
          if (col + t.Width > townCol + TOWN_WIDTH)
            col = townCol;
          if (row < townRow || row + t.Height > townRow + TOWN_HEIGHT)
            break;
        }
      }
      else if (choice == 2)
      {
        // start at bottom left
        int row = townRow + TOWN_HEIGHT - t.Height - 1 - rng.Next(0, 6);
        int col = townCol + rng.Next(0, 6);
        int deltaRow = -2;
        int deltaCol = 2;

        while (true)
        {
          if (CheckAlongRow(map, row, col, townRow, townCol, deltaCol, t, building, rng))
            return true;
          row += deltaRow;
          col += deltaCol;
          if (col + t.Width >= townCol + TOWN_WIDTH)
            col = townCol;
          if (row < townRow || row + t.Height > townRow + TOWN_HEIGHT)
            break;
        }
      }
      else if (choice == 3)
      {
        // start at top right
        int row = townRow + rng.Next(0, 6);
        int col = townCol + TOWN_WIDTH - t.Width - 1 - rng.Next(0, 6);
        int deltaRow = 2;
        int deltaCol = -2;

        while (true)
        {
          if (CheckAlongRow(map, row, col, townRow, townCol, deltaCol, t, building, rng))
            return true;
          row += deltaRow;
          col += deltaCol;
          if (col < townCol)
            col = townCol + TOWN_WIDTH - t.Width - 1;
          if (row < townRow || row + t.Height > townRow + TOWN_HEIGHT)
            break;
        }
      }
      else
      {
        // start at bottom right
        var row = townRow + TOWN_HEIGHT - t.Height - 1 - rng.Next(0, 6);
        var col = townCol + TOWN_WIDTH - t.Width - 1 - rng.Next(0, 6);
        int deltaRow = -2;
        int deltaCol = -2;

        while (true)
        {
          if (CheckAlongRow(map, row, col, townRow, townCol, deltaCol, t, building, rng))
            return true;
          row += deltaRow;
          col += deltaCol;
          if (col < townCol)
            col = townCol + TOWN_WIDTH - t.Width - 1;
          if (row < townRow || row + t.Height > townRow + TOWN_HEIGHT)
            break;
        }
      }
    }

    return false;
  }

  static bool GoodSpotForForge(Map map, int row, int col)
  {
    if (map.TileAt(row, col).Type != TileType.StoneFloor)
      return false;

    foreach (var (adjR, adjC) in Util.Adj8Sqs(row, col))
    {
      if (map.TileAt(adjR, adjC).Type == TileType.ClosedDoor)
        return false;
    }

    return true;
  }

  void PlaceBuildings(Map map, int townRow, int townCol, Rng rng)
  {
    // Step 1, get rid of most but not all the trees in the town and replace them with grass
    for (int r = townRow; r < townRow + TOWN_HEIGHT; r++)
    {
      for (int c = townCol; c < townCol + TOWN_WIDTH; c++)
      {
        if (map.TileAt(r, c).IsTree() && rng.NextDouble() < 0.85)
          map.SetTile(r, c, TileFactory.Get(TileType.Grass));
        else if (map.TileAt(r, c).Type == TileType.Mountain)
          map.SetTile(r, c, TileFactory.Get(TileType.Grass));
      }
    }

    // Next place the tavern; it's the largest building and the hardest to find a spot for
    PlaceTavern(map, townRow, townCol, rng);

    var cottages = Templates.Keys.Where(k => k.StartsWith("cottage")).ToList();

    // create the town's market
    var j = cottages[rng.Next(cottages.Count)];
    PlaceBuilding(map, townRow, townCol, Templates[j], BuildingType.Market, rng);

    // next, the smithy
    j = cottages[rng.Next(cottages.Count)];
    PlaceBuilding(map, townRow, townCol, Templates[j], BuildingType.Smithy, rng);
    var smithySqs = Town.Smithy.ToList();

    if (smithySqs.Count == 0)
      throw new PlacingBuldingException();

    smithySqs.Shuffle(rng);
    int f = 0;
    while (true)
    {
      var loc = smithySqs[f++];
      if (GoodSpotForForge(map, loc.Row, loc.Col))
      {
        map.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.Forge));
        break;
      }
    }

    // there's only one shrine in the town. Maybe in the future I'll implement
    // religious rivalries
    string temple = rng.Next(2) == 0 ? "shrine 1" : "shrine 2";
    PlaceBuilding(map, townRow, townCol, Templates[temple], BuildingType.Shrine, rng);

    // place the cottages/homes
    for (int i = 0; i < 6; i++)
    {
      int h = rng.Next(cottages.Count);
      string home = cottages[h];
      if (!PlaceBuilding(map, townRow, townCol, Templates[home], BuildingType.Home, rng))
        break;
    }
  }

  // Draw the paths in the town. For now they just converge on the town square, but perhaps 
  // I'll do paths between the buildings later. (If townsfolk eventually are 'friends' with
  // each other, maybe draw a path between their homes)
  //
  // This can maybe be replaced by a Dijkstra Map with a building's door as the start and
  // the down square as the goal?
  void DrawPathsInTown(Map map, Rng rng, int townRow, int townCol)
  {
    HashSet<(int, int)> doors = [];

    for (int r = townRow; r < townRow + TOWN_HEIGHT; r++)
    {
      for (int c = townCol; c < townCol + TOWN_WIDTH; c++)
      {
        if (map.TileAt(r, c).Type == TileType.ClosedDoor)
        {
          foreach (var adj in Util.Adj4Sqs(r, c))
          {
            var tile = map.TileAt(adj.Item1, adj.Item2);
            if (tile.Type == TileType.Grass || tile.IsTree())
            {
              map.SetTile(adj.Item1, adj.Item2, TileFactory.Get(TileType.Dirt));
              doors.Add((adj.Item1, adj.Item2));
            }
          }
        }
      }
    }

    // pick a random spot in the town square for the paths to converge on
    int j = rng.Next(Town.TownSquare.Count);
    if (Town.TownSquare.Count == 0)
      return;

    var centre = Town.TownSquare.ToList()[j];
    var dmap = new DijkstraMap(map, [], 129, 129, true);
    dmap.Generate(DijkstraMap.CostWithDoors, (centre.Row, centre.Col), TOWN_WIDTH);

    foreach (var doorstep in doors)
    {
      List<(int, int)> path;

      path = dmap.ShortestPath(doorstep.Item1, doorstep.Item2);
      
      foreach (var sq in path)
      {
        var tile = map.TileAt(sq);
        if (tile.Type == TileType.Grass || tile.IsTree())
          map.SetTile(sq, TileFactory.Get(TileType.Dirt));
        else if (tile.Type == TileType.Water)
        {
          map.SetTile(sq, TileFactory.Get(TileType.Bridge));
          var col = sq.Item2 + 1;
          while (map.TileAt(sq.Item1, col).Type == TileType.Water)
          {
            map.SetTile(sq.Item1, col, TileFactory.Get(TileType.Bridge));
            ++col;
          }

          col = sq.Item2 - 1;
          while (map.TileAt(sq.Item1, col).Type == TileType.Water)
          {
            map.SetTile(sq.Item1, col, TileFactory.Get(TileType.Bridge));
            --col;
          }
        }
      }
    }
  }

  public void AddWell(Map map, Rng rng)
  {
    List<Loc> locs = [];
    foreach (Loc loc in Town.TownSquare)
    {
      bool okay = true;
      foreach (var adj in Util.Adj4Sqs(loc.Row, loc.Col))
      {
        Tile tile = map.TileAt(adj.Item1, adj.Item2);
        if (tile.Type != TileType.Grass && tile.Type != TileType.Dirt && !tile.IsTree())
        {
          okay = false;
          break;
        }
      }

      if (!okay)
        continue;
      locs.Add(loc);
    }

    Loc well = locs[rng.Next(locs.Count)];
    map.SetTile(well.Row, well.Col, TileFactory.Get(TileType.Well));
  }

  static int CountBlockedSqs(Map map, int startRow, int startCol)
  {
    int blocked = 0;
    for (int r = startRow; r < startRow + TOWN_HEIGHT; r++)
    {
      for (int c = startCol; c < startCol + TOWN_WIDTH; c++)
      {
        switch (map.TileAt(r, c).Type)
        {
          case TileType.WorldBorder:
          case TileType.Mountain:
          case TileType.SnowPeak:
          case TileType.DeepWater:
          case TileType.Water:
            ++blocked;
            break;
        }
      }
    }

    return blocked;
  }

  bool DrawWitchesCottage(Map map, int r, int c, int townCentreRow, int townCentreCol, Template template, GameObjectDB objDb, Rng rng)
  {
    DrawBuilding(map, r, c, townCentreRow, townCentreCol, template, BuildingType.WitchesCottage, rng);

    Town.WitchesCottage = [];
    Town.WitchesGarden = [];

    // The witches also get a well
    List<(int, int)> opts = [];
    (int, int) frontDoor = (-1, -1);
    for (int dr = 0; dr < 15; dr++) 
    {
      for (int dc = 0; dc < 15; dc++)
      {
        Town.WitchesCottage.Add(new Loc(0, 0, r + dr, c + dc));
        TileType tile = map.TileAt(r + dr, c + dc).Type;
        switch (tile)
        {
          case TileType.Grass:
          case TileType.Dirt:
          case TileType.Conifer:
          case TileType.GreenTree:
          case TileType.OrangeTree:
          case TileType.RedTree:
          case TileType.YellowTree:
          case TileType.Sand:
            opts.Add((r + dr, c + dc));
            break;
          case TileType.ClosedDoor:
            frontDoor = (r + dr, c + dc);
            break;
        }
      }
    }

    int gr = rng.Next(15) + r;
    int gc = (gr > r + 11 ? rng.Next(15) : rng.Next(12, 15)) + c;

    Loc gardenSq = new(0, 0, gr + 1, gc + 1);

    // draw the garden, which is just dirt but maybe I'll eventually
    // have crops/plants
    int gate = rng.Next(10);
    map.SetTile(gr, gc, TileFactory.Get(TileType.CornerFence));
    if (gate != 0)
      map.SetTile(gr, gc + 1, TileFactory.Get(TileType.HFence));
    if (gate != 1)
      map.SetTile(gr, gc + 2, TileFactory.Get(TileType.HFence));
    map.SetTile(gr, gc + 3, TileFactory.Get(TileType.CornerFence));

    if (gate != 2)
      map.SetTile(gr + 1, gc, TileFactory.Get(TileType.VFence));
    Town.WitchesGarden.Add(new Loc(0, 0, gr + 1, gc + 1));
    map.SetTile(gr + 1, gc + 1, TileFactory.Get(TileType.Dirt));
    Town.WitchesGarden.Add(new Loc(0, 0, gr + 2, gc + 1));
    map.SetTile(gr + 1, gc + 2, TileFactory.Get(TileType.Dirt));
    if (gate != 3)
      map.SetTile(gr + 1, gc + 3, TileFactory.Get(TileType.VFence));

    if (gate != 4)
      map.SetTile(gr + 2, gc, TileFactory.Get(TileType.VFence));
    Town.WitchesGarden.Add(new Loc(0, 0, gr + 2, gc + 1));
    map.SetTile(gr + 2, gc + 1, TileFactory.Get(TileType.Dirt));
    Town.WitchesGarden.Add(new Loc(0, 0, gr + 2, gc + 2));
    map.SetTile(gr + 2, gc + 2, TileFactory.Get(TileType.Dirt));
    if (gate != 5)
      map.SetTile(gr + 2, gc + 3, TileFactory.Get(TileType.VFence));

    if (gate != 6)
      map.SetTile(gr + 3, gc, TileFactory.Get(TileType.VFence));
    Town.WitchesGarden.Add(new Loc(0, 0, gr + 3, gc + 1));
    map.SetTile(gr + 3, gc + 1, TileFactory.Get(TileType.Dirt));
    Town.WitchesGarden.Add(new Loc(0, 0, gr + 3, gc + 2));
    map.SetTile(gr + 3, gc + 2, TileFactory.Get(TileType.Dirt));
    if (gate != 7)
      map.SetTile(gr + 3, gc + 3, TileFactory.Get(TileType.VFence));

    map.SetTile(gr + 4, gc, TileFactory.Get(TileType.CornerFence));
    if (gate != 8)
      map.SetTile(gr + 4, gc + 1, TileFactory.Get(TileType.HFence));
    if (gate != 9)
      map.SetTile(gr + 4, gc + 2, TileFactory.Get(TileType.HFence));
    map.SetTile(gr + 4, gc + 3, TileFactory.Get(TileType.CornerFence));

    // Is it a good placement for the cottage? Make sure there's a walkable 
    // path from the front door to the garden. (Sometimes the garden was 
    // created in the middle of mountains, eetc)
    Loc frontDoorLoc = new(0, 0, frontDoor.Item1, frontDoor.Item2);
    Dictionary<TileType, int> costs = [];
    costs.Add(TileType.Grass, 1);
    costs.Add(TileType.GreenTree, 1);
    costs.Add(TileType.YellowTree, 1);
    costs.Add(TileType.RedTree, 1);
    costs.Add(TileType.OrangeTree, 1);
    costs.Add(TileType.Conifer, 1);
    costs.Add(TileType.Dirt, 1);
    costs.Add(TileType.ClosedDoor, 1);

    var gardenPath = AStar.FindPath(objDb, map, frontDoorLoc, gardenSq, costs, false);
    if (gardenPath.Count == 0)
      return false;

    // Draw a road from the front step to the town
    CampaignCreator.DrawRoad(map, map.Height, frontDoor, Town, TileType.Dirt, true, rng);

    if (opts.Count > 0)
    {
      var wellSq = opts[rng.Next(opts.Count)];
      map.SetTile(wellSq, TileFactory.Get(TileType.Well));
    }

    return true;
  }

  void AddWitchesCottage(Map map, int townCentreRow, int townCentreCol, Template template, GameObjectDB objDb, Rng rng)
  {
    // Find candidate spots for the witches' cottage
    List<(int, int)> candidates = [];
    for (int r = 3; r < map.Height - 16; r++)
    {
      for (int c = 3; c < map.Width - 16; c++)
      {
        if (Util.Distance(r, c, townCentreRow, townCentreCol) >= 65)
          candidates.Add((r, c));
      }
    }
    candidates.Shuffle(rng);

    int topLeftRow, topLeftCol;
    foreach (var sq in candidates)
    {
      int waterCount = 0;
      int mountainCount = 0;
      for (int r = 0; r < 15; r++)
      {
        for (int c = 0; c < 15; c++)
        {
          Tile tile = map.TileAt(sq.Item1 + r, sq.Item2 + c);
          if (tile.Type == TileType.Mountain || tile.Type == TileType.SnowPeak)
            ++mountainCount;
          if (tile.Type == TileType.DeepWater || tile.Type == TileType.Water)
            ++ waterCount;
        }
      }

      if (waterCount >= 75 || mountainCount >= 75)
        continue;

      topLeftRow = sq.Item1;
      topLeftCol = sq.Item2;

      if (topLeftRow == -1 && topLeftCol == -1)
        continue;

      Map tmp = (Map) map.Clone();
      bool success = DrawWitchesCottage(tmp, topLeftRow, topLeftCol, townCentreRow, townCentreCol, template, objDb, rng);
      if (success)
      {
        for (int r = 0; r < map.Height; r++)
        {
          for (int c = 0; c < map.Width; c++)
          {
            map.SetTile(r, c, tmp.TileAt(r, c));
          }
        }

        // Record the full bounds of the witches' property
        int minRow = int.MaxValue, maxRow = 0, minCol = int.MaxValue, maxCol = 0;
        foreach (Loc loc in Town.WitchesGarden)
        {
          if (loc.Row < minRow) minRow = loc.Row;
          if (loc.Row > maxRow) maxRow = loc.Row;
          if (loc.Col < minCol) minCol = loc.Col;
          if (loc.Col > maxCol) maxCol = loc.Col;
        }
        foreach (Loc loc in Town.WitchesCottage)
        {
          if (loc.Row < minRow) minRow = loc.Row;
          if (loc.Row > maxRow) maxRow = loc.Row;
          if (loc.Col < minCol) minCol = loc.Col;
          if (loc.Col > maxCol) maxCol = loc.Col;
        }

        for (int r = minRow - 2; r < maxRow + 2; r++)
        {
          for (int c = minCol - 2; c < maxCol + 2; c++)
          {
            if (!map.InBounds(r, c))
              continue;
            Town.WitchesYard.Add(new Loc(0, 0, r, c));
          }
        }

        return;
      }
    }

    throw new Exception("Unable to find a valid placement for witches' cottage");
  }

  void CalcRoofs(Map map)
  {
    void CalcBuilding(HashSet<Loc> building)
    {
      foreach (Loc loc in building)
      {
        Tile tile = map.TileAt(loc.Row, loc.Col);
        if (tile.Type == TileType.WoodFloor || tile.Type == TileType.StoneFloor || tile.Type == TileType.Forge)
          Town.Roofs.Add(loc);
      }
    }

    CalcBuilding(Town.Shrine);
    CalcBuilding(Town.Tavern);
    CalcBuilding(Town.Market);
    CalcBuilding(Town.Smithy);
    CalcBuilding(Town.WitchesCottage);
    foreach (HashSet<Loc> home in Town.Homes)
      CalcBuilding(home);
  }

  public Map DrawnTown(Map map, Rng rng)
  {
    int rows = 0, width = 0;
    List<char> sqs = [];
    bool noRotate = false;
    string currBuildling = "";
    foreach (var line in File.ReadLines(ResourcePath.GetDataFilePath("buildings.txt")))
    {
      if (line.StartsWith('%'))
      {
        if (currBuildling != "")
        {
          var template = new Template() { Sqs = sqs, NoRotate = noRotate, Width = width, Height = rows };
          Templates.Add(currBuildling, template);
        }
        currBuildling = line[1..];
        rows = 0;
        sqs = [];
        noRotate = false;
      }
      else if (line == "no rotate")
      {
        noRotate = true;
      }
      else
      {
        width = line.Length;
        sqs.AddRange(line.ToCharArray());
        ++rows;
      }
    }
    var lastTemplate = new Template() { Sqs = sqs, NoRotate = noRotate, Width = width, Height = rows };
    Templates.Add(currBuildling, lastTemplate);

    int wildernessSize = map.Height;

    // Try to find a spot on the map without too many water/mountain squares.
    // We'll loop up to 5 times and then try to place buildings. (If we can't
    // place enough cottages the map will be rejected anyhow)
    int startRow, startCol;
    int acceptableBlocked = (int)(TOWN_WIDTH * TOWN_HEIGHT * 0.15);
    int tries = 0;
    do
    {
      // Pick starting co-ordinates that are in the centre-ish area of the map
      startRow = rng.Next(wildernessSize / 4, wildernessSize / 2);
      startCol = rng.Next(wildernessSize / 4, wildernessSize / 2);

      int blockedSqs = CountBlockedSqs(map, startRow, startCol);
      if (blockedSqs <= acceptableBlocked)
        break;
      ++tries;
    }
    while (tries < 5);

    Town.Row = startRow;
    Town.Col = startCol;
    Town.Height = TOWN_HEIGHT;
    Town.Width = TOWN_WIDTH;

    PlaceBuildings(map, startRow, startCol, rng);

    if (Town.Homes.Count < 4)
    {
      throw new InvalidTownException();
    }

    int centreRow = startRow + TOWN_HEIGHT / 2;
    int centreCol = startCol + TOWN_WIDTH / 2;
    TownCentre = (centreRow, centreCol);

    // Mark town square
    for (int r = centreRow - 5; r < centreRow + 5; r++)
    {
      for (int c = centreCol - 5; c < centreCol + 5; c++)
      {
        Tile tile = map.TileAt(r, c);
        if (tile.Type == TileType.Grass || tile.Type == TileType.Dirt || tile.IsTree())
          Town.TownSquare.Add(new Loc(0, 0, r, c));
      }
    }

    DrawPathsInTown(map, rng, startRow, startCol);

    AddWell(map, rng);

    AddWitchesCottage(map, centreRow, centreCol, Templates["cottage 3"], new GameObjectDB(), rng);

    CalcRoofs(map);

    return map;
  }
}

class Template
{
  public List<char> Sqs { get; set; } = [];
  public int Width { get; set; }
  public int Height { get; set; }
  public bool NoRotate { get; set; }
}
