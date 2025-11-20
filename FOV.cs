// Delve - A roguelike computer RPG
// Written in 2024 by Dana Larose <ywg.dana@gmail.com>
//
// To the extent possible under law, the author(s) have dedicated all copyright
// and related and neighboring rights to this software to the public domain
// worldwide. This software is distributed without any warranty.
//
// You should have received a copy of the CC0 Public Domain Dedication along 
// with this software. If not, 
// see <http://creativecommons.org/publicdomain/zero/1.0/>.

// Shadowcasting FOV code very extremely cribbed from Bob Nystrom's site:
//     https://journal.stuffwithstuff.com/2015/09/07/what-the-hero-sees/

namespace Yarl2;

static class Illumination
{
  public const int None = 0;
  public const int NE = 0b0001;
  public const int NW = 0b0010;
  public const int SE = 0b0100;
  public const int SW = 0b1000;
  public const int Full = 0b1111;
}

class Shadow(float start, float end)
{
  public float Start { get; set; } = start;
  public float End { get; set; } = end;

  public bool Contains(Shadow other) => Start <= other.Start && End >= other.End;
}

class ShadowLine
{
  readonly List<Shadow> _shadows = [];

  public bool IsFullShadow() => _shadows.Count == 1 && _shadows[0].Start == 0 && _shadows[0].End == 1;
  public bool IsInShadow(Shadow projection) => _shadows.Exists(s => s.Contains(projection));

  public void Add(Shadow shadow)
  {
    int index = 0;

    for (; index < _shadows.Count; index++)
    {
      if (_shadows[index].Start >= shadow.Start)
        break;
    }

    Shadow? overlappingPrevious = null;
    if (index > 0 && _shadows[index - 1].End > shadow.Start)
    {
      overlappingPrevious = _shadows[index - 1];
    }

    Shadow? overlappingNext = null;
    if (index < _shadows.Count && _shadows[index].Start < shadow.End)
    {
      overlappingNext = _shadows[index];
    }

    if (overlappingNext is not null)
    {
      if (overlappingPrevious is not null)
      {
        overlappingPrevious.End = overlappingNext.End;
        _shadows.RemoveAt(index);
      }
      else
      {
        overlappingNext.Start = shadow.Start;
      }
    }
    else if (overlappingPrevious is not null)
    {
      overlappingPrevious.End = shadow.End;
    }
    else
    {
      _shadows.Insert(index, shadow);
    }
  }
}

class FieldOfView
{
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

  static Dictionary<Loc, int> CalcOctant(int radius, Loc origin, Map map, int octant, GameObjectDB objDb, Dictionary<Loc, bool> opaqueLocs)
  {
    Dictionary<Loc, int> visibleSqs = [];
    bool fullShadow = false;
    var line = new ShadowLine();

    for (int row = 1; row <= radius; row++)
    {
      for (int col = 0; col <= row; col++)
      {
        var (dr, dc) = RotateOctant(row, col, octant);
        int r = origin.Row + dr;
        int c = origin.Col + dc;

        // The distance check trims the view area to be more round
        int d = (int)Math.Sqrt(dr * dr + dc * dc);
        if (!map.InBounds(r, c) || d > radius)
          break;

        var projection = ProjectTile(row, col);
        if (!line.IsInShadow(projection))
        {
          int illum;
          Loc loc = origin with { Row = r, Col = c};
          if (IsOpaque(loc, origin, map, objDb, opaqueLocs))
          {
            line.Add(projection);
            fullShadow = line.IsFullShadow();
            illum = CalcIllumination(loc, origin, map, objDb, opaqueLocs);            
          }
          else
          {
            illum = Illumination.Full;
          }
          
          if (!visibleSqs.TryAdd(loc, illum))
          {
            visibleSqs[loc] |= illum;
          }
        }

        if (fullShadow)
          return visibleSqs;
      }
    }

    return visibleSqs;
  }

  // Calculate which corneres of a tile are illuminated. Opaque squares are only lit on their
  // corner relative to the light source.
  // The extra complicated cases are the corners of a room
  //
  // ....#
  // ..@.#
  // #####
  // 
  // In this case, the @ can see the NW corner of the corner tile. If there is a light source
  // on the other side of the wall, it would be illuminating the SW corner of the corner tile.
  // (And the player wouldn't see it as lit, assuming their own light source is radius 1)
  static int CalcIllumination(Loc pt, Loc origin, Map map, GameObjectDB objDb, Dictionary<Loc, bool> opaqueLocs)
  {
    int illum = Illumination.None;
    if (pt.Row > origin.Row) 
    {
      Loc sqAbove = pt with { Row = pt.Row - 1};
      bool opaqueAbove = IsOpaque(sqAbove, origin, map, objDb, opaqueLocs);
      if (!opaqueAbove)
        illum |= Illumination.NE | Illumination.NW;
          
      if (opaqueAbove && pt.Col > origin.Col)
      {
        Loc sqLeft = pt with { Col = pt.Col - 1};
        Loc sqNW = pt with { Row = pt.Row - 1, Col = pt.Col - 1};
        if (IsOpaque(sqLeft, origin, map, objDb, opaqueLocs) && !IsOpaque(sqNW, origin, map, objDb, opaqueLocs))
          illum |= Illumination.NW;
      }
      else if (opaqueAbove && pt.Col < origin.Col)
      {
        Loc sqRight = pt with { Col = pt.Col + 1};
        Loc sqNE = pt with { Row = pt.Row - 1, Col = pt.Col + 1};
        if (IsOpaque(sqRight, origin, map, objDb, opaqueLocs) && !IsOpaque(sqNE, origin, map, objDb, opaqueLocs))
          illum |= Illumination.NE;
      }
    }
          
    if (pt.Row < origin.Row)
    {
      Loc locBelow = pt with { Row = pt.Row + 1};
      bool opaqueBelow = IsOpaque(locBelow, origin, map, objDb, opaqueLocs);
      if (!opaqueBelow)
        illum |= Illumination.SE | Illumination.SW;

      if (opaqueBelow && pt.Col > origin.Col)
      {
        Loc sqLeft = pt with { Col = pt.Col - 1};
        Loc sqSW = pt with { Row = pt.Row + 1, Col = pt.Col - 1};
        if (IsOpaque(sqLeft, origin, map, objDb, opaqueLocs) && !IsOpaque(sqSW, origin, map, objDb, opaqueLocs))
          illum |= Illumination.SW;
      }
      else if (opaqueBelow && pt.Col < origin.Col)
      {
        Loc sqRight = pt with { Col = pt.Col + 1};
        Loc sqSE = pt with { Row = pt.Row + 1, Col = pt.Col + 1};
        if (IsOpaque(sqRight, origin, map, objDb, opaqueLocs) && !IsOpaque(sqSE, origin, map, objDb, opaqueLocs))
           illum |= Illumination.SE;
      }
    }

    // We don't have to check for corners here because they should be covered by the above checks
    
    if (pt.Col > origin.Col && !IsOpaque(pt with { Col = pt.Col - 1}, origin, map, objDb, opaqueLocs))
      illum |= Illumination.NW | Illumination.SW;
    else if (pt.Col < origin.Col && !IsOpaque(pt with { Col = pt.Col + 1}, origin, map, objDb, opaqueLocs))
      illum |= Illumination.NE | Illumination.SE;
 
    return illum;
  }
  
  static bool IsOpaque(Loc loc, Loc origin, Map map, GameObjectDB objDb, Dictionary<Loc, bool> opaqueLocs)
  {
    if (opaqueLocs.TryGetValue(loc, out bool opacity))
      return opacity;

    if (map.TileAt(loc.Row, loc.Col).Opaque())
    {
      opacity = true;      
    }

    int d = Util.Distance(loc, origin);
    if (d >= objDb.VisibilityAtLocation(loc))
      opacity = true;
    
    if (opacity)
      opaqueLocs.Add(loc, true);

    return opacity;
  }

  public static Dictionary<Loc, int> CalcVisible(int radius, Loc loc, Map map, GameObjectDB objDb)
  {
    Dictionary<Loc, bool> opaqueLocs = [];
    Dictionary<Loc, int> visible = [];
    visible.Add(loc, Illumination.Full);

    for (int j = 0; j < 8; j++)
    {
      Dictionary<Loc, int> octant = CalcOctant(radius, loc, map, j, objDb, opaqueLocs);
      foreach (var sq in octant)
      {
        if (!visible.TryAdd(sq.Key, sq.Value))
        {
          visible[sq.Key] |= sq.Value;
        }
      }
    }

    return visible;
  }

  static Shadow ProjectTile(int row, int col)
  {
    float topLeft = col / (row + 2.0f);
    float bottomRight = (col + 1.0f) / (row + 1.0f);

    return new Shadow(topLeft, bottomRight);
  }
}
