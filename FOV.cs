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
    private readonly List<Shadow> _shadows = new();

    public bool IsFullShadow() => _shadows.Count == 1 && _shadows[0].Start == 0 && _shadows[0].End == 1;

    public bool IsInShadow(Shadow projection)
    {
        foreach (var shadow in _shadows) 
        { 
            if (shadow.Contains(projection)) 
                return true;
        }

        return false;
    }

    public void Add(Shadow shadow)
    {
        var index = 0;

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
                overlappingPrevious.End = overlappingNext.Start;
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

internal class FieldOfView
{
    private static (short, short) RotateOctant(short row, short col, short octant)
    {
        return octant switch
        {
            0 => (col, (short)(-row)),
            1 => (row, (short)(-col)),
            2 => (row, col),
            3 => (col, row),
            4 => ((short)(-col), row),
            5 => ((short)(-row), col),
            6 => ((short) (-row), (short)(-col)),
            _ => ((short)(-col), (short)(-row)),
        };
    }

    private static HashSet<(ushort, ushort)> CalcOctant(Actor actor, Map map, short octant)
    {
        var visibleSqs = new HashSet<(ushort, ushort)>();

        bool fullShadow = false;
        var line = new ShadowLine();
        
        for (short row = 1; row < actor.CurrVisionRadius; row++)
        {
            for (short col = 0; col <= row; col++)
            {
                var (dr, dc) = RotateOctant(row, col, octant);
                ushort r = (ushort)(actor.Row + dr);
                ushort c = (ushort)(actor.Col + dc);

                if (!map.InBounds(r, c))
                    break;

                var projection = ProjectTile(row, col);
                bool visible = !line.IsInShadow(projection);

                if (visible)
                    visibleSqs.Add((r, c));

                var tile = map.TileAt(r, c);
                if (visible && tile.Opaque())
                {
                    line.Add(projection);
                    fullShadow = line.IsFullShadow();
                }

                if (fullShadow)
                    return visibleSqs;
            }
        }

        return visibleSqs;
    }

    public static HashSet<(ushort, ushort)> CalcVisible(Actor actor, Map map)
    {
        var visibleSqs = new HashSet<(ushort, ushort)>() { (actor.Row, actor.Col) };

        return visibleSqs
                .Union(CalcOctant(actor, map, 0))
                .Union(CalcOctant(actor, map, 1))
                .Union(CalcOctant(actor, map, 2))
                .Union(CalcOctant(actor, map, 3))
                .Union(CalcOctant(actor, map, 4))
                .Union(CalcOctant(actor, map, 5))
                .Union(CalcOctant(actor, map, 6))
                .Union(CalcOctant(actor, map, 7))                
                .ToHashSet();
    }

    private static Shadow ProjectTile(short row, short col)
    {            
        float topLeft = col / (row + 2.0f);
        float bottomRight = (col + 1.0f) / (row + 1.0f);

        return new Shadow(topLeft, bottomRight);
    }
}
