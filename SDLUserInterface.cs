// Yarl2 - A roguelike computer RPG
// Written in 2024 by Dana Larose <ywg.dana@gmail.com>
//
// To the extent possible under law, the author(s) have dedicated all copyright
// and related and neighboring rights to this software to the public domain
// worldwide. This software is distributed without any warranty.
//
// You should have received a copy of the CC0 Public Domain Dedication along 
// with this software. If not, 
// see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System.Runtime.InteropServices;

using SDL2;
using static SDL2.SDL;

namespace Yarl2;

class SDLUserInterface : UserInterface
{
  readonly IntPtr _window;
  readonly IntPtr _renderer, _font;
  readonly int _fontWidth;
  readonly int _fontHeight;
  Dictionary<Sqr, IntPtr> _cachedGlyphs = [];
  Dictionary<Colour, SDL_Color> _colours;
  
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
    _renderer = SDL_CreateRenderer(_window, -1, 
      SDL_RendererFlags.SDL_RENDERER_ACCELERATED | SDL_RendererFlags.SDL_RENDERER_PRESENTVSYNC);

    _colours = [];
  }

  protected override GameEvent PollForEvent()
  {
    while (SDL_PollEvent(out var e) != 0)
    {
      switch (e.type)
      {
        case SDL_EventType.SDL_QUIT:
          return new GameEvent(GameEventType.Quiting, '\0');
        case SDL_EventType.SDL_TEXTINPUT:
          char c;
          unsafe
          {
            c = (char)*e.text.text;
          }
          SDL_FlushEvent(SDL_EventType.SDL_TEXTINPUT);
          return new GameEvent(GameEventType.KeyInput, c);
        case SDL_EventType.SDL_KEYDOWN:
          // I feel like there has to be a better way to handle this stuff, but 
          // they keydown event was receiving , and . etc even when the shift 
          // key was held down but SDL_TEXTINPUT doesn't receive carriages returns,
          // etc. I need to look at someone else's SDL keyboard handling code...                    
          if (e.key.keysym.sym == SDL_Keycode.SDLK_LSHIFT || e.key.keysym.sym == SDL_Keycode.SDLK_RSHIFT)
            return new GameEvent(GameEventType.NoEvent, '\0');
          var k = e.key.keysym.sym;
          var ch = (char)e.key.keysym.sym;
          return k switch
          {
            SDL_Keycode.SDLK_ESCAPE => new GameEvent(GameEventType.KeyInput, ch),
            SDL_Keycode.SDLK_RETURN => new GameEvent(GameEventType.KeyInput, ch),
            SDL_Keycode.SDLK_BACKSPACE => new GameEvent(GameEventType.KeyInput, ch),
            SDL_Keycode.SDLK_TAB => new GameEvent(GameEventType.KeyInput, ch),
            SDL_Keycode.SDLK_LEFT => new GameEvent(GameEventType.KeyInput, 'h'),
            SDL_Keycode.SDLK_RIGHT => new GameEvent(GameEventType.KeyInput, 'l'),
            SDL_Keycode.SDLK_UP => new GameEvent(GameEventType.KeyInput, 'k'),
            SDL_Keycode.SDLK_DOWN => new GameEvent(GameEventType.KeyInput, 'j'),
            SDL_Keycode.SDLK_KP_1 => new GameEvent(GameEventType.KeyInput, 'b'),
            SDL_Keycode.SDLK_KP_2 => new GameEvent(GameEventType.KeyInput, 'j'),
            SDL_Keycode.SDLK_KP_3 => new GameEvent(GameEventType.KeyInput, 'n'),
            SDL_Keycode.SDLK_KP_4 => new GameEvent(GameEventType.KeyInput, 'h'),
            SDL_Keycode.SDLK_KP_5 => new GameEvent(GameEventType.KeyInput, '.'),
            SDL_Keycode.SDLK_KP_6 => new GameEvent(GameEventType.KeyInput, 'l'),
            SDL_Keycode.SDLK_KP_7 => new GameEvent(GameEventType.KeyInput, 'y'),
            SDL_Keycode.SDLK_KP_8 => new GameEvent(GameEventType.KeyInput, 'k'),
            SDL_Keycode.SDLK_KP_9 => new GameEvent(GameEventType.KeyInput, 'u'),
            _ => new GameEvent(GameEventType.NoEvent, '\0')
          };
        default:
          return new GameEvent(GameEventType.NoEvent, '\0');
      }
    }

    return new GameEvent(GameEventType.NoEvent, '\0');
  }

  SDL_Color ToSDLColour(Colour colour)
  {
    if (!_colours.TryGetValue(colour, out SDL_Color value))
    {
      value = new SDL_Color()
      {
        a = (byte)colour.Alpha,
        r = (byte)colour.R,
        g = (byte)colour.G,
        b = (byte)colour.B
      };
      _colours.Add(colour, value);
    }

    return value;
  }

  public override void WriteLine(string message, int lineNum, int col, int width, Colour textColour)
  {
    message = message.PadRight(width);
    var surface = SDL_ttf.TTF_RenderUNICODE_Shaded(_font, message, ToSDLColour(textColour), ToSDLColour(Colours.BLACK));
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

  void UpdateMainTexture()
  {    
    int displayHeight = SqsOnScreen.GetLength(0);
    int displayWidth = SqsOnScreen.GetLength(1);

    for (int row = 0; row < displayHeight; row++)
    {
      for (int col = 0; col < displayWidth; col++)
      {
        WriteSq(row, col, SqsOnScreen[row, col]);
      }
    }
  }

  protected override void WriteSq(int row, int col, Sqr sq)
  {
    if (!_cachedGlyphs.TryGetValue(sq, out IntPtr texture))
    {
      nint surface;
      surface = SDL_ttf.TTF_RenderUNICODE_Shaded(_font, sq.Ch.ToString(), ToSDLColour(sq.Fg), ToSDLColour(sq.Bg));
      var toCache = SDL_CreateTextureFromSurface(_renderer, surface);
      SDL_FreeSurface(surface);
      texture = toCache;
      _cachedGlyphs.Add(sq, texture);
    }

    var loc = new SDL_Rect { x = col * _fontWidth + 2, y = row * _fontHeight, h = _fontHeight, w = _fontWidth };

    SDL_RenderCopy(_renderer, texture, IntPtr.Zero, ref loc);
  }

  protected override void ClearScreen() => SDL_RenderClear(_renderer);
  protected override void Blit() => SDL_RenderPresent(_renderer);
  
  public override void UpdateDisplay(GameState? gs)
  {
    SDL_RenderClear(_renderer);
    if (_longMessage is not null)
    {
      for (int j = 0; j < _longMessage.Count; j++)
      {
        WriteLine(_longMessage[j], j, 0, ScreenWidth, Colours.WHITE);
      }

      WritePopUp();
    }
    else
    {
      if (gs is not null && gs.Player is not null)
      {
        WriteSideBar(gs);        
      }

      UpdateMainTexture();

      WriteMessagesSection();

      if (MenuRows.Count > 0)
      {
        WriteDropDown();
      }

      WritePopUp();
      WriteConfirmation();
    }
    
    SDL_RenderPresent(_renderer);
  }
}
