using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Yarl2
{
    internal class GameEngine
    {
        public readonly ushort VisibleWidth;
        public readonly ushort VisibleHeight;

        public GameEngine(ushort visWidth, ushort visHeight)
        {
            VisibleWidth = visWidth;
            VisibleHeight = visHeight;
        }

        public Dictionary<(short, short), Tile> CalcVisible(Player player, Map map)
        {
            var visible = new Dictionary<(short, short), Tile>();

            for (int row = player.Row - 5; row < player.Row + 5; row++)
            {
                for (int col = player.Col - 5; col < player.Col + 5; col++)
                {
                    if (row < 0 || col < 0)
                    {
                        visible.Add(((short)row, (short)col), Tile.Unknown);
                    }
                    else if (!map.InBounds((ushort)row, (ushort)col)) 
                    {
                        visible.Add(((short)row, (short)col), Tile.Unknown);
                    }
                    else
                    {
                        var r = (ushort)row;
                        var c = (ushort)col;
                        visible.Add(((short)r, (short)c), map.TileAt(r, c));
                    }
                }
            }

            return visible;
        }
    }
}
