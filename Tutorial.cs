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

  static Player TutorialPlayer(GameObjectDB objDb)
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

    var doc = new Item()
    {
      Name = "Encouraging Note",
      Type = ItemType.Document,
      Glyph = new Glyph('?', Colours.WHITE, Colours.LIGHT_GREY, Colours.BLACK, Colours.BLACK)
    };
    doc.Traits.Add(new ScrollTrait());
    
    var rt = new ReadableTrait("Dungeon delving can be tough, and it may take you many tries to beat the game so don't get discouraged and have fun!")
    {
      OwnerID = doc.ID
    };
    doc.Traits.Add(rt);
    objDb.Add(doc);
    player.Inventory.Add(doc, player.ID);

    for (int i = 0; i < 3; i++)
      player.Inventory.Add(ItemFactory.Get(ItemNames.TORCH, objDb), player.ID);
    player.CalcHP();

    Item sword = ItemFactory.Get(ItemNames.SHORTSHORD, objDb);
    objDb.SetToLoc(new Loc(1, 0, 1, 8), sword);    
    Item armour = ItemFactory.Get(ItemNames.LEATHER_ARMOUR, objDb);
    objDb.SetToLoc(new Loc(1, 0, 2, 8), armour);
  
    return player;
  }

  static Campaign Campaign()
  {
    Map tutorialMap = new(25, 25, TileType.DungeonWall);
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
    tutorialMap.SetTile(10, 10, TileFactory.Get(TileType.DungeonFloor));
    tutorialMap.SetTile(11, 10, TileFactory.Get(TileType.ClosedDoor));

    for (int c = 3; c < 15; c++)
    {
      tutorialMap.SetTile(12, c, TileFactory.Get(TileType.DungeonFloor));
    }
    tutorialMap.SetTile(12, 2, TileFactory.Get(TileType.SecretDoor));
    tutorialMap.SetTile(12, 1, TileFactory.Get(TileType.Downstairs));
    
    for (int r = 10; r < 16; r++)
    {
      for (int c = 14; c < 20; c++)
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

    gameState.ObjDb.ConditionalEvents.Add(new PlayerHasLitTorch(gameState, UI));

    string txt = @"This is a short sword, handy for defending yourself!

    You can pick it up with the ',' pickup command and equip it with the 'e' command.
    ";
    objDb.ConditionalEvents.Add(new PlayerAtLoc(gameState, UI, new Loc(1, 0, 1, 8), txt));
    txt = @"Here is some armour, which will help keep you safe from harm.

    You can pick it up with the ',' pickup command and equip it with the 'e' command.
    ";
    objDb.ConditionalEvents.Add(new PlayerAtLoc(gameState, UI, new Loc(1, 0, 2, 8), txt));

    string welcomeText = @"Delve is a dungeon crawling adventure game and your main activity will be exploring dark dungeons full of monsters (and loot!). This tutorial will provide you some basic information and teach you the core commands.    
    Delve uses letters and symbols for its display. Let's start with a quick breakdown of what you'll see on the screen:

        @ - this symbol represents you, the adventurer, townsfolk and other NPCs
        [LIGHTGREY #] - walls
        [LIGHTGREY .] - floors, the ground, grass, etc
        [BRIGHTRED g],[BRIGHTRED h], etc - letters are generally monsters
        [LIGHTBROWN (],[LIGHTBROWN )], etc] - other symbols are often loot and equipment that you might find in your travels

    You'll interact with Delve's world through several commands (listed at the bottom of your screen). Let's focus on two for the moment: (i)nventory and (a)pply/use equipment.

    (i) inventory shows you what are you currently carrying
    (a) uses or applies an item. (Including such things as drinking a potion, zapping a wand, reading a scroll, ...)

    [LIGHTBLUE Dungeons are dark, so let's light up a torch! Tap 'a' to open a menu of your current equipment and select the letter for a torch.]
    ";
    UI.SetPopup(new Popup(welcomeText, "Tutorial", -3, -1, UserInterface.ScreenWidth - 8), true);
    
    var ce = new FullyEquiped(gameState, UI, new Loc(1, 0, 5, 10));
    foreach (var item in objDb.ItemsAt(new Loc(1, 0, 1, 8)))    
      ce.IDs.Add(item.ID);
    foreach (var item in objDb.ItemsAt(new Loc(1, 0, 2, 8)))
      ce.IDs.Add(item.ID);
    objDb.ConditionalEvents.Add(ce);

    UI.CheatSheetMode = CheatSheetMode.Commands;
    return gameState;
  }
}