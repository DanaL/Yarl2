using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Yarl2
{
    abstract class Actor
    {
        public ushort Row { get; set; }
        public ushort Col { get; set; }
    }

    internal class GameQuitException : Exception { }

    internal class GameEngine
    {
        public readonly ushort VisibleWidth;
        public readonly ushort VisibleHeight;
        private readonly Display ui;

        public GameEngine(ushort visWidth, ushort visHeight, Display display)
        {
            VisibleWidth = visWidth;
            VisibleHeight = visHeight;
            ui = display;
        }

        Dictionary<(short, short), Tile> CalcVisible(Player player, Map map)
        {
            var visible = new Dictionary<(short, short), Tile>();

            for (int row = player.Row - 15; row < player.Row + 15; row++)
            {
                for (int col = player.Col - 20; col < player.Col + 20; col++)
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

        public void Play(Player player, Map map)
        {
            bool playing = true;
            bool update = true;

            do 
            {
                if (update)
                {
                    var visible = CalcVisible(player, map);
                    ui.UpdateDisplay(player, visible);
                }

                update = true;
                var cmd = ui.GetCommand(player, map);

                if (cmd is NullCommand)
                {
                    update = false;
                    Thread.Sleep(25);
                }
                else if (cmd is QuitCommand)
                {
                    playing = false;
                }
                else
                {
                    var result = cmd.Execute();

                    if (result.Message is not null)
                        ui.WriteMessage(result.Message);
                }                
            }
            while (playing);
        }
    }
}
