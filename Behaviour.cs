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

abstract class MoveStrategy
{
  public abstract Action MoveAction(Mob actor, GameState gs);
  public abstract Action RandomMoveAction(Mob actor, GameState gs);
  public abstract Action EscapeRoute(Mob mob, GameState gs);
}

class WallMoveStrategy : MoveStrategy
{
  public override Action MoveAction(Mob actor, GameState gs) => new PassAction();
  public override Action RandomMoveAction(Mob actor, GameState gs) => new PassAction();
  public override Action EscapeRoute(Mob actor, GameState gs) => new PassAction();
}

// For creatures that don't know how to open doors
class DumbMoveStrategy : MoveStrategy
{
  public override Action MoveAction(Mob actor, GameState gs)
  {
    var map = gs.GetDMap() ?? throw new Exception("Dijkstra map should never be null");
    List<(int, int, int)> adj = map.Neighbours(actor.Loc.Row, actor.Loc.Col);
    foreach (var sq in adj)
    {
      var loc = new Loc(actor.Loc.DungeonID, actor.Loc.Level, sq.Item1, sq.Item2);
      
      // We still check if the tile is passable because, say, a door might be
      // closed after the current dijkstra map is calculated and before it is
      // refreshed
      if (!gs.ObjDb.Occupied(loc) && gs.TileAt(loc).Passable())
      {
        return new MoveAction(gs, actor, loc);
      }
    }

    // If we can't find a move to do, pass
    return new PassAction();
  }

  public override Action RandomMoveAction(Mob actor, GameState gs)
  {
    List<Loc> opts = Util.Adj8Locs(actor.Loc)
                         .Where(l => !gs.ObjDb.Occupied(l) && gs.TileAt(l).Passable() && !gs.ObjDb.BlockersAtLoc(l))
                         .ToList();
    if (opts.Count == 0)
      return new PassAction();
    else
      return new MoveAction(gs, actor, opts[gs.Rng.Next(opts.Count)]);
  }

  public override Action EscapeRoute(Mob actor, GameState gs)
  {
    var map = gs.GetDMap();
    if (map is null)
      throw new Exception("No map found");
    List<(int, int)> route = map.EscapeRoute(actor.Loc.Row, actor.Loc.Col, 5);
    if (route.Count > 0)
    {
      Loc loc = actor.Loc with { Row = route[0].Item1, Col = route[0].Item2 };
      return new MoveAction(gs, actor, loc);
    }

    return new PassAction();
  }
}

// I don't know what to call this class, but it's movement for creatures who
// can open doors. OpposableThumbMoveStrategy? :P
class DoorOpeningMoveStrategy : MoveStrategy
{
  public override Action MoveAction(Mob actor, GameState gs)
  {
    var mapWithDoors = gs.GetDMap("doors");
    if (mapWithDoors is null)
      throw new Exception("No doors map found");
    List<(int, int, int)> adj = mapWithDoors.Neighbours(actor.Loc.Row, actor.Loc.Col);
    foreach (var sq in adj)
    {
      var loc = new Loc(actor.Loc.DungeonID, actor.Loc.Level, sq.Item1, sq.Item2);

      if (gs.CurrentMap.TileAt(loc.Row, loc.Col).Type == TileType.ClosedDoor)
      {
        return new OpenDoorAction(gs, actor, gs.CurrentMap, loc);
      }
      else if (!gs.ObjDb.Occupied(loc) && gs.TileAt(loc).Passable())
      {
        // the square is free so move there!
        return new MoveAction(gs, actor, loc);
      }
    }

    // Otherwise do nothing!
    return new PassAction();
  }

  public override Action RandomMoveAction(Mob actor, GameState gs)
  {
    List<Loc> opts = Util.Adj8Locs(actor.Loc)
                         .Where(l => !gs.ObjDb.Occupied(l) && gs.TileAt(l).Passable() && !gs.ObjDb.BlockersAtLoc(l))
                         .ToList();
    if (opts.Count == 0)
      return new PassAction();
    else
      return new MoveAction(gs, actor, opts[gs.Rng.Next(opts.Count)]);
  }

  public override Action EscapeRoute(Mob actor, GameState gs)
  {
    var mapWithDoorts = gs.GetDMap("doors");
    if (mapWithDoorts is null)
      throw new Exception("No doors map found");
    List<(int, int)> route = mapWithDoorts.EscapeRoute(actor.Loc.Row, actor.Loc.Col, 5);
    if (route.Count > 0)
    {      
      Loc loc = actor.Loc with { Row = route[0].Item1, Col = route[0].Item2 };
      if (gs.TileAt(loc).Type == TileType.ClosedDoor) 
      {
        Map map = gs.CurrentDungeon.LevelMaps[loc.Level];
        return new OpenDoorAction(gs, actor, map, loc);
      }

      return new MoveAction(gs, actor, loc);
    }

    return new PassAction();
  }
}

class SimpleFlightMoveStrategy : MoveStrategy
{
  public override Action MoveAction(Mob actor, GameState gs)
  {
    var flyingMap = gs.GetDMap("flying");
    if (flyingMap is null)
      throw new Exception("No flying map found");
    List<(int, int, int)> adj = flyingMap.Neighbours(actor.Loc.Row, actor.Loc.Col);
    foreach (var sq in adj)
    {
      var loc = new Loc(actor.Loc.DungeonID, actor.Loc.Level, sq.Item1, sq.Item2);
      if (!gs.ObjDb.Occupied(loc) && gs.TileAt(loc).PassableByFlight())
      {
        // the square is free so move there!
        return new MoveAction(gs, actor, loc);
      }
    }

    // Otherwise do nothing!
    return new PassAction();
  }

  public override Action RandomMoveAction(Mob actor, GameState gs)
  {
    List<Loc> opts = Util.Adj8Locs(actor.Loc)
                         .Where(l => !gs.ObjDb.Occupied(l) && gs.TileAt(l).PassableByFlight() && !gs.ObjDb.BlockersAtLoc(l))
                         .ToList();
    if (opts.Count == 0)
      return new PassAction();
    else
      return new MoveAction(gs, actor, opts[gs.Rng.Next(opts.Count)]);
  }

  public override Action EscapeRoute(Mob actor, GameState gs)
  {
    var map = gs.GetDMap("flying") ?? throw new Exception("Hmm this shouldn't have happened");      
    List<(int, int)> route = map.EscapeRoute(actor.Loc.Row, actor.Loc.Col, 5);
    if (route.Count > 0)
    {
      Loc loc = actor.Loc with { Row = route[0].Item1, Col = route[0].Item2 };
      return new MoveAction(gs, actor, loc);
    }

    return new PassAction();
  }
}

interface IBehaviour
{
  Action CalcAction(Mob actor, GameState gameState);
  (Action, Inputer?) Chat(Mob actor, GameState gameState);
}

class NullBehaviour : IBehaviour
{
  private static readonly NullBehaviour instance = new NullBehaviour();
  public static NullBehaviour Instance() => instance;

  public Action CalcAction(Mob actor, GameState gameState) => throw new NotImplementedException();  
  public (Action, Inputer?) Chat(Mob actor, GameState gameState) => throw new NotImplementedException();
}

// I think I'll likely eventually merge this into IBehaviour
interface IDialoguer
{
  (string, List<(string, char)>) CurrentText(Mob mob, GameState gs);
  void SelectOption(Mob actor, char opt, GameState gs);
}

class MonsterBehaviour : IBehaviour
{
  readonly Dictionary<string, ulong> _lastUse = [];

  static Loc CalcRangedTarget(Mob mob, GameState gs)
  {
    if (mob.HasTrait<ConfusedTrait>())
      return Util.RandomAdjLoc(gs.Player.Loc, gs);
    else
      return gs.Player.Loc;
  }

  static Loc CalcAdjacentTarget(Mob mob, GameState gs)
  {
    if (mob.HasTrait<ConfusedTrait>())
      return Util.RandomAdjLoc(mob.Loc, gs);
    else
      return gs.Player.Loc;
  }

  Action FromTrait(Mob mob, ActionTrait act, GameState gs)
  {
    if (act is MobMeleeTrait meleeAttack)
    {      
      var p = gs.Player;
      mob.Dmg = new Damage(meleeAttack.DamageDie, meleeAttack.DamageDice, meleeAttack.DamageType);
      _lastUse[act.Name] = gs.Turn;
      
      return new MeleeAttackAction(gs, mob, CalcAdjacentTarget(mob, gs));
    }
    else if (act is MobMissileTrait missileAttack)
    {
      mob.Dmg = new Damage(missileAttack.DamageDie, missileAttack.DamageDice, missileAttack.DamageType);
      _lastUse[act.Name] = gs.Turn;

      var arrowAnim = new ArrowAnimation(gs, ActionTrait.Trajectory(mob, gs.Player.Loc), Colours.LIGHT_BROWN);
      gs.UIRef().RegisterAnimation(arrowAnim);

      var arrow = ItemFactory.Get(ItemNames.ARROW, gs.ObjDb);
      return new MissileAttackAction(gs, mob, gs.Player.Loc, arrow, 0);
    }
    else if (act is SpellActionTrait || act is RangedSpellActionTrait)
    {
      _lastUse[act.Name] = gs.Turn;
      if (act.Name == "Blink")
        return new BlinkAction(gs, mob);
      else if (act.Name == "FogCloud")
        return new FogCloudAction(gs, mob, CalcRangedTarget(mob, gs));
      else if (act.Name == "Nudity")
        return new InduceNudityAction(gs, mob, CalcRangedTarget(mob, gs));
      else if (act.Name == "Entangle")
        return new EntangleAction(gs, mob, CalcRangedTarget(mob, gs));
      else if (act.Name == "Web")
        return new WebAction(gs, gs.Player.Loc);
      else if (act.Name == "Firebolt")
      {
        Loc targetLoc = CalcRangedTarget(mob, gs);
        return new FireboltAction(gs, mob, targetLoc, ActionTrait.Trajectory(mob, targetLoc));
      }
      else if (act.Name == "MirrorImage")
        return new MirrorImageAction(gs, mob, CalcAdjacentTarget(mob, gs));
      else if (act.Name == "DrainTorch")
      {
        return new DrainTorchAction(gs, mob, CalcRangedTarget(mob, gs));
      }      
    }
    else if (act is ConfusingScreamTrait scream)
    {
      _lastUse[act.Name] = gs.Turn;
      var txt = $"{mob.FullName.Capitalize()} screams!";
      return new AoEAction(gs, mob, mob.Loc, $"Confused#0#{scream.DC}#0", scream.Radius, txt);
    }
    else if (act is SummonTrait summon)
    {
      _lastUse[act.Name] = gs.Turn;
      return new SummonAction(mob.Loc, summon.Summons, 1)
      {
        GameState = gs,
        Actor = mob,
        Quip = summon.Quip
      };
    }
    else if (act is SummonUndeadTrait summonUndead)
    {
      _lastUse[act.Name] = gs.Turn;
      return new SummonAction(mob.Loc, summonUndead.Summons(gs, mob), 1) { GameState = gs, Actor = mob };
    }
    else if (act is HealAlliesTrait heal && mob.Traits.OfType<AlliesTrait>().FirstOrDefault() is AlliesTrait alliesTrait)
    {
      _lastUse[act.Name] = gs.Turn;
      List<Mob> candidates = [];
      foreach (ulong id in alliesTrait.IDs)
      {
        if (gs.ObjDb.GetObj(id) is Mob m)
        {
          var hp = m.Stats[Attribute.HP];
          if (hp.Curr < hp.Max)
            candidates.Add(m);
        }          
      }

      if (candidates.Count > 0)
      { 
        int i = gs.Rng.Next(candidates.Count);

        string castText = $"{mob.FullName.Capitalize()} {Grammar.Conjugate(mob, "cast")} a healing spell!";
        return new HealAction(gs, candidates[i], 4, 4)
        {
          Message = castText
        };
      }

      return new PassAction();
    }
    else if (act is ShriekTrait shriek)
    {
      _lastUse[act.Name] = gs.Turn;
      return new ShriekAction(gs, mob, shriek.ShriekRadius);
    }

    return new NullAction();
  }

  static Action CalcMoveAction(Mob mob, GameState gs)
  {
    // eventually add fleeing. Or maybe there will be a 
    // CalcRetreat() method since some monsters can teleport, etc

    if (mob.HasTrait<ConfusedTrait>()) 
      return new MoveAction(gs, mob, Util.RandomAdjLoc(mob.Loc, gs));    
    else 
    {
      Action acc;

      bool indifferent = mob.Stats[Attribute.MobAttitude].Curr == Mob.INDIFFERENT;      
      if (indifferent)
        acc = mob.MoveStrategy.RandomMoveAction(mob, gs);
      else 
        acc = mob.MoveStrategy.MoveAction(mob, gs);

      // Note: maybe HomebodyTrait should prevent a mob from becomming
      // aggressive for reasons other than taking damage?
      if (indifferent && acc is MoveAction move && mob.Traits.OfType<HomebodyTrait>().FirstOrDefault() is HomebodyTrait homebody)
      {
        if (Util.Distance(move.Loc, homebody.Loc) > homebody.Range)
          return new PassAction();
      }

      return acc;
    }      
  }

  Action SelectAction(Mob actor, GameState gs)
  {
    foreach (var act in actor.Actions)
    {
      // Actions should be in the list in order of prerfence
      if (_lastUse.TryGetValue(act.Name, out var last) && last + act.Cooldown > gs.Turn)
        continue;

      if (act.Available(actor, gs))
        return FromTrait(actor, act, gs);
    }

    return new NullAction();
  }

  Action CalculateEscape(Mob actor, GameState gs)
  {
    foreach (Loc adj in Util.Adj8Locs(actor.Loc))
    {
      if (gs.TileAt(adj).Type == TileType.TeleportTrap && !gs.ObjDb.Occupied(adj) && actor.HasTrait<IntelligentTrait>())
      {
        if (gs.LastPlayerFoV.Contains(adj))
          gs.UIRef().AlertPlayer($"{actor.FullName.Capitalize()} jumps into the teleport trap!");
        return new MoveAction(gs, actor, adj);
      }
    }

    foreach (var act in actor.Actions)
    {
      if (act.ActionType == ActionType.Movement && _lastUse.TryGetValue(act.Name, out var last) && last + act.Cooldown > gs.Turn)
        return FromTrait(actor, act, gs);
    }

    Action escapeAction = actor.MoveStrategy.EscapeRoute(actor, gs);
    // If we ge a PassAction back, there was no viable MoveAction for
    // the mob to take.
    if (escapeAction is PassAction)
    {
      // If a monster is cornered, they might freeze and if not
      // do a regular action (such as attack the player)
      if (gs.Rng.NextDouble() < 0.2)
        return escapeAction;

      escapeAction = SelectAction(actor, gs);
      if (escapeAction is NullAction)
        escapeAction = new PassAction();
    }

    return escapeAction;
  }

  public virtual Action CalcAction(Mob actor, GameState gs)
  {
    if (actor.HasActiveTrait<ParalyzedTrait>())
      return new PassAction();
    if (actor.HasTrait<SleepingTrait>())
      return new PassAction();

    switch (actor.Stats[Attribute.MobAttitude].Curr)
    {
      case Mob.INACTIVE:
        return new PassAction();
      case Mob.INDIFFERENT:
        var passive = actor.Actions.Where(a => a.ActionType == ActionType.Passive && a.Available(actor, gs))
                                   .ToList();
        if (passive.Count > 0)
        {
          return FromTrait(actor, passive[gs.Rng.Next(passive.Count)], gs);
        }
        else if (gs.Rng.NextDouble() < 0.5) 
        {
          return new PassAction();
        }
        else
        {
          return CalcMoveAction(actor, gs);
        }
      case Mob.AFRAID:
        return CalculateEscape(actor, gs);
      case Mob.AGGRESSIVE:
        Action action = SelectAction(actor, gs);
        return action is NullAction ? CalcMoveAction(actor, gs) : action;
    }

    return new PassAction();
  
    // if (actor.HasTrait<WorshiperTrait>() && actor.HasTrait<IndifferentTrait>())
    // {
    //   var wt = actor.Traits.OfType<WorshiperTrait>().First();
    //   // Worshippers just hang out near their altar until they become hostile.
    //   Loc loc = Util.RandomAdjLoc(actor.Loc, gs);

    //   Action act;
    //   if (!gs.ObjDb.Occupied(loc) && Util.Distance(wt.Altar, loc) < 4)
    //     act = new MoveAction(gs, actor, loc);
    //   else
    //     act = new PassAction(gs, actor);
    //   if (wt.Chant != "" && gs.Rng.Next(13) == 0)
    //     act.Quip = wt.Chant;

    //   return act;
    // }
  }

  public (Action, Inputer?) Chat(Mob actor, GameState gameState) => (new NullAction(), null);
}

// Disguised monsters behave differently while they are disguised, but then act like a normal monster
// so it just seemed simple (or easy...) to extend MonsterBevaviour
class DisguisedMonsterBehaviour : MonsterBehaviour
{
  public override Action CalcAction(Mob actor, GameState gs)
  {
    bool disguised = actor.Stats[Attribute.InDisguise].Curr == 1;
    if (disguised && Util.Distance(actor.Loc, gs.Player.Loc) > 1)
      return new PassAction();

    if (disguised)
    {
      var disguise = actor.Traits.OfType<DisguiseTrait>().First();
      string txt = $"The {disguise.DisguiseForm} was really {actor.Name.IndefArticle()}!";
      gs.UIRef().AlertPlayer([txt]);
      actor.Glyph = disguise.TrueForm;
      actor.Stats[Attribute.InDisguise].SetMax(0);
    }

    return base.CalcAction(actor, gs);
  }
}

class VillagePupBehaviour : IBehaviour
{
  static bool LocInTown(int row, int col, Town t)
  {
    if (row < t.Row || row >= t.Row + t.Height)
      return false;
    if (col < t.Col || col >= t.Col + t.Width)
      return false;
    return true;
  }

  static bool Passable(TileType type) => type switch
  {
    TileType.Grass => true,
    TileType.GreenTree => true,
    TileType.RedTree => true,
    TileType.OrangeTree => true,
    TileType.YellowTree => true,
    TileType.Dirt => true,
    TileType.Sand => true,
    TileType.Bridge => true,
    _ => false
  };

  public Action CalcAction(Mob pup, GameState gameState)
  {
    double roll = gameState.Rng.NextDouble();
    if (roll < 0.25)
      return new PassAction();

    // in the future, when they become friendly with the player they'll move toward them
    List<Loc> mvOpts = [];
    foreach (var sq in Util.Adj8Sqs(pup.Loc.Row, pup.Loc.Col))
    {
      if (LocInTown(sq.Item1, sq.Item2, gameState.Town))
      {
        var loc = pup.Loc with { Row = sq.Item1, Col = sq.Item2 };
        if (Passable(gameState.TileAt(loc).Type) && !gameState.ObjDb.Occupied(loc))
          mvOpts.Add(loc);
      }
    }

    // Keep the animal tending somewhat to move toward the center of town
    var centerRow = gameState.Town.Row + gameState.Town.Height / 2;
    var centerCol = gameState.Town.Col + gameState.Town.Width / 2;
    var adj = pup.Loc;
    if (pup.Loc.Row < centerRow && pup.Loc.Col < centerCol)
      adj = pup.Loc with { Row = pup.Loc.Row + 1, Col = pup.Loc.Col + 1 };
    else if (pup.Loc.Row > centerRow && pup.Loc.Col > centerCol)
      adj = pup.Loc with { Row = pup.Loc.Row - 1, Col = pup.Loc.Col - 1 };
    else if (pup.Loc.Row < centerRow && pup.Loc.Col > centerCol)
      adj = pup.Loc with { Row = pup.Loc.Row + 1, Col = pup.Loc.Col - 1 };
    else if (pup.Loc.Row > centerRow && pup.Loc.Col < centerCol)
      adj = pup.Loc with { Row = pup.Loc.Row - 1, Col = pup.Loc.Col + 1 };

    if (adj != pup.Loc && Passable(gameState.TileAt(adj).Type) && !gameState.ObjDb.Occupied(adj))
    {
      mvOpts.Add(adj);
      mvOpts.Add(adj);
      mvOpts.Add(adj);
    }

    if (mvOpts.Count == 0)
      return new PassAction();
    else
      return new MoveAction(gameState, pup, mvOpts[gameState.Rng.Next(mvOpts.Count)]);
  }

  public (Action, Inputer) Chat(Mob animal, GameState gs)
  {
    var sb = new StringBuilder(animal.Appearance.IndefArticle().Capitalize());
    sb.Append(".\n\n");

    // Eventually the dog might have different sounds based on mood, etc
    sb.Append("Arf! Arf!");

    gs.UIRef().SetPopup(new Popup(sb.ToString(), "", -1, -1));
    return (new PassAction(), new PauseForMoreInputer());
  }
}

class InnkeeperBehaviour : NPCBehaviour
{
  public override (Action, Inputer?) Chat(Mob actor, GameState gameState)
  {
    var acc = new InnkeeperInputer(actor, gameState);
    var action = new InnkeeperServiceAction(gameState, actor);

    return (action, acc);    
  }
}

class PriestBehaviour : NPCBehaviour
{
  DateTime _lastIntonation = new(1900, 1, 1);

  public override Action CalcAction(Mob actor, GameState gameState)
  {
    Action action = base.CalcAction(actor, gameState);
    if ((DateTime.Now - _lastIntonation).TotalSeconds > 10)
    {
      _lastIntonation = DateTime.Now;
      action.Quip = "Praise be to Huntokar!";
    }

    return action;
  }

  public override (Action, Inputer?) Chat(Mob actor, GameState gameState)
  {
    if (gameState.Player.HasTrait<ShunnedTrait>())
    {
      var acc = new PriestInputer(actor, "Oh my child, what you have done?\n\nAn offering to Huntokar is needed to wash clean the stain on you.", gameState);
      var action = new PriestServiceAction(gameState, actor);
      return (action, acc);
    }

    return base.Chat(actor, gameState);
  }
}

class SmithBehaviour : IBehaviour
{
  DateTime _lastBark = new(1900, 1, 1);

  static string PickBark(Mob smith, Random rng)
  {
    var items = smith.Inventory.UsedSlots()
                               .Select(smith.Inventory.ItemAt)
                               .Select(si => si.Item1).ToList();
    Item? item;
    if (items.Count > 0)
      item = items[rng.Next(items.Count)];
    else
      item = null;

    int roll = rng.Next(2);
    if (roll == 0 && item is not null)
    {
      if (item.Type == ItemType.Weapon)
      {
        if (item.Traits.Any(t => t is DamageTrait trait && trait.DamageType == DamageType.Blunt))
          return $"A stout {item.Name} will serve you well!";
        else
          return $"A sharp {item.Name} will serve you well!";
      }
      else if (item.Name == "helmet" || item.Name == "shield")
        return $"A sturdy {item.Name} will serve you well!";
      else
        return $"Some sturdy {item.Name} will serve you well!";
    }
    else
    {
      return "More work...";
    }
  }

  public Action CalcAction(Mob smith, GameState gameState)
  {
    if ((DateTime.Now - _lastBark).TotalSeconds > 10)
    {
      _lastBark = DateTime.Now;

      return new PassAction()
      {
        Actor = smith,
        GameState = gameState,
        Quip = PickBark(smith, gameState.Rng)
      };
    }
    else
    {
      return new PassAction();
    }
  }

  string Blurb(Mob mob, GameState gs)
  {
    double markup = mob.Stats[Attribute.Markup].Curr / 100.0;
    var sb = new StringBuilder();
    sb.Append('"');

    int roll = gs.Rng.Next(3);
    switch (roll)
    {
      case 0:
        if (markup > 1.75)
          sb.Append("If you're looking for arms or armour, I'm the only game in town!");
        else
          sb.Append("You'll want some weapons or better armour before venturing futher!");
        break;
      case 1:
        sb.Append("Weapons or armour showing signs of wear and tear? I can help with that!");
        break;
      case 2:
        sb.Append("If you find weird gems or monster parts, I may be able to use them to spruce up your gear!");
        break;
    }  
    
    sb.Append('"');

    return sb.ToString();
  }

  public (Action, Inputer) Chat(Mob actor, GameState gs)
  {
    if (gs.Player.HasTrait<ShunnedTrait>())
    {
      return (new NullAction(), new PauseForMoreInputer());
    }

    var acc = new SmithyInputer(actor, Blurb(actor, gs), gs);
    var action = new ShoppingCompletedAction(gs, actor);

    return (action, acc);
  }
}

class GrocerBehaviour : IBehaviour
{
  DateTime _lastBark = new(1900, 1, 1);

  static string PickBark(Random rng)
  {
    int roll = rng.Next(3);
    if (roll == 0)
      return "Supplies for the prudent adventurer!";
    else if (roll == 1)
      return "Check out our specials!";
    else
      return "Store credit only.";
  }

  public Action CalcAction(Mob grocer, GameState gameState)
  {
    if ((DateTime.Now - _lastBark).TotalSeconds > 10)
    {
      _lastBark = DateTime.Now;

      return new PassAction()
      {
        Actor = grocer,
        GameState = gameState,
        Quip = PickBark(gameState.Rng)
      };
    }
    
    return new PassAction();    
  }

  public (Action, Inputer) Chat(Mob actor, GameState gs)
  {
    if (gs.Player.HasTrait<ShunnedTrait>())
    {
      return (new NullAction(), new PauseForMoreInputer());
    }
    var sb = new StringBuilder();
    sb.Append("\"Welcome to the ");
    sb.Append(gs.Town.Name);
    sb.Append(" market!\"");

    var acc = new ShopMenuInputer(actor, sb.ToString(), gs);
    var action = new ShoppingCompletedAction(gs, actor);

    return (action, acc);
  }
}

class NPCBehaviour : IBehaviour, IDialoguer
{
  List<DialogueOption> Options { get; set; } = [];

  public virtual Action CalcAction(Mob actor, GameState gameState)
  {
    if (gameState.Rng.Next(3) == 0)
    {
      Loc loc = Util.RandomAdjLoc(actor.Loc, gameState);
      TileType tile = gameState.TileAt(loc).Type;
      
      if (gameState.ObjDb.Occupied(loc))
        return new PassAction();
      if (gameState.ObjDb.BlockersAtLoc(loc))
        return new PassAction();
      if (!(tile == TileType.WoodFloor || tile == TileType.StoneFloor))
        return new PassAction();
      
      return new MoveAction(gameState, actor, loc);      
    }
    
    return new PassAction();    
  }

  public virtual (Action, Inputer?) Chat(Mob actor, GameState gameState)
  {
    if (gameState.Player.HasTrait<ShunnedTrait>())
    {
      return (new NullAction(), new PauseForMoreInputer());
    }

    var acc = new Dialoguer(actor, gameState);
    var action = new CloseMenuAction(gameState, 1.0);

    return (action, acc);
  }

  public (string, List<(string, char)>) CurrentText(Mob mob, GameState gs)
  {
    string scriptFile = mob.Traits.OfType<DialogueScriptTrait>().First().ScriptFile;
    var dialogue = new DialogueInterpreter();

    string txt = dialogue.Run(scriptFile, mob, gs);
    Options = dialogue.Options;
    List<(string, char)> opts = Options.Select(o => (o.Text, o.Ch)).ToList();
    
    return (txt, opts);
  }

  public void SelectOption(Mob mob, char choice, GameState gs)
  {
    foreach (DialogueOption opt in Options)
    {
      if (opt.Ch == choice)
      {
        var dialogue = new DialogueInterpreter();
        dialogue.Run(opt.Expr, mob, gs);
        break;
      }
    }
  }
}

class MayorBehaviour : NPCBehaviour
{
  Stack<Loc> _path = [];
  DateTime _lastBark = new(1900, 1, 1);

  public override Action CalcAction(Mob actor, GameState gameState)
  {
    if (_path.Count > 0)
    {
      // The mayor is on their way somewhere
      var mv = _path.Pop();

      // Maybe have them say 'Excuse me' in a bark?
      // Should probably also have a bail out if space is still occupied
      // after a few turns
      if (gameState.ObjDb.Occupied(mv)) 
      {
        _path.Push(mv);
        return new PassAction();
      }

      Tile tile = gameState.TileAt(mv);
      if (tile.Type == TileType.ClosedDoor)
      {
        // Should I implement code to make them polite and close the door after?
        _path.Push(mv);
        return new OpenDoorAction(gameState, actor, gameState.Wilderness, mv);
      }
      else
      {
        return new MoveAction(gameState, actor, mv);
      }
    }

    // The mayor's schedule is that from 8:00am to 6:00pm, they'll hang out in the 
    // town square. From 6:00pm to 9:00pm they'll be in the tavern
    var time = gameState.CurrTime();

    if (time.Item1 >= 8 && time.Item1 < 18)
    {
      return DayTimeSchedule(actor, gameState); 
    }
    else if (time.Item1 >= 18 && time.Item1 < 22)
    {
      return EveningSchedule(actor, gameState);
    }
    else
    {
      return NightSchedule(actor, gameState);
    }
  }

  Action DayTimeSchedule(Actor mayor, GameState gs)
  {
    Action action = new PassAction();

    // The mayor wants to be hanging out in the town square
    if (gs.Town.TownSquare.Contains(mayor.Loc))
    {
      // Pick a random move once in a while
      if (gs.Rng.Next(4) == 0)
      {
        var loc = Util.RandomAdjLoc(mayor.Loc, gs);
        var tile = gs.TileAt(loc);
        if (tile.Passable() && !gs.ObjDb.Occupied(loc))
        {
          action = new MoveAction(gs, mayor, loc);
          if ((DateTime.Now - _lastBark).TotalSeconds > 10)
          {
            action.Quip = "Today at least seems peaceful";
            _lastBark = DateTime.Now;
          }
          return action;
        }
      }
    }
    else if (_path.Count == 0)
    {
      var townSqaure = gs.Town.TownSquare.ToList();
      Loc goal = PickDestination(gs.Wilderness, townSqaure, TravelCosts, gs.Rng);
      _path = AStar.FindPath(gs.Wilderness, mayor.Loc, goal, TravelCosts);
    }

    return action;
  }

  Action EveningSchedule(Actor mayor, GameState gs)
  {
    // In the evening, the mayor will be in the tavern
    if (gs.Town.Tavern.Contains(mayor.Loc))
    {
      var loc = Util.RandomAdjLoc(mayor.Loc, gs);
      var tile = gs.TileAt(loc);
      if (tile.Passable() && !gs.ObjDb.Occupied(loc))
        return new MoveAction(gs, mayor, loc);
    }
    else if (_path.Count == 0)
    {
      var tavern = gs.Town.Tavern.ToList();
      Loc goal = PickDestination(gs.Wilderness, tavern, TravelCosts, gs.Rng);
      _path = AStar.FindPath(gs.Wilderness, mayor.Loc, goal, TravelCosts);
    }

    return new PassAction();
  }

  Action NightSchedule(Actor mayor, GameState gs)
  {
    int homeID = mayor.Stats[Attribute.HomeID].Curr;
    var home = gs.Town.Homes[homeID];

    if (!home.Contains(mayor.Loc))
    {
      Loc goal = PickDestination(gs.Wilderness, [.. home], TravelCosts, gs.Rng);
      _path = AStar.FindPath(gs.Wilderness, mayor.Loc, goal, TravelCosts);
    }

    return new PassAction();
  }

  static Dictionary<TileType, int> TravelCosts
  {
    get
    {
      Dictionary<TileType, int> costs = [];
      costs.Add(TileType.Grass, 1);
      costs.Add(TileType.Sand, 1);
      costs.Add(TileType.Dirt, 1);
      costs.Add(TileType.Bridge, 1);
      costs.Add(TileType.GreenTree, 1);
      costs.Add(TileType.RedTree, 1);
      costs.Add(TileType.OrangeTree, 1);
      costs.Add(TileType.YellowTree, 1);
      costs.Add(TileType.StoneFloor, 1);
      costs.Add(TileType.WoodFloor, 1);
      costs.Add(TileType.OpenDoor, 1);
      costs.Add(TileType.Well, 1);
      costs.Add(TileType.ClosedDoor, 2);
      costs.Add(TileType.Water, 3);

      return costs;
    }
  }

  static Loc PickDestination(Map map, List<Loc> options, Dictionary<TileType, int> passable, Random rng)
  {
    do
    {
      Loc goal = options[rng.Next(options.Count)];
      Tile tile = map.TileAt(goal.Row, goal.Col);
      if (passable.ContainsKey(tile.Type))
        return goal;
    }
    while (true);
  }
}

class WidowerBehaviour: NPCBehaviour
{
  DateTime _lastBark = new(1900, 1, 1);

  static string PickBark(Mob mob, Random rng)
  {
    int state;
    if (mob.Stats.TryGetValue(Attribute.DialogueState, out var stateState))
      state = stateState.Curr;
    else
      state = 0;

    List<string> barks = [];
    if (state >= 4)
    {
      barks.Add("I miss you so!");
      barks.Add("Oh why did you have to be an adventurer?");
    }
    else
    {
      barks.Add("Sigh...");
      barks.Add("Are you safe?");
      barks.Add("When will you return?");
    }

    return barks[rng.Next(barks.Count)];
  }

  public override Action CalcAction(Mob actor, GameState gs)
  {
    Action action = base.CalcAction(actor, gs);
    if ((DateTime.Now - _lastBark).TotalSeconds > 10)
    {
      _lastBark = DateTime.Now;
      action.Quip = PickBark(actor, gs.Rng);
      action.Actor = actor;
      action.GameState = gs;
    }

    return action;
  }
}
