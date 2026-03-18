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

namespace Yarl2;

class TerraformTool : Inputer
{
  public const int TILE_SELECTION = 0;
  public const int TILE_PLACEMENT = 1;  
  int _state = TILE_SELECTION;
  TerraformPopup _popup;

  public TerraformTool(GameState gs, Loc start) : base(gs)
  {
    var (scrR, scrC) = gs.UIRef().LocToScrLoc(start.Row, start.Col, GS.Player.Loc.Row, GS.Player.Loc.Col);
    _popup = new TerraformPopup(gs) { State = TILE_SELECTION, CursorRow = scrR, CursorCol = scrC };

    gs.UIRef().SetPopup(_popup);
  }

  public override void Input(char ch)
  {
    KeyCmd cmd = GS.KeyMap.ToCmd(ch);
    if (_state == TILE_SELECTION)
    {
      if (ch == Constants.ESC)
        Close();
      else if (cmd == KeyCmd.MoveW)
        _popup.SelectedTile = (_popup.SelectedTile - 1 + TerraformPopup.Tiles.Length) % TerraformPopup.Tiles.Length;
      else if (cmd == KeyCmd.MoveE)
        _popup.SelectedTile = (++_popup.SelectedTile) % TerraformPopup.Tiles.Length;
      else if (ch == '\n' || ch == '\r')
      {
        _state = TILE_PLACEMENT;
        _popup.State = TILE_PLACEMENT;       
      }
    }
    else if (_state == TILE_PLACEMENT)
    {
      if (ch == Constants.ESC) 
      {
        _state = TILE_SELECTION;
        _popup.State = TILE_SELECTION;
      }
      else if (cmd == KeyCmd.MoveN && _popup.CursorRow > 0)
        --_popup.CursorRow;
      else if (cmd == KeyCmd.MoveW && _popup.CursorCol > 0)      
        --_popup.CursorCol;
      else if (cmd == KeyCmd.MoveS && _popup.CursorRow < UserInterface.ViewHeight - 1)
        ++_popup.CursorRow;
      else if (cmd == KeyCmd.MoveE && _popup.CursorCol < UserInterface.ViewWidth - 1)      
        ++_popup.CursorCol;
      else if (cmd == KeyCmd.MoveNW && _popup.CursorRow > 0 && _popup.CursorCol > 0)
      {
        --_popup.CursorRow;
        --_popup.CursorCol;
      }
      else if (cmd == KeyCmd.MoveNE && _popup.CursorRow > 0 && _popup.CursorCol < UserInterface.ViewWidth - 1)
      {
        --_popup.CursorRow;
        ++_popup.CursorCol;
      }
      else if (cmd == KeyCmd.MoveSW && _popup.CursorRow < UserInterface.ViewHeight - 1 && _popup.CursorCol > 0)
      {
        ++_popup.CursorRow;
        --_popup.CursorCol;
      }
      else if (cmd == KeyCmd.MoveSE && _popup.CursorRow < UserInterface.ViewHeight - 1 && _popup.CursorCol < UserInterface.ViewWidth - 1)
      {
        ++_popup.CursorRow;
        ++_popup.CursorCol;
      }
      else if (ch == '\n' || ch == '\r')
      {
        var (mapR, mapC) = GS.UIRef().ScrLocToGameLoc(_popup.CursorRow, _popup.CursorCol, GS.Player.Loc.Row, GS.Player.Loc.Col);
        Loc loc = new (GS.CurrDungeonID, GS.CurrLevel, mapR, mapC);
        Tile tile = GS.TileAt(loc);
        if (tile.Type == TileType.WorldBorder || tile.Type == TileType.PermWall)
        {
          GS.UIRef().AlertPlayer($"You cannot edit that tile {tile.Type}.");
        }
        else
        {
          GS.CurrentMap.SetTile(mapR, mapC, TerraformPopup.Tiles[_popup.SelectedTile]);
          if (!GS.LastPlayerFoV.ContainsKey(loc))
            GS.CurrentDungeon.RememberedLocs[loc] = new(Util.TileToGlyph(TerraformPopup.Tiles[_popup.SelectedTile]), 0);
          GS.PrepareFieldOfView();  
        }
      }
    }
  }
}

class TerraformPopup(GameState gs) : IPopup
{ 
  public GameState GS { get; set; } = gs;
  public int SelectedTile { get; set; } = 0;
  public int State { get; set; }
  public int CursorRow { get; set; }
  public int CursorCol { get; set; }
  const int _rowWidth = 12; // # of tiles per row
  
  public static Tile[] Tiles = [ TileFactory.Get(TileType.DungeonWall), TileFactory.Get(TileType.DungeonFloor),
    TileFactory.Get(TileType.DeepWater), TileFactory.Get(TileType.LockedDoor), TileFactory.Get(TileType.GreenTree),
    TileFactory.Get(TileType.YellowTree), TileFactory.Get(TileType.RedTree), TileFactory.Get(TileType.Grass),
    TileFactory.Get(TileType.Dirt), TileFactory.Get(TileType.WoodBridge), TileFactory.Get(TileType.Pit),
    TileFactory.Get(TileType.TrapDoor), TileFactory.Get(TileType.TeleportTrap), TileFactory.Get(TileType.Chasm),
    TileFactory.Get(TileType.DartTrap), TileFactory.Get(TileType.IllusoryWall), TileFactory.Get(TileType.SecretDoor)];

  public void SetDefaultTextColour(Colour colour) { }

  public void Draw(UserInterface ui)
  {
    int width = 15;
    int row = 1;
    int startCol = UserInterface.ViewWidth - width - 5;
    
    string topBorder = Constants.TOP_LEFT_CORNER.ToString().PadRight(width + 3, '─') + Constants.TOP_RIGHT_CORNER;
    ui.WriteText([(Colours.WHITE, topBorder)], row++, startCol);

    foreach (var tiles in Tiles.Chunk(_rowWidth))
    {
      List<(Colour, string)> sqs = [(Colours.WHITE, "│ ")];            
      foreach (var tile in tiles)
      {
        if (tile.Type == TileType.SecretDoor)
        {
          sqs.Add((Colours.LIGHT_BLUE, "+"));
        }
        else if (tile.Type == TileType.IllusoryWall)
        {
          sqs.Add((Colours.LIGHT_BLUE, "#"));  
        }
        else
        {
          Glyph glyph = Util.TileToGlyph(tile);
          sqs.Add((glyph.Lit, glyph.Ch.ToString()));  
        }        
      }
      sqs.Add((Colours.WHITE, "".PadRight(width - tiles.Length)));
      sqs.Add((Colours.WHITE, " │"));
      
      ui.WriteText(sqs.ToArray(), row++, startCol);
    }

    string desc = TileName(Tiles[SelectedTile].Type);
    if (State == TerraformTool.TILE_PLACEMENT)
      desc +=  " ✓";
    desc = desc.PadRight(width);      
    string label = "│ " + desc + " │";
    ui.WriteText([(Colours.WHITE, label)], row++, startCol);

    string bottomBorder = Constants.BOTTOM_LEFT_CORNER.ToString().PadRight(width + 3, '─') + Constants.BOTTOM_RIGHT_CORNER;
    ui.WriteText([(Colours.WHITE, bottomBorder)], row, startCol);

    int selectedRow = 2 + SelectedTile / _rowWidth;
    int selectedCol = startCol + 2 + SelectedTile % _rowWidth;
    Glyph selectedGlyph = Util.TileToGlyph(Tiles[SelectedTile]);
    Sqr selected = new(Colours.WHITE, Colours.HILITE, selectedGlyph.Ch);
    ui.WriteSq(selectedRow, selectedCol, selected);

    if (State == TerraformTool.TILE_PLACEMENT)
    {      
      Sqr sqr = ui.SqsOnScreen[CursorRow, CursorCol];
      ui.WriteSq(CursorRow, CursorCol, sqr with { Fg = Colours.WHITE, Bg = Colours.LIGHT_BLUE });      
    }
  }

  static string TileName(TileType type) => type switch 
  {
    TileType.DungeonWall => "Dungeon wall",
    TileType.DungeonFloor => "Dungeon floor",
    TileType.DeepWater => "River",
    TileType.LockedDoor => "Locked door",
    TileType.GreenTree => "Green Tree",
    TileType.YellowTree => "Yellow Tree",
    TileType.RedTree => "Red Tree",
    TileType.Grass => "Grass",
    TileType.Dirt => "Dirt",
    TileType.WoodBridge => "Wood bridge",
    TileType.Pit => "Pit",
    TileType.TrapDoor => "Trap door",
    TileType.TeleportTrap => "Teleport trap",
    TileType.Chasm => "Chasm",
    TileType.DartTrap => "Dart trap",
    TileType.IllusoryWall => "Illusory Wall",
    TileType.SecretDoor => "Secret door",
    _ => ""
  };
}
