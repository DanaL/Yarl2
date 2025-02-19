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
    bool trapSqVisible = gs.LastPlayerFoV.Contains(actor.Loc);
    
    if (actor.HasTrait<IllusionTrait>())
      return;

    if (tile.Type == TileType.HiddenTrapDoor && !flying)
    {
      RevealTrap(tile, gs, loc);
      
      loc = gs.FallIntoTrapdoor(actor, loc);
      if (trapSqVisible)
        ui.AlertPlayer($"A trap door opens up underneath {actor.FullName}!");
      
      if (actor is Player player)
      {
        player.Stats[Attribute.Nerve].Change(-15);
        player.Running = false;
        ui.SetPopup(new Popup($"A trap door opens up underneath you!", "", -1, -1));
      }

      string s = gs.ThingTouchesFloor(loc);
      if (trapSqVisible)
        gs.UIRef().AlertPlayer(s);
      
      throw new AbnormalMovement(loc);
    }
    else if (tile.Type == TileType.TrapDoor && !flying)
    {
      loc = gs.FallIntoTrapdoor(actor, loc);
      ui.SetPopup(new Popup("You plummet into the trap door!", "", -1, -1));      
      gs.UIRef().AlertPlayer("You plummet into the trap door!");
      string s = gs.ThingTouchesFloor(loc);
      gs.UIRef().AlertPlayer(s);

      if (actor is Player player)
      {
        player.Stats[Attribute.Nerve].Change(-15);
      }

      throw new AbnormalMovement(loc);
    }
    else if (!flying && (tile.Type == TileType.HiddenPit || tile.Type == TileType.Pit))
    {
      if (actor is Player player) 
      {
        player.Stats[Attribute.Nerve].Change(-10);
        player.Running = false;
      }

      RevealTrap(tile, gs, loc);
            
      int total = 0;
      int damageDice = 1 + actor.Loc.Level / 5;
      for (int j = 0; j < damageDice; j++)
        total += gs.Rng.Next(6) + 1;
      List<(int, DamageType)> fallDmg = [ (total, DamageType.Blunt) ];
      var (hpLeft, _, _) = actor.ReceiveDmg(fallDmg, 0, gs, null, 1.0);
      if (hpLeft < 1)
      {        
        gs.ActorKilled(actor, "a fall", null);
      }

      actor.Traits.Add(new InPitTrait());

      string s = $"{actor.FullName.Capitalize()} {Grammar.Conjugate(actor, "tumble")} into a pit!";
      if (trapSqVisible)
        gs.UIRef().AlertPlayer(s);      
    }
    else if (tile.Type == TileType.HiddenTeleportTrap || tile.Type == TileType.TeleportTrap)
    {
      // Hmm I don't think I'll charge stress for teleport traps
      if (actor is Player player)
        player.Running = false;

      RevealTrap(tile, gs, loc);

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
      else if (trapSqVisible)
        gs.UIRef().AlertPlayer($"{actor.FullName.Capitalize()} disappears!");

      if (candidates.Count > 0)
      {
        Loc newDest = candidates[gs.Rng.Next(candidates.Count)];
        gs.ResolveActorMove(actor, loc, newDest);
      }
    }
    else if (tile.Type == TileType.DartTrap || tile.Type == TileType.HiddenDartTrap)
    {
      if (actor is Player player) 
      {
        player.Running = false;
        player.Stats[Attribute.Nerve].Change(-10);
      }

      RevealTrap(tile, gs, loc);

      if (trapSqVisible)
        gs.UIRef().AlertPlayer($"A dart flies at {actor.FullName}!");

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
      }

      gs.ItemDropped(dart, loc);

      // To simulate eventually running out of ammuniation, each time the dart
      // trap is triggered there's a 5% chane of switching it to a dungeon floor
      if (gs.Rng.Next(20) == 0) 
      {
        gs.CurrentMap.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.DungeonFloor));        
        if (trapSqVisible)
          gs.UIRef().AlertPlayer("Click.");
      }
    }
    else if (!flying && tile.Type == TileType.JetTrigger)
    {
      if (actor is Player player)
        player.Running = false;
      TriggerJetTrap((JetTrigger) tile, gs, actor);
    }
    else if (tile.Type == TileType.HiddenWaterTrap || tile.Type == TileType.WaterTrap)
    {
      RevealTrap(tile, gs, loc);

      string s;
      if (actor is Player player) 
      {
        player.Stats[Attribute.Nerve].Change(-10);
        player.Running = false;
        s = "You are soaked by a blast of water!";
      }
      else{
        s = $"{actor.FullName.Capitalize()} is soaked by a blast of water";
      }

      if (gs.LastPlayerFoV.Contains(actor.Loc))
        gs.UIRef().AlertPlayer(s);

      s = actor.Inventory.ApplyEffectToInv(DamageType.Wet, gs, loc);      
      if (gs.LastPlayerFoV.Contains(actor.Loc))
        gs.UIRef().AlertPlayer(s);
    }
    else if (tile.Type == TileType.HiddenMagicMouth || tile.Type == TileType.MagicMouth)
    {
      if (actor is Player player) 
      {
        player.Stats[Attribute.Nerve].Change(-5);
        player.Running = false;
      }
      RevealTrap(tile, gs, loc);

      List<string> msgs = [];
      if (gs.LastPlayerFoV.Contains(loc))
      {
       msgs.Add(gs.Rng.Next(3) switch
        {
          0 => "A magic mouth shouts, \"Get a load of this guy!\"",
          1 => "A magic mouth shouts, \"Hey we got an adventurer over here!\"",
          _ => "A magic mouth shrieks!"
        }); 
      }
      else
      {        
        msgs.Add(gs.Rng.Next(3) switch
        {
          0 => "Something shouts, \"Get a load of this guy!\"",
          1 => "Something shouts, \"Hey we got an adventurer over here!\"",
          _ => "You hear a shriek!"
        });
      }
      
      // Wake up nearby monsters within 10 squares      
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

      foreach (string s in msgs)
        gs.UIRef().AlertPlayer(s);
    }
    else if ((tile.Type == TileType.HiddenSummonsTrap || tile.Type == TileType.RevealedSummonsTrap) && actor is Player player)
    {
      // I'm only going to have the player set off summons trap...
      // because magic?
      
      List<Loc> opts = [];
      foreach (Loc adj in Util.Adj8Locs(player.Loc))
      {
        if (gs.TileAt(adj).Passable() && ! gs.ObjDb.Occupied(adj))
          opts.Add(adj);
      }

      if (opts.Count == 0)
        return;
      
      int numOfMonsters = gs.Rng.Next(int.Min(3, opts.Count)) + 1;
      for (int j = 0; j < numOfMonsters; j++)
      {
        int i = gs.Rng.Next(opts.Count);
        Loc spawnLoc = opts[i];
        opts.RemoveAt(i);
        Actor? monster = gs.LevelAppropriateMonster(spawnLoc.DungeonID, spawnLoc.Level);
        if (monster is not null)
        {          
          monster.Loc = spawnLoc;
          gs.ObjDb.Add(monster);
          gs.ObjDb.AddToLoc(spawnLoc, monster);
          gs.AddPerformer(monster);

          SqAnimation anim = new(gs, spawnLoc, Colours.LIGHT_BLUE, Colours.BLACK, '*');
          gs.UIRef().RegisterAnimation(anim);
        }
      }

      player.Stats[Attribute.Nerve].Change(-5);
      player.Running = false;
      gs.CurrentMap.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.DungeonFloor));

      if (player.HasTrait<BlindTrait>())
        gs.UIRef().AlertPlayer("You hear a loud roar and something appears beside you!");
      else 
      {
        gs.UIRef().AlertPlayer("You step on a summon monster trap!");
        gs.UIRef().AlertPlayer("There is a flash of light and smoke and monsters appear!");
      }      
    }
  }

  public static void RevealTrap(Tile tile, GameState gs, Loc loc)
  {
    TileType revealedType = RevealedTrapType(tile.Type);
    switch (tile.Type)
    {
      case TileType.HiddenPit:
      case TileType.HiddenDartTrap:
      case TileType.HiddenTrapDoor:
      case TileType.HiddenTeleportTrap:
      case TileType.HiddenSummonsTrap:
      case TileType.HiddenWaterTrap:
      case TileType.HiddenMagicMouth:
        gs.CurrentMap.SetTile(loc.Row, loc.Col, TileFactory.Get(revealedType));
        break;
      case TileType.JetTrigger:
        JetTrigger trigger = (JetTrigger)tile;
        trigger.Visible = true;
        break;
      case TileType.HiddenBridgeCollapseTrap:
        BridgeCollapseTrap bridgeCollapse = (BridgeCollapseTrap)tile;
        bridgeCollapse.Reveal();
        break;
    }
  }

  static TileType RevealedTrapType(TileType trap) => trap switch
  {
    TileType.TrapDoor => TileType.TrapDoor,
    TileType.HiddenTrapDoor => TileType.TrapDoor,
    TileType.HiddenTeleportTrap => TileType.TeleportTrap,
    TileType.TeleportTrap => TileType.TeleportTrap,
    TileType.HiddenDartTrap => TileType.DartTrap,
    TileType.DartTrap => TileType.DartTrap,
    TileType.JetTrigger => TileType.JetTrigger,
    TileType.HiddenPit => TileType.Pit,
    TileType.Pit => TileType.Pit,
    TileType.WaterTrap => TileType.WaterTrap,
    TileType.HiddenWaterTrap => TileType.WaterTrap,
    TileType.MagicMouth => TileType.MagicMouth,
    TileType.HiddenMagicMouth => TileType.MagicMouth,
    TileType.HiddenSummonsTrap => TileType.RevealedSummonsTrap,
    TileType.RevealedSummonsTrap => TileType.RevealedSummonsTrap,
    TileType.HiddenBridgeCollapseTrap => TileType.ReveealedBridgeCollapseTrap,
    TileType.ReveealedBridgeCollapseTrap => TileType.ReveealedBridgeCollapseTrap,
    _ => throw new Exception("RevealedTrapType() shouldn't be called on a non-trap square")
  };

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

    if (actor is Player player)
    {
      player.Stats[Attribute.Nerve].Change(-10);
    }
    
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

    if (gs.LastPlayerFoV.Contains(loc))
    {
      gs.UIRef().AlertPlayer("Whoosh!! A fire trap!");
      gs.UIRef().PlayAnimation(explosion, gs);
    }

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
        if (gs.LastPlayerFoV.Contains(loc))
          gs.UIRef().AlertPlayer($"{victim.FullName.Capitalize()} {Grammar.Conjugate(victim, "is")} caught in the flames!");
        
        var (hpLeft, _, _) = victim.ReceiveDmg(dmg, 0, gs, null, 1.0);
        if (hpLeft < 1)
        {
          gs.ActorKilled(victim, "flames", null);
        }
      }
    }
  }
}
