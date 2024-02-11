
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
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
        var sgi = new SaveGameInfo(p, ShrunkenCampaign.Shrink(campaign), gameState.CurrLevel, 
                                    gameState.CurrDungeon, 
                                    ShrunkenItemDB.Shrink(gameState.ItemDB));
        var bytes = JsonSerializer.SerializeToUtf8Bytes(sgi,
                        new JsonSerializerOptions { WriteIndented = false, IncludeFields = true });

        // In the future, when this is a real game, I'm going to have check player names for invalid characters or build a 
        // little database of players vs saved games
        string filename = $"{playerName}.dat";
        File.WriteAllBytes(filename, bytes);
    }

    public static (Player?, Campaign, ItemDB) LoadSaveGame(string playerName)
    {
        string filename = $"{playerName}.dat";
        var bytes = File.ReadAllBytes(filename);
        var sgi = JsonSerializer.Deserialize<SaveGameInfo>(bytes);

        var p = ShrunkenPlayer.Inflate(sgi.Player);
        var c = ShrunkenCampaign.Inflate(sgi.Campaign);
        var itemDB = ShrunkenItemDB.Inflate(sgi.ItemDB);
        c.CurrentLevel = sgi.CurrentLevel;
        c.CurrentDungeon = sgi.CurrentDungeon;

        return (p, c, itemDB);
    }

    public static bool SaveFileExists(string playerName) => File.Exists($"{playerName}.dat");
}

internal class ShrunkenPlayer
{
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
            MaxHP = sp.MaxHP,
            CurrHP = sp.CurrHP,
            Inventory = ShrunkenInventory.Inflate(sp.Inventory)
        };
    }
}

record InvItemKVP(char Slot, Item Item);

class ShrunkenInventory
{
    public char NextSlot { get; set; }
    public int Zorkmids { get; set; }
    [JsonInclude]
    public List<InvItemKVP> Items { get; set; }

    public static ShrunkenInventory Shrink(Inventory inv)
    {
        var items = inv.ToKVP();

        return new ShrunkenInventory()
        {
            Zorkmids = inv.Zorkmids,
            NextSlot = inv.NextSlot,
            Items = inv.ToKVP().Select(kvp => new InvItemKVP(kvp.Item1, kvp.Item2)).ToList()
        };
    }

    public static Inventory Inflate(ShrunkenInventory sp)
    {
        var inv = new Inventory();
        
        foreach (var kvp in sp.Items)
        {
            inv.Add(kvp.Item);
        }
        
        inv.Zorkmids = sp.Zorkmids;
        inv.NextSlot = sp.NextSlot;

        return inv;
    }
}

class ShrunkenCampaign
{
    [JsonInclude]
    public Dictionary<int, ShrunkenDungeon> Dungeons = [];

    public static ShrunkenCampaign Shrink(Campaign c)
    {
        ShrunkenCampaign sc = new();
        
        foreach (var k in c.Dungeons.Keys)
        {
            sc.Dungeons.Add(k, ShrunkenDungeon.Shrink(c.Dungeons[k]));
        }

        return sc;
    }

    public static Campaign Inflate(ShrunkenCampaign sc)
    {
        Campaign campaign = new();

        foreach (var k in  sc.Dungeons.Keys)
        {
            campaign.Dungeons.Add(k, ShrunkenDungeon.Inflate(sc.Dungeons[k]));
        }

        return campaign;
    }
}

internal class ShrunkenDungeon
{
    public int ID { get; set; }
    public string? ArrivalMessage { get; set;  }

    [JsonInclude]
    public List<RememberedSq> RememberedSqs;
    [JsonInclude]
    public Dictionary<int, ShrunkenMap> LevelMaps;

    public ShrunkenDungeon()
    {
        RememberedSqs = [];
        LevelMaps = [];
    }

    public static ShrunkenDungeon Shrink(Dungeon dungeon)
    {
        var sd = new ShrunkenDungeon()
        {
            ID = dungeon.ID,
            ArrivalMessage = dungeon.ArrivalMessage,
            RememberedSqs = []
        };

        foreach (var k in dungeon.LevelMaps.Keys)
        {
            sd.LevelMaps.Add(k, ShrunkenMap.Shrink(dungeon.LevelMaps[k]));
        }

        foreach (var sq in dungeon.RememberedSqs)
        {
            sd.RememberedSqs.Add(RememberedSq.FromTuple(sq));
        }

        return sd;
    }

    public static Dungeon Inflate(ShrunkenDungeon sd)
    {
        Dungeon d = new Dungeon(sd.ID, sd.ArrivalMessage);
        d.RememberedSqs = [];

        foreach (var sq in sd.RememberedSqs)
        {
            d.RememberedSqs.Add(sq.ToTuple());
        }

        foreach (var k in sd.LevelMaps.Keys)
        {
            d.LevelMaps.Add(k, ShrunkenMap.Inflate(sd.LevelMaps[k]));
        }

        return d;
    }
}

internal class ShrunkenMap()
{    
    public int Height { get; set; }
    public int Width { get; set; }
    [JsonInclude]
    int[]? Tiles{ get; set; }
    [JsonInclude]
    List<string>? SpecialTiles { get; set; }

    public static ShrunkenMap Shrink(Map map)
    {
        ShrunkenMap sm = new ShrunkenMap
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
            Tile tile = null;
            int[] digits;
            var pieces = s.Split(';');
            var j = int.Parse(pieces[0]);
            var type = (TileType)int.Parse(pieces[1]);
            
            switch (type)
            {
                case TileType.Portal:
                    tile = new Portal(pieces[3]);
                    digits = Regex.Split(pieces[2], @"\D+")
                                    .Skip(1)
                                    .Take(4)
                                    .Select(int.Parse).ToArray();
                    ((Portal)tile).Destination = (digits[0], digits[1], digits[2], digits[3]);
                    break;
                case TileType.Upstairs:
                    tile = new Upstairs(pieces[3]);
                    digits = Regex.Split(pieces[2], @"\D+")
                                    .Skip(1)
                                    .Take(4)
                                    .Select(int.Parse).ToArray();
                    ((Upstairs)tile).Destination = (digits[0], digits[1], digits[2], digits[3]);
                    break;
                case TileType.Downstairs:
                    tile = new Downstairs(pieces[3]);
                    digits = Regex.Split(pieces[2], @"\D+")
                                    .Skip(1)
                                    .Take(4)
                                    .Select(int.Parse).ToArray();
                    ((Downstairs)tile).Destination = (digits[0], digits[1], digits[2], digits[3]);
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

    public static Map Inflate(ShrunkenMap sm)
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
class ShrunkenItemDB
{
    public List<Loc> Keys { get; set; }
    public List<List<Item>> Items { get; set; }

    public static ShrunkenItemDB Shrink(ItemDB itemDB)
    {
        var sidb = new ShrunkenItemDB
        {
            Keys = new(),
            Items = new()
        };

        foreach (var p in itemDB.Dump())
        {
            sidb.Keys.Add(p.Item1);
            sidb.Items.Add(p.Item2);
        }
        
        return sidb;
    }

    public static ItemDB Inflate(ShrunkenItemDB sidb)
    {
        var itemDB = new ItemDB();

        for (int j = 0; j < sidb.Keys.Count; j++)
        {
            itemDB.AddStack(sidb.Keys[j], sidb.Items[j]);
        }

        return itemDB;
    }
}

internal record SaveGameInfo(ShrunkenPlayer? Player, ShrunkenCampaign? Campaign, int CurrentLevel, int CurrentDungeon,
                                ShrunkenItemDB ItemDB);

// SIGH so for tuples, the JsonSerliazer won't serialize a tuple of ints. So, let's make a little object that
// *can* be serialized
internal record struct RememberedSq(int A, int B, int C)
{
    public static RememberedSq FromTuple((int, int, int) t) => new RememberedSq(t.Item1, t.Item2, t.Item3);
    public (int, int, int) ToTuple() => (A, B, C);
}