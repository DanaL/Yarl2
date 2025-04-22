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

enum PlanStatus { Success, Failure, Running }

interface IPathBuilder
{
  Stack<Loc> BuildPath(Loc start);
  bool AtGoal(Loc loc, GameState gs);
}

class FindNearbyGold(Mob mob, GameState gs) : IPathBuilder
{
  Mob Mob { get; set; } = mob;
  Loc Goal { get; set; } = Loc.Nowhere;
  GameState GS { get; set; } = gs;

  public Stack<Loc> BuildPath(Loc start)
  {
    var fov = FieldOfView.CalcVisible(9, Mob.Loc, GS.MapForLoc(Mob.Loc), GS.ObjDb);
    PriorityQueue<Loc, int> goldLocs = new();

    foreach (Loc loc in fov.Keys)
    {
      foreach (Item item in GS.ObjDb.VisibleItemsAt(loc))
      {
        if (item.Name == "zorkmid")
        {
          goldLocs.Enqueue(loc, Util.Distance(start, loc));
          break;
        }
      }
    }

    while (goldLocs.Count > 0)
    {
      Loc loc = goldLocs.Dequeue();
      Stack<Loc> path = AStar.FindPath(GS.ObjDb, GS.MapForLoc(start), start, loc, TravelCosts(Mob), true);
      if (path.Count > 0)
        return path;
    }

    return [];
  }

  static Dictionary<TileType, int> TravelCosts(Mob mob)
  {
    Dictionary<TileType, int> costs = [];
    costs.Add(TileType.DungeonFloor, 1);
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
      Stack<Loc> path = AStar.FindPath(GS.ObjDb, GS.MapForLoc(start), start, loc, TravelCosts, false);
      if (path.Count > 0)
        return path;
    }

    return [];
  }

  public bool AtGoal(Loc loc, GameState gs) => Area.Contains(loc);

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
  public string Label { get; set; } = "";

  public abstract PlanStatus Execute(Mob mob, GameState gs);
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

class Selector(List<BehaviourNode> nodes) : BehaviourNode
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
      mob.ExecuteAction(Power.Action(mob, gs, mob.PickTargetLoc(gs)));

      return PlanStatus.Success;
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

      mob.ExecuteAction(new HealAction(gs, candidates[i], 4, 4));
      return PlanStatus.Success;
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
          mob.ExecuteAction(new MoveAction(gs, mob, adj));
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
        if (tile is Door door && !door.Open)
          mob.ExecuteAction(new OpenDoorAction(gs, mob, loc));
        else
          mob.ExecuteAction(new MoveAction(gs, mob, loc));
        return PlanStatus.Running;
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

    mob.ExecuteAction(new PassAction());
    return PlanStatus.Running;
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

    return tile.Passable() && !gs.ObjDb.Occupied(loc) && !gs.ObjDb.AreBlockersAtLoc(loc);
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

class ValidTarget(Actor actor) : BehaviourNode
{
  Actor Actor { get; set; } = actor;

  public override PlanStatus Execute(Mob mob, GameState gs) =>
    Actor is NoOne ? PlanStatus.Failure : PlanStatus.Success;
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
        action = new MoveAction(gs, mob, next);
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

    mob.ExecuteAction(action);
    return PlanStatus.Success;
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

    mob.ExecuteAction(action);
    return PlanStatus.Success;
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
        mob.ExecuteAction(new MoveAction(gs, mob, next));
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
        mob.ExecuteAction(new MoveAction(gs, mob, next));
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
        mob.ExecuteAction(new OpenDoorAction(gs, mob, loc));
        return PlanStatus.Success;
      }
      else if (!gs.ObjDb.Occupied(loc) && gs.TileAt(loc).Passable())
      {
        mob.ExecuteAction(new MoveAction(gs, mob, loc));
        return PlanStatus.Success;
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
        mob.ExecuteAction(new MoveAction(gs, mob, loc));
        return PlanStatus.Success;
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
        mob.ExecuteAction(new MoveAction(gs, mob, loc));
        return PlanStatus.Success;
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

      mob.ExecuteAction(action);

      return PlanStatus.Running;
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

  static BehaviourNode CreateGreedyMonster(Mob mob, GameState gs)
  {
    Sequence pickUpGold = new([
      new StandingOn("zorkmid"),
      new PickupItem("zorkmid")
    ]);

    FindNearbyGold goldFinder = new(mob, gs);
    BehaviourNode seekGold = new FindGoal(goldFinder);
    Selector huntGold = new([
      seekGold,
      pickUpGold
    ]);

    Sequence gold = new([new Not(new InDanger()), huntGold]) { Label = "seekgold" };

    // Insert the seek gold node after inactive. The greedy monster will hunt
    // gold unless it is immediately adjacent to the player.
    Selector plan = (Selector)CreateMonsterPlan(mob);
    int i = 0;
    foreach (BehaviourNode node in plan.Children)
    {
      if (node.Label == "inactive")
        break;
      ++i;
    }
    plan.Children.Insert(i + 1, gold);

    return plan;
  }

  static BehaviourNode CreateWorshipperPlan(Mob mob, GameState gs)
  {
    WorshiperTrait worshipTrait = mob.Traits.OfType<WorshiperTrait>().First();
    
    Loc altarLoc = worshipTrait.AltarLoc;
    HashSet<Loc> nearbyTiles = [];
    for (int r = altarLoc.Row - 3; r <= altarLoc.Row + 3; r++)
    {
      for (int c = altarLoc.Col - 3; c <= altarLoc.Col + 3; c++)
      {
        Loc loc = altarLoc with { Row = r, Col = c };
        if (gs.TileAt(loc).Passable())
          nearbyTiles.Add(loc);
      }
    }

    string name = MsgFactory.CalcName(mob, gs.Player).Capitalize();
    string s = $"{name} gets angry!";

    Sequence worshipCondition = new([
      new Not(new CheckMonsterAttitude(Mob.AGGRESSIVE)),
      new ThingExists(worshipTrait.AltarId)
    ]);

    Selector worshipBehaviour = new ([
      new RepeatWhile(
        worshipCondition, 
        new WanderInArea(nearbyTiles)
      ),
      new SetMonsterAttitude(Mob.AGGRESSIVE, s)
    ]);
    
    Selector plan = (Selector)CreateMonsterPlan(mob);

    int i = 0;
    for ( ; i < plan.Children.Count; i++)
    {
      if (plan.Children[i].Label == "indifferent")
        break;
    }

    plan.Children.RemoveAt(i);
    plan.Children.Insert(i, worshipBehaviour);

    return plan;
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
    plan.Add(new Sequence([new IsImmobilized(), new PassTurn()]) { Label = "immobilized" });

    // As will an inactive one
    plan.Add(new Sequence([new CheckMonsterAttitude(Mob.INACTIVE), new PassTurn()]) { Label = "inactive" });

    // An indifferent monster might use Passive abilities and/or wander randomly
    // (if not immobile)
    List<BehaviourNode> indifferentNodes = [new CheckMonsterAttitude(Mob.INDIFFERENT)];
    if (passive.Count > 0)
      indifferentNodes.Add(new Selector(passive));
    if (immobile)
      indifferentNodes.Add(new PassTurn());
    else
      indifferentNodes.Add(new PickRandom([new PassTurn(), new RandomMove()]));
    plan.Add(new Sequence(indifferentNodes) { Label = "indifferent" });

    // An afraid monster tries to escape
    plan.Add(new Sequence([new IsFrightened(), new TryToEscape()]) { Label = "scared" });

    if (!mob.HasTrait<PassiveTrait>())
    {
      // Finally, try to attack the player or move toward them.
      if (!mob.HasTrait<ImmobileTrait>())
        actions.Add(new ChaseTarget());
      plan.Add(new Sequence([new CheckMonsterAttitude(Mob.AGGRESSIVE), new Selector(actions)]) { Label = "aggressive" });

      plan.Add(new PassTurn() { Label = "default" });
    }
    else
    {
      plan.Add(new RandomMove() { Label = "default" });
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

    Sequence atInn = new([
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
    "Greedy" => CreateGreedyMonster(mob, gs),
    "Worshipper" => CreateWorshipperPlan(mob, gs),
    _ => throw new Exception($"Unknown Behaviour Tree plan: {plan}")
  };

  // This will expand into the function to calculate the target for mob 
  // attacks. I'm not sure if this belongs here, in the Mob/Actor class, or
  // Behaviour class, but for the moment 'Planner' is where I am trying to
  // place the "Decide what to do" code
  public static ulong SelectTarget(Mob mob, GameState gs) => gs.Player.ID;
}
