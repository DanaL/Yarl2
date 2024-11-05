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

enum InvaderType
{
  Nation, Barbarians, Dragon, Demon, DarkLord
}

enum DisasterType
{
  Plague, Earthquake, Comet
}

class FactDb
{
  readonly List<Nation> _nations = [];
  public IReadOnlyList<Nation> Nations => _nations;
  readonly List<Fact> _historicalEvents = [];
  public IReadOnlyList<Fact> HistoricalEvents => _historicalEvents;
  public RulerInfo Ruler { get; init; }

  public FactDb(Random rng)
  {
    var type = rng.Next(2) switch
    {
      0 => OGRulerType.ElfLord,
      1 => OGRulerType.DwarfLord,
      //_ => OGRulerType.MadWizard
    };

    var nameGen = new NameGenerator(rng, "data/names.txt");
    var name = nameGen.GenerateName(rng.Next(5, 10)).Capitalize();
    RulerInfo ruler = new()
    {
      Type = type,
      Name = name,
      Title = nameGen.PickTitle(),
      Epithet = nameGen.PickEpithet(),
      Beloved = rng.NextDouble() < 0.5
    };
    Ruler = ruler;
  }

  public void Add(Fact fact)
  {
    if (fact is Nation nation)
      _nations.Add(nation);
    else if (fact is Invasion || fact is Disaster)
      _historicalEvents.Add(fact);
  }
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
      case "RulerInfo":
        return new RulerInfo()
        {
          Name = pieces[1],
          Title = pieces[2],
          Epithet = pieces[3],
          Beloved = pieces[4] == "true"
        };
      case "Nation":
        return new Nation()
        {
          Name = pieces[1],
          Desc = pieces[2]
        };
      case "Invasion":
        return new Invasion()
        {
          Invader = pieces[1],
          Type = (InvaderType)Enum.Parse(typeof(InvaderType), pieces[2]),
          Successful = pieces[3] == "true"
        };
      case "Disaster":
        return new Disaster()
        {
          Desc = pieces[1],
          Type = (DisasterType)Enum.Parse(typeof(DisasterType), pieces[2])
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

class Nation : Fact
{  
  public string Name { get; set; } = "";
  public string Desc { get; set; } = "";

  public string FullName => $"{Desc} {Name}";
  public override string ToString() => $"Nation#{Name}#{Desc}";
}

class HistoricalFigure(string name) : Fact
{
  public string Name { get; set; } = name;
  public string Title { get; set; } = "";

  public override string ToString() => $"HistoricalFigure#{Name}#{Title}";
}

class Disaster : Fact
{
  public string Desc { get; set; } = "";
  public DisasterType Type { get; set; }

  public override string ToString() => $"Disaster#{Desc}#{Type}";
}

class Invasion : Fact
{
  public string Invader { get; set; } = "";
  public InvaderType Type { get; set; }
  public bool Successful { get; set; }

  public override string ToString() => $"Invasion#{Invader}#{Type}#{Successful}";
}

class RulerInfo : Fact
{
  public OGRulerType Type { get; set; }
  public string Name { get; set; } = "";
  public string Title { get; set; } = "";
  public string Epithet { get; set; } = "";
  public bool Beloved { get; set; } // Maybe I could classify the epithets they receive?

  public string FullName => $"{Title} {Name} {Epithet.CapitalizeWords()}".Trim();
  public override string ToString() => $"RulerInfo#{Name}#{Title}#{Epithet}#{Beloved}";
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
  public abstract List<Decoration> GenerateDecorations(RulerInfo rulerInfo, Random rng);

  protected Random Rng { get; set; } = rng;

  public string GenerateInvader()
  {
    return "";
  }
}

class InvasionHistoricalEvent : RulerHistoricalEvent
{
  public string Title { get; set; }
  (InvaderType, string) _invader;
  bool _succesful;
  FactDb FactDb;
  NameGenerator NameGen;

  public InvasionHistoricalEvent(FactDb factDb, Random rng) : base(rng)
  {
    _succesful = Rng.NextDouble() < 0.5;
    
    NameGen = new NameGenerator(rng, "data/names.txt");
    
    // Invader can be a monster, or another nation
    var roll = Rng.NextDouble();
    if (roll < 0.5)
    {
      if (factDb.Nations.Count == 0)
        factDb.Add(new Nation());

      _invader = (InvaderType.Nation, $"the {History.GenNation(rng)}");
    }
    else if (roll < 0.75)
    {
      _invader = (InvaderType.Barbarians, "a barbarian horde");
    }
    else if (roll < 0.85)
    {
      _invader = (InvaderType.Dragon, $"the Great Wyrm {NameGen.GenerateName(rng.Next(5, 10)).Capitalize()}");
    }
    else if (roll < 0.9)
    {
      _invader = (InvaderType.Demon, $"the Demon Prince {NameGen.GenerateName(rng.Next(5, 10)).Capitalize()}");
    }
    else
    {
      _invader = (InvaderType.DarkLord, $"the Dark Lord {NameGen.GenerateName(rng.Next(5, 10)).Capitalize()}");
    }

    Title = $"invasion by {_invader.Item2}";
  }

  string ScholarJournal1(Random rng)
  {
    if (FactDb.Nations.Count == 0 || rng.NextDouble() < 0.2)
      FactDb.Add(History.GenNation(rng));
    string nation = FactDb.Nations[rng.Next(FactDb.Nations.Count)].Name;
    return $@"My dear {NameGen.GenerateName(10)}, I am here in this dank place researching the {Title}, having been lead here after discovering an old codex in a library in the {nation} I will...";
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
  public override List<Decoration> GenerateDecorations(RulerInfo rulerInfo, Random rng)
  {
    var decorations = new List<Decoration>
        {
            new(DecorationType.ScholarJournal, ScholarJournal1(rng)),
            new(DecorationType.ScholarJournal, ScholarJounral2(rulerInfo)),
            new(DecorationType.ScholarJournal, ScholarJounral3(rulerInfo))
        };

    return decorations;
  }
}

class History
{
  // Storing a plain list of facts and iterating through them might eventually
  // get goofy, but I don't have a sense of how many facts will end up being 
  // generated in a given playthrough. Dozens? Hundreds? A simple list may well
  // suffice in the end.
  public FactDb FactDb { get; init; }
  public List<Fact> Facts { get; set; } = [];
  public VillainType Villain { get; set; }
  NameGenerator _nameGen;

  static readonly string[] _adjectives = [
    "blue", "red", "crawling", "winter", "burning", "summer", "slow", "biting", "pale", "rasping",
    "glowing", "wet", "moist", "dry", "silent", "yellow", "second", "third" ];
  static readonly string[] _adjectives2 = [ "blue", "red", "crawling", "winter", "burning", "summer", 
    "silent", "second", "third" ];
    

  static readonly string[] _nationModifiers = [
    "the Kingdom of",
    "the Duchy of",
    "the Sovereignty of",
    "the Islands of",
    "the Barony of",
    "North",
    "South",
    "East",
    "West",
    "Greater",
    "Lesser",
    "the Nation of",
    "the Province of",
    "Upper",
    "Lower" ];
  
  public History(Random rng)
  {    
    _nameGen = new NameGenerator(rng, "data/names.txt");

    FactDb = new FactDb(rng);
  }

  string CometDesc(Random rng)
  {
    switch (rng.Next(3))
    {
      case 0:
        return $"the {_adjectives2[rng.Next(_adjectives2.Length)]} starfall";
      case 1:
        string name = _nameGen.GenerateName(rng.Next(5, 10)).Capitalize();
        return name.Last() == 's' ? $"{name}' comet" : $"{name}'s comet";
      default:
        return $"the {_adjectives2[rng.Next(_adjectives2.Length)]} impact";
    }
  }

  public static Nation GenNation(Random rng)
  {
    NameGenerator ng = new(rng, "data/countries.txt");
    string name = ng.GenerateName(rng.Next(5, 12)).Capitalize();
    double roll = rng.NextDouble();
    if (roll < 0.1)
      name = $"Old {name}";
    else if (roll < 0.2)
      name = $"New {name}";

    return new Nation()
    {
      Name = name,      
      Desc = _nationModifiers[rng.Next(_nationModifiers.Length)]
    };
  }

  Fact GenDisaster(Random rng)
  {
    int roll = rng.Next(3);
    DisasterType type = roll switch
    {
      0 => DisasterType.Plague,
      1 => DisasterType.Earthquake,
      _ => DisasterType.Comet
    };

    string desc = "";    
    switch (type)
    {
      case DisasterType.Plague:
        desc = "the " + _adjectives[rng.Next(_adjectives.Length)] + " ";
        desc += rng.Next(5) switch
        {
          0 => "plague",
          1 => "illness",
          2 => "sickness",
          3 => "rot",
          _ => "fever"
        };
        break;
      case DisasterType.Earthquake:
        desc = "earthquake";
        break;
      case DisasterType.Comet:
        desc = CometDesc(rng);
        break;
    }

    return new Disaster()
    {
      Desc = desc,
      Type = type
    };
  }

  Fact GenInvasion(Random rng)
  {
    InvaderType type;
    string invader;
    double roll = rng.NextDouble();
    if (roll < 0.5)
    {
      if (FactDb.Nations.Count == 0)
        FactDb.Add(GenNation(rng));

      type = InvaderType.Nation;
      invader = FactDb.Nations[rng.Next(FactDb.Nations.Count)].FullName;
    }
    else if (roll < 0.75)
    {
      type = InvaderType.Barbarians;
      invader = "a barbarian horde";
    }
    else if (roll < 0.85)
    {
      type = InvaderType.Dragon;
      invader = $"the Great Wyrm {_nameGen.GenerateName(rng.Next(5, 10)).Capitalize()}";
    }
    else if (roll < 0.9)
    {
      type = InvaderType.Demon;
      invader = $"the Demon Prince {_nameGen.GenerateName(rng.Next(5, 10)).Capitalize()}";
    }
    else
    {
      type = InvaderType.DarkLord;
      invader = $"the Dark Lord {_nameGen.GenerateName(rng.Next(5, 10)).Capitalize()}";
    }

    return new Invasion()
    {
      Invader = invader,
      Type = type,
      Successful = rng.NextDouble() < 0.5
    };
  }

  public void GenerateHistory(Random rng)
  {
    // Villain should be turned into a Fact eventually
    Villain = rng.NextDouble() < 0.5 ? VillainType.FieryDemon : VillainType.Necromancer;

    FactDb.Add(GenDisaster(rng));
    FactDb.Add(GenInvasion(rng));
  }
}