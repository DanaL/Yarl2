namespace Yarl2;

// Handers to keep state while we are waiting for user input.
// I'm sure someone smarter could come up with a cleaner 
// solution...

abstract class InputAccumulator 
{
    public virtual bool Success { get; set; }
    public virtual bool Done { get; set; }
    public string Msg { get; set; } = "";

    public abstract void Input(char ch);    
}

class MenuPickAccumulator(HashSet<char> options) : InputAccumulator
{
    private char _choice;
    private HashSet<char> _options = options;

    public char Choice => _choice;

    public override void Input(char ch) 
    {
        if (ch == Constants.ESC)
        {
            Done = true;
            Success = false;
        }
        else if (_options.Contains(ch))
        {
            Msg = "";
            _choice = ch;
            Done = true;
            Success = true;
        }
        else
        {
            Msg = "You don't have that.";
            Done = false;
            Success = false;
        }
    }
}

class PauseForMoreAccumulator : InputAccumulator
{
    private bool _keyPressed;

    public override bool Success => _keyPressed;
    public override bool Done => _keyPressed;

    // We're done on any input 
    public override void Input(char ch) => _keyPressed = true;
}

class YesNoAccumulator : InputAccumulator
{    
    public YesNoAccumulator() => Done = false;

    public override void Input(char ch)
    {
        // Need to eventually handle ESC
        if (ch == 'y')
        {
            Done = true;
            Success = true;
        }
        else if (ch == 'n')
        {
            Done = true;
            Success = false;
        }
    }
}

class DirectionAccumulator : InputAccumulator
{
    private (int, int) _result;

    public DirectionAccumulator() => Done = false;

    public (int, int) Result => _result;

    public override void Input(char ch)
    {
        // Need to eventually handle ESC
        if (ch == 'y')
        {
            _result = (-1, -1);
            Done = true;
            Success = true;
        }
        else if (ch == 'u')
        {
            _result = (-1, 1);
            Done = true;
            Success = true;
        }
        else if (ch == 'h')
        {
            _result = (0, -1);
            Done = true;
            Success = true;
        }
        else if (ch == 'j')
        {
            _result = (1, 0);
            Done = true;
            Success = true;
        }
        else if (ch == 'k')
        {
            _result = (-1, 0);
            Done = true;
            Success = true;
        }
        else if (ch == 'l')
        {
            _result = (0, 1);
            Done = true;
            Success = true;
        }
        else if (ch == 'b')
        {
            _result = (1, -1);
            Done = true;
            Success = true;
        }
        else if (ch == 'n')
        {
            _result = (1, 1); 
            Done = true;
            Success = true;
        }
    }
}