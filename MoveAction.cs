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

using System.Text;

namespace Yarl2;

// The code for MoveAction was getting lengthy enough that I figured I
// should move it to its own file
class MoveAction(GameState gameState, Actor actor, Loc loc) : Action(gameState, actor)
{
  readonly Loc _loc = loc;
  readonly Map _map = gameState.Map!;
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

  string CalcDesc()
  {
    if (Actor is not Player)
      return "";

    var sb = new StringBuilder();
    sb.Append(_map.TileAt(_loc.Row, _loc.Col).StepMessage);

    var items = GameState!.ObjDb.ItemsAt(_loc);
    if (items.Count > 1)
    {
      sb.Append(" There are several items here.");
    }
    else if (items.Count == 1 && items[0].Type == ItemType.Zorkmid)
    {
      if (items[0].Value == 1)
        sb.Append($" There is a lone zorkmid here.");
      else
        sb.Append($" There are {items[0].Value} zorkmids here!");
    }
    else if (items.Count == 1)
    {
      sb.Append($" There is {items[0].FullName.IndefArticle()} here.");
    }

    foreach (var env in GameState!.ObjDb.EnvironmentsAt(_loc))
    {
      if (env.Traits.OfType<StickyTrait>().Any())
      {
        sb.Append(" There are some sticky ");
        sb.Append(env.Name);
        sb.Append(" here.");
      }
    }
    return sb.ToString().Trim();
  }

  bool CanMoveTo()
  {
    static bool CanFly(Actor actor)
    {
      return actor.HasActiveTrait<FlyingTrait>() ||
                actor.HasActiveTrait<FloatingTrait>();
    }
    var tile = _map.TileAt(_loc.Row, _loc.Col);

    if (tile.Passable())
      return true;
    else if (CanFly(Actor!) && tile.PassableByFlight())
      return true;

    return false;
  }

  public override ActionResult Execute()
  {
    var result = new ActionResult();

    // First, is there anything preventing the actor from moving off
    // of the square?
    foreach (var env in GameState!.ObjDb.EnvironmentsAt(Actor!.Loc))
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
      if (Actor is Player)
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
        GameState.WritePopup(msg, "");
        result.EnergyCost = 1.0;
        result.Complete = true;
        //result.Messages.Add(MessageFactory.Phrase(msg, _gs.Player.Loc));
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
    else if (!CanMoveTo())
    {
      result.Complete = false;

      if (Actor.HasTrait<ConfusedTrait>())
      {
        result.Complete = true;
        result.EnergyCost = 1.0;
        string stumbleText = $"{Actor.FullName.Capitalize()} {Grammar.Conjugate(Actor, "stumble")} in confusion!";
        result.Messages.Add(new Message(stumbleText, Actor.Loc));

        if (Actor is Player)
        {
          var tile = _map.TileAt(_loc.Row, _loc.Col);
          result.Messages.Add(new Message(BlockedMessage(tile), Actor.Loc));
        }

        return result;
      }

      if (Actor is Player)
      {
        var tile = _map.TileAt(_loc.Row, _loc.Col);
        if (_bumpToOpen && tile.Type == TileType.ClosedDoor)
        {
          var openAction = new OpenDoorAction(GameState, Actor, _map, _loc);
          result.AltAction = openAction;
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

    GameState.ResolveActorMove(Actor, Actor.Loc, _loc);
    Actor.Loc = _loc;

    if (Actor is Player)
    {
      result.Messages.Add(new Message(CalcDesc(), _loc));
      GameState.Noise(Actor.ID, _loc.Row, _loc.Col, 12);
    }
    else
    {
      var alerted = GameState.Noise(Actor.ID, _loc.Row, _loc.Col, 6);
      if (alerted.Contains(GameState.Player.ID))
      {
        string moveText = "You hear padding footsteps...";
        if (Actor.HasActiveTrait<FlyingTrait>())
          moveText = "You hear softly beating wings...";
        else if (Actor.HasActiveTrait<FloatingTrait>())
          moveText = "";
        
        if (moveText != "")
          result.Messages.Add(new Message(moveText, _loc, true));
      }
    }

    return result;
  }
}
