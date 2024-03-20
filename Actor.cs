
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

// Interface for anything that will get a turn in the game. The Player,
// NPCs/Monsters, even things like torches that are burning or a cursed
// item that zaps the player once in a while
interface IPerformer
{
    double Energy { get; set; }
    double Recovery { get; set; }
    bool RemoveFromQueue { get; set; }
    Action TakeTurn(UserInterface ui, GameState gameState);
}

// I wonder if a simple status will be enough
enum ActorStatus 
{ 
    Idle, 
    Active,
    Indifferent,
    Friendly
}

enum AIType 
{
    Basic,
    BasicHumanoid,
    BasicFlyer,
    Archer,
    Spellcaster,
    KoboldTrickster,
    Villager
}

// Actor should really be an abstract class but abstract classes seemed
// to be problematic when I was trying to use the JSON serialization
// libraries
class Actor : GameObj, IPerformer, IZLevel
{
    static readonly int FLYING_Z = 10;
    static readonly int DEFAULT_Z = 4;
    
    public Dictionary<Attribute, Stat> Stats { get; set; } = [];
    public List<Trait> Traits { get; set; } = [];

    public ActorStatus Status { get; set; }
    public Inventory Inventory { get; set; }

    public double Energy { get; set; } = 0.0;
    public double Recovery { get; set; } = 1.0;
    public bool RemoveFromQueue { get; set; }
    public string Appearance { get; set; } = "";

    protected IBehaviour _behaviour;
    
    public Actor() 
    {
        Inventory = new Inventory(ID);
        _behaviour = new BasicMonsterBehaviour(new DumbMoveStrategy());
    }

    public bool HasActiveTrait<T>() => Traits.Where(t => t.Active)
                                       .OfType<T>().Any();
    public bool HasTrait<T>() => Traits.OfType<T>().Any();
    public override int Z() => HasActiveTrait<FlyingTrait>() ? FLYING_Z : DEFAULT_Z;
    
    public override string FullName => Name.DefArticle();    
    public virtual int TotalMeleeAttackModifier() => 0;
    public virtual int TotalMissileAttackModifier(Item weapon) => 0;
    public virtual int AC => 10;
    public virtual List<Damage> MeleeDamage() => [];
    public virtual void HearNoise(ulong sourceID, int sourceRow, int sourceColumn, GameState gs) { }
    public virtual void CalcEquipmentModifiers() { }
    public bool Hostile => !(Status == ActorStatus.Indifferent || Status == ActorStatus.Friendly);

    public virtual string ChatText() => "";
    public virtual (Action, InputAccumulator) Chat(GameState gs) => (null, null);

    public virtual int ReceiveDmg(IEnumerable<(int, DamageType)> damage, int bonusDamage)
    {
        if (Status == ActorStatus.Idle)
            Status = ActorStatus.Active;

        // Really, in the future, we'll need to check each damage type to see
        // if the Monster is resistant or immune to a particular type.
        int total = damage.Select(d => d.Item1).Sum() + bonusDamage;
        if (total < 0)
            total = 0;
        Stats[Attribute.HP].Curr -= total;

        return Stats[Attribute.HP].Curr;
    }

    public virtual void SetBehaviour(IBehaviour behaviour) => _behaviour = behaviour;

    public virtual Action TakeTurn(UserInterface ui, GameState gameState)
    {
        return _behaviour.CalcAction(this, gameState, ui, ui.Rng);
    }
}

// Covers pretty much any actor that isn't the player. Villagers
// for instance are of type Monster even though it's a bit rude
// to call them that. Dunno why I don't like the term NPC for
// this class
class Monster : Actor
{
    public AIType AIType { get; set;}

    public Monster() { }

    public override List<Damage> MeleeDamage()
    {
        int die = 4;
        if (Stats.TryGetValue(Attribute.DmgDie, out var dmgDie))
            die = dmgDie.Curr;
        int numOfDie = 1;
        if (Stats.TryGetValue(Attribute.DmgRolls, out var rolls))
            numOfDie = rolls.Curr;

        // Blunt for now. I need to add damage type to monster definition file
        // AND handle cases of multiple damage types. Ie., hellhounds and such
        return [ new Damage(die, numOfDie, DamageType.Blunt) ];
    }

    public override void HearNoise(ulong sourceID, int sourceRow, int sourceColumn, GameState gs) 
    {
        if (sourceID == gs.Player.ID && Status == ActorStatus.Idle)
        {
            Status = ActorStatus.Active;
        }
    }

    public override int TotalMeleeAttackModifier() 
    {
        if (Stats.TryGetValue(Attribute.MonsterAttackBonus, out var ab))
            return ab.Curr;
        else
            return 0;
    }

    public override int TotalMissileAttackModifier(Item weapon)
    {
        if (Stats.TryGetValue(Attribute.MonsterAttackBonus, out var ab))
            return ab.Curr;
        else
            return 0;
    }

    public override int AC 
    {
        get 
        {
            if (Stats.TryGetValue(Attribute.AC, out var ac))
                return ac.Curr;
            else
                return base.AC;
        }
    }
}

class Villager : Actor
{    
    public Town Town { get; set; }
    public double Markup { get; set; } // for villagers who sell stuff...

    public override string FullName => Name.Capitalize();  
    public override string ChatText() => ((IChatter)_behaviour).ChatText(this);
    public override (Action, InputAccumulator) Chat(GameState gs) => ((IChatter)_behaviour).Chat(this, gs);
}

class VillageAnimal : Actor
{
    public Town Town { get; set; }
    
    public VillageAnimal()
    {
        _behaviour = new VillagePupBehaviour();
    }
    
    public override string ChatText() => ((IChatter)_behaviour).ChatText(this);
    public override (Action, InputAccumulator) Chat(GameState gs) => ((IChatter)_behaviour).Chat(this, gs);
}

class MonsterFactory
{
    static Dictionary<string, string> _catalog = [];

    static void LoadCatalog()
    {
        foreach (var line in File.ReadAllLines("monsters.txt"))
        {
            int i = line.IndexOf('|');
            string name = line[..i].Trim();
            string val = line[(i + 1)..];
            _catalog.Add(name, val);
        }
    }

    static IBehaviour AITypeToBehaviour(AIType ai) => ai switch
    {
        AIType.BasicHumanoid => new BasicMonsterBehaviour(new DoorOpeningMoveStrategy()),
        AIType.BasicFlyer => new BasicMonsterBehaviour(new SimpleFlightMoveStrategy()),
        AIType.Archer => new ArcherBehaviour(),
        AIType.KoboldTrickster => new KoboldTricksterBehaviour(),
        _ => new BasicMonsterBehaviour(new DumbMoveStrategy()),
    };

    public static Actor Get(string name, Random rng)
    {
        if (_catalog.Count == 0)
            LoadCatalog();

        if (!_catalog.TryGetValue(name, out string? template))
            throw new Exception($"{name}s don't seem to exist in this world!");

        var fields = template.Split('|').Select(f => f.Trim()).ToArray();

        var glyph = new Glyph(fields[0][0], ColourSave.TextToColour(fields[1]),
                                            ColourSave.TextToColour(fields[2]));
        
        Enum.TryParse(fields[12], out AIType ai);
        var m = new Monster()
        {
            Name = name,
            Glyph = glyph,
            AIType = ai,
            Recovery = double.Parse(fields[6])
        };

        if (!string.IsNullOrEmpty(fields[13]))
        {
            foreach (var traitTxt in fields[13].Split(','))
            {
                var trait = TraitFactory.FromText(traitTxt);
                m.Traits.Add(trait);
                //if (Enum.TryParse(feature, out Attribute attr))
                //    m.Features.Add(new Feature(feature, attr, 0, ulong.MaxValue));
            }
        }

        m.SetBehaviour(AITypeToBehaviour(ai));

        int hp = int.Parse(fields[4]);
        m.Stats.Add(Attribute.HP, new Stat(hp));
        int attBonus = int.Parse(fields[5]);
        m.Stats.Add(Attribute.MonsterAttackBonus, new Stat(attBonus));
        int ac = int.Parse(fields[3]);
        m.Stats.Add(Attribute.AC, new Stat(ac));        
        int dmgDie = int.Parse(fields[7]);
        m.Stats.Add(Attribute.DmgDie, new Stat(dmgDie));
        int dmgRolls = int.Parse(fields[8]);
        m.Stats.Add(Attribute.DmgRolls, new Stat(dmgRolls));
        int str = Util.StatRollToMod(int.Parse(fields[9]));
        m.Stats.Add(Attribute.Strength, new Stat(str));
        int dex = Util.StatRollToMod(int.Parse(fields[10]));
        m.Stats.Add(Attribute.Dexterity, new Stat(dex));
        int xpValue = int.Parse(fields[11]);
        m.Stats.Add(Attribute.XPValue, new Stat(xpValue));
        m.Status = rng.NextDouble() < 0.9 ? ActorStatus.Active : ActorStatus.Idle;
        
        return m;
    }
}