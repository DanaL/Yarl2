using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using BearLibNET;
using BearLibNET.DefaultImplementations;
using TKCodes = BearLibNET.TKCodes;

namespace Yarl2
{    
    internal interface IDisplay
    {
        Command GetCommand();
        string QueryUser(string prompt);        
        void TitleScreen();
        void UpdateDisplay(Player player, Dictionary<(short, short), Tile> visible);
        char WaitForInput();
        void WriteLongMessage(List<string> message);
        void WriteMessage(string message);
    }

    internal class BLDisplay : IDisplay, IDisposable
    {
        private const int BACKSPACE = 8;
        private const int ScreenWidth = 60;
        private const int ScreenHeight = 30;
        private const int SideBarWidth = 20;
        private const int ViewWidth = ScreenWidth - SideBarWidth;
        private const int FontSize = 12;
        private Dictionary<int, char>? KeyToChar;

        private readonly short PlayerScreenRow;
        private readonly short PlayerScreenCol;

        private Color BLACK = new() { A = 255, R = 0, G = 0, B = 0 };
        private Color WHITE = new() { A = 255, R = 255, G = 255, B = 255 };
        private Color GREY = new() { A = 255, R = 136, G = 136, B = 136 };
        private Color LIGHT_GREY = new() { A = 255, R = 220, G = 220, B = 220 };
        private Color DARK_GREY = new() { A = 255, R = 72, G = 73, B = 75 };
        private Color YELLOW = new() { A = 255, R = 255, G = 255, B = 53 };

        public BLDisplay(string windowTitle)
        {
            SetUpKeyToCharMap();
            Terminal.Open();
            Terminal.Set($"window: size={ScreenWidth}x{ScreenHeight}, title={windowTitle}; font: DejaVuSansMono.ttf, size={FontSize}");
            
            PlayerScreenRow = (ScreenHeight - 1) / 2 + 1;
            PlayerScreenCol = (ScreenWidth - SideBarWidth - 1) / 2;
        }

        private void SetUpKeyToCharMap()
        {
            KeyToChar = [];
            int curr = (int)TKCodes.InputEvents.TK_A;
            for (int ch = 'a'; ch <= 'z'; ch++)
            {
                KeyToChar.Add(curr++, (char)ch);
            }
            curr = (int)TKCodes.InputEvents.TK_1;
            for (int ch = '1'; ch <= '9'; ch++)
            {
                KeyToChar.Add(curr++, (char)ch);
            }
            KeyToChar.Add(curr, '0');
            KeyToChar.Add((int)TKCodes.InputEvents.TK_RETURN_or_ENTER, '\n');
            KeyToChar.Add((int)TKCodes.InputEvents.TK_SPACE, ' ');
            KeyToChar.Add((int)TKCodes.InputEvents.TK_BACKSPACE, (char)BACKSPACE);
        }

        private (Color, char) TileToGlyph(Tile tile)
        {
            return tile switch
            {
                Tile.PermWall => (DARK_GREY, '#'),
                Tile.Wall =>  (GREY, '#'),
                Tile.Floor => (LIGHT_GREY, '.'),
                _ => (BLACK, ' ')
            };
        }

        public Command GetCommand()
        {
            if (Terminal.HasInput())
            {
                var ch = WaitForInput();

                if (ch == 'h')
                    return Command.MoveWest;
                else if (ch == 'j')
                    return Command.MoveSouth;
                else if (ch == 'k')
                    return Command.MoveNorth;
                else if (ch == 'l')
                    return Command.MoveEast;
                else if (ch == 'y')
                    return Command.MoveNorthWest;
                else if (ch == 'u')
                    return Command.MoveNorthEast;
                else if (ch == 'b')
                    return Command.MoveSouthWest;
                else if (ch == 'n')
                    return Command.MoveSouthEast;
                else if (ch == 'Q')
                    return Command.Quit;
                else
                    return Command.Pass;
            }
            else
                return Command.None;            
        }

        void WriteSideBar(Player player)
        {
            Terminal.Print(ViewWidth, 1, $"| {player.Name}".PadRight(ViewWidth));
            Terminal.Print(ViewWidth, 2, $"| HP: {player.CurrHP} ({player.MaxHP})".PadRight(ViewWidth));

            string blank = "|".PadRight(ViewWidth);
            for (int row = 3; row < ScreenHeight; row++)
            {
                Terminal.Print(ViewWidth, row, blank);
            }
        }

        public void UpdateDisplay(Player player, Dictionary<(short, short), Tile> visible)
        {
            short rowOffset = (short) (player.Row - PlayerScreenRow);
            short colOffset = (short) (player.Col - PlayerScreenCol);
            for (short row = 0; row < ScreenHeight - 1; row++)
            {
                for (short col = 0; col < ScreenWidth - 21; col++)
                {
                    short vr = (short)(row + rowOffset);
                    short vc = (short)(col + colOffset);
                    if (visible.ContainsKey((vr, vc)))
                    {
                        var (color, ch) = TileToGlyph(visible[(vr, vc)]);
                        Terminal.Color(color);
                        Terminal.Put(col, row + 1, ch);
                    }
                    else
                    {
                        Terminal.Put(col, row + 1, ' ');
                    }
                }
            }

            Terminal.Color(WHITE);
            Terminal.Put(PlayerScreenCol, PlayerScreenRow + 1, '@');

            WriteSideBar(player);

            Terminal.Refresh();
        }

        public void WriteLongMessage(List<string> message)
        {
            Terminal.Clear();

            for (int row = 0; row < message.Count; row++)
            {
                Terminal.Print(0, row, message[row]);
            }

            Terminal.Refresh();
            WaitForInput();
        }

        public void WriteMessage(string message)
        {
            Terminal.Print(0, 0, message.PadRight(ScreenWidth));
            Terminal.Refresh();
        }

        public string QueryUser(string prompt)
        {            
            string answer = "";
            do
            {
                string message = $"{prompt} {answer}";
                WriteMessage(message);

                var ch = WaitForInput();
                if (ch == '\n')
                {
                    break;
                }
                else if (ch == BACKSPACE && answer.Length > 0)
                {
                    answer = answer[..^1];
                }
                else if (ch != '\0')
                {
                    answer += ch;
                }
            }
            while (true);

            return answer;
        }

        public char WaitForInput()
        {
            do 
            {
                int key = Terminal.Read();
                
                if (key == (int)TKCodes.InputEvents.TK_CLOSE)
                    throw new GameQuitException();

                if (KeyToChar.TryGetValue(key, out char value))
                {
                    return Terminal.Check((int)TKCodes.InputEvents.TK_SHIFT) ? char.ToUpper(value) : value;
                }
            }
            while (true);
        }

        public void TitleScreen()
        {
            var msg = new List<string>()
            {
                "",
                "",
                "",
                "",
                "     Welcome to Yarl2",
                "       (yet another attempt to make a roguelike,",
                "           this time in C#...)"
            };
            WriteLongMessage(msg);

            Terminal.Clear();
            Terminal.Refresh();
        }

        public void Dispose()
        {            
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                Terminal.Close();
            }            
        }
    }
}
