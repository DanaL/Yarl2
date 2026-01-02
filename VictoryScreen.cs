// Delve - A roguelike computer RPG
// Written in 2026 by Dana Larose <ywg.dana@gmail.com>
//
// To the extent possible under law, the author(s) have dedicated all copyright
// and related and neighboring rights to this software to the public domain
// worldwide. This software is distributed without any warranty.
//
// You should have received a copy of the CC0 Public Domain Dedication along 
// with this software. If not, 
// see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System.Text;

namespace Yarl2;

class Victory
{
  static void PlaceVillager(GameState gs, Actor villager, int centerRow, int centerCol)
  {
    List<Loc> locs = [];

    for (int r = centerRow - 5; r < centerRow + 5; r++)
    {
      for (int c = centerCol - 5; c < centerCol + 5; c++)
      {
        var loc = new Loc(0, 0, r, c);

        if (gs.ObjDb.Occupied(loc))
          continue;

        switch (gs.TileAt(loc).Type)
        {
          case TileType.Bridge:
          case TileType.Dirt:
          case TileType.Grass:
          case TileType.GreenTree:
          case TileType.RedTree:
          case TileType.YellowTree:
          case TileType.OrangeTree:
            locs.Add(loc);
            break;
        }
      }
    }

    if (locs.Count > 0)
    {
      var loc = locs[gs.Rng.Next(locs.Count)];
      gs.ObjDb.ActorMoved(villager, villager.Loc, loc);
    }
  }

  public static void VictoryScreen(GameState gs)
  {
    gs.CurrDungeonID = 0;
    gs.CurrLevel = 0;
    UserInterface ui = gs.UIRef();
    
    var popup = new Popup($"\nCongratulations, Adventurer! The world is again safe from evil, for the time being...\n\n  -- Press any key to continue --", "Victory", -1, -1);
    ui.SetPopup(popup);
    ui.UpdateDisplay(gs);
    ui.BlockForInput(gs);
    ui.ClearLongMessage();
    ui.ClosePopup();

    var town = gs.Campaign.Town!;

    int minRow = int.MaxValue, minCol = int.MaxValue, maxRow = 0, maxCol = 0;
    foreach (var loc in town.TownSquare)
    {
      if (loc.Row < minRow)
        minRow = loc.Row;
      if (loc.Col < minCol)
        minCol = loc.Col;
      if (loc.Row > maxRow)
        maxRow = loc.Row;
      if (loc.Col > maxCol)
        maxCol = loc.Col;
    }

    int playerRow = (minRow + maxRow) / 2;
    int playerCol = (minCol + maxCol) / 2 + 2;
    var playerLoc = new Loc(0, 0, playerRow, playerCol);
    gs.ObjDb.ActorMoved(gs.Player, gs.Player.Loc, playerLoc);
    gs.PrepareFieldOfView();

    List<Actor> villagers = [];
    foreach (var obj in gs.ObjDb.Objs)
    {
      if (obj.Value is Actor actor && actor.HasTrait<VillagerTrait>())
      {
        villagers.Add(actor);
        PlaceVillager(gs, actor, playerRow, playerCol);
      }
    }
    villagers = [.. villagers.Where(v => v.Loc.Row > minRow + 2)];
    
    Animation? bark = null;

    StringBuilder sb = new();
    sb.Append("After your victory deep in the ancient Gaol, you return to ");
    sb.Append(town.Name);
    sb.Append(" and receive the accoldates of the townsfolk.\n\n");
    sb.Append("The darkness has been lifted from the region and the village will soon begin again to prosper. Yet after resting for a time and enjoying the villagers' gratitude and hospitality, the yearning for adventure begins to overtake you.\n\n");
    sb.Append("You've heard, for instance, tales of a fabled dungeon in whose depths lies the legendary Amulet of Yender...\n\n");
    sb.Append("Press any key to exit.");
    string msg = sb.ToString();

    do
    {
      Thread.Sleep(30);
      ui.ClearScreen();
      ui.ClearSqsOnScreen();

      int screenR = 6;
      int screenC = 7;
      for (int r = minRow - 6; r < maxRow + 5; r++)
      {
        for (int c = minCol - 6; c < maxCol + 11; c++)
        {
          Glyph glyph;
          if (r == playerRow && c == playerCol)
          {
            glyph = gs.Player.Glyph with { BG = Colours.HILITE };
            ui.PlayerScreenRow = screenR;
            ui.PlayerScreenCol = screenC;
          }
          else if (gs.ObjDb.Occupant(new Loc(0, 0, r, c)) is Actor actor)
          {
            glyph = actor.Glyph;
          }
          else
          {
            Tile tile = gs.TileAt(new Loc(0, 0, r, c));
            glyph = Util.TileToGlyph(tile);
          }

          Sqr sqr = new(glyph.Lit, glyph.BG, glyph.Ch);
          ui.SqsOnScreen[screenR, screenC++] = sqr;
        }
        ++screenR;
        screenC = 7;
      }

      for (int r = 0; r < UserInterface.ViewHeight; r++)
      {
        for (int c = 0; c < UserInterface.ScreenWidth / 2; c++)
        {
          ui.WriteSq(r, c, ui.SqsOnScreen[r, c]);
        }
      }

      if (bark is not null && bark.Expiry > DateTime.UtcNow)
      {
        bark.Update();
      }
      else if (villagers.Count > 0)
      {
        var v = villagers[gs.Rng.Next(villagers.Count)];
        string cheer;
        if (v.Glyph.Ch == 'd')
        {
          cheer = "Arf! Arf!";
        }
        else
        {
          cheer = gs.Rng.Next(4) switch
          {
            0 => "Huzzah!",
            1 => "Praise them with great praise!",
            2 => "Our hero!",
            _ => "We'll be safe now!"
          };
        }

        bark = new BarkAnimation(gs, 2000, v, cheer) { AlwaysOnTop = true };
      }

      ui.SetPopup(new Overlay(msg));
      ui.UpdateDisplay(gs);
      if (ui.GetKeyInput() != '\0')
        break;
    }
    while (true);
  }
}