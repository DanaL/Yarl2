
namespace Yarl2;

class Util
{
    public static (int, int)[] Adj4 = [ (-1, 0), (1, 0), (0, 1), (0, -1)];
    public static (int, int)[] Adj8= [ (-1, -1), (-1, 0), (-1, 1),
                                        (0, -1), (0, 1),
                                        (1, -1), (1, 0), (1, 1)];
    public static List<(int, int)> NineSqs = [ (-1, -1), (-1, 0), (-1, 1),
                                               (0, -1), (0, 0), (0, 1),
                                               (1, -1), (1, 0), (1, 1) ];

    public static IEnumerable<(int, int)> Adj8Sqs(int r, int c)
    {
        foreach (var d in Adj8)
            yield return (r + d.Item1, c + d.Item2);
    }

    public static int Distance(int x1, int y1, int x2, int y2)
    {
        int dx = Math.Abs(x1 - x2);
        int dy = Math.Abs(y1 - y2);
        return (int)Math.Sqrt(dx * dx + dy * dy);
    }

    // Bresenham straight out of my old Scientific Computing textbook
    public static List<(int, int)> Bresenham(int r0, int c0, int r1, int c1)
    {
        List<(int, int)> pts = [];
        int error = 0;
        int stepR, stepC;
        int deltaC = c1 - c0;
        int r = r0;
        int c = c0;

        if (deltaC < 0)
        {
            deltaC = -deltaC;
            stepC = -1;
        }
        else
        {
            stepC = 1;
        }

        int deltaR = r1 - r0;
        if (deltaR < 0)
        {
            deltaR = -deltaR;
            stepR = -1;
        }
        else
        {
            stepR = 1;
        }

        if (deltaR < deltaC)
        {
            int criterion = deltaC / 2;
            while (c != c1 + stepC)
            {
                pts.Add((r, c));
                c += stepC;
                error += deltaR;
                if (error > criterion)
                {
                    error -= deltaC;
                    r += stepR;
                }
            }
        }
        else
        {
            int criterion = deltaR / 2;
            while (r != r1 + stepR)
            {
                pts.Add((r, c));
                r += stepR;
                error += deltaC;
                if (error > criterion)
                {
                    error -= deltaR;
                    c += stepC;
                }
            }
        }

        return pts;
    }
}

static class ListUtil
{
    public static void Shuffle<T>(this IList<T> list, Random rng)
    {
        int n = list.Count;
        while (n > 1)
        {            
            int k = rng.Next(n);
            n--;
            (list[n], list[k]) = (list[k], list[n]);
        }
    }
}