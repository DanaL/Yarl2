namespace Yarl2;

enum TileType
{
    Unknown,
    PermWall,
    Wall,
    Floor,
    Door
}

internal abstract class Tile 
{
    public TileType Type { get; }
    public abstract bool Passable();
    public abstract bool Opaque();

    protected Tile(TileType type) => Type = type;
}

internal class BasicTile(TileType type, bool passable, bool opaque) : Tile(type)
{
    private readonly bool _passable = passable;
    private readonly bool _opaque = opaque;

    public override bool Passable() => _passable;
    public override bool Opaque() => _opaque;
}
internal class Door(TileType type, bool open) : Tile(type)
{
    public bool Open { get; set;} = open;

    public override bool Passable() => Open;
    public override bool Opaque() => !Open;
}

internal class TileFactory
{
    private static readonly Tile Unknown = new BasicTile(TileType.Unknown, false, true);
    private static readonly Tile Wall = new BasicTile(TileType.Wall, false, true);
    private static readonly Tile PermWall = new BasicTile(TileType.PermWall, false, true);
    private static readonly Tile Floor = new BasicTile(TileType.Floor, true, false);

    public static Tile Get(TileType type)
    {
        return type switch 
        {
            TileType.PermWall => PermWall,
            TileType.Wall => Wall,
            TileType.Floor => Floor,
            TileType.Door => new Door(type, false),
            _ => Unknown
        };
    }
}

internal class Map
{
    public readonly ushort Width;
    public readonly ushort Height;

    public Tile[] Tiles;

    public Map(ushort width, ushort height)
    {
        Width = width;
        Height = height;

        Tiles = new Tile[Height * Width];
    }

    public bool InBounds(short row,  short col) => row >= 0 && row < Height && col >= 0 && col < Width;
    
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
                map.Tiles[row * 20 + col] = TileFactory.Get(TileType.Floor);
            }
        }

        map.Tiles[3 * 20 + 8] = TileFactory.Get(TileType.Wall);
        map.Tiles[3 * 20 + 9] = TileFactory.Get(TileType.Wall);
        map.Tiles[3 * 20 + 10] = TileFactory.Get(TileType.Wall);
        map.Tiles[3 * 20 + 11] = TileFactory.Get(TileType.Wall);
        map.Tiles[3 * 20 + 12] = TileFactory.Get(TileType.Wall);
        map.Tiles[3 * 20 + 13] = TileFactory.Get(TileType.Wall);
        map.Tiles[3 * 20 + 14] = TileFactory.Get(TileType.Wall);

        map.Tiles[5 * 20 + 14] = TileFactory.Get(TileType.Wall);
        map.Tiles[6 * 20 + 14] = TileFactory.Get(TileType.Wall);
        map.Tiles[7 * 20 + 14] = TileFactory.Get(TileType.Door);
        map.Tiles[8 * 20 + 14] = TileFactory.Get(TileType.Wall);
        map.Tiles[9 * 20 + 14] = TileFactory.Get(TileType.Wall);

        return map;
    }

    public void SetRandomTestMap()
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
                Tiles[row * Width + col] = TileFactory.Get(TileType.Floor);
            }
        }

        Random rnd = new Random();
        for (int j = 0; j < 1000;  j++) 
        {
            ushort row = (ushort) rnd.Next(1, Height);
            ushort col = (ushort) rnd.Next(1, Width);
            Tiles[row * Width + col] = TileFactory.Get(TileType.Wall);
        }
    }

    public void SetTile(ushort row, ushort col, Tile tile) => Tiles[row * Width + col] = tile;

    public Tile TileAt(ushort row,  ushort col) 
    { 
        var j = row * Width + col;

        return Tiles[j];
    }

    public void Dump() 
    {
        for (int row = 0; row < Height; row++)
        {
            for (int col = 0; col < Width; col++)
            {
                char ch = Tiles[row * Width + col].Type switch  {
                    TileType.PermWall => '#',
                    TileType.Wall => '#',
                    TileType.Floor => '.',
                    TileType.Door => '+',
                    _ => ' '
                };
                Console.Write(ch);
            }
            Console.WriteLine();
        }
    }
}
