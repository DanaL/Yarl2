namespace Yarl2;

internal class Dungeon
{
    readonly Random _rng = new Random();
    
    // Pick a room template to overlay onto the map (currently either 
    // rectangular or circular)
    private List<(ushort, ushort)> MakeRoomTemplate()
    {
        ushort height, width;
        List<(ushort, ushort)> sqs = new();
        var rn = _rng.NextDouble();
        if (rn < 0.8)
        {
            // make a rectangular room
            height = (ushort)_rng.Next(5, 10);
            if (height % 2 == 0)
                ++height;
            width = (ushort)_rng.Next(5, 20);
            if (width % 2 == 0)
                ++width;
            for (ushort r = 0; r < height; r++)
            {
                for (ushort c = 0; c < width; c++)
                {
                    sqs.Add((r, c));
                }
            }
        } 
        else 
        {
            // make a circular room        
            var radius = (ushort) _rng.Next(3, 6);
            height = (ushort) (radius * 2 + 3);
            width = (ushort) (radius * 2 + 3);
            
            ushort x = radius;
            ushort y = 0;
            ushort error = 0;
            ushort sqrx_inc = (ushort) (2 * radius - 1);
            ushort sqry_inc = 1;
            short rc = (short) (radius + 1);
            short cc = (short) (radius + 1);

            // Draw the outline of a cricle via Bresenham
            while (y <= x) 
            {
                sqs.Add(((ushort)(rc + y), (ushort)(cc + x)));
                sqs.Add(((ushort)(rc + y), (ushort)(cc - x)));
                sqs.Add(((ushort)(rc - y), (ushort)(cc + x)));
                sqs.Add(((ushort)(rc - y), (ushort)(cc - x)));
                sqs.Add(((ushort)(rc + y), (ushort)(cc + x)));
                sqs.Add(((ushort)(rc + y), (ushort)(cc - x)));
                sqs.Add(((ushort)(rc - y), (ushort)(cc + x)));
                sqs.Add(((ushort)(rc - y), (ushort)(cc - x)));
                
                y += 1;
                error += sqry_inc;
                sqry_inc += 2;
                if (error > x) 
                {
                    x -= 1;
                    error -= sqrx_inc;
                    sqrx_inc -= 2;
                }
            }

            // Now turn all the squares inside the circle into floors
            for (ushort r = 1; r < height - 1; r++)
            {
                for (ushort c = 1; c < width - 1; c++)
                {
                    if (Util.Distance((short)r, (short)c, rc, cc) <= radius)
                        sqs.Add((r, c));
                }
            }            
        }

        return sqs;
    }

    private List<Room> AddRooms(Map map, ushort width, ushort height)
    {
        var rooms = new List<Room>();
        var perimeters = new HashSet<(ushort, ushort)>();
        int maxTries = 75;

        for (int x = 0; x < maxTries; x++)
        {
            IEnumerable<(ushort, ushort)> sqs = MakeRoomTemplate();
            short rh = (short)sqs.Select(s => s.Item1).Max();
            short rw = (short)sqs.Select(s => s.Item2).Max();

            var row = (ushort) _rng.Next(1, height - rh - 1);
            if (row % 2 == 0)
                row += 1;                
            var col = (ushort) _rng.Next(1, width - rw - 1);
            if (col % 2 == 0)
                col += 1;
            sqs = sqs.Select(s => ((ushort)(s.Item1 + row), (ushort)(s.Item2 + col)));
            bool overlap = false;
            foreach (var sq in sqs)
            {
                if (map.TileAt(sq.Item1, sq.Item2).Type == TileType.Floor) 
                {
                    overlap = true;
                    break;
                }
                if (perimeters.Contains(sq))
                {

                    overlap = true;
                    break;
                }             
            }
            if (overlap)
                continue;

            var room = new Room(sqs);
            rooms.Add(room);
            perimeters = perimeters.Union(room.Permieter).ToHashSet();

            foreach (var sq in sqs)
            {
                map.SetTile(sq.Item1, sq.Item2, TileFactory.Get(TileType.Floor));
            }
        }

        return rooms;
    }

    private static List<(ushort, ushort)> MazeNeighbours(Map map, ushort row, ushort col, TileType type, short d)
    {
        (short, short)[] adj = [((short)-d, 0), (d, 0), (0, d), (0, (short)-d)];
        return adj.Select(n => ((ushort)(row + n.Item1), (ushort)(col + n.Item2)))
                             .Where(n => map.InBounds((short)n.Item1, (short)n.Item2))
                             .Where(n => map.TileAt(n.Item1, n.Item2).Type == type).ToList();
    }

    private static bool AdjFloors(Map map, ushort row, ushort col)
    {
        (short, short)[] adj = [(-1, -1), (-1, 0), (-1, 1), (0, -1), (0, 1),
                                    (1, -1), (1, 0), (1, 1)];
        return adj.Select(n => ((ushort)(row + n.Item1), (ushort)(col + n.Item2)))
                             .Where(n => map.InBounds((short)n.Item1, (short)n.Item2))
                             .Where(n => map.TileAt(n.Item1, n.Item2).Type == TileType.Floor).Any();
    }

    private void MazeConnect(Map map, ushort r, ushort c)
    {
        var neighbours = MazeNeighbours(map, r, c, TileType.Floor, 2);
        if (neighbours.Count > 0)
        {
            var (nr, nc) = neighbours[_rng.Next(neighbours.Count)];
            ushort br = r, bc = c;
            if (r < nr)
                br = (ushort) (r + 1);
            else if (r > nr)
                br = (ushort)(r - 1);
            else if (c < nc)
                bc = (ushort)(c + 1);
            else if (c > nc)
                bc = (ushort)(c - 1);

            map.SetTile(br, bc, TileFactory.Get(TileType.Floor));         
        }
    }

    private (ushort, ushort) MazeStart(Map map, ushort width, ushort height)
    {
        do 
        {
            var r = (ushort)_rng.Next(height);
            var c = (ushort)_rng.Next(width);

            if (map.TileAt(r, c).Type == TileType.Wall && !AdjFloors(map, r, c))
                return (r, c);
        }
        while (true);
    }

    // Lay down the initial maze. Just using the randomized Prim's algorithm description from Wikipedia
    // https://en.wikipedia.org/wiki/Maze_generation_algorithm#Iterative_randomized_Prim's_algorithm_(without_stack,_without_sets)
    private void CarveMaze(Map map, ushort width, ushort height)
    {
        var (startRow, startCol) = MazeStart(map, width, height);
        map.SetTile(startRow, startCol, TileFactory.Get(TileType.Floor));
        var frontiers = MazeNeighbours(map, startRow, startCol, TileType.Wall, 2);
        
        while (frontiers.Count > 0) 
        {
            var i = _rng.Next(frontiers.Count);
            var (nr, nc) = frontiers[i];

            if (map.TileAt(nr, nc).Type == TileType.Wall) 
            {
                map.SetTile(nr, nc, TileFactory.Get(TileType.Floor));
                MazeConnect(map, nr, nc);
            }
                        
            frontiers.RemoveAt(i);            
            frontiers.AddRange(MazeNeighbours(map, nr, nc, TileType.Wall, 2));                                
        }        
    }

    public Map DrawLevel(ushort width, ushort height)
    {
        var map = new Map(width, height);

        for (short j = 0; j < width * height; j++)
            map.Tiles[j] = TileFactory.Get(TileType.Wall);
    
        var rooms = AddRooms(map, width, height);
        // Draw in the room perimeters
        foreach (var room in rooms)
        {
            foreach (var sq in room.Permieter) 
            {
                if (map.InBounds((short)sq.Item1, (short)sq.Item2))
                    map.SetTile(sq.Item1, sq.Item2, TileFactory.Get(TileType.Wall));
            }
        }

       
        map.Dump();
        Console.WriteLine();

        //CarveMaze(map, width, height);
        //map.Dump();

        return map;
    }
}

enum ScanDirs { RightDown, DownRight, LeftDown, DownLeft, UpRight, RightUp, LeftUp, UpLeft };

class Room
{
    HashSet<(ushort, ushort)> Sqs {get; set; }
    public HashSet<(ushort, ushort)> Permieter { get; set; }

    public Room(IEnumerable<(ushort, ushort)> sqs) 
    {
        Sqs = new HashSet<(ushort, ushort)>(sqs);

        ushort minRow = ushort.MaxValue, maxRow = 0;
        ushort minCol = ushort.MaxValue, maxCol = 0;
        foreach (var sq in sqs)
        {
            if (sq.Item1 < minRow)
                minRow = sq.Item1;
            if (sq.Item1 > maxRow)
                maxRow = sq.Item1;
            if (sq.Item2 > maxCol)
                maxCol = sq.Item2;
            if (sq.Item2 < minCol)
                minCol = sq.Item2;
        }

        minRow = minRow == 0 ? minRow : (ushort)(minRow - 1);
        maxRow += 1;
        minCol = minCol == 0 ? minCol : (ushort)(minCol - 1);
        maxCol += 1;

        Permieter = new();
        for (ushort r = minRow; r <= maxRow; r++) 
        {
            for (ushort c = minCol; c <= maxCol; c++) 
            {
                if (!Sqs.Contains((r, c)))
                    Permieter.Add((r, c));
            }
        }
    }

    public bool Overlaps(Room other)
    {
        return Permieter.Intersect(other.Permieter).Count() > 0 || 
            Permieter.Intersect(other.Sqs).Count() > 0;
    }
}