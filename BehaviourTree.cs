// Delve - A roguelike computer RPG
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
    bool swimmer = false;
    foreach (Trait t in mob.Traits)
    {
      if (t is IntelligentTrait)
        smart = true;
      else if (t is FloatingTrait || t is FlyingTrait)
        airborne = true;
      else if (t is ImmobileTrait)
        immobile = true;
      else if (t is SwimmerTrait)
        swimmer = true;
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
      else if (swimmer)
        map = gs.GetDMap("swim");
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

class HasItem(string name) : BehaviourNode
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

    TravelCostFunction costFunc = DijkstraMap.Cost;
    if (mob.HasTrait<IntelligentTrait>())
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
    Action action = new MoveAction(gs, mob, loc);

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
    mob.ExecuteAction(new PassAction());
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
      mob.ExecuteAction(new MoveAction(gs, mob, loc));
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

    TravelCostFunction costFunc = DijkstraMap.Cost;
    if (mob.HasTrait<IntelligentTrait>())
      costFunc = DijkstraMap.CostWithDoors;
    else if (mob.HasTrait<FloatingTrait>() || mob.HasTrait<FlyingTrait>())
      costFunc = DijkstraMap.CostByFlight;
    else if (mob.HasTrait<SwimmerTrait>())
      costFunc = DijkstraMap.CostForSwimming;
    else if (mob.HasTrait<AmphibiousTrait>())
      costFunc = DijkstraMap.CostForAmphibians;

    // For Swimmers, if the target is on a non-water tile, look for adjacent
    // water tile instead
    if (mob.HasTrait<SwimmerTrait>())
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
        mob.ExecuteAction(new MoveAction(gs, mob, loc));
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
      else if (prevTile is Door prevDoor && prevDoor.Open)
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

    FindNearbyItem goldFinder = new(mob, gs, "zorkmid");
    BehaviourNode seekGold = new FindGoal(goldFinder);
    Selector huntGold = new([
      seekGold,
      pickUpGold
    ]);

    Sequence gold = new([new Not(new InDanger()), huntGold]) { Label = "seekgold" };

    // Insert the seek gold node after inactive. The greedy monster will hunt
    // gold unless it is immediately adjacent to the player.
    Selector plan = CreateMonsterPlan(mob);
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

    Selector worshipBehaviour = new([
      new RepeatWhile(
        worshipCondition,
        new WanderInArea(nearbyTiles)
      ),
      new SetMonsterAttitude(Mob.AGGRESSIVE, s)
    ]);

    Selector plan = CreateMonsterPlan(mob);

    int i = 0;
    for (; i < plan.Children.Count; i++)
    {
      if (plan.Children[i].Label == "indifferent")
        break;
    }

    plan.Children.RemoveAt(i);
    plan.Children.Insert(i, worshipBehaviour);

    return plan;
  }

  static Selector CreateMimicPlan(Mob mimic)
  {    
    List<BehaviourNode> actions = [];
    List<BehaviourNode> passive = [];
    foreach (Power p in mimic.Powers)
    {
      // Some of the powers have slightly more complicated ways of
      // calculating if they are available to use so I am doing them as 
      // subclasses of UsePower. If I get too many of them and this gets gross,
      // I'll have to come up with something cleaner. An actual factory or such?
      BehaviourNode up = p.Name switch
      {
        "Gulp" => new GulpPower(p),
        "Crush" => new CrushPower(p),
        "HealAllies" => new HealAlliesPower(p),
        "TurnIntoBats" => new UseTurnIntoBatsPower(p),
        "Nudity" or "FogCloud" => new SeeToTargetPower(p),
        "Whirlpool" => 
          new UseWhirlpoolPower(p),
        _ => new UsePower(p)
      };

      actions.Add(up);
      if (p.Type == PowerType.Passive)
        passive.Add(up);
    }

    List<BehaviourNode> plan = [
      new Sequence([new IsImmobilized(), new PassTurn()]) { Label = "immobilized" },
      new Sequence([new CheckMonsterAttitude(Mob.INACTIVE), new PassTurn()]) { Label = "inactive" },
      new Sequence([new IsFrightened(), new TryToEscape()]) { Label = "scared" }
    ];

    plan.Add(new Sequence([new IsDisguised(), new Selector([ new Selector(actions), new PassTurn()])]) { Label = "disguised" });
    plan.Add(new Selector([new Selector(actions), 
      new PickWithOdds([(new ChaseTarget(), 5), (new AssumeDisguise(), 2)]), 
      new PassTurn()]) { Label = "revealed" });

    return new Selector(plan);
  }

  static Selector CreateMonsterPlan(Mob mob)
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
        "TurnIntoBats" => new UseTurnIntoBatsPower(p),
        "Nudity" or "FogCloud" => new SeeToTargetPower(p),
        _ => new UsePower(p)
      };

      actions.Add(up);
      if (p.Type == PowerType.Passive)
        passive.Add(up);
    }

    // This will make the monster move to toward the player/target until they 
    // are adjacent and then just hang out
    if (actions.Count == 0 && passive.Count == 0)
      actions.Add(new LurkNearTarget());

    // Not yet handling confused monsters, etc

    List<BehaviourNode> plan = [];

    // A paralyzed monster will just pass its turn
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
      {
        actions.Add(new KeepDistance());
        actions.Add(new ChaseTarget());
      }
      plan.Add(new Sequence([new CheckMonsterAttitude(Mob.AGGRESSIVE), new Selector(actions) { Label = "powers" }]) { Label = "aggressive" });

      plan.Add(new PassTurn() { Label = "default" });
    }
    else
    {
      plan.Add(new Sequence([new Selector(actions), new RandomMove() { Label = "default" }]));
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

  static BehaviourNode BasicWander(Mob mob, GameState gs)
  {
    HashSet<Loc> locs = [];
    Map map = gs.MapForLoc(mob.Loc);
    for (int r = 1; r < map.Height - 1; r++)
    {
      for (int c = 1; c < map.Width - 1; c++)
      {
        switch (map.TileAt(r, c).Type)
        {
          case TileType.DungeonFloor:
          case TileType.IllusoryWall:
          case TileType.Upstairs:
          case TileType.Downstairs:
          case TileType.OpenDoor:
          case TileType.ClosedDoor:
          case TileType.BrokenDoor:        
            locs.Add(mob.Loc with { Row = r, Col = c });
            break;
        }
      }
    }

    return new WanderInArea(locs);
  }

  static BehaviourNode BasicVillager(Mob mob, GameState gs)
  {
    int homeId = mob.Stats[Attribute.HomeID].Curr;

    return WanderInHome(gs.Town.Homes[homeId], gs);
  }

  static BehaviourNode Pup(Mob mob,GameState gs)
  {
    HashSet<Loc> townSqs = [];

    for (int r = gs.Town.Row; r < gs.Town.Row + gs.Town.Height; r++)
    {
      for (int c = gs.Town.Col; c < gs.Town.Col + gs.Town.Width; c++)
      {
        townSqs.Add(new Loc(0, 0, r, c));
      }
    }

    RepeatWhile idleCondition = new RepeatWhile(new CheckMonsterAttitude(0), new WanderInArea(townSqs));

    Sequence pickupBone = new([
      new StandingOn("bone"),
      new PickupItem("bone")
    ]);

    FindNearbyItem boneFinder = new(mob, gs, "bone");
    BehaviourNode seekBone = new FindGoal(boneFinder);    
    Sequence fetchBone = new([new CheckMonsterAttitude(1), new Selector([seekBone, pickupBone]), new SetMonsterAttitude(2, "Arf!")]);

    string blurb = $"{mob.FullName.Capitalize()} wags its tail.";
    Sequence deliverBone = new([
      new CheckMonsterAttitude(2), 
      new Sequence([ new SeekPlayerAStar(), new DropItem("bone"), new SetMonsterAttitude(0, blurb)])
    ]);

    Selector plan = new([idleCondition, fetchBone, deliverBone]);

    return plan;
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
    "MimicPlan" => CreateMimicPlan(mob),
    "PrisonerPlan" => CreatePrisonerPlan(mob),
    "PriestPlan" => WanderInHome(gs.Town.Shrine, gs),
    "GrocerPlan" => WanderInHome(gs.Town.Market, gs),
    "BasicVillagerPlan" => BasicVillager(mob, gs),
    "WitchPlan" => WitchPlan(mob, gs),
    "AlchemistPlan" => AlchemistPlan(mob, gs),
    "BarHoundPlan" => WanderInHome(gs.Town.Tavern, gs),
    "PupPlan" => Pup(mob, gs),
    "SimpleRandomPlan" => new Selector([new RandomMove(), new PassTurn()]),
    "MoonClericPlan" => new Selector([
      new Sequence([new CheckDialogueState(1), new DiceRoll(250), new MoveLevel()]),
      new RandomMove(), new PassTurn()]),
    "BasicIllusionPlan" => new Selector([new ChaseTarget(), new RandomMove()]),
    "Greedy" => CreateGreedyMonster(mob, gs),
    "Worshipper" => CreateWorshipperPlan(mob, gs),
    "BasicWander" => BasicWander(mob, gs),
    _ => throw new Exception($"Unknown Behaviour Tree plan: {plan}")
  };

  // This will expand into the function to calculate the target for mob 
  // attacks. I'm not sure if this belongs here, in the Mob/Actor class, or
  // Behaviour class, but for the moment 'Planner' is where I am trying to
  // place the "Decide what to do" code
  public static ulong SelectTarget(Mob mob, GameState gs) => gs.Player.ID;
}
