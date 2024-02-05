namespace Yarl2;

class Util
{
    public static ushort Distance(short x1, short y1, short x2, short y2)
    {
        ushort dx = (ushort) Math.Abs(x1 - x2);
        ushort dy = (ushort) Math.Abs(y1 - y2);
        return (ushort) Math.Sqrt(dx * dx + dy * dy);
    }
}