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

// class to accumulate a list of facts about the world as historical
// events are generated so that they can be reused.
class WorldFacts
{
    NameGenerator _nationNames;
    NameGenerator _peopleNames;
    Random _rng;
    List<string> _nations = [];
    static string[] _nationModifiers = [
        "Kingdom of", "Duchy of", "Sovereignty of", "Islands of",
        "Barony of", "North", "South", "East", "West", "Greater", "Lesser",
        "Nation of", "Province of", "Upper", "Lower" ];

    public WorldFacts(Random rng)
    {
        _rng = rng;
        _nationNames = new NameGenerator(_rng, "countries.txt");
        _peopleNames = new NameGenerator(_rng, "names.txt");
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
    public abstract List<Decoration> GenerateDecorations();

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
    private RulerInfo _rulerInfo;
    private (InvaderType, string) _invader;
    private bool _succesful;
    private WorldFacts _facts;

    public InvasionHistoricalEvent(RulerInfo rulerInfo, WorldFacts facts, Random rng) : base(rng)
    {
        _rulerInfo = rulerInfo;
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

    private string AnonymousStatue()
    {
        // Variables: successful/not successful, beloved/unloved, ruler type
        switch (_rulerInfo.Type) 
        {
            case OGRulerType.ElfLord:
                if (_succesful && _rulerInfo.Beloved)
                    return "a statue depicting a mighty elf, their sword held aloft.";
                else if (_succesful && !_rulerInfo.Beloved)
                    return "a statue depicting a glaring elf, their boot on the neck of a foe.";
                else if (!_succesful && _rulerInfo.Beloved)
                    return "a statue of an elf, staring defiantly ahead.";
                else
                    return "a statue of a cowering elf.";
            case OGRulerType.DwarfLord:
                if (_succesful && _rulerInfo.Beloved)
                    return "a statue of a fearsome dwarf, who leans on their axe.";
                else if (_succesful && !_rulerInfo.Beloved)
                    return "a statue of a dwarf, their cloak covering their face.";
                else if (!_succesful && _rulerInfo.Beloved)
                    return "a statue of a dwarf who stands protecting their people.";
                else
                    return "a statue of a dwarf, kneeling and weeping.";
        }

        throw new Exception("Hmm we don't know about this kind of statue");
    }

    private string KnownStatue()
    {
        if (_succesful)
            return $"a statue depicting {_rulerInfo.FullName}, victorious in battle.";
        else if (_rulerInfo.Beloved)
            return $"a statue depicting {_rulerInfo.FullName}, grim in face.";
        else
            return $"a statue depicting {_rulerInfo.FullName}, kneeling, their gaze to the ground.";
    }

    private string VisualDesc()
    {
        string defenders = _rulerInfo.Type switch
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

    private string FrescoDesc()
    {
        var roll = Rng.NextDouble();
        if (roll < 0.5)
            return $"A faded fresco shows {VisualDesc()}";
        else if (roll < 0.75)
            return $"On the dusty walls you can make out a scene of {VisualDesc()}";
        else
            return $"A partially destroyed fresco depicts {VisualDesc()}";
    }

    private string MosaicDesc()
    {
        var roll = Rng.NextDouble();
        if (roll < 0.5)
            return $"On broken mosaic tiles you can make out {VisualDesc()}";
        else
            return $"A faded mosaic scene of {VisualDesc()}";
    }

    private string StatueDesc() => Rng.NextDouble() < 0.75 ? AnonymousStatue() : KnownStatue();

    private string ScholarJournal1()
    {
        return $@"My dear {_facts.RulerName()}, I am here in this dank place researching the {Title}, having been lead here after discovering an old codex in a library in the {_facts.GetNation()} I will...";        
    }

    private string ScholarJounral2()
    {
        if (_succesful)
            return $"...have found a scroll extolling the virtues of {_rulerInfo.Name} and their victory over {_invader.Item2}...";
        else
            return $"...describes the lamentations of the people after the ravaging {_invader.Item2} and how...";
    }

    private string ScholarJounral3()
    {
        if (_succesful && _rulerInfo.Beloved)
            return $"...the inscription read: {_rulerInfo.Name}, victorious over {_invader.Item2} was greeted with laurels upon their return...";
        else if (_succesful && !_rulerInfo.Beloved)
            return $"...their victory seems to have cemenented their power over the people, who dwelt in fear of {_rulerInfo.Name}...";
        else
            return $"...I wish to learn what became of {_rulerInfo.FullName} after their devastating defeat in the battle of...";
    }

    // Generate a list of decorations that might be strewn throughout
    // the dungeon
    public override List<Decoration> GenerateDecorations()
    {
        var decorations = new List<Decoration>
        {
            new Decoration(DecorationType.Statue, StatueDesc()),
            new Decoration(DecorationType.Fresco, FrescoDesc()),
            new Decoration(DecorationType.Mosaic, MosaicDesc()),
            new Decoration(DecorationType.ScholarJournal, ScholarJournal1()),
            new Decoration(DecorationType.ScholarJournal, ScholarJounral2()),
            new Decoration(DecorationType.ScholarJournal, ScholarJounral3())
        };

        return decorations;
    }    
}

class RulerInfo
{
    public OGRulerType Type { get; set; }
    public string Name { get; set; }
    public string PrefixTitle { get; set; }
    public string Epithet { get; set; }
    public bool Beloved {  get; set; } // Maybe I could classify the epithets they receive?

    public string FullName => $"{PrefixTitle} {Name} {Epithet}".Trim();
}

class History 
{
    WorldFacts _facts;
    RulerInfo _ruler;
    public VillainType Villain { get; set; }

    private Random _rng;

    public History(Random rng)
    {
        _rng = rng;
    }

    public List<Decoration> GetDecorations()
    {
        var historicalEvent = new InvasionHistoricalEvent(_ruler, _facts, _rng);
        var decs = historicalEvent.GenerateDecorations();

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

        var nameGen = new NameGenerator(_rng, "names.txt");
        var name = _facts.RulerName();

        _ruler = new RulerInfo()
        {
            Type = type,
            Name = name,
            PrefixTitle = nameGen.PickTitle(),
            Epithet = nameGen.PickEpithet(),
            Beloved = _rng.NextDouble() < 0.5
        };
    }

    // This will have to be vastly expanded of course.
    public void GenerateVillain()
    {
        Villain = _rng.NextDouble() < 0.5 ? VillainType.FieryDemon : VillainType.Necromancer;
    }
}