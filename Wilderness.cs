namespace Yarl2;

internal class Wilderness(Random rng)
{
    private Random _rng = rng;

    private double Fuzz() => _rng.NextDouble() - 0.5;

    void DiamondStep(double[] grid, int r, int c, int length, int width)
    {
        var avg = (grid[length * r + c] 
                        + grid[length * r + c + width - 1]
                        + grid[(r + width - 1) * length  + c] 
                        + grid[(r + width - 1) * length + c + width - 1]) / 4.0;

	
        var f = Fuzz();
	    grid[(r + width / 2) * length + c + width / 2] = avg + f;
    }

    void DiamondAverage(double[] grid, int r, int c, int width, int length)
    {
        int count = 0;
        double avg = 0.0;

        if (width <= c)
        {
            avg += grid[r * length + c - width];
            count += 1;
        }
        if (c + width < length)
        {
            avg += grid[r * length + c + width];
            count += 1;
        }
        if (width <= r)
        {
            avg += grid[(r - width) * length + c];
            count += 1;
        }
        if (r + width < length)
        {
            avg += grid[(r + width) * length + c];
            count += 1;
        }

        grid[r * length + c] = avg / count + Fuzz();
    }

    void SquareStep(double[] grid, int r, int c, int width, int length)
    {
        var halfWidth = width / 2;

        DiamondAverage(grid, r - halfWidth, c, halfWidth, length);
        DiamondAverage(grid, r + halfWidth, c, halfWidth, length);
        DiamondAverage(grid, r, c - halfWidth, halfWidth, length);
        DiamondAverage(grid, r, c + halfWidth, halfWidth, length);
    }

    void MidpointDisplacement(double[] grid, int r, int c, int width, int length)
    {
        DiamondStep(grid, r, c, width, length);
        var halfWidth = width / 2;
        SquareStep(grid, r + halfWidth, c + halfWidth, width, length);

        if (halfWidth == 1)
            return;

        MidpointDisplacement(grid, r, c, halfWidth + 1, length);
	    MidpointDisplacement(grid, r, c + halfWidth, halfWidth + 1, length);
	    MidpointDisplacement(grid, r + halfWidth, c, halfWidth + 1, length);
	    MidpointDisplacement(grid, r + halfWidth, c + halfWidth, halfWidth + 1, length);
    }

    Map ToMap(double[] grid, int length)
    {
        var map = new Map(length, length);

        for (int r = 0; r < length; r++)
        {
            for (int c = 0; c < length; c++)
            {
                if (grid[r * length + c] < 1.5)
                    map.SetTile(r, c, TileFactory.Get(TileType.DeepWater));
                else if (grid[r * length + c] < 6.0)
                    map.SetTile(r, c, TileFactory.Get(TileType.Grass));
                else 
                {
                    var tt = _rng.NextDouble() < 0.9 ? TileType.Mountain : TileType.SnowPeak;
                    map.SetTile(r, c, TileFactory.Get(tt));
                }
            }
        }
        return map;
    }

    public Map DrawLevel(int length)
    {
        double[] grid = new double[length * length];
        grid[0] = _rng.NextDouble() * 2.0 - 1.0;
        grid[length - 1] = _rng.NextDouble() * 1.5 + 1.0;
        grid[(length - 1) * length] = _rng.NextDouble() * 2.0 + 10.0;
        grid[length * length - 1] = _rng.NextDouble() * 2.0 + 9.0;

        Console.WriteLine(grid[0]);
        Console.WriteLine(grid[length - 1]);
        Console.WriteLine(grid[(length - 1) * length]);
        Console.WriteLine(grid[length * length - 1]);

        MidpointDisplacement(grid, 0, 0, length, length);

        var map = ToMap(grid, length);
        map.Dump();

        return map;
    }
}