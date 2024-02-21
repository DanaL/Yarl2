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
using System.Text.RegularExpressions;

namespace Yarl2;

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
    public static void WriteSaveGame(string playerName, Player player, Campaign campaign, GameState gameState)
    {
        var p = ShrunkenPlayer.Shrink(player);
        var sgi = new SaveGameInfo(p, CampaignSave.Shrink(campaign), gameState.CurrLevel, 
                                    gameState.CurrDungeon, 
                                    GameObjDBSaver.Shrink(gameState.ObjDB));
        var bytes = JsonSerializer.SerializeToUtf8Bytes(sgi,
                        new JsonSerializerOptions { WriteIndented = false, IncludeFields = true });

        // In the future, when this is a real game, I'm going to have check player names for invalid characters or build a 
        // little database of players vs saved games
        string filename = $"{playerName}.dat";
        File.WriteAllBytes(filename, bytes);
    }

    public static (Player?, Campaign, GameObjectDB) LoadSaveGame(string playerName)
    {
        string filename = $"{playerName}.dat";
        var bytes = File.ReadAllBytes(filename);
        var sgi = JsonSerializer.Deserialize<SaveGameInfo>(bytes);

        var p = ShrunkenPlayer.Inflate(sgi.Player);
        var c = CampaignSave.Inflate(sgi.Campaign);
        var objDB = GameObjDBSaver.Inflate(sgi.ItemDB);
        objDB._objs.Add(p.ID, p);
        c.CurrentLevel = sgi.CurrentLevel;
        c.CurrentDungeon = sgi.CurrentDungeon;

        return (p, c, objDB);
    }

    public static bool SaveFileExists(string playerName) => File.Exists($"{playerName}.dat");
}

internal class ShrunkenPlayer
{
    public ulong ID { get; set; }
    public string Name { get; set; }
    public int Row { get; set; }
    public int Col { get; set; }
    public int MaxHP { get; set; }
    public int CurrHP { get; set; }
    [JsonInclude]
    public ShrunkenInventory Inventory { get; set; }

    public static ShrunkenPlayer Shrink(Player p)
    {
        return new ShrunkenPlayer()
        {
            ID = p.ID,
            Name = p.Name,
            MaxHP = p.MaxHP,
            CurrHP = p.CurrHP,
            Inventory = ShrunkenInventory.Shrink(p.Inventory),
            Row = p.Row,
            Col = p.Col
        };
    }

    public static Player Inflate(ShrunkenPlayer sp)
    {
        return new Player(sp.Name, sp.Row, sp.Col)
        {
            ID = sp.ID,
            MaxHP = sp.MaxHP,
            CurrHP = sp.CurrHP,
            Inventory = ShrunkenInventory.Inflate(sp.Inventory, sp.ID)
        };
    }
}

record InvItemKVP(char Slot, string ItemText);

class ColourSave
{
    public static string ColourToText(Colour colour)
    {
        if (colour == Colours.WHITE) return "white";
        else if (colour == Colours.BLACK) return "black";
        else if (colour == Colours.GREY) return "grey";
        else if (colour == Colours.LIGHT_GREY) return "lightgrey";
        else if (colour == Colours.DARK_GREY) return "darkgrey";
        else if (colour == Colours.YELLOW) return "yellow";
        else if (colour == Colours.YELLOW_ORANGE) return "yelloworange";
        else if (colour == Colours.LIGHT_BROWN) return "lightbrown";
        else if (colour == Colours.BROWN) return "brown";
        else if (colour == Colours.GREEN) return "green";
        else if (colour == Colours.DARK_GREEN) return "darkgreen";
        else if (colour == Colours.LIME_GREEN) return "limegreen";
        else if (colour == Colours.BLUE) return "blue";
        else if (colour == Colours.LIGHT_BLUE) return "lightblue";
        else if (colour == Colours.DARK_BLUE) return "darkblue";
        else if (colour == Colours.BRIGHT_RED) return "brightred";
        else if (colour == Colours.DULL_RED) return "dullred";
        else if (colour == Colours.TORCH_ORANGE) return "torchorange";
        else if (colour == Colours.TORCH_RED) return "torchred";
        else if (colour == Colours.TORCH_YELLOW) return "torchyellow";
        else throw new Exception("Hmm I don't know that colour");
    }
  
    public static Colour TextToColour(string colour)
    {
        if (colour == "white") return Colours.WHITE;
        else if (colour == "black") return Colours.BLACK;
        else if (colour == "grey") return Colours.GREY;
        else if (colour == "lightgrey") return Colours.LIGHT_GREY;
        else if (colour == "darkgrey") return Colours.DARK_GREY;
        else if (colour == "yellow") return Colours.YELLOW;
        else if (colour == "yelloworange") return Colours.YELLOW_ORANGE;
        else if (colour == "lightbrown") return Colours.LIGHT_BROWN;
        else if (colour == "brown") return Colours.BROWN;
        else if (colour == "darkgreen") return Colours.DARK_GREEN;
        else if (colour == "limegreen") return Colours.LIME_GREEN;
        else if (colour == "blue") return Colours.BLUE;
        else if (colour == "lightblue") return Colours.LIGHT_BLUE;
        else if (colour == "darkblue") return Colours.DARK_BLUE;
        else if (colour == "brightred") return Colours.BRIGHT_RED;
        else if (colour == "dullred") return Colours.DULL_RED;
        else if (colour == "torchorange") return Colours.TORCH_ORANGE;
        else if (colour == "torchred") return Colours.TORCH_RED;
        else if (colour == "torchyellow") return Colours.TORCH_YELLOW;
        else throw new Exception("Hmm I don't know that colour");
    }
}

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
        _ => throw new Exception($"Hmm I don't know about Item Type {text}")
    };

    static string GlyphToText(Glyph glyph) => $"{glyph.Ch};{ColourSave.ColourToText(glyph.Lit)};{ColourSave.ColourToText(glyph.Unlit)}";
    static Glyph TextToGlyph(string text)
    {
        var p = text.Split(';');
        return new Glyph(p[0][0], ColourSave.TextToColour(p[1]), ColourSave.TextToColour(p[2]));
    }

    static Loc TextToLoc(string text)
    {
        var digits = text.Split(',').Select(int.Parse).ToArray();
        return new Loc(digits[0], digits[1], digits[2], digits[3]);
    }

    public static string ItemToText(Item item)
    {
        string txt = $"{item.ID}|{item.Loc}|{item.Name}|{item.Stackable}|{item.Slot}|";
        txt += $"{item.Equiped}|{item.Count}|{item.ContainedBy}|";
        txt += string.Join(',', item.Adjectives);
        txt += $"|" + GlyphToText(item.Glyph);

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
            Count = int.Parse(pieces[6]),
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

class ShrunkenInventory
{
    public char NextSlot { get; set; }
    public int Zorkmids { get; set; }
    [JsonInclude]
    public List<InvItemKVP> Items { get; set; }

    public static ShrunkenInventory Shrink(Inventory inv)
    {        
        return new ShrunkenInventory()
        {
            Zorkmids = inv.Zorkmids,
            NextSlot = inv.NextSlot,
            Items = inv.ToKVP().Select(kvp => new InvItemKVP(kvp.Item1, ItemSaver.ItemToText(kvp.Item2))).ToList()
        };
    }

    public static Inventory Inflate(ShrunkenInventory sp, ulong ownerID)
    {
        var inv = new Inventory(ownerID);
        
        foreach (var kvp in sp.Items)
        {
            inv.Add(ItemSaver.TextToItem(kvp.ItemText), ownerID);
        }
        
        inv.Zorkmids = sp.Zorkmids;
        inv.NextSlot = sp.NextSlot;

        return inv;
    }
}

class CampaignSave
{
    public int CurrentDungeon { get; set; }
    public int CurrentLevel { get; set; }
    [JsonInclude]
    public Dictionary<int, DungeonSaver> Dungeons = [];

    public static CampaignSave Shrink(Campaign c)
    {
        CampaignSave sc = new()
        {
            CurrentDungeon = c.CurrentDungeon,
            CurrentLevel = c.CurrentLevel
        };
        
        foreach (var k in c.Dungeons.Keys)
        {
            sc.Dungeons.Add(k, DungeonSaver.Shrink(c.Dungeons[k]));
        }

        return sc;
    }

    public static Campaign Inflate(CampaignSave sc)
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

internal class MapSaver()
{    
    public int Height { get; set; }
    public int Width { get; set; }
    [JsonInclude]
    int[]? Tiles{ get; set; }
    [JsonInclude]
    List<string>? SpecialTiles { get; set; }

    public static MapSaver Shrink(Map map)
    {
        MapSaver sm = new MapSaver
        {
            Height = map.Height,
            Width = map.Width,
            Tiles = new int[map.Tiles.Length],
            SpecialTiles = []
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
                    digits = Regex.Split(pieces[2], @"\D+")
                                    .Select(int.Parse).ToArray();
                    ((Portal)tile).Destination = new Loc(digits[0], digits[1], digits[2], digits[3]);
                    break;
                case TileType.Upstairs:
                    tile = new Upstairs(pieces[3]);
                    digits = Regex.Split(pieces[2], @"\D+")
                                    .Select(int.Parse).ToArray();
                    ((Upstairs)tile).Destination = new Loc(digits[0], digits[1], digits[2], digits[3]);
                    break;
                case TileType.Downstairs:
                    tile = new Downstairs(pieces[3]);
                    digits = Regex.Split(pieces[2], @"\D+")
                                    .Select(int.Parse).ToArray();
                    ((Downstairs)tile).Destination = new Loc(digits[0], digits[1], digits[2], digits[3]);
                    break;
                case TileType.Door:
                    bool open = Convert.ToBoolean(pieces[2]);
                    tile = new Door(TileType.Door, open);
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
class GameObjDBSaver
{
    // Come to think of it, maybe the ID seed belongs in the GameObjDB class
    // instead of being a static field in GameObj hmmm
    public ulong GameObjSeed { get; set; }
    
    [JsonInclude]
    public Dictionary<string, List<string>> ItemsAtLoc = [];
    //public Dictionary<Loc, Actor> _actorLocs = [];
    //public Dictionary<ulong, GameObj> _objs = [];

    public static GameObjDBSaver Shrink(GameObjectDB goDB)
    {
        var sidb = new GameObjDBSaver{ GameObjSeed = GameObj.Seed };

        foreach (var kvp in goDB._itemLocs)
        {
            var itemStrs = kvp.Value.Select(ItemSaver.ItemToText).ToList();
            sidb.ItemsAtLoc.Add(kvp.Key.ToString(), itemStrs);
        }
        // foreach (var p in goDB.ItemDump())
        // {
        //     sidb.ItemsKeys.Add(p.Item1);
        //     sidb.Items.Add(p.Item2);
        // }
        
        // foreach (var a in goDB.ActorDump())
        // {
        //     sidb.MonsterKeys.Add(a.Item1);
        //     sidb.Monsters.Add(a.Item2);
        // }

        return sidb;
    }

    public static GameObjectDB Inflate(GameObjDBSaver sidb)
    {
        GameObj.SetSeed(sidb.GameObjSeed);
        var goDB = new GameObjectDB();
        
        foreach (var kvp in sidb.ItemsAtLoc)
        {
            var items = kvp.Value.Select(ItemSaver.TextToItem).ToList();
            goDB._itemLocs.Add(Loc.FromText(kvp.Key), items);
            foreach (var item in items)
            {
                goDB._objs.Add(item.ID, item);
            }
        }

        return goDB;
    }
}

internal record SaveGameInfo(ShrunkenPlayer? Player, CampaignSave? Campaign, int CurrentLevel, int CurrentDungeon,
                                GameObjDBSaver ItemDB);

// SIGH so for tuples, the JsonSerliazer won't serialize a tuple of ints. So, let's make a little object that
// *can* be serialized
internal record struct RememberedSq(int A, int B, int C, Sqr Sqr)
{
    public static RememberedSq FromTuple((int, int, int) t, Sqr sqr) => new RememberedSq(t.Item1, t.Item2, t.Item3, sqr);
    public (int, int, int) ToTuple() => (A, B, C);
}