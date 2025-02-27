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

using System.Text;
using System.Text.RegularExpressions;

namespace Yarl2;

enum GameEventType { Quiting, KeyInput, EndOfRound, NoEvent, Death, MobSpotted, LocChanged }
record struct GameEvent(GameEventType Type, char Value);
record Sqr(Colour Fg, Colour Bg, char Ch);

enum Dir { North, South, East, West, None }

// I didn't want to be beholden to someone else's colour class and anyhow
// Bearlib's didn't have a comparison operator implemented, which was 
// inconvenient for me
record struct Colour(int R, int G, int B, int Alpha);

class Colours
{
  public static readonly Colour NULL = new(0, 0, 0, 0);
  public static readonly Colour BLACK = new(0, 0, 0, 255);
  public static readonly Colour WHITE = new(255, 255, 255, 255);
  public static readonly Colour GREY = new(136, 136, 136, 255);
  public static readonly Colour LIGHT_GREY = new(220, 220, 220, 255);
  public static readonly Colour DARK_GREY = new(72, 73, 75, 255);
  public static readonly Colour YELLOW = new(255, 255, 53, 255);
  public static readonly Colour YELLOW_ORANGE = new(255, 159, 0, 255);
  public static readonly Colour LIGHT_BROWN = new(160, 82, 45, 255);
  public static readonly Colour BROWN = new(150, 75, 0, 255);
  public static readonly Colour GREEN = new(144, 238, 144, 255);
  public static readonly Colour DARK_GREEN = new(0, 71, 49, 255);
  public static readonly Colour LIME_GREEN = new(191, 255, 0, 255);
  public static readonly Colour BLUE = new(0, 0, 200, 255);
  public static readonly Colour LIGHT_BLUE = new(55, 198, 255, 255);
  public static readonly Colour DARK_BLUE = new(12, 35, 128, 255);
  public static readonly Colour BRIGHT_RED = new(208, 28, 31, 255);
  public static readonly Colour DULL_RED = new(129, 12, 12, 255);
  public static readonly Colour TORCH_ORANGE = new(255, 159, 0, 50);
  public static readonly Colour TORCH_RED = new(208, 28, 31, 25);
  public static readonly Colour TORCH_YELLOW = new(255, 255, 53, 15);
  public static readonly Colour FX_RED = new(128, 00, 00, 175);
  public static readonly Colour FAR_BELOW = new(55, 198, 255, 50);
  public static readonly Colour HILITE = new(255, 255, 53, 128);
  public static readonly Colour PURPLE = new(191, 64, 191, 255);
  public static readonly Colour LIGHT_PURPLE = new(207, 159, 255, 255);
  public static readonly Colour FADED_PURPLE = new(207, 159, 255, 125);
  public static readonly Colour PINK = new(255, 192, 203, 255);
  public static readonly Colour FAINT_PINK = new(178, 102, 255, 125);
  public static readonly Colour ICE_BLUE = new(40, 254, 253, 255);
  public static readonly Colour SEARCH_HIGHLIGHT = new(55, 198, 255, 75);
  public static readonly Colour SOPHIE_GREEN = new(138, 195, 171, 255);

  public static string ColourToText(Colour colour)
  {
    if (colour == WHITE) return "white";
    else if (colour == BLACK) return "black";
    else if (colour == GREY) return "grey";
    else if (colour == LIGHT_GREY) return "lightgrey";
    else if (colour == DARK_GREY) return "darkgrey";
    else if (colour == YELLOW) return "yellow";
    else if (colour == YELLOW_ORANGE) return "yelloworange";
    else if (colour == LIGHT_BROWN) return "lightbrown";
    else if (colour == BROWN) return "brown";
    else if (colour == GREEN) return "green";
    else if (colour == DARK_GREEN) return "darkgreen";
    else if (colour == LIME_GREEN) return "limegreen";
    else if (colour == BLUE) return "blue";
    else if (colour == LIGHT_BLUE) return "lightblue";
    else if (colour == DARK_BLUE) return "darkblue";
    else if (colour == BRIGHT_RED) return "brightred";
    else if (colour == DULL_RED) return "dullred";
    else if (colour == TORCH_ORANGE) return "torchorange";
    else if (colour == TORCH_RED) return "torchred";
    else if (colour == TORCH_YELLOW) return "torchyellow";
    else if (colour == FAR_BELOW) return "farbelow";
    else if (colour == LIGHT_PURPLE) return "lightpurple";
    else if (colour == FADED_PURPLE) return "fadedpurple";
    else if (colour == PURPLE) return "purple";
    else if (colour == PINK) return "pink";
    else if (colour == FAINT_PINK) return "faintpink";
    else if (colour == ICE_BLUE) return "iceblue";
    else if (colour == HILITE) return "hilite";
    else if (colour == SOPHIE_GREEN) return "sophiegreen";
    else if (colour == NULL) return "null";
    else throw new Exception($"Hmm I don't know that colour {colour}");
  }

  public static Colour TextToColour(string colour) => colour switch
  {
    "white" => WHITE,
    "black" => BLACK,
    "grey" => GREY,
    "lightgrey" => LIGHT_GREY,
    "darkgrey" => DARK_GREY,
    "yellow" => YELLOW,
    "yelloworange" => YELLOW_ORANGE,
    "lightbrown" => LIGHT_BROWN,
    "brown" => BROWN,
    "green" => GREEN,
    "darkgreen" => DARK_GREEN,
    "limegreen" => LIME_GREEN,
    "blue" => BLUE,
    "lightblue" => LIGHT_BLUE,
    "darkblue" => DARK_BLUE,
    "brightred" => BRIGHT_RED,
    "dullred" => DULL_RED,
    "torchorange" => TORCH_ORANGE,
    "torchred" => TORCH_RED,
    "torchyellow" => TORCH_YELLOW,
    "farbelow" => FAR_BELOW,
    "lightpurple" => LIGHT_PURPLE,
    "fadedpurple" => FADED_PURPLE,
    "purple" => PURPLE,
    "pink" => PINK,
    "faintpink" => FAINT_PINK,
    "iceblue" => ICE_BLUE,
    "hilite" => HILITE,
    "sophiegreen" => SOPHIE_GREEN,
    "null" => NULL,
    _ => throw new Exception($"Hmm I don't know that colour {colour}")
  };
}

enum Metals { NotMetal, Iron, Steel, Bronze, Mithril, Silver }

static class MetalsExtensions
{
  public static bool CanCorrode(this Metals metal) => metal switch
  {
    Metals.Iron => true,
    Metals.Steel => true,
    Metals.Bronze => true,
    _ => false
  };
}

enum Rust { Rusted, Corroded }

// Miscellaneous constants used in a few places
class Constants
{
  public const int BACKSPACE = 8;
  public const int TAB = 9;
  public const int ESC = 27;
  public static readonly Sqr BLANK_SQ = new(Colours.BLACK, Colours.BLACK, ' ');
  public const char SEPARATOR = '\u001F';
  public static int PRACTICE_RATIO = 100; // how skill use count translates into a bonus
  public const int TELEPATHY_RANGE = 40; // I don't really have a better spot for this right now
  public static readonly string VERSION = "0.3.2";
  public const char TOP_LEFT_CORNER = '┍';
  public const char TOP_RIGHT_CORNER = '┑';
  public const char BOTTOM_LEFT_CORNER = '┕';
  public const char BOTTOM_RIGHT_CORNER = '┙';
}

class Util
{
  public static double ToDouble(string s) => double.Parse(s, System.Globalization.CultureInfo.InvariantCulture);

  public static string NamesFile => ResourcePath.GetDataFilePath("names.txt");

  public static DirectoryInfo UserDir
  {
    get
    {
      string basePath;
      if (OperatingSystem.IsMacOS())
      {
        basePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library",
            "Application Support",
            "ddelve"
        );
      }
      else // Windows and others
      {
        basePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ddelve"
        );
      }

      return new DirectoryInfo(basePath);
    }
  }

  public static string SavePath
  {
    get
    {
      try
      {
        string basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string gamePath = Path.Combine(basePath, "ddelve", "Saves");

        // CreateDirectory is safe to call even if directory exists
        Directory.CreateDirectory(gamePath);

        return gamePath;
      }
      catch (Exception ex)
      {
        throw new Exception($"Failed to initialize save directory: {ex.Message}", ex);
      }
    }
  }

  public static List<int> ToNums(string txt)
  {
    List<int> nums = [];

    foreach (Match match in Regex.Matches(txt, @"-?\d+"))
    {
      nums.Add(int.Parse(match.Value));
    }

    return nums;
  }

  public static (int, int)[] Adj4 = [(-1, 0), (1, 0), (0, 1), (0, -1)];
  public static (int, int)[] Adj8 = [
    (-1, 0), (1, 0), (0, 1), (0, -1),
    (-1, -1), (-1, 1),(1, -1), (1, 1)];
  public static List<(int, int)> NineSqs = [(-1, -1),
    (-1, 0),
    (-1, 1),
    (0, -1),
    (0, 0),
    (0, 1),
    (1, -1),
    (1, 0),
    (1, 1)];

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

  public static IEnumerable<Loc> Adj4Locs(Loc loc)
  {
    foreach (var d in Adj4)
      yield return loc with { Row = loc.Row + d.Item1, Col = loc.Col + d.Item2 };
  }

  public static IEnumerable<Loc> Adj8Locs(Loc loc)
  {
    foreach (var d in Adj8)
      yield return loc with { Row = loc.Row + d.Item1, Col = loc.Col + d.Item2 };
  }

  public static int CountAdjTileType(Map map, int r, int c, TileType type)
  {
    int count = 0;

    foreach (var loc in Adj8Sqs(r, c))
    {
      if (map.TileAt(loc).Type == type)
        ++count;
    }

    return count;
  }

  public static Loc RandomAdjLoc(Loc loc, GameState gs)
  {
    var adj = Adj8Locs(loc).ToList();

    return adj[gs.Rng.Next(adj.Count)];
  }

  public static int Manhattan(Loc a, Loc b)
  {
    int dx = Math.Abs(a.Col - b.Col);
    int dy = Math.Abs(a.Row - b.Row);
    return dx + dy;
  }

  public static int Distance(int x1, int y1, int x2, int y2)
  {
    int dx = Math.Abs(x1 - x2);
    int dy = Math.Abs(y1 - y2);
    return (int)Math.Sqrt(dx * dx + dy * dy);
  }

  public static int Distance(Loc a, Loc b) => Distance(a.Row, a.Col, b.Row, b.Col);

  public static bool PtInSqr(int row, int col, int topRow, int leftCol, int height, int width)
  {
    return row >= topRow && row < topRow + height &&
           col >= leftCol && col < leftCol + width;
  }

  public static double AngleBetweenLocs(Loc a, Loc b)
  {
    double dX = b.Col - a.Col;
    double dY = -(b.Row - a.Row);

    return Math.Atan2(dY, dX);
  }

  public static string RelativeDir(Loc a, Loc b)
  {
    double angle = AngleBetweenLocs(a, b);

    if (angle >= 0 && angle < 0.25)
      return "east";
    else if (angle >= 0.25 && angle < 1.31)
      return "northeast";
    else if (angle >= 1.31 && angle < 1.82)
      return "north";
    else if (angle >= 1.82 && angle < 2.89)
      return "northwest";
    else if (angle >= 2.89 || angle < -2.89)
      return "west";
    else if (angle < 0.0 && angle > -0.25)
      return "east";
    else if (angle <= -0.25 && angle > -1.31)
      return "southeast";
    else if (angle <= -1.31 && angle > -1.82)
      return "south";
    else
      return "southwest";
  }

  public static List<(int, int)> Bresenham(int r0, int c0, int r1, int c1)
  {
    List<(int, int)> pts = [];
    int dr = Math.Abs(r0 - r1);
    int dc = Math.Abs(c0 - c1);
    int sr = r0 < r1 ? 1 : -1;
    int sc = c0 < c1 ? 1 : -1;
    int err = (dc > dr ? dc : -dr) / 2;
    int e2;

    for (; ; )
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

  public static List<Loc> Trajectory(Loc origin, Loc target)
  {
    return [..Bresenham(origin.Row, origin.Col, target.Row, target.Col)
            .Select(sq => origin with { Row = sq.Item1, Col = sq.Item2 })];
  }

  public static HashSet<Loc> LocsInRadius(Loc origin, int radius, int height, int width)
  {
    HashSet<Loc> locs = [];
    Queue<Loc> q = [];
    q.Enqueue(origin);
    HashSet<Loc> visited = [origin];

    while (q.Count > 0)
    {
      Loc loc = q.Dequeue();
      locs.Add(loc);

      foreach (var adj in Adj8Locs(loc))
      {
        if (adj.Row < 0 || adj.Col < 0 || adj.Row >= height || adj.Col >= width)
          continue;
        if (visited.Contains(adj))
          continue;

        int d = Distance(origin, adj);
        if (d <= radius)
        {
          visited.Add(adj);
          q.Enqueue(adj);
        }
      }
    }

    return locs;
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

  public static (int, int) Rotate(int originR, int originC, int targetR, int targetC, double angle)
  {
    double translatedR = targetR - originR;
    double translatedC = targetC - originC;

    double rotatedR = translatedC * Math.Sin(angle) + translatedR * Math.Cos(angle);
    double rotatedC = translatedC * Math.Cos(angle) - translatedR * Math.Sin(angle);

    return ((int)rotatedR + originR, (int)rotatedC + originC);
  }

  public static (int, int) ExtendLine(int r0, int c0, int r1, int c1, int dist)
  {
    double length = Distance(r0, c0, r1, c1);

    // calculate the unit direction of the vector    
    double unitX = (c1 - c0) / length;
    double unitY = (r1 - r0) / length;

    int newR = (int)(r1 + unitY * dist);
    int newC = (int)(c1 + unitX * dist);

    return (newR, newC);
  }

  // I've written floodfill here and there for various effects and noise, etc
  // but I'm hoping I can consolidate on this veresion of the function
  public static HashSet<Loc> FloodFill(GameState gs, Loc origin, int range)
  {
    HashSet<Loc> locs = [];
    Queue<Loc> q = [];
    q.Enqueue(origin);

    while (q.Count > 0)
    {
      Loc curr = q.Dequeue();
      if (locs.Contains(curr))
        continue;
      locs.Add(curr);

      foreach (Loc adj in Adj8Locs(curr))
      {
        if (Distance(origin, adj) > range)
          continue;
        if (locs.Contains(adj))
          continue;
        Tile tile = gs.TileAt(adj);
        if (!(tile.Passable() || tile.PassableByFlight()))
          continue;

        q.Enqueue(adj);
      }
    }

    return locs;
  }

  // I am very bravely breaking from D&D traidtion and I'm just going to 
  // store the stat's modifier instead of the score from 3-18 :O
  // ** I think this can be moved to player creation because that's should
  // be the only place this is used
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

  public static (int, int) KeyToDir(char key) => key switch
  {
    'y' => (-1, -1),
    'u' => (-1, 1),
    'h' => (0, -1),
    'j' => (1, 0),
    'k' => (-1, 0),
    'l' => (0, 1),
    'b' => (1, -1),
    'n' => (1, 1),
    _ => (0, 0)
  };

  public static char ArrowChar(Loc a, Loc b)
  {
    if (a.Row == b.Row)
      return '-';
    else if (a.Col == b.Col)
      return '|';
    else if (a.Col < b.Col && a.Row < b.Row)
      return '\\';
    else if (a.Col > b.Col && a.Row > b.Row)
      return '\\';
    else
      return '/';
  }

  static Glyph VaultDoorGlyph(VaultDoor door)
  {
    char ch = door.Open ? '\\' : 'ǁ';
    var (fg, bg) = MetallicColour(door.Material);

    return new Glyph(ch, fg, bg, Colours.BLACK, Colours.BLACK);
  }

  public static Glyph TileToGlyph(Tile tile) => tile.Type switch
  {
    TileType.PermWall => new Glyph('#', Colours.GREY, Colours.DARK_GREY, Colours.BLACK, Colours.BLACK),
    TileType.StoneWall => new Glyph('░', Colours.GREY, Colours.DARK_GREY, Colours.BLACK, Colours.BLACK),
    TileType.DungeonWall => new Glyph('#', Colours.GREY, Colours.DARK_GREY, Colours.TORCH_ORANGE, Colours.BLACK),
    TileType.DungeonFloor => new Glyph('.', Colours.YELLOW, Colours.GREY, Colours.TORCH_ORANGE, Colours.BLACK),
    TileType.StoneFloor => new Glyph('.', Colours.GREY, Colours.DARK_GREY, Colours.BLACK, Colours.BLACK),
    TileType.StoneRoad => new Glyph('·', Colours.GREY, Colours.DARK_GREY, Colours.BLACK, Colours.BLACK),
    TileType.ClosedDoor => new Glyph('+', Colours.LIGHT_BROWN, Colours.BROWN, Colours.BLACK, Colours.BLACK),
    TileType.LockedDoor => new Glyph('+', Colours.LIGHT_BROWN, Colours.BROWN, Colours.BLACK, Colours.BLACK),
    TileType.OpenDoor => new Glyph('\\', Colours.LIGHT_BROWN, Colours.BROWN, Colours.BLACK, Colours.BLACK),
    TileType.BrokenDoor => new Glyph('\\', Colours.LIGHT_BROWN, Colours.BROWN, Colours.BLACK, Colours.BLACK),
    TileType.DeepWater => new Glyph('}', Colours.BLUE, Colours.DARK_BLUE, Colours.BLACK, Colours.BLACK),
    TileType.Water => new Glyph('}', Colours.BLUE, Colours.DARK_BLUE, Colours.BLACK, Colours.BLACK),
    TileType.Pool => new Glyph('}', Colours.BLUE, Colours.DARK_BLUE, Colours.BLACK, Colours.BLACK),
    TileType.FrozenPool => new Glyph('}', Colours.BLUE, Colours.ICE_BLUE, Colours.WHITE, Colours.LIGHT_GREY),
    TileType.Sand => new Glyph('.', Colours.YELLOW, Colours.YELLOW_ORANGE, Colours.BLACK, Colours.BLACK),
    TileType.Grass => new Glyph('.', Colours.GREEN, Colours.DARK_GREEN, Colours.BLACK, Colours.BLACK),
    TileType.GreenTree => new Glyph('ϙ', Colours.GREEN, Colours.DARK_GREEN, Colours.BLACK, Colours.BLACK),
    TileType.YellowTree => new Glyph('ϙ', Colours.YELLOW, Colours.YELLOW_ORANGE, Colours.BLACK, Colours.BLACK),
    TileType.RedTree => new Glyph('ϙ', Colours.BRIGHT_RED, Colours.DULL_RED, Colours.BLACK, Colours.BLACK),
    TileType.Conifer => new Glyph('▲', Colours.DARK_GREEN, Colours.DARK_GREEN, Colours.BLACK, Colours.BLACK),
    TileType.OrangeTree => new Glyph('ϙ', Colours.YELLOW_ORANGE, Colours.DULL_RED, Colours.BLACK, Colours.BLACK),
    TileType.Mountain => new Glyph('\u039B', Colours.GREY, Colours.DARK_GREY, Colours.BLACK, Colours.BLACK),
    TileType.SnowPeak => new Glyph('\u039B', Colours.WHITE, Colours.GREY, Colours.BLACK, Colours.BLACK),
    TileType.Portal => new Glyph('Ո', Colours.WHITE, Colours.GREY, Colours.BLACK, Colours.BLACK),
    TileType.Shortcut => new Glyph('<', Colours.WHITE, Colours.GREY, Colours.BLACK, Colours.BLACK),
    TileType.Upstairs => new Glyph('<', Colours.WHITE, Colours.GREY, Colours.BLACK, Colours.BLACK),
    TileType.Downstairs => new Glyph('>', Colours.WHITE, Colours.GREY, Colours.BLACK, Colours.BLACK),
    TileType.ShortcutDown => new Glyph('>', Colours.WHITE, Colours.GREY, Colours.BLACK, Colours.BLACK),
    TileType.Cloud => new Glyph('#', Colours.WHITE, Colours.WHITE, Colours.BLACK, Colours.BLACK),
    TileType.Dirt => new Glyph('.', Colours.LIGHT_BROWN, Colours.BROWN, Colours.BLACK, Colours.BLACK),
    TileType.WoodFloor => new Glyph('.', Colours.LIGHT_BROWN, Colours.BROWN, Colours.BLACK, Colours.BLACK),
    TileType.WoodWall => new Glyph('░', Colours.LIGHT_BROWN, Colours.BROWN, Colours.BLACK, Colours.BLACK),
    TileType.HWindow => new Glyph('-', Colours.LIGHT_GREY, Colours.GREY, Colours.BLACK, Colours.BLACK),
    TileType.VWindow => new Glyph('|', Colours.LIGHT_GREY, Colours.GREY, Colours.BLACK, Colours.BLACK),
    TileType.Forge => new Glyph('^', Colours.BRIGHT_RED, Colours.DULL_RED, Colours.TORCH_ORANGE, Colours.BLACK),
    TileType.Well => new Glyph('o', Colours.LIGHT_GREY, Colours.GREY, Colours.BLACK, Colours.BLACK),
    TileType.Bridge => new Glyph('=', Colours.GREY, Colours.DARK_GREY, Colours.BLACK, Colours.BLACK),
    TileType.WoodBridge => new Glyph('=', Colours.LIGHT_BROWN, Colours.BROWN, Colours.BLACK, Colours.BLACK),
    TileType.Landmark => new Glyph('_', Colours.LIGHT_GREY, Colours.GREY, Colours.BLACK, Colours.BLACK),
    TileType.IdolAltar => new Glyph('_', Colours.LIGHT_GREY, Colours.GREY, Colours.BLACK, Colours.BLACK),
    TileType.Chasm => new Glyph('\u2237', Colours.DARK_GREY, Colours.DARK_GREY, Colours.BLACK, Colours.BLACK),
    TileType.CharredGrass => new Glyph('.', Colours.GREY, Colours.DARK_GREY, Colours.BLACK, Colours.BLACK),
    TileType.CharredStump => new Glyph('╵', Colours.GREY, Colours.DARK_GREY, Colours.BLACK, Colours.BLACK),
    TileType.FrozenDeepWater => new Glyph('}', Colours.BLUE, Colours.ICE_BLUE, Colours.WHITE, Colours.LIGHT_GREY),
    TileType.FrozenWater => new Glyph('}', Colours.BLUE, Colours.ICE_BLUE, Colours.WHITE, Colours.LIGHT_GREY),
    TileType.Portcullis => new Glyph('ǁ', Colours.GREY, Colours.DARK_GREY, Colours.BLACK, Colours.BLACK),
    TileType.OpenPortcullis => new Glyph('.', Colours.GREY, Colours.DARK_GREY, Colours.BLACK, Colours.BLACK),
    TileType.GateTrigger => new Glyph(((GateTrigger)tile).Found ? '•' : '.', Colours.YELLOW, Colours.GREY, Colours.TORCH_ORANGE, Colours.BLACK),
    TileType.VaultDoor => VaultDoorGlyph((VaultDoor)tile),
    TileType.HiddenTrapDoor or TileType.HiddenPit => new Glyph('.', Colours.YELLOW, Colours.GREY, Colours.TORCH_ORANGE, Colours.BLACK),
    TileType.TrapDoor or TileType.Pit => new Glyph('^', Colours.YELLOW, Colours.GREY, Colours.BLACK, Colours.BLACK),
    TileType.SecretDoor => new Glyph('#', Colours.GREY, Colours.DARK_GREY, Colours.TORCH_ORANGE, Colours.BLACK),
    TileType.HiddenTeleportTrap => new Glyph('.', Colours.YELLOW, Colours.GREY, Colours.TORCH_ORANGE, Colours.BLACK),
    TileType.TeleportTrap => new Glyph('^', Colours.LIGHT_PURPLE, Colours.PURPLE, Colours.BLACK, Colours.BLACK),
    TileType.BrokenPortcullis => new Glyph('/', Colours.GREY, Colours.DARK_GREY, Colours.BLACK, Colours.BLACK),
    TileType.HiddenDartTrap => new Glyph('.', Colours.YELLOW, Colours.GREY, Colours.TORCH_ORANGE, Colours.BLACK),
    TileType.DartTrap => new Glyph('^', Colours.WHITE, Colours.LIGHT_GREY, Colours.BLACK, Colours.BLACK),
    TileType.HiddenWaterTrap => new Glyph('.', Colours.YELLOW, Colours.GREY, Colours.TORCH_ORANGE, Colours.BLACK),
    TileType.WaterTrap => new Glyph('^', Colours.ICE_BLUE, Colours.BLUE, Colours.BLACK, Colours.BLACK),
    TileType.FireJetTrap =>
      ((FireJetTrap)tile).Seen ? new Glyph('#', Colours.BRIGHT_RED, Colours.DULL_RED, Colours.TORCH_ORANGE, Colours.BLACK)
                               : new Glyph('#', Colours.GREY, Colours.DARK_GREY, Colours.TORCH_ORANGE, Colours.BLACK),
    TileType.JetTrigger =>
      ((JetTrigger)tile).Visible ? new Glyph('^', Colours.YELLOW, Colours.GREY, Colours.TORCH_ORANGE, Colours.BLACK)
                                 : new Glyph('.', Colours.YELLOW, Colours.GREY, Colours.TORCH_ORANGE, Colours.BLACK),
    TileType.MagicMouth => new Glyph('^', Colours.WHITE, Colours.GREY, Colours.TORCH_ORANGE, Colours.BLACK),
    TileType.HiddenMagicMouth => new Glyph('.', Colours.YELLOW, Colours.GREY, Colours.TORCH_ORANGE, Colours.BLACK),
    TileType.Gravestone => new Glyph('\u25AE', Colours.GREY, Colours.DARK_GREY, Colours.BLACK, Colours.BLACK),
    TileType.DisturbedGrave => new Glyph('|', Colours.GREY, Colours.DARK_GREY, Colours.BLACK, Colours.BLACK),
    TileType.BridgeTrigger => new Glyph('•', Colours.YELLOW, Colours.GREY, Colours.TORCH_ORANGE, Colours.BLACK),
    TileType.HiddenBridgeCollapseTrap => new Glyph('.', Colours.YELLOW, Colours.GREY, Colours.TORCH_ORANGE, Colours.BLACK),
    TileType.ReveealedBridgeCollapseTrap => new Glyph('^', Colours.WHITE, Colours.GREY, Colours.BLACK, Colours.BLACK),
    TileType.BusinessSign => new Glyph('Þ', Colours.WHITE, Colours.LIGHT_GREY, Colours.BLACK, Colours.BLACK),
    TileType.FakeStairs => new Glyph('>', Colours.WHITE, Colours.GREY, Colours.BLACK, Colours.BLACK),
    TileType.HiddenSummonsTrap => new Glyph('.', Colours.YELLOW, Colours.GREY, Colours.TORCH_ORANGE, Colours.BLACK),
    TileType.RevealedSummonsTrap => new Glyph('^', Colours.WHITE, Colours.GREY, Colours.BLACK, Colours.BLACK),
    TileType.HFence => new Glyph('-', Colours.LIGHT_BROWN, Colours.BROWN, Colours.BLACK, Colours.BLACK),
    TileType.VFence => new Glyph('|', Colours.LIGHT_BROWN, Colours.BROWN, Colours.BLACK, Colours.BLACK),
    TileType.CornerFence => new Glyph('+', Colours.LIGHT_BROWN, Colours.BROWN, Colours.BLACK, Colours.BLACK),
    TileType.MonsterWall => ((MonsterWall)tile).Glyph,
    TileType.Lever =>
      ((Lever)tile).On ? new Glyph('/', Colours.GREY, Colours.DARK_GREY, Colours.TORCH_ORANGE, Colours.BLACK)
                       : new Glyph('|', Colours.GREY, Colours.DARK_GREY, Colours.TORCH_ORANGE, Colours.BLACK),
    TileType.CreepyAltar => new Glyph('∆', Colours.DULL_RED, Colours.BROWN, Colours.BLACK, Colours.BLACK),
    _ => new Glyph(' ', Colours.BLACK, Colours.BLACK, Colours.BLACK, Colours.BLACK)
  };

  public record CyclopediaEntry(string Title, string Text);
  public static Dictionary<string, CyclopediaEntry> LoadCyclopedia()
  {
    Dictionary<string, CyclopediaEntry> cyclopedia = [];

    var lines = File.ReadAllLines(ResourcePath.GetDataFilePath("cyclopedia.txt"));

    for (int j = 0; j < lines.Length; j += 3)
    {
      string s = lines[j + 1];
      string key, title;
      int k = s.IndexOf('|');
      CyclopediaEntry entry;
      if (k > -1)
      {
        key = s[..k];
        title = s[(k + 1)..];
        entry = new CyclopediaEntry(title, lines[j + 2]);
      }
      else
      {
        key = s;
        title = s;
        entry = new CyclopediaEntry(s, lines[j + 2]);
      }

      cyclopedia.Add(key, entry);
    }

    return cyclopedia;
  }

  public static (Colour, Colour) MetallicColour(Metals metal) => metal switch
  {
    Metals.NotMetal => (Colours.BLACK, Colours.BLACK), // should this be an error condition?
    Metals.Bronze => (Colours.DULL_RED, Colours.BROWN),
    Metals.Mithril => (Colours.LIGHT_BLUE, Colours.LIGHT_GREY),
    _ => (Colours.DARK_GREY, Colours.DARK_GREY)
  };

  // Filter for locs where we may not want to place items
  public static bool GoodFloorSpace(GameObjectDB objDb, Loc loc)
  {
    foreach (Item item in objDb.ItemsAt(loc))
    {
      if (item.HasTrait<OnFireTrait>())
        return false;
    }

    return true;
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
    List<T> res = [];
    for (int j = 0; j < count; j++)
      res.Add(val);
    return res;
  }
}

static class StringUtils
{
  public static string DefArticle(this string s) => $"the {s}";

  public static string IndefArticle(this string s) => s[0] switch
  {
    'A' or 'E' or 'I' or 'O' or 'U' or 'Y' or
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
    else if (s.EndsWith('s') || s.EndsWith('x') || s.EndsWith("ch"))
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

  static HashSet<string> _minorWords = ["of", "the", "and", "a", "an"];
  public static string CapitalizeWords(this string s)
  {
    var words = s.ToLower().Split(' ').Select(w => _minorWords.Contains(w) ? w : w.Capitalize());
    return string.Join(' ', words);
  }

  public static string Possessive(this string s, Actor owner)
  {
    if (owner is Player)
      return "your " + s;
    else if (owner.Name.EndsWith("s"))
      return $"{owner.FullName}' {s}";
    else
      return $"{owner.FullName}'s {s}";
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
    TileType.StoneWall => false,
    TileType.WoodWall => false,
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
    var sqs = new HashSet<(int, int)>();
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

  public Dictionary<int, HashSet<(int, int)>> Find(Map map, bool fillSmallRegions, int smallThreshold, TileType fillTile)
  {
    int regionID = 0;
    Dictionary<int, HashSet<(int, int)>> regions = [];

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
      foreach (int k in regions.Keys)
      {
        if (regions[k].Count <= smallThreshold)
        {
          foreach (var sq in regions[k])
            map.SetTile(sq, TileFactory.Get(fillTile));
          regions.Remove(k);
        }
      }
    }

    // I want to make sure the index IDs are in order
    Dictionary<int, HashSet<(int, int)>> tweaked = [];
    int j = 0;
    foreach (var region in regions.Values)
      tweaked.Add(j++, region);

    return tweaked;
  }
}

class MapUtils
{
  public static void Dump(Map map, Dictionary<(int, int), int> areas)
  {
    char RegionNum(int num)
    {
      if (num < 10)
        return (char)('0' + num);

      return (char)('A' + (10 - num));
    }

    char[,] sqs = new char[map.Height, map.Width];

    for (int r = 0; r < map.Height; r++)
    {
      for (int c = 0; c < map.Width; c++)
      {
        switch (map.TileAt(r, c).Type)
        {
          case TileType.PermWall:
            sqs[r, c] = '#';
            break;
          case TileType.DungeonWall:
          case TileType.StoneWall:
            sqs[r, c] = ' ';
            break;
          case TileType.DungeonFloor:
          case TileType.StoneFloor:
            sqs[r, c] = '.';
            break;
          case TileType.DeepWater:
          case TileType.Water:
            sqs[r, c] = '}';
            break;
          case TileType.ClosedDoor:
          case TileType.LockedDoor:
            sqs[r, c] = '+';
            break;
          case TileType.OpenDoor:
          case TileType.BrokenDoor:
            sqs[r, c] = '/';
            break;
          case TileType.Bridge:
          case TileType.WoodBridge:
            sqs[r, c] = '=';
            break;
          case TileType.Upstairs:
            sqs[r, c] = '<';
            break;
          case TileType.Downstairs:
            sqs[r, c] = '>';
            break;
          case TileType.Chasm:
            sqs[r, c] = ':';
            break;
          case TileType.Portcullis:
          case TileType.OpenPortcullis:
            sqs[r, c] = '"';
            break;
          case TileType.GateTrigger:
            sqs[r, c] = '`';
            break;
          case TileType.VaultDoor:
            sqs[r, c] = '#';
            break;
          default:
            sqs[r, c] = '?';
            break;
        }
      }
    }

    for (int r = 0; r < map.Height; r++)
    {
      var sb = new StringBuilder();
      for (int c = 0; c < map.Width; c++)
      {
        if (areas.ContainsKey((r, c)))
          sb.Append(RegionNum(areas[(r, c)]));
        else
          sb.Append(sqs[r, c]);
      }
      Console.WriteLine(sb.ToString());
    }
  }
}

class QuitGameException : Exception { }
class SaveGameException : Exception { }
class GameNotLoadedException : Exception { }
class PlayerKilledException(string message) : Exception(message) { }
class VictoryException : Exception { }
class InvalidTownException : Exception { }
class PlacingBuldingException : Exception { }
class InvalidRoomException : Exception { }
class CouldNotPlaceDungeonEntranceException : Exception { }
class AbnormalMovement(Loc dest) : Exception
{
  public Loc Dest { get; set; } = dest;
}
class UnknownMonsterException(string name) : Exception
{
  public string Name { get; set; } = name;
}

// Really, a hacked up version of the Shadowcast routine in FoV, but it's
// different enough to (I think) to justify a separate version. Mingling the
// differences would really stink up FOV.cs
class ConeCalculator
{
  public static List<Loc> Affected(int range, Loc origin, Loc target, Map map, GameObjectDB objDb)
  {
    HashSet<Loc> affected = [];

    // even if the target is closer, the cone always covers the full range
    if (Util.Distance(origin, target) < range)
    {
      var (newR, newC) = Util.ExtendLine(origin.Row, origin.Col, target.Row, target.Col, range);
      target = target with { Row = newR, Col = newC };
    }

    var (ar, ac) = Util.Rotate(origin.Row, origin.Col, target.Row, target.Col, -0.523);
    var (br, bc) = Util.Rotate(origin.Row, origin.Col, target.Row, target.Col, 0.523);
    Loc beamA = origin with { Row = ar, Col = ac };
    Loc beamB = origin with { Row = br, Col = bc };
    int octantA = OctantForBeam(origin, beamA);
    int octantB = OctantForBeam(origin, beamB);

    while (octantA != octantB)
    {
      affected = [..affected.Union(CalcOctant(range, origin, map, octantA, objDb))];
      --octantA;
      if (octantA < 0)
        octantA = 7;
    }
    affected = [..affected.Union(CalcOctant(range, origin, map, octantB, objDb))];
    double angleA = Util.AngleBetweenLocs(origin, beamA);
    double angleB = Util.AngleBetweenLocs(origin, beamB);

    // Normalize angles to handle if we're crossing from quadant 7 to 0
    if (Math.Abs(angleA - angleB) > Math.PI)
    {
      // Adjust the negative angle
      if (angleA < 0)
        angleA += 2 * Math.PI;
      if (angleB < 0)
        angleB += 2 * Math.PI;
    }

    double minAngle = double.Min(angleA, angleB);
    double maxAngle = double.Max(angleA, angleB);

    // So the shadowcasting covers 2 or 3 octants (90 to 135 degrees) so we
    // want to trim it to the actual cone/triangle, which I want to be about
    // 60 degrees.
    return [..affected.Where(l =>
    {
      double angle = Util.AngleBetweenLocs(origin, l);

      // Normalize the test angle if necessary
      if (angle < 0 && minAngle > Math.PI / 2)
        angle += 2 * Math.PI;

      return angle >= minAngle && angle <= maxAngle;
    })];
  }

  public static int OctantForBeam(Loc origin, Loc beam)
  {
    double angle = Util.AngleBetweenLocs(origin, beam);

    if (angle >= 0 && angle < Math.PI / 4)
      return 4;
    else if (angle >= Math.PI / 4 && angle < Math.PI / 2)
      return 5;
    else if (angle >= Math.PI / 2 && angle < 3 * Math.PI / 4)
      return 6;
    else if (angle >= 3 * Math.PI / 4 && angle <= Math.PI)
      return 7;
    else if (angle >= -Math.PI && angle < -3 * Math.PI / 4)
      return 0;
    else if (angle >= -3 * Math.PI / 4 && angle < -Math.PI / 2)
      return 1;
    else if (angle >= -Math.PI / 2 && angle < -Math.PI / 4)
      return 2;

    return 3;
  }

  static Shadow ProjectTile(int row, int col)
  {
    float topLeft = col / (row + 2.0f);
    float bottomRight = (col + 1.0f) / (row + 1.0f);

    return new Shadow(topLeft, bottomRight);
  }

  static (int, int) RotateOctant(int row, int col, int octant)
  {
    return octant switch
    {
      0 => (col, -row),
      1 => (row, -col),
      2 => (row, col),
      3 => (col, row),
      4 => (-col, row),
      5 => (-row, col),
      6 => (-row, -col),
      _ => (-col, -row),
    };
  }

  static HashSet<Loc> CalcOctant(int range, Loc origin, Map map, int octant, GameObjectDB objDb)
  {
    HashSet<Loc> affected = [];
    bool fullShadow = false;
    var line = new ShadowLine();

    for (int row = 1; row <= range; row++)
    {
      for (int col = 0; col <= row; col++)
      {
        var (dr, dc) = RotateOctant(row, col, octant);
        int r = origin.Row + dr;
        int c = origin.Col + dc;

        // The distance check trims the view area to be more round
        int d = (int)Math.Sqrt(dr * dr + dc * dc);
        if (!map.InBounds(r, c) || d > range)
          break;

        Shadow projection = ProjectTile(row, col);
        
        if (!line.IsInShadow(projection))
        {
          Loc loc = origin with { Row = r, Col = c };
          if (map.TileAt(r, c) is Door door && !door.Open)
          {
            affected.Add(loc);
            line.Add(projection);
            fullShadow = line.IsFullShadow();
          }
          else if (!map.TileAt(r, c).PassableByFlight() || objDb.BlockersAtLoc(loc))
          {
            line.Add(projection);
            fullShadow = line.IsFullShadow();
          }
          else
          {
            affected.Add(loc);
          }
        }

        if (fullShadow)
          return affected;
      }
    }

    return affected;
  }
}

public static class ResourcePath
{
  public static string GetBaseFilePath(string filename)
  {
    return FindResourcePath("", filename);
  }

  public static string GetDataFilePath(string filename)
  {
    return FindResourcePath("data", filename);
  }

  public static string GetDialogueFilePath(string filename)
  {
    return FindResourcePath("dialogue", filename);
  }

  private static string FindResourcePath(string folder, string filename)
  {
    string path = Path.Combine(folder, filename);
    if (File.Exists(path))
    {
      return path;
    }

    // If that doesn't exist and we're on macOS, try the bundle Resources path
    if (OperatingSystem.IsMacOS())
    {
      string? bundlePath = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);
      if (bundlePath != null)
      {
        string resourcePath = Path.Combine(bundlePath, folder, filename);
        if (File.Exists(resourcePath))
        {
          return resourcePath;
        }
      }
    }

    throw new FileNotFoundException(
        $"Could not find {filename} in {folder} directory. " +
        $"Tried: {path} and bundle resources path. " +
        $"Current directory: {Directory.GetCurrentDirectory()}"
    );
  }
}