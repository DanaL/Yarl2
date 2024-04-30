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

namespace Yarl2;

abstract class Animation
{
  public DateTime Expiry { get; set; } = DateTime.MaxValue;
  public abstract void Update();
}

class AimAnimation : Animation
{
  public Loc Target { get; set; }
  Loc _start;
  readonly int _scrW;
  readonly int _scrH;
  readonly UserInterface _ui;
  readonly GameState _gs;

  public AimAnimation(UserInterface ui, GameState gs, Loc origin, Loc initialTarget)
  {
    _start = origin;
    Target = initialTarget;
    _ui = ui;
    _gs = gs;
    _scrW = UserInterface.ViewWidth;
    _scrH = UserInterface.ViewHeight;
  }

  public override void Update()
  {
    foreach (var pt in Util.Bresenham(_start.Row, _start.Col, Target.Row, Target.Col))
    {
      var (scrR, scrC) = _ui.LocToScrLoc(pt.Item1, pt.Item2, _gs.Player.Loc.Row, _gs.Player.Loc.Col);
      if (scrR > 0 && scrR < _scrH && scrC > 0 && scrC < _scrW)
      {
        Sqr sq = _ui.SqsOnScreen[scrR, scrC] with { Fg = Colours.WHITE, Bg = Colours.HILITE };
        _ui.SqsOnScreen[scrR, scrC] = sq;
      }
    }
  }
}

class ArrowAnimation : Animation
{
  readonly GameState _gs;
  List<(Loc, char)> _frames = [];
  int _frame = 0;
  Colour _ammoColour;
  DateTime _lastFrame;

  public ArrowAnimation(GameState gs, List<Loc> pts, Colour ammoColour)
  {
    _gs = gs;
    _ammoColour = ammoColour;

    for (int j = 0; j < pts.Count - 1; j++)
    {
      _frames.Add((pts[j + 1], CalcChar(pts[j], pts[j + 1])));
    }
  }

  static char CalcChar(Loc a, Loc b)
  {
    if (a.Row == b.Row)
      return '-';
    else if (a.Col == b.Col)
      return '|';
    else if (a.Col < b.Col && a.Row < b.Row)
      return '\\';
    else if (a.Col > b.Col && a.Row > b.Row)
      return '\\';
    else
      return '/';
  }

  public override void Update()
  {
    var ui = _gs.UIRef();
    var (loc, ch) = _frames[_frame];
    var sq = new Sqr(_ammoColour, Colours.BLACK, ch);
    var (scrR, scrC) = ui.LocToScrLoc(loc.Row, loc.Col, _gs.Player.Loc.Row, _gs.Player.Loc.Col);
    ui.SqsOnScreen[scrR, scrC] = sq;

    if ((DateTime.Now - _lastFrame).TotalMicroseconds > 150)
    {
      _lastFrame = DateTime.Now;
      ++_frame;
    }

    if (_frame >= _frames.Count)
    {
      Expiry = DateTime.MinValue;
    }
  }

}

class ThrownMissileAnimation : Animation
{
  readonly GameState _gs;
  Glyph _glyph;
  List<Loc> _pts;
  int _frame;
  DateTime _lastFrame;
  Item _ammo;

  public ThrownMissileAnimation(GameState gs, Glyph glyph, List<Loc> pts, Item ammo)
  {
    _gs = gs;
    _glyph = glyph;
    _pts = pts;
    _frame = 0;
    _lastFrame = DateTime.Now;
    _ammo = ammo;
  }

  public override void Update()
  {
    var pt = _pts[_frame];
    var sq = new Sqr(_glyph.Lit, Colours.BLACK, _glyph.Ch);
    var ui = _gs.UIRef();
    var (scrR, scrC) = ui.LocToScrLoc(pt.Row, pt.Col, _gs.Player.Loc.Row, _gs.Player.Loc.Col);
    ui.SqsOnScreen[scrR, scrC] = sq;

    if ((DateTime.Now - _lastFrame).TotalMicroseconds > 250)
    {
      _lastFrame = DateTime.Now;
      ++_frame;
    }

    if (_frame >= _pts.Count)
    {
      Expiry = DateTime.MinValue;
      _ammo.SetZ(Item.DEFAULT_Z);

      // This happens inside the animation class because I don't want the
      // message to be displayed until the animation has finished.
      var tile = _gs.TileAt(_pts.Last());
      if (tile.Type == TileType.DeepWater)
      {
        var item = _ammo.FullName.DefArticle().Capitalize();
        var msg = new Message($"{item} disappears with a splash.", _pts.Last());
        ui.AlertPlayer([msg], "You hear a splash.", _gs);
      }
    }
  }
}

class BarkAnimation : Animation
{
  readonly GameState _gs;
  readonly Actor _actor;
  readonly string _bark;

  // Duration in milliseconds
  public BarkAnimation(GameState gs, int duration, Actor actor, string bark)
  {
    _gs = gs;
    Expiry = DateTime.Now.AddMilliseconds(duration);
    _actor = actor;
    _bark = bark;
  }

  // Need to actually calculate where to place the bark if the
  // speaker is near an edge of the screen/
  // screenRow and screenCol are the speaker's row/col
  private void RenderLine(int screenRow, int screenCol, string message)
  {
    int pointerCol = screenCol + 1;
    int row = screenRow + (screenRow < 3 ? 1 : -1);
    int row2 = screenRow + (screenRow < 3 ? 2 : -2);

    int col = screenCol - message.Length / 3;
    char pointer = row > 3 ? '/' : '\\';
    if (col == screenCol)
    {
      pointer = '|';
      pointerCol = screenCol;
    }

    if (screenCol - message.Length / 3 < 0)
    {
      col = 0;
    }
    else if (col + message.Length >= UserInterface.ViewWidth)
    {
      col = UserInterface.ViewWidth - message.Length - 1;
      pointer = row > 3 ? '\\' : '/';
      pointerCol = screenCol - 1;
    }

    // This is a dorky way to do this, but doesn't require me writing new UI functions :P
    var ui = _gs.UIRef();
    ui.SqsOnScreen[row, pointerCol] = new Sqr(Colours.WHITE, Colours.BLACK, pointer);
    foreach (char ch in message)
    {
      ui.SqsOnScreen[row2, col++] = new Sqr(Colours.WHITE, Colours.BLACK, ch);
    }
  }

  public override void Update()
  {
    var loc = _actor.Loc;
    var playerLoc = _gs.Player.Loc;

    if (!_gs.LastPlayerFoV.Contains(loc))
      return;

    if (loc.DungeonID == _gs.CurrDungeonID && loc.Level == _gs.CurrLevel)
    {
      var ui = _gs.UIRef();
      var (scrR, scrC) = ui.LocToScrLoc(loc.Row, loc.Col, playerLoc.Row, playerLoc.Col);
      RenderLine(scrR, scrC, _bark);
      if (scrR >= 0 && scrR < UserInterface.ViewHeight && scrC >= 0 && scrC < UserInterface.ViewWidth)
      {
        RenderLine(scrR, scrC, _bark);
      }
    }
  }
}

// Simple animation to draw one character at a location. WIth a bit of work, I
// can probably merge HitAnimation into this, but HitAnimation actually 
// preceded this
class SqAnimation : Animation
{
  readonly GameState _gs;
  Loc _loc;
  Colour _fgColour;
  Colour _bgColour;
  char _ch;

  public SqAnimation(GameState gs, Loc loc, Colour fg, Colour bg, char ch)
  {
    _gs = gs;
    _loc = loc;
    _fgColour = fg;
    _bgColour = bg;
    _ch = ch;
    Expiry = DateTime.Now.AddMilliseconds(250);
  }

  public override void Update()
  {
    var ui = _gs.UIRef();
    var (scrR, scrC) = ui.LocToScrLoc(_loc.Row, _loc.Col, _gs.Player.Loc.Row, _gs.Player.Loc.Col);

    if (!_gs.LastPlayerFoV.Contains(_loc))
      return;

    if (scrR > 0 && scrR < ui.SqsOnScreen.GetLength(0) && scrC > 0 && scrC < ui.SqsOnScreen.GetLength(1))
    {
      Sqr sq = new Sqr(_fgColour, _bgColour, _ch);
      ui.SqsOnScreen[scrR, scrC] = sq;
    }
  }
}

record HighlightSqr(int Row, int Col, Sqr Sqr, Loc Loc, Glyph Glyph, DateTime Expiry);

class MagicMapAnimation(GameState gs, Dungeon dungeon, List<Loc> locs) : Animation
{
  readonly GameState _gs = gs;
  readonly Dungeon _dungeon = dungeon;
  readonly UserInterface _ui = gs.UIRef();
  readonly List<Loc> _locs = locs;
  int _index = 0;
  DateTime _lastFrame = DateTime.Now;
  readonly Queue<HighlightSqr> _sqsToMark = [];
  
  public override void Update()
  {    
    var dd = DateTime.Now - _lastFrame;
    if (dd.TotalMilliseconds < 15)
      return;

    int next = int.Min(_index + 25, _locs.Count);      
    while (_index < next)
    {
      var loc = _locs[_index];
      Tile tile = _gs.TileAt(loc);
      Glyph glyph = Util.TileToGlyph(tile);

      Sqr sqr = _gs.Rng.NextDouble() < 0.1 
                  ? new Sqr(glyph.Lit, Colours.LIGHT_PURPLE, glyph.Ch)
                  : new Sqr(glyph.Lit, Colours.FAINT_PINK, glyph.Ch);        
      var (scrR, scrC) = _ui.LocToScrLoc(loc.Row, loc.Col, _gs.Player.Loc.Row, _gs.Player.Loc.Col);
      _sqsToMark.Enqueue(new HighlightSqr(scrR, scrC, sqr, loc, glyph, DateTime.Now.AddMilliseconds(750)));

      ++_index;
    }

    while (_sqsToMark.Count > 0 && _sqsToMark.Peek().Expiry < DateTime.Now)
      _sqsToMark.Dequeue();

    foreach (var sq in _sqsToMark)
    {
      if (sq.Row >= 0 && sq.Col >= 0 && sq.Row < UserInterface.ViewHeight && sq.Col < UserInterface.ViewWidth)
        _ui.SqsOnScreen[sq.Row, sq.Col] = sq.Sqr;
      _dungeon.RememberedLocs.TryAdd(sq.Loc, sq.Glyph);
    }

    if (_index >= _locs.Count)
    {
      if (_sqsToMark.Count == 0)
        Expiry = DateTime.Now;
      return;
    }

    _lastFrame = DateTime.Now;
  }
}

class HitAnimation : Animation
{
  readonly GameState _gs;
  Loc _loc;
  Colour _colour;
  ulong _victimID;

  public HitAnimation(ulong victimID, GameState gs, Loc loc, Colour colour)
  {
    _gs = gs;
    _loc = loc;
    _colour = colour;
    _victimID = victimID;
    Expiry = DateTime.Now.AddMilliseconds(400);
  }

  public override void Update()
  {
    var occ = _gs.ObjDb.Occupant(_loc);
    if (occ is null || occ.ID != _victimID)
      return;

    var ui = _gs.UIRef();
    var (scrR, scrC) = ui.LocToScrLoc(_loc.Row, _loc.Col, _gs.Player.Loc.Row, _gs.Player.Loc.Col);

    if (scrR > 0 && scrR < ui.SqsOnScreen.GetLength(0) && scrC > 0 && scrC < ui.SqsOnScreen.GetLength(1))
    {
      Sqr sq = ui.SqsOnScreen[scrR, scrC] with { Fg = Colours.WHITE, Bg = _colour };
      ui.SqsOnScreen[scrR, scrC] = sq;
    }
  }
}

class TorchLightAnimationListener : Animation
{
  readonly UserInterface _ui;
  readonly GameState _gs;
  DateTime _lastFrame;
  List<(int, int)> _flickered = [];

  public TorchLightAnimationListener(UserInterface ui, GameState gs)
  {
    _ui = ui;
    _gs = gs;
    _lastFrame = DateTime.Now;
  }

  public override void Update()
  {
    if (_gs.InWilderness)
      return; // we're in the wilderness

    var dd = DateTime.Now - _lastFrame;

    if (dd.TotalMilliseconds > 500)
    {
      PickFlickeringSqs();
      _lastFrame = DateTime.Now;
    }

    SetFlickeringSqsToScreen();
  }

  private void PickFlickeringSqs()
  {
    int count = 0;
    _flickered = [];
    for (int r = 0; r < UserInterface.ViewHeight; r++)
    {
      for (int c = 0; c < UserInterface.ViewWidth; c++)
      {
        if (_ui.SqsOnScreen[r, c].Bg == Colours.TORCH_ORANGE && _gs.Rng.Next(20) == 0)
        {
          _flickered.Add((r, c));
          if (++count > 3)
            return;
        }
      }
    }
  }

  private void SetFlickeringSqsToScreen()
  {
    foreach (var (r, c) in _flickered)
    {
      if (_ui.SqsOnScreen[r, c].Ch == '.')
        _ui.SqsOnScreen[r, c] = new Sqr(Colours.YELLOW_ORANGE, Colours.TORCH_RED, '.');
      else if (_ui.SqsOnScreen[r, c].Ch == '#')
        _ui.SqsOnScreen[r, c] = new Sqr(Colours.TORCH_ORANGE, Colours.TORCH_RED, '#');
    }
  }
}

class CloudAnimationListener(UserInterface ui, GameState gs) : Animation
{
  static Sqr _cloudSqr = new Sqr(Colours.WHITE, Colours.BLACK, '#');
  bool[] _cloud = new bool[9];
  int _row;
  int _col;
  DateTime _lastFrame = DateTime.Now;
  DateTime _nextCloud;
  bool _paused;

  void MakeCloud()
  {
    int count = 0;
    List<int> locs = [0, 1, 2, 3, 4, 5, 6, 7, 8];
    locs.Shuffle(gs.Rng);

    foreach (var k in locs)
    {
      if (count < 7 && gs.Rng.NextDouble() < 0.7)
      {
        _cloud[k] = true;
        ++count;
      }
      else
      {
        _cloud[k] = false;
      }
    }

    if (gs.Rng.NextDouble() < 0.5)
    {
      _row = gs.Rng.Next(-3, (int)(0.33 * UserInterface.ViewHeight));
      _col = -3;
    }
    else
    {
      _row = -3;
      _col = gs.Rng.Next(-3, (int)(0.33 * UserInterface.ViewWidth));
    }

    _paused = false;
  }

  private void EraseCloud()
  {
    int h = UserInterface.ViewHeight;
    int w = UserInterface.ViewWidth;

    for (int r = 0; r < 3; r++)
    {
      for (int c = 0; c < 3; c++)
      {
        int cr = _row + r;
        int cc = _col + c;
        if (cr >= 0 && cr < h && cc >= 0 && cc < w)
          ui.ZLayer[cr, cc] = Constants.BLANK_SQ;
      }
    }
  }

  private void AnimationStep()
  {
    int h = UserInterface.ViewHeight;
    int w = UserInterface.ViewWidth;

    EraseCloud();

    // move and set the new spot
    ++_row;
    ++_col;

    for (int j = 0; j < 9; j++)
    {
      int cr = j / 3;
      int zrow = j / 3 + _row;
      int zcol = j - cr * 3 + _col;
      if (zrow < 0 || zrow >= h || zcol < 0 || zcol >= w)
        continue;
      if (_cloud[j])
        ui.ZLayer[zrow, zcol] = _cloudSqr;
      else
        ui.ZLayer[zrow, zcol] = Constants.BLANK_SQ;
    }
  }

  public override void Update()
  {
    var dd = DateTime.Now - _lastFrame;

    if (!_paused && !gs.InWilderness)
    {
      _paused = true;
      EraseCloud();
    }
    else if (_paused && gs.InWilderness && DateTime.Now > _nextCloud)
    {
      _paused = false;
      MakeCloud();
    }

    if (!_paused && dd.TotalMilliseconds >= 200)
    {
      AnimationStep();
      _lastFrame = DateTime.Now;

      // The cloud has drifted offscreen. We'll wait a little while before 
      // creating the next one.
      if (_row >= UserInterface.ViewHeight || _col >= UserInterface.ViewWidth)
      {
        _paused = true;
        _nextCloud = DateTime.Now.AddSeconds(gs.Rng.Next(5, 16));
      }
    }
  }
}