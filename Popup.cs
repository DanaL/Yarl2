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

using System.Text;

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
    while (!IsAtEnd() && Peek() != ']' && !char.IsWhiteSpace(Peek()))
      Advance();
    Words.Add((CurrentColour, Source[Start..Current]));
  }

  char Peek() => IsAtEnd() ? '\0' : Source[Current];
  char Advance() => Source[Current++];
  bool IsAtEnd() => Current >= Source.Length;
}

class Popup
{
  Colour DefaultTextColour { get; set; } = Colours.WHITE;
  readonly string Title;
  List<(Colour, string)> Words;
  int Width;
  readonly int PreferredRow;
  readonly int PreferredCol;
    
  public Popup(string message, string title, int preferredRow, int preferredCol, int width = -1)
  {
    Title = title;
    Width = width != -1 ? width + 4 : UserInterface.ViewWidth - 4;

    LineScanner scanner = new(message);
    Words = scanner.Scan();
        
    // preferredRow is the ideal row for the bottom of the box
    // and the preferredCol is the ideal col for the centre of it
    PreferredRow = preferredRow;
    PreferredCol = preferredCol;
  }

  public void Draw(UserInterface ui)
  {
    int col, row;
    if (PreferredCol == -1)
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
      int left = (Width - Title.Length) / 2 - 2;
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

    void WritePaddedLine()
    {
      // Pad out so that the right border lines up        
      int padding = Width - currWidth - 2;
      if (padding > 0)
        line.Add((DefaultTextColour, "|".PadLeft(padding, ' ')));
      ui.WriteText(line, row++, col, Width);
      line = [(DefaultTextColour, "| ")];
      currWidth = 0;
    }

    while (w < Words.Count)
    {
      var (colour, word) = Words[w++];

      if (word == "\n")
      {
        WritePaddedLine();
      }
      else if (word.Length <= Width - currWidth - 4)
      {
        currWidth += word.Length;
        if (line.Count > 1 && word[0] != ',')
        {
          word = ' ' + word;
          ++currWidth;
        }
        line.Add((colour, word));
      }
      else
      {
        --w;
        WritePaddedLine();
      }
    }

    WritePaddedLine();

    ui.WriteLine(border, row, col, Width, DefaultTextColour);
  }

  public void SetDefaultTextColour(Colour colour) => DefaultTextColour = colour;
}