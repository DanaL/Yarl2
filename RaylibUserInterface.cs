// Delve - A roguelike computer RPG
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
  Dictionary<char, string> _charCache = [];
  
  float _heldTime;
  double _lastEvent;
  const float INITIAL_DELAY = 0.25f;
  const float REPEAT_INTERVAL = 0.1f;

  public RaylibUserInterface(string windowTitle, Options opt) : base()
  {
    int width = ScreenWidth * (opt.FontSize / 2) + 2;
    int height = ScreenHeight * opt.FontSize + 2;

    SetConfigFlags(ConfigFlags.VSyncHint);
    SetTraceLogLevel(TraceLogLevel.None);
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

    SetWindowPosition(100, 32);
  }

  public override void SetFontSize(int newSize)
  {
    UnloadFont(_font);

    FontSize = newSize;
    _fontWidth = FontSize / 2;
    _fontHeight = FontSize;

    string fontPath = ResourcePath.GetBaseFilePath("DejaVuSansMono.ttf");
    const int GLYPH_COUNT = 65535;
    int[] codepoints = new int[GLYPH_COUNT];
    for (int i = 0; i < GLYPH_COUNT; i++)
      codepoints[i] = i;
    _font = LoadFontEx(fontPath, FontSize, codepoints, GLYPH_COUNT);

    int width = ScreenWidth * _fontWidth + 2;
    int height = ScreenHeight * _fontHeight + 2;
    SetWindowSize(width, height);
  }

  GameEvent? CheckHeldKey(bool pressed, bool down, float dt, double now, char resultChar)
  {
    if (!pressed && !down)
      return null;

    _heldTime += dt;
    if (pressed && _heldTime > INITIAL_DELAY)
    {
      _heldTime = 0;
      _lastEvent = now;
      return new(GameEventType.KeyInput, resultChar);
    }
    else if (down && !pressed && now - _lastEvent > REPEAT_INTERVAL)
    {
      _lastEvent = now;
      return new(GameEventType.KeyInput, resultChar);
    }
    else
    {
      return Constants.NO_EVENT;
    }
  }

  protected override GameEvent PollForEvent(bool pause = true)
  {
    if (pause)
      Delay(2);

    if (WindowShouldClose())
    {
      return State switch
      {
        UIState.InGame =>  new GameEvent(GameEventType.KeyInput, 'S'),
        _ => new GameEvent(GameEventType.Quiting, '\0')
      };
    }

    int ch = GetCharPressed();
    if (ch >= 32 && ch <= 126)
    {
      return new(GameEventType.KeyInput, (char)ch);
    }

    // Handle special keys that might be repeated    
    float dt = GetFrameTime();
    double now = GetTime();

    if (CheckHeldKey(IsKeyPressed(KeyboardKey.Up), IsKeyDown(KeyboardKey.Up), dt, now, Constants.ARROW_N) is GameEvent upEvt)
      return upEvt;
    else if (CheckHeldKey(IsKeyPressed(KeyboardKey.Kp8), IsKeyDown(KeyboardKey.Kp8), dt, now, Constants.ARROW_N) is GameEvent upKpEvt)
      return upKpEvt;
    else if (CheckHeldKey(IsKeyPressed(KeyboardKey.Left), IsKeyDown(KeyboardKey.Left), dt, now, Constants.ARROW_W) is GameEvent leftEvt)
      return leftEvt;
    else if (CheckHeldKey(IsKeyPressed(KeyboardKey.Right), IsKeyDown(KeyboardKey.Right), dt, now, Constants.ARROW_E) is GameEvent rightEvt)
      return rightEvt;
    else if (CheckHeldKey(IsKeyPressed(KeyboardKey.Kp4), IsKeyDown(KeyboardKey.Kp4), dt, now, Constants.ARROW_W) is GameEvent leftKpEvt)
      return leftKpEvt;
    else if (CheckHeldKey(IsKeyPressed(KeyboardKey.Kp6), IsKeyDown(KeyboardKey.Kp6), dt, now, Constants.ARROW_E) is GameEvent rightKpEvt)
      return rightKpEvt;
    else if (CheckHeldKey(IsKeyPressed(KeyboardKey.Down), IsKeyDown(KeyboardKey.Down), dt, now, Constants.ARROW_S) is GameEvent downEvt)
      return downEvt;
    else if (CheckHeldKey(IsKeyPressed(KeyboardKey.Kp2), IsKeyDown(KeyboardKey.Kp2), dt, now, Constants.ARROW_S) is GameEvent downKpEvt)
      return downKpEvt;
    else if (CheckHeldKey(IsKeyPressed(KeyboardKey.Kp7), IsKeyDown(KeyboardKey.Kp7), dt, now, Constants.ARROW_NW) is GameEvent nwKpEvt)
      return nwKpEvt;
    else if (CheckHeldKey(IsKeyPressed(KeyboardKey.Kp9), IsKeyDown(KeyboardKey.Kp9), dt, now, Constants.ARROW_NE) is GameEvent neKpEvt)
      return neKpEvt;
    else if (CheckHeldKey(IsKeyPressed(KeyboardKey.Kp1), IsKeyDown(KeyboardKey.Kp1), dt, now, Constants.ARROW_SW) is GameEvent swKpEvt)
      return swKpEvt;
    else if (CheckHeldKey(IsKeyPressed(KeyboardKey.Kp3), IsKeyDown(KeyboardKey.Kp3), dt, now, Constants.ARROW_SE) is GameEvent seKpEvt)
      return seKpEvt;
    else if (CheckHeldKey(IsKeyPressed(KeyboardKey.Kp5), IsKeyDown(KeyboardKey.Kp5), dt, now, Constants.PASS) is GameEvent passKpEvt)
      return passKpEvt;
    else if (CheckHeldKey(IsKeyPressed(KeyboardKey.Backspace), IsKeyDown(KeyboardKey.Backspace), dt, now, (char) Constants.BACKSPACE) is GameEvent bspcEvt)
      return bspcEvt;

    var key = GetKeyPressed();
    if (key == (int) KeyboardKey.Escape)
    {
      return new(GameEventType.KeyInput, (char)Constants.ESC);
    }
    else if (key == (int) KeyboardKey.Enter || key == (int) KeyboardKey.KpEnter)
    {
      return new(GameEventType.KeyInput, (char)13);
    }
    else if (key == (int) KeyboardKey.Tab)
    {
      return new(GameEventType.KeyInput, (char)Constants.TAB);
    }
   
    _heldTime = 0;

    return Constants.NO_EVENT;
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

  string CharToString(char ch)
  {
    if (!_charCache.TryGetValue(ch, out string? value))
    {
      value = ch.ToString();
      _charCache[ch] = value;
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

  public override void WriteSq(int row, int col, Sqr sq)
  {
    Vector2 position = new(col * (FontSize / 2), row * FontSize);
    DrawRectangle((int) position.X, (int) position.Y, FontSize / 2, FontSize, ToRaylibColor(sq.Bg));

    DrawTextEx(_font, CharToString(sq.Ch), position, FontSize, 0, ToRaylibColor(sq.Fg));
  }

  public override void ClearScreen() => ClearBackground(Color.Black);
  protected override void Blit() => EndDrawing();

  public override void UpdateDisplay(GameState? gs)
  {
    BeginDrawing();
    ClearBackground(Color.Black);

    if (_longMessage.Count > 0)
    {
      WriteLongMessage();
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

      WriteMessagesSection(gs);

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