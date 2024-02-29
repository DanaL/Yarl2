
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

enum AIType 
{
    Basic,
    BasicHumanoid,
    Village
}

// Actor should really be an abstract class but abstract classes seemed
// to be problematic when I was trying to use the JSON serialization
// libraries
class Actor : GameObj
{
    public Dictionary<Attribute, Stat> Stats { get; set; } = [];

    public Actor() { }

    public override string FullName => Name.DefArticle();    
    public virtual int TotalMeleeAttackModifier() => 0;
    public virtual int AC => 10;
    public virtual List<Damage> MeleeDamage() => [];

    public virtual int ReceiveDmg(IEnumerable<(int, DamageType)> damage, int bonusDamage)
    {
        // Really, in the future, we'll need to check each damage type to see
        // if the Monster is resistant or immune to a particular type.
        int total = damage.Select(d => d.Item1).Sum() + bonusDamage;
        if (total < 0)
            total = 0;
        Stats[Attribute.HP].Curr -= total;

        return Stats[Attribute.HP].Curr;
    }
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

    private IBehaviour _behaviour;

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
        return _behaviour.CalcAction(this, gameState, ui.Rng);
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

interface IBehaviour 
{ 
    Action CalcAction(Actor actor, GameState gameState, Random rng);
}

// Very basic idea for a wolf or such, which can move and attack the player
// but doesn't have hands/can't open doors etc
class BasicMonsterBehaviour : IBehaviour
{
    public Action CalcAction(Actor actor, GameState gameState, Random rng)
    {
        // Basic monster behaviour:
        //   1) if adj to player, attack
        //   2) otherwise move toward them
        //   3) Pass I guess

        // Fight!
        if (Util.Distance(actor.Loc, gameState.Player.Loc) <= 1)
        {
            Console.WriteLine($"{actor.FullName} would attack right now!");
            return new PassAction((IPerformer)actor);
        }
       
        // Move!
        var adj = gameState.DMap.Neighbours(actor.Loc.Row, actor.Loc.Col);
        foreach (var sq in adj)
        {
            var loc = new Loc(actor.Loc.DungeonID, actor.Loc.Level, sq.Item1, sq.Item2);
            if (!gameState.ObjDB.Occupied(loc))
            {
                // the square is free so move there!
                return new MoveAction(actor, loc, gameState, rng);
            }
        }
        
        // Otherwise do nothing!
        return new PassAction((IPerformer)actor);
    }
}

// Basic goblins and such. These guys know how to open doors
class BasicHumanoidBehaviour : IBehaviour
{
    public Action CalcAction(Actor actor, GameState gameState, Random rng)
    {
        // Fight!
        if (Util.Distance(actor.Loc, gameState.Player.Loc) <= 1)
        {
            Console.WriteLine($"{actor.FullName} would attack right now!");
            return new PassAction((IPerformer)actor);
        }

        // Move!
        var adj = gameState.DMapDoors.Neighbours(actor.Loc.Row, actor.Loc.Col);
        foreach (var sq in adj)
        {
            var loc = new Loc(actor.Loc.DungeonID, actor.Loc.Level, sq.Item1, sq.Item2);

            if (gameState.CurrentMap.TileAt(loc.Row, loc.Col).Type == TileType.ClosedDoor)
            {
                return new OpenDoorAction(actor, gameState.CurrentMap, loc, gameState);
            }
            else if (!gameState.ObjDB.Occupied(loc))
            {
                // the square is free so move there!
                return new MoveAction(actor, loc, gameState, rng);
            }
        }

        // Otherwise do nothing!
        return new PassAction((IPerformer)actor);
    }
}

class VillagerBehaviour : IBehaviour
{
    public Action CalcAction(Actor actor, GameState gameState, Random rng)
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

    public static Actor? Get(string name)
    {
        if (_catalog.Count == 0)
            LoadCatalog();

        if (!_catalog.TryGetValue(name, out string? template))
            throw new Exception($"{name}s don't seem to exist in this world!");

        var fields = template.Split('|').Select(f => f.Trim()).ToArray();

        var glyph = new Glyph(fields[0][0], ColourSave.TextToColour(fields[1]),
                                            ColourSave.TextToColour(fields[2]));
        Enum.TryParse(fields[10], out AIType ai);
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
        int str = int.Parse(fields[9]);
        m.Stats.Add(Attribute.Strength, new Stat(str));
        int dmgDie = int.Parse(fields[7]);
        m.Stats.Add(Attribute.DmgDie, new Stat(dmgDie));
        int dmgRolls = int.Parse(fields[8]);
        m.Stats.Add(Attribute.DmgRolls, new Stat(dmgRolls));

        return m;
    }
}