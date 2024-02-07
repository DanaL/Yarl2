
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

internal class CloseDoorAction(Actor actor, int row, int col, Map map) : Action
{
    private readonly Actor _actor = actor;
    private readonly int _row = row;
    private readonly int _col = col;
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

internal class OpenDoorAction(Actor actor, int row, int col, Map map) : Action
{
    private readonly Actor _actor = actor;
    private readonly int _row = row;
    private readonly int _col = col;
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
            TileType.DeepWater => "You don't want to get your boots wet.",
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
                    result.AltAction = new OpenDoorAction(_actor, _row, _col, _map);
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
            result.Message = "";
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
    public override ActionResult Execute() 
    {
        throw new GameQuitException();
    }
}
