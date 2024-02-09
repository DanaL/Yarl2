using System.Runtime.InteropServices;

using SDL2;
using static SDL2.SDL;
using BearLibNET.DefaultImplementations;

namespace Yarl2;

internal class SDLUserInterface : UserInterface
{
    private readonly IntPtr _window;
    private readonly IntPtr _renderer, _font;
    private readonly int _fontWidth;
    private readonly int _fontHeight;    
    private IntPtr _lastFrameTexture;
    private SDL_Rect _lastFrameLoc;
    private Dictionary<(char, Color, Color), IntPtr> _cachedGlyphs;

    private Dictionary<Color, SDL_Color> _colours;

    public SDLUserInterface(string windowTitle, Options opt) : base(opt)
    {
        FontSize = opt.FontSize;
        SDL_Init(SDL_INIT_VIDEO);
        SDL_ttf.TTF_Init();
        _font = SDL_ttf.TTF_OpenFont("DejaVuSansMono.ttf", opt.FontSize);
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

    protected override UIEvent PollForEvent()
    {
        while (SDL_PollEvent(out var e) != 0) {
            switch (e.type)
            {
                case SDL_EventType.SDL_QUIT:
                    return new UIEvent(UIEventType.Quiting, '\0');
                case SDL_EventType.SDL_KEYDOWN:
                    if (e.key.keysym.sym == SDL_Keycode.SDLK_LSHIFT || e.key.keysym.sym == SDL_Keycode.SDLK_RSHIFT)
                        return new UIEvent(UIEventType.NoEvent, '\0');
                    var ch = e.key.keysym;
                    return new UIEvent(UIEventType.KeyInput, KeysymToChar(ch));

                default:
                    return new UIEvent(UIEventType.NoEvent, '\0');
            }        
        }

        return new UIEvent(UIEventType.NoEvent, '\0');
    }

    private static char KeysymToChar(SDL_Keysym keysym) 
    {
        return keysym.mod == SDL_Keymod.KMOD_LSHIFT || keysym.mod == SDL_Keymod.KMOD_RSHIFT
            ? char.ToUpper((char)keysym.sym)
            : (char)keysym.sym;
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

    private void SDLPut(int row, int col, char ch, Color color) 
    {
        var key = (ch, color, BLACK);

        if (!_cachedGlyphs.TryGetValue(key, out IntPtr texture))
        {
            var surface =  SDL_ttf.TTF_RenderUNICODE_Shaded(_font, ch.ToString(), ToSDLColour(color), ToSDLColour(BLACK));        
            var toCache = SDL_CreateTextureFromSurface(_renderer, surface);            
            SDL_FreeSurface(surface);
            texture = toCache;
            _cachedGlyphs.Add(key, texture);
        }

        var loc = new SDL_Rect { x = col * _fontWidth + 2, y = row * _fontHeight, h = _fontHeight, w = _fontWidth };

        SDL_RenderCopy(_renderer, texture, IntPtr.Zero, ref loc);
    }

    private IntPtr CreateMainTexture(int playerRow, int playerCol, GameState gameState)
    {
        var tw = ViewWidth * _fontWidth;
        var th = (ScreenHeight - 1) * _fontHeight;
        var targetTexture = SDL_CreateTexture(_renderer, SDL_PIXELFORMAT_RGBX8888, (int) SDL_TextureAccess.SDL_TEXTUREACCESS_TARGET , tw, th);

        SDL_SetRenderTarget(_renderer, targetTexture);

        int rowOffset = playerRow - PlayerScreenRow;
        int colOffset = playerCol - PlayerScreenCol;
        for (int row = 0; row < ScreenHeight - 1; row++)
        {
            for (int col = 0; col < ViewWidth; col++)
            {
                int vr = row + rowOffset;
                int vc = col + colOffset;
                Color color;
                char ch;
                if (gameState.Visible!.Contains((gameState.CurrLevel, vr, vc)))                
                    (color, ch) = TileToGlyph(gameState.Map!.TileAt(vr, vc), true);                
                else if (gameState.Remebered!.Contains((gameState.CurrLevel, vr, vc)))
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

    public override void UpdateDisplay(GameState gameState)
    {
        SDL_RenderClear(_renderer);
        if (_longMessage is not null) 
        {
            for (int j = 0; j < _longMessage.Count; j++)
            {
                WriteLine(_longMessage[j], j, 0, ScreenWidth);
            }
        }
        else
        {
            WriteLine(_messageBuffer, 0, 0, ScreenWidth);
            if (Player is not null) {
                _lastFrameTexture = CreateMainTexture(Player.Row, Player.Col, gameState);
                WriteSideBar(Player);
                SDL_RenderCopy(_renderer, _lastFrameTexture, IntPtr.Zero, ref _lastFrameLoc);
            }
        }
        SDL_RenderPresent(_renderer);
    }
}
