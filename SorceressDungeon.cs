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

  public static (Dungeon, Loc) WumpusDungeon(Loc tower, int DungeonId, GameObjectDB objDb, Rng rng)
  {
    Dungeon dungeon = new(DungeonId, "a Noisome Cavern", "You are in Room 1.", false);

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
        if (map.TileAt(r,c).Type == TileType.DungeonFloor && CountAdjFloors(map, r, c) > 2)
        {
          floorSqs.Add((r, c));
        }
      }
    }
    List<(int, int)> floorsInChambers = [.. floorSqs];

    int i = rng.Next(floorsInChambers.Count);
    Loc arrival = new(DungeonId, 0, floorsInChambers[i].Item1, floorsInChambers[i].Item2);
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

        //i = rng.Next(floorSqs.Count);
        //DecoyMirror2 = floorSqs[i];
        //mm = new("") { Destination = upstairs.Destination };
        //towerDungeon.LevelMaps[lvl].SetTile(DecoyMirror2.Row, DecoyMirror2.Col, mm);
        //floorSqs.RemoveAt(i);
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