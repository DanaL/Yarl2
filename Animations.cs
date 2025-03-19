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

class AimAnimation(UserInterface ui, GameState gs, Loc origin, Loc initialTarget) : Animation
{
  public Loc Target { get; set; } = initialTarget;
  Loc _start = origin;
  readonly int _scrW = UserInterface.ViewWidth;
  readonly int _scrH = UserInterface.ViewHeight;
  readonly UserInterface _ui = ui;
  readonly GameState _gs = gs;

  public override void Update()
  {
    if (Expiry < DateTime.UtcNow)
      return;

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
  readonly List<(Loc, char)> _frames = [];
  int _frame = 0;
  Colour _ammoColour;
  DateTime _lastFrame;

  public ArrowAnimation(GameState gs, List<Loc> pts, Colour ammoColour)
  {
    _gs = gs;
    _ammoColour = ammoColour;

    for (int j = 0; j < pts.Count - 1; j++)
    {
      _frames.Add((pts[j + 1], Util.ArrowChar(pts[j], pts[j + 1])));
    }
  }

  public override void Update()
  {
    if (_frame >= _frames.Count)
    {
      Expiry = DateTime.MinValue;
      return;
    }

    var ui = _gs.UIRef();
    var (loc, ch) = _frames[_frame];
    var sq = new Sqr(_ammoColour, Colours.BLACK, ch);
    var (scrR, scrC) = ui.LocToScrLoc(loc.Row, loc.Col, _gs.Player.Loc.Row, _gs.Player.Loc.Col);
    ui.SqsOnScreen[scrR, scrC] = sq;

    if ((DateTime.UtcNow - _lastFrame).TotalMicroseconds > 150)
    {
      _lastFrame = DateTime.UtcNow;
      ++_frame;
    }
  }
}

class ConeAnimation(UserInterface ui, GameState gs, Loc origin, Loc target, int range, HashSet<DamageType> damageTypes) : Animation
{
  UserInterface UI { get; set; } = ui;
  GameState GS { get; set; } = gs;
  Loc Origin { get; set; } = origin;
  public Loc Target { get; set; } = target;
  int Range { get; set; } = range;
  HashSet<DamageType> DamageTypes { get; set; } = damageTypes;

  public override void Update()
  {
    if (Expiry < DateTime.UtcNow)
      return;

    Map map = GS.Campaign.Dungeons[Origin.DungeonID].LevelMaps[Origin.Level];
    List<Loc> included = ConeCalculator.Affected(Range, Origin, Target, map, GS.ObjDb, DamageTypes);

    foreach (Loc loc in included)
    {
      var (scrR, scrC) = UI.LocToScrLoc(loc.Row, loc.Col, GS.Player.Loc.Row, GS.Player.Loc.Col);
      if (scrR > 0 && scrR < UserInterface.ScreenHeight && scrC > 0 && scrC < UserInterface.ScreenWidth)
      {
        Sqr sq = UI.SqsOnScreen[scrR, scrC] with { Fg = Colours.WHITE, Bg = Colours.HILITE };
        UI.SqsOnScreen[scrR, scrC] = sq;
      }
    }
  }
}

class BeamAnimation(GameState gs, List<Loc> pts, Colour background, Colour foreground) : Animation
{
  readonly GameState _gs = gs;
  readonly List<Loc> _pts = pts;
  int _end = 0;
  DateTime _lastFrame;
  Colour _background = background;
  Colour _foreground = foreground;

  public override void Update()
  {
    if (_end >= _pts.Count)
    {
      Expiry = DateTime.MinValue;
      return;
    }

    var ui = _gs.UIRef();
    for (int p = 0; p < _end; p++)
    {
      var loc = _pts[p];
      var (scrR, scrC) = ui.LocToScrLoc(loc.Row, loc.Col, _gs.Player.Loc.Row, _gs.Player.Loc.Col);
      char ch = ui.SqsOnScreen[scrR, scrC].Ch;
      ui.SqsOnScreen[scrR, scrC] = new Sqr(_foreground, _background, ch);
    }

    if ((DateTime.UtcNow - _lastFrame).TotalMicroseconds > 150)
    {
      _lastFrame = DateTime.UtcNow;
      ++_end;
    }
  }
}

class ExplosionAnimation(GameState gs) : Animation
{
  public Colour MainColour { get; set; }
  public Colour AltColour1 { get; set; }
  public Colour AltColour2 {  get; set; }
  public Colour Highlight { get; set; }
  public Loc Centre {  get; set; }
  public char Ch { get; set; } = '*';
  public HashSet<Loc> Sqs { get; set; } = [];
  readonly Dictionary<Loc, Sqr> _toDraw = [];
  int _radius = 0;
  DateTime _lastFrame = DateTime.UtcNow;
  readonly GameState _gs = gs;
  bool _finalFrame = false;
  readonly double _acceleration = 0.75;
  double _currentDelay = 5.0;

  public override void Update()
  {
    UserInterface ui = _gs.UIRef();

    if ((DateTime.UtcNow - _lastFrame).TotalMilliseconds > _currentDelay)
    {
      List<Loc> newSqs = [.. Sqs.Where(s => Util.Distance(s, Centre) == _radius)];
      if (newSqs.Count == 0 && !_finalFrame)
      {

        Expiry = DateTime.UtcNow.AddMilliseconds(50);
        _finalFrame = true;
      }
      else
      {
        foreach (Loc loc in newSqs)
        {
          double roll = _gs.Rng.NextDouble();
          Colour colour;
          if (roll < 0.8)
            colour = MainColour;
          else if (roll < 0.9)
            colour = AltColour1;
          else
            colour = AltColour2;
          _toDraw.Add(loc, new Sqr(Highlight, colour, Ch));
        }

        ++_radius;       
      }
    }

    foreach (var pt in _toDraw.Keys)
    {
      double roll = _gs.Rng.NextDouble();
      
      var (scrR, scrC) = ui.LocToScrLoc(pt.Row, pt.Col, _gs.Player.Loc.Row, _gs.Player.Loc.Col);      
      if (scrR < 0 || scrR >= UserInterface.ViewHeight || scrC < 0 || scrC >= UserInterface.ViewWidth)
        continue;

      ui.SqsOnScreen[scrR, scrC] = _toDraw[pt];
    }

    _currentDelay *= _acceleration;
    _lastFrame = DateTime.UtcNow;
  }
}

class ThrownMissileAnimation(GameState gs, Glyph glyph, List<Loc> pts, Item ammo) : Animation
{
  readonly GameState _gs = gs;
  Glyph _glyph = glyph;
  List<Loc> _pts = pts;
  int _frame = 0;
  DateTime _lastFrame = DateTime.UtcNow;
  Item _ammo = ammo;

  public override void Update()
  {
    var pt = _pts[_frame];
    var sq = new Sqr(_glyph.Lit, Colours.BLACK, _glyph.Ch);
    var ui = _gs.UIRef();
    var (scrR, scrC) = ui.LocToScrLoc(pt.Row, pt.Col, _gs.Player.Loc.Row, _gs.Player.Loc.Col);
    ui.SqsOnScreen[scrR, scrC] = sq;

    if ((DateTime.UtcNow - _lastFrame).TotalMicroseconds > 250)
    {
      _lastFrame = DateTime.UtcNow;
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
      }
    }
  }
}

class BarkAnimation : Animation
{
  readonly GameState _gs;
  readonly Actor _actor;
  readonly string _bark;
  readonly UserInterface _ui;

  // Duration in milliseconds
  public BarkAnimation(GameState gs, int duration, Actor actor, string bark)
  {
    _gs = gs;
    Expiry = DateTime.UtcNow.AddMilliseconds(duration);
    _actor = actor;
    _bark = bark;
    _ui = gs.UIRef();
  }

  // Need to actually calculate where to place the bark if the
  // speaker is near an edge of the screen/
  // screenRow and screenCol are the speaker's row/col
  void RenderLine(int screenRow, int screenCol, string message)
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

    SetSqr(row, pointerCol, new Sqr(Colours.WHITE, Colours.BLACK, pointer));
    foreach (char ch in message)
    {
      SetSqr(row2, col++, new Sqr(Colours.WHITE, Colours.BLACK, ch));      
    }

    void SetSqr(int row, int col, Sqr sqr)
    {
      if (row < 0 || row >= UserInterface.ViewHeight || col < 0 || col >= UserInterface.ViewWidth)
        return;

      // If a loc is occupied, don't hide the Actor glyph
      Loc playerLoc = _gs.Player.Loc;
      var (mapRow, mapCol) = _ui.ScrLocToGameLoc(row, col, playerLoc.Row, playerLoc.Col);
      Loc mapLoc = playerLoc with { Row = mapRow, Col = mapCol };

      if (!_gs.ObjDb.Occupied(mapLoc) || !_gs.LastPlayerFoV.Contains(mapLoc))
        _ui.SqsOnScreen[row, col] = sqr;
    }
  }

  public override void Update()
  {
    Loc loc = _actor.Loc;
    Loc playerLoc = _gs.Player.Loc;

    // I thought about having part of the message appear on screen even if the
    // speaker is off screen, but didn't want to deal with the extra 
    // complication
    if (!_gs.LastPlayerFoV.Contains(loc))
      return;

    if (loc.DungeonID == _gs.CurrDungeonID && loc.Level == _gs.CurrLevel)
    {
      var ui = _gs.UIRef();
      var (scrR, scrC) = ui.LocToScrLoc(loc.Row, loc.Col, playerLoc.Row, playerLoc.Col);
      if (scrR < 0 || scrR >= UserInterface.ViewHeight || scrC < 0 || scrC >= UserInterface.ViewWidth)
        return;

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
  public bool IgnoreFoV { get; set; } = false;
  readonly GameState _gs;
  Loc _loc;
  Colour _fgColour;
  Colour _bgColour;
  readonly char _ch;

  public SqAnimation(GameState gs, Loc loc, Colour fg, Colour bg, char ch)
  {
    _gs = gs;
    _loc = loc;
    _fgColour = fg;
    _bgColour = bg;
    _ch = ch;
    Expiry = DateTime.UtcNow.AddMilliseconds(250);
  }

  public override void Update()
  {
    var ui = _gs.UIRef();
    var (scrR, scrC) = ui.LocToScrLoc(_loc.Row, _loc.Col, _gs.Player.Loc.Row, _gs.Player.Loc.Col);

    if (!_gs.LastPlayerFoV.Contains(_loc))
      return;

    if (scrR > 0 && scrR < ui.SqsOnScreen.GetLength(0) && scrC > 0 && scrC < ui.SqsOnScreen.GetLength(1))
    {
      Sqr sq = new(_fgColour, _bgColour, _ch);
      ui.SqsOnScreen[scrR, scrC] = sq;
    }
  }
}

record HighlightSqr(int Row, int Col, Sqr Sqr, Loc Loc, Glyph Glyph, DateTime Expiry);

class MagicMapAnimation(GameState gs, Dungeon dungeon, List<Loc> locs, bool tilesOnly = true) : Animation
{
  public Colour Colour { get; set; } = Colours.FAINT_PINK;
  public Colour AltColour { get; set; } = Colours.LIGHT_PURPLE;
  bool TilesOnly { get; set; } = tilesOnly;
  readonly GameState _gs = gs;
  readonly Dungeon _dungeon = dungeon;
  readonly UserInterface _ui = gs.UIRef();
  readonly List<Loc> _locs = locs;
  int _index = 0;
  DateTime _lastFrame = DateTime.UtcNow;
  readonly Queue<HighlightSqr> _sqsToMark = [];
  const double _delay = 250;

  public override void Update()
  {    
    var dd = DateTime.UtcNow - _lastFrame;
    if (dd.TotalMilliseconds < 15)
      return;

    int next = int.Min(_index + 25, _locs.Count);      
    while (_index < next)
    {
      Loc loc = _locs[_index];
      Tile tile = _gs.TileAt(loc);
      Glyph glyph = Util.TileToGlyph(tile);
      char ch = glyph.Ch;
      if (!TilesOnly)
      {
        Glyph g = _gs.ObjDb.GlyphAt(loc);
        if (g != GameObjectDB.EMPTY)
          ch = g.Ch;
      }
      
      Sqr sqr = _gs.Rng.NextDouble() < 0.1 
                  ? new Sqr(glyph.Lit, AltColour, ch)
                  : new Sqr(glyph.Lit, Colour, ch);
      
      var (scrR, scrC) = _ui.LocToScrLoc(loc.Row, loc.Col, _gs.Player.Loc.Row, _gs.Player.Loc.Col);
      _sqsToMark.Enqueue(new HighlightSqr(scrR, scrC, sqr, loc, glyph, DateTime.UtcNow.AddMilliseconds(_delay)));

      ++_index;
    }

    while (_sqsToMark.Count > 0 && _sqsToMark.Peek().Expiry < DateTime.UtcNow)
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
        Expiry = DateTime.UtcNow;
      return;
    }

    _lastFrame = DateTime.UtcNow;
  }
}

class HitAnimation : Animation
{
  readonly GameState _gs;
  Colour _colour;
  readonly ulong _victimID;

  public HitAnimation(ulong victimID, GameState gs, Colour colour)
  {
    _gs = gs;
    _colour = colour;
    _victimID = victimID;
    Expiry = DateTime.UtcNow.AddMilliseconds(400);
  }

  public override void Update()
  {
    if (_gs.ObjDb.GetObj(_victimID) is Actor victim)
    {
      var ui = _gs.UIRef();
      var (scrR, scrC) = ui.LocToScrLoc(victim.Loc.Row, victim.Loc.Col, _gs.Player.Loc.Row, _gs.Player.Loc.Col);

      if (scrR >= 0 && scrR < ui.SqsOnScreen.GetLength(0) && scrC >= 0 && scrC < ui.SqsOnScreen.GetLength(1))
      {
        Sqr sq = ui.SqsOnScreen[scrR, scrC] with { Fg = Colours.WHITE, Bg = _colour };
        ui.SqsOnScreen[scrR, scrC] = sq;
      }
    }
  }
}

// This is also pretty close to SqAnimation...
class PolearmAnimation : Animation
{
  readonly GameState _gs;
  Colour _colour;
  Loc _origin;
  Loc _target;

  public PolearmAnimation(GameState gs, Colour colour, Loc origin, Loc target) 
  { 
    _gs = gs;
    _colour = colour;
    _origin = origin;
    _target = target;
    Expiry = DateTime.UtcNow.AddMilliseconds(400);
  }

  public override void Update()
  {
    var ui = _gs.UIRef();
    Sqr sq = new(_colour, Colours.BLACK, Util.ArrowChar(_origin, _target));
    int dr = (_target.Row - _origin.Row) / 2;
    int dc = (_target.Col - _origin.Col) / 2;
    var (scrR, scrC) = ui.LocToScrLoc(_origin.Row + dr, _origin.Col + dc, _gs.Player.Loc.Row, _gs.Player.Loc.Col);
    ui.SqsOnScreen[scrR, scrC] = sq;
  }
}

class CloudAnimation(UserInterface ui, GameState gs) : Animation
{
  readonly UserInterface _ui = ui;
  readonly GameState _gs = gs;
  static Sqr _cloudSqr = new(Colours.WHITE, Colours.BLACK, '#');
  bool[] _cloud = new bool[9];
  int _row;
  int _col;
  DateTime _lastFrame = DateTime.UtcNow;
  DateTime _nextCloud;
  bool _cloudFinished = true;

  void MakeCloud()
  {
    int count = 0;
    List<int> locs = [0, 1, 2, 3, 4, 5, 6, 7, 8];
    locs.Shuffle(_gs.Rng);

    foreach (var k in locs)
    {
      if (count < 7 && _gs.Rng.NextDouble() < 0.7)
      {
        _cloud[k] = true;
        ++count;
      }
      else
      {
        _cloud[k] = false;
      }
    }

    int height = UserInterface.ViewHeight / 2;
    int width = UserInterface.ViewWidth / 2;
    int screenRow = _gs.Player.Loc.Row;
    int screenCol = _gs.Player.Loc.Col;

    if (_gs.Rng.NextDouble() < 0.5)
    {
      _row = _gs.Rng.Next(screenRow - height - 1, screenRow + 3);
      _col =  screenCol - width - 1;
    }
    else
    {
      _row = screenRow - height - 1;
      _col = _gs.Rng.Next(screenCol - width - 1, screenCol + 3);
    }

    _cloudFinished = false;
  }

  private void DrawCloud()
  {
    for (int r = 0; r < 3; r++)
    {
      for (int c = 0; c < 3; c++)
      {
        int j = r * 3 + c;
        if (!_cloud[j])
          continue;

        int cloudRow = _row + r;
        int cloudCol = _col + c;
        
        if (!_gs.LastPlayerFoV.Contains(new Loc(0, 0, cloudRow, cloudCol)))
          continue;

        var (scrR, scrC) = _ui.LocToScrLoc(cloudRow, cloudCol, _gs.Player.Loc.Row, _gs.Player.Loc.Col);
        if (scrR >= 0 && scrR < UserInterface.ViewHeight && scrC >= 0 && scrC < UserInterface.ViewWidth)
        {
          _ui.SqsOnScreen[scrR, scrC] =_cloudSqr;
        }
      }
    }
  }

  public override void Update()
  {
    TimeSpan dd = DateTime.UtcNow - _lastFrame;

    if (_cloudFinished && _gs.InWilderness && DateTime.UtcNow > _nextCloud)
    {
      MakeCloud();
    }

    DrawCloud();

    if (!_cloudFinished && dd.TotalMilliseconds >= 200)
    {
      ++_row;
      ++_col;
      _lastFrame = DateTime.UtcNow;

      if (_row >= _gs.Wilderness.Height || _col >= _gs.Wilderness.Width)
      {
        _cloudFinished = true;
        _nextCloud = DateTime.UtcNow.AddSeconds(_gs.Rng.Next(5, 15));
      }
    }
  }
}