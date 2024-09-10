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

    if (tile.Type == TileType.HiddenTrapDoor && !flying)
    {
      player.Running = false;
      gs.CurrentMap.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.TrapDoor));
      loc = gs.FallIntoPit(player, loc);
      ui.SetPopup(new Popup("A trap door opens up underneath you!", "", -1, -1));
      List<Message> msgs = [new Message("A trap door opens up underneath you!", loc, false)];
      msgs.Add(gs.ThingAddedToLoc(loc));
      gs. WriteMessages(msgs, "");

      throw new AbnormalMovement(loc);
    }
    else if (tile.Type == TileType.TrapDoor && !flying)
    {
      loc = gs.FallIntoPit(player, loc);
      ui.SetPopup(new Popup("You plummet into the trap door!", "", -1, -1));
      List<Message> msgs = [new Message("You plummet into the trap door!", loc, false)];
      msgs.Add(gs.ThingAddedToLoc(loc));
      gs.WriteMessages(msgs, "");

      throw new AbnormalMovement(loc);
    }
    else if (!flying && (tile.Type == TileType.HiddenPit || tile.Type == TileType.Pit))
    {
      player.Running = false;
      gs.CurrentMap.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.Pit));

      ActionResult result = new() { Messages = [(new Message("You tumble into a pit!", loc))] };
      int total = 0;
      int damageDice = 1 + player.Loc.Level / 5;
      for (int j = 0; j < damageDice; j++)
        total += gs.Rng.Next(6) + 1;
      List<(int, DamageType)> fallDmg = [ (total, DamageType.Blunt) ];
      var (hpLeft, _) = player.ReceiveDmg(fallDmg, 0, gs);
      if (hpLeft < 1)
      {        
        gs.ActorKilled(player, "a fall", result);
      }

      player.Traits.Add(new InPitTrait());

      gs.WriteMessages(result.Messages, "");
    }
    else if (tile.Type == TileType.HiddenTeleportTrap || tile.Type == TileType.TeleportTrap)
    {
      player.Running = false;
      gs.CurrentMap.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.TeleportTrap));

      // Find candidate locations to teleport to
      List<Loc> candidates = [];
      for (int r = 0; r < gs.CurrentMap.Height; r++)
      {
        for (int c = 0; c < gs.CurrentMap.Width; c++)
        {
          var dest = new Loc(gs.CurrDungeonID, gs.CurrLevel, r, c);
          if (gs.CurrentMap.TileAt(r, c).Type == TileType.DungeonFloor && !gs.ObjDb.Occupied(dest))
            candidates.Add(dest);
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
      player.Running = false;
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
    else if (tile.Type == TileType.JetTrigger)
    {
      player.Running = false;
      TriggerJetTrap((JetTrigger) tile, gs, player);
    }
    else if (tile.Type == TileType.HiddenWaterTrap || tile.Type == TileType.WaterTrap)
    {
      player.Running = false;
      gs.CurrentMap.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.WaterTrap));
      List<Message> msgs = [new Message("You are soaked by a blast of water!", loc)];      
      string s = player.Inventory.ApplyEffectToInv(EffectFlag.Wet, gs, loc);
      if (s != "")
        msgs.Add(new Message(s, loc));
      gs.WriteMessages(msgs, "");
    }
  }

  static void TriggerJetTrap(JetTrigger trigger, GameState gs, Player player)
  {
    trigger.Visible = true;

    FireJetTrap jet = (FireJetTrap) gs.TileAt(trigger.JetLoc);
    jet.Seen = true;
    (int, int) delta = jet.Dir switch 
    {
      Dir.North => (-1, 0),
      Dir.South => (1, 0),
      Dir.East => (0, 1),
      _ => (0, -1)
    };

    HashSet<Loc> affected = [];
    Loc start = trigger.JetLoc with { Row = trigger.JetLoc.Row + delta.Item1, Col = trigger.JetLoc.Col + delta.Item2 };
    affected.Add(start);
    Loc loc = start;
    for (int j = 0; j < 5; j++)
    {
      loc = loc with { Row = loc.Row + delta.Item1, Col = loc.Col + delta.Item2 };
      if (!gs.TileAt(loc).PassableByFlight())
        break;
      affected.Add(loc);
    }
    
    var explosion = new ExplosionAnimation(gs!)
    {
      MainColour = Colours.BRIGHT_RED,
      AltColour1 = Colours.YELLOW,
      AltColour2 = Colours.YELLOW_ORANGE,
      Highlight = Colours.WHITE,
      Centre = start,
      Sqs = affected
    };

    gs.WriteMessages([new Message("Whoosh!! A fire trap!", player.Loc)], "");
    gs.UIRef().PlayAnimation(explosion, gs);

    ActionResult result = new();
    int total = 0;
    int damageDice = 2 + player.Loc.Level / 4;
    for (int j = 0; j < damageDice; j++)
      total += gs.Rng.Next(6) + 1;
    List<(int, DamageType)> dmg = [(total, DamageType.Fire)];
    foreach (var pt in affected)
    {
      gs.ApplyDamageEffectToLoc(pt, DamageType.Fire);
      if (gs.ObjDb.Occupant(pt) is Actor victim)
      {
        result.Messages.Add(new Message($"{victim.FullName.Capitalize()} {Grammar.Conjugate(victim, "is")} caught in the flames!", pt));
        
        var (hpLeft, dmgMsg) = victim.ReceiveDmg(dmg, 0, gs);
        if (hpLeft < 1)
        {
          gs.ActorKilled(victim, "flames", result);
        }
      }
    }

    gs.WriteMessages(result.Messages, "");
  }
}
