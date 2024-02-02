using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using SDL2;
using static SDL2.SDL;

using BearLibNET;
using BearLibNET.DefaultImplementations;
using TKCodes = BearLibNET.TKCodes;

namespace Yarl2
{    
    internal abstract class Display
    {
        protected const int ScreenWidth = 60;
        protected const int ScreenHeight = 30;
        protected const int SideBarWidth = 20;
        protected const int ViewWidth = ScreenWidth - SideBarWidth;
        protected const int FontSize = 12;
        protected short PlayerScreenRow;
        protected short PlayerScreenCol;

        protected readonly Color BLACK = new() { A = 255, R = 0, G = 0, B = 0 };
        protected readonly Color WHITE = new() { A = 255, R = 255, G = 255, B = 255 };
        protected readonly Color GREY = new() { A = 255, R = 136, G = 136, B = 136 };
        protected readonly Color LIGHT_GREY = new() { A = 255, R = 220, G = 220, B = 220 };
        protected readonly Color DARK_GREY = new() { A = 255, R = 72, G = 73, B = 75 };
        protected readonly Color YELLOW = new() { A = 255, R = 255, G = 255, B = 53 };

        public abstract Command GetCommand();
        public abstract string QueryUser(string prompt);        
        
        public abstract void UpdateDisplay(Player player, Dictionary<(short, short), Tile> visible);
        public abstract char WaitForInput();
        public abstract void WriteLongMessage(List<string> message);
        public abstract void WriteMessage(string message);

        public virtual void TitleScreen()
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
            WaitForInput();
        }
    }

    internal class SDLDisplay : Display
    {
        private IntPtr _window;
        private readonly IntPtr _renderer, _font;
        private readonly int _fontWidth;
        private readonly int _fontHeight;

        private Dictionary<Color, SDL_Color> _colours;

        public SDLDisplay(string windowTitle)
        {
            SDL_Init(SDL_INIT_VIDEO);
            SDL_ttf.TTF_Init();
            _font = SDL_ttf.TTF_OpenFont("DejaVuSansMono.ttf", 24);
            SDL_ttf.TTF_SizeUTF8(_font, " ", out _fontWidth, out _fontHeight);
            
            int width = ScreenWidth * _fontWidth;
            int height = ScreenHeight * _fontHeight;
            _window = SDL_CreateWindow(windowTitle, 100, 100, width, height, SDL_WindowFlags.SDL_WINDOW_SHOWN | SDL_WindowFlags.SDL_WINDOW_INPUT_FOCUS);
            _renderer = SDL_CreateRenderer(_window, -1, SDL_RendererFlags.SDL_RENDERER_PRESENTVSYNC);

            _colours = [];
        }

        public override Command GetCommand()
        {
            throw new NotImplementedException();
        }

        public override string QueryUser(string prompt)
        {
            throw new NotImplementedException();
        }

        public override void UpdateDisplay(Player player, Dictionary<(short, short), Tile> visible)
        {
            throw new NotImplementedException();
        }

        private static char KeysymToChar(SDL_Keysym keysym) 
        {
        return keysym.mod == SDL_Keymod.KMOD_LSHIFT || keysym.mod == SDL_Keymod.KMOD_RSHIFT
                ? char.ToUpper((char)keysym.sym)
                : (char)keysym.sym;
        }

        public override char WaitForInput()
        {
            while (SDL_PollEvent(out var e) != -1) 
            {
                switch (e.type) 
                {                
                    case SDL_EventType.SDL_KEYDOWN:
                        if (e.key.keysym.sym == SDL_Keycode.SDLK_LSHIFT || e.key.keysym.sym == SDL_Keycode.SDLK_RSHIFT)
                            continue;
                        return KeysymToChar(e.key.keysym);
                        //char ch = (char) e.key.keysym.sym;
                        //Console.WriteLine($"foo {ch}");
                        //Console.WriteLine($"    {e.key.keysym.mod}");
                        //break;
                }
                SDL_Delay(16);
            }

            return '\0';
        }

        private SDL_Color ToSDLColour(Color colour) 
        {
            if (!_colours.TryGetValue(colour, out SDL_Color value)) 
            {
                value = new SDL_Color() { 
                        a = (byte) colour.A, 
                        r = (byte) colour.R,
                        g = (byte) colour.G,
                        b = (byte) colour.B
                };
                _colours.Add(colour, value);
            }

            return value;
        }

        private void WriteLine(string message, int lineNum, bool update)
        {
            message = message.PadRight(ScreenWidth);
            var fontPtr = _font;
            var fh = _fontHeight;
            var surface =  SDL_ttf.TTF_RenderText_Shaded(fontPtr, message, ToSDLColour(WHITE), ToSDLColour(BLACK));        
            var s = (SDL_Surface)Marshal.PtrToStructure(surface, typeof(SDL_Surface))!;
            
            var texture = SDL_CreateTextureFromSurface(_renderer, surface);
            var loc = new SDL_Rect
            {
                x = 2,
                y = lineNum * fh,
                h = fh,
                w = s.w
            };
            
            SDL_FreeSurface(surface);
            SDL_SetRenderDrawColor(_renderer, 0, 0, 0, 255);
            SDL_RenderCopy(_renderer, texture, IntPtr.Zero, ref loc);
            
            if (update)
                SDL_RenderPresent(_renderer);
        }

        public override void WriteLongMessage(List<string> message)
        {
            SDL_RenderClear(_renderer);
            for (int j = 0; j < message.Count; j++)
            {
                WriteLine(message[j], j, false);
            }
            SDL_RenderPresent(_renderer);
        }

        public override void WriteMessage(string message)
        {
            throw new NotImplementedException();
        }
    }

    internal class BLDisplay : Display, IDisposable
    {
        private const int BACKSPACE = 8;

        private Dictionary<int, char>? KeyToChar;

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

        public override Command GetCommand()
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

        public override void UpdateDisplay(Player player, Dictionary<(short, short), Tile> visible)
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

        public override void WriteLongMessage(List<string> message)
        {
            Terminal.Clear();

            for (int row = 0; row < message.Count; row++)
            {
                Terminal.Print(0, row, message[row]);
            }

            Terminal.Refresh();
            WaitForInput();
        }

        public override void WriteMessage(string message)
        {
            Terminal.Print(0, 0, message.PadRight(ScreenWidth));
            Terminal.Refresh();
        }

        public override string QueryUser(string prompt)
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

        public override char WaitForInput()
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

        public override void TitleScreen()
        {
            base.TitleScreen();
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
