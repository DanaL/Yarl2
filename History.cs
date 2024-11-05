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

// Herein is where I generate the history of the dungeon and the town
// (or were I hope that code will be once I figure out how to do it...)

// The types of beings who originally built the dungoen
enum OGRulerType
{
  ElfLord,
  DwarfLord,
  //MadWizard
}

enum VillainType
{
  FieryDemon,
  Necromancer
}

class Fact
{
  public static Fact FromStr(string txt)
  {
    var pieces = txt.Split('#');

    switch (pieces[0])
    {      
      case "LocationFact":
        return new LocationFact()
        {
          Loc = Loc.FromStr(pieces[1]),
          Desc = pieces[2]
        };
      case "HistoricalFigure":
        return new HistoricalFigure(pieces[1])
        {
          Title = pieces[2]
        };
      default:
        return new SimpleFact()
        {
          Name = pieces[1],
          Value = pieces[2]
        };
    }
  }
}

class SimpleFact : Fact
{
  public string Name { get; set; } = "";
  public string Value { get; set; } = "";

  public override string ToString() => $"SimpleFact#{Name}#{Value}";
}

class LocationFact : Fact
{
  public Loc Loc { get; set; }
  public string Desc { get; set; } = "";

  public override string ToString() => $"LocationFact#{Loc}#{Desc}";
}

class HistoricalFigure(string name) : Fact
{
  public string Name { get; set; } = name;
  public string Title { get; set; } = "";

  public override string ToString() => $"HistoricalFigure#{Name}#{Title}";
}

class RulerInfo : Fact
{
  public OGRulerType Type { get; set; }
  public string Name { get; set; } = "";
  public string Title { get; set; } = "";
  public string Epithet { get; set; } = "";
  public bool Beloved { get; set; } // Maybe I could classify the epithets they receive?

  public string FullName => $"{Title} {Name} {Epithet}".Trim();
  public override string ToString() => $"RulerInfo#{Name}#{Title}#{Epithet}#{Beloved}";
}

// class to accumulate a list of facts about the world as historical
// events are generated so that they can be reused.
class WorldFacts
{
  NameGenerator _nationNames;
  NameGenerator _peopleNames;
  Random _rng;
  List<string> _nations = [];
  static string[] _nationModifiers = [
      "Kingdom of",
    "Duchy of",
    "Sovereignty of",
    "Islands of",
    "Barony of",
    "North",
    "South",
    "East",
    "West",
    "Greater",
    "Lesser",
    "Nation of",
    "Province of",
    "Upper",
    "Lower"];

  public WorldFacts(Random rng)
  {
    _rng = rng;
    _nationNames = new NameGenerator(_rng, "data/countries.txt");
    _peopleNames = new NameGenerator(_rng, "data/names.txt");
  }

  public string RulerName() => _peopleNames.GenerateName(_rng.Next(5, 10)).Capitalize();

  private string Modifier() => _nationModifiers[_rng.Next(_nationModifiers.Length)];

  public string GetNation()
  {
    if (_nations.Count == 0 || _rng.NextDouble() < 0.25)
    {
      // name a new nation, add it to the list of nations and
      // return it
      var nation = _nationNames.GenerateName(_rng.Next(5, 12)).Capitalize();
      nation = $"{Modifier()} {nation}";

      _nations.Add(nation);

      return nation;
    }

    // otherwise pick an existing nation
    return _nations[_rng.Next(_nations.Count)];
  }
}

// I want to generate a few history events for rulers from a pool customized a
// bit for each ruler type. Some history events can others. Like, Have A Child 
// adds Usurption and Dynasty or Tragedy to the pool?
//
// Events will be classes that know how to generate text or dungeon decorations
// based on them. Text is different for villager knowledge or historical artifact
// and whether or not the ruler was loved

abstract class RulerHistoricalEvent(Random rng)
{
  public abstract List<Decoration> GenerateDecorations(RulerInfo rulerInfo);

  protected Random Rng { get; set; } = rng;

  public string GenerateInvader()
  {
    return "";
  }
}

enum InvaderType
{
  Nation, Barbarians, Dragon, Demon, DarkLord
}

enum DecorationType
{
  Statue, Fresco, Mosaic, ScholarJournal
}

record Decoration(DecorationType Type, string Desc);

class InvasionHistoricalEvent : RulerHistoricalEvent
{
  public string Title { get; set; }
  (InvaderType, string) _invader;
  bool _succesful;
  WorldFacts _facts;

  public InvasionHistoricalEvent(WorldFacts facts, Random rng) : base(rng)
  {
    _succesful = Rng.NextDouble() < 0.5;
    _facts = facts;

    // Invader can be a monster, or another nation
    var roll = Rng.NextDouble();
    if (roll < 0.5)
      _invader = (InvaderType.Nation, $"the {facts.GetNation()}");
    else if (roll < 0.75)
      _invader = (InvaderType.Barbarians, "a barbarian horde");
    else if (roll < 0.85)
      _invader = (InvaderType.Dragon, $"the Great Wyrm {_facts.RulerName()}");
    else if (roll < 0.9)
      _invader = (InvaderType.Demon, $"the Demon Prince {_facts.RulerName()}");
    else
      _invader = (InvaderType.DarkLord, $"the Dark Lord {_facts.RulerName()}");

    Title = $"invasion by {_invader.Item2}";
  }

  string AnonymousStatue(RulerInfo rulerInfo)
  {
    // Variables: successful/not successful, beloved/unloved, ruler type
    switch (rulerInfo.Type)
    {
      case OGRulerType.ElfLord:
        if (_succesful && rulerInfo.Beloved)
          return "a statue depicting a mighty elf, their sword held aloft.";
        else if (_succesful && !rulerInfo.Beloved)
          return "a statue depicting a glaring elf, their boot on the neck of a foe.";
        else if (!_succesful && rulerInfo.Beloved)
          return "a statue of an elf, staring defiantly ahead.";
        else
          return "a statue of a cowering elf.";
      case OGRulerType.DwarfLord:
        if (_succesful && rulerInfo.Beloved)
          return "a statue of a fearsome dwarf, who leans on their axe.";
        else if (_succesful && !rulerInfo.Beloved)
          return "a statue of a dwarf, their cloak covering their face.";
        else if (!_succesful && rulerInfo.Beloved)
          return "a statue of a dwarf who stands protecting their people.";
        else
          return "a statue of a dwarf, kneeling and weeping.";
    }

    throw new Exception("Hmm we don't know about this kind of statue");
  }

  string KnownStatue(RulerInfo rulerInfo)
  {
    if (_succesful)
      return $"a statue depicting {rulerInfo.FullName}, victorious in battle.";
    else if (rulerInfo.Beloved)
      return $"a statue depicting {rulerInfo.FullName}, grim in face.";
    else
      return $"a statue depicting {rulerInfo.FullName}, kneeling, their gaze to the ground.";
  }

  string VisualDesc(RulerInfo rulerInfo)
  {
    string defenders = rulerInfo.Type switch
    {
      OGRulerType.ElfLord => "an elven army",
      OGRulerType.DwarfLord => "dwarven forces"
    };

    if (_succesful)
    {
      return _invader.Item1 switch
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
      return _invader.Item1 switch
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

  string FrescoDesc(RulerInfo rulerInfo)
  {
    var roll = Rng.NextDouble();
    if (roll < 0.5)
      return $"A faded fresco shows {VisualDesc(rulerInfo)}";
    else if (roll < 0.75)
      return $"On the dusty walls you can make out a scene of {VisualDesc(rulerInfo)}";
    else
      return $"A partially destroyed fresco depicts {VisualDesc(rulerInfo)}";
  }

  string MosaicDesc(RulerInfo rulerInfo)
  {
    var roll = Rng.NextDouble();
    if (roll < 0.5)
      return $"On broken mosaic tiles you can make out {VisualDesc(rulerInfo)}";
    else
      return $"A faded mosaic scene of {VisualDesc(rulerInfo)}";
  }

  string StatueDesc(RulerInfo rulerInfo) => 
    Rng.NextDouble() < 0.75 ? AnonymousStatue(rulerInfo) : KnownStatue(rulerInfo);

  string ScholarJournal1()
  {
    return $@"My dear {_facts.RulerName()}, I am here in this dank place researching the {Title}, having been lead here after discovering an old codex in a library in the {_facts.GetNation()} I will...";
  }

  string ScholarJounral2(RulerInfo rulerInfo)
  {
    if (_succesful)
      return $"...have found a scroll extolling the virtues of {rulerInfo.Name} and their victory over {_invader.Item2}...";
    else
      return $"...describes the lamentations of the people after the ravaging {_invader.Item2} and how...";
  }

  string ScholarJounral3(RulerInfo rulerInfo)
  {
    if (_succesful && rulerInfo.Beloved)
      return $"...the inscription read: {rulerInfo.Name}, victorious over {_invader.Item2} was greeted with laurels upon their return...";
    else if (_succesful && !rulerInfo.Beloved)
      return $"...their victory seems to have cemenented their power over the people, who dwelt in fear of {rulerInfo.Name}...";
    else
      return $"...I wish to learn what became of {rulerInfo.FullName} after their devastating defeat in the battle of...";
  }

  // Generate a list of decorations that might be strewn throughout
  // the dungeon
  public override List<Decoration> GenerateDecorations(RulerInfo rulerInfo)
  {
    var decorations = new List<Decoration>
        {
            new(DecorationType.Statue, StatueDesc(rulerInfo)),
            new(DecorationType.Fresco, FrescoDesc(rulerInfo)),
            new(DecorationType.Mosaic, MosaicDesc(rulerInfo)),
            new(DecorationType.ScholarJournal, ScholarJournal1()),
            new(DecorationType.ScholarJournal, ScholarJounral2(rulerInfo)),
            new(DecorationType.ScholarJournal, ScholarJounral3(rulerInfo))
        };

    return decorations;
  }
}

class History(Random rng)
{
  // Storing a plain list of facts and iterating through them might eventually
  // get goofy, but I don't have a sense of how many facts will end up being 
  // generated in a given playthrough. Dozens? Hundreds? A simple list may well
  // suffice in the end.
  public List<Fact> Facts { get; set; } = [];
  WorldFacts _facts;
  public VillainType Villain { get; set; }

  Random _rng = rng;

  public List<Decoration> GetDecorations(RulerInfo rulerInfo)
  {
    var historicalEvent = new InvasionHistoricalEvent(_facts, _rng);
    var decs = historicalEvent.GenerateDecorations(rulerInfo);

    return decs;
  }

  public void CalcDungeonHistory()
  {
    _facts = new WorldFacts(_rng);

    // Okay, we need to know:
    //  1) Who the ruler was/who founded the dungeon
    //  2) Generate a few events in their life
    //  3) How did the dungeon originally falll to ruin

    var type = _rng.Next(2) switch
    {
      0 => OGRulerType.ElfLord,
      1 => OGRulerType.DwarfLord,
      //_ => OGRulerType.MadWizard
    };

    var nameGen = new NameGenerator(_rng, "data/names.txt");
    var name = _facts.RulerName();

    RulerInfo ruler = new()
    {
      Type = type,
      Name = name,
      Title = nameGen.PickTitle(),
      Epithet = nameGen.PickEpithet(),
      Beloved = _rng.NextDouble() < 0.5
    };
    Facts.Add(ruler);
  }

  // This will have to be vastly expanded of course.
  public void GenerateVillain()
  {
    Villain = _rng.NextDouble() < 0.5 ? VillainType.FieryDemon : VillainType.Necromancer;
  }
}