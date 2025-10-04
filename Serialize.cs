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

record SaveFileInfo(string CharName, string Path);
record SaveGameInfo(CampaignSaver Campaign, GameStateSave GameStateSave, GameObjDBSave ObjDb, Dictionary<string, ItemIDInfo> IDInfo, List<SqrSave> Preview);
record SqrSave(int R, int G, int B, int A, int BgR, int BgG, int BgB, int BgA, char Ch);

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
  public static void WriteSaveGame(GameState gameState, UserInterface ui)
  {
    var objDbSave = GameObjDBSave.Shrink(gameState.ObjDb);

    var preview = GenPreview(ui);
    var sgi = new SaveGameInfo(CampaignSaver.Shrink(gameState.Campaign), GameStateSave.Shrink(gameState), objDbSave, Item.IDInfo, preview);

    var bytes = JsonSerializer.SerializeToUtf8Bytes(sgi,
                    new JsonSerializerOptions { WriteIndented = false, IncludeFields = true });
    
    DirectoryInfo saveDir = new(Util.SavePath);
    try
    {
      if (!saveDir.Exists)
      {
        saveDir.Create();
      }
    }
    catch (Exception)
    {
      throw new Exception("Unable to create or access the Save Game folder. Your game was not saved successfully!");
    }

    try
    {
      // In the future, when this is a real game, I'm going to have check player names for invalid characters or build a 
      // little database of players vs saved games
      string filename = $"{gameState.Player.Name}.dat";
      string fullpath = Path.Combine(saveDir.FullName, filename);
      File.WriteAllBytes(fullpath, bytes);
    }
    catch (Exception)
    {
      throw new Exception("Save failed! Your game was not saved successfully!");
    }    
  }

  public static List<Sqr> FetchSavePreview(string path)
  {    
    byte[] bytes = File.ReadAllBytes(path);
    var sgi = JsonSerializer.Deserialize<SaveGameInfo>(bytes);

    if (sgi is null)
      return [];

    List<Sqr> sqrs = [];
    foreach (var s in sgi.Preview)
    {
      Colour fg = new(s.R, s.G, s.B, s.A);
      Colour bg = new(s.BgR, s.BgG, s.BgB, s.BgA);
      sqrs.Add(new Sqr(fg, bg, s.Ch));
    }

    return sqrs;
  }

  public static GameState LoadSaveGame(string path, Options options, UserInterface ui)
  {
    var bytes = File.ReadAllBytes(path);
    var sgi = JsonSerializer.Deserialize<SaveGameInfo>(bytes) ?? throw new Exception("Failure to deserialize save file :(");
    var campaign = CampaignSaver.Inflate(sgi.Campaign);
    var gs = GameStateSave.Inflate(campaign, sgi.GameStateSave, options, ui);
    
    var objDbSave = sgi.ObjDb;
    var objDb = GameObjDBSave.Inflate(objDbSave);

    gs.ObjDb = objDb;

    foreach (IGameEventListener l in objDb.ActiveListeners())
    {
      if (l.EventType == GameEventType.Death)
        gs.RegisterForEvent(GameEventType.Death, l, l.SourceId);
      else
        gs.RegisterForEvent(GameEventType.EndOfRound, l);
    }

    Item.IDInfo = sgi.IDInfo;

    return gs;
  }

  public static List<SaveFileInfo> GetSavedGames()
  {
    List<SaveFileInfo> files = [];

    DirectoryInfo dir = new(Util.SavePath);
    try
    {
      if (!dir.Exists)
      {
        dir.Create();
      }
    }
    catch (Exception)
    {
      throw new Exception("Unable to create or access saved game folder!");
    }

    foreach (FileInfo file in dir.GetFiles().OrderByDescending(f => f.LastWriteTime))
    {
      if (file.Extension.Equals(".dat", StringComparison.OrdinalIgnoreCase))
      {
        files.Add(new SaveFileInfo(file.Name[..^4], file.FullName));
      }
    }

    return files;
  }

  static List<SqrSave> GenPreview(UserInterface ui)
  {
    List<SqrSave> sqs = [];
    int midR = ui.PlayerScreenRow;
    int midC = ui.PlayerScreenCol;

    for (int r = midR - 5; r < midR + 6; r++)
    {
      for (int c = midC - 5; c < midC + 6; c++)
      {
        Sqr sqr = ui.SqsOnScreen[r, c];
        SqrSave s = new(
          sqr.Fg.R, sqr.Fg.G, sqr.Fg.B, sqr.Fg.Alpha,
          sqr.Bg.R, sqr.Bg.G, sqr.Bg.B, sqr.Bg.Alpha,
          sqr.Ch);
        sqs.Add(s);
      }
    } 

    return sqs;
  }

  public static bool SaveFileExists(string playerName) => File.Exists($"{playerName}.dat");
}

class GameStateSave
{
  public int CurrLevel { get; set; }
  public int CurrDungeonID { get; set; }
  public ulong Turn { get; set; }
  public ulong[] Seed { get; set; } = [];

  public static GameStateSave Shrink(GameState gs) => new()
  {
    CurrDungeonID = gs.CurrDungeonID,
    CurrLevel = gs.CurrLevel,
    Turn = gs.Turn,
    Seed = gs.Rng.State
  };

  public static GameState Inflate(Campaign camp, GameStateSave gss, Options opt, UserInterface ui)
  {
    Rng rng = Rng.FromState(gss.Seed);
    GameState gs = new(camp, opt, ui, rng)
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
  [JsonInclude]
  public HashSet<Loc> WitchesCottage { get; set; } = [];
  [JsonInclude]
  public HashSet<Loc> WitchesGarden { get; set; } = [];
  [JsonInclude]
  public HashSet<Loc> WitchesYard { get; set; } = [];
  [JsonInclude]
  public HashSet<Loc> Roofs { get; set; } = [];

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
  public List<string> Facts { get; set; } = [];
  public List<string> HistoricalEvents { get; set; } = [];
  public List<string> Nations { get; set; } = [];
  public string RulerInfo { get; set; } = "";
  public VillainType Villain { get; set; }
  public string VillainName { get; set; } = "";
  
  public static CampaignSaver Shrink(Campaign c)
  {
    TownSave town = new()
    {
      Shrine = c.Town!.Shrine,
      Tavern = c.Town.Tavern,
      Market = c.Town.Market,
      Smithy = c.Town.Smithy,
      Homes = c.Town.Homes,
      WitchesCottage = c.Town.WitchesCottage,
      WitchesGarden = c.Town.WitchesGarden,
      WitchesYard = c.Town.WitchesYard,
      TakenHomes = c.Town.TakenHomes,
      TownSquare = c.Town.TownSquare,
      Row = c.Town.Row,
      Col = c.Town.Col,
      Height = c.Town.Height,
      Width = c.Town.Width,
      Name = c.Town.Name,
      Roofs = c.Town.Roofs,
    };

    if (c.FactDb is null)
    {
      throw new Exception("FactDb is null!");
    }

    List<string?> wtf = [.. c.FactDb!.Facts.Select(f => f.ToString())];

    CampaignSaver sc = new()
    {
      Town = town,
      Facts = [.. c.FactDb!.Facts.Select(f => f.ToString()!)],
      HistoricalEvents = [.. c.FactDb!.HistoricalEvents.Select(he => he.ToString()!)], 
      Nations = [.. c.FactDb!.Nations.Select(n => n.ToString())],  
      RulerInfo = c.FactDb!.Ruler.ToString(),
      Villain = c.FactDb!.Villain,
      VillainName = c.FactDb!.VillainName      
    };

    foreach (var k in c.Dungeons.Keys)
    {
      sc.Dungeons.Add(k, DungeonSaver.Shrink(c.Dungeons[k]));
    }

    return sc;
  }

  public static Campaign Inflate(CampaignSaver sc)
  {
    Town town = new()
    {
      Shrine = sc.Town!.Shrine,
      Tavern = sc.Town.Tavern,
      Market = sc.Town.Market,
      Smithy = sc.Town.Smithy,
      Homes = sc.Town.Homes,
      TakenHomes = sc.Town.TakenHomes,
      TownSquare = sc.Town.TownSquare,
      WitchesCottage = sc.Town.WitchesCottage,
      WitchesYard = sc.Town.WitchesYard,
      WitchesGarden = sc.Town.WitchesGarden,      
      Row = sc.Town.Row,
      Col = sc.Town.Col,
      Height = sc.Town.Height,
      Width = sc.Town.Width,
      Name = sc.Town.Name,
      Roofs= sc.Town.Roofs
    };

    Campaign campaign = new()
    {
      Town = town
    };

    foreach (var k in sc.Dungeons.Keys)
    {
      campaign.Dungeons.Add(k, DungeonSaver.Inflate(sc.Dungeons[k]));
    }

    RulerInfo ruler = (RulerInfo)Fact.FromStr(sc.RulerInfo);
    FactDb factDb = new(ruler);
    factDb.Villain = sc.Villain;
    factDb.VillainName = sc.VillainName;
    foreach (var f in sc.Facts)
    {
      factDb.Add(Fact.FromStr(f));
    }
    foreach (var he in sc.HistoricalEvents)
    {
      factDb.Add(Fact.FromStr(he));
    }
    foreach (var n in sc.Nations)
    {
      factDb.Add(Fact.FromStr(n));
    }
    campaign.FactDb = factDb;
    
    return campaign;
  }
}

internal class DungeonSaver
{
  public int ID { get; set; }
  public string? ArrivalMessage { get; set; }
  public bool Descending { get; set; }
  public string ExitLoc { get; set; } = "";
  public string Name { get; set; } = "";

  [JsonInclude]
  public List<string> RememberedLocs = [];
  [JsonInclude]
  public Dictionary<int, MapSaver> LevelMaps = [];
  [JsonInclude]
  public List<string> MonsterDecks { get; set; } = [];

  public static DungeonSaver Shrink(Dungeon dungeon)
  {
    DungeonSaver sd = new()
    {
      ID = dungeon.ID,
      ArrivalMessage = dungeon.ArrivalMessage,
      Descending = dungeon.Descending,
      RememberedLocs = [],
      MonsterDecks = [.. dungeon.MonsterDecks.Select(deck => deck.ToString())],
      ExitLoc = dungeon.ExitLoc.ToString(),
      Name = dungeon.Name,
    };

    foreach (var k in dungeon.LevelMaps.Keys)
    {
      sd.LevelMaps.Add(k, MapSaver.Shrink(dungeon.LevelMaps[k]));
    }

    foreach (var kvp in dungeon.RememberedLocs)
    {
      string s = $"{kvp.Key}{Constants.SEPARATOR}{kvp.Value}";
      sd.RememberedLocs.Add(s);      
    }

    return sd;
  }

  public static Dungeon Inflate(DungeonSaver sd)
  {
    Dungeon d = new(sd.ID, sd.Name, sd.ArrivalMessage ?? "", sd.Descending)
    {
      ExitLoc = Loc.FromStr(sd.ExitLoc)
    };

    foreach (string s in sd.RememberedLocs)
    {
      var pieces = s.Split(Constants.SEPARATOR);
      Loc loc = Loc.FromStr(pieces[0]);
      Glyph g = Glyph.TextToGlyph(pieces[1]);
      d.RememberedLocs[loc] = g;
    }
 
    foreach (var k in sd.LevelMaps.Keys)
    {
      d.LevelMaps.Add(k, MapSaver.Inflate(sd.LevelMaps[k]));
    }

    d.MonsterDecks = [.. sd.MonsterDecks.Select(MonsterDeck.FromString)];

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
  public List<string> Alerts { get; set; } = [];
  public bool DiggableFloor { get; set; }
  public bool Submerged { get; set; }

  public static MapSaver Shrink(Map map)
  {  
    MapSaver sm = new()
    {
      Height = map.Height,
      Width = map.Width,
      Tiles = new int[map.Tiles.Length],
      SpecialTiles = [],
      DiggableFloor = map.DiggableFloor,
      Submerged = map.Submerged
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

    sm.Alerts = map.Alerts;

    return sm;
  }

  static Dictionary<int, Tile> InflateSpecialTiles(List<string> shrunken)
  {
    var tiles = new Dictionary<int, Tile>();
    bool open;

    foreach (string s in shrunken)
    {
      Tile? tile = null;
      List<int> digits;
      string[] pieces = s.Split(';');
      int j = int.Parse(pieces[0]);
      HashSet<Loc> tilesSet = [];

      TileType type = (TileType)int.Parse(pieces[1]);
      try
      {
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
          case TileType.Shortcut:
            tile = new Shortcut();
            digits = Util.ToNums(pieces[2]);
            ((Shortcut)tile).Destination = new Loc(digits[0], digits[1], digits[2], digits[3]);
            break;
          case TileType.ShortcutDown:
            tile = new ShortcutDown();
            digits = Util.ToNums(pieces[2]);
            ((ShortcutDown)tile).Destination = new Loc(digits[0], digits[1], digits[2], digits[3]);
            break;
          case TileType.OpenDoor:
          case TileType.ClosedDoor:
            open = Convert.ToBoolean(pieces[2]);
            // technically wrong for open doors but the internal state
            // of the door will work itself out
            tile = new Door(TileType.ClosedDoor, open);
            break;
          case TileType.OpenPortcullis:
          case TileType.Portcullis:
            open = Convert.ToBoolean(pieces[2]);
            tile = new Portcullis(open);
            break;
          case TileType.GateTrigger:
            digits = Util.ToNums(pieces[2]);
            GateTrigger gt = new(new Loc(digits[0], digits[1], digits[2], digits[3]));
            gt.Found = bool.Parse(pieces[3]);
            tile = gt;
            break;
          case TileType.Landmark:
            tile = new Landmark(pieces[2]);
            break;
          case TileType.Gravestone:
            tile = new Gravestone(pieces[2]);
            break;
          case TileType.IdolAltar:
            tile = new IdolAltar(pieces[2])
            {
              IdolID = ulong.Parse(pieces[3]),
              Wall = Loc.FromStr(pieces[4])
            };
            break;
          case TileType.VaultDoor:
            open = Convert.ToBoolean(pieces[2]);
            Enum.TryParse(pieces[3], out Metals met);
            tile = new VaultDoor(open, met);
            break;
          case TileType.FireJetTrap:
            open = Convert.ToBoolean(pieces[2]);
            Enum.TryParse(pieces[3], out Dir dir);
            tile = new FireJetTrap(open, dir);
            break;
          case TileType.JetTrigger:
            digits = Util.ToNums(pieces[2]);
            tile = new JetTrigger(new Loc(digits[0], digits[1], digits[2], digits[3]), Convert.ToBoolean(pieces[3]));
            break;
          case TileType.BridgeTrigger:
            if (pieces[3] != "")
              tilesSet = [.. pieces[3].Split('|').Select(Loc.FromStr)];
            tile = new BridgeTrigger()
            {
              Triggered = bool.Parse(pieces[2]),
              BridgeTiles = tilesSet
            };
            break;
          case TileType.HiddenBridgeCollapseTrap:
            if (pieces[3] != "")
              tilesSet = [.. pieces[3].Split('|').Select(Loc.FromStr)];
            tile = new BridgeCollapseTrap()
            {
              Triggered = bool.Parse(pieces[2]),
              BridgeTiles = tilesSet
            };
            break;
          case TileType.BusinessSign:
            tile = new BusinessSign(pieces[2]);
            break;
          case TileType.MonsterWall:
            Glyph glyph = Glyph.TextToGlyph(pieces[2]);
            ulong monsterId = ulong.Parse(pieces[3]);
            tile = new MonsterWall(glyph, monsterId);
            break;
          case TileType.Lever:
            bool on = bool.Parse(pieces[2]);
            digits = Util.ToNums(pieces[3]);
            Loc gateLoc = new(digits[0], digits[1], digits[2], digits[3]);
            Lever l = new(TileType.Lever, on, gateLoc);
            tile = l;
            break;

        }
      }
      catch (Exception ex)
      {
        Console.WriteLine(ex.Message);
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
    Map map = new(sm.Width, sm.Height)
    {
      DiggableFloor = sm.DiggableFloor,
      Submerged = sm.Submerged
    };

    if (sm.Tiles is null || sm.SpecialTiles is null)
      throw new Exception("Invalid save game data!");

    map.Alerts = sm.Alerts;
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
  [JsonInclude]
  public HashSet<Loc> LocListeners { get; set; } = [];
  [JsonInclude]
  public List<string> ConditionalEvents { get; set; } = [];

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
    var fields = txt.Split(Constants.SEPARATOR);
    var p = new Player(fields[2]);

    Enum.TryParse(fields[0], out PlayerLineage charClass);
    p.Lineage = charClass;
    Enum.TryParse(fields[1], out PlayerBackground background);
    p.Background = background;

    p.ID = ulong.Parse(fields[3]);
    p.Loc = Loc.FromStr(fields[5]);

    // Parse the traits
    if (fields[6] != "")
    {
      foreach (var t in fields[6].Split('`'))
      {
        var trait = TraitFactory.FromText(t, p);
        p.Traits.Add(trait);
      }
    }
    p.Stats = StatsFromText(fields[7]);
    p.Recovery = Util.ToDouble(fields[9]);

    if (fields[12] != "" || fields[10] != "")
    {
      p.Inventory = new Inventory(p.ID, objDb)
      {
        Zorkmids = int.Parse(fields[10]),
        LastSlot = fields[11][0]
      };
      p.Inventory.RestoreFromText(fields[12]);
    }

    if (fields[13] != "")
    {
      p.SpellsKnown = [.. fields[13].Split(',')];
    }

    return p;
  }

  static Mob InflateMob(string txt, GameObjectDB objDb)
  {
    var fields = txt.Split(Constants.SEPARATOR);
    var mob = new Mob()
    {
      ID = ulong.Parse(fields[2]),
      Name = fields[1],
      Glyph = Glyph.TextToGlyph(fields[3]),
      Loc = Loc.FromStr(fields[4]),
      Recovery = Util.ToDouble(fields[8]),
      Appearance = fields[12]
    };

    string behaviourStr = fields[0];
    if (Type.GetType($"Yarl2.{behaviourStr}") is Type type)
    {
      IBehaviour behaviour = (IBehaviour)(Activator.CreateInstance(type) ?? throw new Exception("Unable deserialize behaviour"));
      mob.SetBehaviour(behaviour);
    }

    // Parse the traits
    if (fields[5] != "")
    {      
      foreach (var t in fields[5].Split('`'))
      {
        var trait = TraitFactory.FromText(t, mob);
        mob.Traits.Add(trait);
      }
    }
    
    mob.Stats = StatsFromText(fields[6]);

    if (fields[11] != "" || fields[9] != "")
    {
      mob.Inventory = new Inventory(mob.ID, objDb)
      {
        Zorkmids = int.Parse(fields[9]),
        LastSlot = fields[10][0]
      };
      mob.Inventory.RestoreFromText(fields[11]);
    }

    if (fields[13] != "")
    {
      foreach (var s in fields[13].Split('`'))
      {
        Power p = Power.FromText(s);
        mob.Powers.Add(p);
      }
    }
 
    return mob;
  }

  static Item InflateItem(string txt)
  {    
    string[] fields = txt.Split(Constants.SEPARATOR);
    
    Enum.TryParse(fields[0], out ItemType itemType);
    Item item = new()
    {
      Type = itemType,
      Name = fields[1],
      ID = ulong.Parse(fields[2]),
      Glyph = Glyph.TextToGlyph(fields[3]),
      Loc = Loc.FromStr(fields[4]),
      Slot = fields[6][0],
      Equipped = bool.Parse(fields[7].ToLower()),
      ContainedBy = ulong.Parse(fields[8]),
      Value = int.Parse(fields[9]),
    };

    if (fields[5] != "")
    {
      foreach (string t in fields[5].Split('`'))
      {
        Trait trait = TraitFactory.FromText(t, item);
        item.Traits.Add(trait);
      }
    }

    item.SetZ(int.Parse(fields[10]));

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

    throw new Exception("Hmm this shouldn't happen!");
  }

  public static GameObjDBSave Shrink(GameObjectDB objDb)
  {
    GameObjDBSave sidb = new();

    sidb.LocListeners = objDb.LocListeners;
    
    foreach (var kvp in objDb.Objs)
    {
      var obj = kvp.Value;
      if (obj is Player player)
      {
        var sb = new StringBuilder("Player:");
        sb.Append(player.Lineage);
        sb.Append(Constants.SEPARATOR);
        sb.Append(player.Background);
        sb.Append(Constants.SEPARATOR);
        sb.Append(obj.ToString());
        sb.Append(Constants.SEPARATOR);
        sb.Append(StatsToText(player.Stats));
        sb.Append(Constants.SEPARATOR);
        sb.Append(player.Energy);
        sb.Append(Constants.SEPARATOR);
        sb.Append(player.Recovery);
        sb.Append(Constants.SEPARATOR);
        sb.Append(player.Inventory.Zorkmids);
        sb.Append(Constants.SEPARATOR);
        sb.Append(player.Inventory.LastSlot);
        sb.Append(Constants.SEPARATOR);
        sb.Append(player.Inventory.ToText());
        sb.Append(Constants.SEPARATOR);
        sb.Append(string.Join(',', player.SpellsKnown));

        sidb.Objects.Add(sb.ToString());
      }
      else if (obj is Mob mob)
      {
        StringBuilder sb = new("Mob:");
        sb.Append(mob.Behaviour.GetType().Name);
        sb.Append(Constants.SEPARATOR);
        sb.Append(obj.ToString());
        sb.Append(Constants.SEPARATOR);
        sb.Append(StatsToText(mob.Stats));
        sb.Append(Constants.SEPARATOR);
        sb.Append(mob.Energy);
        sb.Append(Constants.SEPARATOR);
        sb.Append(mob.Recovery);
        sb.Append(Constants.SEPARATOR);
        sb.Append(mob.Inventory.Zorkmids);
        sb.Append(Constants.SEPARATOR);
        sb.Append(mob.Inventory.LastSlot);
        sb.Append(Constants.SEPARATOR);
        sb.Append(mob.Inventory.ToText());
        sb.Append(Constants.SEPARATOR);
        sb.Append(mob.Appearance);

        string powers = string.Join("`", mob.Powers.Select(p => p.ToString()));
        sb.Append(Constants.SEPARATOR);
        sb.Append(powers);

        sidb.Objects.Add(sb.ToString());
      }
      else if (obj is Item item)
      {
        var sb = new StringBuilder("Item:");
        sb.Append(item.Type);
        sb.Append(Constants.SEPARATOR);
        sb.Append(obj.ToString());
        sb.Append(Constants.SEPARATOR);
        sb.Append(item.Slot);
        sb.Append(Constants.SEPARATOR);
        sb.Append(item.Equipped);
        sb.Append(Constants.SEPARATOR);
        sb.Append(item.ContainedBy);
        sb.Append(Constants.SEPARATOR);
        sb.Append(item.Value);
        sb.Append(Constants.SEPARATOR);
        sb.Append(item.Z());

        sidb.Objects.Add(sb.ToString());
      }
    }

    foreach (ConditionalEvent ce in objDb.ConditionalEvents)
    {
      sidb.ConditionalEvents.Add(ce.AsText());
    }

    return sidb;
  }

  public static GameObjectDB Inflate(GameObjDBSave sidb)
  {
    GameObjectDB objDb = new();

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
          objDb.SetActorToLoc(obj.Loc, obj.ID);
      }
    }

    objDb.LocListeners = sidb.LocListeners;

    GameObj.SetSeed(maxID + 1);

    foreach (string ce in sidb.ConditionalEvents)
    {
      objDb.ConditionalEvents.Add(ConditionalEvent.FromText(ce));
    }

    return objDb;
  }
}
