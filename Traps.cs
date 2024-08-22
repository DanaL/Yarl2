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
  public static void TriggerTrap(GameState gs, Player player, Loc loc, Tile tile, bool flying)
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
        var msg = gs.ResolveActorMove(player, loc, newDest);
        if (msg != NullMessage.Instance)
          gs.WriteMessages([msg], "");
      }
    }
    else if (tile.Type == TileType.DartTrap || tile.Type == TileType.HiddenDartTrap)
    {
      gs.CurrentMap.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.DartTrap));
      gs.WriteMessages([new Message("A dart flies at you!", player.Loc)], "");

      Item dart = ItemFactory.Get(ItemNames.DART, gs.ObjDb);
      dart.Loc = loc;
      dart.Traits.Add(new PoisonCoatedTrait());
      dart.Traits.Add(new AdjectiveTrait("poisoned"));
      dart.Traits.Add(new PoisonerTrait() { DC = 11 + gs.CurrLevel, Strength = int.Max(1, gs.CurrLevel / 4) });

      int attackRoll = gs.Rng.Next(1, 21) + loc.Level / 3;
      if (attackRoll > player.AC)
      {
        ActionResult result = new();
        Battle.ResolveMissileHit(dart, player, dart, gs, result);
        if (result.Messages.Count > 0)
          gs.WriteMessages(result.Messages, "");
      }

      gs.ItemDropped(dart, loc);

      // To simulate eventually running out of ammuniation, each time the dart
      // trap is triggered there's a 5% chane of switching it to a dungeon floor
      if (gs.Rng.Next(20) == 0) 
      {
        gs.CurrentMap.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.DungeonFloor));
        gs.WriteMessages([new Message("Click.", player.Loc, false)], "");
      }
    }
  }
}
