﻿
namespace Yarl2;

internal class ActionResult
{
    public bool Successful { get; set; }
    public string? Message { get; set; }
    public Action? AltAction { get; set; }

    public ActionResult() { }
}

internal abstract class Action
{
    public abstract ActionResult Execute();
}

internal class PortalAction(GameState gameState) : Action
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

internal class DownstairsAction(GameState gameState) : PortalAction(gameState)
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

internal class UpstairsAction(GameState gameState) : PortalAction(gameState)
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

internal abstract class DirectionalAction() : Action
{
    public int Row { get; set; }
    public int Col { get; set; }
}

internal class CloseDoorAction(Actor actor, Map map) : DirectionalAction
{
    private readonly Actor _actor = actor;
    private readonly Map _map = map;

    public override ActionResult Execute()
    {
        var result = new ActionResult() { Successful = false };
        var door = _map.TileAt(Row, Col);

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

internal class OpenDoorAction(Actor actor, Map map) : DirectionalAction
{
    private readonly Actor _actor = actor;
    private readonly Map _map = map;

    public override ActionResult Execute()
    {
        var result = new ActionResult() { Successful = false };
        var door = _map.TileAt(Row, Col);

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

internal class MoveAction(Actor actor, int row, int col, GameState gameState) : Action
{
    private readonly Actor _actor = actor;
    private readonly int _row = row;
    private readonly int _col = col;
    private readonly Map _map = gameState.Map!;
    private readonly bool _bumpToOpen = gameState.Options!.BumpToOpen;

    static string BlockedMessage(Tile tile)
    {
        return tile.Type switch
        {
            TileType.DeepWater => "The ocean seems deep and cold.",
            TileType.Mountain or TileType.SnowPeak => "You cannot scale the mountain!",
            _ => "You cannot go that way!"
        }; ;
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
                    openAction.Row = _row;
                    openAction.Col = _col;
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
            result.Message = _map.TileAt(_row, _col).StepMessage;
        }

        return result;
    }
}

internal class PassAction(Actor actor) : Action
{
    private Actor _actor = actor;

    public override ActionResult Execute() 
    {
        // do nothing for now but eventually there will be an energy cost to passing
        // (ie., time will pass in game)
        return new ActionResult() { Successful = true };
    }
}

// I guess I can later add extra info about whether or not the player died, quit,
// or quit and saved?
internal class QuitAction : Action
{
    public override ActionResult Execute() => throw new GameQuitException();
}

internal class SaveGameAction : Action
{
    public override ActionResult Execute() => throw new Exception("Shouldn't actually try to execute a Save Game action!");
}

internal class NullAction : Action
{
    public override ActionResult Execute() => throw new Exception("Hmm this should never happen");
}