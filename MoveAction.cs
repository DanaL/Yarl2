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

// The code for MoveAction was getting lengthy enough that I figured I
// should move it to its own file

class MoveAction(GameState gameState, Actor actor, Loc loc) : Action(gameState, actor)
{
  public Loc Loc { get; init; } = loc;
  
  public static bool CanMoveTo(Actor actor, Map map, Loc loc)
  {
    static bool CanFly(Actor actor)
    {
      return actor.HasActiveTrait<FlyingTrait>() ||
                actor.HasActiveTrait<FloatingTrait>();
    }

    var tile = map.TileAt(loc.Row, loc.Col);
    if (tile.Passable())
      return true;
    else if (CanFly(actor) && tile.PassableByFlight())
      return true;
    else if (tile.Type == TileType.Water || tile.Type == TileType.DeepWater || tile.Type == TileType.Chasm)
    {
      foreach (Trait t in actor.Traits)
      {
        if (t is WaterWalkingTrait)
          return true;
        if (t is ConfusedTrait)
          return true;
        if (t is TipsyTrait)
          return true;
      }
    }

    return false;
  }

  protected bool StuckOnLoc(Actor actor, ActionResult result, GameState gs, UserInterface ui)
  {
    // Is something blocking your egress from your loc?
    if (gs.ObjDb.ItemsAt(Loc).Any(item => item.HasTrait<BlockTrait>()))
    {
      Item blockage = gs.ObjDb.ItemsAt(Loc).Where(item => item.HasTrait<BlockTrait>()).First();
      if (blockage.Type == ItemType.Statue)
      {
        string msg;
        if (blockage.Traits.OfType<DescriptionTrait>().FirstOrDefault() is DescriptionTrait desc)
          msg = desc.Text;
        else
          msg = $"{Grammar.Possessive(actor).Capitalize()} way is blocked by a statue.";

        if (actor is Player)
          gs.UIRef().SetPopup(new Popup(msg, "a statue", -1, -1));
        ui.AlertPlayer(msg);
      }
      else
      {
        string msg = $"{Grammar.Possessive(actor).Capitalize()} way is blocked by ";
        if (blockage.HasTrait<PluralTrait>())
          msg += $"some {blockage.Name}!";
        else
          msg += $"{blockage.Name.IndefArticle()}!";
        ui.AlertPlayer(msg);
      }

      result.Succcessful = false;
      result.EnergyCost = 0.0;

      return true;
    }

    // We have to check if the occupant is the Actor because in cases like 
    // tunneling into the floor, we move the actor onto their current location
    // in order to correctly resolve as though they had stepped onto the
    // square
    if (gs.ObjDb.Occupied(Loc) && gs.ObjDb.Occupant(Loc) != Actor)
    {
      result.Succcessful = false;
      result.EnergyCost = 0.0;
      return true;
    }
    
    // Check for webs
    foreach (var env in gs.ObjDb.EnvironmentsAt(actor.Loc))
    {
      var web = env.Traits.OfType<StickyTrait>().FirstOrDefault();
      if (web is not null && !actor.HasTrait<TeflonTrait>())
      {
        bool strCheck = actor.AbilityCheck(Attribute.Strength, web.DC, gs.Rng);
        if (!strCheck)
        {
          result.EnergyCost = 1.0;
          result.Succcessful = false;
          var txt = $"{actor.FullName.Capitalize()} {Grammar.Conjugate(actor, "is")} stuck to {env.Name.DefArticle()}!";
          ui.AlertPlayer(txt);
          return true;
        }
        else
        {
          var txt = $"{actor.FullName.Capitalize()} {Grammar.Conjugate(actor, "tear")} through {env.Name.DefArticle()}.";
          ui.AlertPlayer(txt);
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
        result.Succcessful = false;
        result.EnergyCost = 1.0;

        return true;
      }
    }

    if (actor.HasTrait<InPitTrait>())
    {
      if (actor.AbilityCheck(Attribute.Strength, 13, gs.Rng))
      {
        string s = $"{actor.FullName.Capitalize()} {Grammar.Conjugate(actor, "crawl")} to the edge of the pit.";
        if (gs.LastPlayerFoV.Contains(actor.Loc))
          ui.AlertPlayer(s);
        actor.Traits = [.. actor.Traits.Where(t => t is not InPitTrait)];
      }
      else if (Actor is Player)
      {        
        ui.AlertPlayer("You are still stuck in the pit.");
      }

      result.Succcessful = false;
      result.EnergyCost = 1.0;

      // return true regardless because even if you successfully escape, 
      // it still takes up your move action.
      return true;
    }

    Tile currTile = gs.TileAt(actor.Loc);
    if (currTile.Type == TileType.FrozenDeepWater || currTile.Type == TileType.FrozenWater)
    {
      int slipDC = actor.HasTrait<TipsyTrait>() ? 15 : 11;
      // For slippery tiles, the actor needs to succeed on a dex check before moving, unless 
      // the Actor is flying or floating
      if (actor.HasActiveTrait<FlyingTrait>() || actor.HasActiveTrait<FloatingTrait>())
      {
        return false;
      }
      else if (actor.AbilityCheck(Attribute.Dexterity, slipDC, gs.Rng))
      {
        return false;
      }
      else
      {
        result.Succcessful = false;
        result.EnergyCost = 1.0;
        ui.AlertPlayer(MsgFactory.SlipOnIceMessage(actor, actor.Loc, gs));
      }
    }

    return false;
  }

  public override ActionResult Execute()
  {
    ActionResult result = base.Execute();
    UserInterface ui = GameState!.UIRef();

    if (StuckOnLoc(Actor!, result, GameState!, ui))
      return result;

    if (!GameState.CurrentMap.InBounds(Loc.Row, Loc.Col))
    {
      // in theory this shouldn't ever happen...
      result.Succcessful = false;
      if (Actor is Player)
        ui.AlertPlayer("You cannot go that way!");

      return result;
    }
    else if (!CanMoveTo(Actor!, GameState.CurrentMap, Loc))
    {
      result.Succcessful = false;
      result.EnergyCost = 0.0;

      return result;
    }
    
    result.Succcessful = true;
    result.EnergyCost = 1.0;

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

    return result;
  }
}

// I think only the Player should ever call this aciton. Monsters/NPCs should
// be choosing specific Attack or Move actions
class BumpAction(GameState gameState, Actor actor, Loc loc) : MoveAction(gameState, actor, loc)
{
  readonly bool _bumpToOpen = gameState.Options!.BumpToOpen;
  readonly bool _lockedDoorMenu = gameState.Options.BumpForLockedDoors;

  public override ActionResult Execute()
  {
    ActionResult result = new() { Succcessful = false, EnergyCost = 0.0 };
    UserInterface ui = GameState!.UIRef();
    Player player = GameState!.Player;

    if (Actor!.Traits.OfType<SwallowedTrait>().FirstOrDefault() is SwallowedTrait swallowed)
    {
      // if the actor is swalled by another creature, any direction they move
      // in will attack the monster
      if (GameState!.ObjDb.GetObj(swallowed.SwallowerID) is Actor target)
      {
        var attackAction = new MeleeAttackAction(GameState, Actor, target.Loc);
        result.AltAction = attackAction;        
      }
      else
      {
        throw new Exception($"{Actor.Name} was swallowed by something that doesn't seem to exist?");
      }      
    }
    // There are corner cases when I want to move the actor onto the sq they're already
    // on (like digging while in a pit and turning it into a trapdoor) to re-resolve
    // the effects of moving onto the sq, hence the Occupant ID != Actor.ID check
    else if (GameState!.ObjDb.Occupied(Loc) && GameState.ObjDb.Occupant(Loc)!.ID != Actor.ID)
    {
      result.Succcessful = false;
      Actor? occ = GameState.ObjDb.Occupant(Loc);
      if (occ is not null && occ.Behaviour is VillagePupBehaviour)
      {
        string msg;
        if (GameState.Rng.NextDouble() < 0.5)
          msg = $"You pat {occ.FullName}.";
        else
          msg = $"You give {occ.FullName} some scritches.";
        GameState.UIRef().SetPopup(new Popup(msg, "", -1, -1));
        result.EnergyCost = 1.0;
        result.Succcessful = true;
      }
      else if (Actor.Traits.OfType<GrappledTrait>().FirstOrDefault() is GrappledTrait grappled && occ!.ID != grappled.GrapplerID)
      {
        result.EnergyCost = 1.0;
        result.Succcessful = false;

        if (GameState.ObjDb.GetObj(grappled.GrapplerID) is Actor grappler)
        {
          string s = $"You cannot attack {occ.FullName} while grappled by {grappler.FullName}!";
          GameState.UIRef().AlertPlayer(s);
        }
      }
      else if (occ is not null && !Battle.PlayerWillAttack(occ))
      {
        result.Succcessful = false;

        if (GameState.Options.BumpToChat)
        {
          ChatAction chat = new (GameState, GameState.Player) { Loc = Loc };
          result.AltAction = chat;
        }
        else
        {
          ui.AlertPlayer($"You don't want to attack {occ.FullName}!");
        }        
      }
      else if (occ is not null && Actor.HasTrait<FrightenedTrait>())
      {
        result.EnergyCost = 1.0;
        result.Succcessful = false;
        ui.AlertPlayer("You are too frightened to attack!");
      }
      else
      {
        var attackAction = new MeleeAttackAction(GameState, player, Loc);
        result.AltAction = attackAction;
      }
    }
    else if (!CanMoveTo(player, GameState.CurrentMap, Loc))
    {
      result.Succcessful = false;
      Tile tile = GameState.CurrentMap.TileAt(Loc.Row, Loc.Col);
    
      if (_bumpToOpen && tile.Type == TileType.ClosedDoor)
      {
        result.AltAction = new OpenDoorAction(GameState, Actor, Loc);
      }
      else if (_lockedDoorMenu && tile.Type == TileType.LockedDoor)
      {
        ui.AlertPlayer(BlockedMessage(tile));
        GameState.Player.ReplacePendingAction(null, new LockedDoorMenu(ui, GameState, Loc));
      }
      else if (!GameState.InWilderness && tile.Type == TileType.DeepWater)
      {
        // If we are in the dungeon, we'll let the player jump into rivers
        // (and/or they can stumble in while confused, etc)

        if (GameState.CurrentDungeon.RememberedLocs.ContainsKey(Loc))
        {
          ui.SetPopup(new Popup("Really jump into the water? (y/n)", "", -1, -1));
          GameState.Player.ReplacePendingAction(new DiveAction(GameState, player, Loc, true), new YesOrNoInputer());
        }
        else
        {
          GameState.RememberLoc(Loc, tile);
          result.AltAction = new DiveAction(GameState, player, Loc, false);
        }
      }
      else if (tile.Type == TileType.Chasm)
      {
        if (GameState.CurrentDungeon.RememberedLocs.ContainsKey(Loc))
        {
          ui.SetPopup(new Popup("Really jump into the chasm? (y/n)", "", -1, -1));
          GameState.Player.ReplacePendingAction(new DiveAction(GameState, player, Loc, true), new YesOrNoInputer());
        }
        else
        {
          GameState.RememberLoc(Loc, tile);
          result.AltAction = new DiveAction(GameState, player, Loc, false);
        }
      }
      else if (tile.Type == TileType.Lever)
      {
        Lever lever = (Lever)tile;
        lever.Activate(GameState);

        result.EnergyCost = 1.0;
        result.Succcessful = true;
      }
      else
      {
        ui.AlertPlayer(BlockedMessage(tile));
      }
      
      // If the player is blind, remember what tile they bumped into
      // so that it displays on screen
      if (player.HasTrait<BlindTrait>())
        GameState.RememberLoc(Loc, tile);
    }
    else
    {
      result = base.Execute();
    }

    return result;
  }

  static string BlockedMessage(Tile tile) => tile.Type switch
  {
    TileType.DeepWater => "The water seems deep and cold.",
    TileType.Mountain or TileType.SnowPeak => "You cannot scale the mountain!",
    TileType.Chasm => "Do you really want to jump into the chasm?",
    TileType.Portcullis => "The portcullis is closed.",
    TileType.VaultDoor => "Metal doors bar your path!",
    TileType.LockedDoor => "The door is locked!",
    _ => "You cannot go that way!"
  };
}