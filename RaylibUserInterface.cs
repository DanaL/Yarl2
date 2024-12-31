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

using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Yarl2;

class RaylibUserInterface : UserInterface
{
  Font _font;
  int _fontWidth;
  int _fontHeight;
  Dictionary<Colour, Color> _colours = [];
  DateTime _lastKeyTime = DateTime.MinValue;

  public RaylibUserInterface(string windowTitle, Options opt) : base(opt)
  {
    const int padding = 20;
    int width = ScreenWidth * (opt.FontSize / 2) + (padding * 2);
    int height = ScreenHeight * opt.FontSize + (padding * 2);

    SetConfigFlags(ConfigFlags.VSyncHint);    
    InitWindow(width, height, windowTitle);
    SetExitKey(KeyboardKey.Null);
    SetTargetFPS(60);

    FontSize = opt.FontSize;
    string fontPath = ResourcePath.GetBaseFilePath("DejaVuSansMono.ttf");

    // This feels unnecessary to me, but if I don't load the font this way
    // the unicode characters don't render correctly.
    const int GLYPH_COUNT = 65535;
    int[] codepoints = new int[GLYPH_COUNT];
    for (int i = 0; i < GLYPH_COUNT; i++)
    {
      codepoints[i] = i;
    }
    _font = LoadFontEx(fontPath, FontSize, codepoints, GLYPH_COUNT);

    _fontWidth = FontSize / 2;
    _fontHeight = FontSize;

    SetWindowPosition(100, 100);
  }

  protected override GameEvent PollForEvent()
  {
    if (WindowShouldClose())
      return new GameEvent(GameEventType.Quiting, '\0');
      
    int ch = GetCharPressed();
    if (ch > 0)
    {
      return new GameEvent(GameEventType.KeyInput, (char)ch);
    }
  
    bool isAnyKeyDown = false;
    GameEvent? gameEvent = null;

    if (IsKeyDown(KeyboardKey.Left))
    {
      isAnyKeyDown = true;
      gameEvent = new GameEvent(GameEventType.KeyInput, 'h');
    }
    else if (IsKeyDown(KeyboardKey.Right))
    {
      isAnyKeyDown = true;
      gameEvent = new GameEvent(GameEventType.KeyInput, 'l');
    }
    else if (IsKeyDown(KeyboardKey.Up))
    {
      isAnyKeyDown = true;
      gameEvent = new GameEvent(GameEventType.KeyInput, 'k');
    }
    else if (IsKeyDown(KeyboardKey.Down))
    {
      isAnyKeyDown = true;
      gameEvent = new GameEvent(GameEventType.KeyInput, 'j');
    }
    else if (IsKeyPressed(KeyboardKey.Escape))
    {
      isAnyKeyDown = true;
      gameEvent = new GameEvent(GameEventType.KeyInput, (char) Constants.ESC);
    }
    else if (IsKeyPressed(KeyboardKey.Enter)) 
    {
      isAnyKeyDown = true;
      gameEvent = new GameEvent(GameEventType.KeyInput, (char)13);  // ASCII CR
    }
    else if (IsKeyDown(KeyboardKey.Backspace)) 
    {
      isAnyKeyDown = true;
      gameEvent = new GameEvent(GameEventType.KeyInput, (char) Constants.BACKSPACE);
    }

    if (isAnyKeyDown && gameEvent is not null)
    {
      TimeSpan delta = DateTime.Now - _lastKeyTime;
      if (isAnyKeyDown && delta.TotalMilliseconds >= 150)
      {
        _lastKeyTime = DateTime.Now;
        return (GameEvent) gameEvent;
      }
    }
   
    return new GameEvent(GameEventType.NoEvent, '\0');
  }

  Color ToRaylibColor(Colour colour)
  {
    if (!_colours.TryGetValue(colour, out Color value))
    {
      value = new Color(colour.R, colour.G, colour.B, colour.Alpha);
      _colours.Add(colour, value);
    }

    return value;
  }

  public override void WriteLine(string message, int lineNum, int col, int width, Colour textColour)
  {
    WriteLine(message, lineNum, col, width, textColour, Colours.BLACK);
  }

  public override void WriteLine(string message, int lineNum, int col, int width, Colour textColour, Colour bgColour)
  {
    message = message.PadRight(width);
    Vector2 position = new(col * _fontWidth, lineNum * _fontHeight);

    DrawRectangle((int)position.X, (int)position.Y, width * _fontWidth, _fontHeight, ToRaylibColor(bgColour));
    DrawTextEx(_font, message, position, _fontHeight, 0, ToRaylibColor(textColour));
  }

  protected override void WriteSq(int row, int col, Sqr sq)
  {
    Vector2 position = new(col * (FontSize / 2), row * FontSize);
    DrawRectangle((int) position.X, (int) position.Y, FontSize / 2, FontSize, ToRaylibColor(sq.Bg));
    
    DrawTextEx(_font, sq.Ch.ToString(), position, FontSize, 0, ToRaylibColor(sq.Fg));
  }

  protected override void ClearScreen() => ClearBackground(Color.Black);
  protected override void Blit() => EndDrawing();

  public override void UpdateDisplay(GameState? gs)
  {
    BeginDrawing();
    ClearBackground(Color.Black);

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

      // Update main display
      int displayHeight = SqsOnScreen.GetLength(0);
      int displayWidth = SqsOnScreen.GetLength(1);
      for (int row = 0; row < displayHeight; row++)
      {
        for (int col = 0; col < displayWidth; col++)
        {
          WriteSq(row, col, SqsOnScreen[row, col]);
        }
      }

      WriteMessagesSection();

      if (MenuRows.Count > 0)
      {
        WriteDropDown();
      }

      WritePopUp();
      WriteConfirmation();
    }

    EndDrawing();
  }
}