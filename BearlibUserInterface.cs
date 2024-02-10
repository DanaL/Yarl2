using BearLibNET;
using TKCodes = BearLibNET.TKCodes;

namespace Yarl2;

internal class BLUserInferface : UserInterface, IDisposable
{        
    private readonly Dictionary<int, char> KeyToChar = [];

    public BLUserInferface(string windowTitle, Options opt) : base(opt)
    {
        FontSize = opt.FontSize;
        SetUpKeyToCharMap();
        Terminal.Open();
        Terminal.Set($"window: size={ScreenWidth}x{ScreenHeight}, title={windowTitle}; font: DejaVuSansMono.ttf, size={FontSize}");
        Terminal.Refresh();                           
    }

    private void SetUpKeyToCharMap()
    {
        int curr = (int)TKCodes.InputEvents.TK_A;
        for (int ch = 'a'; ch <= 'z'; ch++)
        {
            KeyToChar.Add(curr++, (char)ch);
        }
        curr = (int)TKCodes.InputEvents.TK_1;
        for (int ch = '1'; ch <= '9'; ch++)
        {
            KeyToChar.Add(curr++, (char)ch);
        }
        KeyToChar.Add(curr, '0');
        KeyToChar.Add((int)TKCodes.InputEvents.TK_RETURN_or_ENTER, '\n');
        KeyToChar.Add((int)TKCodes.InputEvents.TK_SPACE, ' ');
        KeyToChar.Add((int)TKCodes.InputEvents.TK_BACKSPACE, (char)BACKSPACE);
        KeyToChar.Add((int)TKCodes.InputEvents.TK_COMMA, ',');
        KeyToChar.Add((int)TKCodes.InputEvents.TK_PERIOD, '.');
    }

    protected override UIEvent PollForEvent()
    {        
        if (Terminal.HasInput())
        {
            int key = Terminal.Read();
            if (key == (int)TKCodes.InputEvents.TK_CLOSE)
                return new UIEvent(UIEventType.Quiting, '\0');

            if (KeyToChar.TryGetValue(key, out char value))
            {
                // I feel like there has to be a better way to handle shifted characters
                // in Bearlib but I haven't found it yet...                
                if (Terminal.Check((int)TKCodes.InputEvents.TK_SHIFT))
                {
                    value = value switch
                    {
                        ',' => '<',
                        '.' => '>',
                        _ => char.ToUpper(value)
                    };                   
                }
            }
            
            // When (SHIFT, CTRL, etc) is pressed HasInput() is still true but
            // we only want to return a KeyInput event if there's an actual
            // value entered
            if (value != '\0')
                return new UIEvent(UIEventType.KeyInput, value);
        }

        return new UIEvent(UIEventType.NoEvent, '\0');
    }

    void WriteSideBar()
    {
        Terminal.Print(ViewWidth, 1, $"| {Player.Name}".PadRight(ViewWidth));
        Terminal.Print(ViewWidth, 2, $"| HP: {Player.CurrHP} ({Player.MaxHP})".PadRight(ViewWidth));

        string blank = "|".PadRight(ViewWidth);
        for (int row = 3; row < ScreenHeight; row++)
        {
            Terminal.Print(ViewWidth, row, blank);
        }
    }

    public override void UpdateDisplay()
    {
        Terminal.Clear();

        if (_longMessage != null)
        {            
            for (int row = 0; row < _longMessage.Count; row++)
            {
                Terminal.Print(0, row, _longMessage[row]);
            }
        }
        else
        {
            Terminal.Print(0, 0, _messageBuffer.PadRight(ScreenWidth));

            if (Player is not null)
            {
                for (int row = 0; row < ScreenHeight - 1; row++)
                {
                    for (int col = 0; col < ViewWidth; col++)
                    {
                        var (colour, ch) = SqsOnScreen[row, col];
                        Terminal.Color(colour);
                        Terminal.Put(col, row + 1, ch);
                    }
                }

                Terminal.Color(WHITE);
                Terminal.Put(PlayerScreenCol, PlayerScreenRow + 1, '@');

                WriteSideBar();
            }
        }
        
        Terminal.Refresh();
    }

    public void Dispose()
    {            
        Dispose(true);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            Terminal.Close();
        }            
    }
}
