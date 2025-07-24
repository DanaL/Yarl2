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

using System.Collections.Generic;

namespace Yarl2;

enum TileType
{
  Unknown, WorldBorder, PermWall, DungeonWall, DungeonFloor, StoneFloor,
  StoneWall, ClosedDoor, OpenDoor, LockedDoor, BrokenDoor, HWindow, VWindow,
  DeepWater, Water, FrozenDeepWater, FrozenWater, Sand, Grass, Mountain,
  GreenTree, OrangeTree, RedTree, YellowTree, Conifer,
  SnowPeak, Portal, Upstairs, Downstairs, Cloud, WoodWall, WoodFloor, Forge,
  Dirt, StoneRoad, Well, Bridge, WoodBridge, Pool, FrozenPool,
  Landmark, Chasm, CharredGrass, CharredStump, Portcullis, OpenPortcullis,
  BrokenPortcullis, GateTrigger, VaultDoor, HiddenTrapDoor, TrapDoor,
  SecretDoor, HiddenTeleportTrap, TeleportTrap, HiddenDartTrap, DartTrap,
  FireJetTrap, JetTrigger, HiddenPit, Pit, WaterTrap, HiddenWaterTrap,
  MagicMouth, HiddenMagicMouth, IdolAltar, Gravestone, DisturbedGrave,
  BridgeTrigger, HiddenBridgeCollapseTrap, ReveealedBridgeCollapseTrap, Shortcut, 
  ShortcutDown, BusinessSign, FakeStairs, HiddenSummonsTrap, RevealedSummonsTrap,
  HFence, VFence, CornerFence, MonsterWall, Lever
}

interface ITriggerable
{
  void Trigger();
}

abstract class Tile(TileType type) : IZLevel
{
  public virtual TileType Type { get; protected set; } = type;
  public virtual string StepMessage => "";

  public int Z() => Type switch
  {
    TileType.Water => 6,
    TileType.DeepWater => 6,
    _ => 0
  };

  public abstract bool Passable();
  public abstract bool PassableByFlight();
  public abstract bool Opaque();

  public bool IsTree() => Type switch
  {
    TileType.GreenTree => true,
    TileType.RedTree => true,
    TileType.YellowTree => true,
    TileType.OrangeTree => true,
    TileType.Conifer => true,
    _ => false
  };

  public bool SoundProof() => Type switch
  {
    TileType.WorldBorder => true,
    TileType.DungeonWall => true,
    TileType.PermWall => true,
    TileType.WoodWall => true,
    TileType.ClosedDoor => true,
    TileType.LockedDoor => true,
    TileType.Mountain => true,
    TileType.SnowPeak => true,
    TileType.VaultDoor => true,
    TileType.SecretDoor => true,
    _ => false
  };

  public bool Flammable() => Type switch
  {
    TileType.GreenTree => true,
    TileType.RedTree => true,
    TileType.OrangeTree => true,
    TileType.YellowTree => true,
    TileType.Conifer => true,
    TileType.Grass => true,
    TileType.WoodBridge => true,
    _ => false
  };

  public bool IsVisibleTrap() => Type switch
  {
    TileType.TrapDoor => true,
    TileType.TeleportTrap => true,
    TileType.DartTrap => true,
    TileType.Pit => true,
    TileType.WaterTrap => true,
    TileType.RevealedSummonsTrap => true,
    TileType.ReveealedBridgeCollapseTrap => true,
    TileType.MagicMouth => true,    
    _ => false
  };

  public bool IsTrap() => Type switch
  {
    TileType.TrapDoor => true,
    TileType.HiddenTrapDoor => true,
    TileType.HiddenTeleportTrap => true,
    TileType.TeleportTrap => true,
    TileType.HiddenDartTrap => true,
    TileType.DartTrap => true,
    TileType.JetTrigger => true,
    TileType.HiddenPit => true,
    TileType.Pit => true,
    TileType.WaterTrap => true,
    TileType.HiddenWaterTrap => true,
    TileType.MagicMouth => true,
    TileType.HiddenMagicMouth => true,
    TileType.HiddenBridgeCollapseTrap => true,
    TileType.ReveealedBridgeCollapseTrap => true,
    TileType.HiddenSummonsTrap => true,
    TileType.RevealedSummonsTrap => true,
    _ => false
  };

  public static string TileDesc(TileType type) => type switch
  {
    TileType.Water => "water",
    TileType.DeepWater => "deep water",
    TileType.PermWall => "a wall",
    TileType.DungeonWall => "a wall",
    TileType.StoneWall => "a wall",
    TileType.WoodWall => "a wall",
    TileType.DungeonFloor => "stone floor",
    TileType.StoneFloor => "stone floor",
    TileType.WoodFloor => "wood floor",
    TileType.Mountain => "a mountain",
    TileType.SnowPeak => "a mountain",
    TileType.GreenTree => "trees",
    TileType.YellowTree => "trees",
    TileType.RedTree => "trees",
    TileType.OrangeTree => "trees",
    TileType.Conifer => "trees",
    TileType.Grass => "grass",
    TileType.OpenDoor => "an open door",
    TileType.BrokenDoor => "a broken door",
    TileType.ClosedDoor => "a closed door",
    TileType.LockedDoor => "a locked door",
    TileType.HWindow => "a window",
    TileType.VWindow => "a window",
    TileType.Sand => "sand",
    TileType.Dirt => "dirt path",
    TileType.StoneRoad => "ancient flagstones",
    TileType.Well => "a well",
    TileType.Bridge => "a bridge",
    TileType.WoodBridge => "a wood bridge",
    TileType.Chasm => "a chasm",
    TileType.Landmark => "a landmark",
    TileType.Forge => "a forge",
    TileType.Shortcut => "some stairs up",
    TileType.Upstairs => "some stairs up",
    TileType.ShortcutDown => "some stairs down",
    TileType.Downstairs => "some stairs down",
    TileType.Portal => "a dungeon entrance",
    TileType.CharredGrass => "charred grass",
    TileType.CharredStump => "charred stump",
    TileType.FrozenWater => "ice",
    TileType.FrozenDeepWater => "ice",
    TileType.Portcullis => "portcullis",
    TileType.OpenPortcullis => "open portcullis",
    TileType.BrokenPortcullis => "broken portcullis",
    TileType.GateTrigger => "pressure plate",
    TileType.VaultDoor => "vault door",
    TileType.HiddenTrapDoor => "stone floor",
    TileType.TrapDoor => "trap door",
    TileType.SecretDoor => "a wall",
    TileType.HiddenTeleportTrap => "stone floor",
    TileType.TeleportTrap => "teleport trap",
    TileType.HiddenDartTrap => "stone floor",
    TileType.DartTrap => "dart trap",
    TileType.JetTrigger => "trigger",
    TileType.FireJetTrap => "fire jet",
    TileType.HiddenPit => "stone floor",
    TileType.Pit => "pit",
    TileType.HiddenWaterTrap => "stone floor",
    TileType.WaterTrap => "water trap",
    TileType.MagicMouth => "a magic mouth",
    TileType.HiddenMagicMouth => "stone floor",
    TileType.Pool => "a pool",
    TileType.FrozenPool => "ice",
    TileType.Gravestone => "a gravestone",
    TileType.DisturbedGrave => "a disturbed grave",
    TileType.BridgeTrigger => "pressure plate",
    TileType.HiddenBridgeCollapseTrap => "stone floor",
    TileType.BusinessSign => "a sign",
    TileType.FakeStairs => "stairs",
    TileType.HiddenSummonsTrap => "stone floor",
    TileType.HFence => "fence",
    TileType.VFence => "fence",
    TileType.CornerFence => "fence",
    TileType.RevealedSummonsTrap => "monster summon trap",
    TileType.ReveealedBridgeCollapseTrap => "bridge collapse trigger",
    TileType.Lever => "a lever",
    _ => "unknown"
  };

  public List<DamageType> TerrainEffects()
  {
    List<DamageType> flags = [];

    switch (Type)
    {
      case TileType.Water:
      case TileType.DeepWater:
        flags.Add(DamageType.Wet);
        break;
    }

    return flags;
  }
}

class BasicTile : Tile
{
  readonly bool _passable;
  readonly bool _passableByFlight;
  readonly bool _opaque;
  readonly string _stepMessage;

  public override bool Passable() => _passable;
  public override bool PassableByFlight() => _passableByFlight;
  public override bool Opaque() => _opaque;
  public override string StepMessage => _stepMessage;

  public BasicTile(TileType type, bool passable, bool opaque, bool passableByFlight) : base(type)
  {
    _passable = passable;
    _opaque = opaque;
    _stepMessage = "";
    _passableByFlight = passableByFlight;
  }

  public BasicTile(TileType type, bool passable, bool opaque, bool passableByFlight, string stepMessage) : base(type)
  {
    _passable = passable;
    _opaque = opaque;
    _stepMessage = stepMessage;
    _passableByFlight = passableByFlight;
  }
}

class Door(TileType type, bool open) : Tile(type)
{
  public bool Open { get; set; } = open;

  // Not sure if this is a good way to handle this for places like 
  // the pathfinding code or if it's a gross hack
  public override TileType Type => Open ? TileType.OpenDoor : base.Type;
  public override bool Passable() => Open;
  public override bool PassableByFlight() => Open;
  public override bool Opaque() => !Open;

  public override string ToString() => $"{(int)Type};{Open}";
}

class FireJetTrap(bool seen, Dir dir) : Tile(TileType.FireJetTrap)
{
  public DamageType Damage { get; set; } = DamageType.Fire;
  public bool Seen { get; set; } = seen;
  public Dir Dir { get; set; } = dir;

  public override bool Opaque() => true;
  public override bool Passable() => false;
  public override bool PassableByFlight() => false;

  public override string ToString() => $"{(int)Type};{Seen};{Dir}";
}

class JetTrigger(Loc jetLoc, bool visible) : Tile(TileType.JetTrigger)
{
  public Loc JetLoc { get; set; } = jetLoc;
  public bool Visible { get; set; } = visible;

  public override bool Opaque() => false;
  public override bool Passable() => true;
  public override bool PassableByFlight() => true;

  public override string ToString() => $"{(int)Type};{JetLoc};{Visible}";
}

// Portcullis is pretty close to the door, but I didn't want to connect them
// in a class hierarchy because I didn't want to have to worry about bugs 
// where I was doing, like, "if (foo is Door) { ... }" and implicitly treat
// a portcullis like a door when I didn't intend to.
class Portcullis(bool open) : Tile(TileType.Portcullis), ITriggerable
{
  public bool Open { get; set; } = open;

  public override TileType Type => Open ? TileType.OpenPortcullis : TileType.Portcullis;

  public override bool Passable() => Open;
  public override bool PassableByFlight() => Open;
  public override bool Opaque() => false;

  public override string ToString() => $"{(int)Type};{Open}";

  public void Trigger() => Open = !Open;  
}

class GateTrigger(Loc gate) : Tile(TileType.GateTrigger), IGameEventListener
{
  public Loc Gate { get; set; } = gate;
  public ulong ObjId => 0;
  public ulong SourceId { get; set; }

  public bool Found { get; set; } = false;

  public override bool Passable() => true;
  public override bool PassableByFlight() => true;
  public override bool Opaque() => false;

  public override string ToString() => $"{(int)Type};{Gate};{Found}";

  public bool Expired { get; set; }
  public bool Listening => true;
  public GameEventType EventType => GameEventType.LocChanged;

  public void EventAlert(GameEventType eventType, GameState gs, Loc loc)
  {
    if (gs.TileAt(Gate) is Portcullis portcullis)
    {
      portcullis.Trigger();  
      if (gs.LastPlayerFoV.Contains(loc))
        Found = true;
      if (loc == gs.Player.Loc)
        gs.Player.HaltTravel();
      gs.Noise(Gate.Row, Gate.Col, 7);
      gs.UIRef().AlertPlayer("You hear a metallic grinding!");
    }
  }
}

class VaultDoor(bool open, Metals material) : Tile(TileType.VaultDoor)
{
  public Metals Material { get; set; } = material;
  public bool Open { get; set; } = open;

  public override bool Passable() => Open;
  public override bool PassableByFlight() => Open;
  public override bool Opaque() => false;

  public override string ToString() => $"{(int)Type};{Open};{Material}";
}

class Portal(string stepMessage) : Tile(TileType.Portal)
{
  private readonly string _stepMessage = stepMessage;
  public Loc Destination { get; set; }
  public override bool Passable() => true;
  public override bool PassableByFlight() => true;
  public override bool Opaque() => false;

  public override string StepMessage => _stepMessage;

  public override string ToString()
  {
    return $"{(int)Type};{Destination};{_stepMessage}";
  }
}

class Shortcut : Portal
{
  public Shortcut()  : base("A long stairway extending upwards.") => Type = TileType.Shortcut;

  public override string ToString() => base.ToString();
}

class ShortcutDown : Portal
{
  public ShortcutDown()  : base("A long stairway extending into darkness.") => Type = TileType.ShortcutDown;

  public override string ToString() => base.ToString();
}

class Upstairs : Portal
{
  public Upstairs(string stepMessage) : base(stepMessage) => Type = TileType.Upstairs;

  public override string ToString() => base.ToString();
}

class Downstairs : Portal
{
  public Downstairs(string stepMessage) : base(stepMessage) => Type = TileType.Downstairs;

  public override string ToString() => base.ToString();
}

class Landmark(string stepMessage) : Tile(TileType.Landmark)
{
  private readonly string _stepMessage = stepMessage;
  public override bool Passable() => true;
  public override bool PassableByFlight() => true;
  public override bool Opaque() => false;

  public override string StepMessage => _stepMessage;

  public override string ToString() => $"{(int)Type};{_stepMessage}";
}

class Gravestone : Landmark
{
  public Gravestone(string stepMessage) : base(stepMessage) => Type = TileType.Gravestone;
}

class IdolAltar : Landmark
{
  public ulong IdolID { get; set; }
  public IdolAltar(string stepMessage) : base(stepMessage) => Type = TileType.IdolAltar;
  public Loc Wall { get; set; }

  public override string ToString() => $"{(int)Type};{StepMessage};{IdolID};{Wall}";
}

class BridgeCollapseTrap() : Tile(TileType.HiddenBridgeCollapseTrap), IGameEventListener
{
  public override bool Passable() => true;
  public override bool PassableByFlight() => true;
  public override bool Opaque() => false;
  public bool Triggered { get; set; } = false;
  public bool Expired { get; set; }
  public bool Listening => true;
  public HashSet<Loc> BridgeTiles { get; set; } = [];
  public ulong SourceId { get; set; }
  public GameEventType EventType => GameEventType.LocChanged;
  public ulong ObjId => 0;

  public override string ToString() => $"{(int)Type};{Triggered};{string.Join('|', BridgeTiles)}";

  public void Reveal() => Type = TileType.ReveealedBridgeCollapseTrap;

  public void EventAlert(GameEventType eventType, GameState gs, Loc loc)
  {
    if (!Triggered)
    {
      Triggered = true;

      foreach (Loc bridgeLoc in BridgeTiles)
      {
        gs.BridgeDestroyed(bridgeLoc);
      }

      if (gs.LastPlayerFoV.Contains(loc))
      {        
        string s = "The bridge collapses into the chasm!";
        gs.UIRef().AlertPlayer(s);
        gs.UIRef().SetPopup(new Popup(s, "", -1, -1));
      }
      else
      {
        gs.UIRef().AlertPlayer("You hear a distant crashing sound!");
      }
    }    
  }
}

class BridgeTrigger() : Tile(TileType.BridgeTrigger), IGameEventListener
{
  public override bool Passable() => true;
  public override bool PassableByFlight() => true;
  public override bool Opaque() => false;
  public bool Triggered { get; set; } = false;
  public bool Expired { get; set; }
  public bool Listening => true;
  public HashSet<Loc> BridgeTiles { get; set; } = [];
  public ulong SourceId { get; set; }
  public GameEventType EventType => GameEventType.LocChanged;

  public ulong ObjId => 0;

  public override string ToString() => 
    $"{(int)Type};{Triggered};{string.Join('|', BridgeTiles)}";

  public void EventAlert(GameEventType eventType, GameState gs, Loc loc)
  {
    if (!Triggered)
    {
      Map map = gs.Campaign.Dungeons[loc.DungeonID].LevelMaps[loc.Level];
      foreach (Loc bridgeLoc in BridgeTiles)
      {
        map.SetTile(bridgeLoc.Row, bridgeLoc.Col, TileFactory.Get(TileType.WoodBridge));
      }
      Triggered = true;

      if (gs.LastPlayerFoV.Contains(loc))
      {
        string s = "You hear the sound of machinery as a bridge rises!";
        gs.UIRef().AlertPlayer(s);
        gs.UIRef().SetPopup(new Popup(s, "", -1, -1));
      }
      else
      {
        gs.UIRef().AlertPlayer("You hear a faint sound of machinery.");
      }
    }    
  }
}

class BusinessSign(string stepMessage) : Tile(TileType.BusinessSign)
{
  public override bool Opaque() => false;
  public override bool Passable() => true;
  public override bool PassableByFlight() => true;

  readonly string _stepMessage = stepMessage;
  public override string StepMessage => _stepMessage;

  public override string ToString() => $"{(int)Type};{_stepMessage}";
}

class MonsterWall(Glyph glyph, ulong monsterId) : Tile(TileType.MonsterWall)
{
  public Glyph Glyph { get; set; } = glyph;
  public ulong MonsterId { get; set; } = monsterId;

  public override bool Opaque() => true;
  public override bool Passable() => false;
  public override bool PassableByFlight() => false;

  public override string ToString() => $"{(int)Type};{Glyph};{MonsterId}";
}

class Lever(TileType type, bool on, Loc gate) : Tile(type)
{
  public bool On { get; set; } = on;
  public Loc Gate { get; set; } = gate;

  // Not sure if this is a good way to handle this for places like 
  // the pathfinding code or if it's a gross hack
  public override bool Passable() => false;
  public override bool PassableByFlight() => false;
  public override bool Opaque() => false;

  public override string ToString() => $"{(int)Type};{On};{Gate}";

  public void Activate(GameState gs)
  {
    if (gs.TileAt(Gate) is Portcullis portcullis)
    {
      On = !On;
      portcullis.Trigger();      
      gs.Noise(Gate.Row, Gate.Col, 7);
      gs.UIRef().AlertPlayer("You hear a metallic grinding!");
    }
  }
}

class TileFactory
{
  static readonly Tile WorldBorder = new BasicTile(TileType.WorldBorder, false, true, false);
  static readonly Tile Unknown = new BasicTile(TileType.Unknown, false, true, false);
  static readonly Tile DungeonWall = new BasicTile(TileType.DungeonWall, false, true, false);
  static readonly Tile StoneWall = new BasicTile(TileType.StoneWall, false, true, false);
  static readonly Tile PermWall = new BasicTile(TileType.PermWall, false, true, false);
  static readonly Tile Floor = new BasicTile(TileType.DungeonFloor, true, false, true);
  static readonly Tile StoneFloor = new BasicTile(TileType.StoneFloor, true, false, true);
  static readonly Tile DeepWater = new BasicTile(TileType.DeepWater, false, false, true);
  static readonly Tile Grass = new BasicTile(TileType.Grass, true, false, true);
  static readonly Tile Sand = new BasicTile(TileType.Sand, true, false, true);
  static readonly Tile GreenTree = new BasicTile(TileType.GreenTree, true, false, true);
  static readonly Tile YellowTree = new BasicTile(TileType.YellowTree, true, false, true);
  static readonly Tile RedTree = new BasicTile(TileType.RedTree, true, false, true);
  static readonly Tile OrangeTree = new BasicTile(TileType.OrangeTree, true, false, true);
  static readonly Tile Conifer = new BasicTile(TileType.Conifer, true, false, true);
  static readonly Tile Mountain = new BasicTile(TileType.Mountain, false, true, false);
  static readonly Tile SnowPeak = new BasicTile(TileType.Mountain, false, true, false);
  static readonly Tile Cloud = new BasicTile(TileType.Cloud, true, false, true);
  static readonly Tile Water = new BasicTile(TileType.Water, true, false, true, "You splash into the water.");
  static readonly Tile HWindow = new BasicTile(TileType.HWindow, false, false, false);
  static readonly Tile VWindow = new BasicTile(TileType.VWindow, false, false, false);
  static readonly Tile WoodWall = new BasicTile(TileType.WoodWall, false, true, false);
  static readonly Tile WoodFloor = new BasicTile(TileType.WoodFloor, true, false, true);
  static readonly Tile Forge = new BasicTile(TileType.Forge, true, false, true);
  static readonly Tile Dirt = new BasicTile(TileType.Dirt, true, false, true);
  static readonly Tile StoneRoad = new BasicTile(TileType.StoneRoad, true, false, true);
  static readonly Tile Well = new BasicTile(TileType.Well, true, false, true);
  static readonly Tile Bridge = new BasicTile(TileType.Bridge, true, false, true);
  static readonly Tile WoodBridge = new BasicTile(TileType.WoodBridge, true, false, true);
  static readonly Tile Chasm = new BasicTile(TileType.Chasm, false, false, true);
  static readonly Tile CharredGrass = new BasicTile(TileType.CharredGrass, true, false, true);
  static readonly Tile CharredStump = new BasicTile(TileType.CharredStump, true, false, true);
  static readonly Tile FrozenDeepWater = new BasicTile(TileType.FrozenDeepWater, true, false, true);
  static readonly Tile FrozenWater = new BasicTile(TileType.FrozenWater, true, false, true);
  static readonly Tile HiddenTrapDoor = new BasicTile(TileType.HiddenTrapDoor, true, false, true);
  static readonly Tile TrapDoor = new BasicTile(TileType.TrapDoor, true, false, true);
  static readonly Tile SecretDoor = new BasicTile(TileType.SecretDoor, false, true, false);
  static readonly Tile BrokenDoor = new BasicTile(TileType.BrokenDoor, true, false, true);
  static readonly Tile TeleportTrap = new BasicTile(TileType.HiddenTeleportTrap, true, false, true);
  static readonly Tile VisibileTeleportTrap = new BasicTile(TileType.TeleportTrap, true, false, true);
  static readonly Tile BrokenPortcullis = new BasicTile(TileType.BrokenPortcullis, true, false, true);
  static readonly Tile HiddenDartTrap = new BasicTile(TileType.HiddenDartTrap, true, false, true);
  static readonly Tile DartTrap = new BasicTile(TileType.DartTrap, true, false, true);
  static readonly Tile HiddenPit = new BasicTile(TileType.HiddenPit, true, false, true);
  static readonly Tile Pit = new BasicTile(TileType.Pit, true, false, true);
  static readonly Tile HiddenWaterTrap = new BasicTile(TileType.HiddenWaterTrap, true, false, true);
  static readonly Tile WaterTrap = new BasicTile(TileType.WaterTrap, true, false, true);
  static readonly Tile MagicMouth = new BasicTile(TileType.MagicMouth, true, false, true);
  static readonly Tile HiddenMagicMouth = new BasicTile(TileType.HiddenMagicMouth, true, false, true);
  static readonly Tile Pool = new BasicTile(TileType.Pool, true, false, true);
  static readonly Tile FrozenPool = new BasicTile(TileType.FrozenPool, true, false, true);
  static readonly Tile DisturbedGrave = new BasicTile(TileType.DisturbedGrave, true, false, true);
  static readonly Tile FakeStairs = new BasicTile(TileType.FakeStairs, true, false, true);
  static readonly Tile HiddenSummonsTrap = new BasicTile(TileType.HiddenSummonsTrap, true, false, true);
  static readonly Tile RevealedSummonsTrap = new BasicTile(TileType.RevealedSummonsTrap, true, false, true);
  static readonly Tile HFence = new BasicTile(TileType.HFence, false, false, true);
  static readonly Tile VFence = new BasicTile(TileType.VFence, false, false, true);
  static readonly Tile CornerFence = new BasicTile(TileType.CornerFence, false, false, true);
    
  public static Tile Get(TileType type) => type switch
  {
    TileType.WorldBorder => WorldBorder,
    TileType.PermWall => PermWall,
    TileType.DungeonWall => DungeonWall,
    TileType.StoneWall => StoneWall,
    TileType.DungeonFloor => Floor,
    TileType.StoneFloor => StoneFloor,
    TileType.DeepWater => DeepWater,
    TileType.Sand => Sand,
    TileType.Grass => Grass,
    TileType.GreenTree => GreenTree,
    TileType.RedTree => RedTree,
    TileType.YellowTree => YellowTree,
    TileType.OrangeTree => OrangeTree,
    TileType.Conifer => Conifer,
    TileType.Mountain => Mountain,
    TileType.SnowPeak => SnowPeak,
    TileType.ClosedDoor => new Door(type, false),
    TileType.LockedDoor => new Door(type, false),
    TileType.Water => Water,
    TileType.Cloud => Cloud,
    TileType.HWindow => HWindow,
    TileType.VWindow => VWindow,
    TileType.WoodFloor => WoodFloor,
    TileType.WoodWall => WoodWall,
    TileType.Forge => Forge,
    TileType.Dirt => Dirt,
    TileType.Well => Well,
    TileType.Bridge => Bridge,
    TileType.WoodBridge => WoodBridge,
    TileType.Chasm => Chasm,
    TileType.StoneRoad => StoneRoad,
    TileType.CharredGrass => CharredGrass,
    TileType.CharredStump => CharredStump,
    TileType.FrozenDeepWater => FrozenDeepWater,
    TileType.FrozenWater => FrozenWater,
    TileType.HiddenTrapDoor => HiddenTrapDoor,
    TileType.TrapDoor => TrapDoor,
    TileType.SecretDoor => SecretDoor,
    TileType.BrokenDoor => BrokenDoor,
    TileType.HiddenTeleportTrap => TeleportTrap,
    TileType.TeleportTrap => VisibileTeleportTrap,
    TileType.BrokenPortcullis => BrokenPortcullis,
    TileType.DartTrap => DartTrap,
    TileType.HiddenDartTrap => HiddenDartTrap,
    TileType.HiddenPit => HiddenPit,
    TileType.Pit => Pit,
    TileType.HiddenWaterTrap => HiddenWaterTrap,
    TileType.WaterTrap => WaterTrap,
    TileType.MagicMouth => MagicMouth,
    TileType.HiddenMagicMouth => HiddenMagicMouth,
    TileType.Pool => Pool,
    TileType.DisturbedGrave => DisturbedGrave,
    TileType.FakeStairs => FakeStairs,
    TileType.HiddenSummonsTrap => HiddenSummonsTrap,
    TileType.RevealedSummonsTrap => RevealedSummonsTrap,
    TileType.HFence => HFence,
    TileType.VFence => VFence,
    TileType.CornerFence => CornerFence,
    _ => Unknown
  };
}

class Map : ICloneable
{
  public readonly int Width;
  public readonly int Height;

  public Tile[] Tiles;
  public List<string> Alerts = [];

  public Map(int width, int height)
  {
    Width = width;
    Height = height;

    Tiles = new Tile[Height * Width];
  }

  public Map(int width, int height, TileType type)
  {
    Width = width;
    Height = height;
    Tiles = Enumerable.Repeat(TileFactory.Get(type), Width * Height).ToArray();
  }

  public bool IsTile((int, int) pt, TileType type) => InBounds(pt) && TileAt(pt).Type == type;

  public bool InBounds(int row, int col) => row >= 0 && row < Height && col >= 0 && col < Width;
  public bool InBounds((int, int) loc) => loc.Item1 >= 0 && loc.Item1 < Height && loc.Item2 >= 0 && loc.Item2 < Width;

  // I'll need to search out a bunch of dungeon floors (the main use for this function) so I 
  // should build up a list of random floors and pick from among them instead of randomly
  // trying squares. (And remove from list when I SetTile())...
  // Potential infinite loop alert D:
  public (int, int) RandomTile(TileType type, Rng rng)
  {
    do
    {
      int r = rng.Next(Height);
      int c = rng.Next(Width);

      if (TileAt(r, c).Type == type)
        return (r, c);
    }
    while (true);
  }

  // List of floors that are good spots to place items or mobs. Should be
  // free of other occuptants, rubble/statues, or hazards like campfires
  public List<Loc> ClearFloors(int dungeonId, int level, GameObjectDB objDb)
  {     
    List<Loc> floors = [];
    for (int r = 0; r < Height; r++)
    {
      for (int c = 0; c <Width; c++)
      {
        Tile tile = TileAt(r, c);
        if (tile.Type != TileType.DungeonFloor)
          continue;
        Loc loc = new(dungeonId, level, r, c);
        if (objDb.Occupied(loc) || objDb.AreBlockersAtLoc(loc) || objDb.HazardsAtLoc(loc))
          continue;
        floors.Add(loc);
      }
    }

    return floors;
  }

  static bool IsRoomFloorTile(TileType type) => type switch
  {
    TileType.DungeonFloor => true,
    TileType.Upstairs => true,
    TileType.Downstairs => true,
    _ => false
  };

  public List<(int, int)> SqsOfType(TileType type)
  {
    List<(int, int)> sqs = [];
    for (int r = 0; r < Height; r++)
    {
      for (int c = 0; c < Width; c++)
      {
        if (TileAt(r, c).Type == type)
          sqs.Add((r, c));
      }
    }

    return sqs;
  }

  // Find rooms -- flood fill to find areas on map that are rooms,
  // ie contiguous floor squares at least 9x9 and no longer than 16x16
  public List<List<(int, int)>> FindRooms(int minSize)
  {
    List<List<(int, int)>> rooms = [];
    var visited = new bool[Height, Width];

    // Scan through map looking for unvisited floor tiles
    for (int r = 1; r < Height - 1; r++)
    {
      for (int c = 1; c < Width - 1; c++)
      {        
        if (!visited[r, c] && IsRoomFloorTile(TileAt(r, c).Type) && IsRoomSq(r, c))
        {
          // Found a new potential room tile (has 8 adjacent floors), flood fill from this point
          List<(int r, int c)> floors = [];
          RoomFloodFill(r, c, visited, floors);

          if (floors.Count > minSize && floors.Count <= 256)
          {
            // I'm not actually checking/rejecting long, narrow rooms (like
            // say a 2x6 room) but the dungeon generator doesn't generally
            // make rooms of that shape.
            rooms.Add(floors);
          }
        }
      }
    }

    return rooms;
  }

  bool IsRoomSq(int r, int c)
  {
    int adjFloors = Util.Adj8Sqs(r, c).Count(sq => IsRoomFloorTile(TileAt(sq).Type));
    return adjFloors >= 3; // Should it be 3 because of corners? But that doesn't really matter
                           // for my purposes, I don't think.
  }

  void RoomFloodFill(int r, int c, bool[,] visited, List<(int r, int c)> floors)
  {
    if (!InBounds(r, c)) 
      return;
    if (visited[r, c]) 
      return;
    if (!IsRoomFloorTile(TileAt(r, c).Type))
      return;
    if (!IsRoomSq(r, c))
      return;
    
    // Is the tile an an exit, which I'm defining as door or passable tile with 
    // walls to the left and right and open spaces up and down. So
    //
    //  .....       ..#..
    //  ##.##  or   .....
    //  .....       ..#..
    if (TileAt(r-1, c).Type == TileType.DungeonWall && TileAt(r+1, c).Type == TileType.DungeonWall
      && IsRoomFloorTile(TileAt(r, c-1).Type) && IsRoomFloorTile(TileAt(r, c+1).Type))
    {
      return;
    }
    if (TileAt(r, c-1).Type == TileType.DungeonWall && TileAt(r, c+1).Type == TileType.DungeonWall
      && IsRoomFloorTile(TileAt(r-1, c).Type) && IsRoomFloorTile(TileAt(r+1, c).Type))
    {
      return;
    }

    // Mark as visited and add to room
    visited[r, c] = true;
    floors.Add((r, c));

    RoomFloodFill(r - 1, c, visited, floors);
    RoomFloodFill(r + 1, c, visited, floors);
    RoomFloodFill(r, c - 1, visited, floors);
    RoomFloodFill(r, c + 1, visited, floors);
  }

  public void SetTile(int row, int col, Tile tile) => Tiles[row * Width + col] = tile;
  public void SetTile((int, int) loc, Tile tile) => Tiles[loc.Item1 * Width + loc.Item2] = tile;

  public Tile TileAt(int row, int col) => Tiles[row * Width + col];
  public Tile TileAt((int, int) loc) => Tiles[loc.Item1 * Width + loc.Item2];

  public void Dump()
  {
    for (int row = 0; row < Height; row++)
    {
      for (int col = 0; col < Width; col++)
      {
        char ch = Tiles[row * Width + col].Type switch
        {
          TileType.WorldBorder => ' ',
          TileType.PermWall => '#',
          TileType.DungeonWall => '#',
          TileType.DungeonFloor or TileType.Sand => '.',
          TileType.ClosedDoor or TileType.LockedDoor => '+',
          TileType.Mountain or TileType.SnowPeak => '^',
          TileType.Grass => ',',
          TileType.GreenTree => 'T',
          TileType.RedTree => 'T',
          TileType.OrangeTree => 'T',
          TileType.YellowTree => 'T',
          TileType.DeepWater => '~',
          TileType.WoodBridge => '=',
          TileType.Upstairs => '<',
          TileType.Downstairs => '>',
          TileType.VaultDoor => '|',
          TileType.OpenPortcullis => '|',
          TileType.Portcullis => '|',
          _ => ' '
        };
        Console.Write(ch);
      }
      Console.WriteLine();
    }
  }

  public void DumpMarkRooms(List<List<(int,int)>> rooms)
  {
    HashSet<(int, int)> floors = new();
    foreach (List<(int, int)> room in rooms)
    {
      foreach ((int r, int c) in room)
      {
        floors.Add((r, c));
      }
    }

    for (int row = 0; row < Height; row++)
    {
      for (int col = 0; col < Width; col++)
      {
        if (floors.Contains((row, col)))
        {
          Console.Write('*');
          continue;
        }

        char ch = Tiles[row * Width + col].Type switch
        {
          TileType.WorldBorder => ' ',
          TileType.PermWall => '#',
          TileType.DungeonWall => '#',
          TileType.DungeonFloor or TileType.Sand => '.',
          TileType.ClosedDoor or TileType.LockedDoor => '+',
          TileType.Mountain or TileType.SnowPeak => '^',
          TileType.Grass => ',',
          TileType.GreenTree => 'T',
          TileType.RedTree => 'T',
          TileType.OrangeTree => 'T',
          TileType.YellowTree => 'T',
          TileType.DeepWater => '~',
          TileType.WoodBridge => '=',
          TileType.Upstairs => '<',
          TileType.Downstairs => '>',
          TileType.VaultDoor => '|',
          TileType.OpenPortcullis => '|',
          TileType.Portcullis => '|',
          _ => ' '
        };
        Console.Write(ch);
      }
      Console.WriteLine();
    }
  }

  public object Clone()
  {
    var temp = new Map(Width, Height);
    if (Tiles is not null)
      temp.Tiles = (Tile[])Tiles.Clone();

    return temp;
  }
}

// False == wall, true == floor
class CACave
{
  static bool[,] Iteration(bool[,] map, int height, int width)
  {
    var next = new bool[height, width];

    for (int r = 0; r < height; r++)
    {
      for (int c = 0; c < width; c++)
      {
        if (r == 0 || r == height - 1 || c == 0 || c == width - 1)
        {
          next[r, c] = false;
        }
        else
        {
          int adj = !map[r, c] ? 1 : 0;
          foreach (var sq in Util.Adj8Sqs(r, c))
          {
            if (!map[sq.Item1, sq.Item2])
              ++adj;
          }

          next[r, c] = adj < 5;
        }
      }
    }

    return next;
  }

  public static bool[,] GetCave(int height, int width, Rng rng)
  {
    var template = new bool[height, width];

    for (int r = 0; r < height; r++)
    {
      for (int c = 0; c < width; c++)
      {
        template[r, c] = rng.NextDouble() > 0.45;
      }
    }

    for (int j = 0; j < 4; j++)
    {
      template = Iteration(template, height, width);
    }

    return template;
  }
}

// Tower/mansion style map I'll use Binary Space Partitioning to build
class Tower(int height, int width, int minLength)
{
  const int VERTICAL = 0;
  const int HORIZONTAL = 1;

  int Height { get; set; } = height;
  int Width { get; set; } = width;
  int MinLength { get; set; } = minLength;

  void Partition(bool[,] map, int tr, int lc, int br, int rc, Rng rng)
  {
    List<int> options = [];
    if (br - tr > MinLength)
      options.Add(HORIZONTAL);
    if (rc - lc > MinLength)
      options.Add(VERTICAL);

    // We're done recursing
    if (options.Count == 0)
      return;

    int choice = options[rng.Next(options.Count)];

    if (choice == VERTICAL)
    {
      int a = int.Min(lc + MinLength, Width - 1), b = int.Max(0, rc - MinLength);
      int col;
      if (a == b)
        col = a;
      else
        col = rng.Next(int.Min(a, b), int.Max(a, b));
      
      for (int r = tr; r <= br; r++)
      {
        map[r, col] = true;
      }

      Partition(map, tr, lc, br, col, rng);
      Partition(map, tr, col + 1, br, rc, rng);
    }
    else 
    {
      int a = int.Min(tr + MinLength, Height - 1), b = int.Max(0, br - MinLength);
      int row;
      if (a == b)
        row = a;
      else
        row = rng.Next(int.Min(a, b), int.Max(a, b));

      for (int c = lc; c <= rc; c++)
      {
        map[row, c] = true;
      }

      Partition(map, tr, lc, row, rc, rng);
      Partition(map, row + 1, lc, br, rc, rng);
    }
  }

  static List<Room> FindRooms(Map map)
  {
    RegionFinder rf = new(new DungeonPassable());
    Dictionary<int, HashSet<(int, int)>> regions = rf.Find(map, false, 0, TileType.DungeonFloor);

    // Convert the hashset of floor tiles to Room objects
    List<Room> rooms = [];
    foreach (var room in regions.Values)
    {
      Room r = new() { Sqs = room };

      foreach ((int row, int col) in room)
      {
        foreach (var sq in Util.Adj8Sqs(row, col))
        {
          if (map.TileAt(sq).Type == TileType.DungeonWall)
            r.Perimeter.Add(sq);
        }
      }

      rooms.Add(r);
    }

    return rooms;
  }

  void Dump(bool[,] map)
  {
    for (int r = 0; r < Height; r++)
    {
      for (int c = 0; c < Width; c++)
      {
        char ch = map[r, c] ? '#' : '.';
        Console.Write(ch);
      }
      Console.WriteLine();
    }
  }

  static void MergeAdjacentRooms(Map map, Room room, List<Room> rooms, Rng rng)
  {
    List<List<(int, int)>> adjWalls = [];
    foreach (Room r in rooms)
    {
      if (r == room)
        continue;

      List<(int, int)> walls = [];
      foreach ((int row, int col) in room.Perimeter.Intersect(r.Perimeter))
      {
       // We want to look for shared walls where there are floor sqs either
       // north and south or east and west.
       if (DoorCandidate(map, row, col))
        {
          walls.Add((row, col));
        }
      }

      if (walls.Count > 0)
      {
        adjWalls.Add(walls);
      }
    }

    if (adjWalls.Count == 0)
      return;

    int i = rng.Next(adjWalls.Count);
    foreach ((int row, int col) in adjWalls[i])
    {
      map.SetTile(row, col, TileFactory.Get(TileType.DungeonFloor));
    }
  }

  static bool DoorCandidate(Map map, int row, int col)
  {
    if (map.TileAt(row - 1, col).Type == TileType.DungeonFloor && map.TileAt(row + 1, col).Type == TileType.DungeonFloor)
      return true;
    if (map.TileAt(row, col - 1).Type == TileType.DungeonFloor && map.TileAt(row, col + 1).Type == TileType.DungeonFloor)
      return true;

    return false;
  }

  static void EraseExteriorRoom(Map map, Room room, List<Room> rooms)
  {
    List<Room> otherRooms = [.. rooms.Where(r => r != room)];
    foreach ((int r, int c) in room.Sqs)
    {
      map.SetTile(r, c, TileFactory.Get(TileType.WorldBorder));
    }
    foreach ((int r, int c) in room.Perimeter)
    {
      if (!SharedWall(r, c, otherRooms))
        map.SetTile(r, c, TileFactory.Get(TileType.WorldBorder));
    }

    rooms.Remove(room);

    static bool SharedWall(int r, int c, List<Room> others)
    {
      foreach (Room room in others)
      {
        if (room.Perimeter.Contains((r, c)))
          return true;
      }

      return false;
    }
  }

  static Room MergeRooms(Room a, Room b)
  {
    Room r = new()
    {
      Sqs = [.. a.Sqs.Union(b.Sqs)]
    };

    foreach ((int row, int col) in a.Perimeter.Union(b.Perimeter))
    {
      if (OutsideWall(row, col))
        r.Perimeter.Add((row, col));
      else
        r.Sqs.Add((row, col));      
    }

    return r;

    bool OutsideWall(int row, int col)
    {
      foreach (var sq in Util.Adj8Sqs(row, col))
      {
        if (!(a.Sqs.Contains(sq) || b.Sqs.Contains(sq) || a.Perimeter.Contains(sq) || b.Perimeter.Contains(sq)))
          return true;
      }

      return false;
    }
  }

  static void SetDoors(Map map, Rng rng)
  {
    map.Dump();

    // Just rebuilding the set of rooms here. It seemed simpler than trying to
    // merge Room objects when we are merged interior rooms and I can't imagine
    // the inefficiency will be even noticable.
    Dictionary<int, Room> rooms = [];
    int i = 0;
    foreach (Room r in FindRooms(map))
    {
      rooms[i++] = r;
    }

    List<int> roomIds = [.. Enumerable.Range(0, rooms.Count)];
    roomIds.Shuffle(rng);

    while (roomIds.Count > 1)
    {      
      int j = roomIds[0];
      Room room = rooms[j];

      // Find the adjacent rooms
      List<int> adjRooms = [];
      foreach (int otherId in roomIds)
      {
        if (otherId == j)
          continue;

        foreach ((int r, int c) in room.Perimeter.Intersect(rooms[otherId].Perimeter))
        {
          if (DoorCandidate(map, r, c))
          {
            adjRooms.Add(otherId);
            break;
          }
        }
      }

      if (adjRooms.Count == 0)
      {
        // if there were no adjacent rooms, the room is a dud like:
        //
        //       ##########
        //       #........#
        //       #........#
        //   ##############
        //   #...#
        //   #...#
        //   #####
        //
        // I'll just fill them in with walls?

        rooms.Remove(j);
        roomIds.Remove(j);

        continue;
      }

      adjRooms.Shuffle(rng);

      int toMerge = rng.Next(1, adjRooms.Count + 1);
      for (int k = 0; k < toMerge; k++)
      {
        int otherId = adjRooms[k];
        Room other = rooms[otherId];

        // place the door
        List<(int r, int c)> doorable = [];
        List<(int r, int c)> shared = [.. room.Perimeter.Intersect(other.Perimeter)];
        foreach ((int r, int c) in shared)
        {
          if (DoorCandidate(map, r, c))
            doorable.Add((r, c));
        }

        (int dr, int dc) = doorable[rng.Next(doorable.Count)];
        TileType tile = rng.NextDouble() <= 0.25 ? TileType.ClosedDoor : TileType.LockedDoor;

        map.SetTile(dr, dc, TileFactory.Get(tile));

        room = MergeRooms(room, other);
        rooms.Remove(otherId);
        roomIds.Remove(otherId);
      }

      rooms[j] = room;
    }
  }

  static void TweakMap(Map map, List<Room> rooms, Rng rng)
  {
    List<Room> corners = [];
    List<Room> exterior = [];
    List<Room> interior = [];

    foreach (Room room in rooms)
    {
      bool north = NorthExterior(room.Perimeter);
      bool south = SouthExterior(map, room.Perimeter);
      bool west = WestExterior(room.Perimeter);
      bool east = EastExterior(map, room.Perimeter);

      if (north || south)
      {
        if (east || west)
          corners.Add(room);
        else
          exterior.Add(room);
      }
      else if (west || east)
      {
        exterior.Add(room);
      }
      else
      {
        interior.Add(room);
      }
    }

    foreach (Room room in corners)
    {
      EraseExteriorRoom(map, room, rooms);
    }

    int exteriorToRemove = rng.Next(1, 4);
    List<int> exteriorIndexes = [.. Enumerable.Range(0, exterior.Count)];
    while (exteriorIndexes.Count > 0 && exteriorToRemove > 0)
    {
      int j = rng.Next(exteriorIndexes.Count);

      Room room = exterior[j];
      EraseExteriorRoom(map, room, rooms);

      --exteriorToRemove;
      exteriorIndexes.RemoveAt(j);
    }

    map.Dump();

    int toMerge = int.Min(rng.Next(8, 12), interior.Count);
    List<int> indexes = [.. Enumerable.Range(0, interior.Count)];
    indexes.Shuffle(rng);
    for (int j = 0; j < toMerge; j++)
    {
      int m = indexes[j];
      MergeAdjacentRooms(map, rooms[m], rooms, rng);
    }

    SetDoors(map, rng);

    bool NorthExterior(HashSet<(int, int)> perimeter)
    {
      foreach ((int r, _) in perimeter)
      {
        if (r == 1)
          return true;
      }

      return false;
    }

    bool SouthExterior(Map map, HashSet<(int, int)> perimeter)
    {
      foreach ((int r, _) in perimeter)
      {
        if (r == map.Height - 2)
          return true;
      }

      return false;
    }

    bool WestExterior(HashSet<(int, int)> perimeter)
    {
      foreach ((_, int c) in perimeter)
      {
        if (c == 1)
          return true;
      }

      return false;
    }

    bool EastExterior(Map map, HashSet<(int, int)> perimeter)
    {
      foreach ((_, int c) in perimeter)
      {
        if (c == map.Width - 2)
          return true;
      }

      return false;
    }
  }

  public bool[,] Build(Rng rng)
  {
    // False == floor, true == wall
    var map = new bool[Height, Width];
    for (int r = 0; r < Height; r++)
    {
      map[r, 0] = true;
      map[r, Width - 1] = true;
    }
    for (int c = 0; c < Width; c++)
    {
      map[0, c] = true;
      map[Height - 1, c] = true;
    }

    Partition(map, 1, 1, Height - 2, Width - 2, rng);

    Map tower = new(Width + 2, Height + 2);
    for (int r = 0; r < Height + 2; r++)
    {
      for (int c = 0; c < Width + 2; c++)
      {
        if (r == 0 || c == 0)
          tower.SetTile(r, c, TileFactory.Get(TileType.WorldBorder));
        else if (r == Height + 1 || c == Width + 1)
          tower.SetTile(r, c, TileFactory.Get(TileType.WorldBorder));
        else if (map[r - 1, c - 1])
          tower.SetTile(r, c, TileFactory.Get(TileType.DungeonWall));
        else
          tower.SetTile(r, c, TileFactory.Get(TileType.DungeonFloor));
      }
    }

    List<Room> rooms = FindRooms(tower);

    TweakMap(tower, rooms, rng);

    tower.Dump();

    return map;
  }
}