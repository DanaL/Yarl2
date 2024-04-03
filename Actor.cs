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

using System.ComponentModel.Design;

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

// Actor should really be an abstract class but abstract classes seemed
// to be problematic when I was trying to use the JSON serialization
// libraries
abstract class Actor : GameObj, IPerformer, IZLevel
{
    static readonly int FLYING_Z = 10;
    static readonly int DEFAULT_Z = 4;
    
    public Dictionary<Attribute, Stat> Stats { get; set; } = [];
    
    public ActorStatus Status { get; set; }
    public Inventory Inventory { get; set; }

    public double Energy { get; set; } = 0.0;
    public double Recovery { get; set; } = 1.0;
    public bool RemoveFromQueue { get; set; }
    public string Appearance { get; set; } = "";

    protected IBehaviour _behaviour;
    public IBehaviour Behaviour => _behaviour;

    public override int Z() => HasActiveTrait<FlyingTrait>() ? FLYING_Z : DEFAULT_Z;

    public override string FullName
    {
        get
        {         
            // This is dumb, but I haven't decided the best way to differentiate 
            // between monsters and villages, because their full names are calculated
            // differently. (This will also be a problem if I want to want to have 
            // boss monsters with proper names. I wouldn't want the game to refer to
            // someone as like 'The Saruman the White', etc)
            // Maybe I can stick a calc fullname function in IBehaviour and override it
            // for those who need it?
            if (_behaviour is PriestBehaviour)
                return Name.Capitalize();
            else if (_behaviour is GrocerBehaviour)
                return Name.Capitalize();
            else if (_behaviour is SmithBehaviour)
                return Name.Capitalize();
            
            return Name.DefArticle();
        }
    }

    public virtual int TotalMeleeAttackModifier() => 0;
    public virtual int TotalMissileAttackModifier(Item weapon) => 0;
    public virtual int AC => 10;
    public virtual List<Damage> MeleeDamage() => [];
    public virtual void HearNoise(ulong sourceID, int sourceRow, int sourceColumn, GameState gs) { }
    public virtual void CalcEquipmentModifiers() { }
    public bool Hostile => !(Status == ActorStatus.Indifferent || Status == ActorStatus.Friendly);
        
    public Actor()
    {
        Inventory = new EmptyInventory(ID);
    }

    public virtual int ReceiveDmg(IEnumerable<(int, DamageType)> damage, int bonusDamage, GameState gs)
    {
        if (Status == ActorStatus.Idle)
            Status = ActorStatus.Active;

        // Really, in the future, we'll need to check each damage type to see
        // if the Monster is resistant or immune to a particular type.
        int total = damage.Select(d => d.Item1).Sum() + bonusDamage;
        if (total < 0)
            total = 0;
        Stats[Attribute.HP].Curr -= total;

        if (HasTrait<DividerTrait>() && Stats[Attribute.HP].Curr > 2)
        {
            foreach (var dmg in damage)
            {
                switch (dmg.Item2)
                {
                    case DamageType.Piercing:
                    case DamageType.Slashing:
                    case DamageType.Blunt:
                        Divide(gs);
                        goto done_dividing;
                    default:
                        continue;
                }
            }            
        }
done_dividing:

        return Stats[Attribute.HP].Curr;
    }

    // Candidate spots will be spots adjacent to the contiguous group of the 
    // same monster. (I'm just basing this on name since I don't really have
    // a monster-type field) Yet another floodfill of sorts...
    void Divide(GameState gs)
    {
        var map = gs.Campaign.Dungeons[Loc.DungeonID].LevelMaps[Loc.Level];
        List<Loc> candidateSqs = [];
        Queue<Loc> q = [];
        q.Enqueue(Loc);
        HashSet<Loc> visited = [];

        while (q.Count > 0)
        {
            var curr = q.Dequeue();

            if (visited.Contains(curr))
                continue;

            visited.Add(curr);

            foreach (var adj in Util.Adj8Locs(curr))
            {                
                var tile = map.TileAt(adj.Row, adj.Col);
                var occ = gs.ObjDb.Occupant(adj);
                if (occ is null && tile.Passable())
                {
                    candidateSqs.Add(adj);
                }
                if (!visited.Contains(adj) && occ is not null && occ.Name == Name && occ is not Player)
                {
                    q.Enqueue(adj);
                }
            }
        }

        if (candidateSqs.Count > 0)
        {
            var spot = candidateSqs[gs.Rng.Next(candidateSqs.Count)];
            var other = MonsterFactory.Get(Name, gs.Rng);
            var hp = Stats[Attribute.HP].Curr / 2;
            var half = Stats[Attribute.HP].Curr - hp;
            Stats[Attribute.HP].Curr = hp;
            other.Stats[Attribute.HP].SetMax(half);

            var msg = new Message($"{Name.DefArticle()} divides into two!!", Loc, false);
            gs.UIRef().AlertPlayer([msg], "", gs);

            gs.ObjDb.AddNewActor(other, spot);
            gs.AddPerformer(other);
        }
    }

    public virtual void SetBehaviour(IBehaviour behaviour) => _behaviour = behaviour;

    public abstract Action TakeTurn(UserInterface ui, GameState gameState);

    public bool AbilityCheck(Attribute attr, int dc, Random rng)
    {
        int roll = rng.Next(20) + 1 + Stats[attr].Curr;
        return roll >= dc;
    }
}

class Mob : Actor
{
    public IMoveStrategy MoveStrategy { get; set; }
    public List<ActionTrait> Actions { get; set; } = [];

    public Mob()
    {
        _behaviour = new MonsterBehaviour();
        MoveStrategy = new DumbMoveStrategy();
    }
    
    public Damage? Dmg { get; set; }
    public override List<Damage> MeleeDamage()
    {
        List<Damage> dmgs = [ Dmg ?? new Damage(4, 1, DamageType.Blunt)];

        return dmgs;
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
        return Stats.TryGetValue(Attribute.MonsterAttackBonus, out var ab) ? ab.Curr : 0;
    }

    public override int TotalMissileAttackModifier(Item weapon)
    {
        return Stats.TryGetValue(Attribute.MonsterAttackBonus, out var ab) ? ab.Curr : 0;
    }

    public override int AC => Stats.TryGetValue(Attribute.AC, out var ac) ? ac.Curr : base.AC;

    public override Action TakeTurn(UserInterface ui, GameState gameState)
    {
        return _behaviour.CalcAction(this, gameState, ui);
    }
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

    static IMoveStrategy TextToMove(string txt) => txt.ToLower() switch
    {
        "door" => new DoorOpeningMoveStrategy(),
        "flying" => new SimpleFlightMoveStrategy(),
        "wall" => new WallMoveStrategy(),
        _ => new DumbMoveStrategy()
    };

    //       0       1    2      3   4   5           6         7    8    9       10        11       12
    // name, symbol, lit, unlit, AC, HP, Attack Mod, Recovery, Str, Dex, Xp val, Movement, Actions, Other Traits 
    // skeleton        |z|white        |darkgrey  |12| 8|2| 1.0| 6|1|12|10|2|Basic|
    public static Actor Get(string name, Random rng)
    {
        if (_catalog.Count == 0)
            LoadCatalog();

        if (!_catalog.TryGetValue(name, out string? template))
            throw new Exception($"{name}s don't seem to exist in this world!");

        var fields = template.Split('|').Select(f => f.Trim()).ToArray();

        var glyph = new Glyph(fields[0][0], Colours.TextToColour(fields[1]),
                                            Colours.TextToColour(fields[2]), Colours.BLACK);
        
        var mv = TextToMove(fields[10]);
        var m = new Mob()
        {
            Name = name,
            Glyph = glyph,
            Recovery = double.Parse(fields[6]),
            MoveStrategy = mv
        };

        int hp = int.Parse(fields[4]);
        m.Stats.Add(Attribute.HP, new Stat(hp));
        int attBonus = int.Parse(fields[5]);
        m.Stats.Add(Attribute.MonsterAttackBonus, new Stat(attBonus));
        int ac = int.Parse(fields[3]);
        m.Stats.Add(Attribute.AC, new Stat(ac));        
        int str = Util.StatRollToMod(int.Parse(fields[7]));
        m.Stats.Add(Attribute.Strength, new Stat(str));
        int dex = Util.StatRollToMod(int.Parse(fields[8]));
        m.Stats.Add(Attribute.Dexterity, new Stat(dex));
        int xpValue = int.Parse(fields[9]);
        m.Stats.Add(Attribute.XPValue, new Stat(xpValue));
        
        if (fields[11] != "")
        {
            foreach (var actionTxt in fields[11].Split(',')) 
            {
                m.Actions.Add((ActionTrait)TraitFactory.FromText(actionTxt, m));
            }
        }

        if (!string.IsNullOrEmpty(fields[12]))
        {
            foreach (var traitTxt in fields[12].Split(','))
            {
                var trait = TraitFactory.FromText(traitTxt, m);
                m.Traits.Add(trait);                
            }
        }
        
        m.Status = rng.NextDouble() < 0.9 ? ActorStatus.Active : ActorStatus.Idle;
        
        return m;
    }
}