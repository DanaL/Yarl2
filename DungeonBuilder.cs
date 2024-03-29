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

// Herein is the code for building the main dungeon of the game

namespace Yarl2;

abstract class DungeonBuilder
{
    public (int, int) ExitLoc { get; set; }
}

// A class to build an arena type level for easy testing of monsters and items
class ArenaBuilder : DungeonBuilder
{
    private int _dungeonID;
    
    public Dungeon Generate(int id,  (int, int) entrance, GameObjectDB objDb, Random rng)
    {
        int _numOfLevels = 1;
        _dungeonID = id;
        var dungeon = new Dungeon(id, "");
        var mapper = new DungeonMap(rng);
        Map[] levels = new Map[_numOfLevels];
        int h = 51, w = 51;

        var cave = CACave.GetCave(h - 1, w - 1, rng);
        var caveMap = new Map(w, h);
        for (int r = 0; r < h - 1; r++)
        {
            for (int c = 0; c < w - 1; c++)
            {
                var tileType = cave[r, c] ? TileType.DungeonFloor : TileType.DungeonWall;
                caveMap.SetTile(r + 1, c + 1, TileFactory.Get(tileType));
            }
        }
        for (int c = 0; c < w; c++)
        {
            caveMap.SetTile(0, c, TileFactory.Get(TileType.PermWall));
            caveMap.SetTile(h - 1, c , TileFactory.Get(TileType.PermWall));
        }
        for (int r = 0; r < h; r++)
        {
            caveMap.SetTile(r, 0, TileFactory.Get(TileType.PermWall));
            caveMap.SetTile(r, w - 1, TileFactory.Get(TileType.PermWall));
        }

        levels[0] = caveMap;

        // let's stick a pond in the centre
        var pond = CACave.GetCave(20, 20, rng);
        for (int r = 0; r < 20; r++)
        {
            for (int c = 0; c < 20; c++)
            {
                if (pond[r, c])
                {
                    caveMap.SetTile(r + 15, c + 15, TileFactory.Get(TileType.DeepWater));
                }                
            }
        }
        //for (int l = 0; l < _numOfLevels; l++)
        //{
        //    levels[l] = mapper.DrawLevel(w, h);
        //    dungeon.AddMap(levels[l]);
        //}

        //DungeonMap.AddRiver(levels[0], w + 1, h + 1, TileType.DeepWater, rng);
        //// We want to make any square that's a wall below the chasm into dungeon floor
        //for (int r = 1; r < h; r++)
        //{
        //    for (int c = 1; c < w; c++)
        //    {
        //        var pt = (r, c);
        //        if (levels[0].IsTile(pt, TileType.Chasm) && levels[1].IsTile(pt, TileType.DungeonWall))
        //        {
        //            levels[1].SetTile(pt, TileFactory.Get(TileType.DungeonFloor));
        //        }
        //    }
        //}

        //ExitLoc = floors[0][rng.Next(floors[0].Count)];
        // It's convenient to know where all the stairs are.
        List<List<(int, int)>> floors = [];
        for (int lvl = 0; lvl < _numOfLevels; lvl++)
        {
            floors.Add([]);
            for (int r = 0; r < h; r++)
            {
                for (int c = 0; c < w; c++)
                {
                    if (levels[lvl].TileAt(r, c).Type == TileType.DungeonFloor)
                        floors[lvl].Add((r, c));
                }
            }
        }

        // so first set the exit stairs
        ExitLoc = floors[0][rng.Next(floors[0].Count)];

        var exitStairs = new Upstairs("")
        {
            Destination = new Loc(0, 0, entrance.Item1, entrance.Item2)
        };
        levels[0].SetTile(ExitLoc, exitStairs);
        dungeon.AddMap(levels[0]);

        return dungeon;
    }
}

class MainDungeonBuilder : DungeonBuilder
{
    private int _dungeonID;

    void SetStairs(Map[] levels, int height, int width, int numOfLevels, (int,int) entrance, Random rng)
    {
        List<List<(int, int)>> floors = [];

        // It's convenient to know where all the stairs are.
        for (int lvl = 0; lvl < numOfLevels; lvl++) {
            floors.Add([]);
            for (int r = 0; r < height; r++)
            {
                for (int c = 0; c < width; c++)
                {
                    if (levels[lvl].TileAt(r, c).Type == TileType.DungeonFloor)
                        floors[lvl].Add((r, c));
                }
            }
        }
        
        // so first set the exit stairs
        ExitLoc = floors[0][rng.Next(floors[0].Count)];
        var exitStairs = new Upstairs("")
        {
            Destination = new Loc(0, 0, entrance.Item1, entrance.Item2)
        };
        levels[0].SetTile(ExitLoc, exitStairs);

        // I want the dungeon levels to be, geographically, neatly stacked so
        // the stairs between floors will be at the same location. (Ie., if 
        // the down stairs on level 3 is at 34,60 then the stairs up from 
        // level 4 should be at 34,60 too)
        for (int l = 0; l < numOfLevels - 1; l++)
        {
            var lvl = levels[l];
            var nlvl = levels[l+1]; 
            // find the pairs of floor squares shared between the two levels
            List<(int,int)> shared = [];
            for (int r = 1; r < height - 1; r++)
            {
                for (int c = 1; c < width - 1; c++)
                {
                    if (lvl.TileAt(r, c).Type == TileType.DungeonFloor && nlvl.TileAt(r, c).Type == TileType.DungeonFloor)
                    {
                        shared.Add((r, c));
                    }
                }
            }

            var pick = shared[rng.Next(shared.Count)];

            var down = new Downstairs("")
            {
                Destination = new Loc(_dungeonID, l + 1, pick.Item1, pick.Item2)
            };
            levels[l].SetTile(pick.Item1, pick.Item2, down);

            var up = new Upstairs("")
            {
                Destination = new Loc(_dungeonID, l, pick.Item1, pick.Item2)
            };
            levels[l+1].SetTile(pick.Item1, pick.Item2, up);
        }
    }

    private void PlaceStatue(Map map, int height, int width, string statueDesc, Random rng)
    {
        List<(int, int)> candidateSqs = [];
        // Find all the candidate squares where the statue(s) might go
        for (int r = 1; r < height - 1; r++)
        {
            for (int c = 1; c < width - 1; c++)
            {
                if (map.TileAt(r, c).Type == TileType.DungeonFloor)
                {
                    bool viable = true;
                    foreach (var t in Util.Adj4Sqs(r, c))
                    {
                        if (map.TileAt(t).Type != TileType.DungeonFloor)
                        {
                            viable = false;
                            break;
                        }
                    }

                    if (viable)
                        candidateSqs.Add((r, c));
                }
            }
        }

        if (candidateSqs.Count > 0) 
        {
            var sq = candidateSqs[rng.Next(candidateSqs.Count)];
            foreach (var n in Util.Adj4Sqs(sq.Item1, sq.Item2))
            {
                map.SetTile(n, TileFactory.Get(TileType.Statue));
            }

            var tile = new Landmark(statueDesc.Capitalize());
            map.SetTile(sq, tile);
        }
    }

    private void PlaceFresco(Map map, int height, int width, string frescoText, Random rng)
    {
        List<(int, int)> candidateSqs = [];
        // We're looking for any floor square that's adjacent to wall
        for (int r = 1; r < height - 1; r++)
        {
            for (int c = 1; c < width - 1; c++)
            {
                if (map.TileAt(r, c).Type == TileType.DungeonFloor)
                {
                    bool viable = false;
                    foreach (var t in Util.Adj4Sqs(r, c))
                    {
                        if (map.TileAt(t).Type == TileType.DungeonWall)
                        {
                            viable = true;
                            break;
                        }
                    }

                    if (viable)
                        candidateSqs.Add((r, c));
                }
            }
        }

        if (candidateSqs.Count > 0)
        {
            var sq = candidateSqs[rng.Next(candidateSqs.Count)];            
            var tile = new Landmark(frescoText.Capitalize());
            map.SetTile(sq, tile);
        }
    }

    private void PlaceDocument(Map map, int level, int height, int width, string documentText, GameObjectDB objDb, Random rng)
    {
        // Any floor will do...
        List<(int, int)> candidateSqs = [];
        for (int r = 1; r < height - 1; r++)
        {
            for (int c = 1; c < width - 1; c++)
            {
                if (map.TileAt(r, c).Type == TileType.DungeonFloor)                
                        candidateSqs.Add((r, c));                
            }
        }

        string adjective;
        string desc;
        var roll = rng.NextDouble();
        if (roll < 0.5) 
        {
            desc = "scroll";
            adjective = "tattered";
        }
        else
        {
            desc = "page";
            adjective = "torn";
        }

        var doc = new Item() { Name = desc, Type = ItemType.Document, Stackable = false,
                                    Glyph = new Glyph('?', Colours.WHITE, Colours.LIGHT_GREY, Colours.BLACK), 
                                    Adjectives = [ adjective] };
        var rt = new ReadableTrait(documentText)
        {
            ContainerID = doc.ID
        };
        doc.Traits.Add(rt);
        var (row, col) = candidateSqs[rng.Next(candidateSqs.Count)];
        var loc = new Loc(_dungeonID, level, row, col);
        objDb.Add(doc);
        objDb.SetToLoc(loc, doc);
    }

    // While I'm testing I'll just stick all the decorations on level 1
    private void DecorateDungeon(Map[] levels, int height, int width, int numOfLevels, History history, GameObjectDB objDb, Random rng)
    {
        var decorations = history.GetDecorations();

        // I eventually probably won't include every decoration from every fact
        foreach (var decoration in decorations) 
        { 
            if (decoration.Type == DecorationType.Statue)
            {
                PlaceStatue(levels[0], height, width, decoration.Desc, rng);
            }
            else if (decoration.Type == DecorationType.Mosaic)
            {
                var sq = levels[0].RandomTile(TileType.DungeonFloor, rng);
                var mosaic = new Landmark(decoration.Desc.Capitalize());
                levels[0].SetTile(sq, mosaic);
            }
            else if (decoration.Type == DecorationType.Fresco)
            {
                PlaceFresco(levels[0], height, width, decoration.Desc, rng);
            }
            else if (decoration.Type == DecorationType.ScholarJournal)
            {
                PlaceDocument(levels[0], 0, height, width, decoration.Desc, objDb, rng);
            }
        }
    }

    // I think this seed generated isolated rooms :O
    //    -5586292
    public Dungeon Generate(int id, string arrivalMessage, int h, int w, int numOfLevels, (int, int) entrance, History history, GameObjectDB objDb, Random rng)
    {
        _dungeonID = id;
        var dungeon = new Dungeon(id, arrivalMessage);
        var mapper = new DungeonMap(rng);
        Map[] levels = new Map[numOfLevels];

        for (int l = 0; l < numOfLevels; l++) 
        {
            levels[l] = mapper.DrawLevel(w, h);
            dungeon.AddMap(levels[l]);
        }   

        DungeonMap.AddRiver(levels[0], w + 1, h + 1, TileType.DeepWater, rng);
        // We want to make any square that's a wall below the chasm into dungeon floor
        for (int r = 1; r < h; r++)
        {
            for (int c = 1; c < w; c++) 
            {
                var pt = (r, c);
                if (levels[0].IsTile(pt, TileType.Chasm) && levels[1].IsTile(pt, TileType.DungeonWall))
                {
                    levels[1].SetTile(pt, TileFactory.Get(TileType.DungeonFloor));
                }
            }
        }

        SetStairs(levels, h, w, numOfLevels, entrance, rng);
        
        DecorateDungeon(levels, h, w, numOfLevels, history, objDb, rng);

        return dungeon;
    }
}