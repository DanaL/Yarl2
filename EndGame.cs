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

    Portal portal = new("A smouldering arch covered in profane sigils.", TileType.ProfanePortal)
    {
      Destination = finalDungeonLoc
    };
 
    gs.Wilderness.SetTile(finalDungeonLoc.Row, finalDungeonLoc.Col, portal);
  }
}