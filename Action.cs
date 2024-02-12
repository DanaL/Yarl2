
using System.Reflection.Metadata.Ecma335;

namespace Yarl2;

class ActionResult
{
    public bool Successful { get; set; }
    public string? Message { get; set; }
    public Action? AltAction { get; set; }

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
        var (dungeon, level, r, c) = portal.Destination;
        _gameState.EnterLevel(dungeon, level);
        _gameState.Player!.Row = r;
        _gameState.Player!.Col = c;
        result.Successful = true;
        result.Message = _gameState.CurrentDungeon.ArrivalMessage;
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

class CloseDoorAction(Actor actor, Map map) : DirectionalAction(actor)
{    
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
                if (_actor is Player)
                    result.Message = "You close the door.";
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

class OpenDoorAction(Actor actor, Map map) : DirectionalAction(actor)
{
    private readonly Map _map = map;

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
                if (_actor is Player)
                    result.Message = "You open the door.";
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
            TileType.DeepWater => "The ocean seems deep and cold.",
            TileType.Mountain or TileType.SnowPeak => "You cannot scale the mountain!",
            _ => "You cannot go that way!"
        }; ;
    }

    private string CalcDesc()
    {
        var loc = new Loc(_gameState.CurrDungeon, _gameState.CurrLevel, _row, _col);
        var items = _gameState.ItemDB.ItemsAt(loc);

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
                    var openAction = new OpenDoorAction(_actor, _map);                    
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
        Loc loc = new Loc(_gameState.CurrDungeon, _gameState.CurrLevel, _actor.Row, _actor.Col);
        var itemStack = _gameState.ItemDB.ItemsAt(loc);

        var inv = (_actor as Player).Inventory;
        bool freeSlot = inv.UsedSlots().Length < 26;

        if (!freeSlot)
            return new ActionResult() { Successful=false, Message="There's no room in your inventory!" };

        int i = Choice - 'a';
        var item = itemStack[i];
        itemStack.RemoveAt(i);
        inv.Add(item);
        return new ActionResult() { Successful=false, Message=$"You pick up {item.FullName.DefArticle()}" };
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
        var item = (_actor as Player).Inventory.ItemAt(Choice);        
        _ui.CloseMenu();

        if (item.Equiped)
        {
            return new ActionResult() { Successful=false, Message="You cannot drop something you have equiped." };
        }
        else 
        {
            ((Player) actor).Inventory.Remove(Choice, 1);
            _gameState.ItemDropped(item, _actor.Row, _actor.Col);
            item.Equiped = false;
            _actor.CalcEquipmentModifiers();

            return new ActionResult() { Successful=true, Message=$"You drop {item.FullName.DefArticle()}." };
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

        var item = (_actor as Player).Inventory.ItemAt(Choice);        
        _ui.CloseMenu();

        var (equipResult, conflict) = ((Player) actor).Inventory.ToggleEquipStatus(Choice);
        
        switch (equipResult)
        {
            case EquipingResult.Equiped:
                result = new ActionResult() { Successful=true, Message=$"You ready {item.FullName.DefArticle()}." };
                break;
            case EquipingResult.Unequiped:
                result = new ActionResult() { Successful=true, Message=$"You unequip {item.FullName.DefArticle()}." };
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
        
        _actor.CalcEquipmentModifiers();

        return result;
    }

    public override void ReceiveAccResult(AccumulatorResult result)
    {
        var menuResult = (MenuAccumulatorResult)result;
        Choice = menuResult.Choice;
    }
}

class PassAction(Actor actor) : Action
{
    private Actor _actor = actor;

    public override ActionResult Execute() 
    {
        // do nothing for now but eventually there will be an energy cost to passing
        // (ie., time will pass in game)
        return new ActionResult() { Successful = true };
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