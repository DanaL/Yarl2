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
  public static void TriggerTrap(GameState gs, Actor actor, Loc loc, Tile tile, bool flying)
  {
    UserInterface ui = gs.UIRef();

    if (tile.Type == TileType.HiddenTrapDoor && !flying)
    {
      if (actor is Player player)
        player.Running = false;
      gs.CurrentMap.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.TrapDoor));
      loc = gs.FallIntoPit(actor, loc);
      ui.SetPopup(new Popup("A trap door opens up underneath you!", "", -1, -1));
      List<string> msgs = [ "A trap door opens up underneath you!" ];
      msgs.Add(gs.ThingAddedToLoc(loc));
      gs.UIRef().AlertPlayer(msgs);
      
      throw new AbnormalMovement(loc);
    }
    else if (tile.Type == TileType.TrapDoor && !flying)
    {
      loc = gs.FallIntoPit(actor, loc);
      ui.SetPopup(new Popup("You plummet into the trap door!", "", -1, -1));
      List<string> msgs = [ "You plummet into the trap door!" ];
      msgs.Add(gs.ThingAddedToLoc(loc));
      gs.UIRef().AlertPlayer(msgs);

      throw new AbnormalMovement(loc);
    }
    else if (!flying && (tile.Type == TileType.HiddenPit || tile.Type == TileType.Pit))
    {
      if (actor is Player player)
        player.Running = false;
      gs.CurrentMap.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.Pit));

      ActionResult result = new() { Messages = [ "You tumble into a pit!" ]};
      int total = 0;
      int damageDice = 1 + actor.Loc.Level / 5;
      for (int j = 0; j < damageDice; j++)
        total += gs.Rng.Next(6) + 1;
      List<(int, DamageType)> fallDmg = [ (total, DamageType.Blunt) ];
      var (hpLeft, _) = actor.ReceiveDmg(fallDmg, 0, gs, null, 1.0);
      if (hpLeft < 1)
      {        
        gs.ActorKilled(actor, "a fall", result, null);
      }

      actor.Traits.Add(new InPitTrait());

      gs.UIRef().AlertPlayer(result.Messages);
    }
    else if (tile.Type == TileType.HiddenTeleportTrap || tile.Type == TileType.TeleportTrap)
    {
      if (actor is Player player)
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

      if (actor is Player)
        gs.UIRef().AlertPlayer("Your stomach lurches!");

      if (candidates.Count > 0)
      {
        Loc newDest = candidates[gs.Rng.Next(candidates.Count)];
        string msg = gs.ResolveActorMove(actor, loc, newDest);
        if (msg != "")
          gs.UIRef().AlertPlayer(msg);
      }
    }
    else if (tile.Type == TileType.DartTrap || tile.Type == TileType.HiddenDartTrap)
    {
      if (actor is Player player)
        player.Running = false;
      gs.CurrentMap.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.DartTrap));
      gs.UIRef().AlertPlayer("A dart flies at you!");

      Item dart = ItemFactory.Get(ItemNames.DART, gs.ObjDb);
      dart.Loc = loc;
      dart.Traits.Add(new PoisonCoatedTrait());
      dart.Traits.Add(new AdjectiveTrait("poisoned"));
      dart.Traits.Add(new PoisonerTrait() { DC = 11 + gs.CurrLevel, Strength = int.Max(1, gs.CurrLevel / 4), Duration = 10 });

      int attackRoll = gs.Rng.Next(1, 21) + loc.Level / 3;
      if (attackRoll > actor.AC)
      {
        ActionResult result = new();
        Battle.ResolveMissileHit(dart, actor, dart, gs, result);
        gs.UIRef().AlertPlayer(result.Messages);
      }

      gs.ItemDropped(dart, loc);

      // To simulate eventually running out of ammuniation, each time the dart
      // trap is triggered there's a 5% chane of switching it to a dungeon floor
      if (gs.Rng.Next(20) == 0) 
      {
        gs.CurrentMap.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.DungeonFloor));
        gs.UIRef().AlertPlayer("Click.");
      }
    }
    else if (tile.Type == TileType.JetTrigger)
    {
      if (actor is Player player)
        player.Running = false;
      TriggerJetTrap((JetTrigger) tile, gs, actor);
    }
    else if (tile.Type == TileType.HiddenWaterTrap || tile.Type == TileType.WaterTrap)
    {
      if (actor is Player player)
        player.Running = false;
      gs.CurrentMap.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.WaterTrap));
      List<string> msgs = [ "You are soaked by a blast of water!" ];      
      string s = actor.Inventory.ApplyEffectToInv(EffectFlag.Wet, gs, loc);
      if (s != "")
        msgs.Add(s);
      gs.UIRef().AlertPlayer(msgs);
    }
    else if (tile.Type == TileType.HiddenMagicMouth || tile.Type == TileType.MagicMouth)
    {
      if (actor is Player player)
        player.Running = false;
      gs.CurrentMap.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.MagicMouth));
      
      string s = gs.Rng.Next(3) switch
      {
        0 => "A magic mouth shouts, \"Get a load of this guy!\"",
        1 => "A magic mouth shouts, \"Hey we got an adventurer over here!\"",
        _ => "A magic mouth shrieks!"
      };

      // Wake up nearby monsters within 10 squares
      List<string> msgs = [ s ];
      for (int r = loc.Row - 10; r <= loc.Row + 10; r++)
      {
        for (int c = loc.Col - 10; c <= loc.Col + 10; c++)
        {
          if (!gs.CurrentMap.InBounds(r, c))
            continue;
          
          var checkLoc = new Loc(gs.CurrDungeonID, gs.CurrLevel, r, c);
          if (gs.ObjDb.Occupant(checkLoc) is Actor monster && monster != actor)
          {
            if (monster.Stats.TryGetValue(Attribute.MobAttitude, out var attitude) && attitude.Curr != Mob.AFRAID)
            {
              attitude.SetMax(Mob.AGGRESSIVE);
            }

            var sleeping = monster.Traits.FirstOrDefault(t => t is SleepingTrait);
            if (sleeping is not null)
            {
              monster.Traits.Remove(sleeping);
              if (gs.LastPlayerFoV.Contains(checkLoc))
                msgs.Add($"{monster.FullName.Capitalize()} wakes up!");
            }
          }          
        }
      }
      gs.UIRef().AlertPlayer(msgs);
    }
  }

  static void TriggerJetTrap(JetTrigger trigger, GameState gs, Actor actor)
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

    gs.UIRef().AlertPlayer("Whoosh!! A fire trap!");
    gs.UIRef().PlayAnimation(explosion, gs);

    ActionResult result = new();
    int total = 0;
    int damageDice = 2 + actor.Loc.Level / 4;
    for (int j = 0; j < damageDice; j++)
      total += gs.Rng.Next(6) + 1;
    List<(int, DamageType)> dmg = [(total, DamageType.Fire)];
    foreach (var pt in affected)
    {
      gs.ApplyDamageEffectToLoc(pt, DamageType.Fire);
      if (gs.ObjDb.Occupant(pt) is Actor victim)
      {
        result.Messages.Add($"{victim.FullName.Capitalize()} {Grammar.Conjugate(victim, "is")} caught in the flames!");
        
        var (hpLeft, _) = victim.ReceiveDmg(dmg, 0, gs, null, 1.0);
        if (hpLeft < 1)
        {
          gs.ActorKilled(victim, "flames", result, null);
        }
      }
    }

    gs.UIRef().AlertPlayer(result.Messages);
  }
}
