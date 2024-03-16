
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
    Villager
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
    public double Recovery { get; set; } = 1.0;
    public bool RemoveFromQueue { get; set; }
    public string Appearance { get; set; } = "";

    protected IBehaviour _behaviour;
    
    public Actor() 
    {
        Inventory = new Inventory(ID);
        _behaviour = new BasicMonsterBehaviour();
    }

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

    public bool HasFeature(Attribute attr) => Features.Any(f => f.Attribute == attr);
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
    
    public void SetBehaviour(AIType aiType)
    {
        IBehaviour behaviour = aiType switch 
        {
            AIType.Villager => new VillagerBehaviour(),
            AIType.BasicHumanoid => new BasicHumanoidBehaviour(),
            AIType.BasicFlyer => new BasicFlyingBehaviour(),
            AIType.Archer => new ArcherBehaviour(),
            _ => new BasicMonsterBehaviour(),
        };

        _behaviour = behaviour;
        AIType = aiType;
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

interface IChatter
{
    string ChatText(Actor actor);
    (Action, InputAccumulator) Chat(Actor actro, GameState gs);
}

interface IBehaviour 
{ 
    Action CalcAction(Actor actor, GameState gameState, UserInterface ui, Random rng);
}

class VillagePupBehaviour : IBehaviour, IChatter
{
    // Eventually different messages depending on if the dog is friendly or not?
    public string ChatText(Actor animal)
    {
        return "Arf! Arf!";
    }

    static bool LocInTown(int row, int col, Town t)
    {
        if (row < t.Row || row >= t.Row + t.Height)
            return false;
        if (col < t.Col || col >= t.Col + t.Width)
            return false;
        return true;
    }

    static bool Passable(TileType type) => type switch
    {
        TileType.Grass => true,
        TileType.Tree => true,
        TileType.Dirt => true,
        TileType.Sand => true,
        TileType.Bridge => true,
        _ => false
    };

    public Action CalcAction(Actor actor, GameState gameState, UserInterface ui, Random rng)
    {
        var animal = (VillageAnimal)actor;
        var town = animal.Town;
        
        double roll = rng.NextDouble();
        if (roll < 0.25)
            return new PassAction();

        // in the future, when they become friendly with the player they'll move toward them
        List<Loc> mvOpts = [];
        foreach (var sq in Util.Adj8Sqs(animal.Loc.Row, animal.Loc.Col))
        {
            if (LocInTown(sq.Item1, sq.Item2, town))
            {
                var loc = animal.Loc with { Row = sq.Item1, Col = sq.Item2};
                if (Passable(gameState.TileAt(loc).Type))
                    mvOpts.Add(loc);
            }
        }

        // Keep the animal tending somewhat to move toward the center of town
        var centerRow = town.Row + town.Height/2;
        var centerCol = town.Col + town.Width/2;
        var adj = animal.Loc;
        if (animal.Loc.Row < centerRow && animal.Loc.Col < centerCol)
            adj = animal.Loc with { Row = animal.Loc.Row + 1, Col = animal.Loc.Col + 1 };
        else if (animal.Loc.Row > centerRow && animal.Loc.Col > centerCol)
            adj = animal.Loc with { Row = animal.Loc.Row - 1, Col = animal.Loc.Col - 1 };
        else if (animal.Loc.Row < centerRow && animal.Loc.Col > centerCol)
            adj = animal.Loc with { Row = animal.Loc.Row + 1, Col = animal.Loc.Col - 1 };
        else if (animal.Loc.Row > centerRow && animal.Loc.Col < centerCol)
            adj = animal.Loc with { Row = animal.Loc.Row - 1, Col = animal.Loc.Col + 1 };

        if (adj != animal.Loc && Passable(gameState.TileAt(adj).Type) && !gameState.ObjDB.Occupied(adj))
        {
            mvOpts.Add(adj);
            mvOpts.Add(adj);
            mvOpts.Add(adj);
        }

        if (mvOpts.Count == 0)
            return new PassAction();
        else
            return new MoveAction(actor, mvOpts[rng.Next(mvOpts.Count)], gameState, rng);
    }

    public (Action, InputAccumulator) Chat(Actor animal, GameState gs)
    {
        var sb = new StringBuilder(animal.Appearance.IndefArticle().Capitalize());
        sb.Append(".\n\n");
        sb.Append(animal.ChatText());
        
        gs.UI.Popup(sb.ToString());
        return (new PassAction(), new PauseForMoreAccumulator());
    }
}

class PriestBehaviour : IBehaviour, IChatter
{
    DateTime _lastIntonation = new(1900, 1, 1);

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

    public (Action, InputAccumulator) Chat(Actor priest, GameState gs)
    {
        var sb = new StringBuilder(priest.Appearance.IndefArticle().Capitalize());
        sb.Append(".\n\n");
        sb.Append(priest.ChatText());
        sb.Append("\n\n");

        gs.UI.Popup(sb.ToString(), priest.FullName);
        return (new PassAction(), new PauseForMoreAccumulator());
    }

    public string ChatText(Actor priest)
    {
        var sb = new StringBuilder();
        sb.Append("\"It is my duty to look after the spiritual well-being of the village of ");
        sb.Append(((Villager) priest).Town.Name);
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
                                   .Select(smith.Inventory.ItemAt)
                                   .Select(si => si.Item1).ToList();
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

    public string ChatText(Actor smith)
    {
        var sb = new StringBuilder();
        sb.Append('"');
        double markup = ((Villager)smith).Markup;
        if (markup > 1.75)
            sb.Append("If you're looking for arms or armour, I'm the only game in town!");
        else
            sb.Append("You'll want some weapons or better armour before venturing futher!");
        sb.Append('"');

        return sb.ToString();
    }

    public (Action, InputAccumulator) Chat(Actor actor, GameState gs)
    {
        Villager smith = (Villager) actor;
        var acc = new ShopMenuAccumulator(smith, gs.UI);
        var action = new ShopAction(smith, gs);

        return (action, acc);
    }
}

class GrocerBehaviour : IBehaviour, IChatter
{
    DateTime _lastBark = new(1900, 1, 1);
        
    static string PickBark(Actor grocer, Random rng)
    {        
        int roll = rng.Next(3);
        if (roll == 0)
            return "Supplies for the prudent adventurer!";
        else if (roll == 1)
            return "Check out our specials!";
        else
            return "Store credit only.";
    }

    public Action CalcAction(Actor grocer, GameState gameState, UserInterface ui, Random rng)
    {
        if ((DateTime.Now - _lastBark).TotalSeconds > 10)
        {
            var bark = new BarkAnimation(ui, 2500, grocer, PickBark(grocer, rng));
            ui.RegisterAnimation(bark);
            _lastBark = DateTime.Now;

            return new PassAction();
        }
        else
        {
            return new PassAction();
        }
    }

    public string ChatText(Actor actor)
    {
        var grocer = (Villager)actor;
        var sb = new StringBuilder();
        sb.Append('"');
        sb.Append("Welcome to ");
        sb.Append(grocer.Town.Name.Capitalize());
        sb.Append(" market!");
        sb.Append('"');

        return sb.ToString();
    }

    public (Action, InputAccumulator) Chat(Actor actor, GameState gs)
    {
        var smith = (Villager)actor;
        var acc = new ShopMenuAccumulator(smith, gs.UI);
        var action = new ShopAction(smith, gs);

        return (action, acc);
    }
}

class BasicFlyingBehaviour : IBehaviour
{
    public Action CalcAction(Actor actor, GameState gs, UserInterface ui, Random rng)
    {
        if (actor.Status == ActorStatus.Idle)
        {
            return new PassAction();
        }

        if (Util.Distance(actor.Loc, gs.Player.Loc) <= 1)
        {
            return new MeleeAttackAction(actor, gs.Player.Loc, gs, rng);
        }

        var adj = gs.DMapFlight.Neighbours(actor.Loc.Row, actor.Loc.Col);
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

class ArcherBehaviour : IBehaviour
{
    bool ClearShot(GameState gs, IEnumerable<Loc> trajectory)
    {
        foreach (var loc in trajectory)
        {
            var tile = gs.TileAt(loc);
            if (!(tile.Passable() || tile.PassableByFlight()))
                return false;
        }

        return true;
    }

    public Action CalcAction(Actor actor, GameState gs, UserInterface ui, Random rng)
    {
        int range = 8;
        // I'm going use Radius for Range for archers for now. Will
        // there ever be a reason for a Monster to have both a 
        // Radius and Range stat? That's a worry for future Dana!
        if (actor.Stats.TryGetValue(Attribute.Radius, out Stat? r))
        {
            range = r.Curr;
        }

        if (actor.Status == ActorStatus.Idle)
        {
            return new PassAction();
        }

        var p = gs.Player;
        int distanceFromPlayer = Util.Distance(actor.Loc, p.Loc);
        if (distanceFromPlayer <= range)
        {
            var trajectory = Util.Bresenham(actor.Loc.Row, actor.Loc.Col, p.Loc.Row, p.Loc.Col)
                                 .Select(sq => actor.Loc with { Row = sq.Item1, Col = sq.Item2 })
                                 .ToList();

            if (ClearShot(gs, trajectory))
            {
                var arrow = ItemFactory.Get("arrow", gs.ObjDB);
                var arrowAnim = new ArrowAnimation(ui, trajectory, Colours.LIGHT_BROWN);
                ui.RegisterAnimation(arrowAnim);
                return new MissileAttackAction(actor, p.Loc, gs, arrow, rng);
            }
        }

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
        Enum.TryParse(fields[12], out AIType ai);
        var m = new Monster()
        {
            Name = name,
            Glyph = glyph,
            AIType = ai,
            Recovery = double.Parse(fields[6])
        };
        m.SetBehaviour(ai);

        if (!string.IsNullOrEmpty(fields[13]))
        {
            foreach (var feature in fields[13].Split(','))
            {
                if (Enum.TryParse(feature, out Attribute attr))
                    m.Features.Add(new Feature(feature, attr, 0, ulong.MaxValue));
            }
        }

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