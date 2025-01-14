﻿// Yarl2 - A roguelike computer RPG
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
  readonly bool _bumpToOpen = gameState.Options!.BumpToOpen;

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
    else if ((tile.Type == TileType.Water || tile.Type == TileType.DeepWater) && actor.HasTrait<WaterWalkingTrait>())
      return true;

    return false;
  }

  public override ActionResult Execute()
  {
    ActionResult result = base.Execute();
    bool isPlayer = Actor is Player;
    Tile currTile = GameState!.TileAt(Actor!.Loc);
    UserInterface ui = GameState.UIRef();

    // First, is there anything preventing the actor from moving off
    // of the square?
    foreach (var env in GameState.ObjDb.EnvironmentsAt(Actor.Loc))
    {
      var web = env.Traits.OfType<StickyTrait>().FirstOrDefault();
      if (web is not null && !Actor.HasTrait<TeflonTrait>())
      {
        bool strCheck = Actor.AbilityCheck(Attribute.Strength, web.DC, GameState.Rng);
        if (!strCheck)
        {
          result.EnergyCost = 1.0;
          result.Complete = true;
          var txt = $"{Actor.FullName.Capitalize()} {MsgFactory.CalcVerb(Actor, Verb.Etre)} stuck to {env.Name.DefArticle()}!";
          ui.AlertPlayer(txt);
          return result;
        }
        else
        {
          var txt = $"{Actor.FullName.Capitalize()} {MsgFactory.CalcVerb(Actor, Verb.Tear)} through {env.Name.DefArticle()}.";
          ui.AlertPlayer(txt);
          GameState.ObjDb.RemoveItemFromGame(env.Loc, env);
        }
      }
    }

    if (!GameState.CurrentMap.InBounds(Loc.Row, Loc.Col))
    {
      // in theory this shouldn't ever happen...
      result.Complete = false;
      if (isPlayer)
        ui.AlertPlayer("You cannot go that way!");
    }
    else if (Actor.Traits.OfType<SwallowedTrait>().FirstOrDefault() is SwallowedTrait swallowed)
    {
      // if the actor is swalled by another creature, any direction they move
      // in will attack the monster
      if (GameState.ObjDb.GetObj(swallowed.SwallowerID) is Actor target)
      {
        var attackAction = new MeleeAttackAction(GameState, Actor, target.Loc);
        result.AltAction = attackAction;
      }

      // plus attacking gives a chance to expel the victim
    }
    // There are corner cases when I want to move the actor onto the sq they're already
    // on (like digging while in a pit and turning it into a trapdoor) to re-resolve
    // the effects of moving onto the sq, hence the Occupant ID != Actor.ID check
    else if (GameState.ObjDb.Occupied(Loc) && GameState.ObjDb.Occupant(Loc)!.ID != Actor.ID)
    {
      result.Complete = false;
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
        result.Complete = true;        
      }
      else if (occ is not null && !Battle.PlayerWillAttack(occ))
      {
        ui.AlertPlayer($"You don't want to attack {occ.FullName}!");        
      }
      else if (occ is not null && Actor.HasTrait<FrightenedTrait>())
      {
        string s = $"{Actor.FullName.Capitalize()} {Grammar.Conjugate(Actor, "is")} too frightened to attack!";
        ui.AlertPlayer(s);
      }
      else
      {
        var attackAction = new MeleeAttackAction(GameState, Actor, Loc);
        result.AltAction = attackAction;
      }
    }    
    else if (!CanMoveTo(Actor, GameState.CurrentMap, Loc))
    {
      result.Complete = false;
      Tile tile = GameState.CurrentMap.TileAt(Loc.Row, Loc.Col);

      if (Actor.HasTrait<ConfusedTrait>())
      {
        result.Complete = true;
        result.EnergyCost = 1.0;
        string stumbleText = $"{Actor.FullName.Capitalize()} {Grammar.Conjugate(Actor, "stumble")} in confusion!";
        ui.AlertPlayer(stumbleText);

        if (isPlayer)
          ui.AlertPlayer(BlockedMessage(tile));
      }
      else if (isPlayer)
      {        
        if (_bumpToOpen && tile.Type == TileType.ClosedDoor)
        {
          var openAction = new OpenDoorAction(GameState, Actor, Loc);
          result.AltAction = openAction;
        }
        else if (!GameState.InWilderness && tile.Type == TileType.DeepWater)
        {
          // If we are in the dungeon, we'll let the player jump into rivers
          // (and/or they can stumble in while confused, etc)

          if (GameState.CurrentDungeon.RememberedLocs.ContainsKey(Loc))
          {
            GameState.UIRef().SetPopup(new Popup("Really jump into the water? (y/n)", "", -1, -1));
            GameState.Player.ReplacePendingAction(new DiveAction(GameState, Actor, Loc, true), new YesOrNoInputer());
          }
          else
          {
            GameState.RememberLoc(Loc, tile);
            result.EnergyCost = 0;
            result.AltAction = new DiveAction(GameState, Actor, Loc, false);
          }
        }
        else if (tile.Type == TileType.Chasm)
        {
          if (GameState.CurrentDungeon.RememberedLocs.ContainsKey(Loc))
          {
            GameState.UIRef().SetPopup(new Popup("Really jump into the chasm? (y/n)", "", -1, -1));
            GameState.Player.ReplacePendingAction(new DiveAction(GameState, Actor, Loc, true), new YesOrNoInputer());
          }
          else
          {
            GameState.RememberLoc(Loc, tile);
            result.EnergyCost = 0;
            result.AltAction = new DiveAction(GameState, Actor, Loc, false);
          }
        }
        else
        {
          ui.AlertPlayer(BlockedMessage(tile));
        }
      }

      // If the player is blind, remember what tile they bumped into
      // so that it displays on screen
      if (isPlayer && Actor.HasTrait<BlindTrait>())
        GameState.RememberLoc(Loc, tile);
    }
    else if (Actor.HasActiveTrait<GrappledTrait>())
    {
      var gt = Actor.Traits.OfType<GrappledTrait>().First();
      if (Actor.AbilityCheck(Attribute.Strength, gt.DC, GameState.Rng))
      {
        Actor.Traits.Remove(gt);
        string txt = $"{Actor.FullName.Capitalize()} {MsgFactory.CalcVerb(Actor, Verb.Break)} free of the grapple!";  
        ui.AlertPlayer(txt);
        return ActuallyDoMove(result);
      }      
      else
      {
        string txt = $"{Actor.FullName.Capitalize()} {MsgFactory.CalcVerb(Actor, Verb.Etre)} grappled by ";
        GameObj? grappler = GameState.ObjDb.GetObj(gt.GrapplerID);
        txt += $"{grappler!.FullName}!";
        ui.AlertPlayer(txt);
        ui.AlertPlayer($"{Actor.FullName.Capitalize()} cannot get away!");
        result.Complete = true;
        result.EnergyCost = 1.0;        
      }
    }
    else if (Actor.HasTrait<InPitTrait>())
    {
      if (Actor.AbilityCheck(Attribute.Strength, 13, GameState.Rng))
      {
        string s = $"{Actor.FullName.Capitalize()} {Grammar.Conjugate(Actor, "crawl")} to the edge of the pit.";
        ui.AlertPlayer(s);
        result.Complete = true;
        result.EnergyCost = 1.0;
        Actor.Traits = Actor.Traits.Where(t => t is not InPitTrait).ToList();
      }
      else
      {
        result.Complete = true;
        result.EnergyCost = 1.0;
        if (Actor is Player)
          ui.AlertPlayer("You are still stuck in the pit.");
      }
    }
    else if (GameState.ObjDb.ItemsAt(Loc).Any(item => item.HasTrait<BlockTrait>()))
    {
      Item blockage = GameState.ObjDb.ItemsAt(Loc).Where(item => item.HasTrait<BlockTrait>()).First();
      if (blockage.Type == ItemType.Statue)
      {
        string msg;
        if (blockage.Traits.OfType<DescriptionTrait>().FirstOrDefault() is DescriptionTrait desc)
          msg = desc.Text;
        else
          msg = $"{Grammar.Possessive(Actor).Capitalize()} way is blocked by a statue.";

        if (Actor is Player)
          GameState.UIRef().SetPopup(new Popup(msg, "a statue", -1, -1));
        ui.AlertPlayer(msg);
      }
      else
      {
        string msg = $"{Grammar.Possessive(Actor).Capitalize()} way is blocked by ";
        if (blockage.HasTrait<PluralTrait>())
          msg += $"some {blockage.Name}!";
        else
          msg += $"{blockage.Name.IndefArticle()}!";
        ui.AlertPlayer(msg);
      }
      
      result.Complete = true;
      result.EnergyCost = 0.0;
    }
    else if (currTile.Type == TileType.FrozenDeepWater || currTile.Type == TileType.FrozenWater)
    {
      // For slippery tiles, the actor needs to succeed on a dex check before moving, unless 
      // the Actor is flying or floating
      if (Actor.HasActiveTrait<FlyingTrait>() || Actor.HasActiveTrait<FloatingTrait>()) 
      {
        return ActuallyDoMove(result);
      }
      else if (Actor.AbilityCheck(Attribute.Dexterity, 11, GameState.Rng))
      {
        return ActuallyDoMove(result);
      }
      else
      {       
        result.Complete = true;
        result.EnergyCost = 1.0;
        ui.AlertPlayer(MsgFactory.SlipOnIceMessage(Actor, Loc, GameState));
      }
    }
    else
    {
      return ActuallyDoMove(result);
    }

    return result;
  }

  ActionResult ActuallyDoMove(ActionResult result)
  {
    result.Complete = true;
    result.EnergyCost = 1.0;

    try 
    {
      string moveMsg = GameState!.ResolveActorMove(Actor!, Actor!.Loc, Loc);
      GameState!.UIRef().AlertPlayer(moveMsg);      
    }
    catch (AbnormalMovement abMov)
    {
      Actor!.Loc = abMov.Dest;
    }
    
    if (Actor is Player)
    {
      GameState!.UIRef().AlertPlayer(GameState!.LocDesc(Actor.Loc));
      GameState.Noise(Actor.Loc.Row, Actor.Loc.Col, Actor.GetMovementNoise());      
    }
    else
    {
      // Not 100% sure what I want to do with this. ATM a monster can
      // wake up other monsters and not sure if I want that. And if I keep
      // on with the "You hear padding footsteps..." stuff I'm going to have
      // provide different messages for all the different kinds of mobs

      //var alerted = GameState.Noise(Actor.ID, _loc.Row, _loc.Col, 6);
      //if (alerted.Contains(GameState.Player.ID))
      //{
      //  string moveText = "You hear padding footsteps...";
      //  if (Actor.HasActiveTrait<FlyingTrait>())
      //    moveText = "You hear softly beating wings...";
      //  else if (Actor.HasActiveTrait<FloatingTrait>())
      //    moveText = "";
        
      //  if (moveText != "")
      //    result.Messages.Add(new Message(moveText, _loc, true));
      //}
    }

    return result;
  }
}