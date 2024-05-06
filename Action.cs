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

namespace Yarl2;

class ActionResult
{
  public bool Complete { get; set; }
  public List<Message> Messages { get; set; } = [];
  public string MessageIfUnseen { get; set; } = "";
  public Action? AltAction { get; set; }
  public double EnergyCost { get; set; } = 0.0;
  
  public ActionResult() { }
}

abstract class Action
{
  public Actor? Actor { get; set; }
  public GameState? GameState { get; set; }
  public string Quip { get; set; } = "";
  public int QuipDuration { get; set; } = 2500;

  public Action() { }
  public Action(GameState gs, Actor actor)
  {
    Actor = actor;
    GameState = gs;
  }

  public virtual ActionResult Execute()
  {
    if (!string.IsNullOrEmpty(Quip) && Actor is not null && GameState is not null)
    {
      var bark = new BarkAnimation(GameState, QuipDuration, Actor, Quip);
      GameState.UIRef().RegisterAnimation(bark);      
    }

    return new ActionResult();
  }

  public virtual void ReceiveAccResult(AccumulatorResult result) { }
}

class MeleeAttackAction(GameState gs, Actor actor, Loc loc) : Action(gs, actor)
{
  Loc _loc = loc;
  
  public override ActionResult Execute()
  {
    var result = base.Execute();
    result.Complete = true;

    var target = GameState!.ObjDb.Occupant(_loc);
    if (target is not null)
      result = Battle.MeleeAttack(Actor!, target, GameState);
    else
    {
      result.EnergyCost = 1.0;
      var msg = $"{Actor.FullName.Capitalize()} {Grammar.Conjugate(Actor, "swing")} wildly!";
      result.Messages.Add(new Message(msg, Actor.Loc));
    }

    return result;
  }
}

// This is a different class from MissileAttackAction because it will take the result the 
// aim selection. It also handles the animation and following the path of the arrow
class ArrowShotAction(GameState gs, Actor actor, Item ammo, int attackBonus) : Action(gs, actor)
{
  Loc? _loc;
  readonly Item _ammo = ammo;
  readonly int _attackBonus = attackBonus;

  public override ActionResult Execute()
  {
    var result = base.Execute();

    if (_loc is Loc loc)
    {      
      // Calculate the path the arrow will tkae. If there is a monster in the way of the one
      // actually targetted, it might be struck instead
      var trajectory = Util.Bresenham(Actor!.Loc.Row, Actor.Loc.Col, loc.Row, loc.Col)
                           .Select(p => new Loc(Actor.Loc.DungeonID, Actor.Loc.Level, p.Item1, p.Item2))
                           .ToList();
      List<Loc> pts = [];
      for (int j = 0; j < trajectory.Count; j++)
      {
        var pt = trajectory[j];
        var tile = GameState!.TileAt(pt);
        if (GameState.ObjDb.Occupant(pt) is Actor occ && occ != Actor)
        {
          pts.Add(pt);
          var attackResult = Battle.MissileAttack(Actor!, occ, GameState, _ammo, _attackBonus);
          result.Messages.AddRange(attackResult.Messages);
          result.EnergyCost = attackResult.EnergyCost;
          if (attackResult.Complete)
            break;
        }
        else if (tile.Passable() || tile.PassableByFlight())
        {
          pts.Add(pt);
        }
        else
        {
          break;
        }
      }

      var anim = new ArrowAnimation(GameState!, pts, _ammo.Glyph.Lit);
      GameState!.UIRef().PlayAnimation(anim, GameState);
    }
    else
    {
      throw new Exception("Null location passed to ArrowShotAction. Why would you do that?");
    }

    return result;
  }

  public override void ReceiveAccResult(AccumulatorResult result)
  {
    var locResult = result as LocAccumulatorResult;
    _loc = locResult.Loc;
  }
}

class MissileAttackAction(GameState gs, Actor actor, Loc? loc, Item ammo, int attackBonus) : Action(gs, actor)
{
  Loc? _loc = loc;
  readonly Item _ammo = ammo;
  readonly int _attackBonus = attackBonus;

  public override ActionResult Execute()
  {
    var result = new ActionResult() { Complete = true };

    if (_loc is Loc loc)
    {
      var target = GameState!.ObjDb.Occupant(loc);
      if (target is not null)
        result = Battle.MissileAttack(Actor!, target, GameState, _ammo, _attackBonus);

      return result;
    }
    else
    {
      throw new Exception("Null location passed to MissileAttackAction. Why would you do that?");
    }
  }

  public override void ReceiveAccResult(AccumulatorResult result)
  {
    var locResult = result as LocAccumulatorResult;
    _loc = locResult.Loc;
  }
}

class ApplyTraitAction(GameState gs, Actor actor, BasicTrait trait) : Action(gs, actor)
{
  readonly BasicTrait _trait = trait;

  public override ActionResult Execute()
  {
    ActionResult result = base.Execute();
    result.EnergyCost = 1.0;

    if (Actor is not null)
    {
      Actor.Traits.Add(_trait);
      if (_trait is IGameEventListener listener)
        GameState!.RegisterForEvent(GameEventType.EndOfRound, listener);
      if (_trait is IOwner owned)
        owned.OwnerID = Actor.ID;

      string desc = _trait.Desc();
      if (desc.Length > 0)
        result.Messages.Add(new Message(Actor.FullName.Capitalize() + " " + desc, Actor.Loc));    
    }
    
    return result;
  }
}

class AoEAction(GameState gs, Actor actor, Loc target, EffectFactory ef, int radius, string txt) : Action(gs, actor)
{
  Loc _target { get; set; } = target;
  EffectFactory _effectFactory { get; set; } = ef;
  int _radius { get; set; } = radius;
  string _effectText { get; set; } = txt;

  public override ActionResult Execute()
  {
    var result = base.Execute();
    result.Messages.Add(new Message(_effectText, Actor.Loc));

    var affected = GameState!.Flood(_target, _radius);
    foreach (var loc in affected)
    {
      // Ugh at the moment I can't handle things like a fireball
      // hitting an area and damaging items via this :/
      if (GameState.ObjDb.Occupant(loc) is Actor occ)
      {
        var effect = _effectFactory.Get(occ.ID);
        if (effect.IsAffected(occ, GameState))
        {
          string txt = effect.Apply(occ, GameState);
          if (txt != "")
            result.Messages.Add(new Message(txt, loc));
        }
      }
    }

    return result;
  }
}

// Action for when an actor jumps into a river or chasm (and eventually lava?)
class DiveAction(GameState gs, Actor actor, Loc loc) : Action(gs, actor)
{
  Loc _loc { get; set; } = loc;

  void PlungeIntoWater(Actor actor, GameState gs, ActionResult result)
  {    
    gs.UIRef().AlertPlayer(new Message($"{actor.FullName.Capitalize()} {Grammar.Conjugate(actor, "plunge")} into the water!", actor.Loc), "", gs);
    
    string msg = gs.FallIntoWater(actor, _loc);
    if (msg.Length > 0)
    {
      result.Messages.Add(new Message(msg, actor.Loc));
    }
  }

  void PlungeIntoChasm(Actor actor, GameState gs, ActionResult result)
  {
    gs.UIRef().AlertPlayer(new Message($"{actor.FullName.Capitalize()} {Grammar.Conjugate(actor, "leap")} into the darkness!", actor.Loc), "", gs);
    var landingSpot = new Loc(_loc.DungeonID, _loc.Level + 1, _loc.Row, _loc.Col);

    string msg = gs.FallIntoChasm(actor, landingSpot);
    result.Messages.Add(new Message(msg, landingSpot));
    result.Messages.Add(new Message(gs.LocDesc(landingSpot), landingSpot));
  }

  public override ActionResult Execute()
  {
    var result = base.Execute();
    result.EnergyCost = 1.0;

    var tile = GameState!.TileAt(_loc);
    if (tile.Type == TileType.DeepWater)
    {
      PlungeIntoWater(Actor!, GameState, result);
    }
    else if (tile.Type == TileType.Chasm)
    {
      PlungeIntoChasm(Actor!, GameState, result);
    }

    return result;
  }

  public override void ReceiveAccResult(AccumulatorResult result) { }
}

class PortalAction : Action
{  
  public PortalAction(GameState gameState) => GameState = gameState;

  protected void UsePortal(Portal portal, ActionResult result)
  {
    var start = GameState!.Player!.Loc;
    var (dungeon, level, _, _) = portal.Destination;
    GameState.EnterLevel(GameState.Player!, dungeon, level);
    GameState.Player!.Loc = portal.Destination;
    GameState.ResolveActorMove(GameState.Player!, start, portal.Destination);
    GameState.RefreshPerformers();

    result.Complete = true;

    if (start.DungeonID != portal.Destination.DungeonID)
      result.Messages.Add(new Message(GameState.CurrentDungeon.ArrivalMessage, portal.Destination));

    result.EnergyCost = 1.0;
  }

  public override ActionResult Execute()
  {
    var result = new ActionResult() { Complete = false };

    var p = GameState!.Player!;
    var t = GameState.CurrentMap.TileAt(p.Loc.Row, p.Loc.Col);

    if (t.Type == TileType.Portal)
    {
      UsePortal((Portal)t, result);
    }
    else
    {
      result.Messages.Add(new Message("There is nowhere to go here.", GameState.Player.Loc));
    }

    return result;
  }
}

class DownstairsAction(GameState gameState) : PortalAction(gameState)
{
  public override ActionResult Execute()
  {
    var result = new ActionResult() { Complete = false };

    var p = GameState!.Player!;
    var t = GameState.CurrentMap.TileAt(p.Loc.Row, p.Loc.Col);

    if (t.Type == TileType.Downstairs)
    {
      UsePortal((Portal)t, result);
    }
    else
    {
      result.Messages.Add(new Message("You cannot go down here.", GameState.Player.Loc));
    }

    return result;
  }
}

class UpstairsAction(GameState gameState) : PortalAction(gameState)
{
  public override ActionResult Execute()
  {
    var result = new ActionResult() { Complete = false };

    var p = GameState!.Player!;
    var t = GameState.CurrentMap.TileAt(p.Loc.Row, p.Loc.Col);

    if (t.Type == TileType.Upstairs)
    {
      UsePortal((Portal)t, result);
    }
    else
    {
      result.Messages.Add(new Message("You cannot go up here.", GameState.Player.Loc));
    }

    return result;
  }
}

class ShoppingCompletedAction : Action
{
  readonly Mob _shopkeeper;
  int _invoice;
  List<(char, int)> _selections = [];

  public ShoppingCompletedAction(GameState gs, Mob shopkeeper)
  {
    GameState = gs;
    _shopkeeper = shopkeeper;
  }

  public override ActionResult Execute()
  {
    var result = new ActionResult()
    {
      Complete = _invoice > 0,
      EnergyCost = 1.0
    };

    GameState!.Player.Inventory.Zorkmids -= _invoice;

    foreach (var (slot, count) in _selections)
    {
      List<Item> bought = _shopkeeper.Inventory.Remove(slot, count);
      foreach (var item in bought)
        GameState.Player.Inventory.Add(item, GameState.Player.ID);
    }

    string txt = $"You pay {_shopkeeper.FullName} {_invoice} zorkmid";
    if (_invoice > 1)
      txt += "s";
    txt += " and collect your goods.";

    var msg = new Message(txt, GameState.Player.Loc);
    result.Messages.Add(msg);

    return result;
  }

  public override void ReceiveAccResult(AccumulatorResult result)
  {
    var shopResult = result as ShoppingAccumulatorResult;
    _invoice = shopResult.Zorkminds;
    _selections = shopResult.Selections;
  }
}

abstract class DirectionalAction : Action
{
  protected Loc _loc { get; set; }

  public DirectionalAction(Actor actor)
  {
    Actor = actor;
  }

  public override void ReceiveAccResult(AccumulatorResult result)
  {
    var dirResult = (DirectionAccumulatorResult)result;
    _loc = Actor!.Loc with { Row = Actor.Loc.Row + dirResult.Row, Col = Actor.Loc.Col + dirResult.Col };
  }
}

class ChatAction : DirectionalAction
{
  public ChatAction(GameState gs, Actor actor) : base(actor)
  {
    GameState = gs;   
  }

  public override ActionResult Execute()
  {
    var result = new ActionResult() { Complete = false };

    var other = GameState!.ObjDb.Occupant(_loc);

    if (other is null)
    {
      result.Messages.Add(new Message("There's no one there!", Actor!.Loc));
    }
    else
    {
      var (chatAction, acc) = other.Behaviour.Chat((Mob)other, GameState);

      if (chatAction is NullAction)
      {
        result.Messages.Add(new Message("They aren't interested in chatting.", Actor!.Loc));
        result.Complete = true;
        result.EnergyCost = 1.0;
      }
      else
      {
        GameState.Player.ReplacePendingAction(chatAction, acc!);
      }

      return new ActionResult() { Complete = false, EnergyCost = 0.0 };
    }

    return result;
  }
}

class CloseDoorAction : DirectionalAction
{
  readonly Map _map;

  public CloseDoorAction(GameState gs, Actor actor, Map map) : base(actor)
  {
    GameState = gs;
    _map = map;
  }

  public override ActionResult Execute()
  {
    var result = new ActionResult() { Complete = false };
    var door = _map.TileAt(_loc.Row, _loc.Col);

    if (door is Door d)
    {
      if (d.Open)
      {
        d.Open = false;
        result.Complete = true;
        result.EnergyCost = 1.0;

        var msg = MsgFactory.Phrase(Actor!.ID, Verb.Close, "the door", false, _loc, GameState!);
        result.Messages.Add(msg);
        result.MessageIfUnseen = "You hear a door close.";

        // Find any light sources tht were affecting the door and update them, since
        // it's now open. (Eventually gotta extend it to any aura type effects)
        foreach (var src in GameState!.ObjsAffectingLoc(_loc, TerrainFlag.Lit))
        {
          GameState.CurrentMap.RemoveEffectFromMap(TerrainFlag.Lit, src.ID);
          GameState.ToggleEffect(src, Actor.Loc, TerrainFlag.Lit, true);
        }
      }
      else if (Actor is Player)
      {
        result.Messages.Add(new Message("The door is already closed.", GameState!.Player.Loc));
      }
    }
    else if (Actor is Player)
    {
      result.Messages.Add(new Message("There's no door there!", GameState!.Player.Loc));
    }

    return result;
  }
}

class OpenDoorAction : DirectionalAction
{
  readonly Map _map;

  public OpenDoorAction(GameState gs, Actor actor, Map map) : base(actor)
  {
    _map = map;
    GameState = gs;
  }

  public OpenDoorAction(GameState gs, Actor actor, Map map, Loc loc) : base(actor)
  {
    _map = map;
    _loc = loc;
    GameState = gs;
  }

  public override ActionResult Execute()
  {
    var result = new ActionResult() { Complete = false };
    var door = _map.TileAt(_loc.Row, _loc.Col);

    if (door is Door d)
    {
      if (!d.Open)
      {
        d.Open = true;
        result.Complete = true;
        result.EnergyCost = 1.0;

        var msg = MsgFactory.Phrase(Actor!.ID, Verb.Open, "door", false, _loc, GameState!);
        result.Messages.Add(msg);
        result.MessageIfUnseen = "You hear a door open.";

        // Find any light sources tht were affecting the door and update them, since
        // it's now open. (Eventually gotta extend it to any aura type effects)

        foreach (var src in GameState!.ObjsAffectingLoc(_loc, TerrainFlag.Lit))
        {
          GameState.ToggleEffect(src, src.Loc, TerrainFlag.Lit, true);
        }
        GameState.ToggleEffect(Actor, _loc, TerrainFlag.Lit, true);
      }
      else if (Actor is Player)
      {
        result.Messages.Add(new Message("The door is already open.", GameState!.Player.Loc));
      }
    }
    else if (Actor is Player)
    {
      result.Messages.Add(new Message("There's no door there!", GameState!.Player.Loc));
    }

    return result;
  }
}

class PickupItemAction(GameState gs, Actor actor) : Action(gs, actor)
{
  public ulong ItemID { get; set; }
  
  public override ActionResult Execute()
  {
    var result = new ActionResult() { Complete = true, EnergyCost = 1.0 };

    GameState!.ClearMenu();
    var itemStack = GameState.ObjDb.ItemsAt(Actor!.Loc);
    var inv = Actor.Inventory;
    bool freeSlot = inv.UsedSlots().Length < 26;
    Item item = itemStack.Where(i => i.ID == ItemID).First();

    if (!freeSlot)
    {
      return new ActionResult() {
        Complete = false,
        Messages = [new Message("There's no room in your inventory!", GameState.Player.Loc)]
      };
    }

    // First, is there anything preventing the actor from picking items up
    // off the floor? (At the moment it's just webs in the game, but a 
    // Sword-in-the-Stone situation might be neat)
    foreach (var env in GameState.ObjDb.EnvironmentsAt(Actor.Loc))
    {
      var web = env.Traits.OfType<StickyTrait>().First();
      if (web is not null)
      {
        bool strCheck = Actor.AbilityCheck(Attribute.Strength, web.DC, GameState.Rng);
        if (!strCheck)
        {
          var txt = $"{item.FullName.DefArticle().Capitalize()} {MsgFactory.CalcVerb(item, Verb.Etre)} stuck to {env.Name.DefArticle()}!";
          var stickyMsg = new Message(txt, Actor.Loc);
          return new ActionResult() { EnergyCost = 1.0, Complete = false, Messages = [stickyMsg] };
        }
        else
        {
          var txt = $"{Actor.FullName.Capitalize()} {MsgFactory.CalcVerb(Actor, Verb.Tear)} {item.FullName.DefArticle()} from {env.Name.DefArticle()}.";
          result.Messages.Add(new Message(txt, Actor.Loc));
        }
      }
    }

    char slot = '\0';
    int count = 0;
    if (item.HasTrait<StackableTrait>())
    {
      foreach (var pickedUp in itemStack.Where(i => i == item))
      {
        GameState.ObjDb.RemoveItem(Actor.Loc, item);
        slot = inv.Add(item, Actor.ID);
        ++count;
      }
    }
    else
    {
      GameState.ObjDb.RemoveItem(Actor.Loc, item);
      slot = inv.Add(item, Actor.ID);
    }

    var pickupMsg = $"{Actor.FullName.Capitalize()} {Grammar.Conjugate(Actor, "pick")} up ";
    if (item.Type == ItemType.Zorkmid && item.Value == 1)
      pickupMsg += "a zorkmid.";
    else if (item.Type == ItemType.Zorkmid)
      pickupMsg += $"{item.Value} zorkmids.";
    else if (count > 1)
      pickupMsg += $"{count} {item.FullName.Pluralize()}.";
    else
      pickupMsg += item.FullName.DefArticle() + ".";

    if (slot != '\0')
      pickupMsg += $" ({slot})";
    
    result.Messages.Add(new Message(pickupMsg, Actor.Loc));

    return result;
  }

  public override void ReceiveAccResult(AccumulatorResult result)
  {
    var objIDResult = (ObjIDAccumulatorResult)result;
    ItemID = objIDResult.ID;
  }
}

class UseItemAction(GameState gs, Actor actor) : Action(gs, actor)
{
  public char Choice { get; set; }
  
  public override ActionResult Execute()
  {
    var (item, itemCount) = Actor!.Inventory.ItemAt(Choice);
    GameState!.ClearMenu();

    var useableTraits = item.Traits.Where(t => t is IUSeable).ToList();
    if (useableTraits.Count != 0)
    {
      var result = new ActionResult() { Complete = true, EnergyCost = 1.0 };
      Item? toUse = Actor.Inventory.RemoveByID(item.ID)
                      ?? throw new Exception("Using item in inventory that doesn't exist :O This shouldn't happen :O");

      toUse.Traits = toUse.Traits.Where(t => t is not StackableTrait).ToList();

      if (!toUse.HasTrait<ConsumableTrait>())
        Actor.Inventory.Add(toUse, Actor.ID);

      if (toUse.HasTrait<WrittenTrait>())
      {
        // Eventually being blind will prevent you from reading things

        if (Actor.HasTrait<ConfusedTrait>())
        {
          string txt = $"{Actor.FullName} {Grammar.Conjugate(Actor, "is")} too confused to read that!";
          var msg = new Message(txt, Actor.Loc);
          return new ActionResult() { Complete = true, Messages = [msg], EnergyCost = 1.0 };
        }
      }

      bool success = false;
      foreach (IUSeable trait in useableTraits)
      {
        var useResult = trait.Use(Actor, GameState, Actor.Loc.Row, Actor.Loc.Col);
        result.Complete = useResult.Successful;
        result.Messages.Add(new Message(useResult.Message, Actor.Loc));
        success = useResult.Successful;

        if (useResult.ReplacementAction is not null)
        {
          result.Complete = false;
          result.AltAction = useResult.ReplacementAction;
          result.EnergyCost = 0.0;
        }
      }

      return result;
    }
    else
    {
      var msg = new Message("You don't know a way to use that!", GameState.Player.Loc);
      return new ActionResult() { Complete = true, Messages = [msg], EnergyCost = 0.0 };
    }
  }

  public override void ReceiveAccResult(AccumulatorResult result)
  {
    var menuResult = (MenuAccumulatorResult)result;
    Choice = menuResult.Choice;
  }
}

class SummonAction(Loc target, string summons, int count) : Action()
{
  readonly Loc _target = target;
  readonly string _summons = summons;
  readonly int _count = count;

  Loc SpawnPt()
  {
    var gs = GameState!;
    if (gs.TileAt(_target).Passable() && !gs.ObjDb.Occupied(_target))
      return _target;

    var locs = Util.Adj8Locs(_target)
                   .Where(l => gs.TileAt(l).Passable() && !gs.ObjDb.Occupied(l))
                   .ToList();
    if (locs.Count == 0)
      return Loc.Nowhere;
    else
      return locs[gs.Rng.Next(locs.Count)];
  }

  public override ActionResult Execute()
  {
    base.Execute();

    int summonCount = 0;
    for (int j = 0; j < _count; j++)
    {
      var loc = SpawnPt();
      if (loc != Loc.Nowhere)
      {
        var summoned = MonsterFactory.Get(_summons, GameState!.Rng);
        GameState.ObjDb.AddNewActor(summoned, loc);
        GameState.AddPerformer(summoned);
        ++summonCount;
      }
    }
    
    string txt;
    if (summonCount == 0) 
    {
      txt = "A spell fizzles.";
    }
    else
    {
      txt = $"{Actor!.FullName.Capitalize()} {Grammar.Conjugate(Actor, "summon")} ";
      if (_count == 1)
        txt += _summons.IndefArticle() + "!";
      else
        txt += $"some {_summons.Pluralize()}!";
    }
    
    var msg = new Message(txt, Actor!.Loc);
    return new ActionResult() { Complete = true, Messages = [msg], EnergyCost = 1.0 };
  }
}

class MagicMapAction(GameState gs, Actor caster) : Action(gs, caster)
{
  // Essentially we want to flood fill out and mark all reachable squares as 
  // remembered, ignoreable Passable() or not but stopping a walls. This will
  // currently not fully map a level with disjoint spaces but I'm not sure if
  // I think that's a problem or not.
  void FloodFillMap(GameState gs, Loc start)
  {    
    Dungeon dungeon = gs.CurrentDungeon;
    PriorityQueue<Loc, int> locsQ = new();
    HashSet<Loc> visited = [];
    var q = new Queue<Loc>();
    q.Enqueue(start);
    
    while (q.Count > 0) 
    { 
      var curr = q.Dequeue();
      visited.Add(curr);

      foreach (var adj in Util.Adj8Locs(curr)) 
      {        
        if (visited.Contains(adj))
          continue;

        var tile = gs.TileAt(adj);
        locsQ.Enqueue(adj, Util.Distance(Actor!.Loc, adj));

        switch (tile.Type)
        {
          case TileType.Unknown:
          case TileType.DungeonWall:
          case TileType.PermWall:
          case TileType.WorldBorder:            
            break;
          default:
            if (!visited.Contains(adj))
              q.Enqueue(adj);
            break;
        }

        visited.Add(adj);
      }
    }

    List<Loc> locs = [];
    while (locsQ.Count > 0)
      locs.Add(locsQ.Dequeue());
    
    var anim = new MagicMapAnimation(gs, dungeon, locs);
    gs.UIRef().RegisterAnimation(anim);
  }

  public override ActionResult Execute()
  {
    var result = base.Execute();

    // It's probably a bug if a monster invokes this action??
    if (Actor is Player player)
    {
      result.Complete = true;
      result.EnergyCost = 1.0;

      if (GameState!.InWilderness)
      {
        result.Messages.Add(new Message("The wide world is big for the spell! The magic fizzles!", player.Loc));
      }
      else
      {
        result.Messages.Add(new Message("A vision of your surroundings fills your mind!", player.Loc));
        FloodFillMap(GameState, player.Loc);
      }      
    }

    return result;
  }
}

class MirrorImageAction : Action
{
  readonly Loc _target;

  public MirrorImageAction(GameState gs, Actor caster, Loc target)
  {
    GameState = gs;
    Actor = caster;
    _target = target;
  }

  Mob MakeDuplciate()
  {
    var glyph = new Glyph(Actor.Glyph.Ch, Actor.Glyph.Lit, Actor.Glyph.Unlit);
    
    // I originally implemented MirrorImage for cloakers, who can fly but I
    // think it makes sense for all mirror images since they're illusions that
    // may drift over water/lava 
    var dup = new Mob()
    {
      Name = Actor.Name,
      Glyph = glyph,
      Recovery = 1.0,
      MoveStrategy = new SimpleFlightMoveStrategy()
    };

    dup.Stats.Add(Attribute.HP, new Stat(1));
    dup.Stats.Add(Attribute.AC, new Stat(10));
    dup.Stats.Add(Attribute.XPValue, new Stat(0));
    dup.Traits.Add(new FlyingTrait());
    dup.Stats[Attribute.Attitude] = new Stat((int)MobAttitude.Active);

    var illusion = new IllusionTrait()
    {
      SourceID = Actor.ID,
      ObjID = dup.ID
    };
    dup.Traits.Add(illusion);   
    GameState.RegisterForEvent(GameEventType.Death, illusion, Actor.ID);

    var msg = new DeathMessageTrait() { Message = $"{dup.FullName.Capitalize()} fades away!" };
    dup.Traits.Add(msg);

    return dup;
  }

  public override ActionResult Execute()
  {
    // Mirror image, create 4 duplicates of caster surrounding the target location
    List<Loc> options = [];

    foreach (var loc in Util.Adj8Locs(_target))
    {
      if (GameState!.TileAt(loc).PassableByFlight() && !GameState.ObjDb.Occupied(loc))
        options.Add(loc);
    }

    if (options.Count == 0)
    {
      var msg = new Message("A spell fizzles...", Actor!.Loc);
      return new ActionResult() { Complete = true, Messages = [msg], EnergyCost = 1.0 };
    }

    List<Mob> images = [];
    int duplicates = int.Min(options.Count, 4);
    while (duplicates > 0)
    {
      int i = GameState!.Rng.Next(options.Count);
      Loc loc = options[i];
      options.RemoveAt(i);

      var dup = MakeDuplciate();
      GameState.ObjDb.AddNewActor(dup, loc);
      GameState.AddPerformer(dup);

      images.Add(dup);

      --duplicates;
    }

    // We've created the duplicates so now the caster swaps locations
    // with one of them
    Mob swap = images[GameState.Rng.Next(images.Count)];
    GameState.SwapActors(Actor, swap);

    var result = base.Execute();
    result.Complete = true;
    result.EnergyCost = 1.0;
    result.Messages.Add(new Message("How puzzling!", _target));

    return result;
  }
}

class FogCloudAction : Action
{
  readonly ulong _casterID;
  readonly Loc _target;

  public FogCloudAction(GameState gs, Actor caster, Loc target)
  {
    GameState = gs;
    _casterID = caster.ID;
    _target = target;
  }

  public override ActionResult Execute()
  {
    var gs = GameState!;
    for (int r = _target.Row - 2; r < _target.Row + 3; r++)
    {
      for (int c = _target.Col - 2; c < _target.Col + 3; c++)
      {
        if (!gs.CurrentMap.InBounds(r, c))
          continue;
        var mist = ItemFactory.Mist(gs);
        var mistLoc = _target with { Row = r, Col = c };
        var timer = mist.Traits.OfType<CountdownTrait>().First();
        gs.RegisterForEvent(GameEventType.EndOfRound, timer);
        gs.ObjDb.Add(mist);
        gs.ItemDropped(mist, mistLoc);
      }
    }

    var txt = MsgFactory.Phrase(_casterID, Verb.Cast, _target, gs).Text;
    var msg = new Message(txt + " Fog Cloud!", _target, false);

    return new ActionResult() { Complete = true, Messages = [msg], EnergyCost = 1.0 };
  }
}

class EntangleAction : Action
{
  readonly ulong _casterID;
  readonly Loc _target;

  public EntangleAction(GameState gs, Actor caster, Loc target)
  {
    GameState = gs;
    _casterID = caster.ID;
    _target = target;
  }

  public override ActionResult Execute()
  {
    foreach (var (r, c) in Util.Adj8Sqs(_target.Row, _target.Col))
    {
      var loc = _target with { Row = r, Col = c };
      var tile = GameState!.TileAt(loc);
      if (tile.Type != TileType.Unknown && tile.Passable() && !GameState.ObjDb.Occupied(loc))
      {
        Actor vines = MonsterFactory.Get("vines", GameState.Rng);
        vines.Loc = loc;
        GameState.ObjDb.Add(vines);
        GameState.ObjDb.AddToLoc(loc, vines);
      }
    }

    var txt = MsgFactory.Phrase(_casterID, Verb.Cast, _target, GameState!).Text;
    var msg = new Message(txt + " Entangle!", _target, false);
    return new ActionResult() { Complete = true, Messages = [msg], EnergyCost = 1.0 };
  }
}

class FireboltAction(GameState gs, Actor caster, Loc target, List<Loc> trajectory) : Action(gs, caster)
{
  readonly Loc _target = target;
  readonly List<Loc> _trajectory = trajectory;

  public override ActionResult Execute()
  {
    var anim = new ArrowAnimation(GameState!, _trajectory, Colours.YELLOW_ORANGE);
    GameState!.UIRef().RegisterAnimation(anim);

    var firebolt = ItemFactory.Get("firebolt", GameState!.ObjDb);
    var attack = new MissileAttackAction(GameState, Actor!, _target, firebolt, 0);

    var txt = MsgFactory.Phrase(Actor!.ID, Verb.Cast, _target, GameState).Text;
    var msg = new Message(txt + " Firebolt!", _target, false);
    return new ActionResult() { Complete = true, Messages = [msg], AltAction = attack, EnergyCost = 0.0 };
  }
}

class WebAction : Action
{
  readonly Loc _target;

  public WebAction(GameState gs, Loc target)
  {
    GameState = gs;
    _target = target;
  }

  public override ActionResult Execute()
  {
    var w = ItemFactory.Web();
    GameState!.ObjDb.Add(w);
    GameState.ItemDropped(w, _target);

    foreach (var sq in Util.Adj8Sqs(_target.Row, _target.Col))
    {
      if (GameState.Rng.NextDouble() < 0.666)
      {
        w = ItemFactory.Web();
        GameState.ObjDb.Add(w);
        GameState.ItemDropped(w, _target with { Row = sq.Item1, Col = sq.Item2 });
      }
    }

    var txt = "";
    var victim = GameState.ObjDb.Occupant(_target);
    if (victim is not null)
      txt = $"{victim.FullName.Capitalize()} {MsgFactory.CalcVerb(victim, Verb.Etre)} caught up in webs!";
    var msg = new Message(txt, _target, false);
    return new ActionResult() { Complete = true, Messages = [msg], EnergyCost = 1.0 };
  }
}

class BlinkAction(GameState gs, Actor caster) : Action(gs, caster)
{  
  public override ActionResult Execute()
  {
    List<Loc> sqs = [];
    var start = Actor!.Loc;

    for (var r = start.Row - 12; r < start.Row + 12; r++)
    {
      for (var c = start.Col - 12; c < start.Col + 12; c++)
      {
        var loc = start with { Row = r, Col = c };
        int d = Util.Distance(start, loc);
        if (d >= 8 && d <= 12 && GameState!.TileAt(loc).Passable() && !GameState.ObjDb.Occupied(loc))
        {
          sqs.Add(loc);
        }
      }
    }

    if (sqs.Count == 0)
    {
      var msg = new Message("A spell fizzles...", Actor.Loc);
      return new ActionResult() { Complete = true, Messages = [msg], EnergyCost = 1.0 };
    }
    else
    {
      // Teleporting removes the grapple trait
      var grappled = Actor.Traits.OfType<GrappledTrait>()
                                 .FirstOrDefault();
      if (grappled is not null)
        Actor.Traits.Remove(grappled);
        
      var landingSpot = sqs[GameState!.Rng.Next(sqs.Count)];
      var mv = new MoveAction(GameState, Actor, landingSpot);
      GameState.UIRef().RegisterAnimation(new SqAnimation(GameState, landingSpot, Colours.WHITE, Colours.LIGHT_PURPLE, '*'));
      GameState.UIRef().RegisterAnimation(new SqAnimation(GameState, start, Colours.WHITE, Colours.LIGHT_PURPLE, '*'));
      var msg = MsgFactory.Phrase(Actor.ID, Verb.Blink, Actor.Loc, GameState);
      var txt = $"Bamf! {msg.Text} away!";
      msg = new Message(txt, Actor.Loc);

      return new ActionResult() { Complete = false, Messages = [msg], EnergyCost = 0.0, AltAction = mv };
    }
  }
}

class AntidoteAction(GameState gs, Actor target) : Action(gs, target)
{
  
  public override ActionResult Execute()
  {
    if (Actor is Player && !Actor.HasTrait<PoisonedTrait>())
    {
      return new ActionResult() { Complete = true, Messages = [new Message("That tasted not bad.", Actor.Loc)], EnergyCost = 1.0 };
    }

    foreach (var t in Actor!.Traits.OfType<PoisonedTrait>())
    {
      GameState!.StopListening(GameEventType.EndOfRound, t);
    }
    Actor.Traits = Actor.Traits.Where(t => t is not PoisonedTrait).ToList();
    var msg = new Message($"{Actor.FullName.Capitalize()} {MsgFactory.CalcVerb(Actor, Verb.Feel)} better.", Actor.Loc);

    return new ActionResult() { Complete = true, Messages = [msg], EnergyCost = 1.0 };
  }
}

class HealAction(GameState gs, Actor target, int healDie, int healDice) : Action(gs, target)
{
  readonly int _healDie = healDie;
  readonly int _healDice = healDice;

  public override ActionResult Execute()
  {
    var hp = 0;
    for (int j = 0; j < _healDice; j++)
      hp += GameState!.Rng.Next(_healDie) + 1;
    Actor!.Stats[Attribute.HP].Change(hp);
    var plural = Actor.HasTrait<PluralTrait>();
    var msg = MsgFactory.Phrase(Actor.ID, Verb.Etre, Verb.Heal, plural, false, Actor.Loc, GameState!);
    var txt = msg.Text[..^1] + $" for {hp} HP.";

    return new ActionResult() { Complete = true, Messages = [new Message(txt, Actor.Loc, false)], EnergyCost = 1.0 };
  }
}

class DropZorkmidsAction(GameState gs, Actor actor) : Action(gs, actor)
{
  int _amount;

  public override ActionResult Execute()
  {
    double cost = 1.0;
    bool successful = true;
    Message alert;

    var inventory = Actor!.Inventory;
    if (_amount > inventory.Zorkmids)
    {
      _amount = inventory.Zorkmids;
    }

    if (_amount == 0)
    {
      cost = 0.0; // we won't make the player spend an action if they drop nothing
      successful = false;
      alert = new Message("You hold onto your zorkmids.", Actor.Loc);
    }
    else
    {
      var coins = ItemFactory.Get("zorkmids", GameState!.ObjDb);
      GameState.ItemDropped(coins, Actor.Loc);
      coins.Value = _amount;
      string msg = $"{MsgFactory.CalcName(Actor).Capitalize()} {MsgFactory.CalcVerb(Actor, Verb.Drop)} ";
      if (_amount == 1)
        msg += "a single zorkmid.";
      else if (_amount == inventory.Zorkmids)
        msg += "all your money!";
      else
        msg += $"{_amount} zorkmids.";

      alert = new Message(msg, Actor.Loc);

      inventory.Zorkmids -= _amount;
    }

    return new ActionResult() { Complete = successful, Messages = [alert], EnergyCost = cost };
  }

  public override void ReceiveAccResult(AccumulatorResult result)
  {
    var count = ((NumericAccumulatorResult)result).Amount;
    _amount = count;
  }
}

class DropStackAction(GameState gs, Actor actor, char slot) : Action(gs, actor)
{
  readonly char _slot = slot;
  int _amount;

  public override ActionResult Execute()
  {
    var (item, itemCount) = Actor!.Inventory.ItemAt(_slot);
    GameState!.UIRef().ClosePopup();

    if (_amount == 0 || _amount > itemCount)
      _amount = itemCount;

    var droppedItems = Actor.Inventory.Remove(_slot, _amount);
    foreach (var droppedItem in droppedItems)
    {
      GameState.ItemDropped(droppedItem, Actor.Loc);
      droppedItem.Equiped = false;
    }

    Actor.CalcEquipmentModifiers();
    Message alert = MsgFactory.Phrase(Actor.ID, Verb.Drop, item.ID, _amount, false, Actor.Loc, GameState);

    return new ActionResult() { Complete = true, Messages = [alert], EnergyCost = 1.0 };
  }

  public override void ReceiveAccResult(AccumulatorResult result)
  {
    var count = ((NumericAccumulatorResult)result).Amount;
    _amount = count;
  }
}

class ThrowAction(GameState gs, Actor actor, char slot) : Action(gs, actor)
{
  readonly char _slot = slot;
  Loc _target { get; set; }

  Loc FinalLandingSpot(Loc loc)
  {
    var tile = GameState!.TileAt(loc);

    while (tile.Type == TileType.Chasm)
    {
      loc = loc with { Level = loc.Level + 1 };
      tile = GameState.TileAt(loc);
    }

    return loc;
  }

  void ProjectileLands(List<Loc> pts, Item ammo, ActionResult result)
  {
    var anim = new ThrownMissileAnimation(GameState!, ammo.Glyph, pts, ammo);
    GameState!.UIRef().PlayAnimation(anim, GameState);

    var landingPt = FinalLandingSpot(pts.Last());
    GameState.CheckMovedEffects(ammo, Actor!.Loc, landingPt);
    GameState.ItemDropped(ammo, landingPt);
    ammo.Equiped = false;
    Actor.CalcEquipmentModifiers();

    var tile = GameState.TileAt(landingPt);
    if (tile.Type == TileType.Chasm)
    {
      string txt = $"{ammo.FullName.DefArticle().Capitalize()} tumbles into the darkness.";
      var msg = new Message(txt, landingPt);
      result.Messages.Add(msg);
    }
  }

  public override ActionResult Execute()
  {
    var result = new ActionResult() { Complete = true, EnergyCost = 1.0 };
    var ammo = Actor!.Inventory.Remove(_slot, 1).First();
    if (ammo != null)
    {
      // Calculate where the projectile will actually stop
      var trajectory = Util.Bresenham(Actor.Loc.Row, Actor.Loc.Col, _target.Row, _target.Col)
                              .Select(p => new Loc(Actor.Loc.DungeonID, Actor.Loc.Level, p.Item1, p.Item2))
                              .ToList();
      List<Loc> pts = [];
      for (int j = 0; j < trajectory.Count; j++)
      {
        var pt = trajectory[j];
        var tile = GameState!.TileAt(pt);
        var occ = GameState.ObjDb.Occupant(pt);
        if (j > 0 && occ != null)
        {
          pts.Add(pt);

          // I'm not handling what happens if a projectile hits a friendly or 
          // neutral NPCs
          var attackResult = Battle.MissileAttack(Actor, occ, GameState, ammo, 0);
          result.Messages.AddRange(attackResult.Messages);
          result.EnergyCost = attackResult.EnergyCost;
          if (attackResult.Complete)
          {
            break;
          }
        }
        else if (tile.Passable() || tile.PassableByFlight())
        {
          pts.Add(pt);
        }
        else
        {
          break;
        }
      }

      ProjectileLands(pts, ammo, result);
    }

    return result;
  }

  public override void ReceiveAccResult(AccumulatorResult result)
  {
    var locResult = (LocAccumulatorResult)result;
    _target = locResult.Loc;
  }
}

class FireSelectedBowAction(GameState gs, Player player) : Action(gs, player)
{
  public char Choice { get; set; }

  public override ActionResult Execute()
  {
    var result = base.Execute();

    var ui = GameState!.UIRef();
    ui.CloseMenu();
    var player = Actor as Player;

    var (item, _) = player!.Inventory.ItemAt(Choice);
    if (item is null || item.Type != ItemType.Bow)
    {
      result.Messages.Add(new Message("That doesn't make any sense!", player.Loc));
    }
    else
    {
      item.Equiped = true;
      player.FireReadedBow(item, gs);
      
      result.EnergyCost = 0.0;
      result.Complete = false;
    }

    return result;
  }

  public override void ReceiveAccResult(AccumulatorResult result)
  {
    var menuResult = (MenuAccumulatorResult)result;
    Choice = menuResult.Choice;
  }
}

class ThrowSelectionAction(GameState gs, Player player) : Action(gs, player)
{
  public char Choice { get; set; }
  
  public override ActionResult Execute()
  {
    var ui = GameState!.UIRef();
    ui.CloseMenu();
    var player = Actor as Player;

    var (item, _) = player!.Inventory.ItemAt(Choice);
    if (item is null)
    {
      var msg = new Message("That doesn't make sense", player.Loc);
      var result = new ActionResult() { Complete = false, EnergyCost = 0.0 };
      result.Messages.Add(msg);
      return result;
    }
    else if (item.Type == ItemType.Armour && item.Equiped)
    {
      var msg = new Message("You're wearing that!", player.Loc);
      var result = new ActionResult() { Complete = false, EnergyCost = 0.0 };
      result.Messages.Add(msg);
      return result;
    }

    var action = new ThrowAction(GameState, player, Choice);
    var range = 7 + player.Stats[Attribute.Strength].Curr;
    if (range < 2)
      range = 2;
    var acc = new AimAccumulator(GameState, player.Loc, range);
    player.ReplacePendingAction(action, acc);

    return new ActionResult() { Complete = false, EnergyCost = 0.0 };
  }

  public override void ReceiveAccResult(AccumulatorResult result)
  {
    var menuResult = (MenuAccumulatorResult)result;
    Choice = menuResult.Choice;
  }
}

class DropItemAction(GameState gs, Actor actor) : Action(gs, actor)
{
  public char Choice { get; set; }
  
  public override ActionResult Execute()
  {
    var ui = GameState!.UIRef();
    ui.CloseMenu();

    if (Choice == '$')
    {
      var inventory = Actor!.Inventory;
      if (inventory.Zorkmids == 0)
      {
        var msg = new Message("You have no money!", GameState.Player.Loc);
        return new ActionResult() { Complete = false, Messages = [msg] };
      }
      var dropMoney = new DropZorkmidsAction(GameState, Actor);
      ui.SetPopup(new Popup("How much?", "", -1, -1));
      var acc = new NumericAccumulator(ui, "How much?");
      if (Actor is Player player)
      {
        player.ReplacePendingAction(dropMoney, acc);
        return new ActionResult() { Complete = false, EnergyCost = 0.0 };
      }
      else
        // Will monsters ever just decide to drop money?
        return new ActionResult() { Complete = true };
    }

    var (item, itemCount) = Actor!.Inventory.ItemAt(Choice);
    if (item.Equiped && item.Type == ItemType.Armour)
    {
      var msg = new Message("You cannot drop something you're wearing.", GameState.Player.Loc);
      return new ActionResult() { Complete = false, Messages = [msg] };
    }
    else if (itemCount > 1)
    {
      var dropStackAction = new DropStackAction(GameState, Actor, Choice);
      var prompt = $"Drop how many {item.FullName.Pluralize()}?\n(enter for all)";
      ui.SetPopup(new Popup(prompt, "", -1, -1));
      var acc = new NumericAccumulator(ui, prompt);
      if (Actor is Player player)
      {
        player.ReplacePendingAction(dropStackAction, acc);
        return new ActionResult() { Complete = false, EnergyCost = 0.0 };
      }
      else
        // When monsters can drop stuff I guess I'll have to handle that here??
        return new ActionResult() { Complete = true };
    }
    else
    {
      Actor.Inventory.Remove(Choice, 1);
      GameState.ItemDropped(item, Actor.Loc);
      item.Equiped = false;
      Actor.CalcEquipmentModifiers();

      var alert = MsgFactory.Phrase(Actor.ID, Verb.Drop, item.ID, 1, false, Actor.Loc, GameState);
      ui.AlertPlayer([alert], "", GameState);

      return new ActionResult() { Complete = true, EnergyCost = 1.0 };
    }
  }

  public override void ReceiveAccResult(AccumulatorResult result)
  {
    var menuResult = (MenuAccumulatorResult)result;
    Choice = menuResult.Choice;
  }
}

class ToggleEquipedAction(GameState gs, Actor actor) : Action(gs, actor)
{
  public char Choice { get; set; }
  
  public override ActionResult Execute()
  {
    ActionResult result;
    var (item, _) = Actor!.Inventory.ItemAt(Choice);
    GameState!.ClearMenu();

    bool equipable = item is null || item.Type switch
    {
      ItemType.Armour => true,
      ItemType.Weapon => true,
      ItemType.Tool => true,
      ItemType.Bow => true,
      _ => false
    };

    if (!equipable)
    {
      var msg = new Message("You cannot equip that!", GameState.Player.Loc);
      return new ActionResult() { Complete = false, Messages = [msg] };
    }

    var (equipResult, conflict) = ((Player)Actor).Inventory.ToggleEquipStatus(Choice);
    Message alert;
    switch (equipResult)
    {
      case EquipingResult.Equiped:
        alert = MsgFactory.Phrase(Actor.ID, Verb.Ready, item.ID, 1, false, Actor.Loc, GameState);
        result = new ActionResult() { Complete = true, Messages = [alert], EnergyCost = 1.0 };
        break;
      case EquipingResult.Unequiped:
        alert = MsgFactory.Phrase(Actor.ID, Verb.Unready, item.ID, 1, false, Actor.Loc, GameState);
        result = new ActionResult() { Complete = true, Messages = [alert], EnergyCost = 1.0 };
        break;
      default:
        string msg = "You are already wearing ";
        if (conflict == ArmourParts.Hat)
          msg += "a helmet.";
        else if (conflict == ArmourParts.Shirt)
          msg += "some armour.";
        alert = new Message(msg, GameState.Player.Loc);
        result = new ActionResult() { Complete = true, Messages = [alert] };
        break;
    }

    Actor.CalcEquipmentModifiers();

    return result;
  }

  public override void ReceiveAccResult(AccumulatorResult result)
  {
    var menuResult = (MenuAccumulatorResult)result;
    Choice = menuResult.Choice;
  }
}

sealed class PassAction : Action
{
  public PassAction() { }
  public PassAction(GameState? gs, Actor? actor)
  {
    GameState = gs;
    Actor = actor;
  }

  public sealed override ActionResult Execute()
  {
    base.Execute();

    return new ActionResult() { Complete = true, EnergyCost = 1.0 };
  }      
}

class CloseMenuAction : Action
{
  readonly double _energyCost;

  public CloseMenuAction(GameState gs, double energyCost = 0.0)
  {
    GameState = gs;
    _energyCost = energyCost;
  }

  public override ActionResult Execute()
  {
    GameState!.ClearMenu();
    return new ActionResult() { Complete = true, EnergyCost = _energyCost };
  }
}

// I guess I can later add extra info about whether or not the player died, quit,
// or quit and saved?
class QuitAction : Action
{
  public override ActionResult Execute() => throw new GameQuitException();
}

class SaveGameAction : Action
{
  public override ActionResult Execute() => throw new Exception("Shouldn't actually try to execute a Save Game action!");
}

class NullAction : Action
{
  public override ActionResult Execute() => throw new Exception("Hmm this should never happen");
}