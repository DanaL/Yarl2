
namespace Yarl2;

internal class Player : Actor
{
    public string Name { get; set; }
    public int MaxHP { get; set; }
    public int CurrHP { get; set; }
    private IInputAccumulator? _accumulator;
    private Action _deferred;

    public Player(string name, int row, int col)
    {
        Name = name;
        Row = row;
        Col = col;
        MaxHP = 20;
        CurrHP = 15;
        MaxVisionRadius = 15;
        CurrVisionRadius = MaxVisionRadius;
    }

    public override Action TakeTurn(UserInterface ui, GameState gameState)
    {
        if (ui.InputBuffer.Count > 0)
        {
            char ch = ui.InputBuffer.Dequeue();
            
            if (_accumulator is not null)
            {
                _accumulator.Input(ch);
                if (_accumulator.Done)
                {                    
                    if (_accumulator.Success)
                    {
                        _accumulator = null;
                        return _deferred;
                    }
                    else
                    {
                        _accumulator = null;
                        ui.WriteMessage("Nevermind.");
                        return new NullAction();
                    }
                }
            }

            if (ch == 'h')
                return new MoveAction(this, Row, Col - 1, gameState);
            else if (ch == 'j')
                return new MoveAction(this, Row + 1, Col, gameState);
            else if (ch == 'k')
                return new MoveAction(this, Row - 1, Col, gameState);
            else if (ch == 'l')
                return new MoveAction(this, Row, Col + 1, gameState);
            else if (ch == 'y')
                return new MoveAction(this, Row - 1, Col - 1, gameState);
            else if (ch == 'u')
                return new MoveAction(this, Row - 1, Col + 1, gameState);
            else if (ch == 'b')
                return new MoveAction(this, Row + 1, Col - 1, gameState);
            else if (ch == 'n')
                return new MoveAction(this, Row + 1, Col + 1, gameState);
            else if (ch == 'E')
                return new PortalAction(gameState);
            else if (ch == '>')
                return new DownstairsAction(gameState);
            else if (ch == '<')
                return new UpstairsAction(gameState);
            else if (ch == 'Q')
                return new QuitAction();
            else if (ch == 'S')
            {
                _accumulator = new YesNoAccumulator();
                _deferred = new SaveGameAction();
                ui.WriteMessage("Really quit and save? (y/n)");
            }
            else
                return new PassAction(this);
        }

        return new NullAction();
    }
}
