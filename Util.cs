
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

using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Yarl2;

// I didn't want to be beholden to someone else's colour class and anyhow
// Bearlib's didn't have a comparison operator implemented, which was 
// inconvenient for me
record struct Colour(int R, int G, int B, int Alpha);

enum Dir { North, South, East, West }

class Colours
{
    public static readonly Colour BLACK = new(0, 0 , 0, 255);
    public static readonly Colour WHITE = new(255, 255, 255, 255);
    public static readonly Colour GREY = new(136, 136, 136, 255);
    public static readonly Colour LIGHT_GREY = new(220, 220, 220, 255);
    public static readonly Colour DARK_GREY = new(72, 73, 75, 255);
    public static readonly Colour YELLOW = new(255, 255, 53, 255);
    public static readonly Colour YELLOW_ORANGE = new(255, 159, 0, 255);
    public static readonly Colour LIGHT_BROWN = new(160, 82, 45, 255);
    public static readonly Colour BROWN = new(43, 23, 0, 255);
    public static readonly Colour GREEN = new(144, 238, 144, 255);
    public static readonly Colour DARK_GREEN = new(0, 71, 49, 255);
    public static readonly Colour LIME_GREEN = new(191, 255, 0, 255);
    public static readonly Colour BLUE = new(0, 0, 200, 255);
    public static readonly Colour LIGHT_BLUE = new(55, 198, 255, 255);
    public static readonly Colour DARK_BLUE = new(12, 35, 64, 255);
    public static readonly Colour BRIGHT_RED = new(208, 28, 31, 255);
    public static readonly Colour DULL_RED = new(129, 12, 12, 255);
    public static readonly Colour TORCH_ORANGE = new(255, 159, 0, 50);
    public static readonly Colour TORCH_RED = new(208, 28, 31, 25);
    public static readonly Colour TORCH_YELLOW = new(255, 255, 53, 15);
    public static readonly Colour FX_RED = new(128, 00, 00, 175);
    public static readonly Colour FAR_BELOW = new(55, 198, 255, 75);
}

// Miscellaneous constants used in a few places
class Constants
{
    public const int BACKSPACE = 8;
    public const int ESC = 27;
}

partial class Util
{
    [GeneratedRegex(@"\D+")]
    public static partial Regex DigitsRegex();

    public static (int, int)[] Adj4 = [ (-1, 0), (1, 0), (0, 1), (0, -1)];
    public static (int, int)[] Adj8= [ (-1, -1), (-1, 0), (-1, 1),
                                        (0, -1), (0, 1),
                                        (1, -1), (1, 0), (1, 1)];
    public static List<(int, int)> NineSqs = [ (-1, -1), (-1, 0), (-1, 1),
                                               (0, -1), (0, 0), (0, 1),
                                               (1, -1), (1, 0), (1, 1) ];

    public static IEnumerable<(int, int)> Adj4Sqs(int r, int c)
    {
        foreach (var d in Adj4)
            yield return (r + d.Item1, c + d.Item2);
    }

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

    public static int Distance(Loc a, Loc b) => Distance(a.Row, a.Col, b.Row, b.Col);

    public static List<(int, int)> Bresenham(int r0, int c0, int r1, int c1)
    {
        List<(int, int)> pts = [];
        int dr = Math.Abs(r0 - r1);
        int dc = Math.Abs(c0 - c1);
        int sr = r0 < r1 ? 1 : -1;
        int sc = c0 < c1 ? 1 : -1;
        int err = (dc > dr ? dc : -dr) / 2;
        int e2;

        for ( ; ; )
        {
            pts.Add((r0, c0));
            if (r0 == r1 && c0 == c1)
                break;
            e2 = err;
            if (e2 > -dc)
            {
                err -= dr;
                c0 += sc;
            }
            if (e2 < dr)
            {
                err += dc;
                r0 += sr;
            }
        }
                
        return pts;
    }

    public static List<(int, int)> BresenhamCircle(int row, int col, int radius)
    {
        List<(int, int)> sqs = [];

        int x = 0;
        int y = radius;
        int d = 3 * 2 * radius;

        while (x <= y)
        {
            sqs.Add((row + y, col + x));
            sqs.Add((row - y, col + x));
            sqs.Add((row + y, col - x));
            sqs.Add((row - y, col - x));

            sqs.Add((row + x, col + y));
            sqs.Add((row - x, col + y));
            sqs.Add((row + x, col - y));
            sqs.Add((row - x, col - y));

            if (d < 0)
                d += 4 * x + 6;
            else
            {
                d += 4 * (x - y) + 10;
                y--;
            }
            x++;
        }
        
        return sqs;
    }

    public static string PlayerClassToStr(PlayerClass charClass) => charClass switch
    {
        PlayerClass.OrcReaver => "Orc Reaver",
        _ => "Dwarf Stalwart"
    };

    // I am very bravely breaking from D&D traidtion and I'm just going to 
    // store the stat's modifier instead of the score from 3-18 :O
    public static int StatRollToMod(int roll)
    {
        if (roll < 4)
            return -4;
        else if (roll == 4 || roll == 5)
            return -3;
        else if (roll == 6 || roll == 7)
            return -2;
        else if (roll == 8 || roll == 9)
            return -1;
        else if (roll == 10 || roll == 11)
            return 0;
        else if (roll == 12 || roll == 13)
            return 1;
        else if (roll == 14 || roll == 15)
            return 2;
        else if (roll == 16 || roll == 17)
            return 3;
        else
            return 4;
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

    public static List<T> Filled<T>(T val, int count)
    {
        List<T> res = new List<T>();
        for (int j = 0; j < count; j ++)
            res.Add(val);
        return res;
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

    public static string Pluralize(this string s)
    {
        if (s.Contains(" of "))
        {
            int space = s.IndexOf(' ');
            s = s[..space] + 's' + s[space..];

            return s;
        }
        else if (s.EndsWith("s") || s.EndsWith("x") || s.EndsWith("ch"))
        {
            return s + "es";
        }
        else
        {
            return s + "s";
        }
    }

    public static string Capitalize(this string s)
    {
       if (s != "" && char.IsLetter(s[0]))
            return $"{char.ToUpper(s[0])}{s[1..]}";
        else
            return s;
    }
}

interface IPassable
{
    bool Passable(TileType type);
}

class DungeonPassable : IPassable
{
    public bool Passable(TileType type) => type switch
    {
        TileType.DungeonFloor => true,
        TileType.BrokenDoor => true,
        TileType.OpenDoor => true,
        TileType.ClosedDoor => true,
        TileType.LockedDoor => true,
        TileType.WoodBridge => true,
        _ => false
    };
}

class WildernessPassable : IPassable 
{
    public bool Passable(TileType type) => type switch
    {
        TileType.Mountain => false,
        TileType.SnowPeak => false,
        TileType.Water => false,
        TileType.DeepWater => false,
        _ => true
    };
}

// Class that will divide a map into disjoint regions. Useful for making sure
// a dungeon level is fully connected, or for finding 'hidden valleys' in 
// the wilderness map
class RegionFinder(IPassable pc)
{
    IPassable _passableChecker = pc;

    (int, int) FindDisjointFloor(Map map, Dictionary<int, HashSet<(int, int)>> regions)
    {
        for (int r = 0; r < map.Height; r++)
        {
            for (int c = 0; c < map.Width; c++)
            {
                if (_passableChecker.Passable(map.TileAt(r, c).Type))
                {
                    bool found = false;
                    foreach (var region in regions.Values) 
                    {
                        if (region.Contains((r, c))) 
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                        return (r, c);
                }
            }
        }

        return (-1, -1);
    }

    HashSet<(int, int)> FloodFillRegion(Map map, int row, int col)
    {
        var sqs = new HashSet<(int ,int)>();
        var q = new Queue<(int, int)>();
        q.Enqueue((row, col));

        while (q.Count > 0)
        {
            var sq = q.Dequeue();

            if (sqs.Contains(sq))
                continue;

            sqs.Add(sq);
        
            foreach (var d in Util.Adj4)
            {
                var nr = sq.Item1 + d.Item1;
                var nc = sq.Item2 + d.Item2;
                var n = (nr, nc);
                if (!sqs.Contains(n) && map.InBounds(nr, nc) && _passableChecker.Passable(map.TileAt(nr, nc).Type))
                {
                    q.Enqueue(n);
                }
            }
        }

        return sqs;
    }

    public Dictionary<int, HashSet<(int, int)>> Find(Map map, bool fillSmallRegions, TileType fillTile)
    {
        int regionID = 0;
        var regions = new Dictionary<int, HashSet<(int, int)>>();
        
        do
        {
            var (startRow, startCol) = FindDisjointFloor(map, regions);
            if (startRow == -1 || startCol == -1)
                break;
            regions[regionID++] = FloodFillRegion(map, startRow, startCol);            
        }
        while (true);
        
        // Check for any regions that have very than three squares and just delete them
        if (fillSmallRegions)
        {
            foreach (var k in regions.Keys)
            {
                if (regions[k].Count <= 3)
                {
                    foreach (var sq in regions[k])
                        map.SetTile(sq, TileFactory.Get(fillTile));
                    regions.Remove(k);
                }
            }
        }
        return regions;
    }
}

class GameQuitException : Exception { }
class PlayerKilledException(string message) : Exception(message) { }