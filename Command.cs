
namespace Yarl2;

internal class CommandResult
{
    public bool Successful { get; set; }
    public string? Message { get; set; }

    public CommandResult() { }
}

internal abstract class Command
{
    public abstract CommandResult Execute();
}

internal class MoveCommand(Actor actor, short row, short col, Map map) : Command
{
    private Actor _actor = actor;
    private short _row = row;
    private short _col = col;
    private Map _map = map;

    public override CommandResult Execute()
    {
        CommandResult result = new CommandResult();

        ushort nextRow = (ushort)_row;
        ushort nextCol = (ushort)_col;

        if (!_map.InBounds(nextRow, nextCol) || !_map.TileAt(nextRow, nextCol).Passable())
        {
            result.Successful = false;
            if (_actor is Player)
                result.Message = "You cannot go that way!";
        }
        else
        {
            result.Successful = true;
            _actor.Row = nextRow;
            _actor.Col = nextCol;
            result.Message = "";
        }

        return result;
    }
}

internal class PassCommand(Actor actor) : Command
{
    private Actor _actor = actor;

    public override CommandResult Execute() 
    {
        // do nothing for now but eventually there will be an energy cost to passing
        // (ie., time will pass in game)
        return new CommandResult() { Successful = true };
    }
}

// I guess I can later add extra info about whether or not the player died, quit,
// or quit and saved?
internal class QuitCommand : Command
{
    public override CommandResult Execute() 
    {
        throw new GameQuitException();
    }
}

// This could be a Singleton?
internal class NullCommand : Command
{
    public override CommandResult Execute() 
    {
        throw new Exception("Hmm this shouldn't be called.");
    }
}
