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

class ActionResult
{
  public bool Complete { get; set; } = false;
  public List<string> Messages { get; set; } = [];
  public Action? AltAction { get; set; }
  public double EnergyCost { get; set; } = 0.0;
  
  public ActionResult() { }
}

abstract class Action
{
  public Actor? Actor { get; set; }
  public GameState? GameState { get; set; }
  public string Quip { get; set; } = "";
  public string Message { get; set; } = "";
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

    ActionResult result = new();
    if (Message is not null)
      result.Messages.Add(Message);

    return result;
  }

  public virtual void ReceiveUIResult(UIResult result) { }
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
    {
      result = Battle.MeleeAttack(Actor!, target, GameState);
    }
    else
    {
      result.EnergyCost = 1.0;
      var msg = $"{Actor!.FullName.Capitalize()} {Grammar.Conjugate(Actor, "swing")} wildly!";
      result.Messages.Add(msg);
    }

    return result;
  }
}

// This is a different class from MissileAttackAction because it will take the result the 
// aim selection. It also handles the animation and following the path of the arrow
class ArrowShotAction(GameState gs, Actor actor, Item? bow, Item ammo, int attackBonus) : TargetedAction(gs, actor)
{
  readonly Item _ammo = ammo;
  readonly int _attackBonus = attackBonus;

  public override ActionResult Execute()
  {
    var result = base.Execute();
    var trajectory = Trajectory();
    List<Loc> pts = [];
    bool creatureTargeted = false;
    bool targetHit = false;
    for (int j = 0; j < trajectory.Count; j++)
    {
      var pt = trajectory[j];
      Tile tile = GameState!.TileAt(pt);
      if (GameState.ObjDb.Occupant(pt) is Actor occ && occ != Actor)
      {
        pts.Add(pt);
        ActionResult attackResult = Battle.MissileAttack(Actor!, occ, GameState, _ammo, _attackBonus, new ArrowAnimation(GameState!, pts, _ammo.Glyph.Lit));
        creatureTargeted = true;
        targetHit = attackResult.Complete;

        result.Messages.AddRange(attackResult.Messages);
        result.EnergyCost = attackResult.EnergyCost;
        if (attackResult.Complete)
        {
          pts = [];
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

    if (pts.Count > 0)
    {
      var anim = new ArrowAnimation(GameState!, pts, _ammo.Glyph.Lit);
      GameState!.UIRef().PlayAnimation(anim, GameState);
    }
   
    if (creatureTargeted && !targetHit && Actor is Player player && bow is Item && bow.HasTrait<BowTrait>())
    {
      player.ExerciseStat(Attribute.BowUse);
    }

    return result;
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
        result = Battle.MissileAttack(Actor!, target, GameState, _ammo, _attackBonus, null);

      return result;
    }
    else
    {
      throw new Exception("Null location passed to MissileAttackAction. Why would you do that?");
    }
  }

  public override void ReceiveUIResult(UIResult result) => _loc = ((LocUIResult)result).Loc;
}

class ApplyTraitAction(GameState gs, Actor actor, TemporaryTrait trait) : Action(gs, actor)
{
  readonly TemporaryTrait _trait = trait;

  public override ActionResult Execute()
  {
    ActionResult result = base.Execute();
    result.EnergyCost = 1.0;

    if (Actor is not null)
    {
      List<string> msgs = _trait.Apply(Actor, GameState!);
      if (msgs.Count > 0)
      {
        result.Messages.AddRange(msgs);
      }
    }
    
    return result;
  }
}

class ShriekAction(GameState gs, Mob actor, int radius) : Action(gs, actor)
{
  int Radius { get; set; } = radius;

  public override ActionResult Execute()
  {
    ActionResult result = base.Execute();
    result.EnergyCost = 1.0;

    string msg;
    if (GameState!.LastPlayerFoV.Contains(Actor!.Loc))
      msg = $"{Actor.FullName.Capitalize()} lets out a piercing shriek!";
    else
      msg = "You hear a piercing shriek!";
    result.Messages.Add(msg);

    for (int r = Actor.Loc.Row - Radius; r < Actor.Loc.Row + Radius; r++)
    {
      for (int c = Actor.Loc.Col - Radius; c < Actor.Loc.Col + Radius; c++)
      {
        if (!GameState.CurrentMap.InBounds(r, c))
          continue;
        Loc loc = Actor.Loc with { Row = r, Col = c };
        if (GameState.ObjDb.Occupant(loc) is Mob mob)
        {
          Stat attittude = mob.Stats[Attribute.MobAttitude];
          if (attittude.Curr != Mob.AFRAID)
            mob.Stats[Attribute.MobAttitude].SetMax(Mob.AGGRESSIVE);
          Console.WriteLine($"{mob.FullName.Capitalize()} wakes up");
          mob.Traits = mob.Traits.Where(t => t is not SleepingTrait).ToList();
        }
      }
    }

    return result;
  }
} 

class AoEAction(GameState gs, Actor actor, Loc target, string effectTemplate, int radius, string txt) : Action(gs, actor)
{
  Loc Target { get; set; } = target;
  string EffectTemplate { get; set; } = effectTemplate;
  public int Radius { get; set; } = radius;
  string EffectText { get; set; } = txt;

  public override ActionResult Execute()
  {
    var result = base.Execute();
    result.Messages.Add(EffectText);

    var affected = GameState!.Flood(Target, Radius);
    foreach (var loc in affected)
    {
      // Ugh at the moment I can't handle things like a fireball
      // hitting an area and damaging items via this :/
      if (GameState.ObjDb.Occupant(loc) is Actor occ)
      {
        var effect = (TemporaryTrait) TraitFactory.FromText(EffectTemplate, occ);
        effect.Apply(occ, GameState);
      }
    }

    return result;
  }
}

class BashAction(GameState gs, Actor actor) : Action(gs, actor)
{
  Loc Target { get; set; }

  static bool CheckForInjury(TileType type) => type switch
  {
    TileType.ClosedDoor => true,
    TileType.LockedDoor => true,
    TileType.WoodWall => true,
    TileType.PermWall => true,
    TileType.StoneWall => true,
    TileType.DungeonWall => true,
    TileType.GreenTree => true,
    TileType.OrangeTree => true,
    TileType.YellowTree => true,
    TileType.RedTree => true,
    _ => false
  };

  public override ActionResult Execute()
  {
    var result = base.Execute();
    var gs = GameState!;

    // I'll probably want to do a knock-back kind of thing?
    if (gs.ObjDb.Occupied(Target))
    {
      result.Messages.Add("There's someone in your way!");
      return result;
    }

    Tile tile = gs.TileAt(Target);
    if (tile.Type == TileType.ClosedDoor || tile.Type == TileType.LockedDoor)
    {
      result.Messages.Add("Bam!");
      result.EnergyCost = 1.0;
      result.Complete = false;

      int dc = 14 + gs.CurrLevel/4;
      int roll = gs.Rng.Next(1, 21) + Actor!.Stats[Attribute.Strength].Curr;

      if (roll >= dc)
      {
        result.Messages.Add("You smash open the door!");
        gs.CurrentMap.SetTile(Target.Row, Target.Col, TileFactory.Get(TileType.BrokenDoor));
      }
      else
      {
        result.Messages.Add("The door holds firm!");
      }

      gs.Noise(Target.Row, Target.Col, 5);
    }

    // I should impose a small chance of penalty/injury so that spamming
    // bashing is a little risky
    if (CheckForInjury(tile.Type) && gs.Rng.Next(4) == 0) {
      var lame = new LameTrait()
      {
        OwnerID = Actor!.ID,
        ExpiresOn = gs.Turn + (ulong) gs.Rng.Next(100, 151)
      };

      List<string> msgs = lame.Apply(Actor!, gs);
      if (msgs.Count > 0)
        result.Messages.AddRange(msgs);
      else
      {        
        result.Messages.Add($"You injure your leg kicking {Tile.TileDesc(tile.Type)}!");
      }
    }

    return result;
  }

  public override void ReceiveUIResult(UIResult result)
  {
    var dirResult = (DirectionUIResult)result;
    var actorLoc = Actor!.Loc;
    Target = actorLoc with { Row = actorLoc.Row + dirResult.Row, 
                            Col = actorLoc.Col + dirResult.Col };
  }
}

class DisarmAction(GameState gs, Actor actor, Loc loc) : Action(gs, actor)
{
  Loc Origin { get; set; } = loc;

  public override ActionResult Execute()
  {
    ActionResult result = base.Execute();
    result.EnergyCost = 1.0;
    result.Complete = true;

    Map map = GameState!.CurrentMap;
    int trapCount = 0;
    foreach (Loc loc in Util.LocsInRadius(Origin, 3, map.Height, map.Width))
    {
      Tile tile = GameState.TileAt(loc);
      if (tile.IsTrap())
      {
        map.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.DungeonFloor));
        ++trapCount;
        if (GameState.LastPlayerFoV.Contains(loc))
        {
          SqAnimation anim = new(GameState, loc, Colours.WHITE, Colours.FADED_PURPLE, '^');
          GameState.UIRef().RegisterAnimation(anim);
          result.Messages.Add("A trap is destroyed!");
        }
        else
        {
          result.Messages.Add("You hear crunching and tinkling of machinery being destroyed.");
        }
      }
    }

    if (trapCount == 0)
      result.Messages.Add("The spell doesn't seem to do anything at all.");

    return result;
  }
}

// Action for when an actor jumps into a river or chasm (and eventually lava?)
class DiveAction(GameState gs, Actor actor, Loc loc, bool voluntary) : Action(gs, actor)
{
  Loc Loc { get; set; } = loc;
  bool Voluntary { get; set; } = voluntary;

  void PlungeIntoWater(Actor actor, GameState gs, ActionResult result)
  {
    if (actor is Player && Voluntary)
      result.Messages.Add("You plunge into the water!");
    else if (actor is Player)
      result.Messages.Add("You stumble and fall into some water!");
    else if (gs.LastPlayerFoV.Contains(Loc))
      result.Messages.Add($"{actor.FullName.Capitalize()} {Grammar.Conjugate(actor, "plunge")} into the water!");
    else
      result.Messages.Add("You hear a splash!");
    
    string msg = gs.FallIntoWater(actor, Loc);
    if (msg.Length > 0)
    {
      result.Messages.Add(msg);
    }
  }

  void PlungeIntoChasm(Actor actor, GameState gs, ActionResult result)
  {
    if (actor is Player && Voluntary)
      result.Messages.Add("You leap into the darkness!");
    else if (actor is Player)
      result.Messages.Add("There's no floor beneath your feet!");
    else if (gs.LastPlayerFoV.Contains(loc))
      result.Messages.Add($"{actor.FullName.Capitalize()} {Grammar.Conjugate(actor, "fall")} into the darkness!");
    
    var landingSpot = new Loc(Loc.DungeonID, Loc.Level + 1, Loc.Row, Loc.Col);

    gs.FallIntoChasm(actor, landingSpot);    
  }

  public override ActionResult Execute()
  {
    var result = base.Execute();
    result.EnergyCost = 1.0;

    var tile = GameState!.TileAt(Loc);
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
}

abstract class PortalAction : Action
{  
  public PortalAction(GameState gameState) => GameState = gameState;

  protected void UsePortal(Portal portal, ActionResult result)
  {
    var start = GameState!.Player!.Loc;        
    var (dungeon, level, _, _) = portal.Destination;
    
    GameState.PlayerEntersLevel(GameState.Player!, dungeon, level);
    GameState.Player!.Loc = portal.Destination;
    string moveMsg = GameState.ResolveActorMove(GameState.Player!, start, portal.Destination);    
    result.Messages.Add(moveMsg);

    GameState.RefreshPerformers();
    GameState.UpdateFoV();

    if (start.DungeonID != portal.Destination.DungeonID)
      result.Messages.Add(GameState.CurrentDungeon.ArrivalMessage);

    result.Complete = true;
    result.EnergyCost = 1.0;
  }
}

class DownstairsAction(GameState gameState) : PortalAction(gameState)
{
  public override ActionResult Execute()
  {
    var result = new ActionResult() { Complete = false };

    var p = GameState!.Player!;
    var t = GameState.CurrentMap.TileAt(p.Loc.Row, p.Loc.Col);

    if (t.Type == TileType.Downstairs || t.Type == TileType.Portal || t.Type == TileType.ShortcutDown)
    {
      UsePortal((Portal)t, result);
    }
    else
    {
      result.Messages.Add("You cannot go down here.");
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
    else if (t is Shortcut shortcut)
    {
      // We want to turn the square on the surface into a portal back down.
      // (The idea is that by using the shortcut the player opens the
      // portcullis on the surface)
      ShortcutDown portal = new()
      {
        Destination = p.Loc
      };
      GameState.Campaign.Dungeons[0].LevelMaps[0].SetTile(shortcut.Destination.Row, shortcut.Destination.Col, portal);

      UsePortal((Portal)t, result);
      result.Messages.Add("You climb a long stairway out of the dungeon.");
      GameState.UIRef().SetPopup(new Popup("You climb a long stairway out of the dungeon.", "", -1, -1));
    }
    else
    {
      result.Messages.Add("You cannot go up here.");
    }

    return result;
  }
}

class UpgradeItemAction : Action
{
  char ItemSlot {  get; set; }
  char ReagentSlot { get; set; }
  readonly Mob _shopkeeper;
  int Total { get; set; }

  public UpgradeItemAction(GameState gs, Mob shopkeeper)
  {
    GameState = gs;
    _shopkeeper = shopkeeper;
  }

  public override ActionResult Execute()
  {
    ActionResult result = base.Execute();
    result.Complete = Total > 0;
    result.EnergyCost = 1.0;

    var (item, _) = GameState!.Player.Inventory.ItemAt(ItemSlot);
    var (reagent, _) = GameState.Player.Inventory.ItemAt(ReagentSlot);

    if (item is null || reagent is null)
      throw new Exception("Hmm this shouldn't happen when upgrading an item!");

    bool canUpgrade = Alchemy.Compatible(item, reagent);
    if (canUpgrade)
    {
      GameState.Player.Inventory.Zorkmids -= Total;

      var (success, msg) = Alchemy.UpgradeItem(item, reagent);

      GameState.Player.Inventory.RemoveByID(reagent.ID);

      GameState.UIRef().SetPopup(new Popup(msg, "", -1, -1));
      result.Messages.Add(msg);
    }
    else
    {
      string txt = $"Hmm I can't figure out a way to enchant your {item!.Name} with {reagent!.Name.IndefArticle()}.";
      GameState.UIRef().SetPopup(new Popup(txt, "", -1, -1));
    }

    return result;
  }

  public override void ReceiveUIResult(UIResult result)
  {
    var upgradeResult = (UpgradeItemUIResult)result;
    Total = upgradeResult.Zorkminds;
    ItemSlot = upgradeResult.ItemSlot;
    ReagentSlot = upgradeResult.ReagentSLot;
  }
}

// This is the action for paying an NPC to repair an item
class RepairItemAction : Action
{
  readonly Mob _shopkeeper;
  int Total { get; set; }
  HashSet<ulong> ToRepair { get; set; } = [];

  public RepairItemAction(GameState gs, Mob shopkeeper)
  {
    GameState = gs;
    _shopkeeper = shopkeeper;
  }

  public override ActionResult Execute()
  {
    ActionResult result = base.Execute();
    result.Complete = Total > 0;
    result.EnergyCost = 1.0;

    GameState!.Player.Inventory.Zorkmids -= Total;

    List<Item> items = [];
    foreach (Item item in GameState.Player.Inventory.Items())
    {
      if (ToRepair.Contains(item.ID))
      {
        EffectApplier.RemoveRust(item);
        items.Add(item);

        // Removing it and adding it back in will unstack an item where needed.
        // Ie., you have a stack of rusted daggers but only reapir one.
        if (item.HasTrait<StackableTrait>())
        {
          GameState.Player.Inventory.RemoveByID(item.ID);
          GameState.Player.Inventory.Add(item, GameState.Player.ID);
        }        
      }
    }
    
    string txt = $"{_shopkeeper.FullName.Capitalize()} gets to work and soon your ";
    if (items.Count > 1)
      txt += "items look almost as good as new!";
    else
      txt += items[0].Name + " looks almost as good as new!";

    GameState.UIRef().SetPopup(new Popup(txt, "", -1, -1));
    result.Messages.Add(txt);

    return result;
  }

  public override void ReceiveUIResult(UIResult result)
  {
    var repairResult = (RepairItemUIResult)result;
    Total = repairResult.Zorkminds;
    ToRepair = new HashSet<ulong>(repairResult.ItemIds);
  }
}

class PriestServiceAction : Action
{
  readonly Mob _priest;
  int Invoice { get; set; } = 0;
  string Service { get; set; } = "";

  public PriestServiceAction(GameState gs, Mob priest)
  {
    GameState = gs;
    _priest = priest;
  }

  public override ActionResult Execute()
  {
    ActionResult result = base.Execute();
    result.Complete = true;
    result.EnergyCost = 1.0;

    if (Service == "Absolution")
    {
      GameState!.Player.Inventory.Zorkmids -= Invoice;

      string s = $"{_priest.FullName.Capitalize()} accepts your donation, chants a prayer while splashing you with holy water.";
      s += "\n\nYou feel cleansed.";

      result.Messages.Add("You feel cleansed.");
      GameState.UIRef().SetPopup(new Popup(s, "", -1, -1));

      GameState.Player.Traits = GameState.Player.Traits.Where(t => t is not ShunnedTrait).ToList();
    }

    return result;
  }

  public override void ReceiveUIResult(UIResult result)
  {
    var serviceResult = (PriestServiceUIResult) result;
    Invoice = serviceResult.Zorkminds;
    Service = serviceResult.Service;
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

    result.Messages.Add(txt);

    return result;
  }

  public override void ReceiveUIResult(UIResult result)
  {
    var shopResult = (ShoppingUIResult) result;
    _invoice = shopResult.Zorkminds;
    _selections = shopResult.Selections;
  }
}

abstract class DirectionalAction(GameState gs, Actor actor) : Action(gs, actor)
{
  public Loc Loc { get; set; }

  public override void ReceiveUIResult(UIResult result)
  {
    var dirResult = (DirectionUIResult)result;
    Loc = Actor!.Loc with { Row = Actor.Loc.Row + dirResult.Row, Col = Actor.Loc.Col + dirResult.Col };
  }
}

class ChatAction(GameState gs, Actor actor) : DirectionalAction(gs, actor)
{  
  public override ActionResult Execute()
  {
    var result = new ActionResult() { Complete = false };

    var other = GameState!.ObjDb.Occupant(Loc);

    if (other is null)
    {
      result.Messages.Add("There's no one there!");
    }
    else
    {
      var (chatAction, acc) = other.Behaviour.Chat((Mob)other, GameState);

      if (chatAction is NullAction)
      {
        string s = $"{other.FullName.Capitalize()} turns away from you.";
        result.Messages.Add(s);
        result.Complete = true;
        result.EnergyCost = 1.0;
        GameState.UIRef().SetPopup(new Popup(s, "", -1, -1));
        return result;
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

  public CloseDoorAction(GameState gs, Actor actor, Map map) : base(gs, actor)
  {
    GameState = gs;
    _map = map;
  }

  public override ActionResult Execute()
  {
    var result = new ActionResult() { Complete = false, EnergyCost = 0.0 };
    var door = _map.TileAt(Loc.Row, Loc.Col);

    if (door is Door d)
    {
      var gs = GameState!;
      if (gs.ObjDb.Occupied(Loc))
      {
        result.Messages.Add("There is someone in the way.");
        return result;
      }
      if (gs.ObjDb.ItemsAt(Loc).Count > 0)
      {
        result.Messages.Add("There is something in the way.");
        return result;
      }

      if (d.Open)
      {
        d.Open = false;
        result.Complete = true;
        result.EnergyCost = 1.0;
        result.Messages.Add(MsgFactory.DoorMessage(Actor!, Loc, Verb.Close, GameState!));
      }
      else if (Actor is Player)
      {
        result.Messages.Add("The door is already closed.");
      }
    }
    else if (Actor is Player)
    {
      string s = door.Type == TileType.BrokenDoor ? "The door is broken!" : "There's no door there!";
      result.Messages.Add(s);
    }

    return result;
  }
}

class OpenDoorAction : DirectionalAction
{
  readonly Map _map;

  public OpenDoorAction(GameState gs, Actor actor, Map map) : base(gs, actor)
  {
    _map = map;
    GameState = gs;
  }

  public OpenDoorAction(GameState gs, Actor actor, Map map, Loc loc) : base(gs, actor)
  {
    _map = map;
    Loc = loc;
    GameState = gs;
  }

  public override ActionResult Execute()
  {
    var result = new ActionResult() { Complete = false };
    var door = _map.TileAt(Loc.Row, Loc.Col);

    if (door is Door d)
    {
      if (d.Type == TileType.LockedDoor)
      {
        result.Complete = true;
        result.EnergyCost = 1.0;

        result.Messages.Add("The door is locked!");
      }
      else if (!d.Open)
      {
        d.Open = true;
        result.Complete = true;
        result.EnergyCost = 1.0;
        result.Messages.Add(MsgFactory.DoorMessage(Actor!, Loc, Verb.Open, GameState!));
      }
      else if (Actor is Player)
      {
        result.Messages.Add("The door is already open.");
      }
    }
    else if (door is VaultDoor vd)
    {
      string msg = vd.Open ? "The doors stand open." : "You'll need a key!";
      result.Messages.Add(msg);
    }
    else if (Actor is Player)
    {
      result.Messages.Add("There's no door there!");
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
        Messages = [ "There's no room in your inventory!" ]
      };
    }

    if (item.HasTrait<AffixedTrait>())
    {
      return new ActionResult() { EnergyCost = 0.0, Complete = false, Messages = ["You cannot pick that up!"] };
    }

    // First, is there anything preventing the actor from picking items up
    // off the floor? (At the moment it's just webs in the game, but a 
    // Sword-in-the-Stone situation might be neat)
    foreach (var env in GameState.ObjDb.EnvironmentsAt(Actor.Loc))
    {
      var web = env.Traits.OfType<StickyTrait>().FirstOrDefault();
      if (web is not null)
      {
        bool strCheck = Actor.AbilityCheck(Attribute.Strength, web.DC, GameState.Rng);
        if (!strCheck)
        {
          var txt = $"{item.FullName.DefArticle().Capitalize()} {MsgFactory.CalcVerb(item, Verb.Etre)} stuck to {env.Name.DefArticle()}!";
          return new ActionResult() { EnergyCost = 1.0, Complete = false, Messages = [txt] };
        }
        else
        {
          var txt = $"{Actor.FullName.Capitalize()} {MsgFactory.CalcVerb(Actor, Verb.Tear)} {item.FullName.DefArticle()} from {env.Name.DefArticle()}.";
          result.Messages.Add(txt);
        }
      }
    }

    char slot = '\0';
    int count = 0;
    if (item.HasTrait<StackableTrait>())
    {
      foreach (var pickedUp in itemStack.Where(i => i == item))
      {
        GameState.ObjDb.RemoveItemFromLoc(Actor.Loc, pickedUp);
        slot = inv.Add(pickedUp, Actor.ID);
        ++count;
      }
    }
    else
    {
      GameState.ObjDb.RemoveItemFromLoc(Actor.Loc, item);
      slot = inv.Add(item, Actor.ID);
    }

    var pickupMsg = $"{Actor.FullName.Capitalize()} {Grammar.Conjugate(Actor, "pick")} up ";
    if (item.Type == ItemType.Zorkmid && item.Value == 1)
      pickupMsg += "a zorkmid.";
    else if (item.Type == ItemType.Zorkmid)
      pickupMsg += $"{item.Value} zorkmids.";
    else if (count > 1)
      pickupMsg += $"{count} {item.FullName.Pluralize()}.";
    else if (item.HasTrait<NamedTrait>())
      pickupMsg += item.FullName;
    else
      pickupMsg += item.FullName.DefArticle() + ".";

    if (slot != '\0')
      pickupMsg += $" ({slot})";
    
    if (item.Traits.OfType<OwnedTrait>().FirstOrDefault() is OwnedTrait ownedTrait)
    {
      List<string> msgs = GameState.OwnedItemPickedUp(ownedTrait.OwnerIDs, Actor, item.ID);
      result.Messages.AddRange(msgs);
    }

    // Clear the 'in a pit' flag when an item is picked up
    if (item.HasTrait<InPitTrait>())
    {
      item.Traits = item.Traits.Where(t => t is not InPitTrait).ToList();
    }

    result.Messages.Add(pickupMsg);

    return result;
  }

  public override void ReceiveUIResult(UIResult result) => ItemID = ((ObjIdUIResult)result).ID;
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
        var summoned = MonsterFactory.Get(_summons, GameState!.ObjDb, GameState.Rng);
        GameState.ObjDb.AddNewActor(summoned, loc);
        GameState.AddPerformer(summoned);
        ++summonCount;
      }
    }

    List<string> msgs = [];
    if (GameState!.LastPlayerFoV.Contains(Actor!.Loc))
    {
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

      msgs.Add(txt);
    }

    return new ActionResult() { Complete = true, Messages = msgs, EnergyCost = 1.0 };
  }
}

class SearchAction(GameState gs, Actor player) : Action(gs, player)
{
  public override ActionResult Execute()
  {
    var result = base.Execute();
    result.Complete = true;
    result.EnergyCost = 1.0;

    GameState gs = GameState!;    
    Loc playerLoc = Actor!.Loc;
    List<Loc> sqsToSearch = gs.LastPlayerFoV
                              .Where(loc => Util.Distance(playerLoc, loc) <= 3).ToList();

    bool rogue = gs.Player.Background == PlayerBackground.Skullduggery;
    int dc;
    foreach (Loc loc in sqsToSearch)
    {
      // I'm not going to roll to find secret items. I'm not sure I should
      // even bother for traps/secret doors
      foreach (Item item in gs.ObjDb.ItemsAt(loc))
      {
        if (item.HasTrait<HiddenTrait>())
        {
          item.Traits = item.Traits.Where(t => t is not HiddenTrait).ToList();
          result.Messages.Add($"You find {item.FullName.IndefArticle()}!");
        }        
      }

      Tile tile = gs.TileAt(loc);
      switch (tile.Type)
      {
        case TileType.SecretDoor:
          dc = 10 + gs.CurrLevel + 1;
          if (rogue)
            dc -= 2;
          dc = int.Min(dc, 20);
          if (gs.Rng.Next(1, 21) <= dc) 
          {
            result.Messages.Add("You spot a secret door!");
            gs.CurrentMap.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.ClosedDoor));
          }
          break;
        case TileType.HiddenPit:
        case TileType.HiddenTrapDoor:
        case TileType.HiddenTeleportTrap:
        case TileType.HiddenDartTrap:
          dc = 15 + gs.CurrLevel + 1;
          if (rogue)
            dc -= 2;
          dc = int.Min(dc, 20);
          if (gs.Rng.Next(1, 21) <= dc)
          {
            TileType replacementTile = tile.Type switch 
            {
              TileType.HiddenPit => TileType.Pit,
              TileType.HiddenTeleportTrap => TileType.TeleportTrap,
              TileType.HiddenDartTrap => TileType.DartTrap,
              _ => TileType.TrapDoor
            };
            result.Messages.Add("You spot a trap!");
            gs.CurrentMap.SetTile(loc.Row, loc.Col, TileFactory.Get(replacementTile));
          }          
          break;
        case TileType.HiddenMagicMouth:
          dc = 10 + gs.CurrLevel + 1;
          if (rogue)
            dc -= 2;
          dc = int.Min(dc, 20);
          if (gs.Rng.Next(1, 21) <= dc)
          {
            result.Messages.Add("You spot a magic mouth!");
            gs.CurrentMap.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.MagicMouth));
          }
          break;
        case TileType.JetTrigger:
          JetTrigger jt = (JetTrigger)tile;
          if (!jt.Visible)
          {
            dc = 15 + gs.CurrLevel + 1;
            if (rogue)
              dc -= 2;
            dc = int.Min(dc, 20);
            if (gs.Rng.Next(1, 21) <= dc)
            {
              jt.Visible = true;
              result.Messages.Add("You spot a loose flagstone!");
            }
          }
          break;
        case TileType.GateTrigger:
          GateTrigger gt = (GateTrigger)tile;
          if (!gt.Found)
          {
            dc = 12 + gs.CurrLevel + 1;
            if (rogue)
              dc -= 2;
            dc = int.Min(dc, 20);
            if (gs.Rng.Next(1, 21) <= dc)
            {
              result.Messages.Add("You spot a pressure plate!");
              gt.Found = true;
            }
          }
          
          break;
        // Eventually I probably want to add searching for gargoyles, mimics, 
        // invisible monsters, etc
      }      
    }

    var anim = new MagicMapAnimation(gs, gs.CurrentDungeon, sqsToSearch, false)
    {
      Fast = true,
      Colour = Colours.SEARCH_HIGHLIGHT,
      AltColour = Colours.SEARCH_HIGHLIGHT
    };
    gs.UIRef().RegisterAnimation(anim);

    return result;
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
        result.Messages.Add("The wide world is big for the spell! The magic fizzles!");
      }
      else
      {
        result.Messages.Add("A vision of your surroundings fills your mind!");
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

  static Mob MakeDuplciate(GameState gs, Actor src)
  { 
    var glyph = new Glyph(src.Glyph.Ch, src.Glyph.Lit, src.Glyph.Unlit, src.Glyph.BGLit, src.Glyph.BGUnlit);
    
    // I originally implemented MirrorImage for cloakers, who can fly but I
    // think it makes sense for all mirror images since they're illusions that
    // may drift over water/lava 
    var dup = new Mob()
    {
      Name = src.Name,
      Glyph = glyph,
      Recovery = 1.0,
      MoveStrategy = new SimpleFlightMoveStrategy()
    };

    dup.Stats.Add(Attribute.HP, new Stat(1));
    dup.Stats.Add(Attribute.AC, new Stat(10));
    dup.Stats.Add(Attribute.MobAttitude, new Stat(Mob.AGGRESSIVE));

    dup.Traits.Add(new FlyingTrait());

    var illusion = new IllusionTrait()
    {
      SourceID = src.ID,
      ObjId = dup.ID
    };
    dup.Traits.Add(illusion);   
    gs.RegisterForEvent(GameEventType.Death, illusion, src.ID);

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
      return new ActionResult() { Complete = true, Messages = ["A spell fizzles..."], EnergyCost = 1.0 };
    }

    List<Mob> images = [];
    int duplicates = int.Min(options.Count, 4);
    while (duplicates > 0)
    {
      int i = GameState!.Rng.Next(options.Count);
      Loc loc = options[i];
      options.RemoveAt(i);

      var dup = MakeDuplciate(GameState, Actor!);
      GameState.ObjDb.AddNewActor(dup, loc);
      GameState.AddPerformer(dup);

      images.Add(dup);

      --duplicates;
    }

    // We've created the duplicates so now the caster swaps locations
    // with one of them
    Mob swap = images[GameState!.Rng.Next(images.Count)];
    GameState.SwapActors(Actor!, swap);

    var result = base.Execute();
    result.Complete = true;
    result.EnergyCost = 1.0;
    result.Messages.Add("How puzzling!");

    return result;
  }
}

class FogCloudAction : Action
{
  readonly Loc _target;

  public FogCloudAction(GameState gs, Actor caster, Loc target) : base(gs, caster)
  {
    GameState = gs;
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

    string txt = $"{Actor!.FullName.Capitalize()} {Grammar.Conjugate(Actor, "cast")} Fog Cloud!";
    
    return new ActionResult() { Complete = true, Messages = [ txt ], EnergyCost = 1.0 };
  }
}

class DrainTorchAction : Action
{
  readonly Loc _target;

  public DrainTorchAction(GameState gs, Actor caster, Loc target) : base(gs, caster)
  {
    _target = target;
  }

  public override ActionResult Execute()
  {
    ActionResult result = base.Execute();
    result.Complete = true;
    result.EnergyCost = 1.0;

    bool success = false;
    if (GameState!.ObjDb.Occupant(_target) is Actor victim)
    {
      foreach (var item in victim.Inventory.Items())
      {
        if (item.Traits.OfType<TorchTrait>().FirstOrDefault() is TorchTrait torch)
        {
          if (torch.Lit && torch.Fuel > 0)
          {
            int drain = GameState.Rng.Next(350, 751);
            torch.Fuel = int.Max(0, torch.Fuel - drain);
            string s = $"{Actor!.FullName.Capitalize()} {Grammar.Conjugate(Actor, "drain")}";
            result.Messages.Add($" {item.FullName.Possessive(victim)}!");
            success = true;
          }
        }
      }
    }
    
    if (!success)
    {
      result.Messages.Add("The spell fizzles.");
    }

    return result;
  }
}

class EntangleAction : Action
{
  readonly Loc _target;

  public EntangleAction(GameState gs, Actor caster, Loc target) : base(gs, caster)
  {
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
        Actor vines = MonsterFactory.Get("vines", GameState.ObjDb, GameState.Rng);
        vines.Loc = loc;
        GameState.ObjDb.Add(vines);
        GameState.ObjDb.AddToLoc(loc, vines);
      }
    }

    string txt = $"{Actor!.FullName.Capitalize()} {Grammar.Conjugate(Actor, "cast")} Entangle!";
    
    return new ActionResult() { Complete = true, Messages = [ txt ], EnergyCost = 1.0 };
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

    var firebolt = ItemFactory.Get(ItemNames.FIREBOLT, GameState!.ObjDb);
    var attack = new MissileAttackAction(GameState, Actor!, _target, firebolt, 0);

    string txt = $"{Actor!.FullName.Capitalize()} {Grammar.Conjugate(Actor, "cast")} Firebolt!";
    return new ActionResult() { Complete = true, Messages = [ txt ], AltAction = attack, EnergyCost = 0.0 };
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
    return new ActionResult() { Complete = true, Messages = [txt], EnergyCost = 1.0 };
  }
}

class WordOfRecallAction(GameState gs) : Action(gs, gs.Player)
{
  public override ActionResult Execute()
  {
    var result = base.Execute();

    var player = GameState!.Player;
    if (player.HasTrait<RecallTrait>() || player.Loc.DungeonID == 0)
    {
      result.Messages.Add("You shudder for a moment.");
      result.Complete = true;
      result.EnergyCost = 1.0;

      return result;
    }

    ulong happensOn = GameState.Turn + (ulong) GameState.Rng.Next(10, 21);
    var recall = new RecallTrait()
    {
      ExpiresOn = happensOn
    };

    GameState.RegisterForEvent(GameEventType.EndOfRound, recall);
    GameState.Player.Traits.Add(recall);

    result.Messages.Add("The air crackles around you.");
    result.Complete = true;
    result.EnergyCost = 1.0;

    return result;
  }
};

class KnockAction(GameState gs, Actor caster) : Action(gs, caster)
{  
  public override ActionResult Execute()
  {
    var result = base.Execute();

    if (Actor is Actor caster)
    {
      result.Messages.Add("You hear a spectral knocking.");
      result.EnergyCost = 1.0;

      var sqs = GameState!.Flood(caster.Loc, 4, true);
      foreach (Loc sq in sqs)
      {
        Tile tile = GameState.TileAt(sq);
        if (tile.Type == TileType.LockedDoor || tile.Type == TileType.SecretDoor)
        {
          result.Messages.Add("Click!");
          GameState.CurrentMap.SetTile(sq.Row, sq.Col, TileFactory.Get(TileType.ClosedDoor));
        }
      }

      var anim = new MagicMapAnimation(GameState, GameState.CurrentDungeon, [.. sqs]);
      GameState.UIRef().RegisterAnimation(anim);
    }
    
    return result;
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
      return new ActionResult() { Complete = true, Messages = ["A spell fizzles..."], EnergyCost = 1.0 };
    }
    else
    {
      // Teleporting removes the grapple trait and in-pit traits
      Actor.Traits = Actor.Traits.Where(t => t is not GrappledTrait && t is not InPitTrait).ToList();
        
      var landingSpot = sqs[GameState!.Rng.Next(sqs.Count)];
      var mv = new MoveAction(GameState, Actor, landingSpot);
      GameState.UIRef().RegisterAnimation(new SqAnimation(GameState, landingSpot, Colours.WHITE, Colours.LIGHT_PURPLE, '*'));
      GameState.UIRef().RegisterAnimation(new SqAnimation(GameState, start, Colours.WHITE, Colours.LIGHT_PURPLE, '*'));
      string msg = $"Bamf! {Actor.FullName.Capitalize()} {Grammar.Conjugate(Actor, "blink")} away!";

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
      return new ActionResult() { Complete = true, Messages = ["That tasted not bad."], EnergyCost = 1.0 };
    }

    foreach (var t in Actor!.Traits.OfType<PoisonedTrait>())
    {
      GameState!.StopListening(GameEventType.EndOfRound, t);
    }
    Actor.Traits = Actor.Traits.Where(t => t is not PoisonedTrait).ToList();
    string msg = $"That makes {Actor.FullName} {MsgFactory.CalcVerb(Actor, Verb.Feel)} better.";

    return new ActionResult() { Complete = true, Messages = [msg], EnergyCost = 1.0 };
  }
}

class HealAction(GameState gs, Actor target, int healDie, int healDice) : Action(gs, target)
{
  readonly int _healDie = healDie;
  readonly int _healDice = healDice;

  public override ActionResult Execute()
  {
    ActionResult result = base.Execute();

    Stat hpStat = Actor!.Stats[Attribute.HP];
    int hpBefore = hpStat.Curr;

    int hp = 0;
    for (int j = 0; j < _healDice; j++)
      hp += GameState!.Rng.Next(_healDie) + 1;
    hpStat.Change(hp);
    int delta = hpStat.Curr - hpBefore;

    string txt;
    if (delta > 0)
    {
      txt = $"{Actor.FullName.Capitalize()} {Grammar.Conjugate(Actor, "is")} healed for {delta} HP.";
    }
    else
    {
      txt = "";
    }

    var healAnim = new SqAnimation(GameState!, Actor.Loc, Colours.WHITE, Colours.PURPLE, '\u2665');
    GameState!.UIRef().RegisterAnimation(healAnim);

    result.Messages.Add(txt);
    result.Complete = true;
    result.EnergyCost = 1.0;

    return result;
  }
}

class DropZorkmidsAction(GameState gs, Actor actor) : Action(gs, actor)
{
  int _amount;

  public override ActionResult Execute()
  {
    double cost = 1.0;
    bool successful = true;
    string msg;
    List<string> msgs = [];

    var inventory = Actor!.Inventory;
    if (_amount > inventory.Zorkmids)
    {
      _amount = inventory.Zorkmids;
    }

    if (_amount == 0)
    {
      cost = 0.0; // we won't make the player spend an action if they drop nothing
      successful = false;
      msgs.Add("You hold onto your zorkmids.");
    }
    else
    {      
      var coins = ItemFactory.Get(ItemNames.ZORKMIDS, GameState!.ObjDb);
      GameState.ItemDropped(coins, Actor.Loc);
      coins.Value = _amount;
      msg = $"{MsgFactory.CalcName(Actor, GameState.Player).Capitalize()} {MsgFactory.CalcVerb(Actor, Verb.Drop)} ";
      if (_amount == 1)
        msg += "a single zorkmid.";
      else if (_amount == inventory.Zorkmids)
        msg += "all your money!";
      else
        msg += $"{_amount} zorkmids.";
      msgs.Add(msg);

      if (Actor is Player && GameState.TileAt(Actor.Loc).Type == TileType.Well && coins.Value == 1)
      {
        msgs.Add("The coin disappears into the well and you hear a faint plop.");
        GameState.ObjDb.RemoveItemFromGame(Actor.Loc, coins);

        if (GameState.Rng.Next(100) < 5 && !Actor.HasTrait<AuraOfProtectionTrait>())
        {
          msgs.Add("A warm glow surrounds you!");
          Actor.Traits.Add(new AuraOfProtectionTrait());
        }
      }

      inventory.Zorkmids -= _amount;
    }

    return new ActionResult() { Complete = successful, Messages = msgs, EnergyCost = cost };
  }
  
  public override void ReceiveUIResult(UIResult result) => _amount = ((NumericUIResult)result).Amount;
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

    string alert = MsgFactory.Phrase(Actor.ID, Verb.Drop, item.ID, _amount, false, GameState);

    return new ActionResult() { Complete = true, Messages = [alert], EnergyCost = 1.0 };
  }

  public override void ReceiveUIResult(UIResult result) => _amount = ((NumericUIResult)result).Amount;
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
    GameState.ItemDropped(ammo, landingPt);
    ammo.Equiped = false;
    
    var tile = GameState.TileAt(landingPt);
    if (tile.Type == TileType.Chasm)
    {
      result.Messages.Add($"{ammo.FullName.DefArticle().Capitalize()} tumbles into the darkness.");
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
          var attackResult = Battle.MissileAttack(Actor, occ, GameState, ammo, 0, null);
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

  public override void ReceiveUIResult(UIResult result) => _target = ((LocUIResult)result).Loc;
}

class FireSelectedBowAction(GameState gs, Player player) : Action(gs, player)
{
  public char Choice { get; set; }

  public override ActionResult Execute()
  {
    var result = base.Execute();

    GameState!.ClearMenu();

    var player = Actor as Player;

    var (item, _) = player!.Inventory.ItemAt(Choice);
    if (item is null || item.Type != ItemType.Bow)
    {
      result.Messages.Add("That doesn't make any sense!");
    }
    else
    {
      player.FireReadedBow(item, GameState);
      
      result.EnergyCost = 0.0;
      result.Complete = false;
    }

    return result;
  }

  public override void ReceiveUIResult(UIResult result) => Choice = ((MenuUIResult)result).Choice;
}

class ThrowSelectionAction(GameState gs, Player player) : Action(gs, player)
{
  public char Choice { get; set; }
  
  public override ActionResult Execute()
  {
    GameState!.ClearMenu();
    var player = Actor as Player;

    var (item, _) = player!.Inventory.ItemAt(Choice);
    if (item is null)
    {
      var result = new ActionResult() { Complete = false, EnergyCost = 0.0 };
      result.Messages.Add("That doesn't make sense");
      return result;
    }
    else if (item.Type == ItemType.Armour && item.Equiped)
    {
      var result = new ActionResult() { Complete = false, EnergyCost = 0.0 };
      result.Messages.Add("You're wearing that!");
      return result;
    }

    var action = new ThrowAction(GameState, player, Choice);
    var range = 7 + player.Stats[Attribute.Strength].Curr;
    if (range < 2)
      range = 2;
    var acc = new Aimer(GameState, player.Loc, range);
    player.ReplacePendingAction(action, acc);

    return new ActionResult() { Complete = false, EnergyCost = 0.0 };
  }

  public override void ReceiveUIResult(UIResult result) => Choice = ((MenuUIResult)result).Choice;
}

class DropItemAction(GameState gs, Actor actor) : Action(gs, actor)
{
  public char Choice { get; set; }
  
  public override ActionResult Execute()
  {
    GameState!.ClearMenu();
    UserInterface ui = GameState.UIRef();

    if (Choice == '$')
    {
      var inventory = Actor!.Inventory;
      if (inventory.Zorkmids == 0)
      {
        return new ActionResult() { Complete = false, Messages = ["You have no money!"] };
      }
      var dropMoney = new DropZorkmidsAction(GameState, Actor);
      ui.SetPopup(new Popup("How much?", "", -1, -1));
      var acc = new NumericInputer(ui, "How much?");
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
      return new ActionResult() { Complete = false, Messages = ["You cannot drop something you're wearing."] };
    }
    if (item.Equiped && item.Type == ItemType.Ring)
    {
      return new ActionResult() { Complete = false, Messages = ["You'll need to take it off first."] };
    }
    else if (itemCount > 1)
    {
      var dropStackAction = new DropStackAction(GameState, Actor, Choice);
      var prompt = $"Drop how many {item.FullName.Pluralize()}?\n(enter for all)";
      ui.SetPopup(new Popup(prompt, "", -1, -1));
      var acc = new NumericInputer(ui, prompt);
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
      string alert = MsgFactory.Phrase(Actor.ID, Verb.Drop, item.ID, 1, false, GameState);
      ui.AlertPlayer(alert);

      Actor.Inventory.Remove(Choice, 1);
      GameState.ItemDropped(item, Actor.Loc);
      item.Equiped = false;

      return new ActionResult() { Complete = true, EnergyCost = 1.0 };
    }
  }

  public override void ReceiveUIResult(UIResult result) => Choice = ((MenuUIResult)result).Choice;
}

class ApplyPoisonAction(GameState gs, Actor actor) : Action(gs, actor)
{
  public char Choice { get; set; }

  public override ActionResult Execute()
  {
    ActionResult result = base.Execute();

    GameState!.ClearMenu();

    var (item, _) = Actor!.Inventory.ItemAt(Choice);

    if (item != null)
    {      
      item.Traits.Add(new PoisonCoatedTrait());
      item.Traits.Add(new AdjectiveTrait("poisoned"));
      item.Traits.Add(new PoisonerTrait() { DC = 15, Strength = 1, Duration = 10 });

      string name = Actor.FullName.Capitalize();
      string verb = Grammar.Conjugate(Actor, "smear");
      string objName = item.FullName.DefArticle();
      string s = $"{name} {verb} some poison on {objName}.";

      result.Messages.Add(s);
    }

    result.Complete = true;
    result.EnergyCost = 1.0;

    return result;
  }

  public override void ReceiveUIResult(UIResult result) => Choice = ((MenuUIResult)result).Choice;
}

class IdentifyItemAction(GameState gs, Actor actor) : Action(gs, actor)
{
  public char Choice { get; set; }

  public override ActionResult Execute()
  {
    ActionResult result = base.Execute();

    GameState!.ClearMenu();
        
    var (item, _) = Actor!.Inventory.ItemAt(Choice);

    if (Item.IDInfo.TryGetValue(item.Name, out var idInfo))
    {
      Item.IDInfo[item.Name] = idInfo with { Known = true };
    }

    string s = $"\n It's {item.FullName.IndefArticle()}! \n";
    GameState.UIRef().SetPopup(new Popup(s, "", -1, -1));

    result.Complete = true;
    result.EnergyCost = 1.0;

    string m = $"{Actor.FullName.Capitalize()} {Grammar.Conjugate(Actor, "identify")} {item.FullName.DefArticle()}.";
    result.Messages.Add(m);

    return result;
  }

  public override void ReceiveUIResult(UIResult result) => Choice = ((MenuUIResult)result).Choice;
}

class ToggleEquipedAction(GameState gs, Actor actor) : Action(gs, actor)
{
  public char Choice { get; set; }
  
  public override ActionResult Execute()
  {
    ActionResult result;
    var (item, _) = Actor!.Inventory.ItemAt(Choice);
    GameState!.ClearMenu();

    if (item is null)
    {
      return new ActionResult() { Complete = false, Messages = ["You cannot equip that!"] };
    }

    bool equipable = item.Type switch
    {
      ItemType.Armour => true,
      ItemType.Weapon => true,
      ItemType.Tool => true,
      ItemType.Bow => true,
      ItemType.Ring => true,
      ItemType.Talisman => true,
      _ => false
    };

    if (!equipable)
    {
      return new ActionResult() { Complete = false, Messages = ["You cannot equip that!"] };
    }

    var (equipResult, conflict) = ((Player)Actor).Inventory.ToggleEquipStatus(Choice);
    string alert;
    switch (equipResult)
    {
      case EquipingResult.Equiped:
        alert = MsgFactory.Phrase(Actor.ID, Verb.Ready, item.ID, 1, false, GameState);
        result = new ActionResult() { Complete = true, Messages = [alert], EnergyCost = 1.0 };

        if (item.HasTrait<CursedTrait>())
        {
          if (item.Type == ItemType.Ring)
            result.Messages.Add("The ring tightens around your finger!");
        }
        break;
      case EquipingResult.Cursed:
        alert = "You cannot remove it! It seems to be cursed!";
        result = new ActionResult() { Messages = [alert], Complete = true, EnergyCost = 1.0 };
        break;
      case EquipingResult.Unequiped:
        alert = MsgFactory.Phrase(Actor.ID, Verb.Unready, item.ID, 1, false, GameState);
        result = new ActionResult() { Complete = true, Messages = [alert], EnergyCost = 1.0 };
        break;
      case EquipingResult.TwoHandedConflict:
        result = new ActionResult() { Complete = true, Messages = ["You cannot wear a shield with a two-handed weapon!"], EnergyCost = 0.0 };
        break;
      case EquipingResult.ShieldConflict:
        result = new ActionResult() { Complete = true, Messages = ["You cannot use a two-handed weapon with a shield!"], EnergyCost = 0.0 };
        break;
      case EquipingResult.TooManyRings:
        result = new ActionResult() { Complete = true, Messages = ["You are already wearing two rings!"], EnergyCost = 0.0 };
        break;
      case EquipingResult.TooManyTalismans:
        result = new ActionResult() { Complete = true, Messages = ["You many only use two talismans!"], EnergyCost = 0.0 };
        break;
      default:
        string msg = "You are already wearing ";
        msg += conflict switch
        {
          ArmourParts.Hat => "a helmet.",
          ArmourParts.Shield => "a shield.",
          ArmourParts.Boots => "boots.",
          ArmourParts.Cloak => "a cloak.",
          _ => "some armour."
        };
        result = new ActionResult() { Complete = true, Messages = [msg] };
        break;
    }

    if (item.Traits.OfType<GrantsTrait>().FirstOrDefault() is GrantsTrait grants)
    {
      if (Item.IDInfo.TryGetValue(item.Name, out ItemIDInfo? value))
        Item.IDInfo[item.Name] = value with { Known = true };

      if (equipResult == EquipingResult.Equiped)
      {
        result.Messages.AddRange(grants.Grant(Actor, GameState, item));
      }
      else if (equipResult == EquipingResult.Unequiped)
      {
        grants.Remove(Actor, GameState, item);
      }
    }

    return result;
  }

  public override void ReceiveUIResult(UIResult result) => Choice = ((MenuUIResult)result).Choice;
}

class FireballAction(GameState gs, Actor actor, Trait src) : TargetedAction(gs, actor)
{
  readonly Trait _source = src;
  
  public override ActionResult Execute()
  {
    ActionResult result = base.Execute();
    result.EnergyCost = 1.0;
    result.Complete = true;

    // Fireball shoots toward the target and then explodes, but its path 
    // may be interrupted
    List<Loc> pts = [];
    Loc actualLoc = Target;
    foreach (var pt in Trajectory())
    {      
      var tile = GameState!.TileAt(pt);
      if (!(tile.Passable() || tile.PassableByFlight()))
        break;

      actualLoc = pt;
      pts.Add(pt);

      if (GameState.ObjDb.Occupant(pt) is Actor occ && occ != Actor)
        break;      
    }

    var ui = GameState!.UIRef();

    var anim = new ArrowAnimation(GameState!, pts, Colours.BRIGHT_RED);
    ui.PlayAnimation(anim, GameState);
    
    var affected = GameState!.Flood(actualLoc, 3);
    affected.Add(actualLoc);

    var explosion = new ExplosionAnimation(GameState!)
    {
      MainColour = Colours.BRIGHT_RED,
      AltColour1 = Colours.YELLOW,
      AltColour2 = Colours.YELLOW_ORANGE,
      Highlight = Colours.WHITE,
      Centre = actualLoc,
      Sqs = affected
    };
    ui.PlayAnimation(explosion, GameState);
    
    int total = 0;
    for (int j = 0; j < 4; j++)
      total += GameState.Rng.Next(6) + 1;
    List<(int, DamageType)> dmg = [(total, DamageType.Fire)];
    foreach (var pt in affected)
    {
      GameState.ApplyDamageEffectToLoc(pt, DamageType.Fire);
      if (GameState.ObjDb.Occupant(pt) is Actor victim)
      {
        result.Messages.Add($"{victim.FullName.Capitalize()} {Grammar.Conjugate(victim, "is")} caught in the flames!");

        var (hpLeft, dmgMsg) = victim.ReceiveDmg(dmg, 0, GameState, null, 1.0);
        if (hpLeft < 1)
        {
          GameState.ActorKilled(victim, "a fireball", result, null);
        }        
      }
    }

    if (_source is WandTrait wand)
    {
      Item.IDInfo["wand of fireballs"] = Item.IDInfo["wand of fireballs"] with { Known = true };
      wand.Used();
    }
    else if (_source is IUSeable useable)
    {
      useable.Used();
    }

    return result;
  }
}

class FrostRayAction(GameState gs, Actor actor, Trait src) : TargetedAction(gs, actor)
{
  readonly Trait _source = src;

  public override ActionResult Execute()
  {
    var result = base.Execute();
    result.EnergyCost = 1.0;
    result.Complete = true;

    Item ray = new()
    {
      Name = "ray of frost",
      Type = ItemType.Weapon,
      Glyph = new Glyph('*', Colours.LIGHT_BLUE, Colours.BLUE, Colours.BLACK, Colours.BLACK)
    };
    ray.Traits.Add(new DamageTrait() { DamageDie = 4, NumOfDie = 3, DamageType = DamageType.Cold });
    GameState!.ObjDb.Add(ray);

    // Ray of frost is a beam so unlike things like magic missle, it doesn't stop 
    // when it hits an occupant.
    List<Loc> pts = [];
    foreach (var pt in Trajectory())
    {
      var tile = GameState!.TileAt(pt);
      if (!(tile.Passable() || tile.PassableByFlight()))
        break;
      pts.Add(pt);
    }

    var anim = new BeamAnimation(GameState!, pts, Colours.LIGHT_BLUE, Colours.WHITE);
    GameState!.UIRef().PlayAnimation(anim, GameState);

    foreach (var pt in pts)
    {
      GameState.ApplyDamageEffectToLoc(pt, DamageType.Cold);

      if (GameState.ObjDb.Occupant(pt) is Actor occ && occ != Actor)
      {
        int attackMod = 3;
        var attackResult = Battle.MagicAttack(Actor!, occ, GameState, ray, attackMod, null);
        result.Messages.AddRange(attackResult.Messages);        
      }
    }

    if (_source is WandTrait wand)
    {
      Item.IDInfo["wand of frost"] = Item.IDInfo["wand of frost"] with { Known = true };
      wand.Used();
    }
    else if (_source is IUSeable useable)
    {
      useable.Used();
    }

    return result;
  }
}

class MagicMissleAction(GameState gs, Actor actor, Trait src) : TargetedAction(gs, actor)
{
  readonly Trait _source = src;
  
  public override ActionResult Execute()
  {
    ActionResult result = base.Execute();
    result.EnergyCost = 1.0;
    result.Complete = true;

    Item missile = new()
    {
      Name = "magic missile",
      Type = ItemType.Weapon,
      Glyph = new Glyph('-', Colours.YELLOW_ORANGE, Colours.YELLOW_ORANGE, Colours.BLACK, Colours.BLACK)
    };
    missile.Traits.Add(new DamageTrait() { DamageDie = 6, NumOfDie = 2, DamageType = DamageType.Force });
    GameState!.ObjDb.Add(missile);

    List<Loc> pts = [];
    foreach (var pt in Trajectory())
    {
      var tile = GameState!.TileAt(pt);
      if (GameState.ObjDb.Occupant(pt) is Actor occ && occ != Actor)
      {
        pts.Add(pt);

        // I didn't want magic missile to be auto-hit like in D&D, but I'll give it a nice
        // attack bonus
        int attackMod = 5;
        var attackResult = Battle.MagicAttack(Actor!, occ, GameState, missile, attackMod, new ArrowAnimation(GameState!, pts, Colours.YELLOW_ORANGE));
        result.Messages.AddRange(attackResult.Messages);
        if (attackResult.Complete)
        {
          pts = [];
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
   
    if (_source is WandTrait wand)
    {
      Item.IDInfo["wand of magic missiles"] = Item.IDInfo["wand of magic missiles"] with { Known = true };
      wand.Used();
    }
    else if (_source is IUSeable useable) 
    {
      useable.Used();
    }

    var anim = new ArrowAnimation(GameState!, pts, Colours.YELLOW_ORANGE);
    GameState!.UIRef().PlayAnimation(anim, GameState);

    result.Messages.Add("Pew pew pew!");

    return result;
  }
}

abstract class TargetedAction(GameState gs, Actor actor) : Action(gs, actor)
{
  protected Loc Target { get; set; }

  protected List<Loc> Trajectory()
  {
    return Util.Bresenham(Actor!.Loc.Row, Actor.Loc.Col, Target.Row, Target.Col)
               .Select(p => new Loc(Actor.Loc.DungeonID, Actor.Loc.Level, p.Item1, p.Item2))
               .ToList();
  }

  public override void ReceiveUIResult(UIResult result) => Target = ((LocUIResult)result).Loc;
}

class SwapWithMobAction(GameState gs, Actor actor, Trait src) : Action(gs, actor)
{
  readonly Trait _source = src;
  Loc _target;

  public override ActionResult Execute()
  {
    ActionResult result = base.Execute();
    result.EnergyCost = 1.0;
    result.Complete = true;

    if (GameState!.ObjDb.Occupant(_target) is Actor victim)
    {
      if (_source is WandTrait wand)
      {
        Item.IDInfo["wand of swap"] = Item.IDInfo["wand of swap"] with { Known = true };
        wand.Used();
      }

      if (Actor!.ID == victim.ID)
      {        
        var txt = $"{Actor.FullName.Capitalize()} {Grammar.Conjugate(Actor, "feel")} a sense of vertigo followed by existential dread.";
        result.Messages.Add(txt);

        var confused = new ConfusedTrait() { DC = 15 };
        confused.Apply(Actor, GameState);
      }
      else
      {
        GameState.SwapActors(Actor!, victim);
        result.Messages.Add("Bamf!");
      }      
    }
    else
    {
      if (_source is IUSeable useable)
        useable.Used();
      result.Messages.Add("The magic is realised but nothing happens. The spell fizzles.");
    }

    return result;
  }

  public override void ReceiveUIResult(UIResult result) => _target = ((LocUIResult)result).Loc;
}

class CastHealMonster(GameState gs, Actor actor, Trait src) : Action(gs, actor)
{
  readonly Trait _source = src;
  Loc _target;

  public override ActionResult Execute()
  {
    ActionResult result = base.Execute();
    result.EnergyCost = 1.0;
    result.Complete = true;


    if (GameState!.ObjDb.Occupant(_target) is Actor target)
    {
      if (target is Player)
      {
        result.Messages.Add("The magic is realised but nothing happens. The spell fizzles.");
      }
      else
      {
        var healAction = new HealAction(GameState, target, 6, 2);
        result.AltAction = healAction;
        result.EnergyCost = 0.0;
        result.Complete = false;
      }

      if (_source is WandTrait wand)
      {
        Item.IDInfo["wand of heal monster"] = Item.IDInfo["wand of heal monster"] with { Known = true };
        wand.Used();
      }
    }
    else
    {
      result.Messages.Add("The magic is realised but nothing happens. The spell fizzles.");
    }

    if (_source is IUSeable useable)
      useable.Used();

    return result;
  }

  public override void ReceiveUIResult(UIResult result) => _target = ((LocUIResult)result).Loc;
}

class InventoryChoiceAction(GameState gs, Actor actor, InventoryOptions opts, Action replacementAction) : Action(gs, actor)
{
  InventoryOptions InvOptions = opts;
  Action ReplacementAction { get; set; } = replacementAction;

  public override ActionResult Execute()
  {
    ActionResult result = base.Execute();

    if (Actor is Player player)
    {
      char[] slots = player.Inventory.UsedSlots();
      player.Inventory.ShowMenu(GameState!.UIRef(), InvOptions);
      Inputer inputer = new Inventorier([.. slots]);
      player.ReplacePendingAction(ReplacementAction, inputer);
    }

    return result;
  }
}

class UseWandAction(GameState gs, Actor actor, WandTrait wand) : Action(gs, actor)
{
  readonly WandTrait _wand = wand;

  public override ActionResult Execute()
  {
    ActionResult result = new()
    {
      Complete = false,
      EnergyCost = 0.0
    };

    if (Actor is not Player player)
      throw new Exception("Boy did something sure go wrong!");

    Inputer inputer;
    switch (_wand.Effect)
    {
      case "magicmissile":
        inputer = new Aimer(GameState!, player.Loc, 7);
        player.ReplacePendingAction(new MagicMissleAction(GameState!, player, _wand), inputer);
        break;
      case "fireball":
        inputer = new Aimer(GameState!, player.Loc, 12);
        player.ReplacePendingAction(new FireballAction(GameState!, player, _wand), inputer);
        break;
      case "swap":
        inputer = new Aimer(GameState!, player.Loc, 25);
        player.ReplacePendingAction(new SwapWithMobAction(GameState!, player, _wand), inputer);
        break;
      case "healmonster":
        inputer = new Aimer(GameState!, player.Loc, 7);
        player.ReplacePendingAction(new CastHealMonster(GameState!, player, _wand), inputer);
        break;
      case "frost":
        inputer = new Aimer(GameState!, player.Loc, 7);
        player.ReplacePendingAction(new FrostRayAction(GameState!, player, _wand), inputer);
        result.Messages.Add("Which way?");
        break;
    }
    
    return result;
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