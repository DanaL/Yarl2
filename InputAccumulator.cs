namespace Yarl2;

// Handers to keep state while we are waiting for user input.
// I'm sure someone smarter could come up with a cleaner 
// solution...

internal interface IInputAccumulator 
{
    bool Success { get; }
    bool Done { get; }
    void Input(char ch);
}

internal class YesNoAccumulator : IInputAccumulator
{
    private bool _done;
    private bool _success;

    public YesNoAccumulator() => _done = false;

    public bool Success => _success;
    public bool Done => _done;

    public void Input(char ch)
    {
        // Need to eventually handle ESC
        if (ch == 'y')
        {
            _done = true;
            _success = true;
        }
        else if (ch == 'n')
        {
            _done = true;
            _success = false;
        }
    }
}

internal class DirectionAccumulator : IInputAccumulator
{
    private bool _done;
    private bool _success;
    private (int, int) _result;

    public DirectionAccumulator() => _done = false;

    public bool Success => _success;
    public bool Done => _done;
    public (int, int) Result => _result;

    public void Input(char ch)
    {
        // Need to eventually handle ESC
        if (ch == 'y')
        {
            _result = (-1, -1);
            _done = true;
            _success = true;
        }
        else if (ch == 'u')
        {
            _result = (-1, 1);
            _done = true;
            _success = true;
        }
        else if (ch == 'h')
        {
            _result = (0, -1);
            _done = true;
            _success = true;
        }
        else if (ch == 'j')
        {
            _result = (1, 0);
            _done = true;
            _success = true;
        }
        else if (ch == 'k')
        {
            _result = (-1, 0);
            _done = true;
            _success = true;
        }
        else if (ch == 'l')
        {
            _result = (0, 1);
            _done = true;
            _success = true;
        }
        else if (ch == 'b')
        {
            _result = (1, -1);
            _done = true;
            _success = true;
        }
        else if (ch == 'n')
        {
            _result = (1, 1); 
            _done = true;
            _success = true;
        }
    }
}