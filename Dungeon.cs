namespace Yarl2;

internal class Dungeon
{
    readonly Random _rng = new Random();
    
    // Pick a room template to overlay onto the map (currently either 
    // rectangular or circular)
    private (List<(int, int)>, RoomShapes) MakeRoomTemplate()
    {
        int height, width;
        RoomShapes shape;
        List<(int, int)> sqs = new();
        var rn = _rng.NextDouble();
        rn = 0.5;
        if (rn < 0.8)
        {
            // make a rectangular room
            shape = RoomShapes.Rect;
            height = _rng.Next(5, 10);
            if (height % 2 == 0)
                ++height;
            width = _rng.Next(5, 20);
            if (width % 2 == 0)
                ++width;
            for (int r = 0; r < height; r++)
            {
                for (int c = 0; c < width; c++)
                {
                    sqs.Add((r, c));
                }
            }
        } 
        else 
        {
            // make a circular room       
            shape = RoomShapes.Round; 
            var radius = _rng.Next(3, 6);
            height = radius * 2 + 3;
            width = radius * 2 + 3;
            
            int x = radius;
            int y = 0;
            int error = 0;
            int sqrx_inc = 2 * radius - 1;
            int sqry_inc = 1;
            int rc = (short) (radius + 1);
            int cc = radius + 1;

            // Draw the outline of a cricle via Bresenham
            while (y <= x) 
            {
                sqs.Add((rc + y, cc + x));
                sqs.Add((rc + y, cc - x));
                sqs.Add((rc - y, cc + x));
                sqs.Add((rc - y, cc - x));
                sqs.Add((rc + y, cc + x));
                sqs.Add((rc + y, cc - x));
                sqs.Add((rc - y, cc + x));
                sqs.Add((rc - y, cc - x));
                
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
            for (int r = 1; r < height - 1; r++)
            {
                for (int c = 1; c < width - 1; c++)
                {
                    if (Util.Distance(r, c, rc, cc) <= radius)
                        sqs.Add((r, c));
                }
            }            
        }

        return (sqs, shape);
    }

    private List<Room> AddRooms(Map map, int width, int height)
    {
        var rooms = new List<Room>();
        var perimeters = new HashSet<(int, int)>();
        int maxTries = 75;

        for (int x = 0; x < maxTries; x++)
        {
            var (sqs, shape) = MakeRoomTemplate();
            short rh = (short)sqs.Select(s => s.Item1).Max();
            short rw = (short)sqs.Select(s => s.Item2).Max();

            var row =  _rng.Next(1, height - rh - 1);
            if (row % 2 == 0)
                row += 1;                
            var col =  _rng.Next(1, width - rw - 1);
            if (col % 2 == 0)
                col += 1;
            sqs = sqs.Select(s => (s.Item1 + row, s.Item2 + col)).ToList();
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

            var room = new Room(sqs, shape);
            rooms.Add(room);
            perimeters = perimeters.Union(room.Permieter).ToHashSet();

            foreach (var sq in sqs)
            {
                map.SetTile(sq.Item1, sq.Item2, TileFactory.Get(TileType.Floor));
            }
        }

        return rooms;
    }

    private static List<(int, int)> MazeNeighbours(Map map, int row, int col, TileType type, short d)
    {
        (short, short)[] adj = [((short)-d, 0), (d, 0), (0, d), (0, (short)-d)];
        return adj.Select(n => (row + n.Item1, col + n.Item2))
                             .Where(n => map.InBounds((short)n.Item1, (short)n.Item2))
                             .Where(n => map.TileAt(n.Item1, n.Item2).Type == type).ToList();
    }

    private static int AdjFloors(Map map, int row, int col)
    {
        (short, short)[] adj = [(-1, -1), (-1, 0), (-1, 1), (0, -1), (0, 1),
                                    (1, -1), (1, 0), (1, 1)];
        return adj.Select(n => (row + n.Item1, col + n.Item2))
                             .Where(n => map.InBounds((short)n.Item1, (short)n.Item2))
                             .Where(n => map.TileAt(n.Item1, n.Item2).Type == TileType.Floor).Count();
    }

    private void ConnectNeighbours(Map map, int r1, int c1, int r2, int c2)
    {
        if (r1 < r2)
            map.SetTile(r1 + 1, c1, TileFactory.Get(TileType.Floor));
        else if (r1 > r2)
            map.SetTile(r1 - 1, c1, TileFactory.Get(TileType.Floor));
        else if (c1 < c2)
            map.SetTile(r1, c1 + 1, TileFactory.Get(TileType.Floor));
        else if (c1 > c2)
            map.SetTile(r1, c1 - 1, TileFactory.Get(TileType.Floor));
    }

    private static (bool, int, int) MazeStart(Map map, int width, int height)
    {
        for (int r = 1; r < height - 1; r++) 
        {
            for (int c = 1; c < width - 1; c++)
            {
                if (map.TileAt(r, c).Type == TileType.Wall && AdjFloors(map, r, c) == 0)
                    return (true, r, c);
            }
        }

        return (false, 0, 0);
    }   

    private IEnumerable<(int, int)> NextNeighbours(Map map, int r, int c)
    {
        return MazeNeighbours(map, r, c, TileType.Wall, 2)
                    .Where(s => AdjFloors(map, s.Item1, s.Item2) == 0);
    }

    // Random floodfill maze passages. (We have to do this a few times)
    private bool CarveMaze(Map map, int width, int height)
    {
        var (success, startRow, startCol) = MazeStart(map, width, height);

        if (success)
        {            
            map.SetTile(startRow, startCol, TileFactory.Get(TileType.Floor));

            // find neighbours (2 sq away) that they are fully enclosed
            var neighbours = NextNeighbours(map, startRow, startCol)
                                .Select(n => (n, (startRow, startCol))).ToList();
            while (neighbours.Count > 0)
            {
                var i = _rng.Next(neighbours.Count);
                var (next, prev) = neighbours[i];
                neighbours.RemoveAt(i);

                if (map.TileAt(next.Item1, next.Item2).Type == TileType.Floor)
                    continue;

                map.SetTile(next.Item1, next.Item2, TileFactory.Get(TileType.Floor));
                ConnectNeighbours(map, prev.Item1, prev.Item2, next.Item1, next.Item2);
                
                neighbours.AddRange(NextNeighbours(map, next.Item1, next.Item2)
                                        .Select(n => (n, (next.Item1, next.Item2))));
            }
        }

        return success;
    }

    private static bool ValidDoor(Map map, int r, int c)
    {
        if (!map.InBounds(r, c))
            return false;
        
        if (map.InBounds(r-1, c) && map.InBounds(r+1, c)
                && map.TileAt(r-1, c).Type == TileType.Floor
                && map.TileAt(r+1, c).Type == TileType.Floor)
            return true;

        if (map.InBounds(r, c-1) && map.InBounds(r, c+1)
                && map.TileAt(r, c-1).Type == TileType.Floor
                && map.TileAt(r, c+1).Type == TileType.Floor)
            return true;
        
        return false;
    }

    private void ConnectRegions(Map map, int width, int height, List<Room> rooms)
    {
        // For rectangular rooms, each perimeter sqpare should be next to a hallway
        // (rounded rooms are more complicated)
        // So start by picking a random perimeter sq to turn into a door from each room
        foreach (var room in rooms.Where(r => r.Shape == RoomShapes.Rect))
        {
            while (true)
            {
                var door = room.DoorCandidate(_rng);
                if (ValidDoor(map, door.Item1, door.Item2)) 
                {
                    map.SetTile(door.Item1, door.Item2, TileFactory.Get(TileType.Door));
                    break;
                }
            }
        }
    }

    public Map DrawLevel(int width, int height)
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

        bool mazing = true;
        while (mazing)
        {
            mazing = CarveMaze(map, width, height);            
        }
        
        ConnectRegions(map, width, height, rooms);

        map.Dump();
        Console.WriteLine();

        return map;
    }
}

enum ScanDirs { RightDown, DownRight, LeftDown, DownLeft, UpRight, RightUp, LeftUp, UpLeft };
enum RoomShapes { Rect, Round }

class Room
{
    public RoomShapes Shape { get; set; }
    HashSet<(int, int)> Sqs {get; set; }
    public HashSet<(int, int)> Permieter { get; set; }

    public Room(IEnumerable<(int, int)> sqs, RoomShapes shape) 
    {
        Shape = shape;
        Sqs = new HashSet<(int, int)>(sqs);

        int minRow = int.MaxValue, maxRow = 0;
        int minCol = int.MaxValue, maxCol = 0;
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

        minRow = minRow == 0 ? minRow : (minRow - 1);
        maxRow += 1;
        minCol = minCol == 0 ? minCol : (minCol - 1);
        maxCol += 1;

        Permieter = [];
        for (int r = minRow; r <= maxRow; r++) 
        {
            for (int c = minCol; c <= maxCol; c++) 
            {
                if (!Sqs.Contains((r, c)))
                    Permieter.Add((r, c));
            }
        }
    }

    public bool Overlaps(Room other)
    {
        return Permieter.Intersect(other.Permieter).Any() || 
            Permieter.Intersect(other.Sqs).Any();
    }

    public (int, int) DoorCandidate(Random rng)
    {
        do
        {
            var (dr, dc) = Permieter.ElementAt(rng.Next(Permieter.Count));
            (short, short)[] adj = [(-1, 0), (1, 0), (0, -1), (0, 1)];
            foreach (var n in adj)
            {
                short nr = (short) (dr + n.Item1);
                short nc = (short) (dc + n.Item2);
                if (nr >= 0 && nc >= 0 && Sqs.Contains((nr, nc)))
                {
                    return (dr, dc);
                }                
            }
        } 
        while (true);
    }
}