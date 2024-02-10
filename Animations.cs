namespace Yarl2;

internal interface IAnimationListener
{
    void Update();
}

internal class CloudAnimationListener : IAnimationListener
{
    readonly UserInterface _ui;
    bool[] _cloud = new bool[9];
    int _row;
    int _col;
    DateTime _lastFrame;
    DateTime _nextCloud;
    bool _paused;

    bool InWilderness => _ui.CurrentDungeon == 0 && _ui.CurrentLevel == 0;

    public CloudAnimationListener(UserInterface ui)
    {
        _ui = ui;
        _lastFrame = DateTime.Now;

        // Only make a cloud if we're in the wilderness
        if (InWilderness)
            MakeCloud();
    }

    void MakeCloud()
    {                
        var rng = new Random();
        int count = 0;
        List<int> locs = [ 0, 1, 2, 3, 4, 5, 6, 7, 8 ];
        locs.Shuffle(rng);

        foreach (var k in locs) 
        {
            if (count < 7 && rng.NextDouble() < 0.7)
            {
                _cloud[k] = true;
                ++ count;
            }
            else
            {
                _cloud[k] = false;
            }
        }

        if (rng.NextDouble() < 0.5)
        {
            _row = rng.Next(-3, (int) (0.33 * UserInterface.ScreenHeight));
            _col = -3;
        }
        else
        {
            _row = -3;
            _col = rng.Next(-3, (int) (0.33 * UserInterface.ViewWidth));
        }

        _paused = false;
    }

    private void EraseCloud()
    {
        int h = UserInterface.ScreenHeight - 1;
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
        int h = UserInterface.ScreenHeight - 1;
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
            if (_row >= UserInterface.ScreenHeight - 1 || _col >= UserInterface.ViewWidth) 
            {
                var rng = new Random();
                _paused = true;
                _nextCloud = DateTime.Now.AddSeconds(rng.Next(5, 16));                
            }
        }
    }
}

internal class WaterAnimationListener : IAnimationListener
{
    DateTime _lastFrame;
    UserInterface _ui;
    HashSet<(int, int)> _sparkles = [];

    public WaterAnimationListener(UserInterface ui)
    {
        _ui = ui;
        _lastFrame = DateTime.Now;
        
        SetSparkles();
    }

    void SetSparkles()
    {
        _sparkles = [];
        var rng = new Random();        
        for (int r = 0; r < UserInterface.ScreenHeight - 1; r++) 
        {
            for (int c = 0; c < UserInterface.ViewWidth; c++)
            {
                if (rng.NextDouble() < 0.05)
                    _sparkles.Add((r, c));
                if (_sparkles.Count >= 10)
                    return;
            }            
        }
    }
    
    public void Update() 
    {
        foreach (var sq in _sparkles)
        {
            var t = _ui.SqsOnScreen[sq.Item1, sq.Item2];
            if (t.Item2 == '}')
            {
                _ui.SqsOnScreen[sq.Item1, sq.Item2] = (_ui.LIGHT_BLUE, '~');
            }
        }

        var dd = DateTime.Now - _lastFrame;
        if (dd.TotalMilliseconds >= 150)
        {
            _lastFrame = DateTime.Now;
            SetSparkles();
        }    
    }
}