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

class TownBuilder
{
    const int TOWN_HEIGHT = 36;
    const int TOWN_WIDTH = 60;

    Dictionary<string, Template> Templates { get; set; } = [];

    void DrawBuilding(Map map, int row, int col, int townRow, int townCol, Template t, BuildingType building, Random rng)
    {
        bool isWood = rng.NextDouble() < 0.7;

        // rotate would go here

        for (int r = 0; r < t.Height; r++) 
        { 
            for (int c = 0; c < t.Width; c++) 
            {
                int currRow = row + r;
                int currCol = col + c;
                var tileType = t.Sqs[r * t.Width + c] switch
                {
                    '#' => isWood ? TileType.WoodWall : TileType.Wall,
                    '`' => TileType.Grass,
                    '+' => TileType.Door,
                    '|' or '-' => TileType.Window,
                    'T' => TileType.Tree,
                    '.' => TileType.WoodFloor,
                    _ => throw new Exception("Invalid character in building template!")
                };

                map.SetTile(currRow, currCol, TileFactory.Get(tileType));
            }
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
                    case TileType.Window:
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

        }

        return false;
    }

    void PlaceTavern(Map map, int townRow, int townCol, Random rng)
    {
        List<int> options = [1];// [1, 2, 3, 4];
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

                delta = 1;
                if (CheckAlongCol(map, startRow, townCol, townRow, townCol, delta, template, BuildingType.Tavern, rng))
                    break;
            }

            
        }
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

        // Start with the tavern; it's the largest building and the hardest to find a spot for
        PlaceTavern(map, townRow, townCol, rng);
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
