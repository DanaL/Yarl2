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

class Traps
{
  public static Message TriggerTrap(GameState gs, Player player, Loc loc, Tile tile, bool flying)
  {
    UserInterface ui = gs.UIRef();

    if (tile.Type == TileType.Pit && !flying)
    {
      gs.CurrentMap.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.OpenPit));
      loc = gs.FallIntoPit(player, loc);
      ui.SetPopup(new Popup("A pit opens up underneath you!", "", -1, -1));
      List<Message> msgs = [new Message("A pit opens up underneath you!", loc, false)];
      msgs.Add(gs.ThingAddedToLoc(loc));
     gs. WriteMessages(msgs, "");
      throw new AbnormalMovement(loc);
    }
    else if (tile.Type == TileType.OpenPit && !flying)
    {
      loc = gs.FallIntoPit(player, loc);
      ui.SetPopup(new Popup("You tumble into the pit!", "", -1, -1));
      List<Message> msgs = [new Message("You tumble into the pit!", loc, false)];
      msgs.Add(gs.ThingAddedToLoc(loc));
      gs.WriteMessages(msgs, "");
      throw new AbnormalMovement(loc);
    }
    else if (tile.Type == TileType.HiddenTeleportTrap || tile.Type == TileType.TeleportTrap)
    {
      gs.CurrentMap.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.TeleportTrap));

      // Find candidate locations to teleport to
      List<Loc> candidates = [];
      for (int r = 0; r < gs.CurrentMap.Height; r++)
      {
        for (int c = 0; c < gs.CurrentMap.Width; c++)
        {
          var dest = new Loc(gs.CurrDungeonID, gs.CurrLevel, r, c);
          if (gs.CurrentMap.TileAt(r, c).Type == TileType.DungeonFloor && !gs.ObjDb.Occupied(loc))
            candidates.Add(loc);
        }
      }

      gs.WriteMessages([new Message("Your stomach lurches!", player.Loc, false)], "");
      if (candidates.Count > 0)
      {
        Loc newDest = candidates[gs.Rng.Next(candidates.Count)];
        return gs.ResolveActorMove(player, loc, newDest);
      }
    }

    return NullMessage.Instance;
  }
}
