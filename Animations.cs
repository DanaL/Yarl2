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
    public DateTime Expiry { get; set; }
    public abstract void Update();
}

class AimAnimation : Animation
{
    public Loc Target { get; set; }
    Loc _start;    
    readonly int _scrW;
    readonly int _scrH;
    readonly UserInterface _ui;

    public AimAnimation(UserInterface ui, Loc start)
    {
        _start = start;
        Target = start;
        Expiry = DateTime.MaxValue;
        _ui = ui;
        _scrW = ui.SqsOnScreen.GetLength(1);
        _scrH = ui.SqsOnScreen.GetLength(0);
    }
    
    public override void Update()
    {    
        foreach (var pt in Util.Bresenham(_start.Row, _start.Col, Target.Row, Target.Col))
        {
            var (scrR, scrC) = _ui.LocToScrLoc(pt.Item1, pt.Item2);
             if (scrR > 0 && scrR < _scrH && scrC > 0 && scrC < _scrW)
             {
                Sqr sq = _ui.SqsOnScreen[scrR, scrC] with { Fg = Colours.WHITE, Bg = Colours.HILITE };
                _ui.SqsOnScreen[scrR, scrC] = sq;
            }
        }
    }
}

class MissileAnimation : Animation 
{
    readonly UserInterface _ui;
    Glyph _glyph;
    List<Loc> _pts;
    int _frame;
    DateTime _lastFrame;
    Item _ammo;

    public MissileAnimation(UserInterface ui, Glyph glyph, List<Loc> pts, Item ammo)
    {
        _ui = ui;
        _glyph = glyph;
        _pts = pts;
        Expiry = DateTime.MaxValue;
        _frame = 0;
        _lastFrame = DateTime.Now;
        _ammo = ammo;
    }

    public override void Update()
    {
        var pt = _pts[_frame];
        var sq = new Sqr(_glyph.Lit, Colours.BLACK, _glyph.Ch);
        var (scrR, scrC) = _ui.LocToScrLoc(pt.Row, pt.Col);
        _ui.SqsOnScreen[scrR, scrC] = sq;
        
        if ((DateTime.Now - _lastFrame).TotalMilliseconds > 5)
        {
            _lastFrame = DateTime.Now;
            ++_frame;
        }

        if (_frame >= _pts.Count) 
        {
            Expiry = DateTime.MinValue;
            _ammo.Hidden = false;
        }
    }
}

class BarkAnimation : Animation
{    
    readonly UserInterface _ui;
    Actor _actor;
    string _bark;

    // Duration in milliseconds
    public BarkAnimation(UserInterface ui, int duration, Actor actor, string bark)
    {
        _ui = ui;
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
        _ui.SqsOnScreen[row, pointerCol] = new Sqr(Colours.WHITE, Colours.BLACK, pointer);        
        foreach (char ch in message) 
        {
            _ui.SqsOnScreen[row2, col++] = new Sqr(Colours.WHITE, Colours.BLACK, ch);
        }        
    }

    public override void Update()
    {        
        var gs = _ui.GameState;
        var loc = _actor.Loc;

        // praise me I remembered the Pythagorean theorem existed...
        int maxDistance = (int) Math.Sqrt(UserInterface.ViewHeight * UserInterface.ViewHeight + UserInterface.ViewWidth * UserInterface.ViewWidth);
        if (Util.Distance(loc, gs.Player.Loc) > maxDistance)
            return;
        if (!gs.LOSBetween(loc, gs.Player.Loc)) 
            return;

        if (loc.DungeonID == gs.CurrDungeon && loc.Level == gs.CurrLevel)
        {
            var (scrR, scrC) = _ui.LocToScrLoc(loc.Row, loc.Col);
            if (scrR >= 0 && scrR < UserInterface.ViewHeight && scrC >= 0 && scrC < UserInterface.ViewWidth)
            {
                RenderLine(scrR, scrC, _bark);
            }
        }        
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
        var occ = _gs.ObjDB.Occupant(_loc);
        if (occ is null || occ.ID != _victimID)
            return;

        var (scrR, scrC) = _gs.UI.LocToScrLoc(_loc.Row, _loc.Col);

        if (scrR > 0 && scrR < _gs.UI.SqsOnScreen.GetLength(0) && scrC > 0 && scrC < _gs.UI.SqsOnScreen.GetLength(1))
        {
            Sqr sq = _gs.UI.SqsOnScreen[scrR, scrC] with { Fg = Colours.WHITE, Bg = _colour };
            _gs.UI.SqsOnScreen[scrR, scrC] = sq;
        }
    }
}

class TorchLightAnimationListener : Animation
{
    Random _rng;
    readonly UserInterface _ui;
    DateTime _lastFrame;
    List<(int, int)> _flickered = [];

    public TorchLightAnimationListener(UserInterface ui, Random rng)
    {
        Expiry = DateTime.MaxValue;
        _ui = ui;
        _lastFrame = DateTime.Now;
        _rng = rng;
    }

    public override void Update()
    {
        if (_ui.CurrentDungeon == 0)
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
                if (_ui.SqsOnScreen[r, c].Bg == Colours.TORCH_ORANGE && _rng.Next(20) == 0)
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

class CloudAnimationListener : Animation
{
    readonly UserInterface _ui;
    bool[] _cloud = new bool[9];
    int _row;
    int _col;
    DateTime _lastFrame;
    DateTime _nextCloud;
    bool _paused;
    Random _rng;

    bool InWilderness => _ui.CurrentDungeon == 0 && _ui.CurrentLevel == 0;

    public CloudAnimationListener(UserInterface ui, Random rng)
    {
        Expiry = DateTime.MaxValue;
        _ui = ui;
        _lastFrame = DateTime.Now;
        _rng = rng;

        // Only make a cloud if we're in the wilderness
        if (InWilderness)
            MakeCloud();
    }

    void MakeCloud()
    {                
        int count = 0;
        List<int> locs = [ 0, 1, 2, 3, 4, 5, 6, 7, 8 ];
        locs.Shuffle(_rng);

        foreach (var k in locs) 
        {
            if (count < 7 && _rng.NextDouble() < 0.7)
            {
                _cloud[k] = true;
                ++ count;
            }
            else
            {
                _cloud[k] = false;
            }
        }

        if (_rng.NextDouble() < 0.5)
        {
            _row = _rng.Next(-3, (int) (0.33 * UserInterface.ViewHeight));
            _col = -3;
        }
        else
        {
            _row = -3;
            _col = _rng.Next(-3, (int) (0.33 * UserInterface.ViewWidth));
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
                    _ui.ZLayer[cr, cc] = TileFactory.Get(TileType.Unknown); 
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
            int cr = j/3;
            int zrow = j/3 + _row;
            int zcol = j - cr*3 + _col;
            if (zrow < 0 || zrow >= h || zcol < 0 || zcol >= w)
                continue;
            if (_cloud[j])
                    _ui.ZLayer[zrow, zcol] = TileFactory.Get(TileType.Cloud);
                else
                    _ui.ZLayer[zrow, zcol] = TileFactory.Get(TileType.Unknown);
        }
    }

    public override void Update()
    {
        var dd = DateTime.Now - _lastFrame;

        if (!_paused && !InWilderness)
        {
            _paused = true;
            EraseCloud();
        }
        else if (_paused && InWilderness && DateTime.Now > _nextCloud)
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
                _nextCloud = DateTime.Now.AddSeconds(_rng.Next(5, 16));                
            }
        }
    }
}