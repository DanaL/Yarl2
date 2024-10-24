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

using System.Text;

namespace Yarl2;

abstract class DungeonBuilder
{
  public (int, int) ExitLoc { get; set; }
}

// A class to build an arena type level for easy testing of monsters and items
class ArenaBuilder : DungeonBuilder
{
  public Dungeon Generate(int id, (int, int) entrance, GameObjectDB objDb, Random rng)
  {
    int _numOfLevels = 1;
    var dungeon = new Dungeon(id, "");
    var mapper = new DungeonMap(rng);
    Map[] levels = new Map[_numOfLevels];
    int h = 31, w = 31;

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
      caveMap.SetTile(h - 1, c, TileFactory.Get(TileType.PermWall));
    }
    for (int r = 0; r < h; r++)
    {
      caveMap.SetTile(r, 0, TileFactory.Get(TileType.PermWall));
      caveMap.SetTile(r, w - 1, TileFactory.Get(TileType.PermWall));
    }

    levels[0] = caveMap;

    // let's stick a pond in the centre
    //var pond = CACave.GetCave(12, 12, rng);
    //for (int r = 0; r < 12; r++)
    //{
    //  for (int c = 0; c < 12; c++)
    //  {
    //    if (pond[r, c])
    //    {
    //      caveMap.SetTile(r + 15, c + 15, TileFactory.Get(TileType.DeepWater));
    //    }
    //  }
    //}
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

  void SetStairs(Map[] levels, int height, int width, int numOfLevels, (int, int) entrance, Random rng)
  {
    List<List<(int, int)>> floors = [];

    // It's convenient to know where all the stairs are.
    for (int lvl = 0; lvl < numOfLevels; lvl++)
    {
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
      var nlvl = levels[l + 1];
      // find the pairs of floor squares shared between the two levels
      List<(int, int)> shared = [];
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
      levels[l + 1].SetTile(pick.Item1, pick.Item2, up);
    }
  }

  void PlaceStatue(Map map, int height, int width, string statueDesc, Random rng)
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

      var statueSqs = Util.Adj4Sqs(sq.Item1, sq.Item2).ToList();
      var statueSq = statueSqs[rng.Next(statueSqs.Count)];
      map.SetTile(statueSq, TileFactory.Get(TileType.Statue));
      
      var tile = new Landmark(statueDesc.Capitalize());
      map.SetTile(sq, tile);
    }
  }

  void PlaceFresco(Map map, int height, int width, string frescoText, Random rng)
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

    var doc = new Item()
    {
      Name = desc,
      Type = ItemType.Document,
      Glyph = new Glyph('?', Colours.WHITE, Colours.LIGHT_GREY, Colours.BLACK, Colours.BLACK)
    };
    doc.Traits.Add(new FlammableTrait());
    doc.Traits.Add(new WrittenTrait());
    doc.Traits.Add(new AdjectiveTrait(adjective));

    var rt = new ReadableTrait(documentText)
    {
      OwnerID = doc.ID
    };
    doc.Traits.Add(rt);
    var (row, col) = candidateSqs[rng.Next(candidateSqs.Count)];
    var loc = new Loc(_dungeonID, level, row, col);
    objDb.Add(doc);
    objDb.SetToLoc(loc, doc);
  }

  void DecorateDungeon(Map[] levels, int height, int width, int numOfLevels, History history, GameObjectDB objDb, Random rng)
  {
    var decorations = history.GetDecorations();

    // I eventually probably won't include every decoration from every fact
    foreach (var decoration in decorations)
    {
      if (rng.NextDouble() < 0.1)
        continue;
        
      int level = rng.Next(numOfLevels);

      if (decoration.Type == DecorationType.Statue)
      {
        PlaceStatue(levels[level], height, width, decoration.Desc, rng);
      }
      else if (decoration.Type == DecorationType.Mosaic)
      {
        var sq = levels[level].RandomTile(TileType.DungeonFloor, rng);
        var mosaic = new Landmark(decoration.Desc.Capitalize());
        levels[level].SetTile(sq, mosaic);
      }
      else if (decoration.Type == DecorationType.Fresco)
      {
        PlaceFresco(levels[level], height, width, decoration.Desc, rng);
      }
      else if (decoration.Type == DecorationType.ScholarJournal)
      {
        PlaceDocument(levels[level], level, height, width, decoration.Desc, objDb, rng);
      }
    }

    int fallenAdventurer = rng.Next(1, numOfLevels);
    AddFallenAdventurer(objDb, levels[fallenAdventurer], fallenAdventurer, history, rng);

    for (int levelNum = 0; levelNum < levels.Length; levelNum++)
    {
      Treasure.AddTreasureToDungeonLevel(objDb, levels[levelNum], _dungeonID, levelNum, rng);
    }
  }

  void AddFallenAdventurer(GameObjectDB objDb, Map level, int levelNum, History history, Random rng)
  {
    var sq = level.RandomTile(TileType.DungeonFloor, rng);
    var loc = new Loc(_dungeonID, levelNum, sq.Item1, sq.Item2);

    for (int j = 0; j < 3; j++)
    {
      var torch = ItemFactory.Get(ItemNames.TORCH, objDb);
      objDb.SetToLoc(loc, torch);
    }
    if (rng.NextDouble() < 0.25)
    {
      var poh = ItemFactory.Get(ItemNames.POTION_HEALING, objDb);
      objDb.SetToLoc(loc, poh);
    }
    if (rng.NextDouble() < 0.25)
    {
      var antidote = ItemFactory.Get(ItemNames.ANTIDOTE, objDb);
      objDb.SetToLoc(loc, antidote);
    }
    if (rng.NextDouble() < 0.25)
    {
      var blink = ItemFactory.Get(ItemNames.SCROLL_BLINK, objDb);
      objDb.SetToLoc(loc, blink);
    }

    // add trinket
    var trinket = new Item()
    {
      Name = "tin locket",
      Type = ItemType.Trinket,
      Value = 1,
      Glyph = new Glyph('"', Colours.GREY, Colours.LIGHT_GREY, Colours.BLACK, Colours.BLACK)
    };
    objDb.Add(trinket);
    objDb.SetToLoc(loc, trinket);

    string text = "Scratched into the stone: if only I'd managed to level up.";
    var tile = new Landmark(text);
    level.SetTile(sq, tile);

    // Generate an actor for the fallen adventurer so I can store their 
    // name and such in the objDb. Maybe sometimes they'll be an actual
    // ghost?
    var ng = new NameGenerator(rng, "data/names.txt");
    var adventurer = new Mob()
    {
      Name = ng.GenerateName(rng.Next(5, 12))
    };
    adventurer.Traits.Add(new FallenAdventurerTrait());
    adventurer.Traits.Add(new OwnsItemTrait() { ItemID = trinket.ID });
    objDb.Add(adventurer);
  }

  List<(int, int)> FloorsNearWater(Map map, int row, int col, int d)
  {
    List<(int, int)> sqs = [];

    int loR = int.Max(0, row - d);
    int hiR = int.Min(map.Height - 1, row + d);
    for (int r = loR; r < hiR; r++)
    {
      if (map.TileAt(r, col).Type == TileType.DungeonFloor)
        sqs.Add((r, col));
    }

    int loC = int.Max(0, col - d);
    int hiC = int.Min(map.Width - 1, col + d);
    for (int c = loC; c < hiC; c++)
    {
      if (map.TileAt(row, c).Type == TileType.DungeonFloor)
        sqs.Add((row, c));
    }
    
    return sqs;
  }

  string DeepOneShrineDesc(Random rng)
  {
    var sb = new StringBuilder();
    sb.Append("A shrine depicting ");

    string adj = rng.Next(4) switch
    {
      0 => "a grotesque ",
      1 => "a misshapen ",
      2 => "a rough-hewn ",
      _ => "a crudely carved "
    };
    sb.Append(adj);

    string feature;
    switch (rng.Next(4))
    {
      case 0:
        sb.Append("humanoid with ");
        feature = rng.Next(3) switch
        {
          0 => "eyestalks and lobster claws.",
          1 => "the head of a carp.",
          _ => "a crab's body."
        };
        sb.Append(feature);
        break;        
      case 1:
        sb.Append("shark with ");
        feature = rng.Next(2) == 0 ? "the arms of a human." : "eyestalks.";
        sb.Append(feature);
        break;
      case 2:
        sb.Append("turtle with ");
        feature = rng.Next(2) == 0 ? "a human face." : "a shark's head.";
        sb.Append(feature);
        break;
      default:
        sb.Append("lobster with ");
        feature = rng.Next(2) == 0 ? "a human face." : "a shark's head.";
        sb.Append(feature);
        break;
    }

    string decoration = rng.Next(4) switch
    {
      0 => " It is strewn with shells and glass bleads.",
      1 => " It is streaked with blood.",
      2 => " It is adorned with teeth and driftwood.",
      3 => " It is decorated with rotting meat and worthless baubles."
    };
    sb.Append(decoration);

    return sb.ToString();
  }

  // Add a deep one shrine near the river that was generated on the map, if
  // possible
  void DeepOneShrine(Map map, int dungeonID, int level, GameObjectDB objDb, Random rng)
  {
    static string CalcChant(Random rng)
    {
      int roll = rng.Next(4);

      char[] subs = ['w', 'v', 'u', 'm', 'n', '\'', ' '];
      var sb = new StringBuilder("Ooooooo");     
      for (int i = 0; i < 5; i++)
      {
        int c = rng.Next(1, sb.Length - 1);
        sb[c] = subs[rng.Next(subs.Length)];
      }
      sb.Append('!');
      
      return sb.ToString();
    }

    HashSet<(int, int)> candidates = [];

    for (int r = 0; r < map.Height; r++) 
    { 
      for (int c = 0; c < map.Width; c++) 
      { 
        if (map.TileAt(r, c).Type == TileType.DeepWater)
        {
          foreach (var sq in FloorsNearWater(map, r, c, 3))
            candidates.Add(sq);
        }
      }
    }

    if (candidates.Count == 0)
      // can't place the shrine
      return;

    var floors = candidates.ToList();
    var loc = floors[rng.Next(floors.Count)];

    Tile shrine = new Landmark(DeepOneShrineDesc(rng));
    map.SetTile(loc, shrine);
    Loc shrineLoc = new(dungeonID, level, loc.Item1, loc.Item2);

    List<Loc> deepOneLocs = floors.Select(sq => new Loc(dungeonID, level, sq.Item1, sq.Item2))
                                  .Where(l => Util.Distance(shrineLoc, l) <= 3)
                                  .ToList();
    
    int numOfDeepOnes = int.Min(rng.Next(3) + 2, deepOneLocs.Count);
    List<Actor> deepOnes = [];
    for (int j = 0; j < numOfDeepOnes; j++)
    {
      if (deepOneLocs.Count == 0)
        break;

      Actor d = MonsterFactory.Get("deep one", objDb, rng);
      d.Traits.Add(new WorshiperTrait() 
      { 
        Altar = shrineLoc,
        Chant = CalcChant(rng)
      });

      int x = rng.Next(deepOneLocs.Count);
      Loc pickedLoc = deepOneLocs[x];
      deepOneLocs.RemoveAt(x);

      objDb.AddNewActor(d, pickedLoc);
      deepOnes.Add(d);
    }

    Actor shaman = MonsterFactory.Get("deep one shaman", objDb, rng);
    shaman.Traits.Add(new WorshiperTrait() 
    { 
      Altar = shrineLoc,
      Chant = CalcChant(rng)
    });

    if (deepOneLocs.Count > 0)
    {
      Loc shamanLoc = deepOneLocs[rng.Next(deepOneLocs.Count)];
      objDb.AddNewActor(shaman, shamanLoc);
      deepOnes.Add(shaman);
    }

    foreach (Actor deepOne in deepOnes)
    {
      List<ulong> allies = deepOnes.Select(k => k.ID)
                                   .Where(id => id != deepOne.ID)
                                   .ToList();
      deepOne.Traits.Add(new AlliesTrait() { IDs = allies });
    }

    // Add a few items nearby
    List<Loc> nearbyLocs = [];
    for (int r = -2; r < 3; r++)
    {
      for (int c = -2;  c < 3; c++)
      {
        Loc l = shrineLoc with { Row = shrineLoc.Row + r, Col = shrineLoc.Col + c };
        if (map.InBounds(l.Row, l.Col) && map.TileAt(l.Row, l.Col).Type == TileType.DungeonFloor)
        {
          nearbyLocs.Add(l);
        }
      }
    }

    if (nearbyLocs.Count > 0)
    {
      foreach (Item loot in Treasure.PoorTreasure(4, rng, objDb))
      {
        loot.Traits.Add(new OwnedTrait() { 
          OwnerIDs = deepOnes.Select(d => d.ID).ToList()
        });
        Loc itemLoc = nearbyLocs[rng.Next(nearbyLocs.Count)];
        objDb.SetToLoc(itemLoc, loot);
      }
    }
  }

  static  bool IsWall(TileType type)
  {
    return type == TileType.DungeonWall || type == TileType.PermWall;
  }

  static bool IsNWCorner(Map map, int row, int col)
  {
    if (!IsWall(map.TileAt(row - 1, col - 1).Type))
      return false;
    if (!IsWall(map.TileAt(row - 1, col).Type))
      return false;
    if (!IsWall(map.TileAt(row - 1, col - 1).Type))
      return false;
    if (!IsWall(map.TileAt(row, col - 1).Type))
      return false;
    if (map.TileAt(row, col + 1).Type != TileType.DungeonFloor)
      return false;
    if (!IsWall(map.TileAt(row + 1, col - 1).Type))
      return false;
    if (map.TileAt(row + 1, col).Type != TileType.DungeonFloor)
      return false;
    if (!IsWall(map.TileAt(row + 1, col + 1).Type))
      return false;
      
    return true;
  }

  static bool IsNECorner(Map map, int row, int col)
  {
    if (!IsWall(map.TileAt(row - 1, col - 1).Type))
      return false;
    if (!IsWall(map.TileAt(row - 1, col).Type))
      return false;
    if (!IsWall(map.TileAt(row - 1, col - 1).Type))
      return false;    
    if (map.TileAt(row, col - 1).Type != TileType.DungeonFloor)
      return false;
    if (!IsWall(map.TileAt(row, col + 1).Type))
      return false;
    if (!IsWall(map.TileAt(row + 1, col - 1).Type))
      return false;
    if (map.TileAt(row + 1, col).Type != TileType.DungeonFloor)
      return false;
    if (!IsWall(map.TileAt(row + 1, col + 1).Type))
      return false;
      
    return true;
  }

  static bool IsSWCorner(Map map, int row, int col)
  {
    if (!IsWall(map.TileAt(row - 1, col - 1).Type))
      return false;
    if (map.TileAt(row - 1, col).Type != TileType.DungeonFloor)
      return false;    
    if (!IsWall(map.TileAt(row - 1, col - 1).Type))
      return false;
    if (!IsWall(map.TileAt(row, col - 1).Type))
      return false;
    if (map.TileAt(row, col + 1).Type != TileType.DungeonFloor)
      return false;
    if (!IsWall(map.TileAt(row + 1, col - 1).Type))
      return false;
    if (!IsWall(map.TileAt(row + 1, col).Type))
      return false;
    if (!IsWall(map.TileAt(row + 1, col + 1).Type))
      return false;
      
    return true;
  }

  static bool IsSECorner(Map map, int row, int col)
  {
    if (!IsWall(map.TileAt(row - 1, col - 1).Type))
      return false;
    if (map.TileAt(row - 1, col).Type != TileType.DungeonFloor)
      return false;
    if (!IsWall(map.TileAt(row - 1, col - 1).Type))
      return false;    
    if (map.TileAt(row, col - 1).Type != TileType.DungeonFloor)
      return false;
    if (!IsWall(map.TileAt(row, col + 1).Type))
      return false;
    if (!IsWall(map.TileAt(row + 1, col - 1).Type))
      return false;
    if (!IsWall(map.TileAt(row + 1, col).Type))
      return false;
    if (!IsWall(map.TileAt(row + 1, col + 1).Type))
      return false;
      
    return true;
  }

  static List<(Loc, string)> FindCorners(Map map, int dungeonID, int level)
  {
    List<(Loc, string)> corners = [];

    for (int r = 1; r < map.Height - 1; r++)
    {
      for (int c = 1; c < map.Width - 1; c++)
      {
        TileType tile = map.TileAt(r, c).Type;

        if (tile != TileType.DungeonFloor)
          continue;

        if (IsNWCorner(map, r, c))
          corners.Add((new Loc(dungeonID, level, r, c), "nw"));
        else if (IsNECorner(map, r, c))
          corners.Add((new Loc(dungeonID, level, r, c), "ne"));
        else if (IsSWCorner(map, r, c))
          corners.Add((new Loc(dungeonID, level, r, c), "sw"));
        else if (IsSECorner(map, r, c))
          corners.Add((new Loc(dungeonID, level, r, c), "se"));
      }
    }

    return corners;
  }

  static void SetTraps(Map map, int dungeonID, int level, int depth, Random rng)
  {    
    int numOfTraps = rng.Next(1, 6);
    for (int j = 0 ; j < numOfTraps; j++)
    {
      int roll = rng.Next(6);
      if (roll == 0 && level < depth - 1)
      {
        var sq = map.RandomTile(TileType.DungeonFloor, rng);
        map.SetTile(sq, TileFactory.Get(TileType.HiddenTeleportTrap));
      }
      else if (roll == 1)
      {
        var sq = map.RandomTile(TileType.DungeonFloor, rng);
        map.SetTile(sq, TileFactory.Get(TileType.HiddenDartTrap));
      }
      else if (roll == 2)
      {
        var corners = FindCorners(map, dungeonID, level);
        var (corner, dir) = corners[0];
        FireJetTrap(map, corner, dir, rng);
      }
      else if (roll == 3 || roll == 4)
      {
        var sq = map.RandomTile(TileType.DungeonFloor, rng);
        map.SetTile(sq, TileFactory.Get(TileType.HiddenPit));
      }
      else if (roll == 5)
      {
        var sq = map.RandomTile(TileType.DungeonFloor, rng);
        map.SetTile(sq, TileFactory.Get(TileType.HiddenWaterTrap));
      }
      else
      {
        var sq = map.RandomTile(TileType.DungeonFloor, rng);
        map.SetTile(sq, TileFactory.Get(TileType.HiddenTrapDoor));
      }        
    }
  }

  static bool CanPlaceJetTrigger(Map map, (int, int) corner, (int, int) delta)
  {
    (int, int) loc = corner;
    int count = 0;

    while (map.InBounds(loc) && map.TileAt(loc).Type == TileType.DungeonFloor && count < 4)
    {
      ++count;
      loc = (loc.Item1 + delta.Item1, loc.Item2 + delta.Item2);
    }

    return count == 4;
  }

  static void FireJetTrap(Map map, Loc cornerLoc, string dir, Random rng)
  {
    (int, int) deltaH, deltaV;
    Dir horizontalDir, verticalDir;
    switch (dir)
    {
      case "nw":
        deltaH = (0, 1);
        deltaV = (1, 0);
        horizontalDir = Dir.East;
        verticalDir = Dir.South;
        break;
      case "ne":
        deltaH = (0, -1);
        deltaV = (0, 1);
        horizontalDir = Dir.West;
        verticalDir = Dir.South;
        break;
      case "sw":
        deltaH = (0, 1);
        deltaV = (-1, 0);
        horizontalDir = Dir.East;
        verticalDir = Dir.North;
        break;
      default:
        deltaH = (0, -1);
        deltaV = (-1, 0);
        horizontalDir = Dir.West;
        verticalDir = Dir.North;
        break;
    }

    bool horizontalValid = CanPlaceJetTrigger(map, (cornerLoc.Row, cornerLoc.Col), deltaH);
    bool verticalValid = CanPlaceJetTrigger(map, (cornerLoc.Row, cornerLoc.Col), deltaV);

    if (!horizontalValid && !verticalValid)
      return;

    Loc jetLoc;
    Loc triggerLoc;
    Dir jetDir;
    if (horizontalValid && verticalValid)
    {
      if (rng.NextDouble() < 0.5)
      {
        // horizontal
        jetDir = horizontalDir;
        jetLoc = cornerLoc with { Col = cornerLoc.Col - deltaH.Item2 };
        triggerLoc = cornerLoc with { Col = cornerLoc.Col + deltaH.Item2 * rng.Next(1, 4)};
      }
      else
      {
        // vertical
        jetDir = verticalDir;
        jetLoc = cornerLoc with { Row = cornerLoc.Row - deltaV.Item1 };
        triggerLoc = cornerLoc with { Row = cornerLoc.Row + deltaV.Item1 * rng.Next(1, 4)};
      }
    }
    else if (horizontalValid)
    {
      jetDir = horizontalDir;
      jetLoc = cornerLoc with { Col = cornerLoc.Col - deltaH.Item2 };
      triggerLoc = cornerLoc with { Col = cornerLoc.Col + deltaH.Item2 * rng.Next(1, 4)};
    }
    else
    {
      jetDir = verticalDir;
      jetLoc = cornerLoc with { Row = cornerLoc.Row - deltaV.Item1 };
      triggerLoc = cornerLoc with { Row = cornerLoc.Row + deltaV.Item1 * rng.Next(1, 4)};
    }

    Tile fireJet = new FireJetTrap(false, jetDir);
    map.SetTile(jetLoc.Row, jetLoc.Col, fireJet);
    Tile trigger = new JetTrigger(jetLoc, false);
    map.SetTile(triggerLoc.Row, triggerLoc.Col, trigger);
  }

  static void PutSecretsDoorInHallways(Map map, Random rng)
  {
    List<(int, int)> candidates = [];
    for (int r = 0; r < map.Height; r++)
    {
      for (int c = 0; c < map.Width; c++)
      {
        if (map.TileAt(r, c).Type == TileType.DungeonFloor)
        {
          int adjFloors = Util.Adj8Sqs(r, c)
                              .Select(map.TileAt)
                              .Where(t => t.Type == TileType.DungeonFloor).Count();
          if (adjFloors == 2)
            candidates.Add((r, c));
        }
      }
    }

    if (candidates.Count > 0)
    {
      int numtoAdd = rng.Next(1, 4);
      for (int j = 0; j < numtoAdd; j++)
      {
        (int, int) sq = candidates[rng.Next(candidates.Count)];
        map.SetTile(sq, TileFactory.Get(TileType.SecretDoor));
      }      
    }
  }

  (bool, Dir) ValidSpotForGatedStairs(Map map, int r, int c)
  {
    int walls = 0;
    int floors = 0;
    List<TileType> tiles = [];
    for (int dr = - 1; dr < 2; dr++)
    {
      for (int dc = - 1; dc < 2; dc++)
      {
        TileType tile = map.TileAt(r + dr, c + dc).Type;
        tiles.Add(tile);
        if (tile == TileType.DungeonWall)
          ++walls;
        if (tile == TileType.DungeonFloor)
          ++floors;
      }
    }

    if (walls != 6 || floors != 3)
      return (false, Dir.None);
    
    TileType df = TileType.DungeonFloor;
    if (tiles[0] == df && tiles[1] == df && tiles[2] == df)
      return (true, Dir.North);

    if (tiles[0] == df && tiles[3] == df && tiles[6] == df)
      return (true, Dir.West);

    if (tiles[2] == df && tiles[5] == df && tiles[8] == df)
      return (true, Dir.East);

    if (tiles[6] == df && tiles[7] == df && tiles[8] == df)
      return (true, Dir.South);

    return (false, Dir.None);
  }

  void PlaceLevelFiveGate(Map map, Random rng, History history)
  {
    List<(int, int, Dir)> candidates = [];

    // We're looking for a spot to make a gate/portcullis like:
    //
    //  ###.
    //  #>ǁ.
    //  ###.
    //
    for (int r = 1; r < map.Height - 1; r += 2)
    {
      for (int c = 1; c < map.Width - 1; c += 2)
      {
        var (valid, dir) = ValidSpotForGatedStairs(map, r, c);
        if (valid)
          candidates.Add((r, c, dir));          
      }
    }

    // Gotta throw an exception if there were no candidates
    var (sr, sc, sdir) = candidates[rng.Next(candidates.Count)];

    // I guess I should be making sure the stair location actually makes sense
    // for the next level
    Tile door = new VaultDoor(false, Metals.Iron);
    Tile stairs = new Downstairs("");
    map.SetTile(sr, sc, door);
    Loc doorLoc = new(1, 4, sr, sc);
    history.Facts.Add(new SimpleFact() { Name = "Level 5 Gate Loc", Value = doorLoc.ToString()});
    switch (sdir)
    {
      case Dir.North:
        map.SetTile(sr + 1, sc, stairs);
        break;
      case Dir.South:
      map.SetTile(sr - 1, sc, stairs);
        break;
      case Dir.East:
        map.SetTile(sr, sc - 1, stairs);
        break;
      case Dir.West:
        map.SetTile(sr, sc + 1, stairs);
        break;
    }
  }

  public Dungeon Generate(int id, string arrivalMessage, int h, int w, int numOfLevels, (int, int) entrance, History history, GameObjectDB objDb, Random rng, List<MonsterDeck> monsterDecks)
  {
    static bool ReplaceChasm(Map map, (int, int) pt)
    {
      switch (map.TileAt(pt).Type)
      {
        case TileType.Chasm:
        case TileType.Bridge:
        case TileType.WoodBridge:
          return true;
        default:
          return false;
      }      
    }

    _dungeonID = id;
    var dungeon = new Dungeon(id, arrivalMessage);
    var mapper = new DungeonMap(rng);
    Map[] levels = new Map[numOfLevels];

    for (int levelNum = 0; levelNum < numOfLevels; levelNum++)
    {
      levels[levelNum] = mapper.DrawLevel(w, h);
      dungeon.AddMap(levels[levelNum]);      
    }

    // Add rivers/chasms and traps to some of the levels
    for (int levelNum = 0; levelNum < numOfLevels - 1; levelNum++)
    {
      if (rng.Next(4) == 0)
      {
        TileType riverTile;
        if (levelNum < numOfLevels - 1 && rng.Next(3) == 0)
          riverTile = TileType.Chasm;
        else
          riverTile = TileType.DeepWater;        
        DungeonMap.AddRiver(levels[levelNum], w + 1, h + 1, riverTile, rng);

        // When making a chasm, we want to turn any walls below chasms on the 
        // floor below into floors. 
        if (riverTile == TileType.Chasm)
        {
          for (int r = 1; r < h; r++)
          {
            for (int c = 1; c < w; c++)
            {
              var pt = (r, c);              
              if (ReplaceChasm(levels[levelNum], pt) && levels[levelNum + 1].IsTile(pt, TileType.DungeonWall))
              {
                levels[levelNum + 1].SetTile(pt, TileFactory.Get(TileType.DungeonFloor));
              }
            }
          }
        }

        if (riverTile == TileType.DeepWater && levelNum > 0)
        {
          monsterDecks[levelNum].Monsters.Add("deep one");
          monsterDecks[levelNum].Monsters.Add("deep one");
          monsterDecks[levelNum].Monsters.Add("deep one");
          monsterDecks[levelNum].Reshuffle(rng);

          DeepOneShrine(levels[levelNum], _dungeonID, levelNum, objDb, rng);
        }
      }

      SetTraps(levels[levelNum], _dungeonID, levelNum, numOfLevels, rng);

      // Sometimes add a secret door or two in hallways
      if (rng.Next(2) == 0)
        PutSecretsDoorInHallways(levels[levelNum], rng);
    }

    SetStairs(levels, h, w, numOfLevels, entrance, rng);
    DecorateDungeon(levels, h, w, numOfLevels, history, objDb, rng);

    for (int levelNum = 0; levelNum < numOfLevels; levelNum++)    
    {
      Vaults.FindPotentialVaults(levels[levelNum], h, w, rng, id, levelNum, objDb, history);
    }
    
    PlaceLevelFiveGate(levels[4], rng, history);

    return dungeon;
  }
}