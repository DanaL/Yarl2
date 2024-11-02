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

// Holds dungeon features/room code because DungeonBuilder was getting too big.
// There's probably code that can be moved over from DungeonBuilder but I'm not
// sure how to orgainize it yet.

// Maybe also a good place to centralize code for finding rooms in the map.

class IdolAltarMaker
{
  static List<(int, int, int, int, int, int)> PotentialClosets(Map map)
  {
    var closets = new List<(int, int, int, int, int, int)>();

    // Check each tile in the map
    for (int r = 2; r < map.Height - 2; r++)
    {
      for (int c = 2; c < map.Width - 2; c++)
      {
        if (map.TileAt(r, c).Type != TileType.DungeonWall)
          continue;

        bool surroundedByWalls = true;
        foreach (var sq in Util.Adj8Sqs(r, c))
        {
          if (map.TileAt(sq).Type != TileType.DungeonWall)
          {
            surroundedByWalls = false;
            break;
          }
        }        
        if (!surroundedByWalls)
          continue;

        if (GoodAltarSpot(map, r - 2, c))
          closets.Add((r, c, r - 2, c, r - 1, c));
        else if (GoodAltarSpot(map, r + 2, c))
          closets.Add((r, c, r + 2, c, r + 1, c));
        else if (GoodAltarSpot(map, r, c - 2))
          closets.Add((r, c, r, c - 2, r, c - 1));
        else if (GoodAltarSpot(map, r, c + 2))
          closets.Add((r, c, r, c + 2, r, c + 1));
      }
    }

    return closets;
  }

  static bool GoodAltarSpot(Map map, int r, int c)
  {
    if (map.TileAt(r, c).Type != TileType.DungeonFloor)
      return false;

    return Util.Adj8Sqs(r, c)
               .Where(t => map.InBounds(t.Item1, t.Item2))
               .Count(t => map.TileAt(t).Type == TileType.DungeonFloor) == 5;
  }

  static bool CheckFloorPattern(Map map, int r, int c, int dr, int dc)
  {
    int checkC = c + dc;
    int checkR = r + dr;

    if (map.TileAt(checkC, checkR).Type != TileType.DungeonFloor)
      return false;

    int floorCount = 0;
    for (int cr = -1; cr <= 1; cr++)
    {
      for (int cc = -1; cc <= 1; cc++)
      {
        if (map.TileAt(checkC + cc, checkR + cr).Type == TileType.DungeonFloor)
          floorCount++;
      }
    }

    return floorCount >= 5;
  }

  public static void MakeAltar(int dungeonID, Map[] levels, GameObjectDB objDb, Random rng, int level)
  {
    Map altarLevel = levels[level];

    var closets = PotentialClosets(altarLevel);    
    if (closets.Count > 0)
    {
      var (closetR, closetC, altarR, altarC, wallR, wallC) = closets[rng.Next(closets.Count)];
      Console.WriteLine($"Altar: {altarR}, {altarC}");

      Item idol = new() { Name = "idol", Type = ItemType.Trinket, Value = 10};
      string altarDesc;

      switch (rng.Next(3))
      {
        case 0:
          idol.Glyph = new Glyph('"', Colours.YELLOW, Colours.YELLOW, Colours.BLACK, Colours.BLACK);
          idol.Traits.Add(new AdjectiveTrait("golden"));
          idol.Traits.Add(new AdjectiveTrait("crescent-shaped"));
          altarDesc = "An altar carved with arcane depictions of the moon.";
          break;
        case 1:
          idol.Name = "tree branch";
          idol.Glyph = new Glyph('"', Colours.LIGHT_BROWN, Colours.BROWN, Colours.BLACK, Colours.BLACK);
          idol.Traits.Add(new AdjectiveTrait("strange"));
          idol.Traits.Add(new AdjectiveTrait("rune-carved"));
          idol.Traits.Add(new FlammableTrait());
          altarDesc = "An engraving of the World Tree.";
          break;
        default:
          idol.Name = "carving";
          idol.Glyph = new Glyph('"', Colours.WHITE, Colours.LIGHT_GREY, Colours.BLACK, Colours.BLACK);
          idol.Traits.Add(new AdjectiveTrait("soapstone"));
          altarDesc = "An altar venerating the leviathan.";
          break;
      }

      objDb.Add(idol);
      Loc idolLoc = new(dungeonID, level, altarR, altarC);
      objDb.SetToLoc(idolLoc, idol);

      Tile altar = new IdolAltar(altarDesc)
      {
        IdolID = idol.ID,
        Wall = new Loc(dungeonID, level, wallR, wallC)
      };
      levels[level].SetTile(altarR, altarC, altar);
      levels[level].SetTile(closetR, closetC, TileFactory.Get(TileType.GreenTree));
    }
  }
}