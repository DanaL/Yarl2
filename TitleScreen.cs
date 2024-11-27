﻿// Yarl2 - A roguelike computer RPG
// Written in 2024 by Dana Larose <ywg.dana@gmail.com>
//
// To the extent possible under law, the author(s) have dedicated all copyright
// and related and neighboring rights to this software to the public domain
// worldwide. This software is distributed without any warranty.
//
// You should have received a copy of the CC0 Public Domain Dedication along 
// with this software. If not, 
// see <http://creativecommons.org/publicdomain/zero/1.0/>.

using Yarl2;

class TitleScreen
{
  UserInterface UI { get; set; }
  Map? Map { get; set; }
  Dictionary<TileType, int> TravelCost { get; set; } = [];
  Loc Player { get; set; }
  Loc Stairs { get; set; }
  Stack<Loc> PlayerPath { get; set; } = [];
  bool InDungeon { get; set; }
  HashSet<Loc> SeenBefore = [];
  GameObjectDB ObjDb { get; set; } = new();

  public TitleScreen(UserInterface ui)
  {
    UI = ui;
    TravelCost.Add(TileType.Bridge, 1);
    TravelCost.Add(TileType.Grass, 1);
    TravelCost.Add(TileType.Dirt, 1);
    TravelCost.Add(TileType.Sand, 1);
    TravelCost.Add(TileType.Water, 1);
    TravelCost.Add(TileType.GreenTree, 1);
    TravelCost.Add(TileType.RedTree, 1);
    TravelCost.Add(TileType.OrangeTree, 1);
    TravelCost.Add(TileType.YellowTree, 1);
    TravelCost.Add(TileType.Conifer, 1);
    TravelCost.Add(TileType.DungeonFloor, 1);
    TravelCost.Add(TileType.ClosedDoor, 1);
    TravelCost.Add(TileType.OpenDoor, 1);
    TravelCost.Add(TileType.Upstairs, 1);
  }

  public void Display()
  {
    string[] text =
    [
      " welcome to             ",
      "  ▗▄▄▄  ▗▞▀▜▌▄▄▄▄  ▗▞▀▜▌▐ ▄▄▄      ▗▄▄▄  ▗▞▀▚▖█ ▗▞▀▚▖▄   ▄ ▗▞▀▚▖",
      "  ▐▌  █ ▝▚▄▟▌█   █ ▝▚▄▟▌  ▀▄▄      ▐▌  █ ▐▛▀▀▘█ ▐▛▀▀▘█   █ ▐▛▀▀▘",
      "  ▐▌  █      █   █        ▄▄▄▀     ▐▌  █ ▝▚▄▄▖█ ▝▚▄▄▖ ▀▄▀  ▝▚▄▄▖",
      "  ▐▙▄▄▀                            ▐▙▄▄▀      █       0.2.0     ",
      "",
            "       a roguelike adventure game",
      "",
      "",
      "",
      " a) load game",
      " b) begin new adventure",
      " c) options"
    ];

    UI.SqsOnScreen = new Sqr[UserInterface.ScreenHeight, UserInterface.ScreenWidth];
    UI.ClearSqsOnScreen();
    for (int r = 0; r < text.Length; r++)
    {
      string row = text[r];
      for (int c = 0; c < row.Length; c++)
      {
        Sqr s = new(Colours.WHITE, Colours.BLACK, row[c]);
        UI.SqsOnScreen[r + 1, c + 1] = s;
      }
    }

    BuildLittleWilderness();

    Update();

    UI.SqsOnScreen = new Sqr[UserInterface.ViewHeight, UserInterface.ViewWidth];
    UI.ClearSqsOnScreen();
  }

  void Update()
  {
    DrawDungeonMiniScreen();
    UI.UpdateDisplay(null);

    char c;
    DateTime lastRedraw = DateTime.MinValue;
    do
    {
      Thread.Sleep(30);
      c = UI.GetKeyInput();

      if (c == 'a')
        break;

      if ((DateTime.Now - lastRedraw).TotalMilliseconds >= 250)
      {
        UpdatePlayer();
        lastRedraw = DateTime.Now;
      }

      DrawDungeonMiniScreen();
      UI.UpdateDisplay(null);
    }
    while (true);
  }

  void UpdatePlayer()
  {
    if (PlayerPath.Count > 0)
    {
      PlayerTurn();
    }
    else if (Map!.TileAt(Player.Row, Player.Col).Type == TileType.Portal)
    {
      BuildLittleDungeon();
      InDungeon = true;      
    }
    else if (InDungeon)
    {
      BuildLittleWilderness();
    }
  }

  void PlayerTurn()
  {
    Loc next = PlayerPath.Peek();
    if (Map!.TileAt(next.Row, next.Col).Type == TileType.ClosedDoor)
    {
      ((Door)Map!.TileAt(next.Row, next.Col)).Open = true;
    }
    else
    {
      Player = PlayerPath.Pop();
    }    
  }

  bool DrawWildernessMap()
  {
    Random rng = new();
    var wildernessGenerator = new Wilderness(rng, 65);
    Map = wildernessGenerator.DrawLevel();

    List<(int, int)> options = [];
    for (int r = 3; r < Map.Height - 3; r++)
    {
      for (int c = 3; c < Map.Width - 3; c++)
      {
        int adjMountains = Util.CountAdjTileType(Map, r, c, TileType.Mountain);
        int adjWater = Util.CountAdjTileType(Map, r, c, TileType.Water);
        if (adjWater > 2)
          continue;
        if (adjMountains >= 4 && adjMountains < 7)
        {
          options.Add((r, c));
        }
      }
    }

    List<(int, int)> startingSpots = [];
    for (int r = 20; r < 44; r++)
    {
      for (int c = 20; c < 44; c++)
      {
        switch (Map.TileAt(r, c).Type)
        {
          case TileType.Mountain:
          case TileType.SnowPeak:
          case TileType.Water:
          case TileType.DeepWater:
            continue;
          default:
            startingSpots.Add((r, c));
            break;
        }
      }
    }
    if (startingSpots.Count == 0)
      return false;

    while (options.Count > 0)
    {
      int i = rng.Next(options.Count);
      int j = rng.Next(startingSpots.Count);

      var (dungeonR, dungeonC) = options[i];
      options.RemoveAt(i);
      Loc dungeon = new(0, 0, dungeonR, dungeonC);
      var (startR, startC) = startingSpots[j];
      startingSpots.RemoveAt(j);
      Loc start = new(0, 0, startR, startC);
      Stack<Loc> path = AStar.FindPath(Map, start, dungeon, TravelCost);

      if (path.Count > 0)
      {
        Map.SetTile(dungeonR, dungeonC, new Portal(""));
        PlayerPath = path;        
        return true;
      }
    }

    return false;
  }

  void BuildLittleWilderness()
  {
    InDungeon = false;

    bool success;
    do
    {
      success = DrawWildernessMap();
    }
    while (!success);
  }

  void DrawDungeonMiniScreen()
  {
    const int viewHeight = 15;
    const int viewWidth = 35;
    const int halfHeight = viewHeight / 2;
    const int halfWidth = viewWidth / 2;

    Dictionary<Loc, Illumination> visible = [];
    int fov = InDungeon ? 5 : 25;
    
    visible = FieldOfView.CalcVisible(fov, Player, Map!, ObjDb);
    
    for (int viewR = 0; viewR < viewHeight; viewR++)
    {
      for (int viewC = 0; viewC < viewWidth; viewC++)
      {
        int mapR = Player.Row + (viewR - halfHeight);
        int mapC = Player.Col + (viewC - halfWidth);

        if (mapR == Player.Row && mapC == Player.Col)
        {
          UI.SqsOnScreen[viewR + 11, viewC + 30] = new Sqr(Colours.WHITE, Colours.BLACK, '@');
        }
        else if (mapR >= 0 && mapR < Map!.Height && mapC >= 0 && mapC < Map.Width)
        {
          Sqr sqr = new(Colours.BLACK, Colours.BLACK, ' ');         
          Loc loc = new(0, 0, mapR, mapC);

          Glyph objGlyph = ObjDb.GlyphAt(loc);
          if (visible.ContainsKey(loc))
          {
            Glyph g = objGlyph != GameObjectDB.EMPTY ? objGlyph : Util.TileToGlyph(Map.TileAt(mapR, mapC));
            sqr = new Sqr(g.Lit, g.BGLit, g.Ch);
            SeenBefore.Add(loc);
          }
          else if (SeenBefore.Contains(loc))
          {
            Glyph g = Util.TileToGlyph(Map.TileAt(mapR, mapC));
            sqr = new Sqr(g.Unlit, g.BGUnlit, g.Ch);
          }            
    
          UI.SqsOnScreen[viewR + 11, viewC + 30] = sqr;
        }
        else
        {
          UI.SqsOnScreen[viewR + 11, viewC + 30] = new Sqr(Colours.BLACK, Colours.BLACK, ' ');
        }
      }
    }
  }

  void BuildLittleDungeon()
  {
    SeenBefore = [];
    Random rng = new();
    DungeonMap mapper = new(rng);
    Map = mapper.DrawLevel(50, 50);

    List<(int, int)> floors = [];
    for (int r = 0; r < Map.Height; r++)
    {
      for (int c = 0; c < Map.Width; c++)
      {
        if (Map.TileAt(r, c).Type == TileType.DungeonFloor)
          floors.Add((r, c));
      }
    }

    int i = rng.Next(floors.Count);
    var (stairsR, stairsC) = floors[i];
    floors.RemoveAt(i);
    Map.SetTile(stairsR, stairsC, new Upstairs(""));
    Stairs = new(0, 0, stairsR, stairsC);
    Player = Stairs;

    for (int j = 0; j < 5; j++)
    {
      if (floors.Count == 0)
        break;

      i = rng.Next(floors.Count);
      var (r,  c) = floors[i];
      Item z = ItemFactory.Get(ItemNames.ZORKMIDS, ObjDb);
      ObjDb.SetToLoc(new Loc(0, 0, r, c), z);
      floors.RemoveAt(i);
    }

    Actor? monster = null;
    for (int j = 0; j < 5; j++)
    {
      if (floors.Count == 0)
        break;

      Actor a = rng.Next(3) switch
      {
        0 => MonsterFactory.Get("kobold", ObjDb, rng),
        1 => MonsterFactory.Get("skeleton", ObjDb, rng),
        _ => MonsterFactory.Get("goblin boss", ObjDb, rng)
      };

      i = rng.Next(floors.Count);
      var (r, c) = floors[i];
      ObjDb.AddNewActor(a, new Loc(0, 0, r, c));
      floors.RemoveAt(i);
      monster = a;
    }

    if (monster is null)
      return;
    
    Stack<Loc> path = AStar.FindPath(Map, Player, monster.Loc, TravelCost);
    if (path.Count > 0)
    {
      path.Pop();
      PlayerPath = path;
    }
  }
}

