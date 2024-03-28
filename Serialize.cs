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

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Yarl2;

//record SaveGameInfo(PlayerSaver? Player, CampaignSaver? Campaign, int CurrentLevel, int CurrentDungeon,
//                                GameObjDBSaver ItemDB, ulong Turn, List<MsgHistory> MessageHistory);

record SaveGameInfo(CampaignSaver Campaign, GameStateSave GameStateSave, PlayerSave Player);

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
        GameObjDBSave.Shrink(gameState.ObjDb);

        var sgi = new SaveGameInfo(CampaignSaver.Shrink(gameState.Campaign), GameStateSave.Shrink(gameState), PlayerSave.Shrink(gameState.Player));

        var bytes = JsonSerializer.SerializeToUtf8Bytes(sgi,
                        new JsonSerializerOptions { WriteIndented = false, IncludeFields = true });
        // In the future, when this is a real game, I'm going to have check player names for invalid characters or build a 
        // little database of players vs saved games
        string filename = $"{gameState.Player.Name}.dat";
        File.WriteAllBytes(filename, bytes);
    }

    public static void WriteSaveGame(string playerName, Player player, Campaign campaign, GameState gameState, List<MsgHistory> MessageHistory)
    {
        //var p = PlayerSaver.Shrink(player);
        //var sgi = new SaveGameInfo(p, CampaignSaver.Shrink(campaign), gameState.CurrLevel, 
        //                            gameState.CurrDungeon, 
        //                            GameObjDBSaver.Shrink(gameState.ObjDB), gameState.Turn, MessageHistory);
        //var bytes = JsonSerializer.SerializeToUtf8Bytes(sgi,
        //                new JsonSerializerOptions { WriteIndented = false, IncludeFields = true });

        //// In the future, when this is a real game, I'm going to have check player names for invalid characters or build a 
        //// little database of players vs saved games
        //string filename = $"{playerName}.dat";
        //File.WriteAllBytes(filename, bytes);
    }

    //public static (Player?, Campaign, GameObjectDB, ulong, List<MsgHistory>) LoadSaveGame(string playerName)
    public static (GameState, Loc) LoadSaveGame(string playerName, Options options, UserInterface ui)
    {
        string filename = $"{playerName}.dat";
        var bytes = File.ReadAllBytes(filename);
        var sgi = JsonSerializer.Deserialize<SaveGameInfo>(bytes);

        var campaign = CampaignSaver.Inflate(sgi.Campaign);
        var gs = GameStateSave.Inflate(campaign, sgi.GameStateSave, options, ui);
        gs.Player = PlayerSave.Inflate(sgi.Player);
        //var objDB = GameObjDBSaver.Inflate(sgi.ItemDB);
        return (gs, gs.Player.Loc);
    }
    //{

    //    var p = PlayerSaver.Inflate(sgi.Player, objDB);
    //    var c = CampaignSaver.Inflate(sgi.Campaign);
    //    c.CurrentDungeon = sgi.CurrentDungeon;
    //    c.CurrentLevel = sgi.CurrentLevel;        
    //    objDB._objs.Add(p.ID, p);
    //    objDB.AddToLoc(p.Loc, p);

    //    return (p, c, objDB, sgi.Turn, sgi.MessageHistory);
    //}

    public static bool SaveFileExists(string playerName) => File.Exists($"{playerName}.dat");
}

class GameStateSave
{
    public int CurrLevel { get; set; }
    public int CurrDungeonID { get; set; }
    public ulong Turn { get; set; }

    public static GameStateSave Shrink(GameState gs) => new GameStateSave()
    {
        CurrDungeonID = gs.CurrDungeonID,
        CurrLevel = gs.CurrLevel,
        Turn = gs.Turn
    };

    public static GameState Inflate(Campaign camp, GameStateSave gss, Options opt, UserInterface ui)
    {
        var gs = new GameState(null, camp, opt, ui, new Random(), 0);
        gs.CurrDungeonID = gss.CurrDungeonID;
        gs.CurrLevel = gs.CurrLevel;
        gs.Turn = gs.Turn;

        return gs;
    }
}

class PlayerSave
{
    public ulong ID { get; set; }
    public string Name { get; set; }
    public Loc Loc {  get; set; }

    [JsonInclude]
    public InventorySaver Inventory { get; set; }

    [JsonInclude]
    public List<AttrStatKVP> Stats { get; set; }

    public static PlayerSave Shrink(Player p) => new()
    {
        ID = p.ID,
        Name = p.Name,
        Stats = AttrStatKVP.Save(p.Stats),
        Loc = p.Loc
        //Inventory = InventorySaver.Shrink(p.Inventory),
        //Loc = p.Loc.ToString()
    };

    public static Player Inflate(PlayerSave sp) => new Player(sp.Name)
    {
        ID = sp.ID,
        Stats = AttrStatKVP.Load(sp.Stats),
        Loc = sp.Loc
        //Inventory = InventorySaver.Inflate(sp.Inventory, sp.ID, objDb),
        //Loc = Yarl2.Loc.FromText(sp.Loc)
    };
}

class MonsterSaver
{
    public ulong ID { get; set; }
    public string Name { get; set; }
    public string Loc { get; set; }

    [JsonInclude]
    public List<AttrStatKVP> Stats;

    public static MonsterSaver Shrink(Monster m) => new()
    {
        ID = m.ID,
        Name = m.Name,
        Stats = AttrStatKVP.Save(m.Stats),
        Loc = m.Loc.ToString()
    };

    public static Monster Inflate(MonsterSaver ms)
    {
        //var m = (Monster) MonsterFactory.Get(ms.Name);
        //m.ID = ms.ID;
        //m.Stats = AttrStatKVP.Load(ms.Stats);
        //m.Loc = Yarl2.Loc.FromText(ms.Loc);

        //return m;
        return null;
    }
}

record AttrStatKVP(string Attr, Stat Stat)
{
    public static Dictionary<Attribute, Stat> Load(List<AttrStatKVP> kvps)
    {
        Dictionary<Attribute, Stat> stats = [];

        foreach (var kvp in kvps) 
        {
            Enum.TryParse(kvp.Attr, out Attribute a);
            stats.Add(a, kvp.Stat);
        }

        return stats;
    }

    public static List<AttrStatKVP> Save(Dictionary<Attribute, Stat> stats)
    {
        return stats.Select(kvp => new AttrStatKVP(kvp.Key.ToString(), kvp.Value))
                    .ToList();
    }
}

record InvItemKVP(char Slot, string ItemText);

// The Item class and its subclasses has proven annoying to serialize so I'm
// going to do a bespoke text format for them. Not too happy about this because
// I'll probably create a bunch of bugs in the meantime :'(
class ItemSaver
{
    static ItemType TextToItemType(string text) => text switch
    {
        "Armour" => ItemType.Armour,
        "Weapon" => ItemType.Weapon,
        "Zorkmid" => ItemType.Zorkmid,
        "Tool" => ItemType.Tool,
        "Document" => ItemType.Document,
        _ => throw new Exception($"Hmm I don't know about Item Type {text}")
    };
   
    static Glyph TextToGlyph(string text)
    {
        var p = text.Split(';');
        return new Glyph(p[0][0], Colours.TextToColour(p[1]), Colours.TextToColour(p[2]), Colours.BLACK);
    }

    static Loc TextToLoc(string text)
    {
        var digits = text.Split(',').Select(int.Parse).ToArray();
        return new Loc(digits[0], digits[1], digits[2], digits[3]);
    }

    public static string ItemToText(Item item)
    {
        string txt = $"{item.ID}|{item.Loc}|{item.Name}|{item.Stackable}|{item.Slot}|";
        txt += $"{item.Equiped}|{item.Value}|{item.ContainedBy}|";
        txt += string.Join(',', item.Adjectives);
        //txt += $"|" + GlyphToText(item.Glyph);

        var traits = string.Join(';', item.Traits.Select(t => t.AsText()));
        if (traits.Length > 0)
            txt += "|" + traits;

        txt += $"|{item.Type}";

        return txt;
    }

    public static Item TextToItem(string text)
    {
        var pieces = text.Split('|');
        
        List<string> adjectives = pieces[8].Split(',').Where(s => s != "")
                                                      .ToList();
        var item = new Item()
        {
            ID = ulong.Parse(pieces[0]),
            Loc = TextToLoc(pieces[1]),
            Name = pieces[2],
            Stackable = bool.Parse(pieces[3]),
            Slot = pieces[4] == "" ? '\0' : pieces[4][0],
            Equiped = bool.Parse(pieces[5]),
            Value = int.Parse(pieces[6]),
            ContainedBy = ulong.Parse(pieces[7]),
            Adjectives = adjectives,
            Glyph = TextToGlyph(pieces[9]),
            Type = TextToItemType(pieces.Last())
        };

        foreach (var traitStr in pieces[10].Split(';'))
            item.Traits.Add(TraitFactory.FromText(traitStr));
        
        return item;
    }
}

class InventorySaver
{
    public char NextSlot { get; set; }
    public int Zorkmids { get; set; }
    [JsonInclude]
    public List<InvItemKVP> Items { get; set; }

    public static InventorySaver Shrink(Inventory inv)
    {        
        return new InventorySaver()
        {
            Zorkmids = inv.Zorkmids,
            NextSlot = inv.NextSlot,
            //Items = inv.ToKVP().Select(kvp => new InvItemKVP(kvp.Item1, ItemSaver.ItemToText(kvp.Item2))).ToList()
        };
    }

    public static Inventory Inflate(InventorySaver sp, ulong ownerID, GameObjectDB objDb)
    {
        var inv = new Inventory(ownerID);
        
        foreach (var kvp in sp.Items)
        {
            var item = ItemSaver.TextToItem(kvp.ItemText);
            objDb.Add(item);
            inv.Add(item, ownerID);
        }
        
        inv.Zorkmids = sp.Zorkmids;
        inv.NextSlot = sp.NextSlot;

        return inv;
    }
}

class CampaignSaver
{
    [JsonInclude]
    public Dictionary<int, DungeonSaver> Dungeons = [];

    public static CampaignSaver Shrink(Campaign c)
    {
        CampaignSaver sc = new();
       
        foreach (var k in c.Dungeons.Keys)
        {
            sc.Dungeons.Add(k, DungeonSaver.Shrink(c.Dungeons[k]));
        }

        return sc;
    }

    public static Campaign Inflate(CampaignSaver sc)
    {
        Campaign campaign = new();

        foreach (var k in  sc.Dungeons.Keys)
        {
            campaign.Dungeons.Add(k, DungeonSaver.Inflate(sc.Dungeons[k]));
        }

        return campaign;
    }
}

internal class DungeonSaver
{
    public int ID { get; set; }
    public string? ArrivalMessage { get; set;  }

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
    int[]? Tiles{ get; set; }
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
            sm.Tiles[j] = (int) t.Type;
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
            int[] digits;
            var pieces = s.Split(';');
            var j = int.Parse(pieces[0]);
            var type = (TileType)int.Parse(pieces[1]);
            
            switch (type)
            {
                case TileType.Portal:
                    tile = new Portal(pieces[3]);
                    digits = Util.DigitsRegex().Split(pieces[2])
                                               .Select(int.Parse).ToArray();
                    ((Portal)tile).Destination = new Loc(digits[0], digits[1], digits[2], digits[3]);
                    break;
                case TileType.Upstairs:
                    tile = new Upstairs(pieces[3]);
                    digits = Util.DigitsRegex().Split(pieces[2])
                                    .Select(int.Parse).ToArray();
                    ((Upstairs)tile).Destination = new Loc(digits[0], digits[1], digits[2], digits[3]);
                    break;
                case TileType.Downstairs:
                    tile = new Downstairs(pieces[3]);
                    digits = Util.DigitsRegex().Split(pieces[2])
                                    .Select(int.Parse).ToArray();
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
        Map map = new Map(sm.Width, sm.Height);

        if (sm.Tiles is null || sm.SpecialTiles is null)
            throw new Exception("Invalid save game data!");

        foreach (var kvp in sm.Effects)
        {
            var d = Util.DigitsRegex().Split(kvp.Key);                                  
            var key = (int.Parse(d[1]), int.Parse(d[2]));
            map.Effects.Add(key, kvp.Value);
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
    // Come to think of it, maybe the ID seed belongs in the GameObjDB class
    // instead of being a static field in GameObj hmmm
    public ulong GameObjSeed { get; set; }
    
    [JsonInclude]
    public Dictionary<string, List<string>> ItemsAtLoc = [];
    [JsonInclude]
    public List<MonsterSaver> Monsters = [];
    
    public static GameObjDBSave Shrink(GameObjectDB objDb)
    {
        var sidb = new GameObjDBSave{ GameObjSeed = GameObj.Seed };

        foreach (var kvp in objDb.Objs)
        {
            var obj = kvp.Value;
            if (obj is Player)
            {
                Console.WriteLine("Player:" + obj.ToString());
            }
            else if (obj is Monster)
            {

            }
            else if (obj is Villager)
            {

            }
            else if (obj is Item)
            {

            }            
        }
        //foreach (var kvp in goDB._itemLocs)
        //{
        //    var itemStrs = kvp.Value.Select(ItemSaver.ItemToText).ToList();
        //    sidb.ItemsAtLoc.Add(kvp.Key.ToString(), itemStrs);
        //}

        //foreach (var kvp in goDB._objs)
        //{
        //    if (kvp.Value is Monster m)
        //    {
        //        sidb.Monsters.Add(MonsterSaver.Shrink(m));
        //    }
        //}
       
        return sidb;
    }

    public static GameObjectDB Inflate(GameObjDBSave sidb)
    {
        GameObj.SetSeed(sidb.GameObjSeed);
        var goDB = new GameObjectDB();
        
        foreach (var kvp in sidb.ItemsAtLoc)
        {
            var items = kvp.Value.Select(ItemSaver.TextToItem).ToList();
            goDB._itemLocs.Add(Loc.FromText(kvp.Key), items);
            foreach (var item in items)
            {
                goDB.Objs.Add(item.ID, item);
            }
        }

        foreach (var ms in sidb.Monsters)
        {
            var m = MonsterSaver.Inflate(ms);
            goDB.Add(m);
            goDB.AddToLoc(m.Loc, m);
        }

        return goDB;
    }
}

// SIGH so for tuples, the JsonSerliazer won't serialize a tuple of ints. So, let's make a little object that
// *can* be serialized
record struct RememberedSq(int A, int B, int C, Sqr Sqr)
{
    public static RememberedSq FromTuple((int, int, int) t, Sqr sqr) => new RememberedSq(t.Item1, t.Item2, t.Item3, sqr);
    public (int, int, int) ToTuple() => (A, B, C);
}