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

interface IMoveStrategy
{
  Action MoveAction(Mob actor, GameState gs);
}

class WallMoveStrategy : IMoveStrategy
{
  public Action MoveAction(Mob actor, GameState gs) =>
      new PassAction();
}

// For creatures that don't know how to open doors
class DumbMoveStrategy : IMoveStrategy
{
  public Action MoveAction(Mob actor, GameState gs)
  {
    var adj = gs.GetDMap().Neighbours(actor.Loc.Row, actor.Loc.Col);
    foreach (var sq in adj)
    {
      var loc = new Loc(actor.Loc.DungeonID, actor.Loc.Level, sq.Item1, sq.Item2);
      
      // We still check if the tile is passable because, say, a door might be
      // closed after the current djikstra map is calculated and before it is
      // refreshed
      if (!gs.ObjDb.Occupied(loc) && gs.TileAt(loc).Passable())
      {
        // the square is free so move there!
        return new MoveAction(gs, actor, loc);
      }
    }

    // If we can't find a move to do, pass
    return new PassAction();
  }
}

// I don't know what to call this class, but it's movement for creatures who
// can open doors. OpposableThumbMoveStrategy? :P
class DoorOpeningMoveStrategy : IMoveStrategy
{
  public Action MoveAction(Mob actor, GameState gs)
  {
    var adj = gs.GetDMap("doors").Neighbours(actor.Loc.Row, actor.Loc.Col);
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
}

class SimpleFlightMoveStrategy : IMoveStrategy
{
  public Action MoveAction(Mob actor, GameState gs)
  {
    var adj = gs.GetDMap("flying").Neighbours(actor.Loc.Row, actor.Loc.Col);
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
}

interface IBehaviour
{
  Action CalcAction(Mob actor, GameState gameState);
  (Action, Inputer?) Chat(Mob actor, GameState gameState);
}

// I think I'll likely eventually merge this into IBehaviour
interface IDialoguer
{
  (string, List<(string, char)>) CurrentText(Mob mob, GameState gs);
  void SelectOption(Mob actor, char opt, GameState gs);
}

class MonsterBehaviour : IBehaviour
{
  Dictionary<string, ulong> _lastUse = [];

  bool Available(ActionTrait act, int distance, ulong turn)
  {
    if (_lastUse.TryGetValue(act.Name, out var last) && last + act.Cooldown > turn)
    {
      return false;
    }

    if (act.MinRange <= distance && act.MaxRange >= distance)
      return true;

    return false;
  }

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
        return new BlinkAction(gs, mob, null);
      else if (act.Name == "FogCloud")
        return new FogCloudAction(gs, mob, CalcRangedTarget(mob, gs));
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
      return new AoEAction(gs, mob, mob.Loc, "Confused#0DC#0", scream.Radius, txt);
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
    else if (act is HealAlliesTrait heal && mob.Traits.OfType<AlliesTrait>().FirstOrDefault() is AlliesTrait alliesTrait)
    {
      _lastUse[act.Name] = gs.Turn;
      List<Mob> candidates = [];
      foreach (ulong id in alliesTrait.IDs)
      {
        var m = gs.ObjDb.GetObj(id) as Mob;
        var hp = m.Stats[Attribute.HP];
        if (hp.Curr < hp.Max)
          candidates.Add(m);
      }

      if (candidates.Count > 0)
      { 
        int i = gs.Rng.Next(candidates.Count);

        string castText = $"{mob.FullName.Capitalize()} {Grammar.Conjugate(mob, "cast")} a healing spell!";
        Message msg = new Message(castText, mob.Loc);
        return new HealAction(gs, candidates[i], 4, 4, null)
        {
          Message = msg
        };
      }

      return new PassAction();
    }

    return new NullAction();
  }

  static Action CalcMoveAction(Mob mob, GameState gs)
  {    
    if (mob.HasTrait<ConfusedTrait>()) 
      return new MoveAction(gs, mob, Util.RandomAdjLoc(mob.Loc, gs));
    else
      return mob.MoveStrategy.MoveAction(mob, gs);
  }

  public virtual Action CalcAction(Mob actor, GameState gs)
  {
    if (actor.Status == MobAttitude.Idle)
    {
      return new PassAction();
    }

    // Should prioritize an escape action if the monster is hurt?
    // Maybe mobs can eventually have a bravery stat?
    // Maybe there should be MobAttitude.Scare or Fleeing and then
    // if they are Fleeing try to get away
    
    if (actor.HasTrait<WorshiperTrait>() && (actor.Status == MobAttitude.Active || actor.Status == MobAttitude.Indifferent))
    {
      var wt = actor.Traits.OfType<WorshiperTrait>().First();
      // Worshippers just hang out near their altar until they become hostile.
      Loc loc = Util.RandomAdjLoc(actor.Loc, gs);

      Action act;
      if (!gs.ObjDb.Occupied(loc) && Util.Distance(wt.Altar, loc) < 4)
        act = new MoveAction(gs, actor, loc);
      else
        act = new PassAction(gs, actor);
      if (wt.Chant != "" && gs.Rng.Next(13) == 0)
        act.Quip = wt.Chant;

      return act;
    }

    // Actions should be in the list in order of prerfence
    foreach (var act in actor.Actions)
    {
      if (_lastUse.TryGetValue(act.Name, out var last) && last + act.Cooldown > gs.Turn)
        continue;

      if (act.Available(actor, gs))
        return FromTrait(actor, act, gs);
    }


    return CalcMoveAction(actor, gs);
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
      var msg = new Message(txt, actor.Loc);
      gs.UIRef().AlertPlayer([msg], "", gs);
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

class PriestBehaviour : IBehaviour, IDialoguer
{
  DateTime _lastIntonation = new(1900, 1, 1);

  public Action CalcAction(Mob actor, GameState gameState)
  {
    if ((DateTime.Now - _lastIntonation).TotalSeconds > 10)
    {
      _lastIntonation = DateTime.Now;

      return new PassAction()
      {
        Actor = actor,
        GameState = gameState,
        Quip = "Praise be to Huntokar!"
      };
    }
    else
    {
      return new PassAction();
    }
  }

  public (Action, Inputer) Chat(Mob priest, GameState gs)
  {
    var acc = new Dialoguer(priest, gs);
    var action = new CloseMenuAction(gs, 1.0);

    return (action, acc);
  }

  public (string, List<(string, char)>) CurrentText(Mob mob, GameState gs)
  {
    string scriptFile = mob.Traits.OfType<DialogueScriptTrait>().First().ScriptFile;
    var dialoguer = new DialogueLoader(scriptFile);

    return (dialoguer.Dialogue(mob, gs), []);
  }

  public void SelectOption(Mob actor, char opt, GameState gs) { }
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

  string Blurb(Mob mob)
  {
    double markup = mob.Stats[Attribute.Markup].Curr / 100.0;
    var sb = new StringBuilder();
    sb.Append('"');
    if (markup > 1.75)
      sb.Append("If you're looking for arms or armour, I'm the only game in town!");
    else
      sb.Append("You'll want some weapons or better armour before venturing futher!");
    sb.Append('"');

    return sb.ToString();
  }

  public (Action, Inputer) Chat(Mob actor, GameState gs)
  {
    var acc = new ShopMenuInputer(actor, Blurb(actor), gs);
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
    var sb = new StringBuilder();
    sb.Append("\"Welcome to the ");
    sb.Append(gs.Town.Name);
    sb.Append(" market!\"");

    var acc = new ShopMenuInputer(actor, sb.ToString(), gs);
    var action = new ShoppingCompletedAction(gs, actor);

    return (action, acc);
  }
}

class VeteranBehaviour : IBehaviour, IDialoguer
{
  public Action CalcAction(Mob actor, GameState gameState)
  {
    if (gameState.Rng.Next(3) == 0)
    {
      Loc loc = Util.RandomAdjLoc(actor.Loc, gameState);
      TileType tile = gameState.TileAt(loc).Type;
      if (!gameState.ObjDb.Occupied(loc) && (tile == TileType.WoodFloor || tile == TileType.StoneFloor))
        return new MoveAction(gameState, actor, loc);      
    }
    
    return new PassAction();    
  }

  public (Action, Inputer?) Chat(Mob actor, GameState gameState)
  {
    var acc = new Dialoguer(actor, gameState);
    var action = new CloseMenuAction(gameState, 1.0);

    return (action, acc);
  }

  public (string, List<(string, char)>) CurrentText(Mob mob, GameState gs)
  {
    var sb = new StringBuilder();
    List<(string, char)> opts = [];

    if (!mob.Stats.TryGetValue(Attribute.DialogueState, out var state) || state.Curr == 0)
    {
      string monsters = "";
      foreach (SimpleFact fact in gs.Facts.OfType<SimpleFact>())
      {
        if (fact.Name == "EarlyDenizen")
        {
          monsters = fact.Value;
          break;
        }
      }

      sb.Append("\"I used to be an adventurer like you! Sure the top floors of the dungeon are all ");
      sb.Append(monsters.Pluralize());
      sb.Append(" and rats, but then things get a lot worse.");

      if (gs.Player.Inventory.Zorkmids > 2)
      {
        sb.Append("\n\n");
        sb.Append("Buy me a drink and I'll repay you in wisdom.\"");

        opts.Add(($"Buy {mob.FullName} a drink. ([YELLOW $]2)", 'a'));        
      }
      else
      {
        sb.Append(" My advice is to take up woodcutting. Or have you considered goat farming?\"");
      }
    }
    else if (state.Curr == 1)
    {
      int maxDepth = gs.Player.Stats[Attribute.Depth].Max;
      if (maxDepth == 0)
        sb.Append("\"Stock up on torches and maybe a flagon of whiskey before you head into the depths!\"");
      else if (maxDepth <= 2)
        sb.Append("\"Some free advice: grind around on the first few floors and save up enough for your retirement!\"");
      else
        sb.Append("\"You've been HOW deep? The horrors that await you...\"");

      if (gs.Player.Inventory.Zorkmids > 2)
      {
        sb.Append("\n\n");
        sb.Append("Buy me a drink and I'll repay you in wisdom.\"");

        opts.Add(($"Buy {mob.FullName} a drink. ([YELLOW $]2)", 'a'));
      }
    }
    else if (state.Curr == 2)
    {
      sb.Append('"');
      sb.Append(Advice(gs.Rng));
      sb.Append('"');
      mob.Stats[Attribute.DialogueState] = new Stat(1);
    }

    return (sb.ToString(), opts);
  }

  // When there's more content in the game, I'll create a proper rumours file
  static string Advice(Random rng) => rng.Next(6) switch
  {
    0 => "Swords aren't so great against skeletons.",
    1 => "Owlbears definitely inherited the bear hug genes.",
    2 => "Someone once told me cloakers make their own flocks.",
    3 => "Mind your torch around wooden structures!",
    4 => "They say swinelings spread plague.",
    _ => "Never drink water. Fish pee in there!"
  };

  public void SelectOption(Mob veteran, char opt, GameState gs)
  {    
    if (opt == 'a')
    {
      // Buy veteran a drink
      gs.Player.Inventory.Zorkmids -= 2;
      veteran.Stats[Attribute.DialogueState] = new Stat(2);
    }
  }
}

class MayorBehaviour : IBehaviour, IDialoguer
{
  Stack<Loc> _path = [];
  DateTime _lastBark = new(1900, 1, 1);

  public Action CalcAction(Mob actor, GameState gameState)
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

  public (Action, Inputer?) Chat(Mob actor, GameState gameState)
  {
    var acc = new Dialoguer(actor, gameState);
    var action = new CloseMenuAction(gameState, 1.0);

    return (action, acc);
  }

  public (string, List<(string, char)>) CurrentText(Mob mob, GameState gs)
  {
    string scriptFile = mob.Traits.OfType<DialogueScriptTrait>().First().ScriptFile;
    var dialoguer = new DialogueLoader(scriptFile);

    return (dialoguer.Dialogue(mob, gs), []);
  }

  public void SelectOption(Mob actor, char opt, GameState gs) { }  
}

// A simple villager who can tell the player a few things about the dungeon
// and area around the town.
class Villager1Behaviour : IBehaviour, IDialoguer
{
  public Action CalcAction(Mob actor, GameState gameState)
  {
    return new PassAction();
  }

  public (Action, Inputer?) Chat(Mob actor, GameState gameState)
  {
    var acc = new Dialoguer(actor, gameState);
    var action = new CloseMenuAction(gameState, 1.0);

    return (action, acc);
  }

  public (string, List<(string, char)>) CurrentText(Mob mob, GameState gs)
  {
    string scriptFile = mob.Traits.OfType<DialogueScriptTrait>().First().ScriptFile;
    var dialoguer = new DialogueLoader(scriptFile);

    return (dialoguer.Dialogue(mob, gs), []);
  }

  public void SelectOption(Mob actor, char opt, GameState gs)
  {
    
  }
}

class WidowerBehaviour: IBehaviour, IDialoguer
{
  DateTime _lastBark = new(1900, 1, 1);

  static string PickBark(Mob mob, Random rng)
  {
    int state;

    if (mob.Stats.TryGetValue(Attribute.DialogueState, out var stateState))
      state = stateState.Curr;
    else
      state = 0;

    if (state == 7)
    {
      // The player has retrieved the token and the widower is in mourning
      string txt;
      if (rng.Next(2) == 0)
        txt = "I miss you so!";
      else
        txt = "Oh why did you have to be an adventurer?";

      return txt;
    }
    else
    {
      int roll = rng.Next(3);
      if (roll == 0)
        return "Sigh...";
      else if (roll == 1)
        return "Are you safe?";
      else
        return "When will you return?";
    }
  }

  public Action CalcAction(Mob actor, GameState gs)
  {
    if ((DateTime.Now - _lastBark).TotalSeconds > 10)
    {
      _lastBark = DateTime.Now;

      return new PassAction()
      {
        Actor = actor,
        GameState = gs,
        Quip = PickBark(actor, gs.Rng)
      };
    }

    // Move to a random adj sq in their home
    int homeID = actor.Stats[Attribute.HomeID].Curr;
    var sqs = gs.Town.Homes[homeID];
    List<Loc> adj = [];
    foreach (var sq in Util.Adj8Locs(actor.Loc))
    {
      var tile = gs.TileAt(sq);
      if (!gs.ObjDb.Occupied(sq) && (tile.Type == TileType.WoodFloor || tile.Type == TileType.StoneFloor))
      {
        adj.Add(sq);
      }
    }

    if (adj.Count > 0)
    {
      var mv = adj[gs.Rng.Next(adj.Count)];
      return new MoveAction(gs, actor, mv);
    }
    else
    {
      return new PassAction();
    }
  }

  public (Action, Inputer?) Chat(Mob actor, GameState gameState)
  {
    var acc = new Dialoguer(actor, gameState);
    var action = new CloseMenuAction(gameState, 1.0);

    return (action, acc);
  }

  static Actor? Partner(Mob mob, GameState gs)
  {
    foreach (var fact in gs.Facts)
    {
      if (fact is RelationshipFact rel && rel.Desc == "romantic" && (rel.Person1 == mob.ID || rel.Person2 == mob.ID))
      {
        ulong otherID = rel.Person1 == mob.ID ? rel.Person2 : rel.Person1;
        return (Actor)gs.ObjDb.GetObj(otherID);
      }
    }

    return null;
  }
  
  ulong TrinketID(Actor partner, GameState gs) 
  {
    foreach (BelongedToFact fact in gs.Facts.OfType<BelongedToFact>())
    {
      if (fact.OwnerID == partner.ID)
      {
        return fact.ItemID;
      }
    }

    return 0;
  }

  public (string, List<(string, char)>) CurrentText(Mob mob, GameState gs)
  {
    var partner = Partner(mob, gs);
    string name = partner.Name.Capitalize();

    int state;
    if (!mob.Stats.TryGetValue(Attribute.DialogueState, out var stateStat))
      state = 0;
    else
      state = stateStat.Curr;

    ulong trinketID = TrinketID(partner, gs);
    bool playerHasTrinket = gs.Player.Inventory.Contains(trinketID);

    // Player had the trinket but doesn't seem to have it
    if ((state == 3 || state == 4) && !playerHasTrinket)
    {
      mob.Stats[Attribute.DialogueState].SetMax(6);
      state = 6;
    }
    else if (playerHasTrinket && state == 6)
    {
      mob.Stats[Attribute.DialogueState].SetMax(4);
      state = 4;
    }

    if (playerHasTrinket && state == 0)
    {
      mob.Stats[Attribute.DialogueState].SetMax(3);
      var item = (Item)gs.ObjDb.GetObj(trinketID);
      var sb = new StringBuilder();
      sb.Append("That ");            
      sb.Append(item.Name);
      sb.Append("! It belong to my love!\n");
      sb.Append("Where did you find it?");

      List<(string, char)> options = [];
      options.Add(("In the ruins.", 'a'));
      //options.Add(($"Keep the {item.Name.DefArticle()}.", 'b'));

      return (sb.ToString(), options);
    }
    else if (playerHasTrinket && state == 1)
    {
      mob.Stats[Attribute.DialogueState].SetMax(3);
      var item = (Item) gs.ObjDb.GetObj(trinketID);
      var sb = new StringBuilder();
      sb.Append("\"You've found ");
      sb.Append(name);
      sb.Append("'s ");
      sb.Append(item.Name);
      sb.Append("! So...then they fell in the dungeon?\n");
      sb.Append("Will you give it to me as a keepsake?\"");

      List<(string, char)> options = [];
      options.Add(($"Hand over the {item.Name.DefArticle()}.", 'a'));
      options.Add(($"Keep the {item.Name.DefArticle()}.", 'b'));

      return (sb.ToString(), options);
    }
    else if (state == 0)
    {      
      var sb = new StringBuilder();
      sb.Append("\"Oh? Are you also an adventurer? ");
      sb.Append(name);
      sb.Append(" believed too they could prevail in the ruins.\"");

      List<(string, char)> options = [];
      options.Add(($"Who is {name}?", 'a'));
      options.Add(("Where are these ruins?", 'b'));

      return (sb.ToString(), options);
    }
    else if (state == 1)
    {
      // Player asked where the ruins were
      Loc dungoenLoc = Loc.Nowhere;
      foreach (LocationFact fact in gs.Facts.OfType<LocationFact>())
      {
        if (fact.Desc == "Dungeon Entrance")
          dungoenLoc = fact.Loc;
      }
      
      var sb = new StringBuilder();
      sb.Append('"');
      sb.Append(name);
      sb.Append(" strode off to the ");      
      sb.Append(Util.RelativeDir(mob.Loc, dungoenLoc));
      sb.Append(". Oh how resolute, heroic, they looked! The sun glinted on their spear tip,");
      sb.Append(" the wind tousled their hair. I hope they return to me soon.\"");

      List<(string, char)> options = [];
      options.Add(($"Who is {name}?", 'a'));
      
      return (sb.ToString(), options);
    }
    else if (state == 2)
    {
      // Player asked who the partner was
      var sb = new StringBuilder();

      if (gs.Rng.NextDouble() < 0.5)
      {
        sb.Append('"');
        sb.Append(name);
        sb.Append("? They came to ");
        sb.Append(gs.Town.Name.Capitalize());
        sb.Append(" seeking fame and glory in adventure. We danced in the tavern and walked together under the stars. ");
        sb.Append(name);
        sb.Append(" said would see the sights of Yendor together.\nIf you find them, please help them return to me.\"");
      }
      else
      {
        sb.Append("\"They are the most fearless soul I've known! ");
        sb.Append(name);
        sb.Append(" arrive in ");
        sb.Append(gs.Town.Name.Capitalize());
        sb.Append(", heard the tales of the ruins and the dangers within. They simply set their jaw and declared that they would drive away the darkness.\n");
        sb.Append("\nHow brave! How dreamy!\"");
      }

      List<(string, char)> options = [];
      options.Add(("Where are these ruins?", 'a'));

      return (sb.ToString(), options);
    }
    else if (state == 4)
    {
      var item = (Item)gs.ObjDb.GetObj(trinketID);
      var sb = new StringBuilder();
      sb.Append("\"Please give me ");
      sb.Append(name);
      sb.Append("'s ");
      sb.Append(item.Name);
      sb.Append(" that I may remember them by it!\"");

      List<(string, char)> options = [];
      options.Add(($"Hand over the {item.Name.DefArticle()}.", 'a'));
      options.Add(($"Keep the {item.Name.DefArticle()}.", 'b'));

      return (sb.ToString(), options);
    }
    else if (state == 5)
    {
      mob.Stats[Attribute.DialogueState].SetMax(4);
      return ("", []);
    }
    else if (state == 6)
    {
      string txt = "Please retrieve my love's token and bring it to me!";
      return (txt, []);
    }
    else if (state == 7) 
    {
      string txt = "Thank you so much, I will treasure it forever!";
      return (txt, []);
    }

    return ("", []);
  }

  public void SelectOption(Mob mob, char opt, GameState gs)
  {
    int state;
    if (!mob.Stats.TryGetValue(Attribute.DialogueState, out var attr))
    {
      mob.Stats.Add(Attribute.DialogueState, new Stat(0));
      state = 0;
    }
    else
    {
      state = attr.Curr;
    }

    if (state == 0 && opt == 'a')
    {
      mob.Stats[Attribute.DialogueState].SetMax(2);
    }
    else if (state == 0 && opt == 'b')
    {
      mob.Stats[Attribute.DialogueState].SetMax(1);
    }
    else if (state == 1 && opt == 'a')
    {
      mob.Stats[Attribute.DialogueState].SetMax(2);
    }
    else if (state == 2 && opt == 'a')
    {
      mob.Stats[Attribute.DialogueState].SetMax(1);
    }
    else if (state == 3 && opt == 'a')
    {
      ulong trinketID = TrinketID(Partner(mob, gs), gs);
      Item trinket = gs.Player.Inventory.RemoveByID(trinketID);
      mob.Inventory.Add(trinket, mob.ID);

      // TODO: need to give the player a reward!

      // give the trinket to the NPC
      mob.Stats[Attribute.DialogueState].SetMax(7);
    }
    else if (state == 3 && opt == 'b')
    {
      mob.Stats[Attribute.DialogueState].SetMax(5);
    }
    else if (state == 4 && opt == 'b')
    {
      mob.Stats[Attribute.DialogueState].SetMax(5);
    }
  }
}
