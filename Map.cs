namespace Yarl2;

enum Tile
{
    Unknown,
    PermWall,
    Wall,
    Floor
}

internal static class TileExtensions
{
    public static bool Passable(this Tile tile)
    {
        return tile switch
        {
            Tile.Floor => true,
            _ => false
        };
    }

    public static bool Opaque(this Tile tile) 
    {
        return tile switch
        {
            Tile.Floor => false,
            _ => true
        };
    }
}

internal class Map
{
    public readonly ushort Width;
    public readonly ushort Height;

    private Tile[] Tiles;

    public Map(ushort width, ushort height)
    {
        Width = width;
        Height = height;

        Tiles = new Tile[Height * Width];
    }

    public bool InBounds(ushort row,  ushort col) 
    {
        return row >= 0 && row < Height && col >= 0 && col < Width;
    }

    public static Map TestMap()
    {
        var map = new Map(20, 20);

        for (var col = 0; col < 20; col++)
        {
            map.Tiles[col] = Tile.PermWall;
            map.Tiles[19 * 20 + col] = Tile.PermWall;
        }

        for (var row = 1; row < 19; row++)
        {
            map.Tiles[row * 20] = Tile.PermWall;
            map.Tiles[row * 20 + 19] = Tile.PermWall;

            for (var col = 1; col < 19; col++)
            {
                map.Tiles[row * 20 + col] = Tile.Floor;
            }
        }

        map.Tiles[3 * 20 + 8] = Tile.Wall;
        map.Tiles[3 * 20 + 9] = Tile.Wall;
        map.Tiles[3 * 20 + 10] = Tile.Wall;
        map.Tiles[3 * 20 + 11] = Tile.Wall;
        map.Tiles[3 * 20 + 12] = Tile.Wall;
        map.Tiles[3 * 20 + 13] = Tile.Wall;
        map.Tiles[3 * 20 + 14] = Tile.Wall;

        map.Tiles[5 * 20 + 14] = Tile.Wall;
        map.Tiles[6 * 20 + 14] = Tile.Wall;
        map.Tiles[7 * 20 + 14] = Tile.Wall;
        map.Tiles[8 * 20 + 14] = Tile.Wall;
        map.Tiles[9 * 20 + 14] = Tile.Wall;

        return map;
    }

    public void SetRandomTestMap()
    {
        for (int col = 0; col < Width; col++) 
        {
            Tiles[col] = Tile.PermWall;
            Tiles[(Height - 1) * Width + col] = Tile.PermWall;
        }

        for (int row = 1; row < Height - 1; row++)
        {
            Tiles[row * Width] = Tile.PermWall;
            Tiles[row * Width + Width - 1] = Tile.PermWall;
            for (int col = 1; col < Width - 1; col++) 
            {
                Tiles[row * Width + col] = Tile.Floor;
            }
        }

        Random rnd = new Random();
        for (int j = 0; j < 1000;  j++) 
        {
            ushort row = (ushort) rnd.Next(1, Height);
            ushort col = (ushort) rnd.Next(1, Width);
            Tiles[row * Width + col] = Tile.Wall;
        }
    }

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
                char ch = Tiles[row * Width + col] switch  {
                    Tile.PermWall => '#',
                    Tile.Wall => '#',
                    Tile.Floor => '.',
                    _ => ' '
                };
                Console.Write(ch);
            }
            Console.WriteLine();
        }
    }
}
