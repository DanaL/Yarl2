namespace Yarl2;

internal class Dungeon
{
    readonly Random _rng = new Random();
    
    // Pick a room template to overlay onto the map (currently either 
    // rectangular or circular)
    private Tile[,] MakeRoomTemplate()
    {
        ushort height, width;
        Tile[,] sqs;
        var rn = _rng.NextDouble();
        rn = 0.81;
        if (rn < 0.8)
        {
            // make a rectangular room
            height = (ushort)_rng.Next(7, 11);
            width = (ushort)_rng.Next(7, 28);
            sqs = new Tile[height, width];
            for (ushort c = 0; c < width; c++) 
            {
                sqs[0, c] = TileFactory.Get(TileType.Wall);
                sqs[height - 1, c] = TileFactory.Get(TileType.Wall);
            }
            for (ushort r = 1; r < height - 1; r++)
            {
                sqs[r, 0] = TileFactory.Get(TileType.Wall);
                sqs[r, width - 1]= TileFactory.Get(TileType.Wall);
                for (ushort c = 1; c < width - 1; c++)
                {
                    sqs[r, c] = TileFactory.Get(TileType.Floor);
                }
            }        
        } 
        else 
        {
            // make a circular room        
            var radius = (ushort) _rng.Next(3, 7);
            height = (ushort) (radius * 2 + 3);
            width = (ushort) (radius * 2 + 3);
            sqs = new Tile[height, width];
            for (ushort r = 0; r < radius * 2 + 3; r++)
            {
                for (ushort c = 0; c < radius * 2 + 3; c++)
                {
                    sqs[r, c] = TileFactory.Get(TileType.Wall);
                }
            }

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
                sqs[rc + y, cc + x] = TileFactory.Get(TileType.Floor);
                sqs[rc + y, cc - x] = TileFactory.Get(TileType.Floor);
                sqs[rc - y, cc + x] = TileFactory.Get(TileType.Floor);
                sqs[rc - y, cc - x] = TileFactory.Get(TileType.Floor);
                sqs[rc + y, cc + x] = TileFactory.Get(TileType.Floor);
                sqs[rc + y, cc - x] = TileFactory.Get(TileType.Floor);
                sqs[rc - y, cc + x] = TileFactory.Get(TileType.Floor);
                sqs[rc - y, cc - x] = TileFactory.Get(TileType.Floor);

                y += 1;
                error += sqry_inc;
                sqry_inc += 2;
                if (error > x) {
                    x -= 1;
                    error -= sqrx_inc;
                    sqrx_inc -= 2;
                }
            }

            // Now turn all the squares inside the circle into floors
            for (short r = 1; r < height - 1; r++)
            {
                for (short c = 1; c < width - 1; c++)
                {
                    if (Util.Distance(r, c, rc, cc) <= radius)
                        sqs[r, c] = TileFactory.Get(TileType.Floor);
                }
            }            
        }

        return sqs;
    }

    private static void DrawRoom(Map map, Tile[,] tiles, ushort row, ushort col)
    {
        for (int r = 0; r < tiles.GetLength(0); r++)
        {
            for (int c = 0; c < tiles.GetLength(1); c++)
            {
                var i = (row + r) * map.Width + col + c;
                map.Tiles[i] = tiles[r, c];
            }
        }
    }

    private (short, short) FindSpotForRoom(Map map, List<Room> rooms, Tile[,] sqs, ushort height, ushort width)
    {
        int roomHeight = sqs.GetLength(0);
        int roomWidth = sqs.GetLength(1);
        short row, col;
        short dr, dc;
        ScanDirs dir;

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
                dc = 1;
                dir = ScanDirs.RightDown;
            }
            else // move down along cols
            {
                dr = 1;
                dc = 0;
                dir = ScanDirs.DownRight;
            }
        }
        else if (corner == 1) // top right
        {
            row = 0;
            col = (short) (width - roomWidth);

            // pick which direction to scan
            if (_rng.Next(2) == 0) // move left along rows
            {
                dr = 0;
                dc = -1;
                dir = ScanDirs.LeftDown;
            }
            else // move down along cols
            {
                dr = 1;
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
                dc = 1;
                dir = ScanDirs.RightUp;
            }
            else // move up along cols
            {
                dr = -1;
                dc = 0;
                dir = ScanDirs.UpRight;
            }
        }
        else // bottom right
        {
            row = (short)(height - roomHeight);
            col = (short)(width - roomWidth);

            // pick which direction to scan
            if (_rng.Next(2) == 0) // move left along rows
            {
                dr = 0;
                dc = -1;
                dir = ScanDirs.LeftUp;
            }
            else // move up along cols
            {
                dr = -1;
                dc = 0;
                dir = ScanDirs.UpLeft;
            }
        }

        // Okay, scan across the map and try to place the new room
        do
        {
            short brr = (short) (row + roomHeight);
            short brc = (short) (col + roomWidth);

            // bounds check needs to go here
            if (!(map.InBounds(row, col) && map.InBounds(row, brc) && map.InBounds(brr, col) && map.InBounds(brr, brc)))
            {
                // We've reached the end of a row or col, so try the next row/col                
                switch (dir)
                {
                    case ScanDirs.RightDown:
                        row += (short)(roomHeight);
                        col = (short)_rng.Next(3);
                        break;
                    case ScanDirs.DownRight:
                        row = (short)_rng.Next(3);
                        col += (short)(roomWidth);
                        break;
                    case ScanDirs.LeftDown:
                        row += (short)(roomHeight);
                        col = (short)(width - roomWidth);
                        break;
                    case ScanDirs.DownLeft:
                        row = (short)_rng.Next(3);
                        col -= (short)(roomWidth);
                        break;
                    case ScanDirs.RightUp:
                        row -= (short)(roomHeight);
                        col = (short)_rng.Next(3);
                        break;
                    case ScanDirs.UpRight:
                        row = (short)(height - roomHeight);
                        col += (short)(roomWidth);
                        break;
                    case ScanDirs.LeftUp:
                        row -= (short)(roomHeight);
                        col = (short)(width - roomWidth);
                        break;
                    case ScanDirs.UpLeft:
                        row = (short)(height - roomHeight);
                        col -= (short)(roomWidth + 1);
                        break;
                }

                // if we're still out of bounds, we've scanned the entire map 
                brr = (short)(row + roomHeight);
                brc = (short)(col + roomWidth);
                if (!(map.InBounds(row, col) && map.InBounds(row, brc) && map.InBounds(brr, col) && map.InBounds(brr, brc)))
                {
                    Console.WriteLine(dir);
                    break;
                }

                continue;
            }
                
            bool overlaps = rooms.Exists(r => r.Overlaps((ushort) row, (ushort) col, (ushort) brr, (ushort) brc));
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

    private void AddRooms(Map map, ushort width, ushort height)
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
        var room = new Room(sqs, row, col, (ushort)(row + sqs.GetLength(0)), 
                                    (ushort)(col + sqs.GetLength(1)), "start");
        rooms.Add(room);

        // Now keep adding rooms until we fail to add one. (Ie., we can't find a spot where
        // it won't overlap with another room
        do
        {
            sqs = MakeRoomTemplate();
            var (r, c) = FindSpotForRoom(map, rooms, sqs, height, width);
            if (r < 0)
            {
                map.Dump();
                break;
            }
            DrawRoom(map, sqs, (ushort) r, (ushort) c);
            room = new Room(sqs, (ushort) r, (ushort) c, (ushort)((ushort)r + sqs.GetLength(0)),
                                        (ushort)(c + sqs.GetLength(1)), "");
            rooms.Add(room);
        }
        while (true);
    }

    private static List<(ushort, ushort)> MazeNeighbours(Map map, ushort row, ushort col, TileType type, short d)
    {
        (short, short)[] _adj = [((short)-d, 0), (d, 0), (0, d), (0, (short)-d)];
        return _adj.Select(n => ((ushort)(row + n.Item1), (ushort)(col + n.Item2)))
                             .Where(n => map.InBounds((short)n.Item1, (short)n.Item2))
                             .Where(n => map.TileAt(n.Item1, n.Item2).Type == type).ToList();        
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

    // Lay down the initial maze. Just using the randomized Prim's algorithm description from Wikipedia
    // https://en.wikipedia.org/wiki/Maze_generation_algorithm#Iterative_randomized_Prim's_algorithm_(without_stack,_without_sets)
    private void CarveMaze(Map map, ushort width, ushort height)
    {
        var startCellRow = (ushort)(_rng.Next(height));
        var startCellCol = (ushort)(_rng.Next(width));
        map.SetTile(startCellRow, startCellCol, TileFactory.Get(TileType.Floor));
        var frontiers = MazeNeighbours(map, startCellRow, startCellCol, TileType.Wall, 2);

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

        //CarveMaze(map, width, height);        
        AddRooms(map, width, height);

        // Placing the rooms down leaves in a bunch of squares that are isolated by
        // themselves so I'll just fill them in before I do more clean up. (Maybe in the
        // future some of them can be used for closets/secret rooms?
        //for (ushort r = 0; r < height; r++)
        //{
        //    for (ushort c= 0; c < width; c++)
        //    {
        //        if (MazeNeighbours(map, r, c, TileType.Floor, 1).Count() == 0)
        //            map.SetTile(r, c, TileFactory.Get(TileType.Wall));
        //    }
        //}
        map.Dump();

        return map;
    }
}

enum ScanDirs{ RightDown, DownRight, LeftDown, DownLeft, UpRight, RightUp, LeftUp, UpLeft };

class Room(Tile[,] Tiles, ushort ULRow, ushort ULCol, ushort LRRow, ushort LRCol, string Label)
{
    public Tile[,] Tiles { get; set; } = Tiles;
    public ushort ULRow { get; set; } = ULRow;
    public ushort ULCol { get; set; } = ULCol;
    public ushort LRRow { get; set; } = LRRow;
    public ushort LRCol { get; set; } = LRCol;
    public string Label { get; set; } = Label;
    public int Height { get; } = Tiles.GetLength(0);
    public int Width { get; } = Tiles.GetLength(1);

    bool Contained(ushort r, ushort c) => r >= ULRow && r <= LRRow && c >= ULCol && c <= LRCol;

    public bool Overlaps(ushort ulr, ushort ulc, ushort lrr, ushort lrc)
    {
        for (ushort r = ulr; r <= lrr; r++)
        {
            for (ushort c = ulc; c <= lrc; c++)
            {
                if (Contained(r, c))
                    return true;
            }
        }
        return false;
    }
        
}