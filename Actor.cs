
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
    Village
}

record Feature(string Name, Attribute Attribute, int Mod, ulong expiry);

// Actor should really be an abstract class but abstract classes seemed
// to be problematic when I was trying to use the JSON serialization
// libraries
class Actor : GameObj, IPerformer
{
    public Dictionary<Attribute, Stat> Stats { get; set; } = [];
    public List<Feature> Features { get; set; } = [];
    public ActorStatus Status { get; set; }
    public Inventory Inventory { get; set; }

    public double Energy { get; set; } = 0.0;
    public double Recovery { get; set; }
    public bool RemoveFromQueue { get; set; }

    protected IBehaviour _behaviour;

    public Actor() 
    {
        Inventory = new Inventory(ID);
    }

    public override string FullName => Name.DefArticle();    
    public virtual int TotalMeleeAttackModifier() => 0;
    public virtual int AC => 10;
    public virtual List<Damage> MeleeDamage() => [];
    public virtual void HearNoise(ulong sourceID, int sourceRow, int sourceColumn, GameState gs) { }
    public virtual void CalcEquipmentModifiers() { }
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

interface IChatter
{
    string ChatText(Villager villager);
    (Action, InputAccumulator) Chat(Villager villager, GameState gs);
}

class Villager : Actor
{
    public string Appearance { get; set; } = "";
    public Town Town { get; set; }
    public double Markup { get; set; } // for villagers who sell stuff...

    public override string FullName => Name.Capitalize();  

    public Villager()
    {
        Glyph = new Glyph('@', Colours.YELLOW, Colours.YELLOW_ORANGE);
        Recovery = 1.0;
    }

    public string ChatText() => ((IChatter)_behaviour).ChatText(this);

    public (Action, InputAccumulator) Chat(GameState gs) => ((IChatter)_behaviour).Chat(this, gs);
}



interface IBehaviour 
{ 
    Action CalcAction(Actor actor, GameState gameState, UserInterface ui, Random rng);
}

class PriestBehaviour : IBehaviour, IChatter
{
    DateTime _lastIntonation = new DateTime(1900, 1, 1);

    public Action CalcAction(Actor actor, GameState gameState, UserInterface ui, Random rng)
    {
        if ((DateTime.Now - _lastIntonation).TotalSeconds > 10)
        {
            var bark = new BarkAnimation(ui, 2500, actor, "Praise be to Huntokar!");
            ui.RegisterAnimation(bark); 
            _lastIntonation = DateTime.Now;

            return new PassAction();
        }
        else
        {
            return new PassAction();
        }
    }

    public (Action, InputAccumulator) Chat(Villager priest, GameState gs)
    {
        var sb = new StringBuilder(priest.Appearance.IndefArticle().Capitalize());
        sb.Append(".\n\n");
        sb.Append(priest.ChatText());
        sb.Append("\n\n");

        gs.UI.Popup(sb.ToString(), priest.FullName);
        return (new PassAction(), new PauseForMoreAccumulator());
    }

    public string ChatText(Villager priest)
    {
        var sb = new StringBuilder();
        sb.Append("\"It is my duty to look after the spiritual well-being of the village of ");
        sb.Append(priest.Town.Name);
        sb.Append(".\"");

        return sb.ToString();
    }
}

class SmithBehaviour : IBehaviour, IChatter
{
    DateTime _lastBark = new(1900, 1, 1);
        
    static string PickBark(Actor smith, Random rng)
    {        
        var items = smith.Inventory.UsedSlots()
                         .Select(s => smith.Inventory.ItemAt(s)).ToList();
        Item? item;
        if (items.Count > 0)
            item = items[rng.Next(items.Count)];
        else
            item = null;

        int roll = rng.Next(2);
        if (roll == 0 && item is not null) 
        {
            if (item.Type == ItemType.Weapon)
            {
                if (item.Traits.Any(t => t is DamageTrait trait && trait.DamageType == DamageType.Blunt))
                    return $"A stout {item.Name} will serve you well!";
                else
                    return $"A sharp {item.Name} will serve you well!";
            }                
            else if (item.Name == "helmet" || item.Name == "shield")
                return $"A sturdy {item.Name} will serve you well!";
            else
                return $"Some sturdy {item.Name} will serve you well!";            
        }
        else 
        {
            return "More work...";
        }
    }

    public Action CalcAction(Actor smith, GameState gameState, UserInterface ui, Random rng)
    {
        if ((DateTime.Now - _lastBark).TotalSeconds > 10)
        {
            var bark = new BarkAnimation(ui, 2500, smith, PickBark(smith, rng));
            ui.RegisterAnimation(bark);
            _lastBark = DateTime.Now;

            return new PassAction();
        }
        else
        {
            return new PassAction();
        }
    }

    public string ChatText(Villager smith)
    {
        var sb = new StringBuilder();
        sb.Append('"');
        if (smith.Markup > 1.75)
            sb.Append("If you're looking for arms or armour, I'm the only game in town!");
        else
            sb.Append("You'll want some weapons or better armour before venturing futher!");
        sb.Append('"');

        return sb.ToString();
    }

    public (Action, InputAccumulator) Chat(Villager smith, GameState gs)
    {
        var acc = new ShopMenuAccumulator(smith, gs.UI);
        var action = new ShopAction(smith, gs);

        return (action, acc);
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
            return new PassAction();
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
        return new PassAction();
    }
}

// Basic goblins and such. These guys know how to open doors
class BasicHumanoidBehaviour : IBehaviour
{
    public Action CalcAction(Actor actor, GameState gs, UserInterface ui, Random rng)
    {
        if (actor.Status == ActorStatus.Idle) 
        {            
            return new PassAction();
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
        return new PassAction();
    }
}

class VillagerBehaviour : IBehaviour
{
    public Action CalcAction(Actor actor, GameState gameState, UserInterface ui, Random rng)
    {
        return new PassAction();
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