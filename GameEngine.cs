﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Yarl2
{
    enum Command
    {
        MoveNorth, MoveSouth, MoveWest, MoveEast,
        MoveNorthEast, MoveSouthEast, MoveNorthWest, MoveSouthWest,
        Pass, Quit
    }

    internal class GameQuitException : Exception { }

    internal class GameEngine
    {
        public readonly ushort VisibleWidth;
        public readonly ushort VisibleHeight;
        private readonly IDisplay ui;

        public GameEngine(ushort visWidth, ushort visHeight, IDisplay display)
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
                for (int col = player.Col - 15; col < player.Col + 15; col++)
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

        void TryToMove(Player player, Map map, Command move) 
        {
            ushort nextRow = 0, nextCol = 0;

            switch (move)
            {
                case Command.MoveNorth:
                    nextRow = (ushort) (player.Row - 1);
                    nextCol = player.Col;
                    break;
                case Command.MoveSouth:
                    nextRow = (ushort)(player.Row + 1);
                    nextCol = player.Col;
                    break;
                case Command.MoveEast:
                    nextRow = player.Row;
                    nextCol = (ushort) (player.Col + 1);
                    break;
                case Command.MoveWest:
                    nextRow = player.Row;
                    nextCol = (ushort)(player.Col - 1);
                    break;
                case Command.MoveNorthWest:
                    nextRow = (ushort)(player.Row - 1);
                    nextCol = (ushort)(player.Col - 1);
                    break;
                case Command.MoveNorthEast:
                    nextRow = (ushort)(player.Row - 1);
                    nextCol = (ushort)(player.Col + 1);
                    break;
                case Command.MoveSouthWest:
                    nextRow = (ushort)(player.Row + 1);
                    nextCol = (ushort)(player.Col - 1);
                    break;
                case Command.MoveSouthEast:
                    nextRow = (ushort)(player.Row + 1);
                    nextCol = (ushort)(player.Col + 1);
                    break;
            }

            if (!map.InBounds(nextRow, nextCol) || !map.TileAt(nextRow, nextCol).Passable())
            {
                ui.WriteMessage("You cannot go that way!");                
            }
            else
            {
                player.Row = nextRow;
                player.Col = nextCol;
                ui.WriteMessage("");
            }
        }

        public void Play(Player player, Map map)
        {
            bool playing = true;

            do 
            {
                var visible = CalcVisible(player, map);
                ui.UpdateDisplay(player, visible);

                var cmd = ui.GetCommand();
                switch (cmd) 
                {
                    case Command.MoveNorth:
                    case Command.MoveSouth:
                    case Command.MoveWest:
                    case Command.MoveEast:
                    case Command.MoveNorthEast:
                    case Command.MoveNorthWest:
                    case Command.MoveSouthEast:
                    case Command.MoveSouthWest:
                        TryToMove(player, map, cmd);
                        break;
                    case Command.Quit:
                        playing = false;
                        break;
                }
            }
            while (playing);
        }
    }
}
