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

interface IMoveStrategy
{
    Action MoveAction(Mob actor, GameState gs);
}

class WallMoveStrategy : IMoveStrategy
{
    public Action MoveAction(Mob actor, GameState gs) =>
        new PassAction();
}

// For creatures that don't know how to open doors
class DumbMoveStrategy : IMoveStrategy
{
    public Action MoveAction(Mob actor, GameState gs)
    {
        var adj = gs.DMap.Neighbours(actor.Loc.Row, actor.Loc.Col);
        foreach (var sq in adj)
        {
            var loc = new Loc(actor.Loc.DungeonID, actor.Loc.Level, sq.Item1, sq.Item2);
            if (!gs.ObjDb.Occupied(loc))
            {
                // the square is free so move there!
                return new MoveAction(actor, loc, gs);
            }
        }

        // If we can't find a move to do, pass
        return new PassAction();
    }
}

// I don't know what to call this class, but it's movement for creatures who
// can open doors. OpposableThumbMoveStrategy? :P
class DoorOpeningMoveStrategy : IMoveStrategy
{
    public Action MoveAction(Mob actor, GameState gs)
    {
        // Move!
        var adj = gs.DMapDoors.Neighbours(actor.Loc.Row, actor.Loc.Col);
        foreach (var sq in adj)
        {
            var loc = new Loc(actor.Loc.DungeonID, actor.Loc.Level, sq.Item1, sq.Item2);

            if (gs.CurrentMap.TileAt(loc.Row, loc.Col).Type == TileType.ClosedDoor)
            {
                return new OpenDoorAction(actor, gs.CurrentMap, loc, gs);
            }
            else if (!gs.ObjDb.Occupied(loc))
            {
                // the square is free so move there!
                return new MoveAction(actor, loc, gs);
            }
        }

        // Otherwise do nothing!
        return new PassAction();
    }
}

class SimpleFlightMoveStrategy : IMoveStrategy
{
    public Action MoveAction(Mob actor, GameState gs)
    {
        var adj = gs.DMapFlight.Neighbours(actor.Loc.Row, actor.Loc.Col);
        foreach (var sq in adj)
        {
            var loc = new Loc(actor.Loc.DungeonID, actor.Loc.Level, sq.Item1, sq.Item2);
            if (!gs.ObjDb.Occupied(loc))
            {
                // the square is free so move there!
                return new MoveAction(actor, loc, gs);
            }
        }

        // Otherwise do nothing!
        return new PassAction();
    }
}

interface IBehaviour
{
    Action CalcAction(Mob actor, GameState gameState, UserInterface ui);
    (Action, InputAccumulator?) Chat(Mob actor, GameState gameState);
}

class MonsterBehaviour : IBehaviour
{
    Dictionary<string, ulong> _lastUse = [];

    bool Available(ActionTrait act, int distance, ulong turn)
    {
        if (_lastUse.TryGetValue(act.Name, out var last) && last + act.Cooldown > turn)
        {
            return false;
        }

        if (act.MinRange <= distance && act.MaxRange >= distance)
            return true;

        return false;
    }

    Action FromTrait(Mob mob, ActionTrait act, GameState gs)
    {
        if (act is MobMeleeTrait meleeAttack)
        {
            var p = gs.Player;
            mob.Dmg = new Damage(meleeAttack.DamageDie, meleeAttack.DamageDice, meleeAttack.DamageType);
            _lastUse[act.Name] = gs.Turn;
            return new MeleeAttackAction(mob, p.Loc, gs);
        }
        else if (act is MobMissileTrait missileAttack)
        {
            mob.Dmg = new Damage(missileAttack.DamageDie, missileAttack.DamageDice, missileAttack.DamageType);
            _lastUse[act.Name] = gs.Turn;

            var arrowAnim = new ArrowAnimation(gs, MobMissileTrait.Trajectory(mob, gs.Player.Loc), Colours.LIGHT_BROWN);
            gs.UIRef().RegisterAnimation(arrowAnim);

            var arrow = ItemFactory.Get("arrow", gs.ObjDb);
            return new MissileAttackAction(mob, gs.Player.Loc, gs, arrow);
        }
        else if (act is SpellActionTrait spell)
        {
            _lastUse[spell.Name] = gs.Turn;
            if (spell.Name == "Blink")
                return new BlinkAction(mob, gs);
            else if (spell.Name == "FogCloud")
                return new FogCloudAction(mob, gs, gs.Player.Loc);
            else if (spell.Name == "Entangle")
                return new EntangleAction(mob, gs, gs.Player.Loc);
            else if (spell.Name == "Web")
                return new WebAction(gs, gs.Player.Loc);
            else if (spell.Name == "Firebolt")
                return new FireboltAction(mob, gs, gs.Player.Loc, ActionTrait.Trajectory(mob, gs.Player.Loc));
        }
        
        return new NullAction();
    }

    public Action CalcAction(Mob actor, GameState gs, UserInterface ui)
    {        
        if (actor.Status == ActorStatus.Idle)
        {
            return new PassAction();
        }

        // Should prioritize an escape action if the monster is hurt?
        // Maybe mobs can eventually have a bravery stat?

        // Actions should be in the list in order of prerfence
        foreach (var act in actor.Actions)
        {
            if (_lastUse.TryGetValue(act.Name, out var last) && last + act.Cooldown > gs.Turn)
                continue;

            if (act.Available(actor, gs)) 
                return FromTrait(actor, act, gs);
        }

        return actor.MoveStrategy.MoveAction(actor, gs);
    }

    public (Action, InputAccumulator?) Chat(Mob actor, GameState gameState) => (new NullAction(), null);
}

class VillagePupBehaviour : IBehaviour
{
    // Eventually different messages depending on if the dog is friendly or not?
    public string ChatText(Mob animal)
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

    public Action CalcAction(Mob pup, GameState gameState, UserInterface ui)
    {
        var town = gameState.Campaign.Town!;

        double roll = gameState.Rng.NextDouble();
        if (roll < 0.25)
            return new PassAction();

        // in the future, when they become friendly with the player they'll move toward them
        List<Loc> mvOpts = [];
        foreach (var sq in Util.Adj8Sqs(pup.Loc.Row, pup.Loc.Col))
        {
            if (LocInTown(sq.Item1, sq.Item2, town))
            {
                var loc = pup.Loc with { Row = sq.Item1, Col = sq.Item2 };
                if (Passable(gameState.TileAt(loc).Type) && !gameState.ObjDb.Occupied(loc))
                    mvOpts.Add(loc);
            }
        }

        // Keep the animal tending somewhat to move toward the center of town
        var centerRow = town.Row + town.Height / 2;
        var centerCol = town.Col + town.Width / 2;
        var adj = pup.Loc;
        if (pup.Loc.Row < centerRow && pup.Loc.Col < centerCol)
            adj = pup.Loc with { Row = pup.Loc.Row + 1, Col = pup.Loc.Col + 1 };
        else if (pup.Loc.Row > centerRow && pup.Loc.Col > centerCol)
            adj = pup.Loc with { Row = pup.Loc.Row - 1, Col = pup.Loc.Col - 1 };
        else if (pup.Loc.Row < centerRow && pup.Loc.Col > centerCol)
            adj = pup.Loc with { Row = pup.Loc.Row + 1, Col = pup.Loc.Col - 1 };
        else if (pup.Loc.Row > centerRow && pup.Loc.Col < centerCol)
            adj = pup.Loc with { Row = pup.Loc.Row - 1, Col = pup.Loc.Col + 1 };

        if (adj != pup.Loc && Passable(gameState.TileAt(adj).Type) && !gameState.ObjDb.Occupied(adj))
        {
            mvOpts.Add(adj);
            mvOpts.Add(adj);
            mvOpts.Add(adj);
        }

        if (mvOpts.Count == 0)
            return new PassAction();
        else
            return new MoveAction(pup, mvOpts[gameState.Rng.Next(mvOpts.Count)], gameState);
    }

    public (Action, InputAccumulator) Chat(Mob animal, GameState gs)
    {
        var sb = new StringBuilder(animal.Appearance.IndefArticle().Capitalize());
        sb.Append(".\n\n");
        sb.Append(animal.ChatText());

        gs.WritePopup(sb.ToString(), "");
        return (new PassAction(), new PauseForMoreAccumulator());
    }
}

class PriestBehaviour : IBehaviour
{
    DateTime _lastIntonation = new(1900, 1, 1);

    public Action CalcAction(Mob actor, GameState gameState, UserInterface ui)
    {
        if ((DateTime.Now - _lastIntonation).TotalSeconds > 10)
        {
            var bark = new BarkAnimation(gameState, 2500, actor, "Praise be to Huntokar!");
            ui.RegisterAnimation(bark);
            _lastIntonation = DateTime.Now;

            return new PassAction();
        }
        else
        {
            return new PassAction();
        }
    }

    public (Action, InputAccumulator) Chat(Mob priest, GameState gs)
    {
        var sb = new StringBuilder(priest.Appearance.IndefArticle().Capitalize());
        sb.Append(".\n\n");
        sb.Append(priest.ChatText());
        sb.Append("\n\n");

        gs.WritePopup(sb.ToString(), priest.FullName);
        return (new PassAction(), new PauseForMoreAccumulator());
    }

    public string ChatText(Mob priest)
    {
        var sb = new StringBuilder();
        sb.Append("\"It is my duty to look after the spiritual well-being of the village.");
        //sb.Append(((Villager)priest).Town.Name);
        //sb.Append(".\"");

        return sb.ToString();
    }
}

class SmithBehaviour(double markup) : IBehaviour, IShopkeeper
{
    public double Markup { get; set; } = markup;
    DateTime _lastBark = new(1900, 1, 1);

    static string PickBark(Mob smith, Random rng)
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

    public Action CalcAction(Mob smith, GameState gameState, UserInterface ui)
    {
        if ((DateTime.Now - _lastBark).TotalSeconds > 10)
        {
            var bark = new BarkAnimation(gameState, 2500, smith, PickBark(smith, gameState.Rng));
            ui.RegisterAnimation(bark);
            _lastBark = DateTime.Now;

            return new PassAction();
        }
        else
        {
            return new PassAction();
        }
    }

    public string ChatText(Mob smith)
    {
        var sb = new StringBuilder();
        sb.Append('"');
        if (Markup > 1.75)
            sb.Append("If you're looking for arms or armour, I'm the only game in town!");
        else
            sb.Append("You'll want some weapons or better armour before venturing futher!");
        sb.Append('"');

        return sb.ToString();
    }

    public (Action, InputAccumulator) Chat(Mob actor, GameState gs)
    {
        var acc = new ShopMenuAccumulator((Mob)actor, gs);
        var action = new ShopAction(actor, gs);

        return (action, acc);
    }
}

class GrocerBehaviour(double markup) : IBehaviour, IShopkeeper
{
    public double Markup { get; set; } = markup;
    DateTime _lastBark = new(1900, 1, 1);

    static string PickBark(Mob grocer, Random rng)
    {
        int roll = rng.Next(3);
        if (roll == 0)
            return "Supplies for the prudent adventurer!";
        else if (roll == 1)
            return "Check out our specials!";
        else
            return "Store credit only.";
    }

    public Action CalcAction(Mob grocer, GameState gameState, UserInterface ui)
    {
        if ((DateTime.Now - _lastBark).TotalSeconds > 10)
        {
            var bark = new BarkAnimation(gameState, 2500, grocer, PickBark(grocer, gameState.Rng));
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
        var sb = new StringBuilder();
        sb.Append('"');
        sb.Append("Welcome to the market!");
        //sb.Append(grocer.Town.Name.Capitalize());
        //sb.Append(" market!");
        sb.Append('"');

        return sb.ToString();
    }

    public (Action, InputAccumulator) Chat(Mob actor, GameState gs)
    {
        var acc = new ShopMenuAccumulator(actor, gs);
        var action = new ShopAction(actor, gs);

        return (action, acc);
    }
}

// class VillagerBehaviour : IBehaviour
// {
//     public Action CalcAction(Actor actor, GameState gameState, UserInterface ui)
//     {
//         return new PassAction();
//     }
// }

interface IShopkeeper
{
    double Markup { get; set; }
}