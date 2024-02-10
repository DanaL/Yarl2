namespace Yarl2;

internal interface IAnimationListener
{
    void Update();
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