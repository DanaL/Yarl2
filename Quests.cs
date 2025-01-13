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

    // To trim things town, we'll look for mountain tiles that have at least 
    // 3 non-mountain, non-water tiles, then pick a random one that is 
    // reachable from the witches' hut.
    List<Loc> opts = [];
    for (int r = 2; r < wilderness.Height - 2; r++)
    {
      for (int c = 2; c < wilderness.Width - 2; c++)
      {
        if (wilderness.TileAt(r, c).Type == TileType.Mountain && ProbablyOpen(wilderness, r, c))
        {
          opts.Add(new Loc(0, 0, r, c));
        }
      }
    }

    // pick start location
    List<Loc> witchSqs = [];
    foreach (Loc loc in gs.Town.WitchesCottage)
    {
      Tile tile = wilderness.TileAt(loc.Row, loc.Col);
      if (tile.Type == TileType.Grass || tile.IsTree())
        witchSqs.Add(loc);
    }
    Loc witches = witchSqs[gs.Rng.Next(witchSqs.Count)];

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

    while (opts.Count > 0)
    {
      int i = gs.Rng.Next(opts.Count);
      Loc loc = opts[i];
      opts.RemoveAt(i);

      // Start from the proposed entrance, otherwise pathfinding will fail
      // because mountains aren't open
      var path = AStar.FindPath(wilderness, loc, witches, costs, true);
      if (path.Count > 0)
        return loc;
    }

    // I'm not sure what to do if there are no valid locations? Just pick
    // some random tree or grass tile??

    return opts[0];
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

    map.Dump();
  }

  public static Dungeon GenerateDungeon(GameState gs)
  {
    int id = gs.Campaign.Dungeons.Keys.Max() + 1;
    Dungeon dungeon = new(id, "You shudder not from cold, but from sensing something unnatural within this cave.");

    bool[,] cave = CACave.GetCave(50, 50, gs.Rng);
    CACave.Dump(cave, 50, 50);

    Map map = new(52, 52);
    for (int j = 0; j < 52; j++)
    {
      map.SetTile(0, j, TileFactory.Get(TileType.PermWall));
      map.SetTile(51, j, TileFactory.Get(TileType.PermWall));
      map.SetTile(j, 0, TileFactory.Get(TileType.PermWall));
      map.SetTile(j, 51, TileFactory.Get(TileType.PermWall));
    }
    for (int r = 0; r < 50; r++)
    {
      for (int c = 0; c < 50; c++)
      {
        TileType tile = cave[r, c] ? TileType.DungeonFloor : TileType.DungeonWall;
        map.SetTile(r + 1, c + 1, TileFactory.Get(tile));
      }
    }

    JoinCaves(map, gs.Rng);

    return dungeon;
  }
}
