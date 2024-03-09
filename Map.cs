
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

[Flags]
enum TerrainFlags
{
    None = 0,
    Lit = 1
}

enum TileType
{
    WorldBorder,
    Unknown,
    PermWall,
    DungeonWall,
    DungeonFloor,
    StoneFloor,
    StoneWall,
    ClosedDoor,
    OpenDoor,
    LockedDoor,
    BrokenDoor,
    HWindow,
    VWindow,
    DeepWater,
    Water,
    Sand,
    Grass,
    Tree,
    Mountain,
    SnowPeak,
    Portal,
    Upstairs,
    Downstairs,
    Cloud,
    WoodWall,   
    WoodFloor,
    Forge,
    Dirt,
    Well,
    Bridge,
    WoodBridge,
    Statue,
    Landmark,
    Chasm
}

abstract class Tile(TileType type)
{
    public virtual TileType Type { get; protected set; } = type;
    public virtual string StepMessage => "";
    public abstract bool Passable();
    public abstract bool Opaque();

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
        _ => false
    };
}

class BasicTile : Tile
{
    private readonly bool _passable;
    private readonly bool _opaque;
    private readonly string _stepMessage;

    public override bool Passable() => _passable;
    public override bool Opaque() => _opaque;
    public override string StepMessage => _stepMessage;

    public BasicTile(TileType type, bool passable, bool opaque) : base(type)
    {
        _passable = passable;
        _opaque = opaque;
        _stepMessage = "";
    }

    public BasicTile(TileType type, bool passable, bool opaque, string stepMessage) : base(type)
    {
        _passable = passable;
        _opaque = opaque;
        _stepMessage = stepMessage;
    }
}

class Door(TileType type, bool open) : Tile(type)
{
    public bool Open { get; set;} = open;

    // Not sure if this is a good way to handle this for places like 
    // the pathfinding code or if it's a gross hack
    public override TileType Type 
    {
        get => Open ? TileType.OpenDoor : TileType.ClosedDoor;       
    }
    public override bool Passable() => Open;
    public override bool Opaque() => !Open;

    public override string ToString()
    {
        return $"{(int)Type};{Open}";
    }
}

class Portal(string stepMessage) : Tile(TileType.Portal) 
{
    private readonly string _stepMessage = stepMessage;
    public Loc Destination { get; set; }
    public override bool Passable() => true;
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
    public override bool Opaque() => false;

    public override string StepMessage => _stepMessage;

    public override string ToString()
    {
        return $"{(int)Type};{_stepMessage}";
    }
}

class TileFactory
{
    private static readonly Tile WorldBorder = new BasicTile(TileType.WorldBorder, false, true);
    private static readonly Tile Unknown = new BasicTile(TileType.Unknown, false, true);
    private static readonly Tile DungeonWall = new BasicTile(TileType.DungeonWall, false, true);
    private static readonly Tile StoneWall = new BasicTile(TileType.StoneWall, false, true);
    private static readonly Tile PermWall = new BasicTile(TileType.PermWall, false, true);
    private static readonly Tile Floor = new BasicTile(TileType.DungeonFloor, true, false);
    private static readonly Tile StoneFloor = new BasicTile(TileType.StoneFloor, true, false);
    private static readonly Tile DeepWater = new BasicTile(TileType.DeepWater, false, false);
    private static readonly Tile Grass = new BasicTile(TileType.Grass, true, false);
    private static readonly Tile Sand = new BasicTile(TileType.Sand, true, false);
    private static readonly Tile Tree = new BasicTile(TileType.Tree, true, false);
    private static readonly Tile Mountain = new BasicTile(TileType.Mountain, false, true);
    private static readonly Tile SnowPeak = new BasicTile(TileType.Mountain, false, true);
    private static readonly Tile Cloud = new BasicTile(TileType.Cloud, true, false);
    private static readonly Tile Water = new BasicTile(TileType.Water, true, false, "You splash into the water.");
    private static readonly Tile HWindow = new BasicTile(TileType.HWindow, false, false);
    private static readonly Tile VWindow = new BasicTile(TileType.VWindow, false, false);
    private static readonly Tile WoodWall = new BasicTile(TileType.WoodWall, false, true);
    private static readonly Tile WoodFloor = new BasicTile(TileType.WoodFloor, true, false);
    private static readonly Tile Forge = new BasicTile(TileType.Forge, true, false);
    private static readonly Tile Dirt = new BasicTile(TileType.Dirt, true, false);
    private static readonly Tile Well = new BasicTile(TileType.Well, true, false);
    private static readonly Tile Bridge = new BasicTile(TileType.Bridge, true, false);
    private static readonly Tile WoodBridge = new BasicTile(TileType.WoodBridge, true, false);
    private static readonly Tile Statue = new BasicTile(TileType.Statue, false, true);
    private static readonly Tile Chasm = new BasicTile(TileType.Chasm, false, false);

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
        TileType.Tree => Tree,
        TileType.Mountain => Mountain,
        TileType.SnowPeak => SnowPeak,
        TileType.ClosedDoor => new Door(type, false),
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
        TileType.Chasm => Chasm,
        _ => Unknown
    };
}

class Map : ICloneable
{
    public readonly int Width;
    public readonly int Height;

    public Tile[] Tiles;
    public Dictionary<(int, int), Dictionary<ulong, TerrainFlags>> Effects = [];
    
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

    // I could speed this up maybe by calculating the intersections
    // whenever the effects are updated. But otoh maybe with things
    // moving around the map it'll just be constantly updating anyhow
    public bool HasEffect(TerrainFlags effect, int row, int col)
    {
        if (!InBounds(row, col) || !Effects.ContainsKey((row, col)))
            return false;

        foreach (var flags in Effects[(row, col)].Values)
        {
            if ((effect & flags) != TerrainFlags.None)
                return true;
        }

        return false;
    }

    public void ApplyEffect(TerrainFlags effect, int row, int col, ulong objID)
    {
        if (!Effects.TryGetValue((row, col), out var flagsDict))
        {
            flagsDict = new() { { objID, effect } };
            Effects.Add((row, col), flagsDict);
        }

        if (!flagsDict.TryAdd(objID, effect))
        {
            flagsDict[objID] |= effect;
        }
    }

    public void RemoveAtLoc(TerrainFlags effect, int row, int col, ulong objID)
    {
        if (Effects.TryGetValue((row, col), out var flagsDict))
        {
            if (flagsDict.ContainsKey(objID))
            {
                flagsDict[objID] &= ~effect;
            }
        }
    }

    // I dunno if this is going to be too slow...
    public void RemoveEffectFromMap(TerrainFlags effect, ulong objID)
    {
        foreach (var flagsDict in Effects.Values)
        {
            if (flagsDict.ContainsKey(objID))
                flagsDict[objID] &= ~effect;
        }
    }

    public bool IsTile((int, int) pt, TileType type) => InBounds(pt) && TileAt(pt).Type == type;

    public bool InBounds(int row,  int col) => row >= 0 && row < Height && col >= 0 && col < Width;
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

        for (int j = 0; j < 1000;  j++) 
        {
            int row = rng.Next(1, Height);
            int col = rng.Next(1, Width);
            Tiles[row * Width + col] = TileFactory.Get(TileType.DungeonWall);
        }
    }

    public void SetTile(int row, int col, Tile tile) => Tiles[row * Width + col] = tile;
    public void SetTile((int, int) loc, Tile tile) => Tiles[loc.Item1 * Width + loc.Item2] = tile;

    public Tile TileAt(int row,  int col) => Tiles[row * Width + col];
    public Tile TileAt((int, int) loc) => Tiles[loc.Item1 * Width + loc.Item2];

    public void Dump() 
    {
        for (int row = 0; row < Height; row++)
        {
            for (int col = 0; col < Width; col++)
            {
                char ch = Tiles[row * Width + col].Type switch  {
                    TileType.PermWall => '#',
                    TileType.DungeonWall => '#',
                    TileType.DungeonFloor or TileType.Sand => '.',
                    TileType.ClosedDoor => '+',
                    TileType.Mountain or TileType.SnowPeak => '^',
                    TileType.Grass => ',',
                    TileType.Tree => 'T',
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
        Map temp = new Map(Width, Height);
        if (Tiles is not null)            
            temp.Tiles = (Tile[])Tiles.Clone();

        return temp;
    }
}

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

    public static bool[,] GetCave(int height,  int width, Random rng)
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