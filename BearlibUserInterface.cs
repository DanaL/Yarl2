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

using BearLibNET;
using Color = BearLibNET.DefaultImplementations.Color;
using TKCodes = BearLibNET.TKCodes;

namespace Yarl2;

internal class BLUserInferface : UserInterface, IDisposable
{        
    private readonly Dictionary<int, char> KeyToChar = [];
    private Dictionary<Colour, Color> _colours = [];

    public BLUserInferface(string windowTitle, Options opt) : base(opt)
    {
        FontSize = opt.FontSize;
        SetUpKeyToCharMap();
        Terminal.Open();
        Terminal.Set($"window: size={ScreenWidth}x{ScreenHeight}, title={windowTitle}; font: DejaVuSansMono.ttf, size={FontSize}");
        Terminal.Refresh();
    }

    private Color ToBearLibColour(Colour colour) 
    {
        if (!_colours.TryGetValue(colour, out Color value)) 
        {
            value = new Color() { 
                    A = 255, 
                    R = colour.R,
                    G = colour.G,
                    B = colour.B
            };
            _colours.Add(colour, value);
        }

        return value;
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
        KeyToChar.Add((int)TKCodes.InputEvents.TK_BACKSPACE, (char)Constants.BACKSPACE);
        KeyToChar.Add((int)TKCodes.InputEvents.TK_COMMA, ',');
        KeyToChar.Add((int)TKCodes.InputEvents.TK_PERIOD, '.');
        KeyToChar.Add((int)TKCodes.InputEvents.TK_ESCAPE, (char)Constants.ESC);        
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
                        '8' => '*',
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
        Terminal.Color(ToBearLibColour(Colours.WHITE));
        Terminal.Print(ViewWidth, 1, $"| {Player.Name}".PadRight(ViewWidth));
        Terminal.Print(ViewWidth, 2, $"| HP: {Player.CurrHP} ({Player.MaxHP})".PadRight(ViewWidth));

        string blank = "|".PadRight(ViewWidth);
        for (int row = 3; row < ViewHeight; row++)
        {
            Terminal.Print(ViewWidth, row, blank);
        }
    }

    private void WriteDropDown()
    {
        Terminal.Color(ToBearLibColour(Colours.WHITE));
        int width = MenuRows!.Select(r => r.Length).Max() + 2;
        int col = ViewWidth - width;
        int row = 1;

        foreach (var line in MenuRows!)
        {
            Terminal.Print(col, row++, line.PadRight(width));
            
        }
        Terminal.Print(col, row, "".PadRight(width));
    }

    // The WritePopUp() and WriteMessageSection() methods between Bearlib and SDL
    // are soooo close to being able to be merged and pulled up into the superclass.
    // All I really need to do is make a wrapper around Termnial.Print()
    private void WritePopUp()
    {
        Terminal.Color(ToBearLibColour(Colours.WHITE));
        var lines = _popupBuffer.Split('\n');
        int bufferWidth = lines.Select(l => l.Length).Max();
        int width = bufferWidth > _popupWidth ? bufferWidth : _popupWidth;
        width += 2;
        int col = (ViewWidth - width) / 2;
        int row = 5;

        Terminal.Print(col, 4, "".PadRight(width));        
        foreach (var line in lines)
        {
            Terminal.Print(col, row++, line.PadRight(width));
        }
        Terminal.Print(col, row, "".PadRight(width));        
    }

    void WriteMessagesSection()
    {
        var msgs = MessageHistory.Take(5)
                                 .Select(msg => msg.Fmt);

        int row = ScreenHeight - 1;
        Colour colour = Colours.WHITE;
        foreach (var msg in msgs)
        {
            Terminal.Color(ToBearLibColour(colour));
            var s = msg.PadRight(ScreenWidth);
            Terminal.Print(0, row--, s.PadRight(ScreenWidth));
            
            if (colour == Colours.WHITE)
                colour = Colours.GREY;
            else if (colour == Colours.GREY)
                colour = Colours.DARK_GREY;
        }
    }

    public override void UpdateDisplay()
    {
        Terminal.Clear();

        if (_longMessage != null)
        {
            Terminal.Color(ToBearLibColour(Colours.WHITE));
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
                for (int row = 0; row < ViewHeight; row++)
                {
                    for (int col = 0; col < ViewWidth; col++)
                    {
                        var (colour, ch) = SqsOnScreen[row, col];
                        Terminal.Color(ToBearLibColour(colour));
                        Terminal.Put(col, row + 1, ch);
                    }
                }

                WriteSideBar();
            }

            if (MessageHistory.Count > 0)
                WriteMessagesSection();

            if (MenuRows is not null)
            {
                WriteDropDown();
            }

            if (_popupBuffer is not null)
            {
                WritePopUp();
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
