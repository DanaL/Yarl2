
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

using System.Globalization;

namespace Yarl2;

// A structure to store info about a dungeon
class Dungeon(int ID, string arrivalMessage)
{
    public int ID { get; init; } = ID;
    public Dictionary<(int, int, int), Sqr> RememberedSqs = [];
    public Dictionary<int, Map> LevelMaps = [];
    public string ArrivalMessage { get; } = arrivalMessage;

    public void AddMap(Map map)
    {
        int id = LevelMaps.Count == 0 ? 0 : LevelMaps.Keys.Max() + 1;
        LevelMaps.Add(id, map);        
    }
}

// A data structure to store all the info about 
// the 'story' of the game. All the dungeon levels, etc
class Campaign
{
    public Dictionary<int, Dungeon> Dungeons = [];
    public int CurrentDungeon { get; set; }
    public int CurrentLevel { get; set; }
    
    public void AddDungeon(Dungeon dungeon)
    {
        int id = Dungeons.Count == 0 ? 0 : Dungeons.Keys.Max() + 1;
        Dungeons.Add(id, dungeon);
    }
}

class PreGameHandler(UserInterface ui)
{
    private UserInterface _ui { get; set; } = ui;
   
    (Campaign, int, int) BeginCampaign(Random rng, GameObjectDB objDb)
    {        
        var campaign = new Campaign();
        var wilderness = new Dungeon(0, "You draw a deep breath of fresh air.");        
        var wildernessGenerator = new Wilderness(rng);
        var wildernessMap = wildernessGenerator.DrawLevel(257);
        
        var tb = new TownBuilder();
        wildernessMap = tb.DrawnTown(wildernessMap, rng);
        wilderness.AddMap(wildernessMap);
        campaign.AddDungeon(wilderness);

        var entrance = wildernessMap.RandomTile(TileType.Tree, rng);
        
        var history = new History(rng);
        history.CalcDungeonHistory();
        history.GenerateVillain();

        var dBuilder = new DungeonBuilder();
        var mainDungeon = dBuilder.Generate(1, "Musty smells. A distant clang. Danger.", 30, 70, 5, entrance, history, objDb, rng);        
        campaign.AddDungeon(mainDungeon);

        var portal = new Portal("You stand before a looming portal.")
        {
            Destination = new Loc(1, 0, dBuilder.ExitLoc.Item1, dBuilder.ExitLoc.Item2)
        };
        wildernessMap.SetTile(entrance, portal);

        // Temp: generate monster decks and populate the first two levels of the dungeon.
        // I'll actually want to save the decks for reuse as random monsters are added
        // in, but I'm not sure where they should live. I guess maybe in the Map structure,
        // which has really come to represent a dungeon level
        var decks = DeckBulder.MakeDecks(1, 2, history.Villain, rng);

        for (int lvl = 0; lvl < 2; lvl++)
        {
            for (int j = 0; j < rng.Next(8, 13); j++)
            {
                var deck = decks[lvl];
                var sq = mainDungeon.LevelMaps[lvl].RandomTile(TileType.DungeonFloor, rng);
                var loc = new Loc(mainDungeon.ID, lvl, sq.Item1, sq.Item2);
                if (deck.Indexes.Count == 0)
                    deck.Reshuffle(rng);
                string m = deck.Monsters[deck.Indexes.Dequeue()];
                Actor monster = MonsterFactory.Get(m, rng);
                monster.Loc = loc;
                objDb.Add(monster);
                objDb.SetToLoc(loc, monster);
            }
        }
        
        campaign.CurrentDungeon = 0;
        campaign.CurrentLevel = 0;
        return (campaign, entrance.Item1, entrance.Item2);        
    }

    public bool StartUp(Random rng)
    {
        try
        {
            string playerName = _ui.BlockingGetResponse("Who are you?");
            SetupGame(playerName, rng);

            return true;
        }
        catch (GameQuitException)
        {
            return false;
        }
    }

    private void SetupGame(string playerName, Random rng)
    {
        if (Serialize.SaveFileExists(playerName))
        {
            var (player, c, objDb, currentTurn, msgHistory) = Serialize.LoadSaveGame(playerName);
            _ui.Player = player;
            _ui.SetupGameState(c, objDb, currentTurn);
            _ui.MessageHistory = msgHistory;
        }
        else
        {
            var objDb = new GameObjectDB();
            var (c, startRow, startCol) = BeginCampaign(rng, objDb);

            var player = PlayerCreator.NewPlayer(playerName, objDb, startRow, startCol, _ui, rng);
            _ui.ClearLongMessage();

            _ui.Player = player;
            _ui.SetupGameState(c, objDb, 1);            
        }
    }    
}

