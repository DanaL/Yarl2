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

// Eventually this will addd NPCs, special features, etc. For now let's
// just get the town buildings drawn on the map

enum BuildingType
{
    Shrine,
    Home,
    Tavern,
    Market,
    Smithy
}

class Town
{
    public HashSet<(int, int)> Shrine { get; set; } = [];
    public HashSet<(int, int)> Tavern { get; set; } = [];
    public HashSet<(int, int)> Market { get; set; } = [];
    public HashSet<(int, int)> Smithy { get; set; } = [];
    public List<HashSet<(int, int)>> Homes { get; set; } = [];
    public List<int> TakenHomes { get; set; } = [];
    public HashSet<Loc> TownSquare { get; set; } = [];
}

class TownBuilder
{
    const int TOWN_HEIGHT = 36;
    const int TOWN_WIDTH = 60;

    public (int, int) TownCentre { get; set; }
    public Town Town { get; set; }
    Dictionary<string, Template> Templates { get; set; } = [];

    public TownBuilder() => Town = new Town();

    // This requires the templates to be squares and while I was writing this code I
    // was too dumb to figure out how to rotate a rectangle so for now I'm going to 
    // stick with square building templates :P
    char[] Rotate(char[] sqs, int width)
    {
        var indices = ListUtils.Filled(0, width * width);
        var rotated = ListUtils.Filled('`', width * width);
        
        for (int i = 0; i < width * width; i++)
        {
            if (i < width)
                indices[i] = i * width + width - 1;
            else
                indices[i] = indices[i - width] - 1;
        }

        foreach (var i in indices)
        {
            char c;
            if (sqs[i] == '|')
                c = '-';
            else if (sqs[i] == '-')
                c = '|';
            else
                c = sqs[i];
            rotated[indices[i]] = c;
        }

        return rotated.ToArray();
    }

    void DrawBuilding(Map map, int row, int col, int townRow, int townCol, Template t, BuildingType building, Random rng)
    {
        bool isWood = rng.NextDouble() < 0.7;
        HashSet<(int, int)> sqs = [];
        char[] buildingSqs = t.Sqs.Select(sq => sq).ToArray();

        // rotate would go here
        if (!t.NoRotate)
        {
            int centreRow = row + t.Height / 2;
            int centreCol = col + t.Width / 2;
            int quarter = TOWN_HEIGHT / 4;
            int northQuarter = townRow + quarter;
            int southQuarter = townCol + quarter + quarter;
            int mid = townCol + TOWN_WIDTH / 2;

            if (centreRow >= southQuarter)
            {
                // rotate doors to face north
                buildingSqs = Rotate(buildingSqs, t.Width);
                buildingSqs = Rotate(buildingSqs, t.Width);
            }
            else if (centreRow > northQuarter && centreCol < mid)
            {
                // rotate doors to face east
                buildingSqs = Rotate(buildingSqs, t.Width);
                buildingSqs = Rotate(buildingSqs, t.Width);
                buildingSqs = Rotate(buildingSqs, t.Width);
            }
            else if (centreRow > northQuarter && centreCol > mid)
            {
                // rotate doors to face west
                buildingSqs = Rotate(buildingSqs, t.Width);
            }
        }
        
        for (int r = 0; r < t.Height; r++) 
        { 
            for (int c = 0; c < t.Width; c++) 
            {
                int currRow = row + r;
                int currCol = col + c;
                var tileType = buildingSqs[r * t.Width + c] switch
                {
                    '#' => isWood ? TileType.WoodWall : TileType.Wall,
                    '`' => TileType.Grass,
                    '+' => TileType.Door,
                    '|' => TileType.VWindow,
                    '-' => TileType.HWindow,
                    'T' => TileType.Tree,
                    '.' => building == BuildingType.Smithy ? TileType.StoneFloor : TileType.WoodFloor,
                    _ => throw new Exception("Invalid character in building template!")
                };

                sqs.Add((currRow, currCol));
                map.SetTile(currRow, currCol, TileFactory.Get(tileType));
            }
        }

        switch (building)
        {
            case BuildingType.Shrine:
                Town.Shrine = sqs;
                break;
            case BuildingType.Tavern:
                Town.Tavern = sqs;
                break;
            case BuildingType.Market: 
                Town.Market = sqs;
                break;
            case BuildingType.Smithy:
                Town.Smithy = sqs;
                break;
            default:
                Town.Homes.Add(sqs);
                break;
        }
    }

    bool BuildingFits(Map map, int nwRow, int nwCol, Template t)
    {
        for (int r = 0; r < t.Height; r++) 
        { 
            for (int c = 0; c < t.Width; c++)
            {
                switch (map.TileAt(nwRow + r, nwCol + c).Type) 
                {
                    case TileType.DeepWater:
                    case TileType.Water:
                    case TileType.Wall:
                    case TileType.WoodWall:
                    case TileType.DungeonFloor:
                    case TileType.WoodFloor:
                    case TileType.Door:
                    case TileType.HWindow:
                        return false;
                    default:
                        continue;
                }
            }
        }

        // We also want to ensure a little space between buildings
        for (int c = 0; c < t.Width; c++)
        {
            var tile = map.TileAt(nwRow - 1, nwCol + c);
            if (tile.Type == TileType.Wall || tile.Type == TileType.WoodWall)
                return false;
            tile = map.TileAt(nwRow + t.Height, nwCol + c);
            if (tile.Type == TileType.Wall || tile.Type == TileType.WoodWall)
                return false;
        }

        for (int r = 0; r < t.Height; r++)
        {
            var tile = map.TileAt(nwRow + r, nwCol - 1);
            if (tile.Type == TileType.Wall || tile.Type == TileType.WoodWall)
                return false;
            tile = map.TileAt(nwRow + r, nwCol + t.Width);
            if (tile.Type == TileType.Wall || tile.Type == TileType.WoodWall)
                return false;
        }

        return true;
    }

    bool CheckAlongCol(Map map, int startRow, int startCol, int townRow, int townCol, int delta, Template t, BuildingType building, Random rng)
    {
        int height = t.Height;

        if (delta > 0)
        {
            int row = startRow;
            while (row + height < townRow + TOWN_HEIGHT)
            {
                if (BuildingFits(map, row, startCol, t))
                {
                    DrawBuilding(map, row, startCol, townRow, townCol, t, building, rng);
                    return true;
                }
                row += delta;
            }
        }
        else
        {
            int row = startRow - height;
            while (row > townRow)
            {
                if (BuildingFits(map, row, startCol, t))
                {
                    DrawBuilding(map, row, startCol, townRow, townCol, t, building, rng);
                    return true;
                }
                row += delta;
            }
        }

        return false;
    }

    bool CheckAlongRow(Map map, int startRow, int startCol, int townRow, int townCol, int delta, Template t, BuildingType building, Random rng)
    {
        int width = t.Width;

        if (delta > 0)
        {
            int col = startCol;
            while (col + width < townCol + TOWN_WIDTH)
            {
                if (BuildingFits(map, startRow, col, t))
                {
                    DrawBuilding(map, startRow, col, townRow, townCol, t, building, rng);
                    return true;
                }
                col += delta;
            }
        }
        else
        {
            int col = townCol + TOWN_WIDTH - width - 1;
            while (col > townCol)
            {
                if (BuildingFits(map, startRow, col, t))
                {
                    DrawBuilding(map, startRow, col, townRow, townCol, t, building, rng);
                    return true;
                }
                col += delta;
            }
        }

        return false;
    }

    // This code (and the general building placement code) is very
    // cut-and-paste-y. But I'll likely eventually rewrite the 
    // town generation and building placement. This is just a 
    // "get something working" first pass at it"
    void PlaceTavern(Map map, int townRow, int townCol, Random rng)
    {
        List<int> options = [1, 2, 3, 4];
        options.Shuffle(rng);

        int o = 0;
        while (o < options.Count) 
        {
            int choice = options[o++];

            if (choice == 1)
            {
                // east facing tavern
                var template = Templates["tavern 1"];
                int startRow, delta;
                if (rng.NextDouble() < 0.5)
                {
                    startRow = townRow;
                    delta = 1;
                }
                else
                {
                    startRow = townRow + TOWN_HEIGHT - 1;
                    delta = -1;
                }

                delta = -1;
                if (CheckAlongCol(map, startRow, townCol, townRow, townCol, delta, template, BuildingType.Tavern, rng))
                    break;
            }
            else if (choice == 2)
            {
                // south facing tavern
                var template = Templates["tavern 2"];
                int startCol, delta;
                if (rng.NextDouble() < 0.5)
                {
                    startCol = townCol;
                    delta = 1;
                }
                else
                {
                    startCol = townCol + TOWN_HEIGHT - template.Height;
                    delta = -1;
                }
                if (CheckAlongRow(map, townRow, startCol, townRow, townCol, delta, template, BuildingType.Tavern, rng))
                    break;
            }
            else if (choice == 3)
            {
                // north facing tavern
                var template = Templates["tavern 3"];
                int startCol, delta;
                if (rng.NextDouble() < 0.5)
                {
                    startCol = townCol;
                    delta = 1;
                }
                else
                {
                    startCol = townCol + TOWN_WIDTH - template.Width;
                    delta = -1;
                }
                if (CheckAlongRow(map, townRow + TOWN_HEIGHT - template.Height - 1, startCol, townRow, townCol, delta, template, BuildingType.Tavern, rng))
                    break;
            }
            else if (choice == 4)
            {
                // west facing tavern
                var template = Templates["tavern 4"];
                int startRow, delta;
                if (rng.NextDouble() < 0.5)
                {
                    startRow = townRow;
                    delta = 1;
                }
                else
                {
                    startRow = townRow + TOWN_HEIGHT;
                    delta = -1;
                }
                if (CheckAlongCol(map, startRow, townCol + TOWN_WIDTH - template.Width - 1, townRow, townCol, delta, template, BuildingType.Tavern, rng))
                    break;
            }
        }
    }

    bool PlaceBuilding(Map map, int townRow, int townCol, Template t, BuildingType building, Random rng)
    {
        List<int> options = [1, 2, 3, 4];
        options.Shuffle(rng);

        int o = 0;
        while (o < options.Count)
        {
            int choice = options[o++];
            if (choice == 1)
            {
                // start at top left, stagger the buildings
                // so the town doesn't look too neat and orderly
                int row = townRow + rng.Next(0, 6);
                int col = townCol + rng.Next(0, 6);
                int deltaRow = 2;
                int deltaCol = 2;

                while (true)
                {
                    if (CheckAlongRow(map, row, col, townRow, townCol, deltaCol, t, building, rng))
                        return true;
                    row += deltaRow;
                    col += deltaCol;
                    if (col + t.Width > townCol + TOWN_WIDTH)
                        col = townCol;
                    if (row < townRow || row + t.Height > townRow + TOWN_HEIGHT)
                        break;
                }                
            }
            else if (choice == 2) 
            { 
                // start at bottom left
                int row = (townRow + TOWN_HEIGHT - t.Height - 1) - rng.Next(0, 6);
                int col = townCol + rng.Next(0, 6);
                int deltaRow = -2;
                int deltaCol = 2;

                while (true)
                {
                    if (CheckAlongRow(map, row, col, townRow, townCol, deltaCol, t, building, rng))
                        return true;
                    row += deltaRow;
                    col += deltaCol;
                    if (col + t.Width >= townCol + TOWN_WIDTH) 
                        col = townCol;
                    if (row < townRow || row + t.Height > townRow + TOWN_HEIGHT)
                        break;
                }
            }
            else if (choice == 3)
            {
                // start at top right
                int row = townRow + rng.Next(0, 6);
                int col = townCol + TOWN_WIDTH - t.Width - 1 - rng.Next(0, 6);
                int deltaRow = 2;
                int deltaCol = -2;

                while (true)
                {
                    if (CheckAlongRow(map, row, col, townRow, townCol, deltaCol, t, building, rng))
                        return true;
                    row += deltaRow;
                    col += deltaCol;
                    if (col < townCol)
                        col = townCol + TOWN_WIDTH - t.Width - 1;
                    if (row < townRow || row + t.Height > townRow + TOWN_HEIGHT)
                        break;
                }
            }
            else
            {
                // start at bottom right
                var row = townRow + TOWN_HEIGHT - t.Height - 1 - rng.Next(0, 6);
                var col = townCol + TOWN_WIDTH - t.Width - 1 - rng.Next(0, 6);
                int deltaRow = -2;
                int deltaCol = -2;

                while (true)
                {
                    if (CheckAlongRow(map, row, col, townRow, townCol, deltaCol, t, building, rng))
                        return true;
                    row += deltaRow;
                    col += deltaCol;
                    if (col < townCol)
                        col = townCol + TOWN_WIDTH - t.Width - 1;
                    if (row < townRow || row + t.Height > townRow + TOWN_HEIGHT)
                        break;
                }
            }
        }

        return false;
    }

    static bool GoodSpotForForge(Map map, int row, int col)
    {
        if (map.TileAt(row, col).Type != TileType.StoneFloor)
            return false;

        foreach (var (adjR, adjC) in Util.Adj8Sqs(row, col))
        {
            if (map.TileAt(adjR, adjC).Type == TileType.Door)
                return false;
        }

        return true;
    }

    void PlaceBuildings(Map map, int townRow, int townCol, Random rng)
    {
        // Step 1, get rid of most but not all the trees in the town and replace them with grass
        for (int r = townRow; r < townRow + TOWN_HEIGHT; r++)
        {
            for (int c = townCol; c < townCol + TOWN_WIDTH; c++)
            {
                if (map.TileAt(r, c).Type == TileType.Tree && rng.NextDouble() < 0.85)
                    map.SetTile(r, c, TileFactory.Get(TileType.Grass));
            }
        }

        // Next place the tavern; it's the largest building and the hardest to find a spot for
        PlaceTavern(map, townRow, townCol, rng);

        var cottages = Templates.Keys.Where(k => k.StartsWith("cottage")).ToList();

        // create the town's market
        var j = cottages[rng.Next(cottages.Count)];
        PlaceBuilding(map, townRow, townCol, Templates[j], BuildingType.Market, rng);

        // next, the smithy
        j = cottages[rng.Next(cottages.Count)];
        PlaceBuilding(map, townRow, townCol, Templates[j], BuildingType.Smithy, rng);
        var smithySqs = Town.Smithy.ToList();
        smithySqs.Shuffle(rng);
        int f = 0;
        while (true)
        {
            var loc = smithySqs[f++];
            if (GoodSpotForForge(map, loc.Item1, loc.Item2)) 
            {
                map.SetTile(loc.Item1, loc.Item2, TileFactory.Get(TileType.Forge));
                break;
            }
        }

        // there's only one shrine in the town. Maybe in the future I'll implement
        // religious rivalries
        string temple = rng.Next(2) == 0 ? "shrine 1" : "shrine 2";        
        PlaceBuilding(map, townRow, townCol, Templates[temple], BuildingType.Shrine, rng);
        
        // place the cottages/homes
        for (int i = 0; i < 6; i++)
        {
            int h = rng.Next(cottages.Count);
            string home = cottages[h];
            if (!PlaceBuilding(map, townRow, townCol, Templates[home], BuildingType.Home, rng))
                break;
        }
    }

    // Draw the paths in the town. For now they just converge on the town square, but perhaps 
    // I'll do paths between the buildings later. (If townsfolk eventually are 'friends' with
    // each other, maybe draw a path between their homes)
    //
    // This can maybe be replaced by a Djikstra Map with a building's door as the start and
    // the down square as the goal?
    void DrawPathsInTown(Map map, Random rng, int townRow, int townCol)
    {
        HashSet<(int, int)> doors = [];

        for (int r = townRow; r < townRow + TOWN_HEIGHT; r++) 
        { 
            for (int c = townCol; c < townCol + TOWN_WIDTH; c++) 
            {                 
                if (map.TileAt(r, c).Type == TileType.Door)
                {
                    foreach (var adj in Util.Adj8Sqs(r, c))
                    {
                        var tile = map.TileAt(adj.Item1, adj.Item2);
                        if (tile.Type == TileType.Grass || tile.Type == TileType.Tree)
                            map.SetTile(adj.Item1, adj.Item2, TileFactory.Get(TileType.Dirt));
                    }
                    doors.Add((r, c));
                }
            }
        }

        // pick a random spot in the town square for the paths to converge on
        int j = rng.Next(Town.TownSquare.Count);
        var centre = Town.TownSquare.ToList()[j];

        
        Dictionary<TileType, int> passable = [];
        passable.Add(TileType.Grass, 1);
        passable.Add(TileType.Dirt, 1);
        passable.Add(TileType.Bridge, 1);
        passable.Add(TileType.Tree, 2);
        passable.Add(TileType.Water, 3);
        passable.Add(TileType.DeepWater, 3);
        
        var dmap = new DjikstraMap(map, townRow, townRow + TOWN_HEIGHT, townCol, townCol + TOWN_WIDTH);
        dmap.Generate(passable, (centre.Row, centre.Col));
        
    }

    public void AddWell(Map map, Random rng)
    {
        List<Loc> locs = Town.TownSquare.Select(sq => sq).ToList();
        locs.Shuffle(rng);

        while (locs.Count > 0) 
        {
            var sq = locs[0];
            locs.RemoveAt(0);

            bool okay = true;
            foreach (var adj in Util.Adj4Sqs(sq.Row, sq.Col)) 
            {
                var tile = map.TileAt(adj.Item1, adj.Item2);
                if (tile.Type != TileType.Grass && tile.Type != TileType.Dirt && tile.Type != TileType.Tree)
                {
                    okay = false;
                    break;
                }
            }
            if (!okay)
                continue;

            map.SetTile(sq.Row, sq.Col, TileFactory.Get(TileType.Well));

            foreach (var adj in Util.Adj8Sqs(sq.Row, sq.Col))
                map.SetTile(adj.Item1, adj.Item2, TileFactory.Get(TileType.Dirt));
            break;
        }
    }

    public Map DrawnTown(Map map, Random rng)
    {        
        int rows = 0, width = 0;
        List<char> sqs = [];
        bool noRotate = false;
        string currBuildling = "";
        foreach (var line in File.ReadLines("buildings.txt"))
        {
            if (line.StartsWith('%'))
            {
                if (currBuildling != "")
                {
                    var template = new Template() { Sqs = sqs, NoRotate = noRotate, Width = width, Height = rows };
                    Templates.Add(currBuildling, template);
                }
                currBuildling = line[1..];
                rows = 0;
                sqs = [];
                noRotate = false;
            }
            else if (line == "no rotate")
            {
                noRotate = true;
            }
            else
            {
                width = line.Length;
                sqs.AddRange(line.ToCharArray());
                ++rows;
            }
        }
        var lastTemplate = new Template() { Sqs = sqs, NoRotate = noRotate, Width = width, Height = rows };
        Templates.Add(currBuildling, lastTemplate);

        int wildernessSize = map.Height;

        // Pick starting co-ordinates that are in the centre-ish area of the map
        int startRow = rng.Next(wildernessSize / 4, wildernessSize / 2);
        int startCol = rng.Next(wildernessSize / 4, wildernessSize / 2);

        PlaceBuildings(map, startRow, startCol, rng);

        int centreRow = startRow + TOWN_HEIGHT / 2;
        int centreCol = startCol + TOWN_WIDTH / 2;
        TownCentre = (centreRow, centreCol);

        // Mark town square
        for (int r = centreRow - 5; r < centreRow + 5; r++)
        {
            for (int c = centreCol - 5; c < centreCol + 5; c++)
            {
                var ttype = map.TileAt(r, c).Type;
                if (ttype == TileType.Grass || ttype == TileType.Tree || ttype == TileType.Dirt)
                    Town.TownSquare.Add(new Loc(0, 0, r, c));
            }
        }

        DrawPathsInTown(map, rng, startRow, startCol);

        AddWell(map, rng);

        return map;
    }
}

class Template
{
    public List<char> Sqs { get; set; } = [];
    public int Width { get; set; }
    public int Height { get; set; }
    public bool NoRotate { get; set; }    
}
