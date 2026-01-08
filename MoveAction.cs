// Delve - A roguelike computer RPG
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

// The code for MoveAction was getting lengthy enough that I figured I
// should move it to its own file

class MoveAction(GameState gameState, Actor actor, Loc loc, bool involuntary) : Action(gameState, actor)
{
  public Loc Loc { get; init; } = loc;
  protected bool Involuntary { get; init; } = involuntary;

  // TTD: for monsters, in their movement action setup, I should be looking
  // there to see if they are confused or tipsy and setting up the Involuntary
  // field. (Currently only setting it for the player)
  public static bool CanMoveTo(Actor actor, Map map, Loc loc, bool involuntary)
  {
    bool canFly = false;
    bool canSwim = false;
    bool waterWalking = false;
    bool confused = false;
    
    foreach (Trait t in actor.Traits)
    {
      if (t is FlyingTrait ft && ft.Active)
        canFly = true;
      else if (t is FloatingTrait flt && flt.Active)
        canFly = true;
      else if (t is SwimmerTrait)
        canSwim = true;
      else if (t is AmphibiousTrait)
        canSwim = true;
      else if (t is WaterWalkingTrait)
        waterWalking = true;
      else if (t is ConfusedTrait)
        confused = true;
    }

    Tile tile = map.TileAt(loc.Row, loc.Col);
    if (tile.Passable())
      return true;
    else if (canFly && tile.PassableByFlight())
      return true;
    else if (canSwim && (tile.Type == TileType.Lake || tile.Type == TileType.Underwater))
      return true;
    else if (actor is not Player && (tile.Type == TileType.Water || tile.Type == TileType.DeepWater))
    {
      if (waterWalking || confused || involuntary || canSwim)
        return true;
    }
    else if (actor is Player && tile.Type == TileType.DeepWater && (confused || involuntary))
    {
      return true;
    }
    else if (tile.Type == TileType.Chasm && (confused || involuntary))
    {
      return true;
    }
    else if (tile.Type == TileType.Lava && (involuntary || actor.Traits.Any(t => t is ImmunityTrait it && it.Type == DamageType.Fire)))
    {
      return true;
    }

    return false;
  }

  protected bool StuckOnLoc(Actor actor, GameState gs, UserInterface ui)
  {
    // Is something blocking your egress from your loc?
    if (gs.ObjDb.ItemsAt(Loc).Any(item => item.HasTrait<BlockTrait>()))
    {
      string msg;
      Item blockage = gs.ObjDb.ItemsAt(Loc).Where(item => item.HasTrait<BlockTrait>()).First();
      if (blockage.Traits.OfType<DescriptionTrait>().FirstOrDefault() is DescriptionTrait desc)
      {
        msg = $"{Grammar.Possessive(actor).Capitalize()} way is blocked by {desc.Text}.";
      }
      else
      {
        string name = blockage.HasTrait<PluralTrait>() ? $"some {blockage.Name}" : blockage.Name.IndefArticle();      
        msg = $"{Grammar.Possessive(actor).Capitalize()} way is blocked by {name}.";
      }

      if (actor is Player)
        gs.UIRef().SetPopup(new Popup(msg, "", -1, -1));
      ui.AlertPlayer(msg);

      return true;
    }

    // We have to check if the occupant is the Actor because in cases like 
    // tunneling into the floor, we move the actor onto their current location
    // in order to correctly resolve as though they had stepped onto the
    // square
    if (gs.ObjDb.Occupied(Loc) && gs.ObjDb.Occupant(Loc) != Actor)
    {
      return true;
    }
    
    // Check for environment hazards that might block the character 
    // (webs, mud, etc). 
    foreach (Item env in gs.ObjDb.EnvironmentsAt(actor.Loc))
    {
      if (env.Name == "mud" && gs.Rng.NextDouble() <= 0.6 && !(actor.HasTrait<FlyingTrait>() || actor.HasTrait<FloatingTrait>()))
      {
        string txt = $"{actor.FullName.Capitalize()} {Grammar.Conjugate(actor, "is")} stuck in mud!";
        ui.AlertPlayer(txt, gs, actor.Loc);
        return true;
      }

      if (env.Name == "grease" && gs.Rng.NextDouble() <= 0.6 && !(actor.HasTrait<FlyingTrait>() || actor.HasTrait<FloatingTrait>()))
      {
        string txt = $"{actor.FullName.Capitalize()} {Grammar.Conjugate(actor, "slip")} in grease!";
        ui.AlertPlayer(txt, gs, actor.Loc);
        return true;
      }

      StickyTrait? web = env.Traits.OfType<StickyTrait>().FirstOrDefault();
      if (web is not null && !actor.HasTrait<TeflonTrait>())
      {
        bool strCheck = actor.AbilityCheck(Attribute.Strength, web.DC, gs.Rng);
        if (!strCheck)
        {
          string txt = $"{actor.FullName.Capitalize()} {Grammar.Conjugate(actor, "is")} stuck to {env.Name.DefArticle()}!";
          ui.AlertPlayer(txt, gs, actor.Loc);
          return true;
        }
        else
        {
          var txt = $"{actor.FullName.Capitalize()} {Grammar.Conjugate(actor, "tear")} through {env.Name.DefArticle()}.";
          ui.AlertPlayer(txt, gs, actor.Loc);
          gs.ObjDb.RemoveItemFromGame(env.Loc, env);
        }
      }
    }

    // is the actor currently being grappled?
    if (actor.HasActiveTrait<GrappledTrait>())
    {
      GrappledTrait gt = actor.Traits.OfType<GrappledTrait>().First();
      if (actor.AbilityCheck(Attribute.Strength, gt.DC, gs.Rng))
      {
        actor.Traits.Remove(gt);
        string txt = $"{actor.FullName.Capitalize()} {Grammar.Conjugate(actor, "break")} free of the grapple!";
        ui.AlertPlayer(txt);        
      }
      else
      {
        string txt = $"{actor.FullName.Capitalize()} {Grammar.Conjugate(actor, "is")} grappled by ";
        GameObj? grappler = gs.ObjDb.GetObj(gt.GrapplerID);
        txt += $"{grappler!.FullName}!";
        ui.AlertPlayer(txt);
        ui.AlertPlayer($"{actor.FullName.Capitalize()} cannot get away!");

        return true;
      }
    }

    if (actor.HasTrait<InPitTrait>())
    {
      if (actor.AbilityCheck(Attribute.Strength, 13, gs.Rng))
      {
        string s = $"{actor.FullName.Capitalize()} {Grammar.Conjugate(actor, "crawl")} to the edge of the pit.";
        if (gs.LastPlayerFoV.ContainsKey(actor.Loc))
          ui.AlertPlayer(s);
        actor.Traits = [.. actor.Traits.Where(t => t is not InPitTrait)];
      }
      else if (Actor is Player)
      {        
        ui.AlertPlayer("You are still stuck in the pit.");
      }

      // return true regardless because even if you successfully escape, 
      // it still takes up your move action.
      return true;
    }

    TileType tile = gs.TileAt(actor.Loc).Type;
    if (tile == TileType.FrozenDeepWater || tile == TileType.FrozenWater || tile == TileType.FrozenLake)
    {
      int slipDC = actor.HasTrait<TipsyTrait>() ? 15 : 11;
      // For ice tiles, the actor needs to succeed on a dex check before moving, unless 
      // the Actor is flying, floating or a frozen creature
      if (actor.Traits.Any(t => t is FlyingTrait || t is FloatingTrait || t is FreezerTrait))
      {
        return false;
      }
      else if (actor.AbilityCheck(Attribute.Dexterity, slipDC, gs.Rng))
      {
        return false;
      }
      else
      {
        ui.AlertPlayer(MsgFactory.SlipOnIceMessage(actor, actor.Loc, gs));
        return true;
      }
    }

    return false;
  }

  public override double Execute()
  {
    base.Execute();
    UserInterface ui = GameState!.UIRef();

    if (StuckOnLoc(Actor!, GameState!, ui))
      return 1.0;

    if (!GameState.CurrentMap.InBounds(Loc.Row, Loc.Col))
    {
      // in theory this shouldn't ever happen...
      if (Actor is Player)
        ui.AlertPlayer("You cannot go that way!");

      return 0.0;
    }
    else if (!CanMoveTo(Actor!, GameState.CurrentMap, Loc, Involuntary))
    {
      return 0.0;
    }
    
    try
    {
      GameState.ResolveActorMove(Actor!, Actor!.Loc, Loc);
    }
    catch (AbnormalMovement abMov)
    {
      Actor!.Loc = abMov.Dest;
    }

    if (Actor is Player)
    {
      ui.AlertPlayer(GameState.LocDesc(Actor.Loc));
      GameState.Noise(Actor.Loc.Row, Actor.Loc.Col, Actor.GetMovementNoise());
    }

    return 1.0;
  }
}

// I think only the Player should ever call this aciton. Monsters/NPCs should
// be choosing specific Attack or Move actions
class BumpAction(GameState gameState, Actor actor, Loc loc, bool involuntary) : MoveAction(gameState, actor, loc, involuntary)
{
  readonly bool _bumpToOpen = gameState.Options!.BumpToOpen;
  readonly bool _lockedDoorMenu = gameState.Options.BumpForLockedDoors;
  
  public override double Execute()
  {
    UserInterface ui = GameState!.UIRef();
    Player player = GameState!.Player;

    // If the destination tile is a MonsterWall, the player attacks the monster
    if (GameState.TileAt(Loc) is MonsterWall mw)
    {
      // if the actor is swallowed by another creature, any direction they move
      // in will attack the monster
      if (GameState!.ObjDb.GetObj(mw.MonsterId) is Actor target)
      {
        Actor!.QueueAction(new MeleeAttackAction(GameState, Actor, target.Loc));
      }
      else
      {
        throw new Exception($"{Actor!.Name} was swallowed by something that doesn't seem to exist?");
      }      
    }
    // There are corner cases when I want to move the actor onto the sq they're already
    // on (like digging while in a pit and turning it into a trapdoor) to re-resolve
    // the effects of moving onto the sq, hence the Occupant ID != Actor.ID check
    else if (GameState!.ObjDb.Occupied(Loc) && GameState.ObjDb.Occupant(Loc)!.ID != Actor!.ID)
    {
      Actor? occ = GameState.ObjDb.Occupant(Loc);
      if (occ is not null && occ.Behaviour is VillagePupBehaviour)
      {
        string msg;
        if (GameState.Rng.NextDouble() < 0.5)
          msg = $"You pat {occ.FullName}.";
        else
          msg = $"You give {occ.FullName} some scritches.";
        GameState.UIRef().SetPopup(new Popup(msg, "", -1, -1));
      }
      else if (Actor.Traits.OfType<GrappledTrait>().FirstOrDefault() is GrappledTrait grappled && occ!.ID != grappled.GrapplerID)
      {
        if (GameState.ObjDb.GetObj(grappled.GrapplerID) is Actor grappler)
        {
          string s = $"You cannot attack {occ.FullName} while grappled by {grappler.FullName}!";
          GameState.UIRef().AlertPlayer(s);
        }
      }
      else if (occ is not null && !Battle.PlayerWillAttack(occ))
      {
        if (GameState.Options.BumpToChat)
        {
          ChatAction chat = new(GameState, GameState.Player) { Loc = Loc };
          GameState.Player.QueueAction(chat);
        }
        else
        {
          ui.AlertPlayer($"You don't want to attack {occ.FullName}!");
        }

        return 0.0;
      }
      else if (occ is not null && Actor.HasTrait<FrightenedTrait>())
      {
        ui.AlertPlayer("You are too frightened to attack!");
      }
      else if (occ is not null && occ.IsDisguised() && GameState.LastPlayerFoV.ContainsKey(occ.Loc))
      {
        DisguiseTrait dt = occ.Traits.OfType<DisguiseTrait>().First();
        ui.AlertPlayer($"Wait! That {dt.DisguiseForm} is actually {occ.Name.IndefArticle()}!");
        occ.Glyph = dt.TrueForm;
        dt.Disguised = false;
      }
      else
      {
        player.QueueAction(new MeleeAttackAction(GameState, player, Loc));
        return 0.0;
      }
    }
    else if (!CanMoveTo(player, GameState.CurrentMap, Loc, Involuntary))
    {
      Tile tile = GameState.CurrentMap.TileAt(Loc.Row, Loc.Col);
    
      if (_bumpToOpen && tile.Type == TileType.ClosedDoor)
      {
        player.QueueAction(new OpenDoorAction(GameState, Actor!, Loc));
        return 0.0;
      }
      else if (_lockedDoorMenu && tile.Type == TileType.LockedDoor)
      {
        ui.AlertPlayer(BlockedMessage(tile));
        GameState.UIRef().SetInputController(new LockedDoorMenu(ui, GameState, Loc));
        return 0.0;
      }
      else if (!GameState.InWilderness && tile.Type == TileType.DeepWater)
      {
        // If we are in the dungeon, we'll let the player jump into rivers
        // (and/or they can stumble in while confused, etc)

        if (GameState.CurrentDungeon.RememberedLocs.ContainsKey(Loc))
        {
          ui.SetPopup(new Popup("Really jump into the water? (y/n)", "", -1, -1));
          YesOrNoInputer yn = new(GameState)
          {
            DeferredAction = new DiveAction(GameState, player, Loc, true)
          };
          ui.SetInputController(yn);          
        }
        else
        {
          GameState.RememberLoc(Loc, tile);
          player.QueueAction(new DiveAction(GameState, player, Loc, false));
        }

        return 0.0;
      }
      else if (tile.Type == TileType.Chasm)
      {
        if (GameState.CurrentDungeon.RememberedLocs.ContainsKey(Loc))
        {
          ui.SetPopup(new Popup("Really jump into the chasm? (y/n)", "", -1, -1));
          YesOrNoInputer yn = new(GameState)
          {
            DeferredAction = new DiveAction(GameState, player, Loc, true)
          };
          ui.SetInputController(yn);
        }
        else
        {
          GameState.RememberLoc(Loc, tile);
          player.QueueAction(new DiveAction(GameState, player, Loc, false));
        }

        return 0.0;
      }
      else if (tile.Type == TileType.Lever)
      {
        Lever lever = (Lever)tile;
        lever.Activate(GameState);
      }
      else if (tile.Type == TileType.BridgeLever)
      {
        // This specific to the drawbridge in the first level of the Gaol.
        // If I add more drawbridges, I'll need to generalize this and probably
        // create a separate class from Lever.
        Lever lever = (Lever)tile;
        if (lever.On)
          GameState.UIRef().AlertPlayer("This lever has already been triggered.");
        else
        {
          GameState.UIRef().AlertPlayer("You hear a rumble and the grinding of machinery.");
          lever.On = true;
          Loc bridge = lever.Gate;
          do 
          {
            GameState.CurrentMap.SetTile(bridge.Row, bridge.Col, TileFactory.Get(TileType.Bridge));
            bridge.Row += 1;
          }
          while (!(GameState.TileAt(bridge).Type == TileType.DungeonFloor || GameState.TileAt(bridge).Type == TileType.Downstairs));
        }
      }
      else
      {
        ui.AlertPlayer(BlockedMessage(tile), GameState, Loc);
      }
      
      // If the player is blind, remember what tile they bumped into
      // so that it displays on screen
      if (player.HasTrait<BlindTrait>())
        GameState.RememberLoc(Loc, tile);
    }
    else
    {
      return base.Execute();
    }

    return 1.0;
  }

  static string BlockedMessage(Tile tile) => tile.Type switch
  {
    TileType.DeepWater => "The water seems deep and cold.",
    TileType.Mountain or TileType.SnowPeak => "You cannot scale the mountain!",
    TileType.Chasm => "Do you really want to jump into the chasm?",
    TileType.Portcullis => "The portcullis is closed.",
    TileType.VaultDoor => "Metal doors bar your path!",
    TileType.LockedDoor => "The door is locked!",
    TileType.DungeonWall => "",
    TileType.Lava => "Entering the lava seems like a terrible idea.",
    TileType.Arioch => "The awful visage of a Demon Lord, writhing in its shackles!",
    _ => "You cannot go that way!"
  };
}