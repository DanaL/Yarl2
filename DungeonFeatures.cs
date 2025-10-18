// Delve - A roguelike computer RPG
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

class Decoration(DecorationType type, string desc)
{
  public static readonly Decoration Null = new NullDecoration();
  public virtual DecorationType Type { get; } = type;
  public virtual string Desc { get; } = desc;
}

class NullDecoration : Decoration
{
  public override DecorationType Type => DecorationType.Statue;
  public override string Desc => "";

  public NullDecoration() : base(DecorationType.Statue, "") { }
}

class Decorations
{  
  public static List<Decoration> GenDecorations(FactDb factDb, Rng rng)
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

    return [.. decorations.Where(d => d is not NullDecoration)];
  }

  static Decoration StatueForEvent(Fact fact, RulerInfo rulerInfo, Rng rng)
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

  static Decoration JournalForEvent1(Fact fact, FactDb factDb, Rng rng)
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

  static Decoration JournalForPlague(Disaster plague, FactDb factDb, Rng rng)
  {
    StringBuilder sb = new();
    NameGenerator ng = new(rng, Util.NamesFile);
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

    return new Decoration(DecorationType.ScholarJournal, sb.ToString());
  }

  static Decoration JournalForEvent2(Fact fact, FactDb factDb, Rng rng)
  {
    if (fact is Invasion invasion)
    {
      return InvasionJournal2(invasion, factDb, rng);
    }

    return Decoration.Null;
  }

  static Decoration JournalForEvent3(Fact fact, FactDb factDb, Rng rng)
  {
    if (fact is Invasion invasion)
    {
      return InvasionJournal3(invasion, factDb, rng);
    }

    return Decoration.Null;
  }

  static Decoration MosaicForEvent(Fact fact, RulerInfo rulerInfo, Rng rng)
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

  static Decoration FrescoeForEvent(Fact fact, RulerInfo rulerInfo, Rng rng)
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

  static Decoration InvasionJournal(Invasion invasion, FactDb factDb, Rng rng)
  {
    if (factDb.Nations.Count == 0 || rng.NextDouble() < 0.2)
      factDb.Add(History.GenNation(rng));

    string nation = factDb.Nations[rng.Next(factDb.Nations.Count)].Name;
    NameGenerator ng = new(rng, Util.NamesFile);
    string text = $@"My dear {ng.GenerateName(rng.Next(8, 12)).Capitalize()}, I am here in this dank place researching the invasion by {invasion.Invader}, having been lead here after discovering an old codex in a library in {nation} I will...";

    return new Decoration(DecorationType.ScholarJournal, text);
  }

  static Decoration InvasionJournal2(Invasion invasion, FactDb factDb, Rng rng)
  {
    string desc;
    if (invasion.Successful)
      desc = $"...have found a scroll extolling the virtues of {factDb.Ruler.Name} and their victory over {invasion.Invader}...";
    else
      desc = $"...describes the lamentations of the people after the ravaging {invasion.Invader} and how...";

    return new Decoration(DecorationType.ScholarJournal, desc);
  }

  static Decoration InvasionJournal3(Invasion invasion, FactDb factDb, Rng rng)
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
      OGRulerType.DwarfLord => "dwarven forces",
      _ => ""
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

  static Decoration InvasionMosaic(Invasion invasion, RulerInfo rulerInfo, Rng rng)
  {
    var roll = rng.NextDouble();
    string desc;
    if (roll < 0.5)
      desc = $"On broken mosaic tiles you can make out {InvasionScene(invasion, rulerInfo)}";
    else
      desc = $"A faded mosaic scene of {InvasionScene(invasion, rulerInfo)}";

    return new Decoration(DecorationType.Mosaic, desc);
  }

  static Decoration InvasionFrescoe(Invasion invasion, RulerInfo rulerInfo, Rng rng)
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

  static Decoration InvasionStatue(Invasion invasion, RulerInfo rulerInfo, Rng rng)
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
  public static void MakeAltar(int dungeonID, Map[] levels, GameObjectDB objDb, FactDb factDb, Rng rng, int level)
  {
    Map altarLevel = levels[level];
    Tile sacredSq;
    var closets = DungeonBuilder.PotentialClosets(altarLevel);
    if (closets.Count > 0)
    {
      var (closetR, closetC, altarR, altarC, wallR, wallC) = closets[rng.Next(closets.Count)];

      Item idol = new() { Name = "idol", Type = ItemType.Trinket, Value = 10 };
      string altarDesc;

      switch (rng.Next(3))
      {
        case 0:
          idol.Glyph = new Glyph('"', Colours.YELLOW, Colours.YELLOW, Colours.BLACK, false);
          idol.Traits.Add(new AdjectiveTrait("golden"));
          idol.Traits.Add(new AdjectiveTrait("crescent-shaped"));
          idol.Traits.Add(new DescriptionTrait("A gold, carved crescent moon, decorated with arcane symbols."));
          sacredSq = TileFactory.Get(TileType.DungeonFloor);
          altarDesc = "An altar carved with arcane depictions of the moon.";
          break;
        case 1:
          idol.Name = "tree branch";
          idol.Glyph = new Glyph('"', Colours.LIGHT_BROWN, Colours.BROWN, Colours.BLACK, false);
          idol.Traits.Add(new AdjectiveTrait("strange"));
          idol.Traits.Add(new AdjectiveTrait("rune-carved"));
          idol.Traits.Add(new DescriptionTrait("A branch carved with druidic runes and pictograms."));
          idol.Traits.Add(new FlammableTrait());
          sacredSq = TileFactory.Get(TileType.OrangeTree);
          altarDesc = "An engraving of the World Tree.";
          break;
        default:
          idol.Name = "carving";
          idol.Glyph = new Glyph('"', Colours.WHITE, Colours.LIGHT_GREY, Colours.BLACK, false);
          idol.Traits.Add(new AdjectiveTrait("soapstone"));
          altarDesc = "An altar venerating the Leviathan.";
          idol.Traits.Add(new DescriptionTrait("A worn soapstone statue of a sea creature."));
          sacredSq = TileFactory.Get(TileType.Pool);
          break;
      }

      // The idol will be found +/- 2 levels of the altar's level
      int lvlLo = int.Max(level - 3, 0);
      int lvlHi = int.Max(level, level + 2) - 1;
      int idolLevel = rng.Next(lvlLo, lvlHi);

      List<Loc> floors = [.. levels[idolLevel].SqsOfType(TileType.DungeonFloor)
                                  .Select(sq => new Loc(dungeonID, idolLevel, sq.Item1, sq.Item2))
                                  .Where(l => !objDb.HazardsAtLoc(l))];
      Loc idolLoc = floors[rng.Next(floors.Count)];
      objDb.Add(idol);
      objDb.SetToLoc(idolLoc, idol);

      Loc prizeLoc = new(dungeonID, level, closetR, closetC);

      Item prize = PickPrize(objDb, rng);
      objDb.SetToLoc(prizeLoc, prize);

      prize = PickPrize(objDb, rng);
      objDb.SetToLoc(prizeLoc, prize);

      Tile altar = new IdolAltar(altarDesc)
      {
        IdolID = idol.ID,
        Wall = new Loc(dungeonID, level, wallR, wallC)
      };
      levels[level].SetTile(altarR, altarC, altar);
      levels[level].SetTile(closetR, closetC, sacredSq);

      if (rng.Next(3) == 0)
      {
        string s = rng.NextDouble() <= 0.5 ? "shadow" : "ghoul";
        Actor monster = MonsterFactory.Get(s, objDb, rng);
        objDb.AddNewActor(monster, prizeLoc);
      }
    }

    Item PickPrize(GameObjectDB objDb, Rng rng) => rng.Next(5) switch
    {
      0 => ItemFactory.Get(ItemNames.POTION_HARDINESS, objDb),
      1 => ItemFactory.Get(ItemNames.HILL_GIANT_ESSENCE, objDb),
      2 => ItemFactory.Get(ItemNames.FIRE_GIANT_ESSENCE, objDb),
      3 => ItemFactory.Get(ItemNames.FROST_GIANT_ESSENCE, objDb),
      _ => ItemFactory.Get(ItemNames.MITHRIL_ORE, objDb)
    };
  }
}

class CaptiveFeature
{
  public static void Create(int dungeonId, int level, Map map, GameObjectDB objDb, FactDb factDb, Rng rng)
  {
    var cells = DungeonBuilder.PotentialClosets(map);
    if (cells.Count == 0)
      return;

    var (cellR, cellC, _, _, gateR, gateC) = cells[rng.Next(cells.Count)];

    map.SetTile(cellR, cellC, TileFactory.Get(TileType.DungeonFloor));
    map.SetTile(gateR, gateC, new Portcullis(false));

    HashSet<(int, int)> leverSqs = [];
    for (int r = cellR - 5; r < cellR + 5; r++)
    {
      for (int c = cellC - 5; c < cellC + 5; c++)
      {
        if (!map.InBounds(r, c) || map.TileAt(r, c).Type != TileType.DungeonWall)
          continue;
        if (Util.Distance(r, c, gateR, gateC) == 1)
          continue;

        // Prevent lever from being generated inside the cell :P
        if (Util.Distance(r, c, cellR, cellC) == 1)
          continue;

        int floors = 0;
        foreach (var (adjR, adjC) in Util.Adj4Sqs(r, c))
        {
          if (map.TileAt(adjR, adjC).Type == TileType.DungeonFloor)
            ++floors;
        }

        if (floors > 0)
          leverSqs.Add((r, c));
      }
    }

    // kind of assuming there will always be at least one...
    Loc gateLoc = new(dungeonId, level, gateR, gateC);
    var (leverR, leverC) = leverSqs.ToList()[rng.Next(leverSqs.Count)];
    Lever lever = new(TileType.Lever, false, gateLoc);
    map.SetTile(leverR, leverC, lever);

    MakePrisoner(cellR, cellC, dungeonId, level, map, objDb, factDb, rng);
  }

  static void MakePrisoner(int cellRow, int cellCol, int dungeonId, int level, Map map, GameObjectDB objDb, FactDb factDb, Rng rng)
  {
    
    Loc cell = new(dungeonId, level, cellRow, cellCol);
    NameGenerator ng = new(rng, Util.NamesFile);

    List<string> species = ["human", "elf", "half-elf", "gnome", "dwarf", "orc", "half-orc"];
    string s = species[rng.Next(species.Count)];
    Mob prisoner = new()
    {
      Name = ng.GenerateName(rng.Next(5, 9)),
      Glyph = new Glyph('@', Colours.FAINT_PINK, Colours.PINK, Colours.BLACK, false),
      Appearance = $"A disheveled, exhausted-looking {s}."
    };
    prisoner.Traits.Add(new VillagerTrait());
    prisoner.Traits.Add(new NamedTrait());
    prisoner.Traits.Add(new IntelligentTrait());
    prisoner.Traits.Add(new BehaviourTreeTrait() { Plan = "PrisonerPlan" });

    // My intent right now is for there to be only one prisoner per run, at least of the type
    // who grants a boon
    string captors = SetCaptors(cell, map, objDb, factDb, rng);
    factDb.Add(new SimpleFact() { Name="ImprisonedBy", Value=captors });
    PrisonerTrait pt = new() { SourceId = prisoner.ID, Cell = cell };
    prisoner.Traits.Add(pt);
    objDb.EndOfRoundListeners.Add(pt);

    // Were the captors going to sacrifice the prisoner?
    if (captors == "cultists" || rng.NextDouble() < 0.2)
    {
      SetCreepyAltar(cell, objDb, map, rng);
      prisoner.Traits.Add(new DialogueScriptTrait() { ScriptFile = "prisoner1.txt" });
    }
    else
    {
      prisoner.Traits.Add(new DialogueScriptTrait() { ScriptFile = "prisoner2.txt" });
    }

    prisoner.SetBehaviour(new PrisonerBehaviour());
    
    prisoner.Stats[Attribute.DialogueState] = new Stat(0);
    prisoner.Stats[Attribute.HP] = new Stat(15);
        
    objDb.AddNewActor(prisoner, cell);
  }

  static int AdjWalls(Map map, int r, int c)
  {
    int walls = 0;
    foreach (var sq in Util.Adj8Sqs(r, c))
    {
      Tile tile = map.TileAt(sq);
      if (tile.Type == TileType.DungeonWall || tile.Type == TileType.PermWall || tile.Type == TileType.WorldBorder)
        ++walls;
    }

    return walls;
  }

  static void SetCreepyAltar(Loc cell, GameObjectDB objDb, Map map, Rng rng)
  {
    List<Loc> sqsNearCell = [];
    for (int r = cell.Row - 4; r <= cell.Row + 4; r++)
    {
      for (int c = cell.Col - 4; c <= cell.Col + 4; c++)
      {
        if (!map.InBounds(r, c))
          continue;
        if (map.TileAt(r, c).Type != TileType.DungeonFloor)
          continue;
        if (AdjWalls(map, r, c) >= 3)
          continue;

        Loc loc = cell with { Row = r, Col = c };        
        sqsNearCell.Add(loc);
      }
    }

    if (sqsNearCell.Count > 0)
    {
      Loc altarLoc = sqsNearCell[rng.Next(sqsNearCell.Count)];
      Item altar = ItemFactory.Get(ItemNames.STONE_ALTAR, objDb);
      altar.Glyph = new Glyph('∆', Colours.DULL_RED, Colours.BROWN, Colours.BLACK, false);
      altar.Traits.Add(new MolochAltarTrait());
      objDb.SetToLoc(altarLoc, altar);
    }
  }

  static string SetCaptors(Loc cell, Map map, GameObjectDB objDb, FactDb factDb, Rng rng)
  {
    string captors;
    if (cell.Level < 5)
    {
      string earlyDenizen = factDb.FactCheck("EarlyDenizen") is SimpleFact fact ? fact.Value.Pluralize() : "";
      captors = rng.NextDouble() < 0.25 ? "cultists" : earlyDenizen;
    }
    else
    {
      captors = rng.Next(3) switch
      {
        0 => "ogres",
        1 => "duergar",
        _ => "drow"
      };
    }

    List<Loc> sqsNearCell = [];
    for (int r = cell.Row - 5; r <= cell.Row + 5; r++)
    {
      for (int c = cell.Col - 5; c <= cell.Col + 5; c++)
      {
        if (!map.InBounds(r, c))
          continue;
        if (!map.TileAt(r, c).Passable())
          continue;
        Loc loc = cell with { Row = r, Col = c };
        if (!Util.GoodFloorSpace(objDb, loc) && !objDb.Occupied(loc))
          continue;

        sqsNearCell.Add(loc);
      }
    }

    if (sqsNearCell.Count < 3)
      return captors;

    switch (captors)
    {
      case "kobolds":
        PlaceMonster("kobold", objDb, sqsNearCell);
        PlaceMonster("kobold", objDb, sqsNearCell);
        PlaceMonster("kobold foreman", objDb, sqsNearCell);
        break;
      case "goblins":
        PlaceMonster("hobgoblin", objDb, sqsNearCell);
        PlaceMonster("goblin", objDb, sqsNearCell);
        PlaceMonster("goblin", objDb, sqsNearCell);
        break;
      case "cultists":
        PlaceMonster("cult leader", objDb, sqsNearCell);
        PlaceMonster("cultist", objDb, sqsNearCell);
        PlaceMonster("cultist", objDb, sqsNearCell);
        break;
      case "drow":
        PlaceMonster("drow warrior", objDb, sqsNearCell);
        PlaceMonster("drow warrior", objDb, sqsNearCell);
        PlaceMonster("drow warrior", objDb, sqsNearCell);
        break;
      case "duergar":
        PlaceMonster("duergar soldier", objDb, sqsNearCell);
        PlaceMonster("duergar soldier", objDb, sqsNearCell);
        PlaceMonster("duergar soldier", objDb, sqsNearCell);
        break;
      case "ogres":
        PlaceMonster("ogre", objDb, sqsNearCell);
        PlaceMonster("ogre", objDb, sqsNearCell);
        break;
    }

    return captors;

    void PlaceMonster(string name, GameObjectDB objDb, List<Loc> sqs)
    {
      Actor monster = MonsterFactory.Get(name, objDb, rng);
      int i = rng.Next(sqsNearCell.Count);
      Loc loc = sqsNearCell[i];
      sqsNearCell.RemoveAt(i);
      objDb.AddNewActor(monster, loc);
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

  static List<(int, int)> TryToDrawTunnel(Map map, int r, int c, Rng rng)
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

  static void DecorateTunnel(int dungeonID, int level, List<(int, int)> tunnel, GameObjectDB objDb, Rng rng)
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

  public static void MakeCollapsedTunnel(int dungeonID, int level, Map map, GameObjectDB objDb, Rng rng)
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