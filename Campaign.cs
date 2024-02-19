
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
   
    (Campaign, int, int) BeginCampaign(Random rng)
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
        
        //var mainDungeon = new Dungeon(1, "Musty smells. A distant clang. Danger.");
        var dBuilder = new DungeonBuilder();
        var mainDungeon = dBuilder.Generate(1, "Musty smells. A distant clang. Danger.", 30, 70, 5, entrance, rng);        
        campaign.AddDungeon(mainDungeon);

        var portal = new Portal("You stand before a looming portal.")
        {
            Destination = new Loc(1, 0, dBuilder.ExitLoc.Item1, dBuilder.ExitLoc.Item2)
        };
        wildernessMap.SetTile(entrance, portal);

        campaign.CurrentDungeon = 0;
        campaign.CurrentLevel = 0;
        return (campaign, entrance.Item1, entrance.Item2);        
    }

    public bool StartUp()
    {
        try
        {
            string playerName = _ui.BlockingGetResponse("Who are you?");
            SetupGame(playerName);

            return true;
        }
        catch (GameQuitException)
        {
            return false;
        }
    }

    private void SetupGame(string playerName)
    {
        if (Serialize.SaveFileExists(playerName))
        {
            var (player, c, objDb) = Serialize.LoadSaveGame(playerName);
            _ui.Player = player;
            _ui.SetupGameState(c, objDb);
        }
        else
        {
            var objDb = new GameObjectDB();
            var (c, startRow, startCol) = BeginCampaign(new Random());
            Player player = new(playerName, startRow, startCol);
            objDb.Add(player);
            var spear = ItemFactory.Get("spear", objDb);
            spear.Adjectives.Add("old");
            spear.Equiped = true;
            player.Inventory.Add(spear, player.ID);
            var armour = ItemFactory.Get("leather armour", objDb);
            armour.Adjectives.Add("battered");
            armour.Equiped = true;
            player.Inventory.Add(armour, player.ID);
            player.Inventory.Add(ItemFactory.Get("dagger", objDb), player.ID);
            player.Inventory.Add(ItemFactory.Get("dagger", objDb), player.ID);
            player.Inventory.Add(ItemFactory.Get("dagger", objDb), player.ID);

            for (int i = 0; i < 10; i++)
            {
                player.Inventory.Add(ItemFactory.Get("torch", objDb), player.ID);
            }
            //player.Inventory.Add(ItemFactory.Get(("torch")));

            _ui.Player = player;

            
            // var m = MonsterFactory.Get("goblin", AIType.Basic);
            // m.Row = startRow + 1;
            // m.Col = startCol - 1;
            // objDb.Add(new Loc(0, 0, startRow + 1, startCol - 1), m);
            // var z = MonsterFactory.Get("zombie", AIType.Basic);
            // z.Row = startRow + 1;
            // z.Col = startCol;
            // objDb.Add(new Loc(0, 0, startRow + 1, startCol), z);

            _ui.SetupGameState(c, objDb);
        }
    }    
}

