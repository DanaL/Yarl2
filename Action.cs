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

using System.Xml.Linq;

namespace Yarl2;

class ActionResult
{
  public bool Succcessful { get; set; } = false;
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

  public virtual void ReceiveUIResult(UIResult result) { }
}

class MeleeAttackAction(GameState gs, Actor actor, Loc target) : Action(gs, actor)
{
  Loc Target { get; set; } = target;

  public override ActionResult Execute()
  {
    ActionResult result = base.Execute();
    result.Succcessful = true;

    if (GameState!.ObjDb.Occupant(Target) is Actor target)
    {
      result = Battle.MeleeAttack(Actor!, target, GameState);
    }
    else
    {
      result.EnergyCost = 1.0;
      GameState.UIRef().AlertPlayer($"{Actor!.FullName.Capitalize()} {Grammar.Conjugate(Actor, "swing")} wildly!");      
    }

    return result;
  }
}

class GulpAction(GameState gs, Actor actor, int dc, int dmgDie, int numOfDice) : Action(gs, actor)
{
  int DC { get; set; } = dc;
  int AcidDie { get; set; } = dmgDie;
  int AcidDice { get; set; } = numOfDice;

  public override ActionResult Execute()
  {
    var result = base.Execute();
    result.Succcessful = true;
    result.EnergyCost = 1.0;

    UserInterface ui = GameState!.UIRef();
    Loc targetLoc = Actor!.PickTargetLoc(GameState!);
    if (GameState.ObjDb.Occupant(targetLoc) is not Actor victim)
      return result;

    string s = $"{Actor!.FullName.Capitalize()} {Grammar.Conjugate(Actor, "bite")} {victim.FullName}!";
    ui.AlertPlayer(s);
    
    if (!victim.AbilityCheck(Attribute.Dexterity, DC, GameState.Rng))
    {
      SwallowedTrait st = new()
      {
        VictimID = victim.ID,
        SwallowerID = Actor.ID,
        Origin = victim.Loc
      };
      GameState.RegisterForEvent(GameEventType.Death, st, Actor.ID);
      victim.Traits.Add(st);

      FullBellyTrait fbt = new()
      {
        VictimID = victim.ID,
        AcidDie = AcidDie,
        AcidDice = AcidDice
      };
      Actor.Traits.Add(fbt);
      GameState.RegisterForEvent(GameEventType.EndOfRound, fbt);

      // This is where the player will 'enter' the pocket dimension representing
      // the interior of the monster's belly.
      var (entry, belly) = PocketDimension.MonsterBelly(Actor, GameState);
      GameState.Campaign.AddDungeon(belly, belly.ID);

      Loc start = victim.Loc;
      GameState.ActorEntersLevel(victim, belly.ID, 0);      
      GameState.ResolveActorMove(victim, start, entry);
      victim.Loc = entry;

      GameState.RefreshPerformers();
      GameState.PrepareFieldOfView();

      if (belly.ArrivalMessage != "")
        ui.AlertPlayer(belly.ArrivalMessage);
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
    var trajectory = Trajectory(Actor!.Loc, false);
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
        targetHit = attackResult.Succcessful;

        result.EnergyCost = attackResult.EnergyCost;
        if (attackResult.Succcessful)
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

class MissileAttackAction(GameState gs, Actor actor, Loc loc, Item ammo) : Action(gs, actor)
{
  Loc _loc = loc;
  readonly Item _ammo = ammo;
  
  public override ActionResult Execute()
  {
    ActionResult result = new() { Succcessful = true };
    ArrowAnimation arrowAnim = new(GameState!, Util.Trajectory(Actor!.Loc, _loc), _ammo.Glyph.Lit);
    GameState!.UIRef().RegisterAnimation(arrowAnim);

    if (GameState!.ObjDb.Occupant(_loc) is Actor target)
    {
      if (Actor is not Player)
      {
        string s = $"{MsgFactory.CalcName(Actor, GameState.Player).Capitalize()} shoots at ";
        s+= $"{MsgFactory.CalcName(target, GameState.Player)}!";
        GameState.UIRef().AlertPlayer(s);
      }

      result = Battle.MissileAttack(Actor!, target, GameState, _ammo, 0, null);
    }

    return result;
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
      UserInterface ui = GameState!.UIRef();
      foreach (string s in msgs)
        ui.AlertPlayer(s);
    }
    
    return result;
  }
}

class ShriekAction(GameState gs, Actor actor, int radius) : Action(gs, actor)
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
    GameState.UIRef().AlertPlayer(msg);

    for (int r = Actor.Loc.Row - Radius; r < Actor.Loc.Row + Radius; r++)
    {
      for (int c = Actor.Loc.Col - Radius; c < Actor.Loc.Col + Radius; c++)
      {
        if (!GameState.CurrentMap.InBounds(r, c))
          continue;
        Loc loc = Actor.Loc with { Row = r, Col = c };
        if (GameState.ObjDb.Occupant(loc) is Mob mob && mob.Stats.ContainsKey(Attribute.MobAttitude))
        {
          Stat attittude = mob.Stats[Attribute.MobAttitude];
          if (attittude.Curr != Mob.AFRAID)
            mob.Stats[Attribute.MobAttitude].SetMax(Mob.AGGRESSIVE);
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
    ActionResult result = base.Execute();
    result.EnergyCost = 1.0;
    result.Succcessful = true;

    GameState!.UIRef().AlertPlayer(EffectText);
    
    var affected = GameState.Flood(Target, Radius);
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

class RumBreathAction(GameState gs, Actor actor, Loc target, int range) : Action(gs, actor)
{
  Loc Target { get; set; } = target;
  int Range { get; set; } = range;

  public override ActionResult Execute()
  {
    ActionResult result = base.Execute();
    result.Succcessful = true;
    result.EnergyCost = 1.0;

    if (GameState!.LastPlayerFoV.Contains(Actor!.Loc))
    {
      string s = $"{Actor.FullName.Capitalize()} {Grammar.Conjugate(Actor, "spew")} a gout of alcohol!";
      GameState!.UIRef().AlertPlayer(s);
    }

    // Actor targets a specific loc, but the cone of the breath weapon extends
    // its full range.
    var (fullR, fullC) = Util.ExtendLine(Actor.Loc.Row, Actor.Loc.Col, Target.Row, Target.Col, Range);
    Loc actualTarget = Target with { Row = fullR, Col = fullC };
    List<Loc> affected = ConeCalculator.Affected(Range, Actor.Loc, actualTarget, GameState.CurrentMap, GameState.ObjDb);
    affected.Insert(0, Actor.Loc);
    var explosion = new ExplosionAnimation(GameState!)
    {
      MainColour = Colours.LIGHT_BROWN,
      AltColour1 = Colours.BROWN,
      AltColour2 = Colours.YELLOW_ORANGE,
      Highlight = Colours.WHITE,
      Centre = Actor.Loc,
      Sqs = [ ..affected ],
      Ch = '#'
    };
    GameState.UIRef().PlayAnimation(explosion, GameState);

    affected.Remove(Actor.Loc);

    UserInterface ui = GameState.UIRef();
    foreach (var pt in affected)
    {
      if (GameState.ObjDb.Occupant(pt) is Actor victim)
      {
        foreach (string s in Battle.HandleTipsy(victim, GameState))
          ui.AlertPlayer(s);
      }
    }

    return result;
  }
}

// I'm sure as I add more breath weapons I'll make this more generic, or extract
// a subclass
class FireBreathAction(GameState gs, Actor actor, Loc target, int range, int dmgDie, int dmgDice) : Action(gs, actor)
{
  Loc Target { get; set; } = target;
  int Range { get; set; } = range;
  int DmgDie { get; set; } = dmgDie;
  int DmgDice { get; set; } = dmgDice;

  public override ActionResult Execute()
  {
    UserInterface ui = GameState!.UIRef();
    ActionResult result = base.Execute();
    result.Succcessful = true;
    result.EnergyCost = 1.0;

    if (GameState.LastPlayerFoV.Contains(Actor!.Loc))
    {
      string s = $"{Actor.FullName.Capitalize()} {Grammar.Conjugate(Actor, "breath")} a gout of flame!";
      ui.AlertPlayer(s);
    }

    // Actor targets a specific loc, but the cone of the breath weapon extends
    // its full range.
    var (fullR, fullC) = Util.ExtendLine(Actor.Loc.Row, Actor.Loc.Col, Target.Row, Target.Col, Range);
    Loc actualTarget = Target with { Row = fullR, Col = fullC };
    List<Loc> affected = ConeCalculator.Affected(Range, Actor.Loc, actualTarget, GameState.CurrentMap, GameState.ObjDb);
    affected.Insert(0, Actor.Loc);
    var explosion = new ExplosionAnimation(GameState!)
    {
      MainColour = Colours.BRIGHT_RED,
      AltColour1 = Colours.YELLOW,
      AltColour2 = Colours.YELLOW_ORANGE,
      Highlight = Colours.WHITE,
      Centre = Actor.Loc,
      Sqs = [ ..affected ],
      Ch = '\u22CF'
    };
    ui.PlayAnimation(explosion, GameState);

    affected.Remove(Actor.Loc);

    foreach (var pt in affected)
    {
      GameState.ApplyDamageEffectToLoc(pt, DamageType.Fire);
    }

    int total = 0;
    for (int j = 0; j < DmgDice; j++)
      total += GameState.Rng.Next(DmgDie) + 1;
    List<(int, DamageType)> dmg = [(total, DamageType.Fire)];    
    foreach (var pt in affected)
    {
      GameState.ApplyDamageEffectToLoc(pt, DamageType.Fire);
      if (GameState.ObjDb.Occupant(pt) is Actor victim)
      {
        ui.AlertPlayer($"{victim.FullName.Capitalize()} {Grammar.Conjugate(victim, "is")} caught in the flames!");

        var (hpLeft, dmgMsg, _) = victim.ReceiveDmg(dmg, 0, GameState, null, 1.0);
        if (dmgMsg != "")
          ui.AlertPlayer(dmgMsg);
        if (hpLeft < 1)
        {
          GameState.ActorKilled(victim, "fiery breath", null);
        }        
      }
    }

    return result;
  }
}

class BashAction(GameState gs, Actor actor) : Action(gs, actor)
{
  public Loc Target { get; set; }

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
    UserInterface ui = gs.UIRef();

    // I'll probably want to do a knock-back kind of thing?
    if (gs.ObjDb.Occupied(Target))
    {
      ui.AlertPlayer("There's someone in your way!");
      return result;
    }

    Tile tile = gs.TileAt(Target);
    if (tile.Type == TileType.ClosedDoor || tile.Type == TileType.LockedDoor)
    {
      ui.AlertPlayer("Bam!");
      result.EnergyCost = 1.0;
      result.Succcessful = false;

      int dc = 14 + gs.CurrLevel/4;
      int roll = gs.Rng.Next(1, 21) + Actor!.Stats[Attribute.Strength].Curr;

      if (roll >= dc)
      {
        ui.AlertPlayer("You smash open the door!");
        gs.CurrentMap.SetTile(Target.Row, Target.Col, TileFactory.Get(TileType.BrokenDoor));
      }
      else
      {
        ui.AlertPlayer("The door holds firm!");
      }

      gs.Noise(Target.Row, Target.Col, 5);
    }

    // I should impose a small chance of penalty/injury so that spamming
    // bashing is a little risky
    if (CheckForInjury(tile.Type) && gs.Rng.Next(4) == 0) 
    {
      LameTrait lame = new()
      {
        OwnerID = Actor!.ID,
        ExpiresOn = gs.Turn + (ulong) gs.Rng.Next(100, 151)
      };

      List<string> msgs = lame.Apply(Actor!, gs);
      if (msgs.Count > 0)
      {
        foreach (string s in msgs)
          ui.AlertPlayer(s);
      }        
      else
      {        
        ui.AlertPlayer($"You injure your leg kicking {Tile.TileDesc(tile.Type)}!");
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
    result.Succcessful = true;
    UserInterface ui = GameState!.UIRef();

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
          ui.RegisterAnimation(anim);
          ui.AlertPlayer("A trap is destroyed!");
        }
        else
        {
          ui.AlertPlayer("You hear crunching and tinkling of machinery being destroyed.");
        }
      }
    }

    if (trapCount == 0)
      ui.AlertPlayer("The spell doesn't seem to do anything at all.");
    
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
    UserInterface ui = gs.UIRef();
    if (actor is Player && Voluntary)
      ui.AlertPlayer("You plunge into the water!");
    else if (actor is Player)
      ui.AlertPlayer("You stumble and fall into some water!");
    else if (gs.LastPlayerFoV.Contains(Loc))
      ui.AlertPlayer($"{actor.FullName.Capitalize()} {Grammar.Conjugate(actor, "plunge")} into the water!");
    else
      ui.AlertPlayer("You hear a splash!");
    
    string msg = gs.FallIntoWater(actor, Loc);
    if (msg.Length > 0)
      ui.AlertPlayer(msg);
  }

  void PlungeIntoChasm(Actor actor, GameState gs, ActionResult result)
  {
    UserInterface ui = gs.UIRef();
    if (actor is Player && Voluntary)
      ui.AlertPlayer("You leap into the darkness!");
    else if (actor is Player)
      ui.AlertPlayer("There's no floor beneath your feet!");
    else if (gs.LastPlayerFoV.Contains(Loc))
      ui.AlertPlayer($"{actor.FullName.Capitalize()} {Grammar.Conjugate(actor, "fall")} into the darkness!");
    
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
    Player player = GameState!.Player;
    Loc start = player.Loc;        
    var (dungeon, level, _, _) = portal.Destination;
    
    bool trip = level > start.Level && GameState.Player.HasTrait<TipsyTrait>() && GameState.Rng.NextDouble() < 0.33;

    GameState.ActorEntersLevel(GameState.Player!, dungeon, level);
    GameState.Player!.Loc = portal.Destination;
    GameState.ResolveActorMove(GameState.Player!, start, portal.Destination);
    
    if (trip)
    {
      GameState.UIRef().AlertPlayer("You trip and fall down the stairs!");
      int dmg = GameState.Rng.Next(1, 5);
      List<(int, DamageType)> fallDmg = [(dmg, DamageType.Blunt)];
      var (hpLeft, dmgMsg, _) = GameState.Player.ReceiveDmg(fallDmg, 0, GameState, null, 1.0);
      GameState.UIRef().AlertPlayer(dmgMsg);
      if (hpLeft < 1)
      {
        GameState.ActorKilled(player, "drunken fall", null);
      }
    }

    GameState.RefreshPerformers();
    GameState.PrepareFieldOfView();

    if (start.DungeonID != portal.Destination.DungeonID)
      GameState.UIRef().AlertPlayer(GameState.CurrentDungeon.ArrivalMessage);
    
    result.Succcessful = true;
    result.EnergyCost = 1.0;
  }
}

class DownstairsAction(GameState gameState) : PortalAction(gameState)
{
  public override ActionResult Execute()
  {
    var result = new ActionResult() { Succcessful = false };

    var p = GameState!.Player!;
    var t = GameState.CurrentMap.TileAt(p.Loc.Row, p.Loc.Col);

    if (t.Type == TileType.Downstairs || t.Type == TileType.Portal || t.Type == TileType.ShortcutDown)
    {
      UsePortal((Portal)t, result);
    }
    else
    {
      GameState!.UIRef().AlertPlayer("You cannot go down here.");
    }

    return result;
  }
}

class UpstairsAction(GameState gameState) : PortalAction(gameState)
{
  public override ActionResult Execute()
  {
    var result = new ActionResult() { Succcessful = false };

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
      GameState.UIRef().AlertPlayer("You climb a long stairway out of the dungeon.");
      GameState.UIRef().SetPopup(new Popup("You climb a long stairway out of the dungeon.", "", -1, -1));
    }
    else
    {
      GameState.UIRef().AlertPlayer("You cannot go up here.");      
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
    result.Succcessful = Total > 0;
    result.EnergyCost = 1.0;

    var (item, _) = GameState!.Player.Inventory.ItemAt(ItemSlot);
    var (reagent, _) = GameState.Player.Inventory.ItemAt(ReagentSlot);

    if (item is null || reagent is null)
      throw new Exception("Hmm this shouldn't happen when upgrading an item!");

    bool canUpgrade = Alchemy.Compatible(item, reagent);
    if (canUpgrade)
    {
      GameState.Player.Inventory.Zorkmids -= Total;

      var (_, msg) = Alchemy.UpgradeItem(item, reagent);

      GameState.Player.Inventory.RemoveByID(reagent.ID);

      GameState.UIRef().SetPopup(new Popup(msg, "", -1, -1));
      foreach (string s in msg.Split('\n'))
        GameState.UIRef().AlertPlayer(s.Trim());      
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
    result.Succcessful = Total > 0;
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
    GameState.UIRef().AlertPlayer(txt);

    return result;
  }

  public override void ReceiveUIResult(UIResult result)
  {
    var repairResult = (RepairItemUIResult)result;
    Total = repairResult.Zorkminds;
    ToRepair = new HashSet<ulong>(repairResult.ItemIds);
  }
}

class InnkeeperServiceAction : Action
{
  readonly Mob _innkeeper;
  int Invoice { get; set; } = 0;
  string Service { get; set; } = "";

  public InnkeeperServiceAction(GameState gs, Mob innkeeper)
  {
    GameState = gs;
    _innkeeper = innkeeper;
  }

  public override ActionResult Execute()
  {
    ActionResult result = base.Execute();
    result.Succcessful = true;
    result.EnergyCost = 1.0;

    if (Service == "Booze")
    {
      GameState!.Player.Inventory.Zorkmids -= Invoice;
      Item booze = ItemFactory.Get(ItemNames.FLASK_OF_BOOZE, GameState.ObjDb);
      GameState.Player.AddToInventory(booze, GameState);
      GameState.UIRef().AlertPlayer($"You purchase a flask of booze from {_innkeeper.FullName}.");
    }
    else if (Service == "Rest")
    {
      GameState!.Player.Inventory.Zorkmids -= Invoice;
      GameState.Player.Stats[Attribute.HP].Reset();
      GameState.Player.Stats[Attribute.Nerve].Change(500);
      
      // Resting at an inn cures poison. It's part of room service.
      foreach (var t in GameState.Player.Traits.OfType<PoisonedTrait>())
      {
        GameState.StopListening(GameEventType.EndOfRound, t);
      }
      GameState.Player.Traits = GameState.Player.Traits.Where(t => t is not PoisonedTrait).ToList();

      // Rest for six hours
      RestingTrait rt = new() { ExpiresOn = GameState.Turn + 360 };
      rt.Apply(GameState.Player, GameState);
    }

    return result;
  }

  public override void ReceiveUIResult(UIResult result)
  {
    var serviceResult = (ServiceResult)result;
    Invoice = serviceResult.Zorkminds;
    Service = serviceResult.Service;
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
    result.Succcessful = true;
    result.EnergyCost = 1.0;

    if (Service == "Absolution")
    {
      GameState!.Player.Inventory.Zorkmids -= Invoice;

      string s = $"{_priest.FullName.Capitalize()} accepts your donation, chants a prayer while splashing you with holy water.";
      s += "\n\nYou feel cleansed.";

      GameState.UIRef().AlertPlayer("You feel cleansed.");
      GameState.UIRef().SetPopup(new Popup(s, "", -1, -1));

      GameState.Player.Traits = GameState.Player.Traits.Where(t => t is not ShunnedTrait).ToList();
    }

    return result;
  }

  public override void ReceiveUIResult(UIResult result)
  {
    var serviceResult = (ServiceResult) result;
    Invoice = serviceResult.Zorkminds;
    Service = serviceResult.Service;
  }
}

class WitchServiceAction(GameState gs, Mob witch) : Action(gs, witch)
{
  int Invoice { get; set; }
  string Service { get; set; } = "";

  public override ActionResult Execute()
  {
    ActionResult result = base.Execute();
    result.Succcessful = true;
    result.EnergyCost = 1.0;

    if (Service == "magic101")
    {
      Stat mp = new(GameState!.Rng.NextDouble() < 0.5 ? 1 : 2);
      GameState.Player.Stats.Add(Attribute.MagicPoints, mp);
      GameState.Player.SpellsKnown.Add("arcane spark");

      Item? crystal = null;
      bool hasFocus = false;
      foreach (Item item in GameState.Player.Inventory.Items())
      {
        if (item.Name == "meditation crystal")
          crystal = item;
        if (item.Type == ItemType.Wand || item.Name == "quarterstaff")
          hasFocus = true;
      }

      if (crystal is not null)
      {
        GameState.Player.Inventory.RemoveByID(crystal.ID);
        GameState.ObjDb.RemoveItemFromGame(Loc.Nowhere, crystal);
      }
      
      if (GameState.FactDb.FactCheck("KylieQuest") is SimpleFact fact)
      {
        fact.Value = "complete";
      }
      
      string s = $"{Actor!.FullName.Capitalize()} gives you a crash course in elementary magic! After some light magic theory and learning to focus through the meditation crystal, you become able to tap into your inner arcane power!";
      if (!hasFocus)
        s += "\n\nOh! You'll also need a magical focus to cast spells through. Any wand will do, or a good quarterstaff!";
      GameState.UIRef().SetPopup(new Popup(s, "", -1, -1));
      GameState.UIRef().AlertPlayer("You have mastered Magic 101.");
      GameState.UIRef().AlertPlayer("You have learned to cast arcane spark.");
    }
    else
    {
      GameState!.Player.Inventory.Zorkmids -= Invoice;
      GameState.Player.SpellsKnown.Add(Service);

      string s = $"{Actor!.FullName.Capitalize()} teaches you an arcane formula and you can now cast the [ICEBLUE {Service}] spell!";
      GameState.UIRef().SetPopup(new Popup(s, "", -1, -1));
      GameState.UIRef().AlertPlayer("You have learned a new spell.");
    }

    return result;
  }

  public override void ReceiveUIResult(UIResult result)
  {
    var serviceResult = (ServiceResult)result;
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
      Succcessful = _invoice > 0,
      EnergyCost = 1.0
    };

    GameState!.Player.Inventory.Zorkmids -= _invoice;

    string txt = $"You pay {_shopkeeper.FullName} {_invoice} zorkmid";
    if (_invoice > 1)
      txt += "s";
    txt += " and collect your goods.";
    GameState.UIRef().AlertPlayer(txt);

    bool overflow = false;
    foreach (var (slot, count) in _selections)
    {
      List<Item> bought = _shopkeeper.Inventory.Remove(slot, count);      
      foreach (Item item in bought)
      {
        char inventorySlot = GameState.Player.AddToInventory(item, GameState);
        if (inventorySlot == '\0')
          overflow = true;
      }
    }

    if (overflow)
    {
      GameState.UIRef().AlertPlayer("Uh-oh - you didn't have enough room in your inventory!");      
    }

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
    var result = new ActionResult() { Succcessful = false };

    var other = GameState!.ObjDb.Occupant(Loc);

    if (other is null)
    {
      GameState.UIRef().AlertPlayer("There's no one there!");      
    }
    else
    {
      var (chatAction, acc) = other.Behaviour.Chat((Mob)other, GameState);

      if (chatAction is NullAction)
      {
        string s = $"{other.FullName.Capitalize()} turns away from you.";
        GameState.UIRef().AlertPlayer(s);
        result.Succcessful = true;
        result.EnergyCost = 1.0;
        GameState.UIRef().SetPopup(new Popup(s, "", -1, -1));
        return result;
      }
      else
      {
        GameState.Player.ReplacePendingAction(chatAction, acc!);
      }

      return new ActionResult() { Succcessful = false, EnergyCost = 0.0 };
    }

    return result;
  }
}

class CloseDoorAction : DirectionalAction
{
  public CloseDoorAction(GameState gs, Actor actor) : base(gs, actor) => GameState = gs;
  public CloseDoorAction(GameState gs, Actor actor, Loc loc) : base(gs, actor)
  {
    Loc = loc;
    GameState = gs;
  }

  public override ActionResult Execute()
  {
    ActionResult result = new() { Succcessful = false, EnergyCost = 0.0 };
    UserInterface ui = GameState!.UIRef();
    Tile door = GameState!.CurrentMap.TileAt(Loc.Row, Loc.Col);

    if (door is Door d)
    {
      var gs = GameState!;
      if (gs.ObjDb.Occupied(Loc))
      {
        ui.AlertPlayer("There is someone in the way.");
        return result;
      }
      if (gs.ObjDb.ItemsAt(Loc).Count > 0)
      {
        ui.AlertPlayer("There is something in the way.");
        return result;
      }

      if (d.Open)
      {
        d.Open = false;
        result.Succcessful = true;
        result.EnergyCost = 1.0;
        ui.AlertPlayer(MsgFactory.DoorMessage(Actor!, Loc, "close", GameState!));
      }
      else if (Actor is Player)
      {
        ui.AlertPlayer("The door is already closed.");
      }
    }
    else if (Actor is Player)
    {
      string s = door.Type == TileType.BrokenDoor ? "The door is broken!" : "There's no door there!";
      ui.AlertPlayer(s);
    }

    return result;
  }
}

class OpenDoorAction : DirectionalAction
{
  public OpenDoorAction(GameState gs, Actor actor) : base(gs, actor) => GameState = gs;
  public OpenDoorAction(GameState gs, Actor actor, Loc loc) : base(gs, actor)
  {
    Loc = loc;
    GameState = gs;
  }

  public override ActionResult Execute()
  {
    ActionResult result = new() { Succcessful = false };
    Tile door = GameState!.CurrentMap.TileAt(Loc.Row, Loc.Col);
    UserInterface ui = GameState.UIRef();

    if (door is Door d)
    {
      if (d.Type == TileType.LockedDoor)
      {
        result.Succcessful = true;
        result.EnergyCost = 1.0;

        ui.AlertPlayer("The door is locked!");
      }
      else if (!d.Open)
      {
        d.Open = true;
        result.Succcessful = true;
        result.EnergyCost = 1.0;
        ui.AlertPlayer(MsgFactory.DoorMessage(Actor!, Loc, "open", GameState!));        
      }
      else if (Actor is Player)
      {
        ui.AlertPlayer("The door is already open.");
      }
    }
    else if (door is VaultDoor vd)
    {
      string msg = vd.Open ? "The doors stand open." : "You'll need a key!";
      ui.AlertPlayer(msg);
    }
    else if (Actor is Player)
    {
      ui.AlertPlayer("There's no door there!");
    }

    return result;
  }
}

// Some monsters, when grappling, automatically deal damage to their victim
class CrushAction(GameState gs, Actor actor, ulong victimId, int dmgDie, int dmgDice) : Action(gs, actor)
{
  public int DmgDie { get; set; } = dmgDie;
  public int DmgDice { get; set; } = dmgDice;
  public ulong VictimId { get; set; } = victimId;

  public override ActionResult Execute()
  {
    var result = new ActionResult() { Succcessful = true, EnergyCost = 1.0 };

    if (GameState!.ObjDb.GetObj(VictimId) is Actor victim)
    {
      if (GameState!.LastPlayerFoV.Contains(Actor!.Loc))
      {
        string s = $"{Actor.FullName.Capitalize()} {Grammar.Conjugate(Actor, "crush")} {victim.FullName}.";
        GameState.UIRef().AlertPlayer(s);
      }

      List<(int, DamageType)> damageRolls = [];
      for (int i = 0; i < DmgDice; i++)
      {
        damageRolls.Add((GameState.Rng.Next(DmgDie) + 1, DamageType.Blunt));        
      }
      var (hpLeft, _, _) = victim.ReceiveDmg(damageRolls, 0, GameState, null, 1.0);
      if (hpLeft < 1)
      {
        GameState.ActorKilled(victim, "being crushed", null);
      }
    }
    
    return result;
  }
}

class PickupItemAction(GameState gs, Actor actor) : Action(gs, actor)
{
  public List<ulong> ItemIDs { get; set; } = [];
  
  public override ActionResult Execute()
  {
    ActionResult result = new() { Succcessful = true, EnergyCost = 1.0 };

    UserInterface ui = GameState!.UIRef();
    ui.ClosePopup();
    
    List<Item> itemStack = GameState.ObjDb.ItemsAt(Actor!.Loc);
    Inventory inv = Actor.Inventory;

    int webDC = 0;
    string webName = "";
    // First, is there anything preventing the actor from picking items up off
    // the floor? (At the moment it's just webs in the game, but a 
    // Sword-in-the-Stone situation might be neat)
    foreach (Item env in GameState.ObjDb.EnvironmentsAt(Actor.Loc))
    {
      if (env.Traits.OfType<StickyTrait>().FirstOrDefault() is StickyTrait web)
      {
        webDC = int.Max(webDC, web.DC);
        webName = env.Name.DefArticle();
      }
    }

    bool anythingPickedUp = false;
    foreach (ulong id in ItemIDs)
    {
      Item item = itemStack.Where(i => i.ID == id).First();

      if (item.HasTrait<AffixedTrait>())
      {
        ui.AlertPlayer($"You cannot pick up {item.FullName.DefArticle()}!");
        continue;
      }

      if (webDC > 0)
      {
        bool strCheck = Actor.AbilityCheck(Attribute.Strength, webDC, GameState.Rng);
        if (strCheck)
        {
          ui.AlertPlayer($"{Actor.FullName.Capitalize()} {Grammar.Conjugate(Actor, "tear")} {item.FullName.DefArticle()} from {webName}.");          
        }
        else
        {
          ui.AlertPlayer($"{item.FullName.DefArticle().Capitalize()} {Grammar.Conjugate(item, "is")} stuck to {webName}!");
          continue;
        }
      }

      bool freeSlot = inv.UsedSlots().Length < 26;
      if (!freeSlot)
      {
        ui.AlertPlayer("There's no room left in your inventory!");
        break;
      }

      int count = 0;
      if (item.HasTrait<StackableTrait>())
      {
        foreach (Item pickedUp in itemStack.Where(i => i == item))
        {
          GameState.ObjDb.RemoveItemFromLoc(Actor.Loc, pickedUp);
          inv.Add(pickedUp, Actor.ID);
          ++count;
        }
      }
      else
      {
        GameState.ObjDb.RemoveItemFromLoc(Actor.Loc, item);
        inv.Add(item, Actor.ID);
      }

      string pickupMsg = $"{Actor.FullName.Capitalize()} {Grammar.Conjugate(Actor, "pick")} up ";
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

      if (item.Traits.OfType<OwnedTrait>().FirstOrDefault() is OwnedTrait ownedTrait)
      {
        List<string> msgs = GameState.OwnedItemPickedUp(ownedTrait.OwnerIDs, Actor, item.ID);
        foreach (string s in msgs)
          ui.AlertPlayer(s);
      }

      // Clear the 'in a pit' flag when an item is picked up
      if (item.HasTrait<InPitTrait>())
      {
        item.Traits = item.Traits.Where(t => t is not InPitTrait).ToList();
      }

      anythingPickedUp = true;
      ui.AlertPlayer(pickupMsg);
    }

    if (!anythingPickedUp)
    {
      result.Succcessful = false;
      result.EnergyCost = 0.0;
    }
            
    return result;
  }

  public override void ReceiveUIResult(UIResult result)
  {
    if (result is LongListResult list)
      ItemIDs = list.Values;    
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

    // We'll keep the level from getting too over-populated. I don't know what
    // a good number is yet, but let's start with 100
    int levelPop = GameState!.ObjDb.LevelCensus(Actor!.Loc.DungeonID, Actor.Loc.Level);
    if (levelPop > 100)
    {
      if (GameState.LastPlayerFoV.Contains(Actor.Loc))
        GameState.UIRef().AlertPlayer("A spell fizzles");

      return new ActionResult() { Succcessful = true, EnergyCost = 1.0 };
    }

    int summonCount = 0;
    for (int j = 0; j < _count; j++)
    {
      var loc = SpawnPt();
      if (loc != Loc.Nowhere)
      {
        var summoned = MonsterFactory.Get(_summons, GameState!.ObjDb, GameState.Rng);
        summoned.Stats[Attribute.MobAttitude].SetMax(Mob.AGGRESSIVE);
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

    foreach (string s in msgs)
      GameState!.UIRef().AlertPlayer(s);

    return new ActionResult() { Succcessful = true, EnergyCost = 1.0 };
  }
}

class SearchAction(GameState gs, Actor player) : Action(gs, player)
{
  public override ActionResult Execute()
  {
    var result = base.Execute();
    result.Succcessful = true;
    result.EnergyCost = 1.0;

    GameState gs = GameState!;
    UserInterface ui = gs.UIRef();
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

          ui.AlertPlayer($"You find {item.FullName.IndefArticle()}!");
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
            ui.AlertPlayer("You spot a secret door!");
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
            ui.AlertPlayer("You spot a trap!");
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
            ui.AlertPlayer("You spot a magic mouth!");
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
              ui.AlertPlayer("You spot a loose flagstone!");
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
              ui.AlertPlayer("You spot a pressure plate!");
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
      Colour = Colours.SEARCH_HIGHLIGHT,
      AltColour = Colours.SEARCH_HIGHLIGHT
    };
    gs.UIRef().RegisterAnimation(anim);

    return result;
  }
}

class DetectTrapsAction(GameState gs, Actor caster) : Action(gs, caster)
{
  public override ActionResult Execute()
  {
    var result = base.Execute();

    if (Actor is Player player)
    {
      Loc playerLoc = GameState!.Player.Loc;
      result.Succcessful = true;
      result.EnergyCost = 1.0;

      int topScreenRow = playerLoc.Row - UserInterface.ViewHeight / 2;
      int topScreenCol = playerLoc.Col - UserInterface.ViewWidth / 2;
      int trapsFound = 0;
      for (int r = 1; r < GameState!.CurrentMap.Height - 1; r++)
      {
        for (int c = 1; c < GameState.CurrentMap.Width - 1; c++)
        {
          Loc loc = GameState.Player.Loc with { Row = r, Col = c };
          Tile tile = GameState.TileAt(loc);
          if (tile.IsTrap())
          {
            ++trapsFound;
            Glyph g = new('^', Colours.WHITE, Colours.WHITE, Colours.BLACK, Colours.BLACK);
            GameState.CurrentDungeon.RememberedLocs[loc] = g;
            Traps.RevealTrap(tile, GameState, loc);

            if (Util.PtInSqr(r, c, topScreenRow, topScreenCol, UserInterface.ViewHeight, UserInterface.ViewWidth))
            {
              SqAnimation anim = new(GameState, loc, Colours.WHITE, Colours.LIGHT_PURPLE, '^') { IgnoreFoV = true };
              GameState.UIRef().RegisterAnimation(anim);
            }
          }
        }
      }
      
      if (trapsFound > 0)
        GameState.UIRef().AlertPlayer("You have a sense of looming danger!");
      else
        GameState.UIRef().AlertPlayer("You feel a certain relief.");
    }

    return result;
  }
}

class DetectTreasureAction(GameState gs, Actor caster) : Action(gs, caster)
{
  public override ActionResult Execute()
  {
    var result = base.Execute();

    if (Actor is Player player)
    {
      Loc playerLoc = GameState!.Player.Loc;
      result.Succcessful = true;
      result.EnergyCost = 1.0;

      int topScreenRow = playerLoc.Row - UserInterface.ViewHeight / 2;
      int topScreenCol = playerLoc.Col - UserInterface.ViewWidth / 2;
      int itemsFound = 0;
      for (int r = 1; r < GameState!.CurrentMap.Height - 1; r++)
      {
        for (int c = 1; c < GameState.CurrentMap.Width - 1; c++)
        {
          Loc loc = GameState.Player.Loc with { Row = r, Col = c };
          Glyph glyph = GameState.ObjDb.ItemGlyphAt(loc);
          if (glyph != GameObjectDB.EMPTY)
          {
            ++itemsFound;
            GameState.CurrentDungeon.RememberedLocs[loc] = glyph;

            if (Util.PtInSqr(r, c, topScreenRow, topScreenCol, UserInterface.ViewHeight, UserInterface.ViewWidth))
            {
              SqAnimation anim = new(GameState, loc, Colours.WHITE, Colours.LIGHT_PURPLE, glyph.Ch) { IgnoreFoV = true };
              GameState.UIRef().RegisterAnimation(anim);
            }
          }
        }
      }
      
      if (itemsFound > 0)
        GameState.UIRef().AlertPlayer("Your nose twitches and you feel greedy!");
      else
        GameState.UIRef().AlertPlayer("You feel a sense of disappointment.");
    }

    return result;
  }
}

class MagicMapAction(GameState gs, Actor caster) : Action(gs, caster)
{
  // Essentially we want to flood fill out and mark all reachable squares as 
  // remembered, ignoreable Passable() or not but stopping at walls. This will
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

    MagicMapAnimation anim = new(gs, dungeon, locs);
    gs.UIRef().RegisterAnimation(anim);
  }

  public override ActionResult Execute()
  {
    var result = base.Execute();

    // It's probably a bug if a monster invokes this action??
    if (Actor is Player player)
    {
      result.Succcessful = true;
      result.EnergyCost = 1.0;

      if (GameState!.InWilderness)
      {
        GameState.UIRef().AlertPlayer("The wide world is big for the spell! The magic fizzles!");
      }
      else
      {
        GameState.UIRef().AlertPlayer("A vision of your surroundings fills your mind!");
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
    Mob dup = new()
    {
      Name = src.Name,
      Glyph = glyph,
      Recovery = 1.0
    };

    dup.Stats.Add(Attribute.HP, new Stat(1));
    dup.Stats.Add(Attribute.AC, new Stat(10));
    dup.Stats.Add(Attribute.MobAttitude, new Stat(Mob.AGGRESSIVE));

    dup.Traits.Add(new FlyingTrait());

    var illusion = new IllusionTrait()
    {
      SourceId = src.ID,
      ObjId = dup.ID
    };
    dup.Traits.Add(illusion);
    gs.RegisterForEvent(GameEventType.Death, illusion, src.ID);

    dup.Traits.Add(new BehaviourTreeTrait() { Plan = "MonsterPlan" });

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
      GameState!.UIRef().AlertPlayer("A spell fizzles...");
      return new ActionResult() { Succcessful = true, EnergyCost = 1.0 };
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
    result.Succcessful = true;
    result.EnergyCost = 1.0;
    GameState.UIRef().AlertPlayer("How puzzling!");

    return result;
  }
}

class FogCloudAction(GameState gs, Actor caster) : Action(gs, caster)
{    
  public override ActionResult Execute()
  {
    ActionResult result = base.Execute();
    result.Succcessful = true;
    result.EnergyCost = 1.0;

    var gs = GameState!;

    Loc targetLoc = Actor!.PickRangedTargetLoc(GameState!);
    if (gs.LastPlayerFoV.Contains(targetLoc))
      gs.UIRef().AlertPlayer($"{Actor!.FullName.Capitalize()} {Grammar.Conjugate(Actor, "cast")} Fog Cloud!");
    
    foreach (Loc loc in Util.LocsInRadius(targetLoc, 2, gs.CurrentMap.Height, gs.CurrentMap.Width))
    {
      var mist = ItemFactory.Mist(gs);
      var timer = mist.Traits.OfType<CountdownTrait>().First();
      gs.RegisterForEvent(GameEventType.EndOfRound, timer);
      gs.ObjDb.Add(mist);
      gs.ItemDropped(mist, loc);
    }

    return result;
  }
}

class InduceNudityAction(GameState gs, Actor caster) : Action(gs, caster)
{    
  public override ActionResult Execute()
  {
    ActionResult result = base.Execute();
    result.Succcessful = true;
    result.EnergyCost = 1.0;
    UserInterface ui = GameState!.UIRef();

    if (GameState!.LastPlayerFoV.Contains(GameState!.Player.Loc))
    {
      string s = $"{Actor!.FullName.Capitalize()} {Grammar.Conjugate(Actor, "dance")} a peculiar jig.";
      ui.AlertPlayer(s);

      Loc targetLoc = Actor.PickRangedTargetLoc(GameState);
      if (GameState.ObjDb.Occupant(targetLoc) is Actor victim)
      {
        var clothes = victim.Inventory.Items()
                                      .Where(i => i.Type == ItemType.Armour && i.Equipped).ToList();
        if (clothes.Count == 0)
          return result;

        Item item = clothes[GameState.Rng.Next(clothes.Count)];
        victim.Inventory.RemoveByID(item.ID);
        item.Equipped = false;
        GameState.ItemDropped(item, victim.Loc);
        s = $"{item.FullName.Possessive(victim).Capitalize()} falls off!";

        if (GameState.LastPlayerFoV.Contains(victim.Loc))
          ui.AlertPlayer(s);
        if (victim is Player)
          GameState.UIRef().SetPopup(new Popup(s, "", -1, -1));
      }
    }

    return result;
  }
}

class DrainTorchAction(GameState gs, Actor caster, Loc target) : Action(gs, caster)
{
  readonly Loc _target = target;

  public override ActionResult Execute()
  {
    ActionResult result = base.Execute();
    result.Succcessful = true;
    result.EnergyCost = 1.0;
    UserInterface ui = GameState!.UIRef();

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
            ui.AlertPlayer(s + $" {item.FullName.Possessive(victim)}!");
            success = true;
          }
        }
      }
    }
    
    if (!success)
    {
      ui.AlertPlayer("The spell fizzles.");
    }

    return result;
  }
}

class EntangleAction(GameState gs, Actor caster) : Action(gs, caster)
{
  
  public override ActionResult Execute()
  {
    Loc targetLoc = Actor!.PickRangedTargetLoc(GameState!);
    foreach (var (r, c) in Util.Adj8Sqs(targetLoc.Row, targetLoc.Col))
    {
      var loc = targetLoc with { Row = r, Col = c };
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
    GameState!.UIRef().AlertPlayer(txt);

    return new ActionResult() { Succcessful = true,EnergyCost = 1.0 };
  }
}

class FireboltAction(GameState gs, Actor caster, Loc target) : Action(gs, caster)
{
  readonly Loc _target = target;

  public override ActionResult Execute()
  {
    var firebolt = ItemFactory.Get(ItemNames.FIREBOLT, GameState!.ObjDb);
    var attack = new MissileAttackAction(GameState, Actor!, _target, firebolt);

    string txt = $"{MsgFactory.CalcName(Actor!, GameState.Player).Capitalize()} {Grammar.Conjugate(Actor!, "cast")} firebolt!";
    GameState.UIRef().AlertPlayer(txt);

    return new ActionResult() { Succcessful = true, AltAction = attack, EnergyCost = 0.0 };
  }
}

class WebAction : Action
{
  Loc Target { get; set; }

  public WebAction(GameState gs, Loc target)
  {
    GameState = gs;
    Target = target;
  }

  public override ActionResult Execute()
  {
    var w = ItemFactory.Web();
    GameState!.ObjDb.Add(w);
    GameState.ItemDropped(w, Target);

    foreach (Loc adj in Util.Adj8Locs(Target))
    {
      if (GameState.TileAt(adj).Passable() && GameState.Rng.NextDouble() < 0.666)
      {
        w = ItemFactory.Web();
        GameState.ObjDb.Add(w);
        GameState.ItemDropped(w, Target with { Row = adj.Row, Col = adj.Col });
      }
    }

    if (GameState.ObjDb.Occupant(Target) is Actor victim)
    {
      string txt = $"{victim.FullName.Capitalize()} {Grammar.Conjugate(victim, "is")} caught up in webs!";
      GameState!.UIRef().AlertPlayer(txt, GameState, Target);
    }

    return new ActionResult() { Succcessful = true, EnergyCost = 1.0 };
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
      GameState!.UIRef().AlertPlayer("You shudder for a moment.");
      result.Succcessful = true;
      result.EnergyCost = 1.0;

      return result;
    }

    ulong happensOn = GameState.Turn + (ulong) GameState.Rng.Next(10, 21);
    var recall = new RecallTrait()
    {
      ExpiresOn = happensOn,
      ObjId = player.ID
    };

    GameState.RegisterForEvent(GameEventType.EndOfRound, recall);
    GameState.Player.Traits.Add(recall);

    GameState.UIRef().AlertPlayer("The air crackles around you.");
    result.Succcessful = true;
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
      GameState!.UIRef().AlertPlayer("You hear a spectral knocking.");
      result.EnergyCost = 1.0;

      var sqs = GameState!.Flood(caster.Loc, 4, true);
      foreach (Loc sq in sqs)
      {
        Tile tile = GameState.TileAt(sq);
        if (tile.Type == TileType.LockedDoor || tile.Type == TileType.SecretDoor)
        {
          GameState.UIRef().AlertPlayer("Click!");
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
    // If the caster is currently swallowed, we want to remove the swallwed 
    // trait before resolving the blink so that we're blinking to a loc in the
    // real level. (This might have problems with weird interactions were, like,
    // the player was standing in lava, then got swallowed, then blinked. 
    // Technically the player would land on the lava sq then blink, which we 
    // might not want. Problem for future Dana
    if (Actor!.Traits.OfType<SwallowedTrait>().FirstOrDefault() is SwallowedTrait swallowed)
    {
      swallowed.Remove(GameState!);
    }

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
      GameState!.UIRef().AlertPlayer("A spell fizzles...");
      return new ActionResult() { Succcessful = true, EnergyCost = 1.0 };
    }
    else
    {
      Actor.ClearAnchors(GameState!);
      
      var landingSpot = sqs[GameState!.Rng.Next(sqs.Count)];      
      GameState.UIRef().RegisterAnimation(new SqAnimation(GameState, landingSpot, Colours.WHITE, Colours.LIGHT_PURPLE, '*'));
      GameState.UIRef().RegisterAnimation(new SqAnimation(GameState, start, Colours.WHITE, Colours.LIGHT_PURPLE, '*'));

      var mv = new MoveAction(GameState, Actor, landingSpot);

      ActionResult result = base.Execute();
      result.Succcessful = false;
      result.EnergyCost = 0.0;
      result.AltAction = mv;
      if (GameState.LastPlayerFoV.Contains(Actor.Loc))
        GameState.UIRef().AlertPlayer($"Bamf! {Actor.FullName.Capitalize()} {Grammar.Conjugate(Actor, "blink")} away!");
      
      return result;
    }
  }
}

class AntidoteAction(GameState gs, Actor target) : Action(gs, target)
{
  public override ActionResult Execute()
  {
    if (Actor is Player && !Actor.HasTrait<PoisonedTrait>())
    {
      GameState!.UIRef().AlertPlayer("That tasted not bad.");
      return new ActionResult() { Succcessful = true, EnergyCost = 1.0 };
    }

    foreach (var t in Actor!.Traits.OfType<PoisonedTrait>())
    {
      GameState!.StopListening(GameEventType.EndOfRound, t);
    }
    Actor.Traits = [..Actor.Traits.Where(t => t is not PoisonedTrait)];
    string msg = $"That makes {Actor.FullName} {Grammar.Conjugate(Actor, "feel")} better.";
    GameState!.UIRef().AlertPlayer(msg);

    return new ActionResult() { Succcessful = true, EnergyCost = 1.0 };
  }
}

class DrinkBoozeAction(GameState gs, Actor target) : Action(gs, target)
{
  public override ActionResult Execute()
  {
    ActionResult result = base.Execute();
    result.EnergyCost = 1.0;
    result.Succcessful = true;
    UserInterface ui = GameState!.UIRef();

    bool canSeeLoc = GameState!.LastPlayerFoV.Contains(Actor!.Loc);

    if (Actor is Player)
      ui.AlertPlayer("Glug! Glug! Glug!");
    else if (canSeeLoc)
      ui.AlertPlayer($"{Actor.FullName.Capitalize()} drinks some booze!");

    foreach (string s in Battle.HandleTipsy(Actor, GameState))
      ui.AlertPlayer(s);

    return result;
  }
}

class HealAction(GameState gs, Actor target, int healDie, int healDice) : Action(gs, target)
{
  readonly int _healDie = healDie;
  readonly int _healDice = healDice;
  
  public override ActionResult Execute()
  {
    ActionResult result = base.Execute();

    if (!Actor!.Stats.ContainsKey(Attribute.HP))
    {
      GameState!.UIRef().AlertPlayer("The spell seems to fizzle.", GameState, Actor.Loc);
      result.Succcessful = true;
      result.EnergyCost = 1.0;
    }

    Stat hpStat = Actor!.Stats[Attribute.HP];
    int hpBefore = hpStat.Curr;

    int hp = 0;

    if (_healDice == -1)
      hp = _healDie;
    else
    {
      for (int j = 0; j < _healDice; j++)
        hp += GameState!.Rng.Next(_healDie) + 1;
    }
    hpStat.Change(hp);
    int delta = hpStat.Curr - hpBefore;

    string txt;
    if (delta > 0 && Actor is Player)
      txt = $"You are healed for {delta} HP.";
    else if (delta > 0)
      txt = $"{Actor.FullName.Capitalize()} is healed.";
    else
      txt = "";
    GameState!.UIRef().AlertPlayer(txt, GameState, Actor.Loc);

    var healAnim = new SqAnimation(GameState!, Actor.Loc, Colours.WHITE, Colours.PURPLE, '\u2665');
    GameState!.UIRef().RegisterAnimation(healAnim);
    
    result.Succcessful = true;
    result.EnergyCost = 1.0;

    if (Actor.Traits.OfType<LameTrait>().FirstOrDefault() is LameTrait lame)
    {
      lame.Remove(GameState, Actor);
    }

    return result;
  }
}

class SootheAction(GameState gs, Actor target, int amount) : Action(gs, target)
{
  int Amount { get; set; } = amount;
  
  public override ActionResult Execute()
  {
    ActionResult result = base.Execute();

    
    if (Actor!.Stats.TryGetValue(Attribute.Nerve, out Stat? nerve))
    {
      GameState!.UIRef().AlertPlayer($"{Actor!.FullName.Capitalize()} {Grammar.Conjugate(Actor, "feel")} more calm.");
      SqAnimation anim = new(GameState!, Actor.Loc, Colours.WHITE, Colours.SOPHIE_GREEN, '\u2665');
      GameState!.UIRef().RegisterAnimation(anim);

      nerve.Change(Amount);
    }
    
    result.Succcessful = true;
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
      coins.Value = _amount;
      GameState.ItemDropped(coins, Actor.Loc);      
      msg = $"{MsgFactory.CalcName(Actor, GameState.Player).Capitalize()} {Grammar.Conjugate(Actor, "drop")} ";
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

    foreach (string s in msgs)
      GameState!.UIRef().AlertPlayer(s);

    return new ActionResult() { Succcessful = successful, EnergyCost = cost };
  }
  
  public override void ReceiveUIResult(UIResult result) => _amount = ((NumericUIResult)result).Amount;
}

class DropStackAction(GameState gs, Actor actor, char slot) : Action(gs, actor)
{
  readonly char _slot = slot;
  int _amount;

  public override ActionResult Execute()
  {
    GameState!.UIRef().ClosePopup();
    ActionResult result = new() { Succcessful = true, EnergyCost = 1.0 };
    var (item, itemCount) = Actor!.Inventory.ItemAt(_slot);
    if (item is null)
      return result; // This should never nhappen

    if (_amount == 0 || _amount > itemCount)
      _amount = itemCount;

    var droppedItems = Actor.Inventory.Remove(_slot, _amount);
    foreach (var droppedItem in droppedItems)
    {
      GameState.ItemDropped(droppedItem, Actor.Loc);
      droppedItem.Equipped = false;
    }

    string s = $"{MsgFactory.CalcName(Actor, GameState.Player).Capitalize()} {Grammar.Conjugate(Actor, "drop")} ";
    s += MsgFactory.CalcName(item, GameState.Player, _amount) + ".";
    GameState!.UIRef().AlertPlayer(s, GameState, Actor.Loc);

    return result;
  }

  public override void ReceiveUIResult(UIResult result) => _amount = ((NumericUIResult)result).Amount;
}

class ThrowAction(GameState gs, Actor actor, char slot) : Action(gs, actor)
{
  readonly char _slot = slot;
  Loc _target { get; set; }

  public override ActionResult Execute()
  {
    var result = new ActionResult() { Succcessful = true, EnergyCost = 1.0 };
    var ammo = Actor!.Inventory.Remove(_slot, 1).First();
    if (ammo != null)
    {
      // Calculate where the projectile will actually stop
      var trajectory = Util.Trajectory(Actor.Loc, _target);
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
          ActionResult attackResult = Battle.MissileAttack(Actor, occ, GameState, ammo, 0, null);
          result.EnergyCost = attackResult.EnergyCost;
          if (attackResult.Succcessful)
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

      var anim = new ThrownMissileAnimation(GameState!, ammo.Glyph, pts, ammo);
      GameState!.UIRef().PlayAnimation(anim, GameState);

      var landingPt = pts.Last();
      GameState.ItemDropped(ammo, landingPt);
      ammo.Equipped = false;
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
      GameState.UIRef().AlertPlayer("That doesn't make any sense!");
    }
    else
    {
      player.FireReadedBow(item, GameState);
      
      result.EnergyCost = 0.0;
      result.Succcessful = false;
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
      ActionResult result = new() { Succcessful = false, EnergyCost = 0.0 };
      GameState.UIRef().AlertPlayer("That doesn't make sense!");
      return result;
    }
    else if (item.Type == ItemType.Armour && item.Equipped)
    {
      ActionResult result = new() { Succcessful = false, EnergyCost = 0.0 };
      GameState.UIRef().AlertPlayer("You're wearing that!");
      return result;
    }
    else if ((item.Type == ItemType.Ring || item.Type == ItemType.Talisman) && item.Equipped)
    {
      ActionResult result = new() { Succcessful = false, EnergyCost = 0.0 };
      GameState.UIRef().AlertPlayer("You'll need to un-equip it first!");
      return result;
    }

    ThrowAction action = new(GameState, player, Choice);
    int range = 7 + player.Stats[Attribute.Strength].Curr;
    if (range < 2)
      range = 2;
    Aimer acc = new(GameState, player.Loc, range);
    player.ReplacePendingAction(action, acc);

    return new ActionResult() { Succcessful = false, EnergyCost = 0.0 };
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
        GameState.UIRef().AlertPlayer("You have no money!");
        return new ActionResult() { Succcessful = false };
      }
      var dropMoney = new DropZorkmidsAction(GameState, Actor);
      ui.SetPopup(new Popup("How much?", "", -1, -1));
      var acc = new NumericInputer(ui, "How much?");
      if (Actor is Player player)
      {
        player.ReplacePendingAction(dropMoney, acc);
        return new ActionResult() { Succcessful = false, EnergyCost = 0.0 };
      }
      else
        // Will monsters ever just decide to drop money?
        return new ActionResult() { Succcessful = true };
    }

    var (item, itemCount) = Actor!.Inventory.ItemAt(Choice);
    if (item is null)
      throw new Exception("Hmm this shouldn't happen!");

    if (item.Equipped && item.Type == ItemType.Armour)
    {
      GameState.UIRef().AlertPlayer("You cannot drop something you are wearing.");
      return new ActionResult() { Succcessful = false };
    }
    if (item.Equipped && item.Type == ItemType.Ring)
    {
      GameState.UIRef().AlertPlayer("You'll need to take it off first.");
      return new ActionResult() { Succcessful = false };
    }
    else if (item.Equipped && item.Type == ItemType.Talisman)
    {
      GameState.UIRef().AlertPlayer("You'll need to un-equip it first.");
      return new ActionResult() { Succcessful = false };
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
        return new ActionResult() { Succcessful = false, EnergyCost = 0.0 };
      }
      else
        // When monsters can drop stuff I guess I'll have to handle that here??
        return new ActionResult() { Succcessful = true };
    }
    else
    {
      string s = $"{MsgFactory.CalcName(Actor, GameState.Player).Capitalize()} {Grammar.Conjugate(Actor, "drop")} ";
      s += MsgFactory.CalcName(item, GameState.Player) + ".";
      ui.AlertPlayer(s);

      Actor.Inventory.Remove(Choice, 1);
      GameState.ItemDropped(item, Actor.Loc);
      item.Equipped = false;

      return new ActionResult() { Succcessful = true, EnergyCost = 1.0 };
    }
  }

  public override void ReceiveUIResult(UIResult result) => Choice = ((MenuUIResult)result).Choice;
}

class ApplyPoisonAction(GameState gs, Actor actor, Item? item) : Action(gs, actor)
{
  public char Choice { get; set; }
  Item? SourceItem { get; set; } = item;

  public override ActionResult Execute()
  {
    ActionResult result = base.Execute();

    GameState!.ClearMenu();

    var (item, _) = Actor!.Inventory.ItemAt(Choice);

    if (item != null)
    {
      string objName = item.FullName.DefArticle();

      item.Traits.Add(new PoisonCoatedTrait());
      item.Traits.Add(new AdjectiveTrait("poisoned"));
      item.Traits.Add(new PoisonerTrait() { DC = 15, Strength = 2, Duration = 10 });

      string name = Actor.FullName.Capitalize();
      string verb = Grammar.Conjugate(Actor, "smear");      
      string s = $"{name} {verb} some poison on {objName}.";
      GameState.UIRef().AlertPlayer(s);

      if (SourceItem is not null && SourceItem.HasTrait<ConsumableTrait>())
      {
        Actor.Inventory.ConsumeItem(SourceItem, Actor, GameState.Rng);
      }
    }

    result.Succcessful = true;
    result.EnergyCost = 1.0;

    return result;
  }

  public override void ReceiveUIResult(UIResult result) => Choice = ((MenuUIResult)result).Choice;
}

class IdentifyItemAction(GameState gs, Actor actor, Item? item) : Action(gs, actor)
{
  public char Choice { get; set; }
  Item? SourceItem { get; set; } = item;

  public override ActionResult Execute()
  {
    ActionResult result = base.Execute();
    result.Succcessful = true;
    result.EnergyCost = 1.0;

    GameState!.ClearMenu();
        
    var (item, _) = Actor!.Inventory.ItemAt(Choice);
    if (item is null)
      return result; // I think this should be impossible?

    item.Identify();

    string s = $"\n It's {item.FullName.IndefArticle()}! \n";
    GameState.UIRef().SetPopup(new Popup(s, "", -1, -1));

    string m = $"{Actor.FullName.Capitalize()} {Grammar.Conjugate(Actor, "identify")} {item.FullName.DefArticle()}.";
    GameState.UIRef().AlertPlayer(m);

    if (SourceItem is not null && SourceItem.HasTrait<ConsumableTrait>())
    {
      Actor.Inventory.ConsumeItem(SourceItem, Actor, GameState.Rng);
    }

    return result;
  }

  public override void ReceiveUIResult(UIResult result) => Choice = ((MenuUIResult)result).Choice;
}

class ToggleEquippedAction(GameState gs, Actor actor) : Action(gs, actor)
{
  public char Choice { get; set; }
  
  public override ActionResult Execute()
  {
    ActionResult result;
    var (item, _) = Actor!.Inventory.ItemAt(Choice);
    GameState!.ClearMenu();

    if (item is null)
    {
      GameState.UIRef().AlertPlayer("You cannot equip that!");
      return new ActionResult() { Succcessful = false };
    }

    if (!item.Equipable())
    {
      GameState.UIRef().AlertPlayer("You cannot equip that!");
      return new ActionResult() { Succcessful = false };
    }

    var (equipResult, conflict) = ((Player)Actor).Inventory.ToggleEquipStatus(Choice);
    string s;
    switch (equipResult)
    {
      case EquipingResult.Equipped:
        s = $"{Actor.FullName.Capitalize()} {Grammar.Conjugate(Actor, "ready")} {item.FullName.DefArticle()}";
        s += item.Type == ItemType.Wand ? " as a casting focus." : ".";
        GameState.UIRef().AlertPlayer(s);
        result = new ActionResult() { Succcessful = true, EnergyCost = 1.0 };

        if (item.HasTrait<CursedTrait>())
        {
          if (item.Type == ItemType.Ring)
            GameState.UIRef().AlertPlayer("The ring tightens around your finger!");          
        }
        break;
      case EquipingResult.Cursed:
        GameState.UIRef().AlertPlayer("You cannot remove it! It seems to be cursed!");
        result = new ActionResult() { Succcessful = true, EnergyCost = 1.0 };
        break;
      case EquipingResult.Unequipped:
        s = $"{Actor.FullName.Capitalize()} {Grammar.Conjugate(Actor, "remove")} {item.FullName.DefArticle()}.";
        GameState.UIRef().AlertPlayer(s);
        result = new ActionResult() { Succcessful = true, EnergyCost = 1.0 };
        break;
      case EquipingResult.TwoHandedConflict:
        GameState.UIRef().AlertPlayer("You cannot wear a shield with a two-handed weapon!");
        result = new ActionResult() { Succcessful = true, EnergyCost = 0.0 };
        break;
      case EquipingResult.ShieldConflict:
        GameState.UIRef().AlertPlayer("You cannot use a two-handed weapon with a shield!");
        result = new ActionResult() { Succcessful = true, EnergyCost = 0.0 };
        break;
      case EquipingResult.TooManyRings:
        GameState.UIRef().AlertPlayer("You are already wearing two rings!");
        result = new ActionResult() { Succcessful = true, EnergyCost = 0.0 };
        break;
      case EquipingResult.TooManyTalismans:
        GameState.UIRef().AlertPlayer("You may only use two talismans at a time!");
        result = new ActionResult() { Succcessful = true, EnergyCost = 0.0 };
        break;
      case EquipingResult.NoFreeHand:
        GameState.UIRef().AlertPlayer("You have no free hands!");
        result = new ActionResult() { Succcessful = true, EnergyCost = 0.0 };
        break;
      default:
        string msg = "You are already wearing ";
        msg += conflict switch
        {
          ArmourParts.Hat => "a helmet.",
          ArmourParts.Shield => "a shield.",
          ArmourParts.Boots => "boots.",
          ArmourParts.Cloak => "a cloak.",
          ArmourParts.Gloves => "some gloves",
          _ => "some armour."
        };
        GameState.UIRef().AlertPlayer(msg);
        result = new ActionResult() { Succcessful = true };
        break;
    }

    if (item.Traits.OfType<GrantsTrait>().FirstOrDefault() is GrantsTrait grants)
    {
      if (Item.IDInfo.TryGetValue(item.Name, out ItemIDInfo? value))
        Item.IDInfo[item.Name] = value with { Known = true };

      if (equipResult == EquipingResult.Equipped)
      {
        foreach (string msg in grants.Grant(Actor, GameState, item))
          GameState.UIRef().AlertPlayer(msg);
      }
      else if (equipResult == EquipingResult.Unequipped)
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
    result.Succcessful = true;

    // Fireball shoots toward the target and then explodes, but its path 
    // may be interrupted
    List<Loc> pts = [];
    Loc actualLoc = Target;
    foreach (var pt in Trajectory(Actor!.Loc, true))
    {            
      actualLoc = pt;
      pts.Add(pt);

      if (GameState!.ObjDb.Occupant(pt) is Actor occ && occ != Actor)
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
        GameState.UIRef().AlertPlayer($"{victim.FullName.Capitalize()} {Grammar.Conjugate(victim, "is")} caught in the flames!");

        var (hpLeft, _, _) = victim.ReceiveDmg(dmg, 0, GameState, null, 1.0);
        if (hpLeft < 1)
        {
          GameState.ActorKilled(victim, "a fireball", null);
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

class RayOfSlownessAction(GameState gs, Actor actor, Trait src, ulong sourceId) : TargetedAction(gs, actor)
{
  readonly Trait _source = src;
  readonly ulong SourceId = sourceId;

    public override ActionResult Execute()
  {
    var result = base.Execute();
    result.EnergyCost = 1.0;
    result.Succcessful = true;

    Item ray = new()
    {
      Name = "ray of slowness",
      Type = ItemType.Weapon,
      Glyph = new Glyph('*', Colours.FADED_PURPLE, Colours.FADED_PURPLE, Colours.BLACK, Colours.BLACK)
    };
    GameState!.ObjDb.Add(ray);

    List<Loc> pts = Trajectory(Actor!.Loc, true).Skip(1).ToList();
    var anim = new BeamAnimation(GameState!, pts, Colours.FADED_PURPLE, Colours.BLACK);
    GameState!.UIRef().PlayAnimation(anim, GameState);

    foreach (Loc loc in pts)
    {
      if (GameState.ObjDb.Occupant(loc) is Actor victim)
      {
        bool alreadyAffected = false;
        foreach (Trait t in victim.Traits)
        {
          // They can only be affected once by the same wand
          if (t is AlacrityTrait at && at.SourceId == SourceId)
          {
            alreadyAffected = true;
            break;
          }
        }

        if (!alreadyAffected)
        {
          if (GameState.LastPlayerFoV.Contains(loc))
          {
            string s = $"{victim.FullName.Capitalize()} {Grammar.Conjugate(victim, "begin")} to move slower.";
            GameState.UIRef().AlertPlayer(s);
          }
            
          victim.Traits.Add(new AlacrityTrait() { Amt = -0.5, SourceId = SourceId });
        }
      }
    }

    if (_source is WandTrait wand)
    {
      Item.IDInfo["wand of slow monster"] = Item.IDInfo["wand of slow monster"] with { Known = true };
      wand.Used();
    }
    else if (_source is IUSeable useable)
    {
      useable.Used();
    }

    return result;
  }
}

class DigRayAction(GameState gs, Actor actor, Trait src) : TargetedAction(gs, actor)
{
  readonly Trait Source = src;

  public override ActionResult Execute()
  {
    var result = base.Execute();
    result.EnergyCost = 1.0;
    result.Succcessful = true;

    List<Loc> pts = Trajectory(Actor!.Loc, false);

    var anim = new BeamAnimation(GameState!, pts, Colours.LIGHT_BROWN, Colours.WHITE);

    UserInterface ui = GameState!.UIRef();
    ui.PlayAnimation(anim, GameState);

    if (Actor is Player)
      ui.AlertPlayer("You hear a zap and the faint, ghostly humming of unseen dwarven miners!");

    foreach (Loc loc in pts)
    {
      if (!GameState.CurrentMap.InBounds(loc.Row, loc.Col))
        break;
      Tile tile = GameState.TileAt(loc);
      switch (tile.Type)
      {
        case TileType.DungeonWall:
          GameState.CurrentMap.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.DungeonFloor));
          break;
        case TileType.StoneWall:
        case TileType.WoodWall:
        case TileType.HWindow:
        case TileType.VWindow:
          GameState.CurrentMap.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.Dirt));
          break;
      }

      var blockages = GameState.ObjDb.ItemsAt(loc).Where(i => i.Type == ItemType.Landscape);
      foreach (Item block in blockages)
        GameState.ObjDb.RemoveItemFromGame(loc, block);
    }

    if (Source is WandTrait wand)
    {
      Item.IDInfo["wand of digging"] = Item.IDInfo["wand of digging"] with { Known = true };
      wand.Used();
    }
    else if (Source is IUSeable useable)
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
    result.Succcessful = true;

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
    List<Loc> pts = Trajectory(Actor!.Loc, true);

    var anim = new BeamAnimation(GameState!, pts, Colours.LIGHT_BLUE, Colours.WHITE);
    GameState!.UIRef().PlayAnimation(anim, GameState);

    foreach (var pt in pts)
    {
      GameState.ApplyDamageEffectToLoc(pt, DamageType.Cold);

      if (GameState.ObjDb.Occupant(pt) is Actor occ && occ != Actor)
      {
        int attackMod = 3;
        var attackResult = Battle.MagicAttack(Actor!, occ, GameState, ray, attackMod, null);
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
    result.Succcessful = true;

    Item missile = new()
    {
      Name = "magic missile",
      Type = ItemType.Weapon,
      Glyph = new Glyph('-', Colours.YELLOW_ORANGE, Colours.YELLOW_ORANGE, Colours.BLACK, Colours.BLACK)
    };
    missile.Traits.Add(new DamageTrait() { DamageDie = 6, NumOfDie = 2, DamageType = DamageType.Force });
    GameState!.ObjDb.Add(missile);

    List<Loc> pts = [];
    // I think I can probably clean this crap up
    foreach (var pt in Trajectory(Actor!.Loc, false))
    {
      var tile = GameState!.TileAt(pt);
      if (GameState.ObjDb.Occupant(pt) is Actor occ && occ != Actor)
      {
        pts.Add(pt);

        // I didn't want magic missile to be auto-hit like in D&D, but I'll give it a nice
        // attack bonus
        int attackMod = 5;
        var attackResult = Battle.MagicAttack(Actor!, occ, GameState, missile, attackMod, new ArrowAnimation(GameState!, pts, Colours.YELLOW_ORANGE));        
        if (attackResult.Succcessful)
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
    GameState!.UIRef().AlertPlayer("Pew pew pew!");

    return result;
  }
}

abstract class TargetedAction(GameState gs, Actor actor) : Action(gs, actor)
{
  protected Loc Target { get; set; }

  // As in, clear of obstacles, not opacity
  bool ClearTileAt(Loc loc)
  {
    Tile tile = GameState!.TileAt(loc);
    if (!(tile.Passable() || tile.PassableByFlight()))
      return false;
    if (GameState.ObjDb.BlockersAtLoc(loc))
      return false;

    return true;
  }

  protected List<Loc> Trajectory(Loc origin, bool filterBlockers)
  {
    if (filterBlockers)
      return [.. Util.Bresenham(origin.Row, origin.Col, Target.Row, Target.Col)
               .Select(p => new Loc(origin.DungeonID, origin.Level, p.Item1, p.Item2))
               .Where(l => ClearTileAt(l))];
    else
      return [.. Util.Bresenham(origin.Row, origin.Col, Target.Row, Target.Col)
                 .Select(p => new Loc(origin.DungeonID, origin.Level, p.Item1, p.Item2))];
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
    result.Succcessful = true;

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
        GameState.UIRef().AlertPlayer(txt);

        var confused = new ConfusedTrait() { DC = 15 };
        confused.Apply(Actor, GameState);
      }
      else
      {
        GameState.SwapActors(Actor!, victim);
        GameState.UIRef().AlertPlayer("Bamf!");
      }      
    }
    else
    {
      if (_source is IUSeable useable)
        useable.Used();
      GameState.UIRef().AlertPlayer("The magic is released but nothing happens. The spell fizzles.");
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
    result.Succcessful = true;


    if (GameState!.ObjDb.Occupant(_target) is Actor target)
    {
      if (target is Player)
      {
        GameState.UIRef().AlertPlayer("The magic is released but nothing happens. The spell fizzles.");
      }
      else
      {
        var healAction = new HealAction(GameState, target, 6, 2);
        result.AltAction = healAction;
        result.EnergyCost = 0.0;
        result.Succcessful = false;
      }

      if (_source is WandTrait wand)
      {
        Item.IDInfo["wand of heal monster"] = Item.IDInfo["wand of heal monster"] with { Known = true };
        wand.Used();
      }
    }
    else
    {
      GameState.UIRef().AlertPlayer("The magic is released but nothing happens. The spell fizzles.");
    }

    if (_source is IUSeable useable)
      useable.Used();

    return result;
  }

  public override void ReceiveUIResult(UIResult result) => _target = ((LocUIResult)result).Loc;
}

class InventoryChoiceAction(GameState gs, Actor actor, InventoryOptions opts, Action replacementAction) : Action(gs, actor)
{
  readonly InventoryOptions InvOptions = opts;
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

class ScatterAction(GameState gs, Actor actor) : Action(gs, actor)
{
  public override ActionResult Execute()
  {
    ActionResult result = new() { Succcessful = true, EnergyCost = 1.0 };

    // Scatter can be used to escape from being swallowed, so if the casting 
    // actor is player and they are swallowed, treat this like a BlinkAction 
    // instead.
    if (Actor is Player && Actor.HasTrait<SwallowedTrait>())
    {
      result.Succcessful = false;
      result.EnergyCost = 0.0;
      result.AltAction = new BlinkAction(GameState!, Actor);

      return result;
    }

    List<Loc> affected = [];
    foreach (var kvp in FieldOfView.CalcVisible(4, Actor!.Loc, GameState!.CurrentMap, GameState.ObjDb))
    {
      if (kvp.Value != Illumination.Full)
        continue;
      if (kvp.Key == Actor.Loc || !GameState.ObjDb.Occupied(kvp.Key))
        continue;
      affected.Add(kvp.Key);
    }
    
    List<Loc> landingSpots = [];
    for (int r = 0; r < GameState.CurrentMap.Height; r++)
    {
      for (int c = 0; c < GameState.CurrentMap.Width; c++)
      {
        Loc loc = new(GameState.CurrDungeonID, GameState.CurrLevel, r, c);

        if (GameState.TileAt(loc).Passable() && !GameState.ObjDb.Occupied(loc))
          landingSpots.Add(loc);
      }
    }

    if (Actor is Player) 
    {
      GameState.UIRef().AlertPlayer("Aroint thee!");
    }
    else if (GameState.LastPlayerFoV.Contains(Actor.Loc))
    {
      string s = $"{Actor.FullName.Capitalize()} {Grammar.Conjugate(Actor, "read")} a scroll! Poof!";
      GameState.UIRef().AlertPlayer(s);
    }
      
    foreach (var loc in affected)
    {
      if (GameState.ObjDb.Occupant(loc) is not Actor victim)
        continue;

      int i = GameState.Rng.Next(landingSpots.Count);
      Loc landingSpot = landingSpots[i];
      landingSpots.RemoveAt(i);

      GameState.UIRef().RegisterAnimation(new SqAnimation(GameState, loc, Colours.WHITE, Colours.LIGHT_PURPLE, '*'));
      GameState.UIRef().RegisterAnimation(new SqAnimation(GameState, landingSpot, Colours.WHITE, Colours.LIGHT_PURPLE, '*'));
      
      victim.ClearAnchors(GameState);
      GameState.ResolveActorMove(victim, loc, landingSpot);
    }

    return result;
  }
}

class UseSpellItemAction(GameState gs, Actor actor, string spell, Item? item) : Action(gs, actor)
{
  string Spell { get; set; } = spell;
  Item? Item { get; set; } = item;

  public override ActionResult Execute()
  {
    ActionResult result = new() { Succcessful = false, EnergyCost = 0.0 };

    Player player = GameState!.Player;
    switch (Spell)
    {
      case "gust of wind":
        player.ReplacePendingAction(
          new CastGustOfWindAction(GameState, player, Item) { FreeToCast = true }, 
          new ConeTargeter(GameState!, 5, player.Loc)
        );
        break;
    }

    return result;
  }
}

class UseWandAction(GameState gs, Actor actor, WandTrait wand, ulong wandId) : Action(gs, actor)
{
  readonly WandTrait _wand = wand;

  public override ActionResult Execute()
  {
    ActionResult result = new()
    {
      Succcessful = false,
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
        GameState!.UIRef().AlertPlayer("Which way?");
        break;
      case "slowmonster":
        inputer = new Aimer(GameState!, player.Loc, 9);
        player.ReplacePendingAction(new RayOfSlownessAction(GameState!, player, _wand, wandId), inputer);
        GameState!.UIRef().AlertPlayer("Which way?");
        break;
      case "digging":
        inputer = new Aimer(GameState!, player.Loc, 10);
        player.ReplacePendingAction(new DigRayAction(GameState!, player, _wand), inputer);
        GameState!.UIRef().AlertPlayer("Which way?");
        break;
      case "summoning":
        return HandleSummoning(result);
    }
    
    return result;
  }

  ActionResult HandleSummoning(ActionResult result)
  {
    // Kind of dorky to hardcore it, but otherwise I'd have to query ObjDb by the wand item ID to 
    // get it's name and currently it is always going to be wand of summoning...
    Item.IDInfo["wand of summoning"] = Item.IDInfo["wand of summoning"] with { Known = true };
    _wand.Used();
    
    // We don't need to replace the player's pending action here because 
    // there's no input needed from the player
    SummonAction summon = new(Actor!.Loc, GameState!.RandomMonster(Actor.Loc.DungeonID), 1)
    {
      GameState = GameState,
      Actor = Actor
    };
    result.AltAction = summon;

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

    return new ActionResult() { Succcessful = true, EnergyCost = 1.0 };
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
    return new ActionResult() { Succcessful = true, EnergyCost = _energyCost };
  }
}

// I guess I can later add extra info about whether or not the player died, quit,
// or quit and saved?
class QuitAction : Action
{
  public override ActionResult Execute() => throw new QuitGameException();
}

class SaveGameAction : Action
{
  public override ActionResult Execute() => throw new SaveGameException();
}

class NullAction : Action
{
  public override ActionResult Execute() => throw new Exception("Hmm this should never happen");
}