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

class ActionResult
{
    public bool Successful { get; set; }
    public string? Message { get; set; }
    public Action? AltAction { get; set; }
    public double EnergyCost { get; set; } = 0.0;

    public ActionResult() { }
}

abstract class Action
{
    public abstract ActionResult Execute();
    public virtual void ReceiveAccResult(AccumulatorResult result) {}
}

class PortalAction(GameState gameState) : Action
{
    protected readonly GameState _gameState = gameState;

    protected void UsePortal(Portal portal, ActionResult result)
    {
        var start = new Loc(_gameState.CurrDungeon, _gameState.CurrLevel, _gameState.Player!.Row, _gameState.Player!.Col);
        var (dungeon, level, r, c) = portal.Destination;
        _gameState.EnterLevel(dungeon, level);
        _gameState.Player!.Row = r;
        _gameState.Player!.Col = c;
        _gameState.ActorMoved(_gameState.Player!, start, portal.Destination);
        result.Successful = true;

        if (start.DungeonID != portal.Destination.DungeonID)
            result.Message = _gameState.CurrentDungeon.ArrivalMessage;
    
        result.EnergyCost = 1.0;
    }

    public override ActionResult Execute()
    {
        var result = new ActionResult() { Successful = false };
        
        var p = _gameState.Player;        
        var t = _gameState.CurrentMap.TileAt(p.Row, p.Col);

        if (t.Type == TileType.Portal) 
        {
            UsePortal((Portal)t, result);
        }
        else
        {
            result.Message = "There is nowhere to go here.";
        }

        return result;
    }
}

class DownstairsAction(GameState gameState) : PortalAction(gameState)
{
    public override ActionResult Execute()
    {
        var result = new ActionResult() { Successful = false };

        var p = _gameState.Player;        
        var t = _gameState.CurrentMap.TileAt(p.Row, p.Col);

        if (t.Type == TileType.Downstairs) 
        {
            UsePortal((Portal)t, result);
        }
        else
        {
            result.Message = "You cannot go down here.";
        }

        return result;
    }
}

class UpstairsAction(GameState gameState) : PortalAction(gameState)
{
    public override ActionResult Execute()
    {
        var result = new ActionResult() { Successful = false };

        var p = _gameState.Player;        
        var t = _gameState.CurrentMap.TileAt(p.Row, p.Col);

        if (t.Type == TileType.Upstairs) 
        {
            UsePortal((Portal)t, result);
        }
        else
        {
            result.Message = "You cannot go up here.";
        }

        return result;
    }
}

abstract class DirectionalAction(Actor actor) : Action
{
    protected readonly Actor _actor = actor;
    protected int _row { get; set; }
    protected int _col { get; set; }

    public override void ReceiveAccResult(AccumulatorResult result)
    {
        var dirResult = (DirectionAccumulatorResult)result;
        _row = _actor.Row + dirResult.Row;
        _col = _actor.Col + dirResult.Col;
    }
}

class CloseDoorAction(Actor actor, Map map, GameState gs) : DirectionalAction(actor)
{
    private GameState _gs = gs;
    private readonly Map _map = map;

    public override ActionResult Execute()
    {
        var result = new ActionResult() { Successful = false };
        var door = _map.TileAt(_row, _col);

        if (door is Door d)
        {
            if (d.Open)
            {                
                d.Open = false;
                result.Successful = true;
                result.EnergyCost = 1.0;
                if (_actor is Player)
                    result.Message = "You close the door.";
                
                // Find any light sources tht were affecting the door and update them, since
                // it's now open. (Eventually gotta extend it to any aura type effects)
                var loc = new Loc(_gs.CurrDungeon, _gs.CurrLevel, _row, _col);                
                foreach (var src in _gs.ObjsAffectingLoc(loc, TerrainFlags.Lit))
                {
                    _gs.CurrentMap.RemoveEffectFromMap(TerrainFlags.Lit, src.ID);
                    _gs.ToggleEffect(src, src.Loc, TerrainFlags.Lit, true);
                }
            }
            else if (_actor is Player)
            {
                result.Message = "The door is already closed!";
            }
        }
        else if (_actor is Player)
        {
            result.Message = "There's no door there!";
        }

        return result;
    }
}

class OpenDoorAction : DirectionalAction
{
    private GameState _gs;
    private readonly Map _map;

    public OpenDoorAction(Actor actor, Map map, GameState gs) : base(actor)
    {
        _map = map;
        _gs = gs;
    }

    public OpenDoorAction(Actor actor, Map map, int row, int col, GameState gs) : base(actor)
    {
        _map = map;
        _row = row;
        _col = col;
        _gs = gs;
    }

    public override ActionResult Execute()
    {
        var result = new ActionResult() { Successful = false };
        var door = _map.TileAt(_row, _col);

        if (door is Door d)
        {
            if (!d.Open)
            {
                d.Open = true;
                result.Successful = true;
                result.EnergyCost = 1.0;
                if (_actor is Player)
                    result.Message = "You open the door.";

                // Find any light sources tht were affecting the door and update them, since
                // it's now open. (Eventually gotta extend it to any aura type effects)
                var loc = new Loc(_gs.CurrDungeon, _gs.CurrLevel, _row, _col);
                foreach (var src in _gs.ObjsAffectingLoc(loc, TerrainFlags.Lit))
                {
                    _gs.ToggleEffect(src, src.Loc, TerrainFlags.Lit, true);
                }
                _gs.ToggleEffect(_actor, new Loc(_gs.CurrDungeon, _gs.CurrLevel, _actor.Row, _actor.Col), TerrainFlags.Lit, true);
            }
            else if (_actor is Player)
            {
                result.Message = "The door is already open!";
            }
        }
        else if (_actor is Player)
        {
            result.Message = "There's no door there!";
        }

        return result;
    }
}

class MoveAction(Actor actor, int row, int col, GameState gameState) : Action
{
    private readonly Actor _actor = actor;
    private readonly int _row = row;
    private readonly int _col = col;
    private readonly Map _map = gameState.Map!;
    private readonly bool _bumpToOpen = gameState.Options!.BumpToOpen;
    private readonly GameState _gameState = gameState;

    static string BlockedMessage(Tile tile)
    {
        return tile.Type switch
        {
            TileType.DeepWater => "The water seems deep and cold.",
            TileType.Mountain or TileType.SnowPeak => "You cannot scale the mountain!",
            _ => "You cannot go that way!"
        }; ;
    }

    private string CalcDesc()
    {
        if (_actor is not Player)
            return "";

        var loc = new Loc(_gameState.CurrDungeon, _gameState.CurrLevel, _row, _col);
        var items = _gameState.ObjDB.ItemsAt(loc);

        if (items.Count == 0)
            return _map.TileAt(_row, _col).StepMessage;
        else if (items.Count > 1)
            return "There are several items here.";
        else
            return $"There is {items[0].FullName.IndefArticle()} here.";
    }

    public override ActionResult Execute()
    {
        var result = new ActionResult();

        if (!_map.InBounds(_row, _col))
        {
            // in theory this shouldn't ever happen...
            result.Successful = false;
            if (_actor is Player)
                result.Message = "You cannot go that way!";
        }
        else if (!_map.TileAt(_row, _col).Passable())
        {
            result.Successful = false;

            if (_actor is Player)
            {
                var tile = _map.TileAt(_row, _col);
                if (_bumpToOpen && tile.Type == TileType.Door)
                {
                    var openAction = new OpenDoorAction(_actor, _map, _row, _col, _gameState);
                    result.AltAction = openAction;
                }
                else
                {
                    result.Message = BlockedMessage(tile);
                }
            }
        }
        else
        {            
            result.Successful = true;
            result.EnergyCost = 1.0;

            var from = new Loc(_gameState.CurrDungeon, _gameState.CurrLevel, _actor.Row, _actor.Col);
            var to = new Loc(_gameState.CurrDungeon, _gameState.CurrLevel, _row, _col);
            _gameState.ActorMoved(actor, from, to);

            _actor.Row = _row;
            _actor.Col = _col;
            result.Message = CalcDesc();
        }

        return result;
    }
}

class PickupItemAction(UserInterface ui, Actor actor, GameState gs) : Action
{
    public char Choice { get; set; }
    private UserInterface _ui = ui;
    private Actor _actor = actor;
    private GameState _gameState = gs;

    public override ActionResult Execute()
    {
        _ui.CloseMenu();
        var loc = new Loc(_gameState.CurrDungeon, _gameState.CurrLevel, _actor.Row, _actor.Col);
        var itemStack = _gameState.ObjDB.ItemsAt(loc);

        var inv = ((Player)_actor).Inventory;
        bool freeSlot = inv.UsedSlots().Length < 26;

        if (!freeSlot)
            return new ActionResult() { Successful=false, Message="There's no room in your inventory!" };

        int i = Choice - 'a';
        var item = itemStack[i];
        itemStack.RemoveAt(i);
        inv.Add(item);
        return new ActionResult() { Successful=true, Message=$"You pick up {item.FullName.DefArticle()}.", EnergyCost = 1.0 };
    }

    public override void ReceiveAccResult(AccumulatorResult result)
    {
        var menuResult = (MenuAccumulatorResult)result;
        Choice = menuResult.Choice;
    }
}

class UseItemAction(UserInterface ui, Actor actor, GameState gs) : Action
{
    public char Choice { get; set; }
    private UserInterface _ui = ui;
    private Actor _actor = actor;
    private GameState _gameState = gs;

    public override ActionResult Execute()
    {
        var item = ((IItemHolder)_actor).Inventory.ItemAt(Choice);
        _ui.CloseMenu();

        if (item is IUseableItem tool)
        {
            var (success, msg) = tool.Use(_gameState, _actor.Row, _actor.Col);
            return new ActionResult() { Successful = success, Message = msg, EnergyCost = 1.0 };
        }
        else
        {
            return new ActionResult() { Successful = false, Message = "You don't know how to use that!" };
        }
    }

    public override void ReceiveAccResult(AccumulatorResult result)
    {
        var menuResult = (MenuAccumulatorResult)result;
        Choice = menuResult.Choice;
    }
}

class DropItemAction(UserInterface ui, Actor actor, GameState gs) : Action
{
    public char Choice { get; set; }
    private UserInterface _ui = ui;
    private Actor _actor = actor;
    private GameState _gameState = gs;

    public override ActionResult Execute() 
    {
        var item = ((IItemHolder)_actor).Inventory.ItemAt(Choice);        
        _ui.CloseMenu();

        if (item.Equiped && item.Type == ItemType.Armour)
        {
            return new ActionResult() { Successful=false, Message="You cannot drop something you're wearing." };
        }
        else 
        {
            ((Player) _actor).Inventory.Remove(Choice, 1);
            _gameState.ItemDropped(item, _actor.Row, _actor.Col);
            item.Equiped = false;
            ((IItemHolder)_actor).CalcEquipmentModifiers();

            return new ActionResult() { Successful=true, Message=$"You drop {item.FullName.DefArticle()}.", EnergyCost = 1.0 };
        }
    }

    public override void ReceiveAccResult(AccumulatorResult result)
    {
        var menuResult = (MenuAccumulatorResult)result;
        Choice = menuResult.Choice;
    }
}

class ToggleEquipedAction(UserInterface ui, Actor actor) : Action
{
    public char Choice { get; set; }
    private UserInterface _ui = ui;
    private Actor _actor = actor;

    public override ActionResult Execute() 
    {
        ActionResult result;

        var item = ((Player)_actor).Inventory.ItemAt(Choice);
        _ui.CloseMenu();

        if (item.Type != ItemType.Armour && item.Type != ItemType.Weapon)
        {
            return new ActionResult() { Successful = false, Message = "You cannot equip that!" };
        }

        var (equipResult, conflict) = ((Player) _actor).Inventory.ToggleEquipStatus(Choice);
        
        switch (equipResult)
        {
            case EquipingResult.Equiped:
                result = new ActionResult() { Successful=true, Message=$"You ready {item.FullName.DefArticle()}.", EnergyCost = 1.0 };
                break;
            case EquipingResult.Unequiped:
                result = new ActionResult() { Successful=true, Message=$"You unequip {item.FullName.DefArticle()}.", EnergyCost = 1.0 };
                break;
            default:
                string msg = "You are already wearing ";
                if (conflict == ArmourParts.Helmet)
                    msg += "a helmet.";
                else if (conflict == ArmourParts.Shirt)
                    msg += "some armour.";
                result = new ActionResult() { Successful=true, Message=msg };
                break;
        }            
        
        ((IItemHolder)_actor).CalcEquipmentModifiers();

        return result;
    }

    public override void ReceiveAccResult(AccumulatorResult result)
    {
        var menuResult = (MenuAccumulatorResult)result;
        Choice = menuResult.Choice;
    }
}

class PassAction(IPerformer performer) : Action
{
    private IPerformer _perfmer = performer;

    public override ActionResult Execute() 
    {
        // do nothing for now but eventually there will be an energy cost to passing
        // (ie., time will pass in game)
        return new ActionResult() { Successful = true, EnergyCost = 1.0 };
    }
}

class CloseMenuAction(UserInterface ui) : Action 
{
    private UserInterface _ui = ui;

    public override ActionResult Execute() 
    { 
        _ui.CloseMenu();
        return new ActionResult() { Successful=true, Message="" };
    }
}

class ExtinguishAction(IPerformer performer, GameState gs) : Action
{
    private IPerformer _performer = performer;
    private GameState _gs = gs;

    public override ActionResult Execute()
    {        
        _performer.RemoveFromQueue = true; // signal to remove it from the performer queue
        _gs.CurrentMap.RemoveEffectFromMap(TerrainFlags.Lit, ((GameObj)_performer).ID);

        return new ActionResult() { Successful = true, Message = "The torch burns out.", EnergyCost = 1.0 };
    }
}

// I guess I can later add extra info about whether or not the player died, quit,
// or quit and saved?
class QuitAction : Action
{
    public override ActionResult Execute() => throw new GameQuitException();
}

class SaveGameAction : Action
{
    public override ActionResult Execute() => throw new Exception("Shouldn't actually try to execute a Save Game action!");
}

class NullAction : Action
{
    public override ActionResult Execute() => throw new Exception("Hmm this should never happen");
}