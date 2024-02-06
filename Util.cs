namespace Yarl2;

class Util
{
    public static int Distance(int x1, int y1, int x2, int y2)
    {
        int dx = Math.Abs(x1 - x2);
        int dy = Math.Abs(y1 - y2);
        return (int)Math.Sqrt(dx * dx + dy * dy);
    }
}