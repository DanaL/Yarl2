// Yarl2 - A roguelike computer RPG
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

class WitchQuest
{  
  public static Loc QuestEntrance(GameState gs)
  {
    static bool ProbablyOpen(Map map, int r, int c)
    {
      int blocked = 0;
      foreach (var adj in Util.Adj8Sqs(r, c))
      {
        TileType tile = map.TileAt(adj.Item1, adj.Item2).Type;
        switch (tile)
        {
          case TileType.Mountain:
          case TileType.SnowPeak:
          case TileType.DeepWater:
          case TileType.Water:
            blocked++;
            break;
        }
      }

      return blocked <= 5;
    }

    Map wilderness = gs.Campaign.Dungeons[0].LevelMaps[0];

    List<Loc> witchSqs = [];
    foreach (Loc loc in gs.Town.WitchesCottage)
    {
      Tile tile = wilderness.TileAt(loc.Row, loc.Col);
      if (tile.Type == TileType.Grass || tile.IsTree())
        witchSqs.Add(loc);
    }
    Loc witches = witchSqs[gs.Rng.Next(witchSqs.Count)];

    // For the entrance to the cave where the witches' quest goal, pick a 
    // location that's not too far from their hut, with a preference for a
    // mountain tile.


    int northRow = int.Max(2, witches.Row - 50);
    int southRow = int.Min(wilderness.Height, witches.Row + 50);
    int westCol = int.Max(2, witches.Col - 50);
    int eastCol = int.Min(wilderness.Width, witches.Col + 50);
    List<Loc> mountains = [];
    List<Loc> others = [];
    for (int r = northRow; r < southRow - 2; r++)
    {
      for (int c = westCol; c < eastCol - 2; c++)
      {
        // We don't want to be too close either
        if (Util.Distance(r, c, witches.Row, witches.Col) < 25)
          continue;

        if (Util.PtInSqr(r, c, gs.Town.Row, gs.Town.Col, gs.Town.Height, gs.Town.Width))
          continue;

        Tile tile = wilderness.TileAt(r, c);
        if (tile.Type == TileType.Mountain && ProbablyOpen(wilderness, r, c))
          mountains.Add(new Loc(0, 0, r, c));      
        else if (tile.Type == TileType.Grass || tile.IsTree())
          others.Add(new Loc(0, 0, r, c));
      }
    }

    Dictionary<TileType, int> costs = [];
    costs.Add(TileType.Grass, 1);
    costs.Add(TileType.Sand, 1);
    costs.Add(TileType.Dirt, 1);
    costs.Add(TileType.Bridge, 1);
    costs.Add(TileType.GreenTree, 1);
    costs.Add(TileType.RedTree, 1);
    costs.Add(TileType.OrangeTree, 1);
    costs.Add(TileType.YellowTree, 1);
    costs.Add(TileType.Conifer, 1);
    costs.Add(TileType.Water, 1);
    costs.Add(TileType.Well, 1);
    costs.Add(TileType.WoodWall, 1);
    costs.Add(TileType.StoneWall, 1);
    costs.Add(TileType.WoodFloor, 1);

    while (mountains.Count > 0)
    {
      int i = gs.Rng.Next(mountains.Count);
      Loc loc = mountains[i];
      mountains.RemoveAt(i);

      // Start from the proposed entrance, otherwise pathfinding will fail
      // because mountains aren't open
      var path = AStar.FindPath(wilderness, loc, witches, costs, true);
      if (path.Count > 0)
        return loc;
    }

    if (others.Count > 0)
    {
      return others[gs.Rng.Next(others.Count)];
    }

    // I'm not sure what to do if there are no valid locations? Is it
    // even possible?
    throw new Exception("I couldn't find a spot for the Witch Quest!");
  }

  static void JoinCaves(Map map, Random rng)
  {
    RegionFinder regionFinder = new(new DungeonPassable());
    var regions = regionFinder.Find(map, true, 4, TileType.DungeonWall);

    if (regions.Count == 1)
      return;

    int sqs = 0;
    int largest = -1;
    foreach (int k in regions.Keys)
    {
      if (regions[k].Count > sqs)
      {
        largest = k;
        sqs = regions[k].Count;
      }
    }

    Dictionary<TileType, int> travelCost = new() {
      { TileType.DungeonWall, 2 },
      { TileType.DungeonFloor, 1 }
    };
    List<int> caves = [.. regions.Keys];
    caves.Remove(largest);
    HashSet<(int, int)> mainCave = regions[largest];
    List<(int, int)> mainSqs = [.. mainCave];
    foreach (int i in caves)
    {
      List<(int, int)> cave = [.. regions[i]];
      var startSq = cave[rng.Next(cave.Count)];
      Loc start = new(0, 0, startSq.Item1, startSq.Item2);
      var endSqr = mainSqs[rng.Next(mainSqs.Count)];
      Loc end = new(0, 0, endSqr.Item1, endSqr.Item2);

      Stack<Loc> path = AStar.FindPath(map, start, end, travelCost, false);
      while (path.Count > 0)
      {
        var sq = path.Pop();
        map.SetTile(sq.Row, sq.Col, TileFactory.Get(TileType.DungeonFloor));
        // We don't have to draw the full path generated. We can stop when we 
        // cross regions
        if (mainCave.Contains((sq.Row, sq.Col)))
          break;
      }
    }
  }

  public static (Dungeon, Loc) GenerateDungeon(GameState gs, Loc entrance)
  {
    int id = gs.Campaign.Dungeons.Keys.Max() + 1;
    Dungeon dungeon = new(id, "You shudder not from cold, but from sensing something unnatural within this cave.");
    MonsterDeck deck = new();
    deck.Monsters.AddRange(["skeleton", "skeleton", "zombie", "zombie", "dire bat"]);
    dungeon.MonsterDecks.Add(deck);

    int caveHeight = 25;
    int caveWidth = 40;
    bool[,] cave = CACave.GetCave(caveHeight, caveWidth, gs.Rng);
    Map map = new(caveWidth + 2, caveHeight + 2, TileType.PermWall);
  
    List<(int, int)> floors = [];
    for (int r = 0; r < caveHeight; r++)
    {
      for (int c = 0; c < caveWidth; c++)
      {
        TileType tile = TileType.DungeonWall;
        if (cave[r, c])
        {
          tile = TileType.DungeonFloor;
          floors.Add((r + 1, c + 1));
        }
        map.SetTile(r + 1, c + 1, TileFactory.Get(tile));
      }
    }

    JoinCaves(map, gs.Rng);

    int i = gs.Rng.Next(floors.Count);
    var exitSq = floors[i];
    floors.RemoveAt(i);
    var exitStairs = new Upstairs("")
    {
      Destination = entrance
    };
    map.SetTile(exitSq, exitStairs);

    dungeon.AddMap(map);

    // Place some remains
    i = gs.Rng.Next(floors.Count);
    var sq = floors[i];
    floors.RemoveAt(i);
    Loc loc = new(id, 0, sq.Item1, sq.Item2);
    Item skull = ItemFactory.Get(ItemNames.SKULL, gs.ObjDb);
    gs.ObjDb.SetToLoc(loc, skull);
    ItemNames itemName = gs.Rng.NextDouble() < 0.8 ? ItemNames.DAGGER : ItemNames.SILVER_DAGGER;
    Item dagger = ItemFactory.Get(itemName, gs.ObjDb);
    gs.ObjDb.SetToLoc(loc, dagger);
    itemName = gs.Rng.NextDouble() < 0.5 ? ItemNames.QUARTERSTAFF : ItemNames.GENERIC_WAND;
    Item focus = ItemFactory.Get(itemName, gs.ObjDb);
    gs.ObjDb.SetToLoc(loc, focus);

    sq = floors[gs.Rng.Next(floors.Count)];
    loc = new Loc(id, 0, sq.Item1, sq.Item2);
    Item crystal = ItemFactory.Get(ItemNames.MEDITATION_CRYSTAL, gs.ObjDb);
    gs.ObjDb.SetToLoc(loc, crystal);
 
    // Add a few monsters to the cave
    int numOfMonsters = gs.Rng.Next(3, 6);
    for (int j = 0; j < numOfMonsters; j++)
    {
      string name = gs.Rng.Next(3) switch
      {
        0 => "skeleton",
        1 => "zombie",
        _ => "dire bat"
      };
      Actor m = MonsterFactory.Get(name, gs.ObjDb, gs.Rng);

      sq = floors[gs.Rng.Next(floors.Count)];
      loc = new Loc(id, 0, sq.Item1, sq.Item2);

      gs.ObjDb.AddNewActor(m, loc);      
    }

    // Add in a 'boss' monster
    string boss = gs.Rng.Next(3) switch
    {
      0 => "ghoul",
      1 => "shadow",
      _ => "phantom"
    };
    Actor b = MonsterFactory.Get(boss, gs.ObjDb, gs.Rng);
    gs.ObjDb.AddNewActor(b, loc);

    return (dungeon, new Loc(id, 0, exitSq.Item1, exitSq.Item2));
  }
}
