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
  readonly Loc _loc = loc;
  readonly Map _map = gameState.CurrMap!;
  readonly bool _bumpToOpen = gameState.Options!.BumpToOpen;
  
  static string BlockedMessage(Tile tile)
  {
    return tile.Type switch
    {
      TileType.DeepWater => "The water seems deep and cold.",
      TileType.Mountain or TileType.SnowPeak => "You cannot scale the mountain!",
      TileType.Chasm => "Do you really want to jump into the chasm?",
      _ => "You cannot go that way!"
    };
  }

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

    return false;
  }

  public override ActionResult Execute()
  {
    var result = base.Execute();
    bool isPlayer = Actor is Player;
    Tile currTile = GameState!.TileAt(Actor!.Loc);

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
          var msg = new Message(txt, Actor.Loc);
          result.Messages.Add(msg);
          return result;
        }
        else
        {
          var txt = $"{Actor.FullName.Capitalize()} {MsgFactory.CalcVerb(Actor, Verb.Tear)} through {env.Name.DefArticle()}.";
          GameState.ObjDb.RemoveItemFromGame(env.Loc, env);
          result.Messages.Add(new Message(txt, Actor.Loc));
        }
      }
    }

    if (!_map.InBounds(_loc.Row, _loc.Col))
    {
      // in theory this shouldn't ever happen...
      result.Complete = false;
      if (isPlayer)
        result.Messages.Add(new Message("You cannot go that way!", GameState.Player.Loc));
    }
    else if (GameState.ObjDb.Occupied(_loc))
    {
      result.Complete = false;
      var occ = GameState.ObjDb.Occupant(_loc);
      if (occ.Behaviour is VillagePupBehaviour)
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
      else if (occ is not null && !occ.Hostile)
      {
        string msg = $"You don't want to attack {occ.FullName}!";
        result.Messages.Add(new Message(msg, GameState.Player.Loc));
      }
      else
      {
        var attackAction = new MeleeAttackAction(GameState, Actor, _loc);
        result.AltAction = attackAction;
      }
    }
    else if (!CanMoveTo(Actor, _map, _loc))
    {
      result.Complete = false;

      if (Actor.HasTrait<ConfusedTrait>())
      {
        result.Complete = true;
        result.EnergyCost = 1.0;
        string stumbleText = $"{Actor.FullName.Capitalize()} {Grammar.Conjugate(Actor, "stumble")} in confusion!";
        result.Messages.Add(new Message(stumbleText, Actor.Loc));

        if (isPlayer)
        {
          var tile = _map.TileAt(_loc.Row, _loc.Col);
          result.Messages.Add(new Message(BlockedMessage(tile), Actor.Loc));
        }

        return result;
      }

      if (isPlayer)
      {
        var tile = _map.TileAt(_loc.Row, _loc.Col);
        if (_bumpToOpen && tile.Type == TileType.ClosedDoor)
        {
          var openAction = new OpenDoorAction(GameState, Actor, _map, _loc);
          result.AltAction = openAction;
        }
        else if (!GameState.InWilderness && tile.Type == TileType.DeepWater)
        {
          // If we are in the dungeon, we'll let the player jump into rivers
          // (and/or they can stumble in while confused, etc)
          GameState.UIRef().SetPopup(new Popup("Really jump into the water? (y/n)", "", -1, -1));
          GameState.Player.ReplacePendingAction(new DiveAction(GameState, Actor, _loc), new YesOrNoInputer());
        }
        else if (tile.Type == TileType.Chasm)
        {
          GameState.UIRef().SetPopup(new Popup("Really jump into the chasm? (y/n)", "", -1, -1));
          GameState.Player.ReplacePendingAction(new DiveAction(GameState, Actor, _loc), new YesOrNoInputer());
        }
        else
        {
          result.Messages.Add(new Message(BlockedMessage(tile), GameState.Player.Loc));
        }
      }
    }
    else if (Actor.HasActiveTrait<GrappledTrait>())
    {
      var gt = Actor.Traits.OfType<GrappledTrait>().First();
      if (Actor.AbilityCheck(Attribute.Strength, gt.DC, GameState.Rng))
      {
        Actor.Traits.Remove(gt);
        string txt = $"{Actor.FullName.Capitalize()} {MsgFactory.CalcVerb(Actor, Verb.Break)} free of the grapple!";        
        result.Messages.Add(new Message(txt, Actor.Loc));
        return ActuallyDoMove(result);
      }      
      else
      {
        string txt = $"{Actor.FullName.Capitalize()} {MsgFactory.CalcVerb(Actor, Verb.Etre)} grappled by ";
        var grappler = GameState.ObjDb.GetObj(gt.GrapplerID);
        txt += $"{grappler.FullName}!";
        result.Messages.Add(new Message(txt, Actor.Loc));
        txt = $"{Actor.FullName.Capitalize()} cannot get away!";
        result.Messages.Add(new Message(txt, Actor.Loc));
        result.Complete = true;
        result.EnergyCost = 1.0;        
      }
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
        Message msg = new($"{Actor.FullName.Capitalize()} {Grammar.Conjugate(Actor, "slip")} on the ice!", Actor.Loc);
        result.Complete = true;
        result.EnergyCost = 1.0;
        result.Messages.Add(msg);
        result.MessageIfUnseen = Actor is Player ? "" : "You hear a clatter!";
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

    GameState!.ResolveActorMove(Actor!, Actor!.Loc, _loc);
    Actor.Loc = _loc;

    if (Actor is Player)
    {
      result.Messages.Add(new Message(GameState.LocDesc(_loc), _loc));
      GameState.Noise(Actor.ID, _loc.Row, _loc.Col, 12);
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