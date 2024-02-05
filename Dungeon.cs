namespace Yarl2;

internal class Dungeon
{
    readonly Random _rng = new Random();

    // Pick a room template to overlay onto the map (currently either 
    // rectangular or circular)
    private Tile[,] PickRoom()
    {
        ushort height, width;
        Tile[,] sqs;
        var rn = _rng.NextDouble();
 
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

    private void DrawRoom(Map map, Tile[,] tiles, ushort row, ushort col)
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

    private void Carve(Map map, ushort width, ushort height)
    {
        var rooms = new List<Room>();
        var center_row = height / 2;
        var center_col = width / 2;
        var row = center_row + _rng.Next(-6, 6);
        var col = center_col + _rng.Next(-10, 10);

        // Draw the starting room to the dungeon map. (This is just the first room we make on the
        // level, not necessaily the entrance room)
        var sqs = PickRoom();
        DrawRoom(map, sqs, (ushort) row, (ushort) col);
        var room = new Room(sqs, (ushort)row, (ushort)col, (ushort)(row + sqs.GetLength(0)), 
                                    (ushort)(col + sqs.GetLength(1)), "start");
        rooms.Add(room);
    }

    public Map DrawLevel(ushort width, ushort height)
    {
        var map = new Map(width, height);

        for (short j = 0; j < width * height; j++)
            map.Tiles[j] = TileFactory.Get(TileType.Wall);

        Carve(map, width, height);

        return map;
    }
}

record Room(Tile[,] Tiles, ushort ULRow, ushort ULCol, ushort LRRow, ushort LRCol, string Label);