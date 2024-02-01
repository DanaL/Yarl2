using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using BearLibNET;
using TKCodes = BearLibNET.TKCodes;

namespace Yarl2
{
    internal class GameQuitException : Exception { }

    internal interface IDisplay
    {
        string QueryUser(string prompt);        
        void TitleScreen();
        char WaitForInput();
        void WriteLongMessage(List<string> message);
        void WriteMessage(string message);
    }

    internal class BLDisplay : IDisplay, IDisposable
    {
        private int BACKSPACE = 8;
        private int ScreenWidth = 60;
        private int ScreenHeight = 30;
        private int FontSize = 12;
        private Dictionary<int, char>? KeyToChar;

        public BLDisplay(string windowTitle)
        {
            SetUpKeyToCharMap();
            Terminal.Open();
            Terminal.Set($"window: size={ScreenWidth}x{ScreenHeight}; font: DejaVuSansMono.ttf, size={FontSize}");
        }

        private void SetUpKeyToCharMap()
        {
            KeyToChar = [];
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
        }

        public void WriteLongMessage(List<string> message)
        {
            Terminal.Clear();

            for (int row = 0; row < message.Count; row++)
            {
                Terminal.Print(0, row, message[row]);
            }

            Terminal.Refresh();
            WaitForInput();
        }

        public void WriteMessage(string message)
        {
            Terminal.Print(0, 0, message.PadRight(ScreenWidth));
            Terminal.Refresh();
        }

        public string QueryUser(string prompt)
        {            
            string answer = "";
            do
            {
                string message = $"{prompt} {answer}";
                WriteMessage(message);

                var ch = WaitForInput();
                if (ch == '\n')
                {
                    break;
                }
                else if (ch == BACKSPACE && answer.Length > 0)
                {
                    answer = answer[..^1];
                }
                else if (ch != '\0')
                {
                    answer += ch;
                }
            }
            while (true);

            return answer;
        }

        public char WaitForInput()
        {
            do 
            {
                int key = Terminal.Read();
                
                if (key == (int)TKCodes.InputEvents.TK_CLOSE)
                    throw new GameQuitException();

                if (KeyToChar.TryGetValue(key, out char value))
                {
                    return Terminal.Check((int)TKCodes.InputEvents.TK_SHIFT) ? char.ToUpper(value) : value;
                }
            }
            while (true);
        }

        public void TitleScreen()
        {
            var msg = new List<string>()
            {
                "",
                "",
                "",
                "",
                "     Welcome to Yarl2",
                "       (yet another attempt to make a roguelike,",
                "           this time in C#...)"
            };
            WriteLongMessage(msg);

            Terminal.Clear();
            Terminal.Refresh();
        }

        public void Dispose()
        {
            Console.WriteLine("Flag");
            
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
}
