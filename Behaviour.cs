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

using System.Reflection.Metadata.Ecma335;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

namespace Yarl2;

// Behaviour tree stuff
enum PlanStatus { Success, Failure, Running }

interface IPathBuilder
{
  Stack<Loc> BuildPath(Loc start);
}

class FindPathToArea(HashSet<Loc> area, GameState gs) : IPathBuilder
{
  HashSet<Loc> Area { get; set; } = area;
  GameState GS { get; set; } = gs;

  public Stack<Loc> BuildPath(Loc start)
  {
    List<Loc> locs = [..Area];
    locs.Shuffle(GS.Rng);

    foreach (Loc loc in locs)
    {
      Stack<Loc> path = AStar.FindPath(GS.MapForLoc(start), start, loc, TravelCosts, false);
      if (path.Count > 0)
        return path;
    }

    return [];
  }

  Loc PickLocInArea(HashSet<Loc> locs)
  {
    int i = GS.Rng.Next(locs.Count);
    return locs.ToList()[i];
  }

  static Dictionary<TileType, int> TravelCosts
  {
    get
    {
      Dictionary<TileType, int> costs = [];
      costs.Add(TileType.Grass, 1);
      costs.Add(TileType.Sand, 1);
      costs.Add(TileType.Dirt, 0);
      costs.Add(TileType.Bridge, 1);
      costs.Add(TileType.GreenTree, 1);
      costs.Add(TileType.RedTree, 1);
      costs.Add(TileType.OrangeTree, 1);
      costs.Add(TileType.YellowTree, 1);
      costs.Add(TileType.Conifer, 1);
      costs.Add(TileType.StoneFloor, 1);
      costs.Add(TileType.WoodFloor, 1);
      costs.Add(TileType.OpenDoor, 1);
      costs.Add(TileType.Well, 1);
      costs.Add(TileType.ClosedDoor, 2);
      costs.Add(TileType.Water, 3);

      return costs;
    }
  }
}

abstract class BehaviourNode
{
  public abstract PlanStatus Execute(Mob mob, GameState gs);
}

class Selector(List<BehaviourNode> nodes) : BehaviourNode
{
  List<BehaviourNode> Children { get; set; } = nodes;
  int Curr { get; set; } = 0;

  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    while (Curr < Children.Count)
    {
      PlanStatus status = Children[Curr].Execute(mob, gs);
      if (status == PlanStatus.Running)
      {
        return status;
      }

      if (status == PlanStatus.Success)
      {
        return status;
      }

      ++Curr;
    }

    return PlanStatus.Failure;
  }
}

class Sequence(List<BehaviourNode> nodes) : BehaviourNode
{
  List<BehaviourNode> Children { get; set; } = nodes;
  int Curr { get; set; } = 0;

  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    while (Curr < Children.Count) 
    {
      PlanStatus status = Children[Curr].Execute(mob, gs);

      if (status == PlanStatus.Running)
      {
        return status;
      }
            
      if (status == PlanStatus.Failure)
      {
        Curr = 0;
        return status;
      }

      ++Curr;
    }

    return PlanStatus.Success;
  }
}

class RepeatWhile(BehaviourNode condition, BehaviourNode child) : BehaviourNode
{
  BehaviourNode Condition { get; set; } = condition;
  BehaviourNode Child { get; set; } = child;

  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    var result = Condition.Execute(mob, gs);

    if (result == PlanStatus.Success)
      return Child.Execute(mob, gs);

    return PlanStatus.Failure;
  }  
}

class WanderInArea(HashSet<Loc> area) : BehaviourNode
{
  HashSet<Loc> Area { get; set; } = area;

  // I'll need to take into account flying/floating creatures eventually
  static bool LocOpen(Map map, GameState gs, Loc loc)
  {
    Tile tile = map.TileAt(loc.Row, loc.Col);

    return tile.Passable() && !gs.ObjDb.Occupied(loc) && !gs.ObjDb.BlockersAtLoc(loc);
  }

  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    Map map = gs.MapForActor(mob);

    List<Loc> adjs = [.. Util.Adj8Locs(mob.Loc).Where(l => Area.Contains(l) && LocOpen(map, gs, l))];

    if (adjs.Count > 0)
    {
      Loc loc = adjs[gs.Rng.Next(adjs.Count)];
      
      Action mv = new MoveAction(gs, mob, loc);
      string bark = mob.GetBark(gs);
      if (bark != "")
        mv.Quip = bark;
      mob.ExecuteAction(mv);
      return PlanStatus.Running;
    }

    return PlanStatus.Failure;
  }
}

class InArea(HashSet<Loc> sqs) : BehaviourNode
{
  readonly HashSet<Loc> Locations = sqs;

  public override PlanStatus Execute(Mob mob, GameState gs) =>
    Locations.Contains(mob.Loc) ? PlanStatus.Success : PlanStatus.Failure;
}

class IsDaytime : BehaviourNode
{
  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    var (hour, _) = gs.CurrTime();
    return hour >= 7 && hour < 19 ? PlanStatus.Success
                                  : PlanStatus.Failure;
  }
}

class IsEvening : BehaviourNode
{
  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    var (hour, _) = gs.CurrTime();
    return hour >= 19 && hour < 22 ? PlanStatus.Success
                                   : PlanStatus.Failure;
  }
}

class IsNight : BehaviourNode
{
  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    var (hour, _) = gs.CurrTime();
    return hour >= 22 || hour <= 6 ? PlanStatus.Success
                                   : PlanStatus.Failure;
  }
}

class WalkPath(Stack<Loc> path) : BehaviourNode
{
  Stack<Loc> Path { get; set; } = path;
  Loc PrevLoc { get; set; }

  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    if (Path.Count > 0)
    {
      Loc next = Path.Peek();
      Tile nextTile = gs.TileAt(next);
      Tile prevTile = gs.TileAt(PrevLoc);

      Action action;
      if (nextTile is Door door && !door.Open)
      {
        action = new OpenDoorAction(gs, mob, next);
      }
      else if (prevTile is Door prevDoor && prevDoor.Open)
      {
        action = new CloseDoorAction(gs, mob, PrevLoc);
      }
      else
      {
        PrevLoc = mob.Loc;
        action = new MoveAction(gs, mob, next);
        Path.Pop();
      }

      bool result = mob.ExecuteAction(action);

      return result ? PlanStatus.Running : PlanStatus.Failure;
    }

    return PlanStatus.Success;
  }  
}

class NavigateToGoal(BehaviourNode goal, IPathBuilder pathBuilder) : BehaviourNode
{
  BehaviourNode Goal { get; set; } = goal;
  IPathBuilder PathBuilder { get; set; } = pathBuilder;
  Stack<Loc>? Path { get; set; } = null;
  Loc PrevLoc { get; set; }

  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    Path ??= PathBuilder.BuildPath(mob.Loc);

    PlanStatus status = Goal.Execute(mob, gs);
    if (status == PlanStatus.Success)
    {
      return status;
    }

    if (Path.Count > 0)
    {
      Loc next = Path.Pop();
      Tile nextTile = gs.TileAt(next);
      Tile prevTile = gs.TileAt(PrevLoc);
      if (nextTile is Door door && !door.Open)
      {
        Path.Push(next);
        mob.ExecuteAction(new OpenDoorAction(gs, mob, next));
        return PlanStatus.Running;
      }
      else if (prevTile is Door prevDoor && prevDoor.Open)
      {
        Path.Push(next);
        mob.ExecuteAction(new CloseDoorAction(gs, mob, PrevLoc));
        return PlanStatus.Running;
      }
      else
      {
        PrevLoc = mob.Loc;
        mob.ExecuteAction(new MoveAction(gs, mob, next));
        return PlanStatus.Running;
      }
    }

    return PlanStatus.Failure;
  }
}

class Planner
{
  static HashSet<Loc> OnlyFloorsInArea(Map map, HashSet<Loc> area)
  {
    static bool IsFloor(Map map, Loc loc)
    {
      TileType tile = map.TileAt(loc.Row, loc.Col).Type;
      return tile == TileType.WoodFloor || tile == TileType.StoneFloor;
    }

    return area.Where(l => IsFloor(map, l)).ToHashSet();
  }

  static BehaviourNode CreateMayorPlan(Actor actor, GameState gs)
  {    
    var (hour, _) = gs.CurrTime();
    if (hour >= 7 && hour < 19)
    {
      // daytimeplan
      FindPathToArea pathBuilder = new(gs.Town.TownSquare, gs);
      BehaviourNode movePlan = new WalkPath(pathBuilder.BuildPath(actor.Loc));

      BehaviourNode daytimeTest = new IsDaytime();
      return new Sequence(
        [movePlan, new RepeatWhile(daytimeTest, new WanderInArea(gs.Town.TownSquare))]
      );
    }
    else if (hour >= 19 && hour < 22)
    {
      // evening plan -- the mayor wants to be in the pub
      HashSet<Loc> tavernFloors = OnlyFloorsInArea(gs.Wilderness, gs.Town.Tavern);
      FindPathToArea pathBuilder = new(tavernFloors, gs);      
      BehaviourNode movePlan = new WalkPath(pathBuilder.BuildPath(actor.Loc));

      BehaviourNode eveningTest = new IsEvening();
      return new Sequence(
        [movePlan, new RepeatWhile(eveningTest, new WanderInArea(tavernFloors))]
      );
    }
    else
    {
      // night plan -- the mayor wants to be at home
      int homeId = actor.Stats[Attribute.HomeID].Curr;
      HashSet<Loc> homeFloors = OnlyFloorsInArea(gs.Wilderness, gs.Town.Homes[homeId]);
      FindPathToArea pathBuilder = new(homeFloors, gs);
      BehaviourNode movePlan = new WalkPath(pathBuilder.BuildPath(actor.Loc));

      BehaviourNode nightTest = new IsNight();
      return new Sequence(
        [movePlan, new RepeatWhile(nightTest, new WanderInArea(homeFloors))]
      );
    }    
  }

  static Loc PickLocInArea(HashSet<Loc> locs, GameState gs)
  {
    int i = gs.Rng.Next(locs.Count);
    return locs.ToList()[i];
  }

  public static BehaviourNode GetPlan(string plan, Actor actor, GameState gs) => plan switch
  {
    "MayorPlan" => CreateMayorPlan(actor, gs),
    _ => throw new Exception($"Unknown Behaviour Tree plan: {plan}")
  };
}

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

    return opts.Count == 0 ? new PassAction() : new MoveAction(gs, actor, opts[gs.Rng.Next(opts.Count)]);
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
    var mapWithDoors = gs.GetDMap("doors") ?? throw new Exception("No doors map found");
    List<(int, int, int)> adj = mapWithDoors.Neighbours(actor.Loc.Row, actor.Loc.Col);
    foreach (var sq in adj)
    {
      var loc = new Loc(actor.Loc.DungeonID, actor.Loc.Level, sq.Item1, sq.Item2);

      if (gs.CurrentMap.TileAt(loc.Row, loc.Col).Type == TileType.ClosedDoor)
      {
        return new OpenDoorAction(gs, actor, loc);
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
        return new OpenDoorAction(gs, actor, loc);
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
  string GetBark(Mob actor, GameState gs);
}

class NullBehaviour : IBehaviour
{
  private static readonly NullBehaviour instance = new();
  public static NullBehaviour Instance() => instance;

  public Action CalcAction(Mob actor, GameState gameState) => throw new NotImplementedException();  
  public (Action, Inputer?) Chat(Mob actor, GameState gameState) => throw new NotImplementedException();
  public string GetBark(Mob actor, GameState gs) => "";
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

  public string GetBark(Mob actor, GameState gs) => "";

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
      {
        _lastUse[act.Name] = gs.Turn;
        return act.Action(actor, gs);
      }
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
      {
        _lastUse[act.Name] = gs.Turn;
        return act.Action(actor, gs);
      }
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
    bool PassiveAvailable(ActionTrait action)
    {
      if (action.ActionType != ActionType.Passive || !action.Available(actor, gs))
        return false;

      if (!_lastUse.TryGetValue(action.Name, out var last))
        return true;
      else if (last + action.Cooldown <= gs.Turn)
        return true;

      return false;
    }

    foreach (Trait t in actor.Traits)
    {
      if (t is ParalyzedTrait || t is SleepingTrait)
        return new PassAction();
    }
    
    switch (actor.Stats[Attribute.MobAttitude].Curr)
    {
      case Mob.INACTIVE:
        return new PassAction();
      case Mob.INDIFFERENT:
        var passive = actor.Actions.Where(a => PassiveAvailable(a)).ToList();

        if (passive.Count > 0)
        {
          ActionTrait act = passive[gs.Rng.Next(passive.Count)];
          _lastUse[act.Name] = gs.Turn;
          return act.Action(actor, gs);
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
        if (gs.Rng.Next(10) == 0)
          actor.Stats[Attribute.MobAttitude].SetCurr(Mob.INDIFFERENT);
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
      gs.UIRef().AlertPlayer(txt);
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

  public string GetBark(Mob actor, GameState gs) => "";

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

class WitchBehaviour : NPCBehaviour
{
  DateTime _lastBark = new(1900, 1, 1);
  
  static string PickBark(Mob witch, GameState gs)
  {
    string grocerName = "";
    if (gs.FactDb.FactCheck("GrocerId") is SimpleFact fact)
    {
      ulong grocerId = ulong.Parse(fact.Value);
      if (gs.ObjDb.GetObj(grocerId) is Actor grocer)
        grocerName = grocer.FullName.Capitalize();
    }
    
    if (witch.HasTrait<InvisibleTrait>())
    {
      return gs.Rng.Next(3) switch
      {
        0 => "I'm over here.",
        1 => "Sophie's been trying invisibility potions again.",
        _ => "Is the potion working?"
      };
    }
    else
    {
      return gs.Rng.Next(4) switch
      {
        0 => "Sophie, did you see that sparrow?",
        1 => $"{grocerName} is charging HOW MUCH for mandrake root?",
        2 => "Do not tarry!",
        _ => "Dark augeries..."
      };
    }    
  }

  public override Action CalcAction(Mob witch, GameState gameState)
  {
    Action action  = new PassAction(gameState, witch);
    if ((DateTime.Now - _lastBark).TotalSeconds > 9)
    {
      action.Quip = PickBark(witch, gameState);
      _lastBark = DateTime.Now;
    }

    return action;
  }

  public override (Action, Inputer?) Chat(Mob actor, GameState gameState)
  {
    var acc = new WitchInputer(actor, gameState);
    var action = new WitchServiceAction(gameState, actor);
    
    return (action, acc);
  }
}

class SmithBehaviour : IBehaviour
{
  DateTime _lastBark = new(1900, 1, 1);

  // Eventually just replace/merge GetPark and PickBark
  public string GetBark(Mob actor, GameState gs) 
  {
    return PickBark(actor, gs.Rng);
  }

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
    var sb = new StringBuilder();
    sb.Append('"');

    string blurb;

    if (gs.FactDb.FactCheck("DwarfMine") is not null && gs.Rng.NextDouble() < 0.25)
    {
      blurb = "The ancient dwarves used to mine mithril in their tunnels. I could do some keen work with mithril!";
    }
    else
    {
      blurb = gs.Rng.Next(3) switch
      {
        0 => "If you're looking for arms or armour, I'm the only game in town!",
        1 => "Weapons or armour showing signs of wear and tear? I can help with that!",
        _ => "If you find weird gems or monster parts, I may be able to use them to spruce up your gear!"
      };
    }

    sb.Append(blurb);
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

class AlchemistBehaviour : NPCBehaviour
{
  DateTime _lastBark = new(1900, 1, 1);

  static string PickBark(GameState gs) => gs.Rng.Next(4) switch
  {
    0 => "Kylie, what do you want for dinner?",
    1 => "I've been working on a new song.",
    2 => "We could use some rain!",
    _ => "I'd better get to the weeding."
  };

  public override Action CalcAction(Mob alchemist, GameState gameState)
  {
    Action action = new PassAction(gameState, alchemist);
    if ((DateTime.Now - _lastBark).TotalSeconds > 11)
    {
      action.Quip = PickBark(gameState);
      _lastBark = DateTime.Now;
    }

    return action;
  }

  public override (Action, Inputer?) Chat(Mob actor, GameState gs)
  {
    string s = "Oh, I dabble in alchemy and potioncraft if you're interested. It pays the bills between gigs.";
    var acc = new ShopMenuInputer(actor, s, gs);
    var action = new ShoppingCompletedAction(gs, actor);

    return (action, acc);
  }
}

class GrocerBehaviour : IBehaviour
{
  DateTime _lastBark = new(1900, 1, 1);

  // Merge/replace with PickBark()
  public string GetBark(Mob actor, GameState gs) => PickBark(gs.Rng);

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

  public virtual string GetBark(Mob actor, GameState gs) => "";

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
  DateTime _lastBark = new(1900, 1, 1);
  
  public override Action CalcAction(Mob actor, GameState gameState)
  {
    return new PassAction();
  }

  public override string GetBark(Mob actor, GameState gs)
  {
    string bark = "";

    if ((DateTime.Now - _lastBark).TotalSeconds > 10)
    {
      _lastBark = DateTime.Now;
      var (hour, _) = gs.CurrTime();
      if (hour >= 7 && hour < 19)
      {
        bark = "Today at least seems peaceful";
      }
      else if (gs.Town.Tavern.Contains(actor.Loc))
      {
        bark = gs.Rng.Next(3) switch
        {
          0 => "Maybe we should have a music festival in town?",
          1 => "Ah the sounds of cheer and commerce!",
          _ => "Drink and be merry, friends!"
        };        
      }
    }
    
    return bark;
  }

  void AddQuipToAction(Actor actor, GameState gs, Action action)
  {
    if ((DateTime.Now - _lastBark).TotalSeconds > 10)
    {
      action.Actor = actor;
      action.GameState = gs;
      action.Quip = "Today at least seems peaceful";
      _lastBark = DateTime.Now;
    }
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
