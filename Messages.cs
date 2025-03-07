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

// Bits and bobs for tracking the messages that occur in the game.
// At the moment it's mainly to alert the player but I have in mind
// eventually NPCs/monsters can react to things that happen in game
// Ie., a shepherd could see "wolf attacks sheep" and can react to
// that.

// I think my verb enum and CalcVerb(), and maybe the whole MessageFactory
// class were dumb ideas and I can just replace them with this 
class Grammar
{
  public static string Conjugate(GameObj subject, string verb)
  {
    var player = subject is Player;

    if (verb == "is" && player)
      return "are";
    else if (verb == "is")
      return verb;

    if (subject is Player || subject.HasTrait<PluralTrait>())
      return verb;
    else if (verb.EndsWith("ss") || verb.EndsWith("sh"))
      return verb + "es";
    else if (verb == "ready")
      return "readies";
    else
      return verb + "s";
  }

  public static string Possessive(GameObj subject)
  {
    if (subject is Player)
      return "your";
    else if (subject.Traits.OfType<NamedTrait>().Any())
      return "their";
    else
      return "its";
  }
}

class MsgFactory
{
  public static string CalcName(GameObj gobj, Player player, int amount = 0)
  {
    StringBuilder sb = new();
    if (gobj is Item item)
    {
      if (amount > 1)
      {
        sb.Append(amount);
        sb.Append(' ');
        sb.Append(item.FullName.Pluralize());
      }
      else if (item.HasTrait<NamedTrait>())
      {
        sb.Append(item.FullName);
      }
      else
      {
        sb.Append(item.FullName.DefArticle());
      }
    }
    else if (gobj is Actor actor)
    {
      if (actor.VisibleTo(player))
        sb.Append(gobj.FullName);
      else
        sb.Append("something");
    }
    else
    {
      // I suppose there might eventually be other types of GameObjects and Items and Actors?
      sb.Append(gobj.FullName);
    }

    return sb.ToString();
  }

  public static string SlipOnIceMessage(Actor actor, Loc loc, GameState gs)
  {
    bool canSeeLoc = gs.LastPlayerFoV.Contains(loc);
    if (canSeeLoc)
      return $"{actor.FullName.Capitalize()} {Grammar.Conjugate(actor, "slip")} on the ice!";
    else if (actor is Player)
      return "You slip on some ice!";
    else
      return "You hear a clatter!";
  }

  public static string DoorMessage(Actor actor, Loc loc, string verb, GameState gs)
  {
    if (Util.AwareOfActor(actor, gs))    
      return $"{actor.FullName.Capitalize()} {Grammar.Conjugate(actor, verb)} the door.";    
    else if (actor is Player)
      return $"You fumble with a door handle and {verb} a door.";
    else if (gs.LastPlayerFoV.Contains(loc))
      return $"You see a door {verb}.";
    else
      return $"You hear a door {verb}.";
  }

  public static string HitMessage(Actor attacker, Actor target, string verb, GameState gs)
  {
    bool canSeeTarget = Util.AwareOfActor(target, gs);
    
    if (attacker is Player)
    {
      return canSeeTarget ? $"You {Grammar.Conjugate(attacker, verb)} {CalcName(target, gs.Player)}!" : "You hit!";
    }
    else if (target is Player)
    {
      return $"{CalcName(attacker, gs.Player).Capitalize()} {Grammar.Conjugate(attacker, verb)} you!";
    }
    else
    {
      return "You hear the sounds of battle.";
    }
  }

  public static string MissMessage(Actor attacker, Actor target, GameState gs)
  {
    bool canSeeTarget = Util.AwareOfActor(target, gs);
    bool canSeeAttacker = Util.AwareOfActor(attacker, gs);

    if (target is Player)
      return canSeeAttacker ? $"{CalcName(attacker, gs.Player).Capitalize()} {Grammar.Conjugate(attacker, "miss")} you!" : "You are missed by an attack!";
    else if (attacker is Player)
      return canSeeTarget ? $"You {Grammar.Conjugate(attacker, "miss")} {CalcName(target, gs.Player)}!" : "Your attack misses!";
    else
      return "You hear the sounds of battle.";
  }

  public static string MobKilledMessage(Actor victim, GameObj? attacker, GameState gs)
  {
    string verb = "killed";
    bool plural = false;
    foreach (Trait t in victim.Traits)
    {
      if (t is PlantTrait || t is ConstructTrait || t is UndeadTrait)
        verb = "destroyed";
      else if (t is PluralTrait)
        plural = true;
    }
 
    string etre = plural ? "are" : "is";
    if (Util.AwareOfActor(victim, gs))
      return $"{CalcName(victim, gs.Player).Capitalize()} {etre} {verb}.";
    else if (attacker is Player)
      return "You kill something!";
    else
      return "Something makes a death-rattle!";
  }
}