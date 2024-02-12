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

// Handers to keep state while we are waiting for user input.
// I'm sure someone smarter could come up with a cleaner 
// solution...

abstract class InputAccumulator 
{
    public virtual bool Success { get; set; }
    public virtual bool Done { get; set; }
    public string Msg { get; set; } = "";

    public abstract void Input(char ch);

    public virtual AccumulatorResult GetResult()
    {
        return new AccumulatorResult();
    }
}

class MenuPickAccumulator(HashSet<char> options) : InputAccumulator
{
    private char _choice;
    private HashSet<char> _options = options;

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

    public override AccumulatorResult GetResult()
    {
        return new MenuAccumulatorResult()
        {
            Choice = _choice
        };
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

    public override AccumulatorResult GetResult()
    {
        return new DirectionAccumulatorResult()
        {
            Row = _result.Item1,
            Col = _result.Item2
        };
    }
}

public class AccumulatorResult {}

public class DirectionAccumulatorResult : AccumulatorResult
{
    public int Row { get; set; }
    public int Col { get; set; }
}

public class MenuAccumulatorResult : AccumulatorResult
{
    public char Choice { get; set; }
}