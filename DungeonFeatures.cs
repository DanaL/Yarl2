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

using System.Text;

namespace Yarl2;

// Holds dungeon features/room code because DungeonBuilder was getting too big.
// There's probably code that can be moved over from DungeonBuilder but I'm not
// sure how to orgainize it yet.

// Maybe also a good place to centralize code for finding rooms in the map.

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
  public static List<Decoration> GenDecorations(FactDb factDb, Random rng)
  {
    List<Decoration> decorations = [];
    RulerInfo rulerInfo = factDb.Ruler;

    foreach (Fact fact in factDb.HistoricalEvents)
    {
      decorations.Add(StatueForEvent(fact, rulerInfo, rng));
      decorations.Add(FrescoeForEvent(fact, rulerInfo, rng));
      decorations.Add(MosaicForEvent(fact, rulerInfo, rng));
      decorations.Add(JournalForEvent1(fact, factDb, rng));
      decorations.Add(JournalForEvent2(fact, factDb, rng));
      decorations.Add(JournalForEvent3(fact, factDb, rng));      
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

  static Decoration JournalForEvent1(Fact fact, FactDb factDb, Random rng)
  {
    if (fact is Invasion invasion)
    {
      return InvasionJournal(invasion, factDb, rng);
    }
    else if (fact is Disaster disaster && disaster.Type == DisasterType.Plague)
    {
      return JournalForPlague(disaster, factDb, rng);
    }

    return Decoration.Null;
  }

  static Decoration JournalForPlague(Disaster plague, FactDb factDb, Random rng)
  {
    StringBuilder sb = new();
    NameGenerator ng = new NameGenerator(rng, "data/names.txt");
    // When more historical facts and such are generated I can have the text
    // be like "After the Battle of Blah Blah, there was a brief respite of
    // prosperity before they were struck by the plague of..."
    sb.Append("An old codex speaks of a contagion that spread across the land called ");
    sb.Append(plague.Desc.CapitalizeWords());
    sb.Append(". The academics debate if this was due to natural causes or");

    switch (rng.Next(3))
    {
      case 0:
        sb.Append(" due to ");
        sb.Append(factDb.Ruler.Name);
        sb.Append(" turning away from the teachings of ");
        int roll = rng.Next(3);
        if (roll == 0)
          sb.Append("Huntokar.");
        else if (roll == 1)
          sb.Append("the Moon Daughters.");
        else
          sb.Append("the Crimson King.");
        break;
      case 1:
        sb.Append(" wrought by the mad experiments of ");
        sb.Append(ng.GenerateName(rng.Next(6, 10)).Capitalize());
        sb.Append('.');
        break;
      default:
        sb.Append(" brought to the land by the machinations of ");
        sb.Append(factDb.VillainName);
        sb.Append('.');
        break;
    }

    sb.Append(" It is to investigate this matter I have come to this place. Surely any remnants of the illness have long since faded to nothing?");

    Console.WriteLine(sb.ToString());
    return new Decoration(DecorationType.ScholarJournal, sb.ToString());
  }

  static Decoration JournalForEvent2(Fact fact, FactDb factDb, Random rng)
  {
    if (fact is Invasion invasion)
    {
      return InvasionJournal2(invasion, factDb, rng);
    }

    return Decoration.Null;
  }

  static Decoration JournalForEvent3(Fact fact, FactDb factDb, Random rng)
  {
    if (fact is Invasion invasion)
    {
      return InvasionJournal3(invasion, factDb, rng);
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

  static Decoration InvasionJournal(Invasion invasion, FactDb factDb, Random rng)
  {
    if (factDb.Nations.Count == 0 || rng.NextDouble() < 0.2)
      factDb.Add(History.GenNation(rng));

    string nation = factDb.Nations[rng.Next(factDb.Nations.Count)].Name;
    NameGenerator ng = new NameGenerator(rng, "data/names.txt");
    string text = $@"My dear {ng.GenerateName(rng.Next(8, 12)).Capitalize()}, I am here in this dank place researching the invasion by {invasion.Invader}, having been lead here after discovering an old codex in a library in {nation} I will...";

    return new Decoration(DecorationType.ScholarJournal, text);
  }

  static Decoration InvasionJournal2(Invasion invasion, FactDb factDb, Random rng)
  {
    string desc;
    if (invasion.Successful)
      desc = $"...have found a scroll extolling the virtues of {factDb.Ruler.Name} and their victory over {invasion.Invader}...";
    else
      desc = $"...describes the lamentations of the people after the ravaging {invasion.Invader} and how...";

    return new Decoration(DecorationType.ScholarJournal, desc);
  }

  static Decoration InvasionJournal3(Invasion invasion, FactDb factDb, Random rng)
  {
    RulerInfo rulerInfo = factDb.Ruler;
    string desc;
    if (invasion.Successful && rulerInfo.Beloved)
      desc = $"...the inscription read: {rulerInfo.Name}, victorious over {invasion.Invader} was greeted with laurels upon their return...";
    else if (invasion.Successful && !rulerInfo.Beloved)
      desc = $"...their victory seems to have cemenented their power over the people, who dwelt in fear of {rulerInfo.Name}...";
    else
      desc = $"...I wish to learn what became of {rulerInfo.FullName} after their devastating defeat in the battle of...";

    return new Decoration(DecorationType.ScholarJournal, desc);
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

  public static void MakeAltar(int dungeonID, Map[] levels, GameObjectDB objDb, FactDb factDb, Random rng, int level)
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

      // The idol will be found +/- 2 levels of the altar's level
      int lvlLo = int.Max(level - 3, 0);
      int lvlHi = int.Max(level, level + 2) - 1;
      int idolLevel = rng.Next(lvlLo, lvlHi);
      var (idolR, idolC) = levels[idolLevel].RandomTile(TileType.DungeonFloor, rng);
      Loc idolLoc = new(dungeonID, idolLevel, idolR, idolC);
      objDb.Add(idol);
      objDb.SetToLoc(idolLoc, idol);

      Item prize = Artifacts.GenArtifact(objDb, factDb, rng);
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

class TunnelCarver
{
  static List<(int, int)> FindStartCandidates(Map map)
  {
    var candidates = new List<(int, int)>();

    for (int r = 5; r < map.Height - 5; r++) 
    {
      for (int c = 5; c < map.Width - 5; c++)
      {
        Tile tile = map.TileAt(r, c);
        if (tile.Type != TileType.DungeonWall)
          continue;

        bool adjFloor = false;
        foreach (var sq in Util.Adj4Sqs(r, c))
        {
          if (map.TileAt(sq).Type == TileType.DungeonFloor) 
          {
            adjFloor = true;
            break;
          }
        }
        if (!adjFloor)
          continue;

        int wallCount = 0;
        foreach (var sq in Util.Adj8Sqs(r, c))
        {
          if (map.TileAt(sq).Type == TileType.DungeonWall)
            wallCount++;
        }        
        if (wallCount >= 5)
          candidates.Add((r, c));
      }      
    }

    return candidates;
  }

  static int TryDirection(Map map, int r, int c, int dr, int dc, (int r, int c) n1, (int r, int c) n2)
  {
    int length = 0;

    while (length < 5)
    {
      if (!map.InBounds(r, c))
        break;
      if (map.TileAt(r, c).Type != TileType.DungeonWall)
        break;
      if (map.TileAt(r + n1.r, c + n1.c).Type != TileType.DungeonWall)
        break;
      if (map.TileAt(r + n2.r, c + n2.c).Type != TileType.DungeonWall)
        break;

      r += dr;
      c += dc;
      ++length;
    }

    return length;
  }

  static List<(int, int)> CarveTunnel(Map map, int r, int c, int dr, int dc, int length)
  {
    var tunnel = new List<(int, int)>();

    for (int i = 0; i < length; i++)
    {
      map.SetTile(r, c, TileFactory.Get(TileType.DungeonFloor));
      tunnel.Add((r, c));
      r += dr;
      c += dc;
    }

    return tunnel;
  }

  static List<(int, int)> TryToDrawTunnel(Map map, int r, int c, Random rng)
  {    
    // Try upwards
    int dr = -1;
    int dc = 0;
    int len = TryDirection(map, r, c, dr, dc, n1: (0, -1), n2: (0, 1));
    if (len > 2)
    {      
      int tunnelLength = rng.Next(3, len + 1);
      return CarveTunnel(map, r, c, dr, dc, tunnelLength);
    }

    // Try downwards
    dr = 1;
    dc = 0;
    len = TryDirection(map, r, c, dr, dc, n1: (0, -1), n2: (0, 1));
    if (len > 2)
    {
      int tunnelLength = rng.Next(3, len + 1);
      return CarveTunnel(map, r, c, dr, dc, tunnelLength);
    }

    // Try left
    dr = 0;
    dc = -1;  
    len = TryDirection(map, r, c, dr, dc, n1: (-1, 0), n2: (1, 0));
    if (len > 2)
    {
      int tunnelLength = rng.Next(3, len + 1);
      return CarveTunnel(map, r, c, dr, dc, tunnelLength);
    }

    // Try right
    dr = 0;
    dc = 1;
    len = TryDirection(map, r, c, dr, dc, n1: (-1, 0), n2: (1, 0));
    if (len > 2)
    {
      int tunnelLength = rng.Next(3, len + 1);
      return CarveTunnel(map, r, c, dr, dc, tunnelLength);
    }

    return [];
  }

  static void DecorateTunnel(int dungeonID, int level, List<(int, int)> tunnel, GameObjectDB objDb, Random rng)
  {
    // We want to have at least one rubble-blocked square
    int i = rng.Next(tunnel.Count);
    Loc loc = new(dungeonID, level, tunnel[i].Item1, tunnel[i].Item2);
    Item rubble = ItemFactory.Get(ItemNames.RUBBLE, objDb);
    objDb.SetToLoc(loc, rubble);
    tunnel.RemoveAt(i);

    // Add some treasure to first location
    int numTreasure = rng.Next(2, 6);
    for (int j = 0; j < numTreasure; j++)
    {
      Item treasure = rng.Next(3) switch
      {
        0 => Treasure.ItemByQuality(TreasureQuality.Common, objDb, rng),  
        1 => Treasure.ItemByQuality(TreasureQuality.Uncommon, objDb, rng),
        _ => Treasure.ItemByQuality(TreasureQuality.Good, objDb, rng)
      };      
      objDb.SetToLoc(loc, treasure);
    }

    // Maybe a skull (or eventually other remains?)
    if (rng.Next(3) == 0)
    {
      Item skull = ItemFactory.Get(ItemNames.SKULL, objDb);
      objDb.SetToLoc(loc, skull);
    }

    for (i = 0; i < tunnel.Count; i++)
    {
      if (rng.Next(5) == 0)
      {
        loc = new(dungeonID, level, tunnel[0].Item1, tunnel[0].Item2);
        rubble = ItemFactory.Get(ItemNames.RUBBLE, objDb);
        objDb.SetToLoc(loc, rubble);
      }
      tunnel.RemoveAt(0);
    }
  }

  public static void MakeCollapsedTunnel(int dungeonID, int level, Map map, GameObjectDB objDb, Random rng)
  {
     List<(int, int)> candidates = FindStartCandidates(map);
    
    // Try up to, I don't know, 50? times
    int tryCount = 0;
    do
    {
      int i = rng.Next(candidates.Count);
      List<(int, int)> tunnel = TryToDrawTunnel(map, candidates[i].Item1, candidates[i].Item2, rng);
      if (tunnel.Count > 0)
      {
        DecorateTunnel(dungeonID, level, tunnel, objDb, rng);
        break;
      }

      candidates.RemoveAt(i);

      ++tryCount;
    }
    while (tryCount < 50);
  }
}