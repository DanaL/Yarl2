
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

// I didn't want to be beholden to someone else's colour class and anyhow
// Bearlib's didn't have a comparison operator implemented, which was 
// inconvenient for me
record struct Colour(int R, int G, int B);

class Colours
{
    public static readonly Colour BLACK = new(0, 0 , 0);
    public static readonly Colour WHITE = new(255, 255, 255);
    public static readonly Colour GREY = new(136, 136, 136);
    public static readonly Colour LIGHT_GREY = new(220, 220, 220);
    public static readonly Colour DARK_GREY = new(72, 73, 75);
    public static readonly Colour YELLOW = new(255, 255, 53);
    public static readonly Colour YELLOW_ORANGE = new(255, 159, 0);
    public static readonly Colour LIGHT_BROWN = new(101, 75, 0);
    public static readonly Colour BROWN = new(101, 67, 33);
    public static readonly Colour GREEN = new(144, 238, 144);
    public static readonly Colour DARK_GREEN = new(0, 71, 49 );
    public static readonly Colour BLUE = new(0, 0, 200);
    public static readonly Colour LIGHT_BLUE = new(55, 198, 255);
    public static readonly Colour DARK_BLUE = new(12, 35, 64);
}

// Miscellaneous constants used in a few places
class Constants
{
    public const int ESC = 27;
}

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

static class ListUtils
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

static class StringUtils
{
    public static string DefArticle(this string s) => $"the {s}";

    public static string IndefArticle(this string s) => s[0] switch
    {
        'a' or 'e' or 'i' or 'o' or 'u' or 'y' => $"an {s}",
        >= '0' and <= '9' => s,
        _ => $"a {s}"
    };
}

// I'm only doing this because the JSONSerializer can't handle
// tuples and I'd have to convert the DB keys to something else
// anyhow
// And it makes a nicer parameter to pass around to methods
record struct Loc(int DungeonID, int Level, int Row, int Col);

record struct Glyph(char Ch, Colour Lit, Colour Unlit);
