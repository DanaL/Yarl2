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
record SqrSave(string Fg, string Bg, char Ch);

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
    var bytes = File.ReadAllBytes(path);
    var sgi = JsonSerializer.Deserialize<SaveGameInfo>(bytes);

    if (sgi is null)
      return [];

    List<Sqr> sqrs = [];
    foreach (var s in sgi.Preview)
    {
      sqrs.Add(new Sqr(Colours.TextToColour(s.Fg), Colours.TextToColour(s.Bg), s.Ch));
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

    // At the moment, EndOfRound is the only type of listener in the game
    foreach (var l in objDb.ActiveListeners())
    {
      gs.RegisterForEvent(GameEventType.EndOfRound, l);
    }

    Item.IDInfo = sgi.IDInfo;

    return gs;
  }

  public static List<SaveFileInfo> GetSavedGames()
  {
    List<SaveFileInfo> files = [];

    DirectoryInfo dir = new(Util.SavePath);
    if (!dir.Exists)
      throw new Exception("Unable to find or access saved game folder!");

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
      for (int c=  midC - 5; c < midC + 6; c++)
      {
        Sqr sqr = ui.SqsOnScreen[r, c];
        SqrSave s = new(Colours.ColourToText(sqr.Fg), Colours.ColourToText(sqr.Bg), sqr.Ch);
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
  public List<string> Facts { get; set; } = [];
  public List<string> HistoricalEvents { get; set; } = [];
  public List<string> Nations { get; set; } = [];
  public string RulerInfo { get; set; } = "";
  public VillainType Villain { get; set; }
  public string VillainName { get; set; } = "";
  [JsonInclude]
  public List<string> MonsterDecks { get; set; } = [];

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

    if (c.FactDb is null)
    {
      throw new Exception("FactDb is null!");
    }

    var wtf = c.FactDb!.Facts.Select(f => f.ToString()).ToList();

    CampaignSaver sc = new()
    {
      Town = town,
      Facts = c.FactDb!.Facts.Select(f => f.ToString()!).ToList(),
      HistoricalEvents = c.FactDb!.HistoricalEvents.Select(he => he.ToString()!).ToList(), 
      Nations = c.FactDb!.Nations.Select(n => n.ToString()).ToList(),  
      RulerInfo = c.FactDb!.Ruler.ToString(),
      Villain = c.FactDb!.Villain,
      VillainName = c.FactDb!.VillainName,
      MonsterDecks = c.MonsterDecks.Select(deck => deck.ToString()).ToList()
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

    campaign.MonsterDecks = sc.MonsterDecks.Select(MonsterDeck.FromString).ToList();

    return campaign;
  }
}

internal class DungeonSaver
{
  public int ID { get; set; }
  public string? ArrivalMessage { get; set; }

  [JsonInclude]
  public List<string> RememberedLocs;
  [JsonInclude]
  public Dictionary<int, MapSaver> LevelMaps;

  public DungeonSaver()
  {
    RememberedLocs = [];
    LevelMaps = [];
  }

  public static DungeonSaver Shrink(Dungeon dungeon)
  {
    var sd = new DungeonSaver()
    {
      ID = dungeon.ID,
      ArrivalMessage = dungeon.ArrivalMessage,
      RememberedLocs = []
    };

    foreach (var k in dungeon.LevelMaps.Keys)
    {
      sd.LevelMaps.Add(k, MapSaver.Shrink(dungeon.LevelMaps[k]));
    }

    foreach (var kvp in dungeon.RememberedLocs)
    {
      string s = $"{kvp.Key};{kvp.Value}";
      sd.RememberedLocs.Add(s);      
    }

    return sd;
  }

  public static Dungeon Inflate(DungeonSaver sd)
  {
    Dungeon d = new Dungeon(sd.ID, sd.ArrivalMessage ?? "");
    
    foreach (string s in sd.RememberedLocs)
    {
      var pieces = s.Split(';');
      Loc loc = Loc.FromStr(pieces[0]);
      Glyph g = Glyph.TextToGlyph(pieces[1]);
      d.RememberedLocs[loc] = g;
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
  public List<string> Alerts { get; set; } = [];

  public static MapSaver Shrink(Map map)
  {  
    MapSaver sm = new()
    {
      Height = map.Height,
      Width = map.Width,
      Tiles = new int[map.Tiles.Length],
      SpecialTiles = [],   
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
        case TileType.Shortcut:
          tile = new Shortcut();
          digits = Util.ToNums(pieces[2]);
          ((Shortcut)tile).Destination = new Loc(digits[0], digits[1], digits[2], digits[3]);
          break;
        case TileType.ShortcutDown:
          tile = new ShortcutDown();
          digits = Util.ToNums(pieces[2]);
          ((Shortcut)tile).Destination = new Loc(digits[0], digits[1], digits[2], digits[3]);
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
          tile = new BridgeTrigger()
          {
            Triggered = bool.Parse(pieces[2]),
            BridgeTiles = pieces[3].Split('|').Select(Loc.FromStr).ToHashSet()
          };
          break;
        case TileType.HiddenBridgeCollapseTrap:
          tile = new BridgeCollapseTrap()
          {
            Triggered = bool.Parse(pieces[2]),
            BridgeTiles = pieces[3].Split('|').Select(Loc.FromStr).ToHashSet()
          };
          break;
        case TileType.BusinessSign:
          tile = new BusinessSign(pieces[2]);
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
    p.Energy = double.Parse(fields[8]);
    p.Recovery = double.Parse(fields[9]);

    if (fields[12] != "")
    {
      p.Inventory = new Inventory(p.ID, objDb)
      {
        Zorkmids = int.Parse(fields[10]),
        NextSlot = fields[11][0]
      };
      p.Inventory.RestoreFromText(fields[12]);
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
      Glyph = Glyph.TextToGlyph(fields[4]),
      Loc = Loc.FromStr(fields[5]),
      Energy = double.Parse(fields[8]),
      Recovery = double.Parse(fields[9]),
      Appearance = fields[13]
    };

    string behaviourStr = fields[1];
    if (Type.GetType($"Yarl2.{behaviourStr}") is Type type)
    {
      IBehaviour behaviour = (IBehaviour)(Activator.CreateInstance(type) ?? throw new Exception("Unable deserialize behaviour"));
      mob.SetBehaviour(behaviour);
    }

    // Parse the traits
    if (fields[6] != "")
    {
      bool flying = false;
      bool immobile = false;
      bool intelligent = false;

      foreach (var t in fields[6].Split('`'))
      {
        var trait = TraitFactory.FromText(t, mob);
        mob.Traits.Add(trait);

        if (trait is FlyingTrait || trait is FloatingTrait)
          flying = true;
        if (trait is ImmobileTrait)
          immobile = true;
        if (trait is IntelligentTrait) 
          intelligent = true;
      }

      if (flying)
        mob.MoveStrategy = new SimpleFlightMoveStrategy();
      else if (intelligent)
        mob.MoveStrategy = new DoorOpeningMoveStrategy();
      else if (immobile)
        mob.MoveStrategy = new WallMoveStrategy();
      else
        mob.MoveStrategy = new DumbMoveStrategy();
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

    if (fields[14] != "")
    {
      foreach (var a in fields[14].Split('`'))
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
      Glyph = Glyph.TextToGlyph(fields[3]),
      Loc = Loc.FromStr(fields[4]),
      Slot = fields[6][0],
      Equiped = bool.Parse(fields[7].ToLower()),
      ContainedBy = ulong.Parse(fields[8]),
      Value = int.Parse(fields[9]),
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
    var sidb = new GameObjDBSave();

    sidb.LocListeners = objDb.LocListeners;

    foreach (var kvp in objDb.Objs)
    {
      var obj = kvp.Value;
      if (obj is Player player)
      {
        var sb = new StringBuilder("Player:");
        sb.Append(player.Lineage);
        sb.Append('|');
        sb.Append(player.Background);
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
        sb.Append(item.Slot);
        sb.Append('|');
        sb.Append(item.Equiped);
        sb.Append('|');
        sb.Append(item.ContainedBy);
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

    objDb.LocListeners = sidb.LocListeners;

    GameObj.SetSeed(maxID + 1);

    return objDb;
  }
}
