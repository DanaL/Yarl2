// Delve - A roguelike computer RPG
// Written in 2025 by Dana Larose <ywg.dana@gmail.com>
//
// To the extent possible under law, the author(s) have dedicated all copyright
// and related and neighboring rights to this software to the public domain
// worldwide. This software is distributed without any warranty.
//
// You should have received a copy of the CC0 Public Domain Dedication along 
// with this software. If not, 
// see <http://creativecommons.org/publicdomain/zero/1.0/>.

namespace Yarl2;

enum PlanStatus { Success, Failure, Running }

interface IPathBuilder
{
  Stack<Loc> BuildPath(Loc start);
  bool AtGoal(Loc loc, GameState gs);
}

class FindNearbyItem(Mob mob, GameState gs, string name) : IPathBuilder
{
  Mob Mob { get; set; } = mob;
  Loc Goal { get; set; } = Loc.Nowhere;
  GameState GS { get; set; } = gs;
  string ItemName { get; set; } = name;

  public Stack<Loc> BuildPath(Loc start)
  {
    var fov = FieldOfView.CalcVisible(9, Mob.Loc, GS.MapForLoc(Mob.Loc), GS.ObjDb);
    PriorityQueue<Loc, int> itemLocs = new();

    foreach (Loc loc in fov.Keys)
    {
      foreach (Item item in GS.ObjDb.VisibleItemsAt(loc))
      {
        if (item.Name == ItemName)
        {
          itemLocs.Enqueue(loc, Util.Distance(start, loc));
          break;
        }
      }
    }

    while (itemLocs.Count > 0)
    {
      Loc loc = itemLocs.Dequeue();

      TravelCostFunction costFunc = DijkstraMap.Cost;
      if (Mob.HasTrait<IntelligentTrait>())
        costFunc = DijkstraMap.CostWithDoors;
      else if (Mob.HasTrait<FloatingTrait>() || Mob.HasTrait<FlyingTrait>())
        costFunc = DijkstraMap.CostByFlight;

      Stack<Loc> path = AStar.FindPath(GS.ObjDb, GS.MapForLoc(start), start, loc, costFunc, true);
      if (path.Count > 0)
        return path;
    }

    return [];
  }

  public bool AtGoal(Loc loc, GameState _) => loc == Goal;
}

class FindPathToArea(HashSet<Loc> area, GameState gs) : IPathBuilder
{
  HashSet<Loc> Area { get; set; } = area;
  GameState GS { get; set; } = gs;

  public Stack<Loc> BuildPath(Loc start)
  {
    List<Loc> locs = [.. Area];
    locs.Shuffle(GS.Rng);

    foreach (Loc loc in locs)
    {
      // I'm using this node to build routes for villagers, so I can assume the cost function
      // that includes doors.
      Stack<Loc> path = AStar.FindPath(GS.ObjDb, GS.MapForLoc(start), start, loc, DijkstraMap.CostWithDoors, false);
      if (path.Count > 0)
        return path;
    }

    return [];
  }

  public bool AtGoal(Loc loc, GameState gs) => Area.Contains(loc);
}

abstract class BehaviourNode
{
  public string Label { get; set; } = "";

  public abstract PlanStatus Execute(Mob mob, GameState gs);

  protected static bool ClearShot(GameState gs, Loc origin, Loc target)
  {
    List<Loc> trajectory = Util.Trajectory(origin, target);
    foreach (var loc in trajectory)
    {
      var tile = gs.TileAt(loc);
      if (!(tile.Passable() || tile.PassableByFlight() || tile.IsWater()))
        return false;
    }

    return true;
  }
}

class Not(BehaviourNode node) : BehaviourNode
{
  BehaviourNode Node { get; set; } = node;

  // This turns PlanStatus.Running into Success, but I'm not sure it makes
  // sense to call Not() on something which might return Running.
  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    PlanStatus result = Node.Execute(mob, gs);

    if (result == PlanStatus.Success)
      return PlanStatus.Failure;

    return PlanStatus.Success;
  }
}

class RandoSelector(List<BehaviourNode> nodes) : BehaviourNode
{
  public List<BehaviourNode> Children { get; set; } = nodes;

  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    int curr = 0;

    Children.Shuffle(gs.Rng);
    while (curr < Children.Count)
    {
      BehaviourNode node = Children[curr];
      PlanStatus status = node.Execute(mob, gs);
      if (status == PlanStatus.Running)
      {
        return status;
      }

      if (status == PlanStatus.Success)
      {
        return status;
      }

      ++curr;
    }
    
    return PlanStatus.Failure;
  }
}

class Selector(List<BehaviourNode> nodes) : BehaviourNode
{
  public List<BehaviourNode> Children { get; set; } = nodes;
  int Curr { get; set; } = 0;

  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    while (Curr < Children.Count)
    {
      BehaviourNode node = Children[Curr];
      PlanStatus status = node.Execute(mob, gs);
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
  public List<BehaviourNode> Children { get; set; } = nodes;
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

// Used for mobs like the moon daughter cleric who disappears for a while.
// I'm not using this for the travelling peddlar yet, but perhaps I should
class GoAway : BehaviourNode
{
  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    gs.RemovePerformerFromGame(mob);
    mob.Loc = Loc.Nowhere;
    mob.Energy = 0.0;
    gs.ObjDb.Add(mob);

    return PlanStatus.Success;
  }
}

class MoveLevel : BehaviourNode
{
  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    // They won't move levels if the player can see them
    if (gs.LastPlayerFoV.ContainsKey(mob.Loc))
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
    mob.ExecuteAction(new PassAction(gs));

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

  protected virtual bool Available(Mob mob, GameState gs)
  {
    if (mob.LastPowerUse.TryGetValue(Power.Name, out ulong lastUse))
    {
      if (gs.Turn < lastUse + Power.Cooldown)
        return false;
    }

    if (Power.Type == PowerType.Attack)
    {
      Loc targetLoc = mob.PickTargetLoc(gs, int.MaxValue);
      if (targetLoc == Loc.Nowhere)
        return false;

      int d = Util.Distance(mob.Loc, targetLoc);

      if (d > 1 && !ClearShot(gs, mob.Loc, targetLoc))
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
      mob.ExecuteAction(Power.Action(mob, gs, mob.PickTargetLoc(gs, int.MaxValue)));

      return PlanStatus.Success;
    }

    return PlanStatus.Failure;
  }  
}

// This is for Powers where the mob must be able to see its target as a
// condition of using it. Ie., spells like InduceNudity
class SeeToTargetPower(Power power) : UsePower(power)
{
  protected override bool Available(Mob mob, GameState gs)
  {
    bool available = base.Available(mob, gs);
    if (!available)
      return false;

    return mob.PickTarget(gs) is not NoOne;
  }
}

class UseWhirlpoolPower(Power power) : UsePower(power)
{
  protected override bool Available(Mob mob, GameState gs)
  {
    Tile tile = gs.TileAt(mob.Loc);

    return tile.IsWater();
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
    if (victimId != 0)
    {
      mob.ExecuteAction(new CrushAction(gs, mob, victimId, Power.DmgDie, Power.NumOfDice));
      return PlanStatus.Success;
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

class SporesPower(Power power) : UsePower(power)
{
  protected override bool Available(Mob mob, GameState gs)
  {
    if (mob.LastPowerUse.TryGetValue(Power.Name, out ulong lastUse))
    {
      if (gs.Turn < lastUse + Power.Cooldown)
        return false;
    }

    Map map = gs.MapForActor(mob);
    for (int r = mob.Loc.Row - 2; r <= mob.Loc.Row + 2; r++)
    {
      for (int c = mob.Loc.Col - 2; c <= mob.Loc.Col + 2; c++)
      {
        if (!map.InBounds(r, c))
          continue;
        
        Loc nearby = mob.Loc with { Row = r, Col = c };

        if (nearby == mob.Loc)
          continue;
        if (!ClearShot(gs, mob.Loc, nearby))
          continue;

        if (gs.ObjDb.Occupied(nearby))
          return true;
      }
    }

    return false;
  }
}

class HealAlliesPower(Power power) : UsePower(power)
{
  protected override bool Available(Mob mob, GameState gs)
  {
    if (mob.LastPowerUse.TryGetValue(Power.Name, out ulong lastUse))
    {
      if (gs.Turn < lastUse + Power.Cooldown)
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

      if (Util.PlayerAwareOfActor(mob, gs))
      {
        string castText = $"{MsgFactory.CalcName(mob, gs.Player).Capitalize()} {Grammar.Conjugate(mob, "cast")} a healing spell!";
        gs.UIRef().AlertPlayer(castText);
      }

      mob.ExecuteAction(new HealAction(gs, candidates[i], 4, 4));
      return PlanStatus.Success;
    }

    return PlanStatus.Failure;
  }
}

class UseTurnIntoBatsPower(Power power) : UsePower(power)
{
  protected override bool Available(Mob mob, GameState gs)
  {
    int hp = 1, maxHp = 1;
    if (mob.Stats.TryGetValue(Attribute.HP, out var currHp))
    {
      hp = currHp.Curr;
      maxHp = currHp.Max;

      if (hp >= maxHp / 3)
        return false;
    }
    
    if (mob.LastPowerUse.TryGetValue(Power.Name, out ulong lastUse))
    {
      if (gs.Turn < lastUse + Power.Cooldown)
        return false;
    }
    
    if (mob.Stats[Attribute.HP].Curr < mob.Stats[Attribute.HP].Max * 0.75)
      return true;

    return gs.Rng.Next(5) == 0;
  }

  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    if (!Available(mob, gs))
      return PlanStatus.Failure;

    mob.ExecuteAction(new TransFormIntoBatsAction(gs, mob));
    mob.LastPowerUse[Power.Name] = gs.Turn;

    return PlanStatus.Success;
  }
}

sealed class PassTurn : BehaviourNode
{
  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    PassAction action;
    string bark = mob.GetBark(gs);
    if (bark != "")
      action = new PassAction(gs, mob) { Quip = bark };
    else
      action = new PassAction(gs);

    mob.ExecuteAction(action);

    return PlanStatus.Success;
  }
}

sealed class WithinRange(int range) : BehaviourNode
{
  readonly int _range = range;

  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    Actor target = mob.PickTarget(gs);

    if (target is NoOne)
      return PlanStatus.Failure;

    return Util.Distance(mob.Loc, target.Loc) <= _range ? PlanStatus.Success : PlanStatus.Failure;
  }
}

class TryToEscape : BehaviourNode
{
  Loc GoalLoc { get; set; } = Loc.Nowhere;

  static (Loc, Loc) PickLocToFleeTo(Mob mob, TravelCostFunction costFunc, GameState gs)
  {
    Dictionary<(int, int), int> extraLocCosts = [];
    foreach (GameObj obj in gs.ObjDb.ObjectsOnLevel(mob.Loc.DungeonID, mob.Loc.Level))
    {
      foreach (Trait t in obj.Traits)
      {
        if (t is BlockTrait)
        {
          extraLocCosts[(obj.Loc.Row, obj.Loc.Col)] = DijkstraMap.IMPASSABLE;
        }
        else if (t is OnFireTrait)
        {
          (int, int) sq = (obj.Loc.Row, obj.Loc.Col);
          extraLocCosts[sq] = extraLocCosts.GetValueOrDefault(sq, 0) + 15;
        }
        else if (t is MoldSporesTrait)
        {
          (int, int) sq = (obj.Loc.Row, obj.Loc.Col);
          extraLocCosts[sq] = extraLocCosts.GetValueOrDefault(sq, 0) + 5;
        }
      }
    }

    foreach (Loc occ in gs.ObjDb.OccupantsOnLevel(mob.Loc.DungeonID, mob.Loc.Level))
    {
      extraLocCosts[(occ.Row, occ.Col)] = DijkstraMap.IMPASSABLE;
    }

    Map map = gs.MapForActor(mob);
    DijkstraMap dmap = new(map, extraLocCosts, map.Height, map.Width, false);
    dmap.Generate(costFunc, (mob.Loc.Row, mob.Loc.Col), 10);
    
    int furthest = 0;
    (int, int) best = (-1, -1);    
    for (int r = int.Max(0, mob.Loc.Row - 10); r <= int.Min(map.Height - 1, mob.Loc.Row + 10); r++)
    {
      for (int c = int.Max(0, mob.Loc.Col - 10); c <= int.Min(map.Width - 1, mob.Loc.Col + 10); c++)
      {
        if (dmap.Sqrs[r, c] < int.MaxValue && dmap.Sqrs[r, c] > furthest)
        {
          best = (r, c);
          furthest = dmap.Sqrs[r, c];
        }
        else if (dmap.Sqrs[r, c] == furthest && gs.Rng.Next(6) == 0)
        {
          best = (r, c);
        }          
      }
    }

    if (best == (-1, -1))
      return (Loc.Nowhere, Loc.Nowhere);

    Loc goal = mob.Loc with { Row = best.Item1, Col = best.Item2 };
    var path = AStar.FindPath(gs.ObjDb, gs.CurrentMap, mob.Loc, goal, costFunc, true);
    if (path is not null && path.Count > 0)
      return (path.Peek(), goal);

    return (Loc.Nowhere, Loc.Nowhere);
  }

  static int FleeWithTeleportTraps(Tile tile)
  {
    if (tile.Type == TileType.TeleportTrap)
      return 500;

    return DijkstraMap.CostWithDoors(tile);
  }

  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    if (gs.Rng.Next(10) == 0)
    {
      int att = gs.Rng.NextDouble() < 0.75 ? Mob.AGGRESSIVE : Mob.INDIFFERENT;
      mob.Stats[Attribute.MobAttitude].SetCurr(att);
      return PlanStatus.Success;
    }

    if (mob.Loc == GoalLoc)
      GoalLoc = Loc.Nowhere;

    TravelCostFunction costFunc = DijkstraMap.Cost;
    bool smart = false;
    bool immobile = false;
    foreach (Trait t in mob.Traits)
    {
      if (t is IntelligentTrait)
      {
        smart = true;
        costFunc = FleeWithTeleportTraps;
        break;
      }
      else if (t is FloatingTrait || t is FlyingTrait)
      {
        costFunc = DijkstraMap.CostByFlight;
        break;
      }
      else if (t is ImmobileTrait)
      {
        immobile = true;
        break;
      }
      else if (t is SwimmerTrait)
      {
        costFunc = DijkstraMap.CostForSwimming;
        break;
      }
    }

    // A smart monster will jump on a teleport trap to escape
    if (smart)
    {
      foreach (Loc adj in Util.Adj8Locs(mob.Loc))
      {
        if (gs.TileAt(adj).Type == TileType.TeleportTrap && !gs.ObjDb.Occupied(adj))
        {
          if (gs.LastPlayerFoV.ContainsKey(adj))
            gs.UIRef().AlertPlayer($"{mob.FullName.Capitalize()} jumps into the teleport trap!");
          mob.ExecuteAction(new MoveAction(gs, mob, adj, false));
          GoalLoc = Loc.Nowhere;
          return PlanStatus.Running;
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
        {
          GoalLoc = Loc.Nowhere;
          return PlanStatus.Running;
        }
      }
    }

    if (!immobile)
    {
      Loc loc = Loc.Nowhere;
      if (GoalLoc == Loc.Nowhere)
      {
        var (first, goal) = PickLocToFleeTo(mob, costFunc, gs);
        GoalLoc = goal;
        loc = first;
      }
      else
      {
        var path = AStar.FindPath(gs.ObjDb, gs.CurrentMap, mob.Loc, GoalLoc, costFunc, true);
        loc = path.Count == 0 ? Loc.Nowhere : path.Peek();
      }
      
      if (loc != Loc.Nowhere)
      {
        Tile tile = gs.TileAt(loc);
        if (tile is Door door && !door.Open)
          mob.ExecuteAction(new OpenDoorAction(gs, mob, loc));
        else 
          mob.ExecuteAction(new MoveAction(gs, mob, loc, false));
        
        return PlanStatus.Running;
      }
      else
      {
        GoalLoc = Loc.Nowhere;
        return PlanStatus.Failure;
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

    mob.ExecuteAction(new PassAction(gs));

    return PlanStatus.Running;
  }
}

sealed class WanderInTavern : BehaviourNode
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

    return tile.Passable() && !gs.ObjDb.Occupied(loc) && !gs.ObjDb.AreBlockersAtLoc(loc);
  }

  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    Map map = gs.MapForActor(mob);

    List<Loc> adjs = [.. Util.Adj8Locs(mob.Loc).Where(l => Area.Contains(l) && LocOpen(map, gs, l))];

    if (adjs.Count > 0)
    {
      Loc loc = adjs[gs.Rng.Next(adjs.Count)];

      Action mv = new MoveAction(gs, mob, loc, false);
      string bark = mob.GetBark(gs);
      if (bark != "")
        mv.Quip = bark;
      mob.ExecuteAction(mv);

      return PlanStatus.Success;
    }

    return PlanStatus.Failure;
  }
}

sealed class InArea(HashSet<Loc> sqs) : BehaviourNode
{
  readonly HashSet<Loc> Locations = sqs;

  public override PlanStatus Execute(Mob mob, GameState gs) =>
    Locations.Contains(mob.Loc) ? PlanStatus.Success : PlanStatus.Failure;
}

sealed class HasItem(string name) : BehaviourNode
{
  string ItemName { get; set; } = name;

  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    foreach (Item item in mob.Inventory.Items())
    {
      if (item.Name == ItemName)
        return PlanStatus.Success;
    }

    return PlanStatus.Failure;
  }
}

sealed class IsDaytime : BehaviourNode
{
  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    var (hour, _) = gs.CurrTime();
    return hour >= 7 && hour < 19 ? PlanStatus.Success
                                  : PlanStatus.Failure;
  }
}

sealed class IsEvening : BehaviourNode
{
  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    var (hour, _) = gs.CurrTime();
    return hour >= 19 && hour < 22 ? PlanStatus.Success
                                   : PlanStatus.Failure;
  }
}

sealed class IsNight : BehaviourNode
{
  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    var (hour, _) = gs.CurrTime();
    return hour >= 22 || hour <= 6 ? PlanStatus.Success
                                   : PlanStatus.Failure;
  }
}

sealed class TimeBetween(int startHour, int startMin, int endHour, int endMin) : BehaviourNode
{
  int Start { get; set; } = startHour * 60 + startMin;
  int End { get; set; } = endHour * 60 + endMin;

  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    var (hour, min) = gs.CurrTime();
    int curr = hour * 60 + min;
    bool between = Start <= End ? curr >= Start && curr <= End
                                : curr >= Start || curr <= End;
    return between ? PlanStatus.Success : PlanStatus.Failure;
  }
}

sealed class DiceRoll(int odds) : BehaviourNode
{
  int Odds { get; set; } = odds;

  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    if (gs.Rng.Next(Odds) == 0)
      return PlanStatus.Success;
    return PlanStatus.Failure;
  }
}

sealed class CheckDialogueState(int state) : BehaviourNode
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

class SetMonsterAttitude(int attitude, string blurb) : BehaviourNode
{
  int Attitude { get; set; } = attitude;
  string Blurb { get; set; } = blurb;

  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    int curr = mob.Stats[Attribute.MobAttitude].Curr;

    if (curr == Attitude)
      return PlanStatus.Failure;

    mob.Stats[Attribute.MobAttitude] = new Stat(Attitude);
    gs.UIRef().AlertPlayer(Blurb, gs, mob.Loc);

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

class ThingExists(ulong id) : BehaviourNode
{
  ulong ID { get; set; } = id;

  public override PlanStatus Execute(Mob mob, GameState gs) 
    => gs.ObjDb.GetObj(ID)is not null ? PlanStatus.Success : PlanStatus.Failure;
}

class StandingOn(string item) : BehaviourNode
{
  string ItemToLookFor { get; set; } = item;

  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    foreach (Item item in gs.ObjDb.VisibleItemsAt(mob.Loc))
    {
      if (item.Name == ItemToLookFor)
        return PlanStatus.Success;
    }

    return PlanStatus.Failure;
  }
}

class DropItem(string item) : BehaviourNode
{
  string ItemToLookFor { get; set; } = item;

  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    foreach (Item item in mob.Inventory.Items())
    {
      if (item.Name == ItemToLookFor)
      {
        DropItemAction drop = new(gs, mob) { Choice = item.Slot };
        mob.ExecuteAction(drop);
        return PlanStatus.Success;
      }
    }

    return PlanStatus.Failure;
  }
}

class PickupItem(string item) : BehaviourNode
{
  string ItemToLookFor { get; set; } = item;

  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    foreach (Item item in gs.ObjDb.VisibleItemsAt(mob.Loc))
    {
      if (item.Name == ItemToLookFor)
      {
        PickupItemAction pickUp = new(gs, mob) { ItemIDs = [item.ID] };
        mob.ExecuteAction(pickUp);

        return PlanStatus.Success;
      }
    }

    return PlanStatus.Failure;
  }
}

// For the moment, I am just considering "in danger" to be "am I standing
// beside the player?"
class InDanger : BehaviourNode
{
  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    if (Util.Distance(mob.Loc, gs.Player.Loc) <= 1)
      return PlanStatus.Success;

    return PlanStatus.Failure;
  }
}

class IsFrightened : BehaviourNode
{
  public override PlanStatus Execute(Mob mob, GameState gs) =>
    mob.HasTrait<FrightenedTrait>() ? PlanStatus.Success : PlanStatus.Failure;
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

class IsDisguised : BehaviourNode
{
  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    if (mob.Traits.OfType<DisguiseTrait>().FirstOrDefault() is DisguiseTrait dt && dt.Disguised)
      return PlanStatus.Success;

    return PlanStatus.Failure;
  }
}

class AssumeDisguise : BehaviourNode
{
  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    mob.ExecuteAction(new AssumeDisguiseAction(gs, mob));

    return PlanStatus.Success;
  }
}

class PickWithOdds : BehaviourNode
{
  List<(int, BehaviourNode)> Nodes { get; set; }
  int Max { get; set; }

  public PickWithOdds(List<(BehaviourNode, int)> nodes)
  {
    Nodes = [];
    int x = 0;

    foreach (var (node, n) in nodes)
    {
      x += n;
      Nodes.Add((x, node));
    }

    Max = x;
  }

  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    int x = gs.Rng.Next(1, Max + 1);

    foreach ((int n, BehaviourNode node) in Nodes)
    {
      if (x <= n)
        return node.Execute(mob, gs);
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
  List<Loc> Area { get; set; } = [.. area];
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
        action = new MoveAction(gs, mob, next, false);
        Path.Pop();
      }

      mob.ExecuteAction(action);
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

    TravelCostFunction costFunc = DijkstraMap.Cost;
    if (mob.HasTrait<IntelligentTrait>() || mob.HasTrait<VillagerTrait>())
      costFunc = DijkstraMap.CostWithDoors;
    else if (mob.HasTrait<FloatingTrait>() || mob.HasTrait<FlyingTrait>())
      costFunc = DijkstraMap.CostByFlight;
    var path = AStar.FindPath(gs.ObjDb, gs.CurrentMap, mob.Loc, goal, costFunc, true);
    return path;
  }
}

// A behaviour where the monster will keep its distance, but which I mean
// it will find the longest range of its attack abilities and if its distance
// from the player is less than that, move further away. (With an additional
// check if it can see the player) The intention is that this node is executed
// if none of the monster's powers are available (ie., its firebolt is on
// cooldown or something)
class KeepDistance : BehaviourNode
{
  static bool BadSquare(GameState gs, Loc loc)
  {
    foreach (Item item in gs.ObjDb.ItemsAt(loc))
    {
      foreach (Trait t in item.Traits)
      {
        if (t is BlockTrait)
          return true;
        if (t is OnFireTrait)
          return true;
      }
    }

    return false;
  }

  static PlanStatus Move(Mob mob, int distanceFromPlayer, GameState gs, bool flying)
  {
    List<Loc> opts = [];
    foreach (Loc adj in Util.Adj8Locs(mob.Loc))
    {
      if (BadSquare(gs, adj))
        continue;

      if (Util.Distance(adj, gs.Player.Loc) <= distanceFromPlayer)
        continue;

      Tile tile = gs.TileAt(adj);
      if (gs.ObjDb.Occupied(adj))
        continue;      
      if (tile.Passable())
        opts.Add(adj);
      else if (flying && tile.PassableByFlight())
        opts.Add(adj);
    }

    if (opts.Count == 0)
      return PlanStatus.Failure;

    Loc loc = opts[gs.Rng.Next(opts.Count)];
    Action action = new MoveAction(gs, mob, loc, false);

    mob.ExecuteAction(action);

    return PlanStatus.Success;
  }

  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    int maxRange = 1;

    // Monsters with hunter will approach their target even though they might
    // have ranged abilities
    if (!mob.HasTrait<HunterTrait>() && mob.Powers.Count > 0)
      maxRange = mob.Powers.Where(p => p.Type == PowerType.Attack).Max(p => p.MaxRange);

    if (!gs.Player.VisibleTo(mob) || !ClearShot(gs, mob.Loc, gs.Player.Loc))
      return PlanStatus.Failure;

    bool flying = mob.HasTrait<FlyingTrait>() || mob.HasTrait<FloatingTrait>();
    int d = Util.Distance(mob.Loc, gs.Player.Loc);
    if (d < maxRange)
    {
      return Move(mob, d, gs, flying);
    }

    return PlanStatus.Failure;
  }
}

class RandomMove : BehaviourNode
{
  static bool BadSquare(GameState gs, Loc loc)
  {
    foreach (Item item in gs.ObjDb.ItemsAt(loc))
    {
      foreach (Trait t in item.Traits)
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
      if (tile.Passable() && !gs.ObjDb.Occupied(adj))
      {
        opts.Add((adj, false));
      }
      else if (tile is Door door && !door.Open && door.Type != TileType.LockedDoor)
      {
        opts.Add((adj, true));
      }
    }

    Action action;
    if (opts.Count > 0)
    {
      var (loc, door) = opts[gs.Rng.Next(opts.Count)];
      action = door ? new OpenDoorAction(gs, mob, loc) : new MoveAction(gs, mob, loc, false);
    }
    else
    {
      action = new PassAction(gs, mob);
    }

    string bark = mob.GetBark(gs);
    if (bark != "")
      action.Quip = bark;

    mob.ExecuteAction(action);

    return PlanStatus.Success;
  }

  static PlanStatus Move(Mob mob, GameState gs, bool flying, bool swimmer)
  {
    List<Loc> opts = [];
    foreach (Loc adj in Util.Adj8Locs(mob.Loc))
    {
      if (BadSquare(gs, adj))
        continue;

      Tile tile = gs.TileAt(adj);
      if (gs.ObjDb.Occupied(adj))
        continue;

      if (swimmer && tile.IsWater())
        opts.Add(adj);
      else if (swimmer)
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
      action = new MoveAction(gs, mob, loc, false);
    }
    else
    {
      action = new PassAction(gs, mob);
    }

    string bark = mob.GetBark(gs);
    if (bark != "")
      action.Quip = bark;

    mob.ExecuteAction(action);

    return PlanStatus.Success;
  }

  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    if (mob.HasTrait<ImmobileTrait>())
    {
      mob.ExecuteAction(new PassAction(gs));
      return PlanStatus.Success;
    }

    foreach (Trait t in mob.Traits)
    {
      if (t is IntelligentTrait)
        return MoveOrDoor(mob, gs);
      if (t is FloatingTrait || t is FlyingTrait)
        return Move(mob, gs, true, false);
      if (t is SwimmerTrait)
        return Move(mob, gs, false, true);
    }

    return Move(mob, gs, false, false);
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
    mob.ExecuteAction(new PassAction(gs));
    gs.FlushPerformers();

    return PlanStatus.Success;
  }
}

class FindGoal(IPathBuilder pathBuilder) : BehaviourNode
{
  IPathBuilder PathBuilder { get; set; } = pathBuilder;
  Stack<Loc>? Path { get; set; } = null;

  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    Path ??= PathBuilder.BuildPath(mob.Loc);

    if (PathBuilder.AtGoal(mob.Loc, gs))
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
          mob.ExecuteAction(new OpenDoorAction(gs, mob, next));
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
        mob.ExecuteAction(new MoveAction(gs, mob, next, false));
        return PlanStatus.Running;
      }
    }

    Path = null;
    return PlanStatus.Failure;
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
          mob.ExecuteAction(new OpenDoorAction(gs, mob, next));
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
        mob.ExecuteAction(new MoveAction(gs, mob, next, false));
        return PlanStatus.Running;
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

    Loc stairsLoc = mob.Loc with { Row = stairs[0].Item1, Col = stairs[0].Item2 };
    Goal = stairsLoc;

    TravelCostFunction costFunc = DijkstraMap.Cost;
    if (mob.HasTrait<IntelligentTrait>())
      costFunc = DijkstraMap.CostWithDoors;
    else if (mob.HasTrait<FloatingTrait>() || mob.HasTrait<FlyingTrait>())
      costFunc = DijkstraMap.CostByFlight;
    return AStar.FindPath(gs.ObjDb, gs.CurrentMap, mob.Loc, Goal, costFunc, true);
  }
}

// This predates me switching ChaseTarget to use A* and I can probably 
// ditch it for that BN subclass
class SeekPlayerAStar : BehaviourNode
{ 
  public override PlanStatus Execute(Mob mob, GameState gs)
  {    
    if (Util.Distance(mob.Loc, gs.Player.Loc) <= 1)
      return PlanStatus.Success;

    Stack<Loc> path = AStar.FindPath(gs.ObjDb, gs.CurrentMap, mob.Loc, gs.Player.Loc, DijkstraMap.Cost, true);

    if (path.Count == 0)
      return PlanStatus.Failure;

    Loc loc = path.Pop();

    // We still check if the tile is passable because, say, a door might be
    // closed after the current dijkstra map is calculated and before it is
    // refreshed      
    if (!gs.ObjDb.Occupied(loc) && gs.TileAt(loc).Passable())
    {
      mob.ExecuteAction(new MoveAction(gs, mob, loc, false));
      return PlanStatus.Running;
    }

    return PlanStatus.Failure;
  }
}

// This is for monsters like floating eyes and gas spores that will just 
// want to approach their target and remain adjacent to them
class LurkNearTarget : BehaviourNode
{
  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    Actor target = mob.PickTarget(gs);

    if (target is not NoOne && Util.Distance(mob.Loc, target.Loc) <= 1)
    {
      mob.ExecuteAction(new PassAction(gs, mob));
      return PlanStatus.Success;      
    }

    return PlanStatus.Failure;
  }
}

class ChaseTarget : BehaviourNode
{
  static PlanStatus ChaseToLoc(Mob mob, Loc target, GameState gs)
  {
    if (mob.HasTrait<ImmobileTrait>())
      return PlanStatus.Failure;

    bool submerged = gs.MapForLoc(target).HasFeature(MapFeatures.Submerged);
    TravelCostFunction costFunc = DijkstraMap.Cost;
    if (mob.HasTrait<IntelligentTrait>())
      costFunc = DijkstraMap.CostWithDoors;
    else if (mob.HasTrait<FloatingTrait>() || mob.HasTrait<FlyingTrait>())
      costFunc = DijkstraMap.CostByFlight;
    else if (mob.HasTrait<SwimmerTrait>())
      // Use amphibian costs if level is submerged, otherwise swimmers can't
      // move through submerged non-water tiles. (Because I didn't originally
      // plan on underwater levels early in delve dev sigh...and I don't want
      // to take time out to clean it all up atm
      costFunc = submerged ? DijkstraMap.CostForAmphibians : DijkstraMap.CostForSwimming;
    else if (mob.HasTrait<AmphibiousTrait>())
      costFunc = DijkstraMap.CostForAmphibians;
    else if (mob.Traits.Any(t => t is ImmunityTrait it && it.Type == DamageType.Fire)) 
    {
      costFunc = DijkstraMap.WrapCostFunction(new() { [TileType.Lava] = 1 });
    }
    // For Swimmers, if the target is on a non-water tile on a non-submerged
    // level, look for adjacent water tile instead
    if (mob.HasTrait<SwimmerTrait>() && !submerged)
    {
      Tile targetTile = gs.TileAt(target);
      if (!targetTile.IsWater())
      {
        Loc? adjacentWater = null;
        foreach (var adj in Util.Adj8Locs(target))
        {
          if (gs.CurrentMap.InBounds(adj.Row, adj.Col) && gs.TileAt(adj).IsWater())
          {
            adjacentWater = adj;
            break;
          }
        }

        if (adjacentWater is null)
          return PlanStatus.Failure; // No adjacent water tile, can't reach target
        
        target = adjacentWater.Value;          
      }
    }

    Stack<Loc> path = AStar.FindPath(gs.ObjDb, gs.CurrentMap, mob.Loc, target, costFunc, true);

    if (path.Count > 0)    
    {
      Loc loc = path.Pop();
      Tile tile = gs.TileAt(loc);

      if (tile is Door door && !door.Open)
      {
        mob.ExecuteAction(new OpenDoorAction(gs, mob, loc));
        return PlanStatus.Success;
      }
      else if (!gs.ObjDb.Occupied(loc))
      {
        mob.ExecuteAction(new MoveAction(gs, mob, loc, false));
        return PlanStatus.Success;
      }
    }

    return PlanStatus.Failure;
  }

  public override PlanStatus Execute(Mob mob, GameState gs)
  {
    Loc targetLoc = mob.PickTargetLoc(gs, int.MaxValue);
    if (targetLoc == Loc.Nowhere)
      return PlanStatus.Failure;

    return ChaseToLoc(mob, targetLoc, gs);
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
      else if (prevTile is Door prevDoor && prevDoor.Open && !gs.ObjDb.Occupied(PrevLoc))
      {
        action = new CloseDoorAction(gs, mob, PrevLoc);

        // This prevents the mob from infinitely attempting to close the 
        // door, but I need an actual way to detect the action failed
        // and abort the current plan.
        PrevLoc = mob.Loc;
      }
      else
      {
        PrevLoc = mob.Loc;
        action = new MoveAction(gs, mob, next, false);
        Path.Pop();
      }

      mob.ExecuteAction(action);

      return PlanStatus.Running;
    }

    return PlanStatus.Success;
  }
}