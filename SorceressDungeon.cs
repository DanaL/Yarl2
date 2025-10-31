// Delve - A roguelike computer RPG
// Written in 2025 by Dana Larose <ywg.dana@gmail.com>
//
// To the extent possible under law, the author(s) have dedicated all copyright
// and related and neighboring rights to this software to the public domain
// worldwide. This software is distributed without any warranty.
//
// You should have received a copy of the CC0 Public Domain Dedication along 
// with this software. If not, 
// see <http://creativecommons.org/publicdomain/zero/1.0/>.

namespace Yarl2;

class SorceressDungeonBuilder(int dungeonId, int height, int width) : DungeonBuilder
{
  int Height { get; set; } = height;
  int Width { get; set; } = width;
  int DungeonId { get; set; } = dungeonId;

  public Loc DecoyMirror1 { get; set; } = Loc.Nowhere;
  public Loc DecoyMirror2 { get; set; } = Loc.Nowhere;

  static Loc MarkVampyCastleLoc(Map map, int h, int w, Loc mirrorExit, int dungeonId, GameObjectDB objDb, Rng rng)
  {
    List<(int, int)> opts = [];
    int mostOpen = 0;
    for (int r = 2; r < h - 9; r++)
    {
      for (int c = 2; c < w - 9; c += 2)
      {
        int x = CountOpenBorderSqs(r, c);
        if (x > mostOpen)
        {
          opts.Clear();
          opts.Add((r, c));
          mostOpen = x;
        }
        else if (x == mostOpen)
        {
          opts.Add((r, c));
        }
      }
    }

    List<(int, int)> optsList = [.. opts];
    var (sr, sc) = optsList[rng.Next(optsList.Count)];

    List<(int, int)> walls = [];
    for (int c = sc; c <= sc + 7; c++)
    {
      map.SetTile(sr, c, TileFactory.Get(TileType.StoneWall));
      walls.Add((sr, c));
      map.SetTile(sr + 7, c, TileFactory.Get(TileType.StoneWall));
      walls.Add((sr + 7, c));

      // Make sure tiles around the building aren't mountains
      TileType tt = rng.Next(3) == 0 ? TileType.Grass : TileType.Conifer;
      map.SetTile(sr - 1, c, TileFactory.Get(tt));
      tt = rng.Next(3) == 0 ? TileType.Grass : TileType.Conifer;
      map.SetTile(sr + 8, c, TileFactory.Get(tt));
    }

    for (int r = sr; r < sr + 7; r++)
    {
      map.SetTile(r, sc, TileFactory.Get(TileType.StoneWall));
      walls.Add((r, sc));
      map.SetTile(r, sc + 7, TileFactory.Get(TileType.StoneWall));
      walls.Add((r, sc + 7));

      // Make sure tiles around the building aren't mountains
      TileType tt = rng.Next(3) == 0 ? TileType.Grass : TileType.Conifer;
      map.SetTile(r, sc - 1, TileFactory.Get(tt));
      tt = rng.Next(3) == 0 ? TileType.Grass : TileType.Conifer;
      map.SetTile(r, sc + 8, TileFactory.Get(tt));
    }

    List<(int, int)> floors = [];
    for (int r = sr + 1; r < sr + 7; r++)
    {
      for (int c = sc + 1; c < sc + 7; c++)
      {
        map.SetTile(r, c, TileFactory.Get(TileType.StoneFloor));
        floors.Add((r, c));
      }
    }

    walls.Remove((sr, sc));
    walls.Remove((sr + 7, sc + 7));
    walls.Remove((sr + 7, sc));
    walls.Remove((sr, sc + 7));

    var door = walls[rng.Next(walls.Count)];
    map.SetTile(door, TileFactory.Get(TileType.LockedDoor));

    MysteriousMirror mm = new("") { Destination = mirrorExit };
    var (mr, mc) = floors[rng.Next(floors.Count)];
    map.SetTile(mr, mc, mm);
    Loc mirrorLoc = new(dungeonId, 0, mr, mc);

    List<(int, int)> graveSpots = [];
    for (int r = sr - 2; r < sr + 9; r++)
    {
      for (int c = sc - 2; c < sc + 9; c++)
      {
        TileType tt = map.TileAt((r, c)).Type;
        if (tt == TileType.Conifer || tt == TileType.Grass)
          graveSpots.Add((r, c));
      }
    }
    int graves = int.Min(graveSpots.Count, rng.Next(4, 7));
    graveSpots.Shuffle(rng);
    NameGenerator ng = new(rng, Util.NamesFile);
    for (int j = 0; j < graves; j++)
    {
      string msg = rng.Next(10) switch
      {
        0 => $"{ng.GenerateName(rng.Next(5, 9)).Capitalize()}, exsanguinated and fondly remembered.",
        1 => $"{ng.GenerateName(rng.Next(5, 9)).Capitalize()}, a snack in many ways.",
        2 => $"{ng.GenerateName(rng.Next(5, 9)).Capitalize()}, a fine rival, a tall glass of blood.",
        _ => "An unmarked grave."
      };

      map.SetTile(graveSpots[j], new Gravestone(msg));
    }

    return mirrorLoc;

    int CountOpenBorderSqs(int r, int c)
    {
      int x = 0;
      for (int dc = c - 1; dc <= c + 7; dc++)
      {
        if (map.TileAt(r - 1, dc).Type == TileType.Grass || map.TileAt(r - 1, dc).Type == TileType.Conifer)
          ++x;
        if (map.TileAt(r + 1, dc).Type == TileType.Grass || map.TileAt(r + 1, dc).Type == TileType.Conifer)
          ++x;
      }
      
      for (int dr = r; dr < r + 7; dr++)
      {
        if (map.TileAt(dr, c - 1).Type == TileType.Grass || map.TileAt(dr, c - 1).Type == TileType.Conifer)
          ++x;
        if (map.TileAt(dr, c + 7).Type == TileType.Grass || map.TileAt(dr, c + 7).Type == TileType.Conifer)
          ++x;
      }

      return x;
    }
  }

  public static (Dungeon, Loc) VampyDungeon(Loc tower, int dungeonId, GameObjectDB objDb, Rng rng)
  {
    Dungeon dungeon = new(dungeonId, "a Gloomy Mountain Valley", "Dark clouds roil across a night sky.", false);

    Map map = new(82, 42, TileType.WorldBorder);
    bool[,] open = CACave.GetCave(40, 80, rng);
    for (int r = 0; r < 40; r++)
    {
      for (int c = 0; c < 80; c++)
      {
        TileType tt = open[r, c] ? TileType.Grass : TileType.Mountain;
        map.SetTile(r + 1 , c + 1, TileFactory.Get(tt));
      }
    }

    ConfigurablePassable passable = new();
    passable.Passable.Add(TileType.Grass);
    CACave.JoinCaves(map, rng, objDb, passable, TileType.Grass, TileType.Mountain, TileType.Mountain);

    for (int r = 1; r <= 40; r++)
    {
      for (int c = 1; c <= 80; c++)
      {
        if (map.TileAt(r, c).Type == TileType.Grass && rng.Next(4) < 3)
          map.SetTile(r + 1, c + 1, TileFactory.Get(TileType.Conifer));
      }
    }

    Loc mirrorLoc = MarkVampyCastleLoc(map, 42, 82, tower, dungeonId, objDb, rng);

    List<(int, int)> arrivalSpots = [];
    for (int r = 1; r < map.Height - 1; r++)
    {
      for (int c = 1; c < map.Width - 1; c++)
      {
        TileType tt = map.TileAt(r, c).Type;
        if ((tt == TileType.Grass || tt == TileType.Conifer) && Util.Distance(mirrorLoc.Row, mirrorLoc.Col, r, c) > 25)
          arrivalSpots.Add((r, c));
      }
    }

    var spot = arrivalSpots[rng.Next(arrivalSpots.Count)];
    Loc arrivalLoc = new(dungeonId, 0, spot.Item1, spot.Item2);

    dungeon.AddMap(map);

    return (dungeon, arrivalLoc);
  }

  public static (Dungeon, Loc) WumpusDungeon(Loc tower, int dungeonId, GameObjectDB objDb, Rng rng)
  {
    Dungeon dungeon = new(dungeonId, "a Noisome Cavern", "You are in Room 1.", false);

    var lines = File.ReadAllLines(ResourcePath.GetDataFilePath("wumpus.txt"));
    Map map = new(lines[0].Length, lines.Length, TileType.PermWall);
    for (int r = 0; r < lines.Length; r++)
    {
      for (int c = 0; c < lines[r].Length; c++)
      {
        if (lines[r][c] == '.')
        {
          map.SetTile(r, c, TileFactory.Get(TileType.DungeonFloor));          
        }
      }
    }

    // Identify the floor squares that are in chambers
    HashSet<(int, int)> floorSqs = [];    
    for (int r = 0; r < map.Height; r++)
    {
      for (int c = 0; c < map.Width; c++)
      {
        if (map.TileAt(r,c).Type == TileType.DungeonFloor && CountAdjFloors(map, r, c) > 3)
        {
          floorSqs.Add((r, c));
        }
      }
    }
    List<(int, int)> floorsInChambers = [.. floorSqs];

    int i = rng.Next(floorsInChambers.Count);
    Loc arrival = new(dungeonId, 0, floorsInChambers[i].Item1, floorsInChambers[i].Item2);
    floorsInChambers.RemoveAt(i);

    List<(int, int)> farSqs = [.. floorsInChambers.Where(s => Util.Distance(s.Item1, s.Item2, arrival.Row, arrival.Col) > 20)];
    var mirror = farSqs[rng.Next(farSqs.Count)];    
    MysteriousMirror mm = new("") { Destination = tower };
    map.SetTile(mirror.Item1, mirror.Item2, mm);
    floorsInChambers.Remove(mirror);

    i = rng.Next(floorsInChambers.Count);
    var pit = floorsInChambers[i];
    map.SetTile(pit, TileFactory.Get(TileType.HiddenPit));
    floorsInChambers.RemoveAt(i);

    i = rng.Next(floorsInChambers.Count);
    pit = floorsInChambers[i];
    map.SetTile(pit, TileFactory.Get(TileType.HiddenPit));
    floorsInChambers.RemoveAt(i);

    dungeon.AddMap(map);

    i = rng.Next(floorsInChambers.Count);
    var batSq = floorsInChambers[i];
    floorsInChambers.RemoveAt(i);
    Loc batLoc = new(dungeonId, 0, batSq.Item1, batSq.Item2);
    Actor bat = MonsterFactory.Get("super bat", objDb, rng);    
    objDb.AddNewActor(bat, batLoc);

    i = rng.Next(floorsInChambers.Count);
    batSq = floorsInChambers[i];
    floorsInChambers.RemoveAt(i);
    batLoc = new(dungeonId, 0, batSq.Item1, batSq.Item2);
    bat = MonsterFactory.Get("super bat", objDb, rng);
    objDb.AddNewActor(bat, batLoc);

    List<Loc> adjToMirror = [.. Util.Adj8Sqs(mirror.Item1, mirror.Item2)
                                  .Where(s => map.TileAt(s).Type == TileType.DungeonFloor)
                                  .Select(s => new Loc(dungeonId, 0, s.Item1, s.Item2))];
    adjToMirror.Shuffle(rng);
    Loc wumpusLoc = adjToMirror[0];
    Actor wumpus = MonsterFactory.Get("wumpus", objDb, rng);
    objDb.AddNewActor(wumpus, wumpusLoc);

    for (int j = 0; j < 4; j++)
    {
      var quality = rng.Next(4) > 0 ? TreasureQuality.Good : TreasureQuality.Uncommon;
      Item item = Treasure.ItemByQuality(quality, objDb, rng);
      var sq = floorsInChambers[rng.Next(floorsInChambers.Count)];
      Loc loc = new(dungeonId, 0, sq.Item1, sq.Item2);
      objDb.Add(item);
      objDb.SetToLoc(loc, item);
    }

    for (int j = 0; j < 3; j++)
    {
      Item item = ItemFactory.Get(ItemNames.BONE, objDb);
      var sq = floorsInChambers[rng.Next(floorsInChambers.Count)];
      Loc loc = new(dungeonId, 0, sq.Item1, sq.Item2);
      objDb.Add(item);
      objDb.SetToLoc(loc, item);
    }

    return (dungeon, arrival);

    int CountAdjFloors(Map map, int row, int col)
    {
      return Util.Adj8Sqs(row, col)
                 .Where(adj => map.TileAt(adj.Item1, adj.Item2).Type == TileType.DungeonFloor)
                 .Count();
    }
  }

  public (Dungeon, Loc) Generate(int entranceRow, int entranceCol, GameObjectDB objDb, Rng rng)
  {
    Dungeon towerDungeon = new(DungeonId, "a Musty Tower", "Ancient halls that smell of dust and magic.", false)
    {
      MonsterDecks = DeckBuilder.ReadDeck("tower", rng)
    };

    int numOfLevels = 4;

    Tower towerBuilder = new(Height, Width, 5);
    Map[] floors = [..towerBuilder.BuildLevels(numOfLevels, rng)];

    SetStairs(DungeonId, floors, Height, Width, numOfLevels, (entranceRow, entranceCol), false, false, rng);

    // Because it's a sorcerous tower, replace the final stairs with a 
    // Mysterious Mirror
    Map penultimate = floors[numOfLevels - 2];
    var upStairsSq = penultimate.SqsOfType(TileType.Upstairs).First();
    Upstairs upstairs = (Upstairs)penultimate.TileAt(upStairsSq);
    MysteriousMirror mm1 = new("") { Destination = upstairs.Destination };
    penultimate.SetTile(upStairsSq, mm1);
    Map ultimate = floors[numOfLevels - 1];
    Loc downLoc = upstairs.Destination;
    Downstairs downstairs = (Downstairs)ultimate.TileAt(downLoc.Row, downLoc.Col);
    MysteriousMirror mm2 = new("") { Destination = downstairs.Destination };
    ultimate.SetTile(downLoc.Row, downLoc.Col, mm2);

    foreach (Map floor in floors)
    {
      towerDungeon.AddMap(floor);
    }

    Loc entrance = Loc.Nowhere;
    for (int r = 0; r < Height; r++)
    {
      for (int c = 0; c < Width; c++)
      {
        if (floors[0].TileAt(r, c).Type == TileType.Downstairs)
        {
          entrance = new(DungeonId, 0, r, c);
          break;
        }
      }
    }
    
    for (int lvl = 0; lvl < towerDungeon.LevelMaps.Count; lvl++)
    {
      Map map = towerDungeon.LevelMaps[lvl];

      List<Loc> doors = [];
      List<Loc> floorSqs = [];
      for (int r = 0; r < map.Height; r++)
      {
        for (int c = 0; c < map.Width; c++)
        {
          switch (map.TileAt(r, c).Type)
          {
            case TileType.DungeonFloor:
              Loc floor = new(DungeonId, lvl, r, c);
              if (Util.GoodFloorSpace(objDb, floor))
                floorSqs.Add(floor);
              break;
            case TileType.ClosedDoor:
            case TileType.OpenDoor:
              doors.Add(new(DungeonId, lvl, r, c));
              break;
          }
        }
      }

      if (lvl == towerDungeon.LevelMaps.Count - 2)
      {
        int i = rng.Next(floorSqs.Count);
        DecoyMirror1 = floorSqs[i];
        MysteriousMirror mm = new("") { Destination = Loc.Nowhere };
        towerDungeon.LevelMaps[lvl].SetTile(DecoyMirror1.Row, DecoyMirror1.Col, mm);
        floorSqs.RemoveAt(i);

        i = rng.Next(floorSqs.Count);
        DecoyMirror2 = floorSqs[i];
        mm = new("") { Destination = upstairs.Destination };
        towerDungeon.LevelMaps[lvl].SetTile(DecoyMirror2.Row, DecoyMirror2.Col, mm);
        floorSqs.RemoveAt(i);
      }

      // Sometimes replace a door with a mimic! Just the sort of thing a 
      // wizard would do!
      if (rng.Next(10) == 0 && doors.Count > 0)
      {
        Loc loc = doors[rng.Next(doors.Count)];
        map.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.DungeonFloor));
        Actor mimic = MonsterFactory.Mimic();
        objDb.AddNewActor(mimic, loc);
      }

      if (rng.Next(5) == 0)
      {
        AddMoldPatch(map, floorSqs, objDb, rng);
      }

      PopulateDungeon(towerDungeon, rng, objDb);

      AddTreasure(objDb, floorSqs, DungeonId, lvl, rng);
    }
    
    return (towerDungeon, entrance);
  }

  static void AddTreasure(GameObjectDB objDb, List<Loc> floors, int dungeonId, int level, Rng rng)
  {
    int numItems = rng.Next(2, 6);
    for (int j = 0; j < numItems; j++)
    {
      TreasureQuality quality;
      double roll = rng.NextDouble();
      if (roll <= 0.1)
        quality = TreasureQuality.Common;
      else if (roll <= 0.5)
        quality = TreasureQuality.Uncommon;
      else
        quality = TreasureQuality.Good;
      PlaceItem(Treasure.ItemByQuality(quality, objDb, rng));
    }

    for (int j = 0; j < rng.Next(1, 4); j++)
    {
      Item zorkmids = ItemFactory.Get(ItemNames.ZORKMIDS, objDb);
      zorkmids.Value = rng.Next(15, 36);
      PlaceItem(zorkmids);
    }

    void PlaceItem(Item item)
    {
      Loc loc = floors[rng.Next(floors.Count)];
      objDb.SetToLoc(loc, item);
    }
  }
}