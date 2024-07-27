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

// Shadowcasting FOV code very extremely cribbed from Bob Nystrom's site:
//     https://journal.stuffwithstuff.com/2015/09/07/what-the-hero-sees/

namespace Yarl2;

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

  static HashSet<Loc> CalcOctant(int radius, Loc origin, Map map, int octant, GameObjectDB objDb)
  {
    HashSet<Loc> visibleSqs = [];
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
          visibleSqs.Add(origin with { Row = r, Col = c });

          var loc = origin with { Row = r, Col = c};
          if (map.TileAt(r, c).Opaque() || objDb.ItemsWithTrait<OpaqueTrait>(loc))
          {
            line.Add(projection);
            fullShadow = line.IsFullShadow();
          }
        }

        if (fullShadow)
          return visibleSqs;
      }
    }

    return visibleSqs;
  }

  public static HashSet<Loc> CalcVisible(int radius, Loc loc, Map map, GameObjectDB objDb)
  {
    HashSet<Loc> visible = [ loc ];
    for (int j = 0; j < 8; j++)
    {
      visible.UnionWith(CalcOctant(radius, loc, map, j, objDb));
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
