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

  public (Dungeon, Loc) Generate(int entranceRow, int entranceCol, GameObjectDB objDb, Rng rng)
  {
    Dungeon towerDungeon = new(DungeonId, "a Musty Tower", "Ancient halls that smell of dust and magic.", false)
    {
      MonsterDecks = DeckBuilder.ReadDeck("tower", rng)
    };

    Tower towerBuilder = new(Height, Width, 5);
    Map[] floors = [..towerBuilder.BuildLevels(5, rng)];

    SetStairs(DungeonId, floors, Height, Width, 5, (entranceRow, entranceCol), false, rng);

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

    PopulateDungeon(towerDungeon, rng, objDb);

    // Sometimes replace a door with a mimic. Just the sort of thing a wizard
    // would do
    for (int lvl = 0; lvl < towerDungeon.LevelMaps.Count; lvl++)
    {
      if (rng.Next(10) > 0)
        continue;

      Map map = towerDungeon.LevelMaps[lvl];
      List<(int, int)> doors = [.. map.SqsOfType(TileType.ClosedDoor).Concat(map.SqsOfType(TileType.OpenDoor))];
      if (doors.Count == 0)
        continue;

      (int mr, int mc) = doors[rng.Next(doors.Count)];
      map.SetTile(mr, mc, TileFactory.Get(TileType.DungeonFloor));

      Actor mimic = MonsterFactory.Mimic();
      objDb.AddNewActor(mimic, new Loc(DungeonId, lvl, mr, mc));

      if (rng.Next(5) == 0)
      {
        AddMoldPatch(DungeonId, lvl, map, objDb, rng);
      }

      AddTreasure(objDb, map, DungeonId, lvl, rng);
    }
    
    return (towerDungeon, entrance);
  }

  static void AddTreasure(GameObjectDB objDb, Map map, int dungeonId, int level, Rng rng)
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
      Item item = Treasure.ItemByQuality(quality, objDb, rng);
      Treasure.AddObjectToLevel(item, objDb, map, dungeonId, level, rng);
    }

    for (int j = 0; j < rng.Next(1, 4); j++)
    {
      Item zorkmids = ItemFactory.Get(ItemNames.ZORKMIDS, objDb);
      zorkmids.Value = rng.Next(15, 36);
      Treasure.AddObjectToLevel(zorkmids, objDb, map, dungeonId, level, rng);
    }
  }
}