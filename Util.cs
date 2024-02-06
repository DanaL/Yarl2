namespace Yarl2;

class Util
{
    public static (int, int)[] Adj4 = [ (-1, 0), (1, 0), (0, 1), (0, -1)];
    public static (int, int)[] Adj8= [ (-1, -1), (-1, 0), (-1, 1),
                                        (0, -1), (0, 1),
                                        (1, -1), (1, 0), (1, 1)];

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
}