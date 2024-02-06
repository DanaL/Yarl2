using System.Security;

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
            height = (ushort)_rng.Next(6, 10);
            width = (ushort)_rng.Next(6, 20);
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

    private static void DrawRoom(Map map, List<(ushort, ushort)> sqs, ushort row, ushort col)
    {
        foreach (var sq in sqs)
        {
            map.SetTile((ushort)(row + sq.Item1), (ushort)(col + sq.Item2), TileFactory.Get(TileType.Floor));
        }
    }

    private (short, short) FindSpotForRoom(Map map, List<Room> rooms, List<(ushort, ushort)> sqs, ushort height, ushort width)
    {
        ushort roomHeight = sqs.Select(s => s.Item1).Max();
        ushort roomWidth = sqs.Select(s => s.Item2).Max();
        short row, col;
        short dr, dc;
        ScanDirs dir;
        short delta = 3; // how much to jump by when scanning

        // Pick a corner to start at and direction to move in
        int corner = _rng.Next(4);
        if (corner == 0) // top left
        {
            row = 0;
            col = 0;

            // pick which direction to scan
            if (_rng.Next(2) == 0) // move right along rows
            {
                dr = 0;
                dc = delta;
                dir = ScanDirs.RightDown;
            }
            else // move down along cols
            {
                dr = delta;
                dc = 0;
                dir = ScanDirs.DownRight;
            }
        }
        else if (corner == 1) // top right
        {
            row = 0;
            col = (short) (width - roomWidth - 1);

            // pick which direction to scan
            if (_rng.Next(2) == 0) // move left along rows
            {
                dr = 0;
                dc = (short)-delta;
                dir = ScanDirs.LeftDown;
            }
            else // move down along cols
            {
                dr = delta;
                dc = 0;
                dir = ScanDirs.DownLeft;
            }
        }
        else if (corner == 2) // bottom left
        {
            row = (short)(height - roomHeight);
            col = 0;

            // pick which direction to scan
            if (_rng.Next(2) == 0) // move right along rows
            {
                dr = 0;
                dc = delta;
                dir = ScanDirs.RightUp;
            }
            else // move up along cols
            {
                dr = (short)-delta;
                dc = 0;
                dir = ScanDirs.UpRight;
            }
        }
        else // bottom right
        {
            row = (short)(height - roomHeight - 1);
            col = (short)(width - roomWidth - 1);

            // pick which direction to scan
            if (_rng.Next(2) == 0) // move left along rows
            {
                dr = 0;
                dc = (short)-delta;
                dir = ScanDirs.LeftUp;
            }
            else // move up along cols
            {
                dr = (short)-delta;
                dc = 0;
                dir = ScanDirs.UpLeft;
            }
        }

        // Okay, scan across the map and try to place the new room
        do
        {
            short brr = (short) (row + roomHeight);
            short brc = (short) (col + roomWidth);

            if (!(map.InBounds(row, col) && map.InBounds(row, brc) && map.InBounds(brr, col) && map.InBounds(brr, brc)))
            {
                // We've reached the end of a row or col, so try the next row/col                
                switch (dir)
                {
                    case ScanDirs.RightDown:
                        row += (short)(roomHeight + 1);
                        col = 0;
                        break;
                    case ScanDirs.DownRight:
                        row = 0;
                        col += (short)(roomWidth + 1);
                        break;
                    case ScanDirs.LeftDown:
                        row += (short)roomHeight;
                        col = (short)(width - roomWidth - 1);
                        break;
                    case ScanDirs.DownLeft:
                        row = (short)_rng.Next(3);
                        col -= (short)roomWidth;
                        break;
                    case ScanDirs.RightUp:
                        row -= (short)(roomHeight);
                        col = (short)_rng.Next(3);
                        break;
                    case ScanDirs.UpRight:
                        row = (short)(height - roomHeight - 1);
                        col += (short)roomWidth;
                        break;
                    case ScanDirs.LeftUp:
                        row -= (short)roomHeight;
                        col = (short)(width - roomWidth - 1);
                        break;
                    case ScanDirs.UpLeft:
                        row = (short)(height - roomHeight - 1);
                        col -= (short)(roomWidth + 1);
                        break;
                }

                // if we're still out of bounds, we've scanned the entire map
                brr = (short)(row + roomHeight);
                brc = (short)(col + roomWidth);
                if (!(map.InBounds(row, col) && map.InBounds(row, brc) && map.InBounds(brr, col) && map.InBounds(brr, brc)))
                {
                    break;
                }

                continue;
            }
            
            var newRoom = new Room(sqs.Select(s => ((ushort)(s.Item1 + row), (ushort)(s.Item2 + col))));
            bool overlaps = rooms.Exists(r => r.Overlaps(newRoom));
            if (!overlaps)
            {
                return (row, col);
            }

            row += dr;
            col += dc;
        }
        while (true);

        return (-1, -1);
    }

    private List<Room> AddRooms(Map map, ushort width, ushort height)
    {
        var rooms = new List<Room>();
        var center_row = height / 2;
        var center_col = width / 2;
        var row = (ushort) (center_row + _rng.Next(-6, 6));
        var col = (ushort) (center_col + _rng.Next(-10, 10));

        // Draw the starting room to the dungeon map. (This is just the first room we make on the
        // level, not necessaily the entrance room)
        var sqs = MakeRoomTemplate();
        DrawRoom(map, sqs, row, col);
        var roomSqs = sqs.Select(s => ((ushort)(s.Item1 + row), (ushort)(s.Item2 + col)));
        var room = new Room(roomSqs);
        rooms.Add(room);

        // Now keep adding rooms until we fail to add one. (Ie., we can't find a spot where
        // it won't overlap with another room
        do
        {
            sqs = MakeRoomTemplate();
            var (r, c) = FindSpotForRoom(map, rooms, sqs, height, width);
            if (r < 0)
            {                
                break;
            }
            DrawRoom(map, sqs, (ushort) r, (ushort) c);
            roomSqs = sqs.Select(s => ((ushort)(s.Item1 + r), (ushort)(s.Item2 + c)));
            rooms.Add(new Room(roomSqs));            
        }
        while (true);

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

        // Fill in any dead ends
        // for (ushort r = 0; r < height; r++)
        // {
        //     for (ushort c = 0; c < width; c++)
        //     {
        //         if (MazeNeighbours(map, r, c, TileType.Wall, 1).Count >= 3)
        //             map.SetTile(r, c, TileFactory.Get(TileType.Wall));
        //     }
        // }
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