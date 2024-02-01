using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Yarl2
{
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
}
