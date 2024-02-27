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

internal class Wilderness(Random rng)
{
    private Random _rng = rng;
    private int _length;

    private int Fuzz() => _rng.Next(-50, 51);

    void DrawARiver(Map map, (int, int) start)
    {
        int row = start.Item1;
        int col = start.Item2;
        var pts = new List<(int, int)>();
        
        do
        {
            int d = _rng.Next(2, 5);
            int columnBoop = _rng.Next(-5, 5);

            int nextRow = row - d;
            int nextCol = col + columnBoop;

            if (!map.InBounds(nextRow, nextCol))
                break;

            var nextSegment = Util.Bresenham(row, col, nextRow, nextCol);
            bool riverCrossing = false;
            foreach (var pt in nextSegment)
            {
                pts.Add(pt);
                if (map.TileAt(pt).Type == TileType.DeepWater || map.TileAt(pt).Type == TileType.Water)
                {                    
                    riverCrossing = true;
                }
            }

            if ((map.TileAt(nextRow, nextCol).Type == TileType.DeepWater || map.TileAt(nextRow, nextCol).Type == TileType.Water) && riverCrossing)
                break;

            row = nextRow;
            col = nextCol;

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

                map.SetTile(pts[j], TileFactory.Get(TileType.Water));                
            }
            map.SetTile(pts.Last(), TileFactory.Get(TileType.Water));

            foreach (var pt in extraPts)
                map.SetTile(pt, TileFactory.Get(TileType.Water));
        }
        while (true);
    }

    List<(int, int)> FindRiverStarts(Map map, int colLo, int colHi)
    {        
        List<(int, int)> candidates = [];
        int x = _length / 3;
        for (int r = _length - x; r < _length - 2; r++)
        {
            for (int c = colLo; c < colHi; c++)
            {
                int mountains = CountAdjType(map, r, c, TileType.Mountain);
                if (mountains > 3) 
                   candidates.Add((r, c));
            }
        }
        
        return candidates;
    }
    
    // Try to draw up to three rivers on the map
    void DrawRivers(Map map)
    {
        var opts = new List<int>() { 0, 1, 2 };
        opts.Shuffle(_rng);

        int third = _length / 3;

        foreach (int o in opts) 
        { 
            if (o == 0)
            {
                var startCandidates = FindRiverStarts(map, 2, third);
                if (startCandidates.Count > 0)
                {
                    var startLoc = startCandidates[_rng.Next(startCandidates.Count)];
                    DrawARiver(map, startLoc);
                }
            }
            else if (o == 1)
            {
                var startCandidates = FindRiverStarts(map, third, third * 2);
                if (startCandidates.Count > 0)
                {
                    var startLoc = startCandidates[_rng.Next(startCandidates.Count)];
                    DrawARiver(map, startLoc);
                }
            }
            else
            {
                var startCandidates = FindRiverStarts(map, third * 2, _length - 2);
                if (startCandidates.Count > 0)
                {
                    var startLoc = startCandidates[_rng.Next(startCandidates.Count)];
                    DrawARiver(map, startLoc);
                }
            }
        }        
    }

    static int CountAdjType(Map map, int r, int c, TileType type)
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
                else if (v < 165)
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

    static void Dump(Map map, int length, string filename)
    {
        using (TextWriter tw = new StreamWriter(filename)) 
        {
            for (int r = 0; r < length; r++)
            {
                for (int c = 0; c < length; c++)
                {
                    var t = map.TileAt(r, c);
                    char ch = t.Type switch
                    {
                        TileType.PermWall => '#',
                        TileType.DungeonWall => '#',
                        TileType.DungeonFloor or TileType.Sand => '.',
                        TileType.Door => '+',
                        TileType.Mountain or TileType.SnowPeak => '^',
                        TileType.Grass => ',',
                        TileType.Tree => 'T',
                        TileType.DeepWater or TileType.Water => '~',
                        _ => '!'
                    };

                    tw.Write(ch);                    
                }
                tw.WriteLine();
            }
        }        
    }
    
    static void SetBorderingWater(Map map, int length)
    {
        int center = length / 2;
        int radius = center - 1;

        for (int r = 0; r < length; r++)
        {
            for (int c = 0; c < length; c++)
            {
                if (Util.Distance(r, c, center, center) > radius)
                    map.SetTile(r, c, TileFactory.Get(TileType.DeepWater));
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

        // I want the outer perimeter to be deep water/ocean
        SetBorderingWater(map, length);
        Dump(map, length, "w2.txt");

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
        
        return map;
    }
}