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

interface IAnimationListener
{
    void Update();
}

class HitAnimation(UserInterface ui) : IAnimationListener
{
    readonly UserInterface _ui = ui;
    Dictionary<Loc, (DateTime TS, Colour C)> _hits = [];

    public void Add(Loc loc, Colour colour)
    {
        // We have only one colour animation on a square at a time
        _hits[loc] = (DateTime.Now, colour);
    }

    public void Update()
    {
        foreach (var loc in _hits.Keys)
        {
            var occ = _ui.GameState.ObjDB.Occupant(loc);
            var dateDiff = (DateTime.Now - _hits[loc].TS).TotalMilliseconds;
            if (dateDiff > 400)
            {
                _hits.Remove(loc);
            }
            else if (occ is not null)
            {
                var (scrR, scrC) = _ui.LocToScrLoc(loc.Row, loc.Col);

                if (scrR > 0 && scrR < _ui.SqsOnScreen.GetLength(0) && scrC > 0 && scrC < _ui.SqsOnScreen.GetLength(1))
                {
                    Sqr sq = _ui.SqsOnScreen[scrR, scrC] with { Fg = Colours.WHITE, Bg = _hits[loc].C};
                    _ui.SqsOnScreen[scrR, scrC] = sq;
                
                }                
            }
        }
    }
}

class TorchLightAnimationListener : IAnimationListener
{
    Random _rng;
    readonly UserInterface _ui;
    DateTime _lastFrame;
    List<(int, int)> _flickered = [];

    public TorchLightAnimationListener(UserInterface ui, Random rng)
    {
        _ui = ui;
        _lastFrame = DateTime.Now;
        _rng = rng;
    }

    public void Update()
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

class CloudAnimationListener : IAnimationListener
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

    public void Update()
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