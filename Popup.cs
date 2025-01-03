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

class LineScanner(string line)
{
  readonly string Source = line;
  readonly List<(Colour, string)> Words = [];
  int Start;
  int Current;
  Colour CurrentColour = Colours.WHITE;
  
  public List<(Colour, string)> Scan()
  {
    while (!IsAtEnd())
    {
      Start = Current;
      Tokenize();
    }

    return Words;
  }

  void Tokenize()
  {
    char c = Advance();
    switch (c)
    {
      case '[':
        ScanColour();
        break;
      case ' ':
        break;
      case '\n':
        Words.Add((CurrentColour, "\n"));
        break;
      case '\\':
        if (Peek() == 'n')
        {
          Words.Add((CurrentColour, "\n"));
          Advance();
        }
        else
        {
          Words.Add((CurrentColour, c.ToString()));
        }
        break;        
      case ']':
        CurrentColour = Colours.WHITE;
        break;
      default:
        ScanWord();
        break;
      
    }
  }

  void ScanColour()
  {
    Start = Current;
    while (char.IsLetterOrDigit(Peek()))
      Advance();
    CurrentColour = Colours.TextToColour(Source[Start..Current].ToLower());
  }

  void ScanWord()
  {
    while (!IsAtEnd() && Peek() != '[' && Peek() != ']' && !char.IsWhiteSpace(Peek()))
    {
      Advance();
    }

    string word = Source[Start..Current];
    if (Peek() == ' ') 
    {
      word += ' ';
      Advance();
    }
    else if (Peek() == ']' && Peeek() == ' ')
    {
      word += ' ';
    }

    Words.Add((CurrentColour, word));
  }

  char Peek() => IsAtEnd() ? '\0' : Source[Current];
  char Peeek()
  {
    if (Current + 1 >= Source.Length)
      return '\0';

    return Source[Current + 1];
  }

  char Advance() => Source[Current++];
  bool IsAtEnd() => Current >= Source.Length;
}

interface IPopup
{
  void Draw(UserInterface ui);
  bool FullWidth { get; set; }
  void SetDefaultTextColour(Colour colour);
}

class Popup : IPopup
{
  Colour DefaultTextColour { get; set; } = Colours.WHITE;
  readonly string Title;
  readonly List<(Colour, string)> Words;
  readonly int Width;
  readonly int PreferredRow;
  readonly int PreferredCol;
  public bool FullWidth { get; set; }

  public Popup(string message, string title, int preferredRow, int preferredCol, int width = -1)
  {
    int widthGuess = GuessWidth(message);
    Title = title;
    if (width > 0)
      Width = width + 4;
    else if (widthGuess == 0 || title.Length > widthGuess)
      Width = title.Length + 6;
    else if (widthGuess < UserInterface.ViewWidth - 4)
      Width = widthGuess + 4;
    else
      Width = UserInterface.ViewWidth - 4;

    LineScanner scanner = new(message);
    Words = scanner.Scan();
        
    // preferredRow is the ideal row for the bottom of the box <-- this is dumb!
    // and the preferredCol is the ideal col for the centre of it
    PreferredRow = preferredRow;
    PreferredCol = preferredCol;
  }

  static int GuessWidth(string message) => message.Split('\n').Select(line => line.Length).Max();

  public void Draw(UserInterface ui)
  {
    int col, row;
    if (PreferredCol == -1 && FullWidth)
      col = (UserInterface.ScreenWidth - Width) / 2;
    else if (PreferredCol == -1)
      col = (UserInterface.ViewWidth - Width) / 2;
    else
      col = PreferredCol - (Width / 2);
    if (PreferredRow == -1)
      row = 5;
    else
      row = PreferredRow + 3;
    
    if (row < 0)
      row = PreferredRow + 3;
    if (col < 0)
      col = 0;

    string border = "+".PadRight(Width - 1, '-') + "+";

    if (Title.Length > 0)
    {
      int left = int.Max(2, (Width - Title.Length) / 2 - 2);
      string title = "+".PadRight(left, '-') + ' ';
      title += Title + ' ';
      title = title.PadRight(Width - 1, '-') + "+";
      ui.WriteLine(title, row++, col, Width, DefaultTextColour);
    }
    else
    {
      ui.WriteLine(border, row++, col, Width, DefaultTextColour);
    }

    int currWidth = 0;
    int w = 0;
    List<(Colour, string)> line = [(DefaultTextColour, "| ")];

    while (w < Words.Count)
    {
      var (colour, word) = Words[w++];

      if (word == "\r")
        continue;

      if (word == "\n")
      {
        WritePaddedLine();
      }
      else if (word.Length < Width - currWidth - 2)
      {
        line.Add((colour, word));
        currWidth += word.Length;
      }
      else
      {
        --w;
        WritePaddedLine();
      }
    }

    WritePaddedLine();

    ui.WriteLine(border, row, col, Width, DefaultTextColour);

    void WritePaddedLine()
    {
      // Calculate total width of all existing content
      int actualWidth = line.Sum(tuple => tuple.Item2.Length);
      
      // Pad out so that the right border lines up
      int padding = Width - actualWidth;
      if (padding > 0)
        line.Add((DefaultTextColour, "|".PadLeft(padding, ' ')));
      ui.WriteText(line, row++, col, Width);
      line = [(DefaultTextColour, "| ")];
      currWidth = 0;
    }
  }

  public void SetDefaultTextColour(Colour colour) => DefaultTextColour = colour;
}

class PopupMenu(string title, List<string> menuItems) : IPopup
{
  public int SelectedRow { get; set; } = 0;
  public bool FullWidth { get; set; }
  Colour DefaultTextColour { get; set; } = Colours.WHITE;
  string Title { get; set; } = title;
  List<string> MenuItems { get; set; } = menuItems;
  

  void IPopup.Draw(UserInterface ui)
  {
    int width = MenuItems.Select(i => i.Length).Max() + 4;
    if (Title.Length + 4 > width)
      width = Title.Length + 5;

    int col = (UserInterface.ViewWidth - width) / 2;
    int row = 2;
    
    string border = "+".PadRight(width - 1, '-') + "+";

    if (Title.Length > 0)
    {
      int left = int.Max(2, (width - Title.Length) / 2 - 2);
      string title = "+".PadRight(left, '-') + ' ';
      title += Title + ' ';
      title = title.PadRight(width - 1, '-') + "+";
      ui.WriteLine(title, row++, col, width, DefaultTextColour);
    }
    else
    {
      ui.WriteLine(border, row++, col, width, DefaultTextColour);
    }

    for (int i = 0; i < MenuItems.Count; i++)
    {
      string item = MenuItems[i];
      if (i == SelectedRow)
      {
        // Mild kludge: HILITE makes the tile transparent, so write a black background
        // before we draw highlighted line
        ui.WriteLine($"| {" ".PadRight(width - 4)} |", row, col, width, DefaultTextColour);
        ui.WriteLine($"{item.PadRight(width - 8)}", row++, col + 2, width - 4, DefaultTextColour, Colours.HILITE);
      }
      else
      {
        ui.WriteLine($"| {item.PadRight(width - 4)} |", row++, col, width, DefaultTextColour);
      }
    }
    

    ui.WriteLine(border, row, col, width, DefaultTextColour);
  }

  public void SetDefaultTextColour(Colour colour) => DefaultTextColour = colour;
}