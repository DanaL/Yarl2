namespace Yarl2;

internal class Wilderness(Random rng)
{
    private Random _rng = rng;
    private int _length;

    private int Fuzz() => _rng.Next(-50, 51);

    (int, int) NextPoint(int r, int c, int d, double angle)
    {
        int nextR = (int)(r + (d * Math.Sin(angle)));
        int nextC = (int)(c + (d * Math.Cos(angle)));

        return (nextR, nextC);
    }

    void DrawARiver(Map map, (int, int) start, double angle)
    {
        int row = start.Item1;
        int col = start.Item2;
        var pts = new List<(int, int)>();
        double currentAngle = angle;

        do
        {
            int d = _rng.Next(2, 5);
            var n = NextPoint(row, col, d, currentAngle);

            if (!map.InBounds(n))
                break;

            var nextSegment = Util.Bresenham(row, col, n.Item1, n.Item2);
            bool riverCrossing = false;
            foreach (var pt in nextSegment)
            {
                pts.Add((pt.Item1, pt.Item2));
                if (map.TileAt(pt).Type == TileType.DeepWater)
                {
                    riverCrossing = true;
                }
            }

            if (map.TileAt(n).Type == TileType.DeepWater && riverCrossing)
                break;

            row = n.Item1; 
            col = n.Item2;
            double angleTweak = _rng.NextDouble() / 2 - 0.25;
            currentAngle += angleTweak;

            // keep the river from turning back and looking like it's flowing uphill into the mountains
            if (currentAngle > -0.1)
                currentAngle = -0.28;
            else if (currentAngle < -0.3)
                currentAngle = -2.6;

            // smooth river
            // bresenham draws lines that can look like:
            //     ~
            //   ~~
            //  ~@
            // I don't want those points where the player could walk
            // say NW and avoid stepping on the river
            List<(int, int)> extraPts = [];
            for (int j = 0; j < pts.Count - 1; j++)
            {
                var a = pts[j];
                var b = pts[j + 1];
                if (a.Item1 != b.Item1 && a.Item2 != b.Item2)
                    extraPts.Add((a.Item1 - 1, a.Item2));

                map.SetTile(pts[j], TileFactory.Get(TileType.DeepWater));
            }

            foreach (var pt in extraPts)
                map.SetTile(pt, TileFactory.Get(TileType.DeepWater));
        }
        while (true);
    }

    (int, int) RiverStart(Map map, int colLo, int colHi)
    {
        int x = _length / 3;

        do
        {
            int r = _rng.Next(_length - x, _length - 2);
            int c = _rng.Next(colLo, colHi);

            int mountains = CountAdjType(map, r, c, TileType.Mountain);
            if (mountains > 3)
                return (r, c);
        }
        while (true);
    }
    
    // Try to draw up to three rivers on the map
    void DrawRivers(Map map)
    {
        var opts = new List<int>() { 0, 1, 2 };
        opts.Shuffle(_rng);

        int passes = 0;
        foreach (int o in opts) 
        { 
            if (o == 0 && _rng.NextDouble() < 0.5)
            {
                var startLoc = RiverStart(map, 2, _length / 3);
                double angle = -0.28;
                DrawARiver(map, startLoc, angle);
            }
            else if (o == 1)
            {
                var startLoc = RiverStart(map, _length / 3, (_length / 3) * 2);
                double angle = -1.5;
                DrawARiver(map, startLoc, angle);
            }
            else
            {
                var startLoc = RiverStart(map, _length - _length / 3, _length - 2);
                double angle = -2.5;
                DrawARiver(map, startLoc, angle);
            }
            ++passes;
        }
        
    }

    int CountAdjType(Map map, int r, int c, TileType type)
    {
        int count = 0;

        foreach (var loc in Util.Adj8Sqs(r, c))
        {
            if (map.TileAt(loc).Type == type)
                ++count;
        }

        return count;
    }

    (int, int) CountAdjTreesAndGrass(Map map, int r, int c)
    {
        int tree = 0;
        int grass = 0;

        foreach (var loc in Util.Adj8Sqs(r, c)) 
        {
            var tt = map.TileAt(loc).Type;
            if (tt == TileType.Tree)
                ++tree;
            else if (tt == TileType.Grass)
                ++grass;
        }

        return (tree, grass);
    }

    Map CAizeTerrain(Map map)
    {
        var next = (Map)map.Clone();
        for (int r = 1; r < _length - 1; r++)
        {
            for (int c = 1; c < _length - 1; c++)
            {
                var (trees, _) = CountAdjTreesAndGrass(map, r, c);
                if (map.TileAt(r, c).Type == TileType.Grass && trees >= 5 && trees <= 8)
                    next.SetTile(r, c, TileFactory.Get(TileType.Tree));
                else if (map.TileAt(r, c).Type == TileType.Tree && trees < 4)
                    next.SetTile(r, c, TileFactory.Get(TileType.Grass));
            }
        }

        return next;
    }

    // Run a sort of cellular automata ule over the trees
    // and grass to clump them together.
    // Two generations seems to make a nice mix .
    Map TweakTreesAndGrass(Map map)
    {              
        map = CAizeTerrain(map);
        map = CAizeTerrain(map);

        return map;
    }

    // Average each point with its neighbours to smooth things out
    void SmoothGrid(int[,] grid)
    {
        for (int r = 0; r < _length; r++) 
        {
            for (int c = 0; c < _length; c++) 
            {
                int avg = grid[r , + c];
                int count = 1;

                if (r >= 1) 
                {
                    if (c >= 1) 
                    {
                        avg += grid[(r - 1), + c - 1];
                        count += 1;
                    }
                    avg += grid[(r - 1), + c];
                    count += 1;
                    if (c + 1 < _length) 
                    {
                        avg += grid[(r - 1), + c + 1];
                        count += 1;
                    }
                }

                if (r > 1 && c >= 1) 
                {
                    avg += grid[(r - 1), c - 1];
                    count += 1;
                }

                if (r > 1 && c + 1 < _length) 
                {
                    avg += grid[(r - 1), c + 1];
                    count += 1;
                }

                if (r > 1 && r + 1 < _length) 
                {
                    if (c >= 1) 
                    {
                        avg += grid[(r - 1), c - 1];
                        count += 1;
                    }
                    avg += grid[(r - 1), c];
                    count += 1;
                    if (c + 1 < _length) 
                    {
                        avg += grid[(r - 1) , c + 1];
                        count += 1;
                    }
                }

                grid[r, c] = avg / count;
            }
        }
    }
    void DiamondStep(int[,] grid, int r, int c, int width)
    {
        int avg = (grid[r, c] 
                        + grid[r, c + width - 1]
                        + grid[r + width - 1, c] 
                        + grid[(r + width - 1), c + width - 1]) / 4;

	
        var f = Fuzz();
	    grid[r + width / 2, + c + width / 2] = avg + f;
    }

    void DiamondAverage(int[,] grid, int r, int c, int width)
    {
        int count = 0;
        double avg = 0.0;

        if (width <= c)
        {
            avg += grid[r, + c - width];
            count += 1;
        }
        if (c + width < _length)
        {
            avg += grid[r, c + width];
            count += 1;
        }
        if (width <= r)
        {
            avg += grid[(r - width), + c];
            count += 1;
        }
        if (r + width < _length)
        {
            avg += grid[r + width, c];
            count += 1;
        }

        grid[r, c] = (int)(avg / count) + Fuzz();
    }

    void SquareStep(int[,] grid, int r, int c, int width)
    {
        var halfWidth = width / 2;

        DiamondAverage(grid, r - halfWidth, c, halfWidth);
        DiamondAverage(grid, r + halfWidth, c, halfWidth);
        DiamondAverage(grid, r, c - halfWidth, halfWidth);
        DiamondAverage(grid, r, c + halfWidth, halfWidth);
    }

    void MidpointDisplacement(int[,] grid, int r, int c, int width)
    {
        DiamondStep(grid, r, c, width);
        var halfWidth = width / 2;
        SquareStep(grid, r + halfWidth, c + halfWidth, width);

        if (halfWidth == 1)
            return;

        MidpointDisplacement(grid, r, c, halfWidth + 1);
	    MidpointDisplacement(grid, r, c + halfWidth, halfWidth + 1);
	    MidpointDisplacement(grid, r + halfWidth, c, halfWidth + 1);
	    MidpointDisplacement(grid, r + halfWidth, c + halfWidth, halfWidth + 1);
    }

    Map ToMap(int[,] grid)
    {
        var map = new Map(_length, _length);

        for (int r = 0; r < _length; r++)
        {
            for (int c = 0; c < _length; c++)
            {
                var v = grid[r, c];
                TileType tt;
                if (v < 25)
                {
                    tt = TileType.DeepWater;
                }
                else if (v < 40)
                {
                    tt = TileType.Sand;
                }
                else if (v < 160)
                {
                    if (v % 2 == 0)
                        tt = TileType.Grass;
                    else
                        tt = TileType.Tree;
                }
                else if (_rng.NextDouble() < 0.9)
                {
                    tt = TileType.Mountain;
                }
                else
                {
                    tt = TileType.Mountain;
                }

                map.SetTile(r, c, TileFactory.Get(tt));                
            }
        }
        return map;
    }

    private void Dump(Map map, string filename)
    {
        using (TextWriter tw = new StreamWriter(filename)) 
        {
            for (int r = 0; r < _length; r++)
            {
                for (int c = 0; c < _length; c++)
                {
                    var t = map.TileAt(r, c);
                    char ch = t.Type switch
                    {
                        TileType.PermWall => '#',
                        TileType.Wall => '#',
                        TileType.Floor or TileType.Sand => '.',
                        TileType.Door => '+',
                        TileType.Mountain or TileType.SnowPeak => '^',
                        TileType.Grass => ',',
                        TileType.Tree => 'T',
                        TileType.DeepWater => '~',
                        _ => ' '
                    };

                    tw.Write(ch);                    
                }
                tw.WriteLine();
            }
        }        
    }
    
    public Map DrawLevel(int length)
    {
        _length = length;
        int[,] grid = new int[length, length];

        if (_rng.NextDouble() < 0.5)
        {
            grid[0, 0] = _rng.Next(-10, 25);
            grid[0, length - 1] = _rng.Next(0, 100);
        }
        else
        {
            grid[0, length - 1] = _rng.Next(-10, 25);
            grid[0, 0] = _rng.Next(0, 100);
        }        
        grid[length - 1, 0] = _rng.Next(250, 300);
        grid[length - 1, length - 1] = _rng.Next(200, 350);

        MidpointDisplacement(grid, 0, 0, length);
        SmoothGrid(grid);
        
        var map = ToMap(grid);
        map = TweakTreesAndGrass(map);

        DrawRivers(map);

        // set the border around the world
        for (int c = 0; c < length; c++)
        {
            map.SetTile(0, c, TileFactory.Get(TileType.WorldBorder));
            map.SetTile(length - 1, c, TileFactory.Get(TileType.WorldBorder));
        }
        for (int r = 1; r < length - 1; r++)
        {
            map.SetTile(r, 0, TileFactory.Get(TileType.WorldBorder));
            map.SetTile(r, length - 1, TileFactory.Get(TileType.WorldBorder));
        }

        Dump(map, "out.txt");

        return map;
    }
}