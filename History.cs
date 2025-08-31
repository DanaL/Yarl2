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

class FactDb(RulerInfo ruler)
{
  readonly List<Nation> _nations = [];
  public IReadOnlyList<Nation> Nations => _nations;
  readonly List<Fact> _historicalEvents = [];
  public IReadOnlyList<Fact> HistoricalEvents => _historicalEvents;
  readonly List<Fact> _facts = [];
  public IReadOnlyList<Fact> Facts => _facts;
  public RulerInfo Ruler { get; init; } = ruler;
  public VillainType Villain { get; set; }
  public string VillainName { get; set; } = "";

  public void Add(Fact fact)
  {
    if (fact is Nation nation)
      _nations.Add(nation);
    else if (fact is Invasion || fact is Disaster)
      _historicalEvents.Add(fact);
    else
      _facts.Add(fact);
  }

  public void ClearFact(Fact fact) => _facts.Remove(fact);

  public Fact? FactCheck(string name)
  {
    foreach (var fact in _facts)
    {
      if (fact is SimpleFact sf && sf.Name == name)
        return fact;
      else if (fact is LocationFact lf && lf.Desc == name)
        return fact;
    }

    return null;
  }
}

class Fact
{
  public static Fact FromStr(string txt)
  {
    var pieces = txt.Split('#');

    return pieces[0] switch
    {
      "LocationFact" => new LocationFact()
      {
        Loc = Loc.FromStr(pieces[1]),
        Desc = pieces[2]
      },
      "HistoricalFigure" => new HistoricalFigure(pieces[1])
      {
        Title = pieces[2]
      },
      "RulerInfo" => new RulerInfo()
      {
        Name = pieces[1],
        Title = pieces[2],
        Epithet = pieces[3],
        Beloved = pieces[4] == "true"
      },
      "Nation" => new Nation()
      {
        Name = pieces[1],
        Desc = pieces[2]
      },
      "Invasion" => new Invasion()
      {
        Invader = pieces[1],
        Type = (InvaderType)Enum.Parse(typeof(InvaderType), pieces[2]),
        Successful = pieces[3] == "true"
      },
      "Disaster" => new Disaster()
      {
        Desc = pieces[1],
        Type = (DisasterType)Enum.Parse(typeof(DisasterType), pieces[2])
      },
      _ => new SimpleFact()
      {
        Name = pieces[1],
        Value = pieces[2]
      },
    };
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

class History(Rng rng)
{
  readonly NameGenerator _nameGen = new(rng, Util.NamesFile);

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

  string CometDesc(Rng rng)
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

  public static Nation GenNation(Rng rng)
  {
    NameGenerator ng = new(rng, ResourcePath.GetDataFilePath("countries.txt"));
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

  Fact GenDisaster(Rng rng)
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

  Fact GenInvasion(FactDb factDb, Rng rng)
  {
    InvaderType type;
    string invader;
    double roll = rng.NextDouble();
    if (roll < 0.5)
    {
      if (factDb.Nations.Count == 0)
        factDb.Add(GenNation(rng));

      type = InvaderType.Nation;
      invader = factDb.Nations[rng.Next(factDb.Nations.Count)].FullName;
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

  public FactDb GenerateHistory(Rng rng)
  {
    NameGenerator nameGen = new(rng, Util.NamesFile);

    var type = rng.Next(2) switch
    {
      0 => OGRulerType.ElfLord,
      _ => OGRulerType.DwarfLord,
    };
    var name = nameGen.GenerateName(rng.Next(5, 10)).Capitalize();
    RulerInfo ruler = new()
    {
      Type = type,
      Name = name,
      Title = nameGen.PickTitle(),
      Epithet = nameGen.PickEpithet(),
      Beloved = rng.NextDouble() < 0.5
    };

    FactDb factDb = new(ruler);

    factDb.Villain = rng.NextDouble() < 0.5 ? VillainType.FieryDemon : VillainType.Necromancer;
    factDb.VillainName = nameGen.GenerateName(rng.Next(8, 13));

    factDb.Add(GenNation(rng));
    factDb.Add(GenNation(rng));
    factDb.Add(GenNation(rng));

    factDb.Add(GenDisaster(rng));
    factDb.Add(GenInvasion(factDb, rng));

    return factDb;
  }

  public static string MagicWord(Rng rng) => rng.Next(5) switch
  {
    0 => "ZELGO MER",
    1 => "DAIYEN FOOELS",
    2 => "ELBIB YLOH",
    3 => "ELAM EBOW",
    _ => "VELOX NEB"
  };

  public static Item SealingTablet1(GameObjectDB objDb)
  {
    string txt = @"
    Part of a broken tablet:
      ┍────────────────┑
      ┇                │
      │ ՓլՈ ԿՈՄ թԺթԺ   |
      ┇  ՄՓ լՓ լՓպՈ    │
      | պՈՄԺ ՍեթՓպ     |
      │  պՓ լԺwԺ ե     ┇
      ┇                │
      \\/\\/\__/\/\__/\/
      ";

    Item tablet = SealingTablet(objDb, txt);
    tablet.Traits.Add(new QuestItem1());
    tablet.Traits.Add(new OnPickupTrait()
    {
      Clear = true,
      Event = "SetAttributeTrigger#MainQuestState#2#0"
    });

    return tablet;
  }

  public static Item SealingTablet2(GameObjectDB objDb)
  {
    string txt = @"
    Part of a broken tablet:
      //\\/\//\\_/\/\/\
      ┇                │
      │   ձՈՄՍԽտԺ      ┇
      ┇   ՓԿե լՓ       |
      |     պԺԿԺլԺ     │ 
      │                │
      ┕────────────────┙
        ";

    Item tablet = SealingTablet(objDb, txt);
    tablet.Traits.Add(new QuestItem2());

    return tablet;
  }

  static Item SealingTablet(GameObjectDB objDb, string txt)
  {
    Item tablet = new()
    {
      Name = "broken tablet",
      Type = ItemType.Document,
      Glyph = new Glyph('▫', Colours.GREY, Colours.DARK_GREY, Colours.BLACK, false)
    };

    ReadableTrait rt = new(txt) { OwnerID = tablet.ID };
    tablet.Traits.Add(rt);
    tablet.Traits.Add(new StoneTabletTrait(txt) { OwnerID = tablet.ID });

    objDb.Add(tablet);

    return tablet;
  }
  
  public static Item SorceressTome(GameObjectDB objDb)
  {
    Item tome = new()
    {
      Name = "Sorceress' Tome",
      Type = ItemType.Document,
      Glyph = new Glyph('♪', Colours.BRIGHT_RED, Colours.DULL_RED, Colours.BLACK, false)
    };

    ReadableTrait rt = new("lorem ipsum") { OwnerID = tome.ID };
    tome.Traits.Add(rt);
    DescriptionTrait dt = new("A leather-bound tome with an arcane symbol emblazoned on the cover.");
    tome.Traits.Add(dt);

    objDb.Add(tome);

    return tome;
  }
}