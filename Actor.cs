
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

// I think not ever monster will have an inventory, I've split out
// an interface to hold the functions the ones who do will need
interface IItemHolder
{
    public Inventory Inventory { get; set; } 

    void CalcEquipmentModifiers();
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
    Village
}

record Feature(string Name, Attribute Attribute, int Mod, ulong expiry);

// Actor should really be an abstract class but abstract classes seemed
// to be problematic when I was trying to use the JSON serialization
// libraries
class Actor : GameObj
{
    public Dictionary<Attribute, Stat> Stats { get; set; } = [];
    public List<Feature> Features { get; set; } = [];
    public ActorStatus Status { get; set; }
    
    protected IBehaviour _behaviour;

    public Actor() { }

    public override string FullName => Name.DefArticle();    
    public virtual int TotalMeleeAttackModifier() => 0;
    public virtual int AC => 10;
    public virtual List<Damage> MeleeDamage() => [];
    public virtual void HearNoise(ulong sourceID, int sourceRow, int sourceColumn, GameState gs) { }
    public bool Hostile => !(Status == ActorStatus.Indifferent || Status == ActorStatus.Friendly);

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
}

// Covers pretty much any actor that isn't the player. Villagers
// for instance are of type Monster even though it's a bit rude
// to call them that. Dunno why I don't like the term NPC for
// this class
class Monster : Actor, IPerformer
{
    public AIType AIType { get; set;}
    public double Energy { get; set; } = 0.0;
    public double Recovery { get; set; }
    public bool RemoveFromQueue { get; set; }

    public Monster() 
    {
        _behaviour = new BasicMonsterBehaviour();
    }

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

    public override int AC 
    {
        get 
        {
            if (Stats.TryGetValue(Attribute.AC, out Stat ac))
                return ac.Curr;
            else
                return base.AC;
        }
    }
    
    public Action TakeTurn(UserInterface ui, GameState gameState)
    {
        return _behaviour.CalcAction(this, gameState, ui, ui.Rng);
    }

    public void SetBehaviour(AIType aiType)
    {
        IBehaviour behaviour = aiType switch 
        {
            AIType.Village => new VillagerBehaviour(),
            AIType.BasicHumanoid => new BasicHumanoidBehaviour(),            
            _ => new BasicMonsterBehaviour(),
        };

        _behaviour = behaviour;
        AIType = aiType;
    }
}

// I don't know if I'll actually need this? Maybe the 'type' of villager
// can just be determined by what behaviour is assigned to them?
enum VillagerType { Peasant, Priest }
class Villager : Actor, IPerformer
{
    public double Energy { get; set; } = 0.0;
    public double Recovery { get; set; } = 1.0;
    public bool RemoveFromQueue { get; set; }
    public VillagerType VillagerType { get; set; }
    public string Appearance { get; set; } = "";

    public override string FullName => Name.Capitalize();  

    public Villager() => Glyph = new Glyph('@', Colours.YELLOW, Colours.YELLOW_ORANGE);

    public Action TakeTurn(UserInterface ui, GameState gameState)
    {
        return _behaviour.CalcAction(this, gameState, ui, ui.Rng);        
    }
}

interface IBehaviour 
{ 
    Action CalcAction(Actor actor, GameState gameState, UserInterface ui, Random rng);
}

class PriestBehaviour : IBehaviour
{
    DateTime _lastIntonation = new DateTime(1900, 1, 1);

    public Action CalcAction(Actor actor, GameState gameState, UserInterface ui, Random rng)
    {
        if ((DateTime.Now - _lastIntonation).TotalSeconds > 10)
        {
            var bark = new BarkAnimation(ui, 2500, actor, "Praise be to Huntokar!");
            ui.RegisterAnimation(bark); 
            _lastIntonation = DateTime.Now;

            return new PassAction((IPerformer)actor);
        }
        else
        {
            return new PassAction((IPerformer)actor);
        }
    }
}

// Very basic idea for a wolf or such, which can move and attack the player
// but doesn't have hands/can't open doors etc
class BasicMonsterBehaviour : IBehaviour
{
    public Action CalcAction(Actor actor, GameState gs, UserInterface ui, Random rng)
    {
        if (actor.Status == ActorStatus.Idle) 
        {
            return new PassAction((IPerformer)actor);
        }

        // Basic monster behaviour:
        //   1) if adj to player, attack
        //   2) otherwise move toward them
        //   3) Pass I guess

        // Fight!
        if (Util.Distance(actor.Loc, gs.Player.Loc) <= 1)
        {
            return new MeleeAttackAction(actor, gs.Player.Loc, gs, rng);
        }
       
        // Move!
        var adj = gs.DMap.Neighbours(actor.Loc.Row, actor.Loc.Col);
        foreach (var sq in adj)
        {
            var loc = new Loc(actor.Loc.DungeonID, actor.Loc.Level, sq.Item1, sq.Item2);
            if (!gs.ObjDB.Occupied(loc))
            {
                // the square is free so move there!
                return new MoveAction(actor, loc, gs, rng);
            }
        }
        
        // Otherwise do nothing!
        return new PassAction((IPerformer)actor);
    }
}

// Basic goblins and such. These guys know how to open doors
class BasicHumanoidBehaviour : IBehaviour
{
    public Action CalcAction(Actor actor, GameState gs, UserInterface ui, Random rng)
    {
        if (actor.Status == ActorStatus.Idle) 
        {            
            return new PassAction((IPerformer)actor);
        }
        
        // Fight!
        if (Util.Distance(actor.Loc, gs.Player.Loc) <= 1)
        {
            return new MeleeAttackAction(actor, gs.Player.Loc, gs, rng);
        }

        // Move!
        var adj = gs.DMapDoors.Neighbours(actor.Loc.Row, actor.Loc.Col);
        foreach (var sq in adj)
        {
            var loc = new Loc(actor.Loc.DungeonID, actor.Loc.Level, sq.Item1, sq.Item2);

            if (gs.CurrentMap.TileAt(loc.Row, loc.Col).Type == TileType.ClosedDoor)
            {
                return new OpenDoorAction(actor, gs.CurrentMap, loc, gs);
            }
            else if (!gs.ObjDB.Occupied(loc))
            {
                // the square is free so move there!
                return new MoveAction(actor, loc, gs, rng);
            }
        }

        // Otherwise do nothing!
        return new PassAction((IPerformer)actor);
    }
}

class VillagerBehaviour : IBehaviour
{
    public Action CalcAction(Actor actor, GameState gameState, UserInterface ui, Random rng)
    {
        return new PassAction((IPerformer)actor);
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

    public static Actor Get(string name, Random rng)
    {
        if (_catalog.Count == 0)
            LoadCatalog();

        if (!_catalog.TryGetValue(name, out string? template))
            throw new Exception($"{name}s don't seem to exist in this world!");

        var fields = template.Split('|').Select(f => f.Trim()).ToArray();

        var glyph = new Glyph(fields[0][0], ColourSave.TextToColour(fields[1]),
                                            ColourSave.TextToColour(fields[2]));
        Enum.TryParse(fields[11], out AIType ai);
        var m = new Monster()
        {
            Name = name,
            Glyph = glyph,
            AIType = ai,
            Recovery = double.Parse(fields[6])
        };
        m.SetBehaviour(ai);

        int hp = int.Parse(fields[4]);
        m.Stats.Add(Attribute.HP, new Stat(hp));
        int attBonus = int.Parse(fields[5]);
        m.Stats.Add(Attribute.MeleeAttackBonus, new Stat(attBonus));
        int ac = int.Parse(fields[3]);
        m.Stats.Add(Attribute.AC, new Stat(ac));
        int str = Util.StatRollToMod(int.Parse(fields[9]));
        m.Stats.Add(Attribute.Strength, new Stat(str));
        int dmgDie = int.Parse(fields[7]);
        m.Stats.Add(Attribute.DmgDie, new Stat(dmgDie));
        int dmgRolls = int.Parse(fields[8]);
        m.Stats.Add(Attribute.DmgRolls, new Stat(dmgRolls));
        int xpValue = int.Parse(fields[10]);
        m.Stats.Add(Attribute.XPValue, new Stat(xpValue));
        m.Status = rng.NextDouble() < 0.9 ? ActorStatus.Active : ActorStatus.Idle;
        
        return m;
    }
}