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

class DungeonBuilder
{
    public (int, int) ExitLoc { get; set; }
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

    public Dungeon Generate(int id, string arrivalMessage, int h, int w, int numOfLevels, (int, int) entrance, Random rng)
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

        SetStairs(levels, h, w, numOfLevels, entrance, rng);

        return dungeon;
    }
}