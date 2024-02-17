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

// Generate dungeon levels! I drew a lot upon Bob Nystrom's blog and Roguelike Basin
class DungeonMap(Random rng)
{
    readonly Random _rng = rng;
    
    // Pick a room template to overlay onto the map (currently either 
    // rectangular or circular)
    (List<(int, int)>, RoomShapes) MakeRoomTemplate()
    {
        int height, width;
        RoomShapes shape;
        List<(int, int)> sqs = new();
        var rn = _rng.NextDouble();
        
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
            int rc = radius + 1;
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

    List<Room> AddRooms(Map map)
    {
        var rooms = new List<Room>();
        var perimeters = new HashSet<(int, int)>();
        int maxTries = 75;

        for (int x = 0; x < maxTries; x++)
        {
            var (sqs, shape) = MakeRoomTemplate();
            int rh = sqs.Select(s => s.Item1).Max();
            int rw = sqs.Select(s => s.Item2).Max();

            var row =  _rng.Next(1, map.Height - rh - 1);
            if (row % 2 == 0)
                row += 1;                
            var col =  _rng.Next(1, map.Width - rw - 1);
            if (col % 2 == 0)
                col += 1;
            sqs = sqs.Select(s => (s.Item1 + row, s.Item2 + col)).ToList();
            bool overlap = false;
            foreach (var sq in sqs)
            {
                if (map.TileAt(sq.Item1, sq.Item2).Type == TileType.DungeonFloor) 
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
                map.SetTile(sq.Item1, sq.Item2, TileFactory.Get(TileType.DungeonFloor));
            }
        }

        return rooms;
    }

    static List<(int, int)> MazeNeighbours(Map map, int row, int col, TileType type, int d)
    {
        (int, int)[] adj = [(-d, 0), (d, 0), (0, d), (0,-d)];
        return adj.Select(n => (row + n.Item1, col + n.Item2))
                             .Where(n => map.InBounds(n.Item1, n.Item2))
                             .Where(n => map.TileAt(n.Item1, n.Item2).Type == type).ToList();
    }

    static int AdjFloors(Map map, int row, int col)
    {
        (int, int)[] adj = [(-1, -1), (-1, 0), (-1, 1), (0, -1), (0, 1),
                                    (1, -1), (1, 0), (1, 1)];
        return adj.Select(n => (row + n.Item1, col + n.Item2))
                             .Where(n => map.InBounds(n.Item1, n.Item2))
                             .Where(n => map.TileAt(n.Item1, n.Item2).Type == TileType.DungeonFloor).Count();
    }

    static void ConnectNeighbours(Map map, int r1, int c1, int r2, int c2)
    {
        if (r1 < r2)
            map.SetTile(r1 + 1, c1, TileFactory.Get(TileType.DungeonFloor));
        else if (r1 > r2)
            map.SetTile(r1 - 1, c1, TileFactory.Get(TileType.DungeonFloor));
        else if (c1 < c2)
            map.SetTile(r1, c1 + 1, TileFactory.Get(TileType.DungeonFloor));
        else if (c1 > c2)
            map.SetTile(r1, c1 - 1, TileFactory.Get(TileType.DungeonFloor));
    }

    static (bool, int, int) MazeStart(Map map)
    {
        for (int r = 1; r < map.Height - 1; r++) 
        {
            for (int c = 1; c < map.Width - 1; c++)
            {
                if (map.TileAt(r, c).Type == TileType.Wall && AdjFloors(map, r, c) == 0)
                    return (true, r, c);
            }
        }

        return (false, 0, 0);
    }

    static IEnumerable<(int, int)> NextNeighbours(Map map, int r, int c)
    {
        return MazeNeighbours(map, r, c, TileType.Wall, 2)
                    .Where(s => AdjFloors(map, s.Item1, s.Item2) == 0);
    }

    // Random floodfill maze passages. (We have to do this a few times)
    bool CarveMaze(Map map)
    {
        var (success, startRow, startCol) = MazeStart(map);

        if (success)
        {            
            map.SetTile(startRow, startCol, TileFactory.Get(TileType.DungeonFloor));

            // find neighbours (2 sq away) that they are fully enclosed
            var neighbours = NextNeighbours(map, startRow, startCol)
                                .Select(n => (n, (startRow, startCol))).ToList();
            while (neighbours.Count > 0)
            {
                var i = _rng.Next(neighbours.Count);
                var (next, prev) = neighbours[i];
                neighbours.RemoveAt(i);

                if (map.TileAt(next.Item1, next.Item2).Type == TileType.DungeonFloor)
                    continue;

                map.SetTile(next.Item1, next.Item2, TileFactory.Get(TileType.DungeonFloor));
                ConnectNeighbours(map, prev.Item1, prev.Item2, next.Item1, next.Item2);
                
                neighbours.AddRange(NextNeighbours(map, next.Item1, next.Item2)
                                        .Select(n => (n, (next.Item1, next.Item2))));
            }
        }

        return success;
    }

    static bool ValidDoor(Map map, int r, int c)
    {
        if (!map.InBounds(r, c))
            return false;
        
        if (map.InBounds(r-1, c) && map.InBounds(r+1, c)
                && map.TileAt(r-1, c).Type == TileType.DungeonFloor
                && map.TileAt(r+1, c).Type == TileType.DungeonFloor)
            return true;

        if (map.InBounds(r, c-1) && map.InBounds(r, c+1)
                && map.TileAt(r, c-1).Type == TileType.DungeonFloor
                && map.TileAt(r, c+1).Type == TileType.DungeonFloor)
            return true;
        
        return false;
    }
    
    static HashSet<(int, int)> FloodFillRegion(Map map, int row, int col)
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
                if (!sqs.Contains(n) && map.InBounds(nr, nc) && map.TileAt(nr, nc).Type != TileType.Wall)
                {
                    q.Enqueue(n);
                }
            }
        }

        return sqs;
    }

    public void Dump(Map map, int width, int height) 
    {
        for (int row = 0; row < height; row++)
        {
            for (int col = 0; col < width; col++)
            {
                char ch = map.TileAt(row, col).Type switch  {
                    TileType.PermWall => '#',
                    TileType.Wall => ' ',
                    TileType.DungeonFloor => '.',
                    TileType.Door => '+',
                    _ => ' '
                };
                Console.Write(ch);
            }
            Console.WriteLine();
        }
    }

    (int, int) FindDisjointFloor(Map map, Dictionary<int, HashSet<(int, int)>> regions)
    {
        for (int r = 0; r < map.Height; r++)
        {
            for (int c = 0; c < map.Width; c++)
            {
                if (map.TileAt(r, c).Type == TileType.DungeonFloor)
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

    Dictionary<int, HashSet<(int, int)>> FindRegions(Map map)
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
        foreach (var k in regions.Keys)
        {
            if (regions[k].Count <= 3)
            {
                foreach (var sq in regions[k])
                    map.SetTile(sq, TileFactory.Get(TileType.Wall));
                regions.Remove(k);
            }
        }
        return regions;
    }

    static int RegionForSq(Dictionary<int, HashSet<(int, int)>> regions, int row, int col)
    {
        foreach (var k in regions.Keys)
        {
            if (regions[k].Contains((row, col)))
                return k;
        }

        return -1;
    }

    static HashSet<int> AdjoiningRegions(Dictionary<int, HashSet<(int, int)>> regions, (int, int) con)
    {
        var adjoining = new HashSet<int>();
        int above = RegionForSq(regions, con.Item1 - 1, con.Item2);
        int below = RegionForSq(regions, con.Item1 + 1, con.Item2);
        int left = RegionForSq(regions, con.Item1, con.Item2 - 1);
        int right = RegionForSq(regions, con.Item1, con.Item2 + 1);

        if (above != -1 && below != -1) 
        {
            adjoining.Add(above);
            adjoining.Add(below);
        }
        if (left != -1 && right != -1)
        {
            adjoining.Add(left);
            adjoining.Add(right);
        }

        return adjoining;
    }

    void AddDoorToRectRoom(Map map, Room room)
    {
        while (true)
        {
            var door = room.DoorCandidate(_rng);
            if (ValidDoor(map, door.Item1, door.Item2))
            {
                map.SetTile(door, TileFactory.Get(TileType.Door));
                return;
            }
        }
    }

    // Circular rooms end up with wall at least two squares away from
    // any hallways, so they are slightly more complicated to join up
    void ConnectCircularRoom(Map map, Room room)
    {
        int minRow = int.MaxValue, minCol = int.MaxValue, maxRow = 0, maxCol = 0;
        foreach (var sq in room.Sqs)
        {
            if (sq.Item1 < minRow) minRow = sq.Item1;
            if (sq.Item2 < minCol) minCol = sq.Item2;
            if (sq.Item1 > maxRow) maxRow = sq.Item1;
            if (sq.Item2 > maxCol) maxCol = sq.Item2;
        }

        bool done = false;
        do
        {
            (int, int) delta;
            int row;
            int col;

            // Pick direction to try drawing a line in
            switch (_rng.Next(4)) 
            {
                case 0: // northward
                    row = minRow;
                    col = _rng.Next(minCol + 1, maxCol);
                    delta = (-1, 0);
                    break;
                case 1: // southward
                    row = maxRow;
                    col = _rng.Next(minCol + 1, maxCol);
                    delta = (1, 0);
                    break;
                case 2: // westward
                    row = _rng.Next(minRow + 1, maxRow);
                    col = minCol;
                    delta = (0, -1);
                    break;
                default: // eastward
                    row = _rng.Next(minRow + 1, maxRow);
                    col = maxCol;
                    delta = (0, 1);
                    break;
            }

            // Because of the irregular shape, sometimes we try
            // to start the tunnel on a wall square, which we don't want
            if (map.TileAt(row, col).Type == TileType.Wall)
                continue;
            
            bool success = false;
            var tunnel = new List<(int, int)>();
            int tunnelR = row + delta.Item1;
            int tunnelC = col + delta.Item2;
            while (map.InBounds(tunnelR, tunnelC) && map.TileAt(tunnelR, tunnelC).Type == TileType.Wall)
            {
                tunnel.Add((tunnelR, tunnelC));
                tunnelR += delta.Item1;
                tunnelC += delta.Item2;

                if (map.InBounds(tunnelR, tunnelC) && map.TileAt(tunnelR, tunnelC).Type == TileType.DungeonFloor)
                {
                    success = true;
                    break;
                }
                else if (tunnel.Count > 4)
                {
                    // We don't want an absurdly long tunnel; there should be a better candidate
                    // (theoretical infinite loop risk? I've never seen a circular room generate that
                    // is surrounded on all sides by walls 4 deep)
                    success = false;
                    break;
                }
            }

            if (success)
            {
                foreach (var sq in tunnel)
                    map.SetTile(sq, TileFactory.Get(TileType.DungeonFloor));

                // We can also make some of tunnel a door.
                if (tunnel.Count == 2 && _rng.NextDouble() < 0.75)
                {
                    map.SetTile(tunnel[0], TileFactory.Get(TileType.Door));
                }
                else if (tunnel.Count > 2)
                {
                    map.SetTile(tunnel[0], TileFactory.Get(TileType.Door));
                    map.SetTile(tunnel.Last(), TileFactory.Get(TileType.Door));
                }

                done = true;
            }
        }
        while (!done);
    }

    void RepairIsolatedRegion(Map map, Dictionary<int, HashSet<(int, int)>> regions)
    {
        // first find the smallest region
        int smallest = -1;
        int count = int.MaxValue;

        foreach (var k in regions.Keys)
        {
            if (regions[k].Count < count)
            {
                count = regions[k].Count;
                smallest = k;
            }
        }

        bool done = false;
        do
        {
            bool success = false;
            var sq = regions[smallest].ElementAt(_rng.Next(count));
            var startRegion = RegionForSq(regions, sq.Item1, sq.Item2);
            var dir = Util.Adj4[_rng.Next(4)];
            var tunnel = new List<(int, int)>();
            while (map.InBounds(sq))
            {
                tunnel.Add(sq);
                if (map.TileAt(sq).Type == TileType.DungeonFloor && RegionForSq(regions, sq.Item1, sq.Item2) != startRegion)
                {
                    success = true;
                    break;
                }
                sq = (sq.Item1 + dir.Item1, sq.Item2 + dir.Item2);
            }

            if (success)
            {
                foreach (var t in tunnel)
                    map.SetTile(t, TileFactory.Get(TileType.DungeonFloor));
                done = true;
            }
        }
        while (!done);
    }

    void ConnectRegions(Map map, List<Room> rooms)
    {
        // For rectangular rooms, each perimeter sqpare should be next to a hallway
        // (rounded rooms are more complicated)
        // So start by picking a random perimeter sq to turn into a door from each room
        foreach (var room in rooms)
        {
            if (room.Shape == RoomShapes.Rect)
                AddDoorToRectRoom(map, room);
            else
                ConnectCircularRoom(map, room);            
        }

        var regions = FindRegions(map);

        if (regions.Count > 1) 
        {
            List<(int, int)> connectors;
            var perimeters = new HashSet<(int, int)>();
            foreach (var room in rooms)
                perimeters = perimeters.Union(room.Permieter).ToHashSet();

            bool done = false;
            do
            {
                connectors = new List<(int, int)>();
                // Find the walls that are adjacent to different regions
                for (int r = 1; r < map.Height - 1; r++)
                {
                    for (int c = 1; c < map.Width - 1; c++)
                    {
                        if (map.TileAt(r, c).Type == TileType.Wall && AdjoiningRegions(regions, (r, c)).Count >= 2)
                        {
                            connectors.Add((r, c));
                        }
                    }
                }

                // Okay, sometimes a region is generated whose walls are all at least two
                // thick. Almost always this is two adjacent round rooms (Round rooms! Why
                // did I decide to implement round rooms??) who got connected which makes my 
                // code to find connectors between regions fail. So, shotgun approach: I'm going
                // to pick a square in the smaller region and erase walls until I reach a floor in
                // another region. 
                if (connectors.Count == 0)
                {
                    RepairIsolatedRegion(map, regions);
                    regions = FindRegions(map);                    
                    if (regions.Count == 1)
                        return;
                }
                else
                {
                    done = true;
                }                
            }
            while (!done);

            done = false;
            do
            {
                var con = connectors[_rng.Next(connectors.Count)];

                // I can check to see if the connector is on the perimeter of a room
                // and make it a door instead
                if (perimeters.Contains(con) && _rng.NextDouble() < 0.8)
                    map.SetTile(con, TileFactory.Get(TileType.Door));
                else
                    map.SetTile(con, TileFactory.Get(TileType.DungeonFloor));

                var adjoiningRegions = AdjoiningRegions(regions, con);
                var remaining = new List<(int, int)>();
                // Remove the connectors we don't need
                foreach (var other in connectors)
                {
                    var otherAdjoining = AdjoiningRegions(regions, other);
                    if (adjoiningRegions.Intersect(otherAdjoining).Count() > 1)
                    {
                        // small chance of removing other connector to create the occasional loop
                        // in the map
                        if (_rng.NextDouble() < 0.20)
                            map.SetTile(other, TileFactory.Get(TileType.DungeonFloor));
                    }
                    else
                    {
                        remaining.Add(other);
                    }
                }

                if (remaining.Count == 0)
                    done = true;
                else
                    connectors = remaining;
            }
            while (!done);
        }
    }

    // Clean up artifacts that my generator sometimes to creates.
    // So far the one I see is useless doors, like doors sitting in
    // open space. (I blame the round rooms which have proven very finicky)
    // We'll just turn them into open floors
    static void TidyUp(Map map)
    {
        for (int r = 1; r < map.Height - 1; r++)
        {
            for (int c = 1; c < map.Width - 1; c++)
            {
                if (map.TileAt(r, c).Type == TileType.Door)
                {
                    var north = map.TileAt(r - 1, c).Type;
                    var south = map.TileAt(r + 1, c).Type;
                    var east = map.TileAt(r, c + 1).Type;
                    var west = map.TileAt(r, c - 1).Type;

                    // I'm too dumb/tired to figure out the much-cleaner inverse logic of this right now
                    if ((north == TileType.Wall || north == TileType.PermWall || north == TileType.Door) && (south == TileType.Wall || south == TileType.PermWall || south == TileType.Door))
                        continue;
                    else if ((east == TileType.Wall || east == TileType.PermWall || east == TileType.Door) && (west == TileType.Wall || west == TileType.PermWall || west == TileType.Door))
                        continue;
                    else
                        map.SetTile(r, c, TileFactory.Get(TileType.DungeonFloor));
                }
            }
        }
    }

    // Get rid of all (most) of the tunnels that go nowhere
    static void FillInDeadEnds(Map map)
    {
        bool done;

        do
        {
            done = true;
            for (int r = 0; r < map.Height; r++)
            {
                for (int c = 0; c < map.Width; c++)
                {
                    if (map.TileAt(r, c).Type == TileType.Wall)
                        continue;

                    // If there's only one exit, it's a dead end
                    int exits = 0;
                    foreach (var adj in Util.Adj4)
                    {
                        int nr = r + adj.Item1;
                        int nc = c + adj.Item2;
                        if (map.InBounds(nr, nc) && map.TileAt(nr, nc).Type != TileType.Wall)
                            ++exits;
                    }

                    if (exits == 1)
                    {
                        map.SetTile(r, c, TileFactory.Get(TileType.Wall));
                        done = false;
                    }
                }
            }
        }
        while (!done);
    }

    public Map DrawLevel(int width, int height)
    {
        var map = new Map(width, height);

        for (int j = 0; j < width * height; j++)
            map.Tiles[j] = TileFactory.Get(TileType.Wall);
    
        var rooms = AddRooms(map);
        // Draw in the room perimeters
        foreach (var room in rooms)
        {
            foreach (var sq in room.Permieter) 
            {
                if (map.InBounds(sq.Item1, sq.Item2))
                    map.SetTile(sq.Item1, sq.Item2, TileFactory.Get(TileType.Wall));
            }
        }

        bool mazing = true;
        while (mazing)
        {
            mazing = CarveMaze(map);
        }

        ConnectRegions(map, rooms);
        FillInDeadEnds(map);

        // We want to surround the level with permanent walls
        var finalMap = new Map(width + 2, height + 2, TileType.PermWall);
        for (int r = 0; r < map.Height; r++)
        {
            for (int c= 0; c < map.Width; c++)
            {
                finalMap.SetTile(r + 1, c + 1, map.TileAt(r, c));
            }
        }

        TidyUp(finalMap);
        
        return finalMap;
    }
}

enum ScanDirs { RightDown, DownRight, LeftDown, DownLeft, UpRight, RightUp, LeftUp, UpLeft };
enum RoomShapes { Rect, Round }

class Room
{
    public RoomShapes Shape { get; set; }
    public HashSet<(int, int)> Sqs {get; set; }
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
            foreach (var n in Util.Adj4)
            {
                int nr = dr + n.Item1;
                int nc = dc + n.Item2;
                if (nr >= 0 && nc >= 0 && Sqs.Contains((nr, nc)))
                {
                    return (dr, dc);
                }                
            }
        } 
        while (true);
    }
}
