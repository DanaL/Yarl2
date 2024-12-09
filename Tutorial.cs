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

class Tutorial(UserInterface ui)
{
  UserInterface UI { get; set; } = ui;

  Player TutorialPlayer(GameObjectDB objDb)
  {
    Dictionary<Attribute, Stat> stats = new()
    {
      { Attribute.Strength, new Stat(1) },
      { Attribute.Constitution, new Stat(3) },
      { Attribute.Dexterity, new Stat(1) },
      { Attribute.Piety, new Stat(0) },
      { Attribute.Will, new Stat(0) },
      { Attribute.Depth, new Stat(0) },
      { Attribute.HP, new Stat(1) }
    };

    Player player = new("Max Damage")
    {
      Lineage = PlayerLineage.Human,
      Background = PlayerBackground.Warrior,
      Stats = stats
    };

    player.Inventory = new Inventory(player.ID, objDb);
    player.CalcHP();

    return player;
  }

  static Campaign Campaign()
  {
    Map tutorialMap = new(20, 30, TileType.DungeonWall);
    for (int r = 1; r < 5; r++)
    {
      for (int c = 6; c < 15; c++)
      {
        tutorialMap.SetTile(r, c, TileFactory.Get(TileType.DungeonFloor));
      }
    }
    tutorialMap.SetTile(5, 10, TileFactory.Get(TileType.DungeonFloor));
    tutorialMap.SetTile(6, 10, TileFactory.Get(TileType.DungeonFloor));
    tutorialMap.SetTile(7, 10, TileFactory.Get(TileType.DungeonFloor));
    tutorialMap.SetTile(8, 10, TileFactory.Get(TileType.DungeonFloor));
    tutorialMap.SetTile(9, 10, TileFactory.Get(TileType.DungeonFloor));
    tutorialMap.SetTile(10, 10, TileFactory.Get(TileType.ClosedDoor));

    for (int c = 3; c < 15; c++)
    {
      tutorialMap.SetTile(11, c, TileFactory.Get(TileType.DungeonFloor));
    }
    tutorialMap.SetTile(11, 2, TileFactory.Get(TileType.SecretDoor));
    tutorialMap.SetTile(11, 1, TileFactory.Get(TileType.Downstairs));
    
    for (int r = 9; r < 16; r++)
    {
      for (int c = 13; c < 20; c++)
      {
        tutorialMap.SetTile(r, c, TileFactory.Get(TileType.DungeonFloor));
      }
    }
    
    Campaign campaign = new();
    Dungeon dungeon = new(1, "");
    dungeon.AddMap(tutorialMap);
    campaign.AddDungeon(new Dungeon(0, ""));
    campaign.AddDungeon(dungeon);

    return campaign;
  }

  public GameState? Setup(Options options)
  {
    int seed = 4;
    Random rng = new(seed);
    GameObjectDB objDb = new();

    Player player = TutorialPlayer(objDb);
    Campaign campaign = Campaign();

    GameState gameState = new(player, campaign, options, UI, rng, seed)
    {
      ObjDb = objDb,
      Turn = 1,
      Tutorial = true,
      CurrDungeonID = 1,
      CurrLevel = 0
    };

    gameState.ObjDb.AddNewActor(player, new Loc(1, 0, 1, 6));
    gameState.UpdateFoV();
    gameState.RecentlySeenMonsters.Add(gameState.Player.ID);

    UI.CheatSheetMode = CheatSheetMode.Movement;
    return gameState;
  }
}