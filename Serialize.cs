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

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Yarl2;

record SaveGameInfo(CampaignSaver Campaign, GameStateSave GameStateSave, GameObjDBSave ObjDb);

// When I started working on saving the game, I had a bunch of problems with
// Json serialize. It particularly seemed to hate that Tile was an abstract
// class would throw an Exception trying to deserialize BaseTiles (but
// didn't have a problem Doors or Portals ¯\_(ツ)_/¯)
//
// So my solution is to create 'simplified' versions of the problem classes
// like Map (and any future 'complex' classes). Also, I like having all the Json
// decorations and such here instead of polluting my game logic code
//
// I have a feeling BinaryFormatter would do what I want, but there's all
// those security warnings surrounding it... (although I'm not sure that's 
// actually a concern for my game's save files)
internal class Serialize
{
  public static void WriteSaveGame(GameState gameState)
  {
    var objDbSave = GameObjDBSave.Shrink(gameState.ObjDb);

    var sgi = new SaveGameInfo(CampaignSaver.Shrink(gameState.Campaign), GameStateSave.Shrink(gameState), objDbSave);

    var bytes = JsonSerializer.SerializeToUtf8Bytes(sgi,
                    new JsonSerializerOptions { WriteIndented = false, IncludeFields = true });
    // In the future, when this is a real game, I'm going to have check player names for invalid characters or build a 
    // little database of players vs saved games
    string filename = $"{gameState.Player.Name}.dat";
    File.WriteAllBytes(filename, bytes);
  }

  //public static (Player?, Campaign, GameObjectDB, ulong, List<MsgHistory>) LoadSaveGame(string playerName)
  public static GameState LoadSaveGame(string playerName, Options options, UserInterface ui)
  {
    string filename = $"{playerName}.dat";
    var bytes = File.ReadAllBytes(filename);
    var sgi = JsonSerializer.Deserialize<SaveGameInfo>(bytes);

    var campaign = CampaignSaver.Inflate(sgi.Campaign);
    var gs = GameStateSave.Inflate(campaign, sgi.GameStateSave, options, ui);
    
    var objDbSave = sgi.ObjDb;
    var objDb = GameObjDBSave.Inflate(objDbSave);

    gs.ObjDb = objDb;

    // At the moment, EndOfRound is the only type of listener in the game
    foreach (var l in objDb.ActiveListeners())
    {
      gs.RegisterForEvent(GameEventType.EndOfRound, l);
    }

    return gs;
  }

  public static bool SaveFileExists(string playerName) => File.Exists($"{playerName}.dat");
}

class GameStateSave
{
  public int CurrLevel { get; set; }
  public int CurrDungeonID { get; set; }
  public ulong Turn { get; set; }
  public int Seed { get; set; }

  public static GameStateSave Shrink(GameState gs)
  {
    var gss = new GameStateSave()
    {
      CurrDungeonID = gs.CurrDungeonID,
      CurrLevel = gs.CurrLevel,
      Turn = gs.Turn,
      Seed = gs.Seed
    };

    return gss;
  }

  public static GameState Inflate(Campaign camp, GameStateSave gss, Options opt, UserInterface ui)
  {
    var rng = new Random(gss.Seed);
    var gs = new GameState(null, camp, opt, ui, rng, gss.Seed)
    {
      CurrDungeonID = gss.CurrDungeonID,
      CurrLevel = gss.CurrLevel,
      Turn = gss.Turn
    };

    return gs;
  }
}

class TownSave
{
  public string Name { get; set; } = "";
  [JsonInclude]
  public HashSet<Loc> Shrine { get; set; } = [];
  [JsonInclude]
  public HashSet<Loc> Tavern { get; set; } = [];
  [JsonInclude]
  public HashSet<Loc> Market { get; set; } = [];
  [JsonInclude]
  public HashSet<Loc> Smithy { get; set; } = [];
  [JsonInclude]
  public List<HashSet<Loc>> Homes { get; set; } = [];
  [JsonInclude]
  public HashSet<int> TakenHomes { get; set; } = [];
  [JsonInclude]
  public HashSet<Loc> TownSquare { get; set; } = [];
  public int Row { get; set; }
  public int Col { get; set; }
  public int Height { get; set; }
  public int Width { get; set; }
}

class CampaignSaver
{
  [JsonInclude]
  public Dictionary<int, DungeonSaver> Dungeons = [];
  public TownSave? Town { get; set; }
  [JsonInclude]
  public List<string> Facts { get; set; } = [];

  public static CampaignSaver Shrink(Campaign c)
  {
    var town = new TownSave()
    {
      Shrine = c.Town!.Shrine,
      Tavern = c.Town.Tavern,
      Market = c.Town.Market,
      Smithy = c.Town.Smithy,
      Homes = c.Town.Homes,
      TakenHomes = c.Town.TakenHomes,
      TownSquare = c.Town.TownSquare,
      Row = c.Town.Row,
      Col = c.Town.Col,
      Height = c.Town.Height,
      Width = c.Town.Width,
      Name = c.Town.Name
    };

    var facts =  c.History!.Facts.Select(f => f.ToString()).ToList();
    CampaignSaver sc = new()
    {
      Town = town,
      Facts = facts
    };

    foreach (var k in c.Dungeons.Keys)
    {
      sc.Dungeons.Add(k, DungeonSaver.Shrink(c.Dungeons[k]));
    }

    return sc;
  }

  public static Campaign Inflate(CampaignSaver sc)
  {
    var town = new Town()
    {
      Shrine = sc.Town!.Shrine,
      Tavern = sc.Town.Tavern,
      Market = sc.Town.Market,
      Smithy = sc.Town.Smithy,
      Homes = sc.Town.Homes,
      TakenHomes = sc.Town.TakenHomes,
      TownSquare = sc.Town.TownSquare,
      Row = sc.Town.Row,
      Col = sc.Town.Col,
      Height = sc.Town.Height,
      Width = sc.Town.Width,
      Name = sc.Town.Name
    };

    Campaign campaign = new()
    {
      Town = town
    };

    foreach (var k in sc.Dungeons.Keys)
    {
      campaign.Dungeons.Add(k, DungeonSaver.Inflate(sc.Dungeons[k]));
    }

    List<Fact> facts = sc.Facts.Select(Fact.FromStr).ToList();
    campaign.History = new History(new Random())
    {
      Facts = facts
    };

    return campaign;
  }
}

internal class DungeonSaver
{
  public int ID { get; set; }
  public string? ArrivalMessage { get; set; }

  [JsonInclude]
  public List<RememberedSq> RememberedSqs;
  [JsonInclude]
  public Dictionary<int, MapSaver> LevelMaps;

  public DungeonSaver()
  {
    RememberedSqs = [];
    LevelMaps = [];
  }

  public static DungeonSaver Shrink(Dungeon dungeon)
  {
    var sd = new DungeonSaver()
    {
      ID = dungeon.ID,
      ArrivalMessage = dungeon.ArrivalMessage,
      RememberedSqs = []
    };

    foreach (var k in dungeon.LevelMaps.Keys)
    {
      sd.LevelMaps.Add(k, MapSaver.Shrink(dungeon.LevelMaps[k]));
    }

    foreach (var sq in dungeon.RememberedSqs)
    {
      sd.RememberedSqs.Add(RememberedSq.FromTuple(sq.Key, sq.Value));
    }

    return sd;
  }

  public static Dungeon Inflate(DungeonSaver sd)
  {
    Dungeon d = new Dungeon(sd.ID, sd.ArrivalMessage);
    d.RememberedSqs = [];

    foreach (var sq in sd.RememberedSqs)
    {
      d.RememberedSqs.Add(sq.ToTuple(), sq.Sqr);
    }

    foreach (var k in sd.LevelMaps.Keys)
    {
      d.LevelMaps.Add(k, MapSaver.Inflate(sd.LevelMaps[k]));
    }

    return d;
  }
}

internal class MapSaver
{
  public int Height { get; set; }
  public int Width { get; set; }
  [JsonInclude]
  int[]? Tiles { get; set; }
  [JsonInclude]
  List<string>? SpecialTiles { get; set; }
  [JsonInclude]
  Dictionary<string, Dictionary<ulong, TerrainFlag>> Effects { get; set; }

  public static MapSaver Shrink(Map map)
  {
    Dictionary<string, Dictionary<ulong, TerrainFlag>> effectsInfo = [];
    foreach (var kvp in map.Effects)
    {
      effectsInfo.Add(kvp.Key.ToString(), kvp.Value);
    }

    MapSaver sm = new MapSaver
    {
      Height = map.Height,
      Width = map.Width,
      Tiles = new int[map.Tiles.Length],
      SpecialTiles = [],
      Effects = effectsInfo
    };

    for (int j = 0; j < map.Tiles.Length; j++)
    {
      var t = map.Tiles[j];
      sm.Tiles[j] = (int)t.Type;
      if (t is not BasicTile)
      {
        sm.SpecialTiles.Add($"{j};{t}");
      }
    }

    return sm;
  }

  static Dictionary<int, Tile> InflateSpecialTiles(List<string> shrunken)
  {
    var tiles = new Dictionary<int, Tile>();

    foreach (string s in shrunken)
    {
      Tile? tile = null;
      List<int> digits;
      var pieces = s.Split(';');
      var j = int.Parse(pieces[0]);
      var type = (TileType)int.Parse(pieces[1]);

      switch (type)
      {
        case TileType.Portal:
          tile = new Portal(pieces[3]);
          digits = Util.ToNums(pieces[2]);
          ((Portal)tile).Destination = new Loc(digits[0], digits[1], digits[2], digits[3]);
          break;
        case TileType.Upstairs:
          tile = new Upstairs(pieces[3]);
          digits = Util.ToNums(pieces[2]);
          ((Upstairs)tile).Destination = new Loc(digits[0], digits[1], digits[2], digits[3]);
          break;
        case TileType.Downstairs:
          tile = new Downstairs(pieces[3]);
          digits = Util.ToNums(pieces[2]);
          ((Downstairs)tile).Destination = new Loc(digits[0], digits[1], digits[2], digits[3]);
          break;
        case TileType.OpenDoor:
        case TileType.ClosedDoor:
          bool open = Convert.ToBoolean(pieces[2]);
          // technically wrong for open doors but the internal state
          // of the door will work itself out
          tile = new Door(TileType.ClosedDoor, open);
          break;
        case TileType.Landmark:
          string msg = pieces[2];
          tile = new Landmark(msg);
          break;
      }

      if (tile is not null)
      {
        tiles.Add(j, tile);
      }
    }

    return tiles;
  }

  public static Map Inflate(MapSaver sm)
  {
    var map = new Map(sm.Width, sm.Height);

    if (sm.Tiles is null || sm.SpecialTiles is null)
      throw new Exception("Invalid save game data!");

    foreach (var kvp in sm.Effects)
    {
      var d = Util.ToNums(kvp.Key);
      map.Effects.Add((d[0], d[1]), kvp.Value);
    }

    try
    {
      var specialTiles = InflateSpecialTiles(sm.SpecialTiles);
      for (int j = 0; j < sm.Tiles.Length; j++)
      {
        var type = (TileType)sm.Tiles[j];
        if (specialTiles.TryGetValue(j, out Tile? value))
        {
          map.Tiles[j] = value;
        }
        else
        {
          map.Tiles[j] = TileFactory.Get(type);
        }
      }
    }
    catch (Exception ex)
    {
      throw new Exception($"Invalid save game data! {ex.Message}");
    }

    return map;
  }
}

// Sigh, JSONSerializer doesn't like my sweet Loc record struct as a dictionary
// key so I have to pack and unpack the ItemDB. I wonder how much of a problem
// this is going to be when the there's a dungeon full of items... (I'd probably
// need to switch to a per-level itemDB)
class GameObjDBSave
{
  [JsonInclude]
  List<string> Objects { get; set; } = [];

  static Glyph TextToGlyph(string text)
  {
    var p = text.Split(',');
    return new Glyph(p[0][0], Colours.TextToColour(p[1]), Colours.TextToColour(p[2]), Colours.TextToColour(p[3]));
  }

  static string StatsToText(Dictionary<Attribute, Stat> stats)
  {
    List<string> pieces = [];
    foreach (var kvp in stats)
    {
      pieces.Add($"{kvp.Key}#{kvp.Value.Max}#{kvp.Value.Curr}");
    }
    return string.Join(',', pieces);
  }

  static Dictionary<Attribute, Stat> StatsFromText(string txt)
  {
    Dictionary<Attribute, Stat> stats = [];

    if (txt != "")
    {
      foreach (var s in txt.Split(','))
      {
        var pieces = s.Split('#');
        Enum.TryParse(pieces[0], out Attribute attr);
        var stat = new Stat()
        {
          Max = int.Parse(pieces[1]),
          Curr = int.Parse(pieces[2])
        };
        stats.Add(attr, stat);
      }
    }

    return stats;
  }

  static Player InflatePlayer(string txt, GameObjectDB objDb)
  {
    var fields = txt.Split('|');
    var p = new Player(fields[1]);

    Enum.TryParse(fields[0], out PlayerClass charClass);
    p.CharClass = charClass;
    p.ID = ulong.Parse(fields[2]);
    p.Loc = Loc.FromStr(fields[4]);

    // Parse the traits
    if (fields[5] != "")
    {
      foreach (var t in fields[5].Split('`'))
      {
        var trait = TraitFactory.FromText(t, p);
        p.Traits.Add(trait);
      }
    }
    p.Stats = StatsFromText(fields[6]);
    p.Energy = double.Parse(fields[7]);
    p.Recovery = double.Parse(fields[8]);

    if (fields[11] != "")
    {
      p.Inventory = new Inventory(p.ID, objDb)
      {
        Zorkmids = int.Parse(fields[9]),
        NextSlot = fields[10][0]
      };
      p.Inventory.RestoreFromText(fields[11]);
    }

    return p;
  }

  // Mob:DumbMoveStrategy|PriestBehaviour|uiliorw|23|@,yellow,yelloworange,black|0,0,111,117|||1|1|0|a||False|tall elf with curly hair, and piercing eyes
  static Mob InflateMob(string txt, GameObjectDB objDb)
  {
    var fields = txt.Split('|');
    var mob = new Mob()
    {
      ID = ulong.Parse(fields[3]),
      Name = fields[2],
      Glyph = TextToGlyph(fields[4]),
      Loc = Loc.FromStr(fields[5]),
      Energy = double.Parse(fields[8]),
      Recovery = double.Parse(fields[9]),
      RemoveFromQueue = bool.Parse(fields[13]),
      Appearance = fields[14]
    };

    string mvStrategyStr = fields[0];
    mob.MoveStrategy = (IMoveStrategy) Activator.CreateInstance(Type.GetType($"Yarl2.{mvStrategyStr}"));
    string behaviourStr = fields[1];
    mob.SetBehaviour((IBehaviour)Activator.CreateInstance(Type.GetType($"Yarl2.{behaviourStr}")));

    // Parse the traits
    if (fields[6] != "")
    {
      foreach (var t in fields[6].Split('`'))
      {
        var trait = TraitFactory.FromText(t, mob);
        mob.Traits.Add(trait);
      }
    }

    mob.Stats = StatsFromText(fields[7]);

    if (fields[12] != "")
    {
      mob.Inventory = new Inventory(mob.ID, objDb)
      {
        Zorkmids = int.Parse(fields[10]),
        NextSlot = fields[11][0]
      };
      mob.Inventory.RestoreFromText(fields[12]);
    }

    if (fields[15] != "")
    {
      foreach (var a in fields[15].Split('`'))
      {
        var action = (ActionTrait) TraitFactory.FromText(a, mob);
        mob.Actions.Add(action);
      }
    }

    return mob;
  }

  static Item InflateItem(string txt)
  {
    var fields = txt.Split('|');

    Enum.TryParse(fields[0], out ItemType itemType);
    var item = new Item()
    {
      Type = itemType,
      Name = fields[1],
      ID = ulong.Parse(fields[2]),
      Glyph = TextToGlyph(fields[3]),
      Loc = Loc.FromStr(fields[4]),
      Stackable = bool.Parse(fields[6].ToLower()),
      Slot = fields[7][0],
      Equiped = bool.Parse(fields[8].ToLower()),
      ContainedBy = ulong.Parse(fields[9]),
      Consumable = bool.Parse(fields[10]),
      Adjectives = [.. fields[11].Split(',')],
      Value = int.Parse(fields[12]),
    };

    // Parse the traits
    if (fields[5] != "")
    {
      foreach (var t in fields[5].Split('`'))
      {
        var trait = TraitFactory.FromText(t, item);
        item.Traits.Add(trait);
      }
    }

    item.SetZ(int.Parse(fields[13]));

    return item;
  }
  static GameObj InflateObj(string txt, GameObjectDB objDb)
  {
    int j = txt.IndexOf(':');
    var type = txt[..j];
    var fields = txt[(j + 1)..];

    if (type == "Player")
      return InflatePlayer(fields, objDb);
    else if (type == "Mob")
      return InflateMob(fields, objDb);
    else if (type == "Item")
      return InflateItem(fields);

    return null;
  }

  public static GameObjDBSave Shrink(GameObjectDB objDb)
  {
    var sidb = new GameObjDBSave();

    foreach (var kvp in objDb.Objs)
    {
      var obj = kvp.Value;
      if (obj is Player player)
      {
        var sb = new StringBuilder("Player:");
        sb.Append(player.CharClass);
        sb.Append('|');
        sb.Append(obj.ToString());
        sb.Append('|');
        sb.Append(StatsToText(player.Stats));
        sb.Append('|');
        sb.Append(player.Energy);
        sb.Append('|');
        sb.Append(player.Recovery);
        sb.Append('|');
        sb.Append(player.Inventory.Zorkmids);
        sb.Append('|');
        sb.Append(player.Inventory.NextSlot);
        sb.Append('|');
        sb.Append(player.Inventory.ToText());

        sidb.Objects.Add(sb.ToString());
      }
      else if (obj is Mob mob)
      {
        var sb = new StringBuilder("Mob:");
        sb.Append(mob.MoveStrategy.GetType().Name);
        sb.Append('|');
        sb.Append(mob.Behaviour.GetType().Name);
        sb.Append('|');
        sb.Append(obj.ToString());
        sb.Append('|');
        sb.Append(StatsToText(mob.Stats));
        sb.Append('|');
        sb.Append(mob.Energy);
        sb.Append('|');
        sb.Append(mob.Recovery);
        sb.Append('|');
        sb.Append(mob.Inventory.Zorkmids);
        sb.Append('|');
        sb.Append(mob.Inventory.NextSlot);
        sb.Append('|');
        sb.Append(mob.Inventory.ToText());
        sb.Append('|');
        sb.Append(mob.RemoveFromQueue);
        sb.Append('|');
        sb.Append(mob.Appearance);

        string actions = string.Join("`", mob.Actions.Select(t => t.AsText()));
        sb.Append('|');
        sb.Append(actions);

        sidb.Objects.Add(sb.ToString());
      }
      else if (obj is Item item)
      {
        var sb = new StringBuilder("Item:");
        sb.Append(item.Type);
        sb.Append('|');
        sb.Append(obj.ToString());
        sb.Append('|');
        sb.Append(item.Stackable);
        sb.Append('|');
        sb.Append(item.Slot);
        sb.Append('|');
        sb.Append(item.Equiped);
        sb.Append('|');
        sb.Append(item.ContainedBy);
        sb.Append('|');
        sb.Append(item.Consumable);
        sb.Append('|');
        sb.Append(string.Join(',', item.Adjectives));
        sb.Append('|');
        sb.Append(item.Value);
        sb.Append('|');
        sb.Append(item.Z());

        sidb.Objects.Add(sb.ToString());
      }
    }

    return sidb;
  }

  public static GameObjectDB Inflate(GameObjDBSave sidb)
  {
    var objDb = new GameObjectDB();

    ulong maxID = 0;
    foreach (var line in sidb.Objects)
    {
      var obj = InflateObj(line, objDb);
      if (obj.ID > maxID)
        maxID = obj.ID;
      objDb.Add(obj);
      if (obj.Loc != Loc.Nowhere && obj.Loc != Loc.Zero)
      {
        if (obj is Item item)
          objDb.SetToLoc(obj.Loc, item);
        else
          objDb.AddToLoc(obj.Loc, (Actor)obj);
      }
    }
    GameObj.SetSeed(maxID + 1);

    return objDb;
  }
}

// SIGH so for tuples, the JsonSerliazer won't serialize a tuple of ints. So, let's make a little object that
// *can* be serialized
record struct RememberedSq(int A, int B, int C, Sqr Sqr)
{
  public static RememberedSq FromTuple((int, int, int) t, Sqr sqr) => new RememberedSq(t.Item1, t.Item2, t.Item3, sqr);
  public (int, int, int) ToTuple() => (A, B, C);
}