using System.Runtime.InteropServices;

using SDL2;
using static SDL2.SDL;

namespace Yarl2;

internal class SDLUserInterface : UserInterface
{
    private readonly IntPtr _window;
    private readonly IntPtr _renderer, _font;
    private readonly int _fontWidth;
    private readonly int _fontHeight;
    private SDL_Rect _mainFrameLoc;
    private Dictionary<(char, Colour, Colour), IntPtr> _cachedGlyphs;
    private Dictionary<Colour, SDL_Color> _colours;

    // This may be a performance kludge for my last of understanding of SDL2 that
    // doesn't pan out.
    private (Colour, char)[,] _prevTiles = new (Colour, char)[ScreenHeight - 1, ViewWidth];    
    private string _prevMessage = "";
    
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

        _mainFrameLoc = new SDL_Rect
        {
            x = 0,
            y = _fontHeight,
            h = (ScreenHeight - 1) * _fontHeight,
            w = ViewWidth * _fontWidth
        };

        _cachedGlyphs = new();

        for (int r = 0; r < ScreenHeight - 1; r++)
        {
            for (int c = 0; c < ViewWidth; c++)
            {
                _prevTiles[r, c] = (Colours.BLACK, ' ');
            }
        }
    }

    public override void CloseMenu() => ClosingMenu = true;

    protected override UIEvent PollForEvent()
    {
        while (SDL_PollEvent(out var e) != 0) 
        {
            switch (e.type)
            {
                case SDL_EventType.SDL_QUIT:
                    return new UIEvent(UIEventType.Quiting, '\0');
                case SDL_EventType.SDL_TEXTINPUT:
                    char c;
                    unsafe
                    {
                        c = (char)*e.text.text;                    
                    }
                    SDL_FlushEvent(SDL_EventType.SDL_TEXTINPUT);
                    return new UIEvent(UIEventType.KeyInput, c);
                case SDL_EventType.SDL_KEYDOWN:
                    // I feel like there has to be a better way to handle this stuff, but 
                    // they keydown event was receiving , and . etc even when the shift 
                    // key was held down but SDL_TEXTINPUT doesn't receive carriages returns,
                    // etc. I need to look at someone else's SDL keyboard handling code...                    
                    if (e.key.keysym.sym == SDL_Keycode.SDLK_LSHIFT || e.key.keysym.sym == SDL_Keycode.SDLK_RSHIFT)
                        return new UIEvent(UIEventType.NoEvent, '\0');
                    var k = e.key.keysym.sym;
                    var ch = (char)e.key.keysym.sym;
                    return k switch 
                    {
                        SDL_Keycode.SDLK_ESCAPE => new UIEvent(UIEventType.KeyInput, ch),
                        SDL_Keycode.SDLK_RETURN => new UIEvent(UIEventType.KeyInput, ch),
                        SDL_Keycode.SDLK_BACKSPACE => new UIEvent(UIEventType.KeyInput, ch),
                        _ => new UIEvent(UIEventType.NoEvent, '\0')
                    };                        
                default:
                    return new UIEvent(UIEventType.NoEvent, '\0');
            }        
        }

        return new UIEvent(UIEventType.NoEvent, '\0');
    }

    private SDL_Color ToSDLColour(Colour colour) 
    {
        if (!_colours.TryGetValue(colour, out SDL_Color value)) 
        {
            value = new SDL_Color() { 
                    a = (byte) 255, 
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
        var surface =  SDL_ttf.TTF_RenderText_Shaded(_font, message, ToSDLColour(Colours.WHITE), ToSDLColour(Colours.BLACK));        
        var s = (SDL_Surface)Marshal.PtrToStructure(surface, typeof(SDL_Surface))!;
        
        var texture = SDL_CreateTextureFromSurface(_renderer, surface);
        var loc = new SDL_Rect
        {
            x = 2 + col * _fontWidth,
            y = lineNum * _fontHeight,
            h = _fontHeight,
            w = s.w
        };
        
        SDL_FreeSurface(surface);
        SDL_SetRenderDrawColor(_renderer, 0, 0, 0, 255);
        SDL_RenderCopy(_renderer, texture, IntPtr.Zero, ref loc);
        SDL_DestroyTexture(texture);
    }

    private void WriteDropDown()
    {
        int width = MenuRows!.Select(r => r.Length).Max() + 2;
        int col = ViewWidth - width;
        int row = 1;

        foreach (var line in MenuRows!)
        {
            WriteLine(" " +line, row++, col, width);
        }
        WriteLine("", row, col, width);
    }

    private void SDLPut(int row, int col, char ch, Colour color) 
    {
        var key = (ch, color, Colours.BLACK);

        if (!_cachedGlyphs.TryGetValue(key, out IntPtr texture))
        {
            var surface =  SDL_ttf.TTF_RenderUNICODE_Shaded(_font, ch.ToString(), ToSDLColour(color), ToSDLColour(Colours.BLACK));        
            var toCache = SDL_CreateTextureFromSurface(_renderer, surface);            
            SDL_FreeSurface(surface);
            texture = toCache;
            _cachedGlyphs.Add(key, texture);
        }

        var loc = new SDL_Rect { x = col * _fontWidth + 2, y = row * _fontHeight, h = _fontHeight, w = _fontWidth };

        SDL_RenderCopy(_renderer, texture, IntPtr.Zero, ref loc);
    }

    private IntPtr CreateMainTexture()
    {
        var tw = ViewWidth * _fontWidth;
        var th = (ScreenHeight - 1) * _fontHeight;
        var targetTexture = SDL_CreateTexture(_renderer, SDL_PIXELFORMAT_RGBX8888, (int) SDL_TextureAccess.SDL_TEXTUREACCESS_TARGET , tw, th);

        SDL_SetRenderTarget(_renderer, targetTexture);

        for (int row = 0; row < ScreenHeight - 1; row++)
        {
            for (int col = 0; col < ViewWidth; col++)
            {
                var (colour, ch) = SqsOnScreen[row, col];                            
                SDLPut(row, col, ch, colour);
            }
        }
        
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

    private bool FrameChanged()
    {
        if (ClosingMenu)
        {
            MenuRows = null;
            ClosingMenu = false;
            return true;
        }

        if (OpeningMenu)
        {
            OpeningMenu = false;
            return true;
        }

        if (_prevMessage != _messageBuffer)
            return true;

        for (int row = 0; row < ScreenHeight - 1; row++)
        {
            for (int col = 0; col < ViewWidth; col++)
            {
                if (_prevTiles[row, col] != SqsOnScreen[row, col])
                    return true;                
            }
        }

        return false;
    }

    private void SaveLastFame()
    {
        for (int row = 0; row < ScreenHeight - 1; row++)
        {
            for (int col = 0; col < ViewWidth; col++)
            {
                _prevTiles[row, col] = SqsOnScreen[row, col];
            }
        }
    }

    public override void UpdateDisplay()
    {
        // TODO: when the sidebar actually does something,
        //       I'll need to also check if it changed
        if (_longMessage is null && !FrameChanged())
            return;
            
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
            _prevMessage = _messageBuffer;
            if (Player is not null)
            {
                WriteSideBar(Player);
                var texture = CreateMainTexture();
                SDL_RenderCopy(_renderer, texture, IntPtr.Zero, ref _mainFrameLoc);
                SDL_DestroyTexture(texture);
                SaveLastFame();
            }

            if (MenuRows is not null)
            {
                WriteDropDown();
            }
        }
        SDL_RenderPresent(_renderer);
    }
}
