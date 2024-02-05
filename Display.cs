﻿using System.Runtime.InteropServices;

using SDL2;
using static SDL2.SDL;

using BearLibNET;
using BearLibNET.DefaultImplementations;
using TKCodes = BearLibNET.TKCodes;

namespace Yarl2;

internal abstract class Display
{
    protected const int BACKSPACE = 8;
    protected const int ScreenWidth = 60;
    protected const int ScreenHeight = 30;
    protected const int SideBarWidth = 20;
    protected const int ViewWidth = ScreenWidth - SideBarWidth;
    
    protected int FontSize;
    protected short PlayerScreenRow;
    protected short PlayerScreenCol;

    protected readonly Color BLACK = new() { A = 255, R = 0, G = 0, B = 0 };
    protected readonly Color WHITE = new() { A = 255, R = 255, G = 255, B = 255 };
    protected readonly Color GREY = new() { A = 255, R = 136, G = 136, B = 136 };
    protected readonly Color LIGHT_GREY = new() { A = 255, R = 220, G = 220, B = 220 };
    protected readonly Color DARK_GREY = new() { A = 255, R = 72, G = 73, B = 75 };
    protected readonly Color YELLOW = new() { A = 255, R = 255, G = 255, B = 53 };
    protected readonly Color LIGHT_BROWN = new() { A = 255, R = 101, G = 75, B = 0 };
    protected readonly Color BROWN = new() { A = 255, R = 101, G = 67, B = 33 };

    public abstract Action? GetCommand(GameState gameState);
    public abstract string QueryUser(string prompt);        
    public abstract void UpdateDisplay(GameState gameState);
    public abstract char WaitForInput();
    public abstract void WriteLongMessage(List<string> message);
    public abstract void WriteMessage(string message);

    public Player? Player { get; set; } = null;

    public Display()
    {
        PlayerScreenRow = (ScreenHeight - 1) / 2 + 1;
        PlayerScreenCol = (ScreenWidth - SideBarWidth - 1) / 2;
    }

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
    }

    private (short, short) AskForDirection()
    {
        do 
        {
            WriteMessage("Which way?");
            char ch = WaitForInput();
            if (ch == 'y')
                return (-1, -1);
            else if (ch == 'u')
                return (-1, 1);
            else if (ch == 'h')
                return (0, -1);
            else if (ch == 'j')
                return (1, 0);
            else if (ch == 'k')
                return (0, -1);
            else if (ch == 'l')
                return (0, 1);
            else if (ch == 'b')
                return (1, -1);
            else if (ch == 'n')
                return (1, 1);
        }
        while (true);
    }

    protected Action KeyToCommand(char ch, GameState gameState)
    {
        Player p = gameState.Player!;
        Map m = gameState.Map!;

        if (ch == 'c') 
        {
            var (dr, dc) = AskForDirection();            
            return new CloseDoorAction(p, (ushort)(p.Row + dr), (ushort)(p.Col + dc), m);
        }
        else if (ch == 'o') 
        {
            var (dr, dc) = AskForDirection();            
            return new OpenDoorAction(p, (ushort)(p.Row + dr), (ushort)(p.Col + dc), m);
        }
        else if (ch == 'h')
            return new MoveAction(p, p.Row, (ushort)(p.Col - 1), gameState);
        else if (ch == 'j')
            return new MoveAction(p, (ushort)(p.Row + 1), p.Col, gameState);
        else if (ch == 'k')
            return new MoveAction(p, (ushort)(p.Row - 1), p.Col, gameState);
        else if (ch == 'l')
            return new MoveAction(p, p.Row, (ushort)(p.Col + 1), gameState);
        else if (ch == 'y')
            return new MoveAction(p, (ushort)(p.Row - 1), (ushort)(p.Col - 1), gameState);
        else if (ch == 'u')
            return new MoveAction(p, (ushort)(p.Row - 1), (ushort)(p.Col + 1), gameState);
        else if (ch == 'b')
            return new MoveAction(p, (ushort)(p.Row + 1), (ushort)(p.Col - 1), gameState);
        else if (ch == 'n')
            return new MoveAction(p, (ushort)(p.Row + 1), (ushort)(p.Col + 1), gameState);
        else if (ch == 'Q')
            return new QuitAction();
        else
            return new PassAction(p);
    }

    protected (Color, char) TileToGlyph(Tile tile, bool lit)
    {
        switch (tile.Type)
        {
            case TileType.Wall:
            case TileType.PermWall:
                return lit ? (GREY, '#') : (DARK_GREY, '#');
            case TileType.Floor:
                return lit ? (YELLOW, '.') : (GREY, '.');
            case TileType.Door:
                char ch = ((Door)tile).Open ? '\\' : '+';
                return lit ? (LIGHT_BROWN, ch) : (BROWN, ch);
            default:
                return (BLACK, ' ');
        }        
    }
}

internal class SDLDisplay : Display
{
    private readonly IntPtr _window;
    private readonly IntPtr _renderer, _font;
    private readonly int _fontWidth;
    private readonly int _fontHeight;
    private string _lastMessage = "";
    private IntPtr _lastFrameTexture;
    private SDL_Rect _lastFrameLoc;
    private Dictionary<(char, Color, Color), IntPtr> _cachedGlyphs;

    private Dictionary<Color, SDL_Color> _colours;

    public SDLDisplay(string windowTitle, int fontSize) : base()
    {
        FontSize = fontSize;
        SDL_Init(SDL_INIT_VIDEO);
        SDL_ttf.TTF_Init();
        _font = SDL_ttf.TTF_OpenFont("DejaVuSansMono.ttf", fontSize);
        SDL_ttf.TTF_SizeUTF8(_font, " ", out _fontWidth, out _fontHeight);
        
        int width = ScreenWidth * _fontWidth;
        int height = ScreenHeight * _fontHeight;
        _window = SDL_CreateWindow(windowTitle, 100, 100, width, height, SDL_WindowFlags.SDL_WINDOW_SHOWN | SDL_WindowFlags.SDL_WINDOW_INPUT_FOCUS);
        _renderer = SDL_CreateRenderer(_window, -1, SDL_RendererFlags.SDL_RENDERER_PRESENTVSYNC);

        _colours = [];

        _lastFrameLoc = new SDL_Rect
        {
            x = 0,
            y = _fontHeight,
            h = (ScreenHeight - 1) * _fontHeight,
            w = ViewWidth * _fontWidth
        };

        _cachedGlyphs = new();
    }

    public override Action? GetCommand(GameState gameState)
    {
        SDL_FlushEvent(SDL_EventType.SDL_TEXTINPUT);

        do
        {
            SDL_WaitEvent(out var e);
            switch (e.type)
            {
                case SDL_EventType.SDL_QUIT:
                    return new QuitAction();
                case SDL_EventType.SDL_TEXTINPUT:
                    char c;
                    unsafe
                    {
                        c = (char)*e.text.text;
                    }

                    return KeyToCommand(c, gameState);
            }
        }
        while (true);
    }

    public override string QueryUser(string prompt)
    {
        string answer = "";

        do 
        {
            WriteMessage($"{prompt} {answer}");
            char ch = WaitForInput();            
            if (ch == 13)
                return answer;
            else if (ch == BACKSPACE)
                answer = answer.Length > 0 ? answer[..^1] : "";
            else
                answer += ch;
        } 
        while (true);
    }

    private static char KeysymToChar(SDL_Keysym keysym) 
    {
        return keysym.mod == SDL_Keymod.KMOD_LSHIFT || keysym.mod == SDL_Keymod.KMOD_RSHIFT
            ? char.ToUpper((char)keysym.sym)
            : (char)keysym.sym;
    }

    public override char WaitForInput()
    {        
        do 
        {
            SDL_WaitEvent(out var e);
            switch (e.type) 
            {                
                case SDL_EventType.SDL_KEYDOWN:                    
                    if (e.key.keysym.sym == SDL_Keycode.SDLK_LSHIFT || e.key.keysym.sym == SDL_Keycode.SDLK_RSHIFT)
                        continue;
                    
                    return KeysymToChar(e.key.keysym);                    
            }
        } 
        while (true);        
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

    private void WriteLine(string message, int lineNum, int col, int width)
    {
        message = message.PadRight(width);
        var fontPtr = _font;
        var fh = _fontHeight;
        var surface =  SDL_ttf.TTF_RenderText_Shaded(fontPtr, message, ToSDLColour(WHITE), ToSDLColour(BLACK));        
        var s = (SDL_Surface)Marshal.PtrToStructure(surface, typeof(SDL_Surface))!;
        
        var texture = SDL_CreateTextureFromSurface(_renderer, surface);
        var loc = new SDL_Rect
        {
            x = 2 + col * _fontWidth,
            y = lineNum * fh,
            h = fh,
            w = s.w
        };
        
        SDL_FreeSurface(surface);
        SDL_SetRenderDrawColor(_renderer, 0, 0, 0, 255);
        SDL_RenderCopy(_renderer, texture, IntPtr.Zero, ref loc);
    }

    public override void WriteLongMessage(List<string> message)
    {
        SDL_RenderClear(_renderer);
        for (int j = 0; j < message.Count; j++)
        {
            WriteLine(message[j], j, 0, ScreenWidth);
        }
        SDL_RenderPresent(_renderer);
        WaitForInput();
    }

    public override void WriteMessage(string message)
    {        
        _lastMessage = message;
        DrawFrame();
    }

    private void SDLPut(short row, short col, char ch, Color color) 
    {
        var key = (ch, color, BLACK);

        if (!_cachedGlyphs.TryGetValue(key, out IntPtr texture))
        {
            var surface =  SDL_ttf.TTF_RenderText_Shaded(_font, ch.ToString(), ToSDLColour(color), ToSDLColour(BLACK));        
            var toCache = SDL_CreateTextureFromSurface(_renderer, surface);            
            SDL_FreeSurface(surface);
            texture = toCache;
            _cachedGlyphs.Add(key, texture);
        }

        var loc = new SDL_Rect { x = col * _fontWidth + 2, y = row * _fontHeight, h = _fontHeight, w = _fontWidth };

        SDL_RenderCopy(_renderer, texture, IntPtr.Zero, ref loc);
    }

    private IntPtr CreateMainTexture(ushort playerRow, ushort playerCol, GameState gameState)
    {
        var tw = ViewWidth * _fontWidth;
        var th = (ScreenHeight - 1) * _fontHeight;
        var targetTexture = SDL_CreateTexture(_renderer, SDL_PIXELFORMAT_RGBX8888, (int) SDL_TextureAccess.SDL_TEXTUREACCESS_TARGET , tw, th);

        SDL_SetRenderTarget(_renderer, targetTexture);

        short rowOffset = (short) (playerRow - PlayerScreenRow);
        short colOffset = (short) (playerCol - PlayerScreenCol);
        for (short row = 0; row < ScreenHeight - 1; row++)
        {
            for (short col = 0; col < ViewWidth; col++)
            {
                ushort vr = (ushort)(row + rowOffset);
                ushort vc = (ushort)(col + colOffset);
                Color color;
                char ch;
                if (gameState.Visible!.Contains((vr, vc)))                
                    (color, ch) = TileToGlyph(gameState.Map!.TileAt(vr, vc), true);                
                else if (gameState.Remebered!.Contains((vr, vc)))
                    (color, ch) = TileToGlyph(gameState.Map!.TileAt(vr, vc), false);
                else
                    (color, ch) = (BLACK, ' ');                
                SDLPut(row, col, ch, color);
            }
        }
        
        SDLPut(PlayerScreenRow, PlayerScreenCol, '@', WHITE);
    
        SDL_SetRenderTarget(_renderer, IntPtr.Zero);
        
        return targetTexture;
    }

    void WriteSideBar(Player player)
    {
        var width = ScreenWidth - ViewWidth;
        WriteLine($"| {player.Name}".PadRight(width), 1, ViewWidth, width);
        WriteLine($"| HP: {player.CurrHP} ({player.MaxHP})".PadRight(width), 2, ViewWidth, width);
        
        string blank = "|".PadRight(ViewWidth);
        for (int row = 3; row < ScreenHeight; row++)
        {
            WriteLine(blank, row, ViewWidth, width);
        }
    }

    private void DrawFrame()
    {
        SDL_RenderClear(_renderer);
        WriteLine(_lastMessage, 0, 0, ScreenWidth);
        if (Player is not null) 
        {
            WriteSideBar(Player);
            SDL_RenderCopy(_renderer, _lastFrameTexture, IntPtr.Zero, ref _lastFrameLoc);
        }
        SDL_RenderPresent(_renderer);
    }

    public override void UpdateDisplay(GameState gameState)
    {
        if (Player is null)
            throw new Exception("Hmm this shouldn't happen");

        _lastFrameTexture = CreateMainTexture(Player.Row, Player.Col, gameState);
        DrawFrame();        
    }
}

internal class BLDisplay : Display, IDisposable
{        
    private readonly Dictionary<int, char> KeyToChar = [];

    public BLDisplay(string windowTitle, int fontSize) : base()
    {
        FontSize = fontSize;
        SetUpKeyToCharMap();
        Terminal.Open();
        Terminal.Set($"window: size={ScreenWidth}x{ScreenHeight}, title={windowTitle}; font: DejaVuSansMono.ttf, size={FontSize}");            
    }

    private void SetUpKeyToCharMap()
    {
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

    public override Action? GetCommand(GameState gameState)
    {
        if (Terminal.HasInput())
        {
            var ch = WaitForInput();
            return KeyToCommand(ch, gameState);
        }
        else 
        {
            return null;
        }
    }

    void WriteSideBar()
    {
        Terminal.Print(ViewWidth, 1, $"| {Player.Name}".PadRight(ViewWidth));
        Terminal.Print(ViewWidth, 2, $"| HP: {Player.CurrHP} ({Player.MaxHP})".PadRight(ViewWidth));

        string blank = "|".PadRight(ViewWidth);
        for (int row = 3; row < ScreenHeight; row++)
        {
            Terminal.Print(ViewWidth, row, blank);
        }
    }

    public override void UpdateDisplay(GameState gameState)
    {
        short rowOffset = (short) (Player!.Row - PlayerScreenRow);
        short colOffset = (short) (Player!.Col - PlayerScreenCol);
        for (short row = 0; row < ScreenHeight - 1; row++)
        {
            for (short col = 0; col < ViewWidth; col++)
            {
                ushort vr = (ushort)(row + rowOffset);
                ushort vc = (ushort)(col + colOffset);

                if (gameState.Visible!.Contains((vr, vc)))
                {
                    var (color, ch) = TileToGlyph(gameState.Map!.TileAt(vr, vc), true);
                    Terminal.Color(color);
                    Terminal.Put(col, row + 1, ch);
                }
                else if (gameState.Remebered!.Contains((vr, vc)))
                {
                    var (color, ch) = TileToGlyph(gameState.Map!.TileAt(vr, vc), false);
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

        WriteSideBar();

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
