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

enum DecorationType
{
  Statue, Fresco, Mosaic, ScholarJournal
}

class Decoration
{
  public static readonly Decoration Null = new NullDecoration();
  public virtual DecorationType Type { get; }
  public virtual string Desc { get; }

  public Decoration(DecorationType type, string desc)
  {
    Type = type;
    Desc = desc;
  }
}

class NullDecoration : Decoration
{
  public override DecorationType Type => DecorationType.Statue;
  public override string Desc => "";

  public NullDecoration() : base(DecorationType.Statue, "") { }
}

class Decorations
{  
  public static List<Decoration> GenDecorations(History history, Random rng)
  {
    List<Decoration> decorations = [];
    RulerInfo rulerInfo = history.FactDb.Ruler;

    foreach (Fact fact in history.FactDb.HistoricalEvents)
    {
      decorations.Add(StatueForEvent(fact, rulerInfo, rng));
      decorations.Add(FrescoeForEvent(fact, rulerInfo, rng));
      decorations.Add(MosaicForEvent(fact, rulerInfo, rng));
      // decorations.Add(Journal1(fact));
      // decorations.Add(Journal2(fact));
      // decorations.Add(Journal3(fact));
    }

    return decorations.Where(d => d is not NullDecoration).ToList();
  }

  static Decoration StatueForEvent(Fact fact, RulerInfo rulerInfo, Random rng)
  {
    if (fact is Invasion invasion)
    {
      return InvasionStatue(invasion, rulerInfo, rng);
    }
    else if (fact is Disaster disaster)
    {

    }

    return Decoration.Null;
  }

  static Decoration MosaicForEvent(Fact fact, RulerInfo rulerInfo, Random rng)
  {
    if (fact is Invasion invasion)
    {
      return InvasionMosaic(invasion, rulerInfo, rng);
    }
    else if (fact is Disaster disaster)
    {

    }

    return Decoration.Null;
  }

  static Decoration FrescoeForEvent(Fact fact, RulerInfo rulerInfo, Random rng)
  {
    if (fact is Invasion invasion)
    {
      return InvasionFrescoe(invasion, rulerInfo, rng);
    }
    else if (fact is Disaster disaster)
    {

    }

    return Decoration.Null;
  }

  static string InvasionScene(Invasion invasion, RulerInfo rulerInfo)
  {
    string defenders = rulerInfo.Type switch
    {
      OGRulerType.ElfLord => "an elven army",
      OGRulerType.DwarfLord => "dwarven forces"
    };

    if (invasion.Successful)
    {
      return invasion.Type switch
      {
        InvaderType.Nation => $"{defenders} driving back an invading army.",
        InvaderType.Dragon => $"{defenders} facing a mighty dragon.",
        InvaderType.Barbarians => $"{defenders} clashing with a barbarian horde.",
        InvaderType.Demon => $"{defenders} facing a terrible demon.",
        InvaderType.DarkLord => $"{defenders} in victory over an army of goblins and kobolds.",
        _ => throw new Exception("Hmm I don't know about that invader type")
      };
    }
    else
    {
      return invasion.Type switch
      {
        InvaderType.Nation => $"{defenders} fleeing an invading army.",
        InvaderType.Dragon => $"a terrible dragon devouring {defenders}.",
        InvaderType.Barbarians => $"{defenders} fleeing a barbarian horde.",
        InvaderType.Demon => $"a horrific demon destroying {defenders}.",
        InvaderType.DarkLord => $"{defenders} falling before army of goblins and kobolds.",
        _ => throw new Exception("Hmm I don't know about that invader type")
      };
    }    
  }

  static Decoration InvasionMosaic(Invasion invasion, RulerInfo rulerInfo, Random rng)
  {
    var roll = rng.NextDouble();
    string desc;
    if (roll < 0.5)
      desc = $"On broken mosaic tiles you can make out {InvasionScene(invasion, rulerInfo)}";
    else
      desc = $"A faded mosaic scene of {InvasionScene(invasion, rulerInfo)}";

    return new Decoration(DecorationType.Mosaic, desc);
  }

  static Decoration InvasionFrescoe(Invasion invasion, RulerInfo rulerInfo, Random rng)
  {
    string desc;
    double roll = rng.NextDouble();
    if (roll < 0.5)
      desc = $"A faded fresco shows {InvasionScene(invasion, rulerInfo)}";
    else if (roll < 0.75)
      desc = $"On the dusty walls you can make out a scene of {InvasionScene(invasion, rulerInfo)}";
    else
      desc = $"A partially destroyed fresco depicts {InvasionScene(invasion, rulerInfo)}";

    return new Decoration(DecorationType.Fresco, desc);
  }

  static Decoration InvasionStatue(Invasion invasion, RulerInfo rulerInfo, Random rng)
  {
    string desc = "";

    if (rng.NextDouble() < 0.75)
    {
      switch (rulerInfo.Type)
      {
        case OGRulerType.ElfLord:
          if (invasion.Successful && rulerInfo.Beloved)
            desc = "a statue depicting a mighty elf, their sword held aloft.";
          else if (invasion.Successful && !rulerInfo.Beloved)
            desc = "a statue depicting a glaring elf, their boot on the neck of a foe.";
          else if (!invasion.Successful && rulerInfo.Beloved)
            desc = "a statue of an elf, staring defiantly ahead.";
          else
            desc = "a statue of a cowering elf.";
          break;
        case OGRulerType.DwarfLord:
          if (invasion.Successful && rulerInfo.Beloved)
            desc = "a statue of a fearsome dwarf, who leans on their axe.";
          else if (invasion.Successful && !rulerInfo.Beloved)
            desc = "a statue of a dwarf, their cloak covering their face.";
          else if (!invasion.Successful && rulerInfo.Beloved)
            desc = "a statue of a dwarf who stands protecting their people.";
          else
            desc = "a statue of a dwarf, kneeling and weeping.";
          break;
      }
    }
    else
    {
      if (invasion.Successful)
        desc = $"a statue depicting {rulerInfo.FullName}, victorious in battle.";
      else if (rulerInfo.Beloved)
        desc = $"a statue depicting {rulerInfo.FullName}, grim in face.";
      else
        desc = $"a statue depicting {rulerInfo.FullName}, kneeling, their gaze to the ground.";
    }

    return new Decoration(DecorationType.Statue, desc);
  }
}

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

  public static void MakeAltar(int dungeonID, Map[] levels, GameObjectDB objDb, History history, Random rng, int level)
  {
    Map altarLevel = levels[level];
    Tile sacredSq;
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
          idol.Traits.Add(new DescriptionTrait("A gold, carved crescent moon, decorated with arcane symbols."));
          sacredSq = TileFactory.Get(TileType.DungeonFloor);
          altarDesc = "An altar carved with arcane depictions of the moon.";
          break;
        case 1:
          idol.Name = "tree branch";
          idol.Glyph = new Glyph('"', Colours.LIGHT_BROWN, Colours.BROWN, Colours.BLACK, Colours.BLACK);
          idol.Traits.Add(new AdjectiveTrait("strange"));
          idol.Traits.Add(new AdjectiveTrait("rune-carved"));
          idol.Traits.Add(new DescriptionTrait("A branch carved with druidic runes and pictograms."));
          idol.Traits.Add(new FlammableTrait());
          sacredSq = TileFactory.Get(TileType.OrangeTree);
          altarDesc = "An engraving of the World Tree.";
          break;
        default:
          idol.Name = "carving";
          idol.Glyph = new Glyph('"', Colours.WHITE, Colours.LIGHT_GREY, Colours.BLACK, Colours.BLACK);
          idol.Traits.Add(new AdjectiveTrait("soapstone"));
          altarDesc = "An altar venerating the leviathan.";
          idol.Traits.Add(new DescriptionTrait("A worn soapstone statue of a sea creature."));
          sacredSq = TileFactory.Get(TileType.Pool);
          break;
      }

      objDb.Add(idol);
      Loc idolLoc = new(dungeonID, level, altarR, altarC);
      objDb.SetToLoc(idolLoc, idol);

      Item prize = Artifacts.GenArtifact(objDb, history, rng);
      Loc prizeLoc = new(dungeonID, level, closetR, closetC);
      objDb.SetToLoc(prizeLoc, prize);

      Tile altar = new IdolAltar(altarDesc)
      {
        IdolID = idol.ID,
        Wall = new Loc(dungeonID, level, wallR, wallC)
      };
      levels[level].SetTile(altarR, altarC, altar);
      levels[level].SetTile(closetR, closetC, sacredSq);
    }
  }
}