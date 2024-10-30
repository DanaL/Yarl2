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

using System.Linq.Expressions;

namespace Yarl2;

enum TileType
{
  Unknown, WorldBorder, PermWall, DungeonWall, DungeonFloor, StoneFloor, 
  StoneWall, ClosedDoor, OpenDoor, LockedDoor, BrokenDoor, HWindow, VWindow,
  DeepWater, Water, FrozenDeepWater, FrozenWater, Sand, Grass, Mountain,
  GreenTree, OrangeTree, RedTree, YellowTree, Conifer,
  SnowPeak, Portal, Upstairs, Downstairs, Cloud, WoodWall, WoodFloor, Forge,
  Dirt, StoneRoad, Well, Bridge, WoodBridge,
  Statue,       // Most statues can be simple tiles so I'm just defining a few different types
  ElfStatue,    // rather than creating a statue subclass where I need to track state during
  DwarfStatue,  // serialization, etc
  Landmark, Chasm, CharredGrass, CharredStump, Portcullis, OpenPortcullis,
  BrokenPortcullis, GateTrigger, VaultDoor, HiddenTrapDoor, TrapDoor,
  SecretDoor, HiddenTeleportTrap, TeleportTrap, HiddenDartTrap, DartTrap,
  FireJetTrap, JetTrigger, HiddenPit, Pit, WaterTrap, HiddenWaterTrap,
  MagicMouth, HiddenMagicMouth
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
    TileType.JetTrigger => true,
    TileType.Pit => true,
    TileType.WaterTrap => true,
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
    TileType.Statue => "a statue",
    TileType.ElfStatue => "a statue",
    TileType.DwarfStatue => "a statue",
    TileType.Upstairs => "some stairs up",
    TileType.Downstairs => "some stairs down",
    TileType.Portal => "a dungeon entrance",
    TileType.CharredGrass => "charred grass",
    TileType.CharredStump => "charred stump",
    TileType.FrozenWater => "ice",
    TileType.FrozenDeepWater => "ice",
    TileType.Portcullis => "portcullis",
    TileType.OpenPortcullis => "open portcullis",
    TileType.BrokenPortcullis => "broken portcullis",
    TileType.GateTrigger => "trigger/pressure plate",
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
    _ => "unknown"
  };

  public List<EffectFlag> TerrainFlags()
  {
    List<EffectFlag> flags = [];

    switch (Type)
    {
      case TileType.Water:
      case TileType.DeepWater:
        flags.Add(EffectFlag.Wet);
        break;
      default:
        flags.Add(EffectFlag.None);
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

class GateTrigger(Loc gate) : Tile(TileType.GateTrigger)
{
  public Loc Gate { get; set; } = gate;
  public bool Found { get; set; } = false;

  public override bool Passable() => true;
  public override bool PassableByFlight() => true;
  public override bool Opaque() => false;
  
  public override string ToString() => $"{(int)Type};{Gate};{Found}";
}

class VaultDoor(bool open, Metals material) : Tile(TileType.VaultDoor)
{
  public Metals Material { get;set; } = material;
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

  public override string ToString()
  {
    return $"{(int)Type};{_stepMessage}";
  }
}

class TileFactory
{
  private static readonly Tile WorldBorder = new BasicTile(TileType.WorldBorder, false, true, false);
  private static readonly Tile Unknown = new BasicTile(TileType.Unknown, false, true, false);
  private static readonly Tile DungeonWall = new BasicTile(TileType.DungeonWall, false, true, false);
  private static readonly Tile StoneWall = new BasicTile(TileType.StoneWall, false, true, false);
  private static readonly Tile PermWall = new BasicTile(TileType.PermWall, false, true, false);
  private static readonly Tile Floor = new BasicTile(TileType.DungeonFloor, true, false, true);
  private static readonly Tile StoneFloor = new BasicTile(TileType.StoneFloor, true, false, true);
  private static readonly Tile DeepWater = new BasicTile(TileType.DeepWater, false, false, true);
  private static readonly Tile Grass = new BasicTile(TileType.Grass, true, false, true);
  private static readonly Tile Sand = new BasicTile(TileType.Sand, true, false, true);
  private static readonly Tile GreenTree = new BasicTile(TileType.GreenTree, true, false, true);
  private static readonly Tile YellowTree = new BasicTile(TileType.YellowTree, true, false, true);
  private static readonly Tile RedTree = new BasicTile(TileType.RedTree, true, false, true);
  private static readonly Tile OrangeTree = new BasicTile(TileType.OrangeTree, true, false, true);
  private static readonly Tile Conifer = new BasicTile(TileType.Conifer, true, false, true);
  private static readonly Tile Mountain = new BasicTile(TileType.Mountain, false, true, false);
  private static readonly Tile SnowPeak = new BasicTile(TileType.Mountain, false, true, false);
  private static readonly Tile Cloud = new BasicTile(TileType.Cloud, true, false, true);
  private static readonly Tile Water = new BasicTile(TileType.Water, true, false, true, "You splash into the water.");
  private static readonly Tile HWindow = new BasicTile(TileType.HWindow, false, false, false);
  private static readonly Tile VWindow = new BasicTile(TileType.VWindow, false, false, false);
  private static readonly Tile WoodWall = new BasicTile(TileType.WoodWall, false, true, false);
  private static readonly Tile WoodFloor = new BasicTile(TileType.WoodFloor, true, false, true);
  private static readonly Tile Forge = new BasicTile(TileType.Forge, true, false, true);
  private static readonly Tile Dirt = new BasicTile(TileType.Dirt, true, false, true);
  private static readonly Tile StoneRoad = new BasicTile(TileType.StoneRoad, true, false, true);
  private static readonly Tile Well = new BasicTile(TileType.Well, true, false, true);
  private static readonly Tile Bridge = new BasicTile(TileType.Bridge, true, false, true);
  private static readonly Tile WoodBridge = new BasicTile(TileType.WoodBridge, true, false, true);
  private static readonly Tile Statue = new BasicTile(TileType.Statue, false, true, false);
  private static readonly Tile ElfStatue = new BasicTile(TileType.ElfStatue, false, true, false);
  private static readonly Tile DwarfStatue = new BasicTile(TileType.DwarfStatue, false, true, false);
  private static readonly Tile Chasm = new BasicTile(TileType.Chasm, false, false, true);
  private static readonly Tile CharredGrass = new BasicTile(TileType.CharredGrass, true, false, true);
  private static readonly Tile CharredStump = new BasicTile(TileType.CharredStump, true, false, true);
  private static readonly Tile FrozenDeepWater = new BasicTile(TileType.FrozenDeepWater, true, false, true);
  private static readonly Tile FrozenWater = new BasicTile(TileType.FrozenWater, true, false, true);
  private static readonly Tile HiddenTrapDoor = new BasicTile(TileType.HiddenTrapDoor, true, false, true);
  private static readonly Tile TrapDoor = new BasicTile(TileType.TrapDoor, true, false, true);
  private static readonly Tile SecretDoor = new BasicTile(TileType.SecretDoor, false, true, false);
  private static readonly Tile BrokenDoor = new BasicTile(TileType.BrokenDoor, true, false, true);
  private static readonly Tile TeleportTrap = new BasicTile(TileType.HiddenTeleportTrap, true, false, true);
  private static readonly Tile VisibileTeleportTrap = new BasicTile(TileType.TeleportTrap, true, false, true);
  private static readonly Tile BrokenPortcullis = new BasicTile(TileType.BrokenPortcullis, true, false, true);
  private static readonly Tile HiddenDartTrap = new BasicTile(TileType.HiddenDartTrap, true, false, true);
  private static readonly Tile DartTrap = new BasicTile(TileType.DartTrap, true, false, true);
  private static readonly Tile HiddenPit = new BasicTile(TileType.HiddenPit, true, false, true);
  private static readonly Tile Pit = new BasicTile(TileType.Pit, true, false, true);
  private static readonly Tile HiddenWaterTrap = new BasicTile(TileType.HiddenWaterTrap, true, false, true);
  private static readonly Tile WaterTrap = new BasicTile(TileType.WaterTrap, true, false, true);
  private static readonly Tile MagicMouth = new BasicTile(TileType.MagicMouth, true, false, true);
  private static readonly Tile HiddenMagicMouth = new BasicTile(TileType.HiddenMagicMouth, true, false, true);

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
    TileType.Statue => Statue,
    TileType.ElfStatue => ElfStatue,
    TileType.DwarfStatue => DwarfStatue,
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
    _ => Unknown
  };
}

class Map : ICloneable
{
  public readonly int Width;
  public readonly int Height;

  public Tile[] Tiles;

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
  // trying squares. (And remove from list when I SetTile()...
  public (int, int) RandomTile(TileType type, Random rng)
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

  public static Map TestMap()
  {
    var map = new Map(20, 20);

    for (var col = 0; col < 20; col++)
    {
      map.Tiles[col] = TileFactory.Get(TileType.PermWall);
      map.Tiles[19 * 20 + col] = TileFactory.Get(TileType.PermWall);
    }

    for (var row = 1; row < 19; row++)
    {
      map.Tiles[row * 20] = TileFactory.Get(TileType.PermWall);
      map.Tiles[row * 20 + 19] = TileFactory.Get(TileType.PermWall);

      for (var col = 1; col < 19; col++)
      {
        map.Tiles[row * 20 + col] = TileFactory.Get(TileType.DungeonFloor);
      }
    }

    map.Tiles[3 * 20 + 8] = TileFactory.Get(TileType.DungeonWall);
    map.Tiles[3 * 20 + 9] = TileFactory.Get(TileType.DungeonWall);
    map.Tiles[3 * 20 + 10] = TileFactory.Get(TileType.DungeonWall);
    map.Tiles[3 * 20 + 11] = TileFactory.Get(TileType.DungeonWall);
    map.Tiles[3 * 20 + 12] = TileFactory.Get(TileType.DungeonWall);
    map.Tiles[3 * 20 + 13] = TileFactory.Get(TileType.DungeonWall);
    map.Tiles[3 * 20 + 14] = TileFactory.Get(TileType.DungeonWall);

    map.Tiles[5 * 20 + 14] = TileFactory.Get(TileType.DungeonWall);
    map.Tiles[6 * 20 + 14] = TileFactory.Get(TileType.DungeonWall);
    map.Tiles[7 * 20 + 14] = TileFactory.Get(TileType.ClosedDoor);
    map.Tiles[8 * 20 + 14] = TileFactory.Get(TileType.DungeonWall);
    map.Tiles[9 * 20 + 14] = TileFactory.Get(TileType.DungeonWall);

    map.Tiles[12 * 20 + 4] = TileFactory.Get(TileType.DungeonWall);
    map.Tiles[12 * 20 + 5] = TileFactory.Get(TileType.DungeonWall);
    map.Tiles[12 * 20 + 6] = TileFactory.Get(TileType.DungeonWall);
    map.Tiles[12 * 20 + 7] = TileFactory.Get(TileType.DungeonWall);
    map.Tiles[12 * 20 + 8] = TileFactory.Get(TileType.DungeonWall);

    map.Tiles[13 * 20 + 4] = TileFactory.Get(TileType.DungeonWall);
    map.Tiles[13 * 20 + 8] = TileFactory.Get(TileType.DungeonWall);
    map.Tiles[14 * 20 + 4] = TileFactory.Get(TileType.DungeonWall);
    map.Tiles[14 * 20 + 8] = TileFactory.Get(TileType.ClosedDoor);
    map.Tiles[15 * 20 + 4] = TileFactory.Get(TileType.DungeonWall);
    map.Tiles[15 * 20 + 8] = TileFactory.Get(TileType.DungeonWall);
    map.Tiles[16 * 20 + 4] = TileFactory.Get(TileType.DungeonWall);
    map.Tiles[16 * 20 + 8] = TileFactory.Get(TileType.DungeonWall);

    map.Tiles[17 * 20 + 4] = TileFactory.Get(TileType.DungeonWall);
    map.Tiles[17 * 20 + 5] = TileFactory.Get(TileType.DungeonWall);
    map.Tiles[17 * 20 + 6] = TileFactory.Get(TileType.DungeonWall);
    map.Tiles[17 * 20 + 7] = TileFactory.Get(TileType.DungeonWall);
    map.Tiles[17 * 20 + 8] = TileFactory.Get(TileType.DungeonWall);

    map.Tiles[14 * 20 + 15] = TileFactory.Get(TileType.DungeonWall);
    map.Tiles[15 * 20 + 15] = TileFactory.Get(TileType.DungeonWall);
    map.Tiles[16 * 20 + 15] = TileFactory.Get(TileType.DungeonWall);
    map.Tiles[17 * 20 + 15] = TileFactory.Get(TileType.DungeonWall);
    map.Tiles[18 * 20 + 15] = TileFactory.Get(TileType.DungeonWall);

    return map;
  }

  public void SetRandomTestMap(Random rng)
  {
    for (int col = 0; col < Width; col++)
    {
      Tiles[col] = TileFactory.Get(TileType.PermWall);
      Tiles[(Height - 1) * Width + col] = TileFactory.Get(TileType.PermWall);
    }

    for (int row = 1; row < Height - 1; row++)
    {
      Tiles[row * Width] = TileFactory.Get(TileType.PermWall);
      Tiles[row * Width + Width - 1] = TileFactory.Get(TileType.PermWall);
      for (int col = 1; col < Width - 1; col++)
      {
        Tiles[row * Width + col] = TileFactory.Get(TileType.DungeonFloor);
      }
    }

    for (int j = 0; j < 1000; j++)
    {
      int row = rng.Next(1, Height);
      int col = rng.Next(1, Width);
      Tiles[row * Width + col] = TileFactory.Get(TileType.DungeonWall);
    }
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

  static void Dump(bool[,] map, int height, int width)
  {
    for (int r = 0; r < height; r++)
    {
      for (int c = 0; c < width; c++)
      {
        Console.Write(map[r, c] ? '.' : '#');
      }
      Console.WriteLine();
    }
    Console.WriteLine();
  }

  public static bool[,] GetCave(int height, int width, Random rng)
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