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

using System.Text;

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
      Stack<Loc> path = AStar.FindPath(GS.ObjDb, GS.MapForLoc(start), start, loc, TravelCosts, false);
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
        Curr = 0;
        return status;
      }

      ++Curr;
    }

    Curr = 0;
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

    Curr = 0;
    
    return PlanStatus.Success;
  }
}

class MoveLevel : BehaviourNode
{
  public override PlanStatus Execute(Mob mob, GameState gs)
  {  
    // They won't move levels if the player can see them
    if (gs.LastPlayerFoV.Contains(mob.Loc))
      return PlanStatus.Failure;

    Dungeon dungeon = gs.Campaign.Dungeons[mob.Loc.DungeonID];
    int nextLevel;
    if (mob.Loc.Level == 0)
      nextLevel = 1;
    else if (mob.Loc.Level == dungeon.LevelMaps.Count - 1)
      nextLevel = mob.Loc.Level - 1;
    else if (gs.Rng.NextDouble() <= 0.5)
      nextLevel = mob.Loc.Level - 1;
    else
      nextLevel = mob.Loc.Level + 1;

    Map nextMap = dungeon.LevelMaps[nextLevel];
    List<Loc> floors = nextMap.ClearFloors(mob.Loc.DungeonID, nextLevel, gs.ObjDb);
    Loc dest = floors[gs.Rng.Next(floors.Count)];

    // This is to expend the actor's energy
    mob.ExecuteAction(new PassAction());

    gs.RemovePerformerFromGame(mob);
    // We don't actually want to remove them from the game, just from the
    // current performer list so add them back in. A bit of a kludge :/
    gs.ObjDb.Add(mob);
    gs.ResolveActorMove(mob, mob.Loc, dest);
    
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

class UsePower(Power power) : BehaviourNode
{
  protected Power Power { get; set; } = power;

  protected static bool ClearShot(GameState gs, Loc origin, Loc target)
  {
    List<Loc> trajectory = Util.Trajectory(origin, target);
    foreach (var loc in trajectory)
    {
      var tile = gs.TileAt(loc);
      if (!(tile.Passable() || tile.PassableByFlight()))
        return false;
    }

    return true;
  }

  protected virtual bool Available(Mob mob, GameState gs)
  {
    if (mob.LastPowerUse.TryGetValue(Power.Name, out ulong lastUse))
    {
      if (gs.Turn <= lastUse + Power.Cooldown)
        return false;
    }

    if (Power.Type == PowerType.Attack)
    {
      Loc targetLoc = mob.PickTargetLoc(gs);    
      if (targetLoc == Loc.Nowhere)
        return false;

      int d = Util.Distance(mob.Loc, targetLoc);

      if (!ClearShot(gs, mob.Loc, targetLoc))
        return false;

      if (Power.MinRange == 0 && Power.MaxRange == 0)
        return true;
      if (d < Power.MinRange || d > Power.MaxRange)    
        return false;
    }
    
    return true;
  }

  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    if (Available(mob, gs))
    {
      mob.LastPowerUse[Power.Name] = gs.Turn;

      bool result = mob.ExecuteAction(Power.Action(mob, gs, mob.PickTargetLoc(gs)));

      return result ? PlanStatus.Success : PlanStatus.Failure;
    }

    return PlanStatus.Failure;
  }
}

class CrushPower(Power power) : UsePower(power)
{
  static ulong Victim(Mob mob, GameState gs)
  {
    foreach (Loc adj in Util.Adj8Locs(mob.Loc))
    {
      if (gs.ObjDb.Occupant(adj) is not Actor victim)
        continue;

      foreach (Trait t in victim.Traits)
      {
        if (t is GrappledTrait grappled && grappled.GrapplerID == mob.ID)
          return victim.ID;
      }
    }

    return 0;
  }

  protected override bool Available(Mob mob, GameState gs)
  {
    return Victim(mob, gs) != 0;
  }

  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    ulong victimId = Victim(mob, gs);
    if (victimId != 0 )
    {      
      bool result = mob.ExecuteAction(new CrushAction(gs, mob, victimId, Power.DmgDie, Power.NumOfDice));
      return result ? PlanStatus.Success : PlanStatus.Failure;
    }

    return PlanStatus.Failure;
  }
}

class GulpPower(Power power) : UsePower(power)
{
  protected override bool Available(Mob mob, GameState gs)
  {
    if (!base.Available(mob, gs))
      return false;

    if (mob.HasTrait<FullBellyTrait>())
      return false;

    return true;
  }
}

class HealAlliesPower(Power power) : UsePower(power)
{
  protected override bool Available(Mob mob, GameState gs)
  {
    if (mob.LastPowerUse.TryGetValue(Power.Name, out ulong lastUse))
    {
      if (gs.Turn <= lastUse + Power.Cooldown)
        return false;
    }

    if (mob.Traits.OfType<AlliesTrait>().FirstOrDefault() is AlliesTrait allies)
    {
      Loc loc = mob.Loc;
      var fov = FieldOfView.CalcVisible(6, loc, gs.CurrentMap, gs.ObjDb);
      foreach (ulong id in allies.IDs)
      {
        if (gs.ObjDb.GetObj(id) is Actor ally)
        {
          Stat hp = ally.Stats[Attribute.HP];
          if (fov.ContainsKey(ally.Loc) && hp.Curr < hp.Max)
            return true;
        }
      }
    }

    return false;
  }

  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    if (!Available(mob, gs))
      return PlanStatus.Failure;

    if (mob.Traits.OfType<AlliesTrait>().FirstOrDefault() is not AlliesTrait alliesTrait)
      return PlanStatus.Failure;

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
      
      if (Util.AwareOfActor(mob, gs))
      {
        string castText = $"{MsgFactory.CalcName(mob, gs.Player).Capitalize()} {Grammar.Conjugate(mob, "cast")} a healing spell!";
        gs.UIRef().AlertPlayer(castText);
      }

      bool result = mob.ExecuteAction(new HealAction(gs, candidates[i], 4, 4));
      return result ? PlanStatus.Success : PlanStatus.Failure;      
    }

    return PlanStatus.Failure;
  }
}

class PassTurn : BehaviourNode
{
  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    PassAction action;
    string bark = mob.GetBark(gs);
    if (bark != "")
      action = new PassAction(gs, mob) { Quip = bark };
    else
      action = new PassAction();

    mob.ExecuteAction(action);

    return PlanStatus.Success;
  }
}

class TryToEscape : BehaviourNode
{
  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    if (gs.Rng.Next(10) == 0)
    {
      int att = gs.Rng.NextDouble() < 0.75 ? Mob.AGGRESSIVE : Mob.INDIFFERENT;
      mob.Stats[Attribute.MobAttitude].SetCurr(att);
      return PlanStatus.Success;
    }

    bool smart = false;
    bool airborne = false;
    bool immobile = false;
    foreach (Trait t in mob.Traits)
    {
      if (t is IntelligentTrait)
        smart = true;
      else if (t is FloatingTrait || t is FlyingTrait)
        airborne = true;
      else if (t is ImmobileTrait)
        immobile = true;
    }

    // A smart monster will jump on a teleport trap to escape
    if (smart)
    {
      foreach (Loc adj in Util.Adj8Locs(mob.Loc))
      {
        if (gs.TileAt(adj).Type == TileType.TeleportTrap && !gs.ObjDb.Occupied(adj))
        {
          if (gs.LastPlayerFoV.Contains(adj))
            gs.UIRef().AlertPlayer($"{mob.FullName.Capitalize()} jumps into the teleport trap!");
          bool result = mob.ExecuteAction(new MoveAction(gs, mob, adj));
          return result ? PlanStatus.Running : PlanStatus.Failure;          
        }
      }
    }

    // Do we have a movement power like Blink available?
    foreach (Power power in mob.Powers)
    {
      if (power.Type == PowerType.Movement)
      {
        UsePower usePower = new(power);
        if (usePower.Execute(mob, gs) == PlanStatus.Success)        
          return PlanStatus.Running;        
      }
    }

    if (!immobile)
    {
      DijkstraMap? map;
      if (airborne)
        map = gs.GetDMap("flying");
      else if (smart)
        map = gs.GetDMap("doors");
      else
        map = gs.GetDMap();
      if (map is null)
        throw new Exception("Dijkstra maps should never be null");

      List<(int, int)> route = map.EscapeRoute(mob.Loc.Row, mob.Loc.Col, 5);
      if (route.Count > 0)
      {
        Loc loc = mob.Loc with { Row = route[0].Item1, Col = route[0].Item2 };

        Tile tile = gs.TileAt(loc);
        bool result;
        if (tile is Door door && !door.Open)
          result = result = mob.ExecuteAction(new OpenDoorAction(gs, mob, loc));
        else
          result = mob.ExecuteAction(new MoveAction(gs, mob, loc));
        return result ? PlanStatus.Running : PlanStatus.Failure;
      }
    }
    
    // Failing that, the monster will try to use one of its powers.
    List<BehaviourNode> nodes = [];
    foreach (Power power in mob.Powers)
    {
      nodes.Add(new UsePower(power));
    }
    Selector selector = new(nodes);
    PlanStatus status = selector.Execute(mob, gs);
    if (status == PlanStatus.Success)
      return PlanStatus.Running;

    return mob.ExecuteAction(new PassAction()) ? PlanStatus.Running : PlanStatus.Failure;
  }
}

class WanderInTavern : BehaviourNode
{
  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    WanderInArea wander = new(gs.Town.Tavern);
    return wander.Execute(mob, gs);
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

class CheckTime(int start, int end) : BehaviourNode
{
  int Start { get; set; } = start;
  int End { get; set; } = end;

  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    var (hour, _) = gs.CurrTime();
    if (hour >= Start && hour <= End)
      return PlanStatus.Success;

    return PlanStatus.Failure;
  }
}

class DiceRoll(int odds) : BehaviourNode
{
  int Odds { get; set; } = odds;

  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    if (gs.Rng.Next(Odds) == 0)
      return PlanStatus.Success;
    return PlanStatus.Failure;
  }
}

class CheckDialogueState(int state) : BehaviourNode
{
  int DialogueState { get; set; } = state;

  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    if (mob.Stats.TryGetValue(Attribute.DialogueState, out Stat? att))
      return att.Curr == DialogueState ? PlanStatus.Success : PlanStatus.Failure;

    return PlanStatus.Failure;
  }
}

class HasTrait<T> : BehaviourNode where T : Trait
{
  public override PlanStatus Execute(Mob mob, GameState gs)
    => mob.HasTrait<T>() ? PlanStatus.Success : PlanStatus.Failure;  
}

class SetDialogueState(int state) : BehaviourNode
{
  int DialogueState { get; set; } = state;

  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    mob.Stats[Attribute.DialogueState] = new Stat(DialogueState);

    return PlanStatus.Success;
  }
}

class CheckMonsterAttitude(int status) : BehaviourNode
{
  int Status { get; set; } = status;

  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    if (mob.Stats.TryGetValue(Attribute.MobAttitude, out Stat? att))
      return att.Curr == Status ? PlanStatus.Success : PlanStatus.Failure;

    return PlanStatus.Failure;
  }
}

class ValidTarget(Actor actor) : BehaviourNode
{
  Actor Actor { get; set; } = actor;

  public override PlanStatus Execute(Mob mob, GameState gs) => 
    Actor is NoOne ? PlanStatus.Failure : PlanStatus.Success;  
}

class IsImmobilized : BehaviourNode
{
  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    foreach (Trait t in mob.Traits)
    {
      if (t is ParalyzedTrait)
        return PlanStatus.Success;
      else if (t is SleepingTrait)
        return PlanStatus.Success;
    }

    return PlanStatus.Failure;
  }  
}

class PickRandom(List<BehaviourNode> nodes) : BehaviourNode
{
  List<BehaviourNode> Nodes { get; set; } = nodes;

  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    BehaviourNode choice = Nodes[gs.Rng.Next(Nodes.Count)];
    return choice.Execute(mob, gs);
  }
}

class FindWayToArea(HashSet<Loc> area) : BehaviourNode
{
  List<Loc> Area { get; set; } = [..area];
  Stack<Loc>? Path { get; set; } = null;
  Loc PrevLoc { get; set; }
  string PrevAction { get; set; } = "";

  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    if (Path is null)
    {
      if (Area.Contains(mob.Loc))
        return PlanStatus.Success;

      Path = BuildPath(mob, gs);
    }
    
    if (Path.Count > 0)
    {
      Loc next = Path.Peek();
      Tile nextTile = gs.TileAt(next);
      Tile prevTile = gs.TileAt(PrevLoc);

      // If the mob moved abnormally, they probably need to recalculate their path
      // Note: there's probably a degenerate case where a mob moving to a specific
      // loc falls through a pit or something and now their goal is on another 
      // level. The game doesn't really handle that yet, but hopefully it will 
      // result in them recalculating their Plan
      if (Util.Distance(mob.Loc, next) > 1)
      {
        Path = null;
        return PlanStatus.Failure;
      }

      Action action;
      if (gs.ObjDb.Occupied(next))
      {
        PrevAction = "pass";
        mob.ExecuteAction(new PassAction(gs, mob) { Quip = "Excuse me" });
        Path = null;
        return PlanStatus.Failure;
      }
      else if (nextTile is Door door && !door.Open)
      {
        PrevAction = "opendoor";
        action = new OpenDoorAction(gs, mob, next);
      }
      else if (prevTile is Door prevDoor && prevDoor.Open && PrevAction != "closedoor")
      {
        PrevAction = "closedoor";
        action = new CloseDoorAction(gs, mob, PrevLoc);
      }
      else
      {
        PrevAction = "move";
        PrevLoc = mob.Loc;
        action = new MoveAction(gs, mob, next);
        Path.Pop();
      }

      if (mob.ExecuteAction(action))
        return PlanStatus.Running;      
    }
    else if (Path.Count == 0)
    {
      Path = null;
      return PlanStatus.Success;
    }

    Path = null;
    return PlanStatus.Failure;
  }

  Stack<Loc> BuildPath(Mob mob, GameState gs)
  {
    Loc goal = Area[gs.Rng.Next(Area.Count)];

    var path = AStar.FindPath(gs.ObjDb, gs.CurrentMap, mob.Loc, goal, TravelCosts(mob), true);
    return path;
  }

  static Dictionary<TileType, int> TravelCosts(Mob mob)
  {
    Dictionary<TileType, int> costs = [];
    costs.Add(TileType.DungeonFloor, 1);
    costs.Add(TileType.DisturbedGrave, 2);
    costs.Add(TileType.Gravestone, 2);
    costs.Add(TileType.Bridge, 1);
    costs.Add(TileType.MagicMouth, 1);
    costs.Add(TileType.HiddenMagicMouth, 1);
    costs.Add(TileType.Dirt, 1);
    costs.Add(TileType.GreenTree, 2);
    costs.Add(TileType.OpenDoor, 1);
    costs.Add(TileType.BrokenDoor, 1);
    costs.Add(TileType.OpenPortcullis, 1);
    costs.Add(TileType.CreepyAltar, 1);
    costs.Add(TileType.Pool, 1);
    costs.Add(TileType.FrozenDeepWater, 2);
    costs.Add(TileType.FrozenWater, 2);
    costs.Add(TileType.Well, 2);
    costs.Add(TileType.Upstairs, 1);
    costs.Add(TileType.Downstairs, 1);
    costs.Add(TileType.Grass, 1);
    costs.Add(TileType.YellowTree, 2);
    costs.Add(TileType.OrangeTree, 2);
    costs.Add(TileType.RedTree, 2);
    costs.Add(TileType.Conifer, 2);
    costs.Add(TileType.WoodFloor, 1);
    costs.Add(TileType.StoneFloor, 1);
    costs.Add(TileType.WoodBridge, 1);
    costs.Add(TileType.Water, 3);
    costs.Add(TileType.Sand, 1);

    foreach (Trait t in mob.Traits)
    {
      if (t is IntelligentTrait || t is VillagerTrait)
        costs.Add(TileType.ClosedDoor, 2);
      if (t is FloatingTrait || t is FlyingTrait)
        costs.Add(TileType.DeepWater, 1);
    }

    return costs;
  }

}

class RandomMove : BehaviourNode
{
  static bool BadSquare(GameState gs, Loc loc)
  {
    foreach (Item item in gs.ObjDb.ItemsAt(loc))
    {
      foreach (Trait t in  item.Traits)
      {
        if (t is BlockTrait)
          return true;
        if (t is OnFireTrait)
          return true;
      }
    }

    return false;
  }

  static PlanStatus MoveOrDoor(Mob mob, GameState gs)
  {
    List<(Loc, bool)> opts = [];

    foreach (Loc adj in Util.Adj8Locs(mob.Loc))
    {      
      if (BadSquare(gs, adj))
        continue;

      Tile tile = gs.TileAt(adj);
      if (tile.Passable() &&! gs.ObjDb.Occupied(adj))
      {
        opts.Add((adj, false));
      }
      else if (tile is Door door && !door.Open)
      {
        opts.Add((adj, true));
      }
    }

    Action action;
    if (opts.Count > 0)
    {
      var (loc, door) = opts[gs.Rng.Next(opts.Count)];
      action = door ? new OpenDoorAction(gs, mob, loc) : new MoveAction(gs, mob, loc);
    }
    else
    {
      action = new PassAction(gs, mob);
    }

    string bark = mob.GetBark(gs);
    if (bark != "")
      action.Quip = bark;

    bool result = mob.ExecuteAction(action);
    return result ? PlanStatus.Success : PlanStatus.Failure;
  }

  static PlanStatus Move(Mob mob, GameState gs, bool flying)
  {
    List<Loc> opts = [];
    foreach (Loc adj in Util.Adj8Locs(mob.Loc))
    {
      if (BadSquare(gs, adj))
        continue;
        
      Tile tile = gs.TileAt(adj);
      if (gs.ObjDb.Occupied(adj))
        continue;
      if (tile.Passable())
        opts.Add(adj);
      else if (flying && tile.PassableByFlight())
        opts.Add(adj);
    }

    Action action;
    if (opts.Count > 0)
    {
      Loc loc = opts[gs.Rng.Next(opts.Count)];
      action = new MoveAction(gs, mob, loc);
    }
    else
    {
      action = new PassAction(gs, mob);
    }

    string bark = mob.GetBark(gs);
    if (bark != "")
      action.Quip = bark;

    bool result = mob.ExecuteAction(action);
    return result ? PlanStatus.Success : PlanStatus.Failure;
  }

  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    if (mob.HasTrait<ImmobileTrait>())
    {
      mob.ExecuteAction(new PassAction());
      return PlanStatus.Success;
    }
      
    foreach (Trait t in mob.Traits)
    {
      if (t is IntelligentTrait)
        return MoveOrDoor(mob, gs);
      if (t is FloatingTrait || t is FlyingTrait)
        return Move(mob, gs, true);
    }

    return Move(mob, gs, false);
  }
}

class JumpToTavern() : BehaviourNode
{  
  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    Map surface = gs.Campaign.Dungeons[0].LevelMaps[0];    
    List<Loc> opts = [];
    foreach (Loc loc in gs.Town.Tavern)
    {
      TileType tile = surface.TileAt(loc.Row, loc.Col).Type;
      if ((tile == TileType.WoodFloor || tile == TileType.StoneFloor) && !gs.ObjDb.Occupied(loc))
        opts.Add(loc);
    }

    if (opts.Count == 0)
      return PlanStatus.Failure;

    Loc dest = opts[gs.Rng.Next(opts.Count)];
    gs.ResolveActorMove(mob, mob.Loc, dest);
    mob.ExecuteAction(new PassAction());
    gs.RefreshPerformers();

    return PlanStatus.Success;
  }
}

// This will probably eventually be a set of subclasses for finding various
// goals.
class FindUpStairs : BehaviourNode
{
  Loc Goal { get; set; }
  Stack<Loc>? Path { get; set; } = null;

  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    Path ??= PathToGoal(mob, gs);

    if (mob.Loc == Goal)
      return PlanStatus.Success;

    if (Path.Count > 0)
    {
      Loc next = Path.Pop();

      // If the mob moved abnormally, they probably need to recalculate their path
      // Note: there's probably a degenerate case where a mob moving to a specific
      // loc falls through a pit or something and now their goal is on another 
      // level. The game doesn't really handle that yet, but hopefully it will 
      // result in them recalculating their Plan
      if (Util.Distance(mob.Loc, next) > 1)
      {
        Path = null;
        return PlanStatus.Failure;
      }

      Tile tile = gs.TileAt(next);
      if (tile is Door door && !door.Open)
      {
        bool result = false;
        if (mob.HasTrait<IntelligentTrait>())
        {
          result = mob.ExecuteAction(new OpenDoorAction(gs, mob, next));
          Path.Push(next);          
        }
        
        if (result)
          return PlanStatus.Running;
        else
        {
          Path = null;
          return PlanStatus.Failure;
        }
      }
      else
      {
        bool result = mob.ExecuteAction(new MoveAction(gs, mob, next));

        if (result)
          return PlanStatus.Running;
        else
        {
          Path = null;
          return PlanStatus.Failure;
        }
      }      
    }

    Path = null;
    return PlanStatus.Failure;
  }

  Stack<Loc> PathToGoal(Mob mob, GameState gs)
  {
    List<(int, int)> stairs = gs.CurrentMap.SqsOfType(TileType.Upstairs);

    if (stairs.Count == 0)
      return [];

    Loc stairsLoc = mob.Loc with {  Row = stairs[0].Item1, Col = stairs[0].Item2 };
    Goal = stairsLoc;
    return AStar.FindPath(gs.ObjDb, gs.CurrentMap, mob.Loc, Goal, TravelCosts(mob), true);
  }

  static Dictionary<TileType, int> TravelCosts(Mob mob)
  {
    Dictionary<TileType, int> costs = [];
    costs.Add(TileType.DungeonFloor, 1);
    costs.Add(TileType.DisturbedGrave, 1);
    costs.Add(TileType.Gravestone, 1);
    costs.Add(TileType.Bridge, 1);
    costs.Add(TileType.WoodBridge, 1);
    costs.Add(TileType.MagicMouth, 1);
    costs.Add(TileType.HiddenMagicMouth, 1);
    costs.Add(TileType.Dirt, 1);
    costs.Add(TileType.GreenTree, 1);
    costs.Add(TileType.OpenDoor, 1);
    costs.Add(TileType.BrokenDoor, 1);
    costs.Add(TileType.OpenPortcullis, 1);
    costs.Add(TileType.CreepyAltar, 1);
    costs.Add(TileType.Pool, 1);
    costs.Add(TileType.FrozenDeepWater, 2);
    costs.Add(TileType.FrozenWater, 2);
    costs.Add(TileType.Well, 1);
    costs.Add(TileType.Upstairs, 1);
    costs.Add(TileType.Downstairs, 1);

    foreach (Trait t in mob.Traits)
    {
      if (t is IntelligentTrait)
        costs.Add(TileType.ClosedDoor, 2);
      if (t is FloatingTrait || t is FlyingTrait)
        costs.Add(TileType.DeepWater, 1);
    }

    return costs;
  }
}

class ChaseTarget : BehaviourNode
{  
  static PlanStatus ChasePlayerDoors(Mob mob, GameState gs)
  {
    if (mob.HasTrait<ImmobileTrait>())
      return PlanStatus.Failure;

    DijkstraMap map = gs.GetDMap("doors") ?? throw new Exception("Dijkstra map should never be null");
    foreach (var sq in map.Neighbours(mob.Loc.Row, mob.Loc.Col))
    {
      Loc loc = new(mob.Loc.DungeonID, mob.Loc.Level, sq.Item1, sq.Item2);

      Tile tile = gs.TileAt(loc);
      if (tile is Door door && !door.Open)
      {
        bool result = mob.ExecuteAction(new OpenDoorAction(gs, mob, loc));
        return result ? PlanStatus.Success : PlanStatus.Failure;
      }
      else if (!gs.ObjDb.Occupied(loc) && gs.TileAt(loc).Passable())
      {
        bool result = mob.ExecuteAction(new MoveAction(gs, mob, loc));
        return result ? PlanStatus.Success : PlanStatus.Failure;
      }
    }

    return PlanStatus.Failure;

  }

  static PlanStatus ChasePlayerFlying(Mob mob, GameState gs)
  {
    DijkstraMap map = gs.GetDMap("flying") ?? throw new Exception("Dijkstra map should never be null");
    foreach (var sq in map.Neighbours(mob.Loc.Row, mob.Loc.Col))
    {
      Loc loc = new(mob.Loc.DungeonID, mob.Loc.Level, sq.Item1, sq.Item2);

      if (!gs.ObjDb.Occupied(loc) && gs.TileAt(loc).PassableByFlight())
      {
        bool result = mob.ExecuteAction(new MoveAction(gs, mob, loc));
        return result ? PlanStatus.Success : PlanStatus.Failure;
      }
    }

    return PlanStatus.Failure;
  }

  static PlanStatus ChasePlayer(Mob mob, GameState gs)
  {
    DijkstraMap map = gs.GetDMap() ?? throw new Exception("Dijkstra map should never be null");

    foreach (var sq in map.Neighbours(mob.Loc.Row, mob.Loc.Col))
    {
      Loc loc = new(mob.Loc.DungeonID, mob.Loc.Level, sq.Item1, sq.Item2);

      // We still check if the tile is passable because, say, a door might be
      // closed after the current dijkstra map is calculated and before it is
      // refreshed      
      if (!gs.ObjDb.Occupied(loc) && gs.TileAt(loc).Passable())
      {
        bool result = mob.ExecuteAction(new MoveAction(gs, mob, loc));
        return result ? PlanStatus.Success : PlanStatus.Failure;
      }
    }

    return PlanStatus.Failure;
  }

  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    Actor target = mob.PickTarget(gs);

    ValidTarget valid = new(target);
    if (valid.Execute(mob, gs) == PlanStatus.Failure)
      return PlanStatus.Failure;

    if (target is Player)
    {
      // if we are chasing the player (the most likely scenario, we can use
      // the dijkstra maps available from  GameState
      foreach (Trait t in mob.Traits)
      {
        if (t is IntelligentTrait)
          return ChasePlayerDoors(mob, gs);
        if (t is FloatingTrait)
          return ChasePlayerFlying(mob, gs);
        if (t is FlyingTrait)
          return ChasePlayerFlying(mob, gs);
      }
      
      return ChasePlayer(mob, gs);
    }
    else
    {
      // How to handle chasing someone other than the Player? I don't want to 
      // calc A* on every turn...although maybe it won't be a problem??
    }

    return PlanStatus.Failure;
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
      if (gs.ObjDb.Occupied(next))
      {
        mob.ExecuteAction(new PassAction(gs, mob) { Quip = "Excuse me" });
        return PlanStatus.Failure;
      }
      else if (nextTile is Door door && !door.Open)
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

class Planner
{
  static BehaviourNode GoToArea(Actor actor, GameState gs, Map map, HashSet<Loc> area)
  {
    FindPathToArea pathBuilder = new(area, gs);
    return new WalkPath(pathBuilder.BuildPath(actor.Loc));   
  }

  static Sequence GoToBuilding(Actor actor, GameState gs, Map map, HashSet<Loc> area)
  {
    HashSet<Loc> floors = OnlyFloorsInArea(map, area);
    FindPathToArea pathBuilder = new(floors, gs);
    BehaviourNode movePlan = new WalkPath(pathBuilder.BuildPath(actor.Loc));

    BehaviourNode nightTest = new IsNight();
    return new Sequence(
      [movePlan, new RepeatWhile(nightTest, new WanderInArea(floors))]
    );
  }

  static Sequence VisitTavern(Actor actor, GameState gs)
  {
    HashSet<Loc> tavernFloors = OnlyFloorsInArea(gs.Wilderness, gs.Town.Tavern);
    FindPathToArea pathBuilder = new(tavernFloors, gs);      
    BehaviourNode movePlan = new WalkPath(pathBuilder.BuildPath(actor.Loc));

    BehaviourNode eveningTest = new IsEvening();
    return new Sequence(
      [movePlan, new RepeatWhile(eveningTest, new WanderInArea(tavernFloors))]
    );
  }

  static HashSet<Loc> OnlyFloorsInArea(Map map, HashSet<Loc> area)
  {
    static bool IsFloor(Map map, Loc loc)
    {
      TileType tile = map.TileAt(loc.Row, loc.Col).Type;
      return tile == TileType.WoodFloor || tile == TileType.StoneFloor;
    }

    return area.Where(l => IsFloor(map, l)).ToHashSet();
  }

  static Sequence CreateSmithPlan(Actor actor, GameState gs)
  {
    List<BehaviourNode> nodes = [];
    HashSet<Loc> smithy = OnlyFloorsInArea(gs.Wilderness, gs.Town.Smithy);

    var (hour, _) = gs.CurrTime();
    if (hour >= 7 && hour < 19)
    {
      if (!gs.Town.Smithy.Contains(actor.Loc))
      {
        FindPathToArea pathBuilder = new(smithy, gs);
        nodes.Add(new WalkPath(pathBuilder.BuildPath(actor.Loc)));
      }
      
      nodes.Add(new RepeatWhile(new IsDaytime(), new WanderInArea(smithy)));
      return new Sequence(nodes);
    }
    else if (hour >= 19 && hour < 22)
    {
      return VisitTavern(actor, gs);
    }
    else
    {
      return GoToBuilding(actor, gs, gs.Wilderness, gs.Town.Smithy);
    }
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
      return VisitTavern(actor, gs);
    }
    else
    {
      int homeId = actor.Stats[Attribute.HomeID].Curr;
      return GoToBuilding(actor, gs, gs.Wilderness, gs.Town.Homes[homeId]);
    }    
  }

  static BehaviourNode CreateMonsterPlan(Mob mob)
  {
    bool immobile = mob.HasTrait<ImmobileTrait>();

    List<BehaviourNode> actions = [];
    List<BehaviourNode> passive = [];
    foreach (Power p in mob.Powers)
    {
      // Ugh a few of the powers have slightly more complicated ways of
      // calculating if they are available to use so I am doing them as 
      // subclasses of UsePower. If I get too many of them and this gets gross,
      // I'll have to come up with something cleaner. An actual factory or such?
      BehaviourNode up = p.Name switch
      {
        "Gulp" => new GulpPower(p),
        "Crush" => new CrushPower(p),
        "HealAllies" => new HealAlliesPower(p),
        _ => new UsePower(p)
      };

      actions.Add(up);
      if (p.Type == PowerType.Passive)
        passive.Add(up);
    }

    // Not yet handling confused monsters, etc

    List<BehaviourNode> plan = [];

    // A paralized monster will just pass its turn
    plan.Add(new Sequence([new IsImmobilized(), new PassTurn()]));

    // As will an inactive one
    plan.Add(new Sequence([new CheckMonsterAttitude(Mob.INACTIVE), new PassTurn()]));

    // An indifferent monster might use Passive abilities and/or wander randomly
    // (if not immobile)
    List<BehaviourNode> indifferentNodes = [new CheckMonsterAttitude(Mob.INDIFFERENT)];
    if (passive.Count > 0)
      indifferentNodes.Add(new Selector(passive));
    if (immobile)
      indifferentNodes.Add(new PassTurn());
    else
      indifferentNodes.Add(new PickRandom([new PassTurn(), new RandomMove()]));    
    plan.Add(new Sequence(indifferentNodes));

    // An afraid monster tries to escape
    plan.Add(new Sequence([new CheckMonsterAttitude(Mob.AFRAID), new TryToEscape()]));

    if (!mob.HasTrait<PassiveTrait>())
    {
      // Finally, try to attack the player or move toward them.
      if (!mob.HasTrait<ImmobileTrait>())
        actions.Add(new ChaseTarget());
      plan.Add(new Sequence([new CheckMonsterAttitude(Mob.AGGRESSIVE), new Selector(actions)]));

      plan.Add(new PassTurn());
    }
    else
    {
      plan.Add(new RandomMove());
    }
    
    return new Selector(plan);
  }

  static BehaviourNode CreatePrisonerPlan(Mob mob)
  {
    List<BehaviourNode> plan = [];

    // Prisoner trapped
    Sequence trapped = new([
      new CheckDialogueState(PrisonerBehaviour.DIALOGUE_CAPTIVE), 
      new Selector([new RandomMove(), new PassTurn()])
    ]);
    plan.Add(trapped);

    // Prisoner trapped but hasn't yet rewarded the player
    Sequence free = new([
      new CheckDialogueState(PrisonerBehaviour.DIALOGUE_FREE),
      new RandomMove()
    ]);
    plan.Add(free);

    // Prisoner has given the player their boon
    Sequence afterBoon = new([
      new CheckDialogueState(PrisonerBehaviour.DIALOGUE_FREE_BOON),
      new FindUpStairs(),
      new PassTurn(),
      new SetDialogueState(PrisonerBehaviour.DIALOGUE_ESCAPING)
    ]);
    plan.Add(afterBoon);

    Sequence escape = new([
      new CheckDialogueState(PrisonerBehaviour.DIALOGUE_ESCAPING),
      new JumpToTavern(),
      new SetDialogueState(PrisonerBehaviour.DIALOGUE_AT_INN)
    ]);
    plan.Add(escape);

    Sequence atInn = new ([
      new CheckDialogueState(PrisonerBehaviour.DIALOGUE_AT_INN),
      new WanderInTavern()
    ]);
    plan.Add(atInn);
    
    return new Selector(plan);
  }

  static BehaviourNode CreateSimpleVillagerPlan(Mob mob)
  {
    HashSet<Loc> home = [];

    return new WanderInArea(home);
  }

  static BehaviourNode WanderInHome(HashSet<Loc> home, GameState gs)
  {
    HashSet<Loc> sqs = [];

    foreach (Loc loc in home)
    {
      TileType tile = gs.TileAt(loc).Type;
      if (tile == TileType.WoodFloor || tile == TileType.StoneFloor)
        sqs.Add(loc);
    }

    return new WanderInArea(sqs);
  }
  
  static BehaviourNode BasicVillager(Mob mob, GameState gs)
  {
    int homeId = mob.Stats[Attribute.HomeID].Curr;

    return WanderInHome(gs.Town.Homes[homeId], gs);
  }

  static BehaviourNode Pup(GameState gs)
  {
    HashSet<Loc> townSqs = [];

    for (int r = gs.Town.Row; r < gs.Town.Row + gs.Town.Height; r++)
    {
      for (int c = gs.Town.Col; c < gs.Town.Col + gs.Town.Width; c++)
      {
        townSqs.Add(new Loc(0, 0, r, c));
      }
    }

    return new WanderInArea(townSqs);
  }

  static BehaviourNode WitchPlan(Mob witch, GameState gs)
  {
    // If the witch is invisible, she'll just stay still. (Just to
    // make it easier for the character to talk to her)
    Sequence isInvisible = new([
      new HasTrait<InvisibleTrait>(),
      new PassTurn()
    ]);

    RepeatWhile daytime = new(new IsDaytime(), new WanderInArea(gs.Town.WitchesYard));

    HashSet<Loc> indoors = OnlyFloorsInArea(gs.Wilderness, gs.Town.WitchesCottage);
    Sequence evening = new([new FindWayToArea(indoors), new WanderInArea(indoors)]);

    return new Selector([isInvisible, daytime, evening]);
  }

  static BehaviourNode AlchemistPlan(Mob alchemist, GameState gs)
  {
    Sequence gardening = new([
      new CheckTime(8, 12),
      new FindWayToArea(gs.Town.WitchesGarden),
      new PickRandom([new PassTurn(), new PassTurn(), new WanderInArea(gs.Town.WitchesGarden)])      
    ]);

    RepeatWhile daytime = new(new IsDaytime(), new WanderInArea(gs.Town.WitchesYard));

    HashSet<Loc> indoors = OnlyFloorsInArea(gs.Wilderness, gs.Town.WitchesCottage);
    Sequence evening = new([new FindWayToArea(indoors), new WanderInArea(indoors)]);
    
    return new Selector([gardening, daytime, evening]);
  }

  // Maybe the Actor/Mob class returns its own plan, obviating the need for 
  // this function?
  public static BehaviourNode GetPlan(string plan, Mob mob, GameState gs) => plan switch
  {
    "MayorPlan" => CreateMayorPlan(mob, gs),
    "SmithPlan" => CreateSmithPlan(mob, gs),
    "MonsterPlan" => CreateMonsterPlan(mob),
    "PrisonerPlan" => CreatePrisonerPlan(mob),
    "PriestPlan" => WanderInHome(gs.Town.Shrine, gs),
    "GrocerPlan" => WanderInHome(gs.Town.Market, gs),
    "BasicVillagerPlan" => BasicVillager(mob, gs),
    "WitchPlan" => WitchPlan(mob, gs),
    "AlchemistPlan" => AlchemistPlan(mob, gs),
    "BarHoundPlan" => WanderInHome(gs.Town.Tavern, gs),
    "PupPlan" => Pup(gs),
    "SimpleRandomPlan" => new Selector([new RandomMove(), new PassTurn()]),
    "MoonClericPlan" => new Selector([
      new Sequence([new CheckDialogueState(1), new DiceRoll(250), new MoveLevel()]),
      new RandomMove(), new PassTurn()]),
    "BasicIllusionPlan" => new Selector([new ChaseTarget(), new RandomMove()]),
    _ => throw new Exception($"Unknown Behaviour Tree plan: {plan}")
  };

  // This will expand into the function to calculate the target for mob 
  // attacks. I'm not sure if this belongs here, in the Mob/Actor class, or
  // Behaviour class, but for the moment 'Planner' is where I am trying to
  // place the "Decide what to do" code
  public static ulong SelectTarget(Mob mob, GameState gs) => gs.Player.ID;
}
 
interface IBehaviour
{
  (Action, Inputer?) Chat(Mob actor, GameState gameState);
  string GetBark(Mob actor, GameState gs);
}

class NullBehaviour : IBehaviour
{
  private static readonly NullBehaviour instance = new();
  public static NullBehaviour Instance() => instance;

  public (Action, Inputer?) Chat(Mob actor, GameState gameState) => throw new NotImplementedException();
  public string GetBark(Mob actor, GameState gs) => "";
}

// I think I'll likely eventually merge this into IBehaviour
interface IDialoguer
{
  void InitDialogue(Mob actor, GameState gs);
  (string, string, List<(string, char)>) CurrentText(Mob mob, GameState gs);
  void SelectOption(Mob actor, char opt, GameState gs);
}

class MonsterBehaviour : IBehaviour
{
  readonly Dictionary<string, ulong> _lastUse = [];

  public string GetBark(Mob actor, GameState gs) => "";

  public (Action, Inputer?) Chat(Mob actor, GameState gameState) => (new NullAction(), null);
}

  
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
    
// Disguised monsters behave differently while they are disguised, but then act like a normal monster
// so it just seemed simple (or easy...) to extend MonsterBevaviour
class DisguisedMonsterBehaviour : MonsterBehaviour
{
  //public override Action CalcAction(Mob actor, GameState gs)
  //{
  //  bool disguised = actor.Stats[Attribute.InDisguise].Curr == 1;
  //  if (disguised && Util.Distance(actor.Loc, gs.Player.Loc) > 1)
  //    return new PassAction();

  //  if (disguised)
  //  {
  //    var disguise = actor.Traits.OfType<DisguiseTrait>().First();
  //    string txt = $"The {disguise.DisguiseForm} was really {actor.Name.IndefArticle()}!";
  //    gs.UIRef().AlertPlayer(txt);
  //    actor.Glyph = disguise.TrueForm;
  //    actor.Stats[Attribute.InDisguise].SetMax(0);
  //  }

  //  return base.CalcAction(actor, gs);
  //}
}

class VillagePupBehaviour : IBehaviour
{  
  public string GetBark(Mob actor, GameState gs) => "";

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

class MoonDaughtersClericBehaviour : NPCBehaviour
{
  DateTime _lastBark = new(1900, 1, 1);

  public override void InitDialogue(Mob mob, GameState gs)
  {
    int dialogueState = mob.Stats.TryGetValue(Attribute.DialogueState, out var ds) ? ds.Curr : 0;
    int lastGiftTime = mob.Stats.TryGetValue(Attribute.LastGiftTime, out var lgt) ? lgt.Curr : 0;
    int turn = (int)gs.Turn % int.MaxValue;
    
    if (dialogueState > 0 && turn - lastGiftTime > 1000)
    {
      mob.Stats[Attribute.DialogueState] = new Stat(0);
    }
  }

  public override string GetBark(Mob actor, GameState gs)
  {    
    if ((DateTime.UtcNow - _lastBark).TotalSeconds > 17)
    {
      _lastBark = DateTime.UtcNow;
      return "Darkness can protect as well as conceal.";
    }

    return "";
  }
}

class GnomeMerchantBehaviour : NPCBehaviour
{
  DateTime _lastBark = new(1900, 1, 1);

  public override void InitDialogue(Mob mob, GameState gs)
  {
    NumberListTrait selections = mob.Traits.OfType<NumberListTrait>()
                                           .Where(t => t.Name == "ShopSelections")
                                           .First();
    selections.Items = [];
    mob.Stats[Attribute.ShopInvoice] = new Stat(0);
  }

  public override string GetBark(Mob actor, GameState gs)
  {    
    if ((DateTime.UtcNow - _lastBark).TotalSeconds > 13)
    {
      _lastBark = DateTime.UtcNow;
      return gs.Rng.Next(4) switch
      {
        0 => "Priced to clear!",
        1 => "I thought this would be easy money!",
        2 => "The customer is always something, something...",
        _ => "Everything must go!"
      };
      
    }

    return "";
  }

  public override bool ConfirmChoices(Actor npc, GameState gs)
  {
    NumberListTrait selections = npc.Traits.OfType<NumberListTrait>()
                                           .Where(t => t.Name == "ShopSelections")
                                           .First();

    if (selections.Items.Count == 0 || npc.Stats[Attribute.ShopInvoice].Curr > gs.Player.Inventory.Zorkmids)
    {
      return false;
    }

    List<Item> inventory = npc.Inventory.Items();
    List<ulong> purchases = [];
    for (int i = 0; i < inventory.Count; i++)
    {
      if (selections.Items.Contains(i))
        purchases.Add(inventory[i].ID);
    }

    foreach (ulong id in purchases)
    {
      Item item = npc.Inventory.RemoveByID(id)!;
      gs.Player.AddToInventory(item, gs);
    }

    gs.Player.Inventory.Zorkmids -= npc.Stats[Attribute.ShopInvoice].Curr;

    gs.UIRef().AlertPlayer($"You hand over your money and {npc.FullName} gives you your goods.");
    
    selections.Items = [];

    return true;
  }
}

class PriestBehaviour : NPCBehaviour
{  
  DateTime _lastBark = new(1900, 1, 1);

  public override string GetBark(Mob actor, GameState gs)
  {    
    if ((DateTime.UtcNow - _lastBark).TotalSeconds > 13)
    {
      _lastBark = DateTime.UtcNow;
      return "Praise be to Huntokar!";
    }

    return "";
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

  public override void RefreshShop(Actor npc, GameState gs) 
  {
    int lastRefresh = npc.Stats[Attribute.InventoryRefresh].Curr;
    int turn = (int)(gs.Turn % int.MaxValue);

    if (Math.Abs(turn - lastRefresh) < 2500)
      return;

    List<int> newMenu = [];
    List<int> options = [ 1, 2, 3, 4, 5 ];
    int lastBlessing = gs.Player.Stats[Attribute.LastBlessing].Curr;
    if (lastBlessing > 0)
    {
      newMenu.Add(lastBlessing);
      options.Remove(lastBlessing);
    }
    while (newMenu.Count < 3)
    {
      int b = options[gs.Rng.Next(options.Count)];
      newMenu.Add(b);
      options.Remove(b);
    }
  
    NumberListTrait blessings = npc.Traits.OfType<NumberListTrait>().Where(t => t.Name == "Blessings").First();
    blessings.Items = newMenu;
    
    npc.Stats[Attribute.InventoryRefresh].SetMax(turn);
  }
}

class WitchBehaviour : NPCBehaviour
{
  DateTime _lastBark = new(1900, 1, 1);
  
  public override string GetBark(Mob mob, GameState gs)
  {
    if ((DateTime.UtcNow - _lastBark).TotalSeconds < 9)
      return "";

    _lastBark = DateTime.UtcNow;

    string grocerName = "";
    if (gs.FactDb.FactCheck("GrocerId") is SimpleFact fact)
    {
      ulong grocerId = ulong.Parse(fact.Value);
      if (gs.ObjDb.GetObj(grocerId) is Actor grocer)
        grocerName = grocer.FullName.Capitalize();
    }
    
    if (mob.HasTrait<InvisibleTrait>())
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

  public override (Action, Inputer?) Chat(Mob actor, GameState gameState)
  {
    var acc = new WitchInputer(actor, gameState);
    var action = new WitchServiceAction(gameState, actor);
    
    return (action, acc);
  }
}

class SmithBehaviour : NPCBehaviour
{
  DateTime _lastBark = new(1900, 1, 1);

  // Eventually just replace/merge GetPark and PickBark
  public override string GetBark(Mob actor, GameState gs) 
  {
    string bark = "";

    if ((DateTime.UtcNow - _lastBark).TotalSeconds > 13)
    {
      bark = PickBark(actor, gs);
      _lastBark = DateTime.UtcNow;
    }

    return bark;
  }

  static string PickBark(Mob smith, GameState gs)
  {
    var (hour, _) = gs.CurrTime();

    if (hour >= 19 && hour < 22) 
    {
      return "Nothing like a good ale after a day at the forge!";
    }
    else if (hour >= 7 && hour < 19) 
    {
      var items = smith.Inventory.UsedSlots()
                                .Select(smith.Inventory.ItemAt)
                                .Select(si => si.Item1).ToList();
      Item? item;
      if (items.Count > 0)
        item = items[gs.Rng.Next(items.Count)];
      else
        item = null;

      int roll = gs.Rng.Next(2);
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
    
    return "";
  }

  static string Blurb(GameState gs)
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

  public override (Action, Inputer) Chat(Mob actor, GameState gs)
  {
    if (gs.Player.HasTrait<ShunnedTrait>())
    {
      return (new NullAction(), new PauseForMoreInputer());
    }

    var acc = new SmithyInputer(actor, Blurb(gs), gs);
    var action = new ShoppingCompletedAction(gs, actor);

    return (action, acc);
  }

  public override void RefreshShop(Actor npc, GameState gs) 
  {
    int lastRefresh = npc.Stats[Attribute.InventoryRefresh].Curr;
    int turn = (int)(gs.Turn % int.MaxValue);

    if (Math.Abs(turn - lastRefresh) < 750)
      return;
    npc.Stats[Attribute.InventoryRefresh].SetMax(turn);
    
    List<Item> currStock = npc.Inventory.Items();

    foreach (Item item in currStock)
    {
      if (gs.Rng.NextDouble() < 0.2)
      {
        npc.Inventory.RemoveByID(item.ID);
        gs.ObjDb.RemoveItemFromGame(Loc.Nowhere, item);
      }
    }

    int newStock = gs.Rng.Next(1, 5);
    for (int j = 0; j < newStock; j++)
    {
      int roll = gs.Rng.Next(12);
      if (roll == 0)
        npc.Inventory.Add(ItemFactory.Get(ItemNames.CHAINMAIL, gs.ObjDb), npc.ID);
      else if (roll == 1)
        npc.Inventory.Add(ItemFactory.Get(ItemNames.SHIELD, gs.ObjDb), npc.ID);
      else if (roll == 3)
        npc.Inventory.Add(ItemFactory.Get(ItemNames.BATTLE_AXE, gs.ObjDb), npc.ID);
      else if (roll == 4)
        npc.Inventory.Add(ItemFactory.Get(ItemNames.PICKAXE, gs.ObjDb), npc.ID);
      else if (roll == 5)
        npc.Inventory.Add(ItemFactory.Get(ItemNames.SILVER_DAGGER, gs.ObjDb), npc.ID);
      else if (roll == 6)
        npc.Inventory.Add(ItemFactory.Get(ItemNames.GUISARME, gs.ObjDb), npc.ID);
      else if (roll == 7)
        npc.Inventory.Add(ItemFactory.Get(ItemNames.RAPIER, gs.ObjDb), npc.ID);
      else if (roll == 8)
        npc.Inventory.Add(ItemFactory.Get(ItemNames.RINGMAIL, gs.ObjDb), npc.ID);
      else if (roll == 9)
        npc.Inventory.Add(ItemFactory.Get(ItemNames.HELMET, gs.ObjDb), npc.ID);
      else if (roll == 10)
        npc.Inventory.Add(ItemFactory.Get(ItemNames.LEATHER_GLOVES, gs.ObjDb), npc.ID);
      else if (roll == 11)
        npc.Inventory.Add(ItemFactory.Get(ItemNames.QUARTERSTAFF, gs.ObjDb), npc.ID);
    }
  }
}

class AlchemistBehaviour : NPCBehaviour
{
  DateTime _lastBark = new(1900, 1, 1);

  public override string GetBark(Mob actor, GameState gs)
  {
    if ((DateTime.UtcNow - _lastBark).TotalSeconds < 9)
      return "";

    _lastBark = DateTime.UtcNow;

    var (hour, _) = gs.CurrTime();

    List<string> barks;
    if (gs.Town.WitchesGarden.Contains(actor.Loc))
    {
      barks = ["Kylie, what do you want for dinner?", "Hey plant buddies, you'all doing great!", "Hello bee friends!", "Hmm you need a little more water.", "♪ Hmm mmm ♪♪"];
    }
    else if (hour < 7 || hour >= 19)
    {
      barks = ["I've been working on a new song!", "How was your day?", "I want to tweak that a recipe a bit."];
    }
    else
    {
      barks = ["I've been working on a new song!", "𝅘𝅥𝅯 Hmm mmm 𝅘𝅥𝅘𝅥", "Kylie, what do you want for dinner?"];
    }
    
    return barks[gs.Rng.Next(barks.Count)];
  }

  public override (Action, Inputer?) Chat(Mob actor, GameState gs)
  {
    string s = "Oh, I dabble in alchemy and potioncraft if you're interested. It pays the bills between gigs.";
    var acc = new ShopMenuInputer(actor, s, gs);
    var action = new ShoppingCompletedAction(gs, actor);

    return (action, acc);
  }

  public override void RefreshShop(Actor npc, GameState gs) 
  {
    int lastRefresh = npc.Stats[Attribute.InventoryRefresh].Curr;
    int turn = (int)(gs.Turn % int.MaxValue);

    if (Math.Abs(turn - lastRefresh) < 750)
      return;
    npc.Stats[Attribute.InventoryRefresh].SetMax(turn);

    List<Item> currStock = npc.Inventory.Items();
    foreach (Item item in currStock)
    {
      if (gs.Rng.NextDouble() < 0.2)
      {
        npc.Inventory.RemoveByID(item.ID);
        gs.ObjDb.RemoveItemFromGame(Loc.Nowhere, item);
      }
    }

    int newStock = gs.Rng.Next(1, 5);
    for (int j = 0; j < newStock; j++)
    {      
      ItemNames itemName = gs.Rng.Next(7) switch
      {
        0 => ItemNames.POTION_HEALING,
        1 => ItemNames.POTION_HEROISM,
        2 => ItemNames.POTION_OF_LEVITATION,
        3 => ItemNames.POTION_MIND_READING,
        4 => ItemNames.ANTIDOTE,
        5 => ItemNames.POTION_OBSCURITY,
        _ => ItemNames.MUSHROOM_STEW
      };
      Item item = ItemFactory.Get(itemName, gs.ObjDb);
      item.Traits.Add(new SideEffectTrait() { Odds = 10, Effect = "Confused#0#13#0" });
      npc.Inventory.Add(item, npc.ID);
    }
  }
}

class GrocerBehaviour : NPCBehaviour
{
  DateTime _lastBark = new(1900, 1, 1);

  public override string GetBark(Mob actor, GameState gs)
  {
    if ((DateTime.UtcNow - _lastBark).TotalSeconds < 10)
      return "";

    _lastBark = DateTime.UtcNow;

    return gs.Rng.Next(3) switch
    {
      0 => "Supplies for the prudent adventurer!",
      1 => "Check out our specials!",
      _ => "Store credit only."
    };
  }
  
  public override (Action, Inputer) Chat(Mob actor, GameState gs)
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

  public override void RefreshShop(Actor npc, GameState gs) 
  {
    int lastRefresh = npc.Stats[Attribute.InventoryRefresh].Curr;
    int turn = (int)(gs.Turn % int.MaxValue);

    if (Math.Abs(turn - lastRefresh) < 750)
      return;
    npc.Stats[Attribute.InventoryRefresh].SetMax(turn);

    List<Item> currStock = npc.Inventory.Items();

    foreach (Item item in currStock)
    {
      if (gs.Rng.NextDouble() < 0.2)
      {
        npc.Inventory.RemoveByID(item.ID);
        gs.ObjDb.RemoveItemFromGame(Loc.Nowhere, item);
      }
    }

    int newStock = gs.Rng.Next(1, 4);
    for (int j = 0; j < newStock; j++)
    {
      int roll = gs.Rng.Next(15);
      if (roll < 3)
        npc.Inventory.Add(ItemFactory.Get(ItemNames.TORCH, gs.ObjDb), npc.ID);
      else if (roll < 5)
        npc.Inventory.Add(ItemFactory.Get(ItemNames.POTION_HEALING, gs.ObjDb), npc.ID);
      else if (roll == 6)
        npc.Inventory.Add(ItemFactory.Get(ItemNames.ANTIDOTE, gs.ObjDb), npc.ID);
      else if (roll == 7)
        npc.Inventory.Add(ItemFactory.Get(ItemNames.SCROLL_DISARM, gs.ObjDb), npc.ID);
      else if (roll == 8)
        npc.Inventory.Add(ItemFactory.Get(ItemNames.SCROLL_BLINK, gs.ObjDb), npc.ID);
      else if (roll == 9)
        npc.Inventory.Add(ItemFactory.Get(ItemNames.SCROLL_KNOCK, gs.ObjDb), npc.ID);
      else if (roll == 10)
        npc.Inventory.Add(ItemFactory.Get(ItemNames.SCROLL_PROTECTION, gs.ObjDb), npc.ID);
      else if (roll == 11)
        npc.Inventory.Add(ItemFactory.Get(ItemNames.POTION_MIND_READING, gs.ObjDb), npc.ID);
      else if (roll == 12)
        npc.Inventory.Add(ItemFactory.Get(ItemNames.POTION_OF_LEVITATION, gs.ObjDb), npc.ID);
      else if (roll == 13)
        npc.Inventory.Add(ItemFactory.Get(ItemNames.SCROLL_TREASURE_DETECTION, gs.ObjDb), npc.ID);
      else if (roll == 14)
        npc.Inventory.Add(ItemFactory.Get(ItemNames.SCROLL_TREASURE_DETECTION, gs.ObjDb), npc.ID);
    }

    // Grocer always keeps a few torches in stock
    bool torchesInStock = npc.Inventory.Items().Any(i => i.Name == "torch");
    if (!torchesInStock)
    {
      for (int j = 0; j < gs.Rng.Next(2, 5); j++)
      {
        npc.Inventory.Add(ItemFactory.Get(ItemNames.TORCH, gs.ObjDb), npc.ID);
      }
    }
  }
}

class NPCBehaviour : IBehaviour, IDialoguer
{
  List<DialogueOption> Options { get; set; } = [];

  public virtual void InitDialogue(Mob actor, GameState gs) {}
  public virtual string GetBark(Mob actor, GameState gs) => "";

  public virtual (Action, Inputer?) Chat(Mob actor, GameState gameState)
  {
    if (gameState.Player.HasTrait<ShunnedTrait>())
    {
      return (new NullAction(), new PauseForMoreInputer());
    }

    Dialoguer acc = new(actor, gameState);
    CloseMenuAction action = new(gameState, 1.0);

    return (action, acc);
  }

  public virtual (string, string, List<(string, char)>) CurrentText(Mob mob, GameState gs)
  {
    string scriptFile = mob.Traits.OfType<DialogueScriptTrait>().First().ScriptFile;
    var dialogue = new DialogueInterpreter();

    var (txt, footer) = dialogue.Run(scriptFile, mob, gs);
    Options = dialogue.Options;
    List<(string, char)> opts = [..Options.Select(o => (o.Text, o.Ch))];
    
    return (txt, footer, opts);
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

  public virtual bool ConfirmChoices(Actor npc, GameState gs) 
  {
    bool hasSelections = npc.Traits.OfType<NumberListTrait>()
                                   .Where(t => t.Name == "ShopSelections")
                                   .Any();
    return !hasSelections;
  }

  public virtual void RefreshShop(Actor npc, GameState gs) { }
}

class MayorBehaviour : NPCBehaviour
{
  DateTime _lastBark = new(1900, 1, 1);
  
  public override string GetBark(Mob actor, GameState gs)
  {
    string bark = "";

    if ((DateTime.UtcNow - _lastBark).TotalSeconds > 10)
    {
      _lastBark = DateTime.UtcNow;
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
}

class WidowerBehaviour: NPCBehaviour
{
  DateTime _lastBark = new(1900, 1, 1);

  public override string GetBark(Mob actor, GameState gs)
  {
    if ((DateTime.UtcNow - _lastBark).TotalSeconds < 15)
      return "";
    _lastBark = DateTime.UtcNow;

    int state;
    if (actor.Stats.TryGetValue(Attribute.DialogueState, out var stateState))
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

    return barks[gs.Rng.Next(barks.Count)];
  }
}

class PrisonerBehaviour : NPCBehaviour
{
  public const int DIALOGUE_CAPTIVE = 0;
  public const int DIALOGUE_FREE = 1;
  public const int DIALOGUE_FREE_BOON = 2;
  public const int DIALOGUE_ESCAPING = 3;
  public const int DIALOGUE_AT_INN = 4;

  DateTime _lastBark = new(1900, 1, 1);

  public override string GetBark(Mob actor, GameState gs)
  {
    if ((DateTime.UtcNow - _lastBark).TotalSeconds <= 10)
      return "";

    _lastBark = DateTime.UtcNow;

    int dialogueState = actor.Stats[Attribute.DialogueState].Curr;
    string capturedBy = ((SimpleFact) gs.FactDb.FactCheck("ImprisonedBy")!).Value;
    return dialogueState switch
    {
      DIALOGUE_FREE => "Thank you!",
      DIALOGUE_FREE_BOON => "Hmm...which way to the exit?",
      DIALOGUE_AT_INN => gs.Rng.Next(3) switch
        {
          0 => "Fresh air at last!",
          1 => "Adventuring is for suckers.",
          _ => "I'm hanging up my sword."
        },        
      _ => gs.Rng.Next(3) switch
            {
              0 => $"I was captured by {capturedBy}!",
              1 => "Help me!",
              _ => "Can you free me?"
            }
    };
  }
}