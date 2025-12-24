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

using Yarl2;

class EndGameDungeonBuilder(int dungeonId, Loc entrance) : DungeonBuilder
{
  const int HEIGHT = 40;
  const int WIDTH = 70;
  int DungeonId { get; set; } = dungeonId;
  Loc Entrance { get; set; } = entrance;
  
  public Dungeon Generate(GameState gs)
  {
    Dungeon dungeon = new(DungeonId, "the Gaol", "Sulphur. Heat. Mortals were not meant for this place.", true);
    DungeonMap mapper = new(gs.Rng);
    Map[] levels = new Map[5];

    //dungeon.MonsterDecks = DeckBuilder.ReadDeck(MainOccupant, rng);

    for (int levelNum = 0; levelNum < 5; levelNum++)
    {
      levels[levelNum] = mapper.DrawLevel(WIDTH, HEIGHT);
      dungeon.AddMap(levels[levelNum]);

      AddSecretDoors(levels[levelNum], gs.Rng);
    }
    
    AddRiverToLevel(new(TileType.Lava, true, true), levels[0], levels[1], 0, HEIGHT, WIDTH, DungeonId, gs.ObjDb, gs.Rng);

    SetStairs(DungeonId, levels, (Entrance.Row, Entrance.Col), dungeon.Descending, gs.Rng);
    dungeon.ExitLoc = new(DungeonId, 0, ExitLoc.Item1, ExitLoc.Item2);
    
    return dungeon;
  }
}

class EndGame
{
  public static int CostNearby(Tile tile)
  {
    switch (tile.Type) 
    {
      case TileType.Mountain:
      case TileType.SnowPeak:
        return 2;
      default:
        if (tile.Passable())
          return 1;
        break;
    }

    return int.MaxValue;
  }

  public static void Setup(GameState gs)
  {
    if (gs.FactDb.FactCheck("Dungeon Entrance") is not LocationFact entranceFact)
      throw new Exception("Missing Dungeon Entrance fact. Cannot build end game.");

    Loc initialDungeon = entranceFact.Loc;

    DijkstraMap dmap = new(gs.Wilderness, [], gs.Wilderness.Height, gs.Wilderness.Width, true);
    dmap.Generate(CostNearby, (initialDungeon.Row, initialDungeon.Col), 10);

    List<Loc> mountains = [];
    List<Loc> passable = [];
    for (int r = initialDungeon.Row - 3; r <= initialDungeon.Row + 3; r++)
    {
      for (int c = initialDungeon.Col -3; c <= initialDungeon.Col + 3; c++)
      {
        Loc loc = initialDungeon with { Row = r, Col = c };
        Tile tile = gs.TileAt(loc);
        if (tile.Type == TileType.Mountain || tile.Type == TileType.SnowPeak)
          mountains.Add(loc);
        else if (tile.Passable())
          passable.Add(loc);
      }
    }

    Loc finalDungeonLoc = Loc.Nowhere;
    if (mountains.Count > 0)
     finalDungeonLoc = mountains[gs.Rng.Next(mountains.Count)];
    else if (passable.Count > 0)
      finalDungeonLoc = passable[gs.Rng.Next(passable.Count)];
    
    if (finalDungeonLoc == Loc.Nowhere)
      throw new Exception("Could not place final dungeon location!");

    var path = dmap.ShortestPath(finalDungeonLoc.Row, finalDungeonLoc.Col);
    foreach (var loc in path)
    {
      if (!gs.Wilderness.TileAt(loc).Passable())
        gs.Wilderness.SetTile(loc, TileFactory.Get(TileType.Dirt));
    }

    EndGameDungeonBuilder db = new (gs.Campaign.Dungeons.Count, finalDungeonLoc);
    Dungeon dungeon = db.Generate(gs);
    gs.Campaign.AddDungeon(dungeon);

    Portal portal = new("A smouldering arch covered in profane sigils.", TileType.ProfanePortal)
    {
      Destination = dungeon.ExitLoc
    };

    gs.Wilderness.SetTile(finalDungeonLoc.Row, finalDungeonLoc.Col, portal);
  }
}