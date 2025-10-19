// Delve - A roguelike computer RPG
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
      case '\t':
        Words.Add((CurrentColour, "\t"));
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
      case '_':
        Words.Add((CurrentColour, " "));
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
  void SetDefaultTextColour(Colour colour);
}

class Popup : IPopup
{
  Colour DefaultTextColour { get; set; } = Colours.WHITE;
  readonly string Title;
  List<(Colour, string)> Words;
  readonly int Width;
  readonly int PreferredRow;
  readonly int PreferredCol;

  public string BarLabel { get; set; } = "";
  public int Value1 { get; set; }
  public int Value2 { get; set; }
  public Colour Colour1 { get; set; }
  public Colour Colour2 { get; set;  }
  public int Pages { get; set; } = 1;

  int Page { get; set; } = 0;

  List<List<(Colour, string)>> CalculatedLines = [];

  public Popup(string message, string title, int preferredRow, int preferredCol, int width = -1)
  {
    Words = [];
    Title = title;
    SetText(message);

    int widthGuess = GuessWidth(message);
    if (width > 0)
      Width = width + 4;
    else if (widthGuess == 0 || Title.Length > widthGuess)
      Width = Title.Length + 6;
    else if (widthGuess < UserInterface.ViewWidth - 4)
      Width = widthGuess + 4;
    else
      Width = UserInterface.ViewWidth - 4;

    // preferredRow is the ideal row for the bottom of the box <-- this is dumb!
    // and the preferredCol is the ideal col for the centre of it
    PreferredRow = preferredRow;
    PreferredCol = preferredCol;
  }

  static int GuessWidth(string message) => message.Split('\n').Select(line => line.Length).Max();

  public void SetText(string message)
  {    
    LineScanner scanner = new(message);
    Words = scanner.Scan();
  }

  public void NextPage() => ++Page;

  public void Draw(UserInterface ui)
  {
    CalculatedLines = [];

    int col;
    if (PreferredCol != -1)
      col = PreferredCol - (Width / 2);
    else if (Width < UserInterface.ViewWidth)
      col = (UserInterface.ViewWidth - Width) / 2;
    else
      col = (UserInterface.ScreenWidth - Width) / 2;

    int row;
    if (PreferredRow == -1)
      row = 5;
    else
      row = PreferredRow + 3;
    if (row < 0)
      row = PreferredRow + 3;

    if (col < 0)
      col = 0;

    string border = Constants.TOP_LEFT_CORNER.ToString().PadRight(Width - 1, '─') + Constants.TOP_RIGHT_CORNER;

    List<(Colour, string)> top;
    if (Title.Length > 0)
    {
      int left = int.Max(2, (Width - Title.Length) / 2 - 2);
      string title = Constants.TOP_LEFT_CORNER.ToString().PadRight(left, '─') + ' ';
      title += Title + ' ';
      title = title.PadRight(Width - 1, '─') + Constants.TOP_RIGHT_CORNER;
      top = [(DefaultTextColour, title)];
    }
    else
    {
      top = [(DefaultTextColour, border)];
    }

    int currWidth = 0;
    int w = 0;
    List<(Colour, string)> line = [(DefaultTextColour, "│ ")];

    while (w < Words.Count)
    {
      var (colour, word) = Words[w++];

      if (word == "\r")
        continue;

      if (word == "\n")
      {
        BuildPaddedLine();
      }
      else if (word == "\t")
      {
        line.Add((Colours.BLACK, "   "));
        currWidth += 3;
      }
      else if (word.Length < Width - currWidth - 2)
      {
        line.Add((colour, word));
        currWidth += word.Length;
      }
      else
      {
        --w;
        BuildPaddedLine();
      }
    }

    BuildPaddedLine();

    string blankLine = "│ " + new string(' ', Width - 4) + " │";

    if (BarLabel != "")
    {      
      CalculatedLines.Add([(Colours.WHITE, blankLine)]);
      int barWidth = Width - BarLabel.Length - 7;
      List<(Colour, string)> barLine = [(Colours.WHITE, $"│ {BarLabel} [")];

      double ratio = Value1 / (double)Value2;
      int bar1Len = (int)(barWidth * ratio);
      int bar2Len = barWidth - bar1Len;
      string bar1 = new('█', bar1Len);
      barLine.Add((Colour1, bar1));

      if (bar2Len > 0)
      {
        string bar2 = new('█', bar2Len);
        barLine.Add((Colour2, bar2));
      }

      barLine.Add((Colours.WHITE, "] │"));

      CalculatedLines.Add(barLine);
      CalculatedLines.Add([(Colours.WHITE, blankLine)]);
    }

    int rowsAvailable = UserInterface.ScreenHeight - row - 2;
    int rowsDisplayed = int.Min(rowsAvailable, CalculatedLines.Count);
    bool pageinate = false;
    Pages = CalculatedLines.Count / rowsAvailable + 1;
    if (CalculatedLines.Count >= rowsAvailable)
    {
      pageinate = true;
      rowsDisplayed -= 2;
    }

    int page = Page * rowsDisplayed;
    if (page >= CalculatedLines.Count)
    {
      Page = 0;
      page = 0;
    }

    ui.WriteText(top, row++, col);
    foreach (var l in CalculatedLines.Skip(Page * rowsDisplayed).Take(rowsDisplayed))
    {
      ui.WriteText(l, row++, col);
    }

    if (pageinate)
    {
      ui.WriteText([(DefaultTextColour, blankLine)], row++, col);
      ui.WriteText([(DefaultTextColour, "│ "), (Colours.DARK_GREY, "next page >".PadLeft(Width - 4, ' ')), (DefaultTextColour, " │")], row++, col);
    }

    string bottomBorder = Constants.BOTTOM_LEFT_CORNER.ToString().PadRight(Width - 1, '─') + Constants.BOTTOM_RIGHT_CORNER.ToString();
    ui.WriteText([(DefaultTextColour, bottomBorder)], row++, col);
    
    void BuildPaddedLine()
    {
      // Calculate total width of all existing content
      int actualWidth = line.Sum(tuple => tuple.Item2.Length);
      
      // Pad out so that the right border lines up
      int padding = Width - actualWidth;
      if (padding > 0)
        line.Add((DefaultTextColour, "│".PadLeft(padding, ' ')));
      CalculatedLines.Add(line);      
      line = [(DefaultTextColour, "│ ")];
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
    
    if (Title.Length > 0)
    {
      int left = int.Max(2, (width - Title.Length) / 2 - 2);
      string title = Constants.TOP_LEFT_CORNER.ToString().PadRight(left, '─') + ' ';
      title += Title + ' ';
      title = title.PadRight(width - 1, '─') + Constants.TOP_RIGHT_CORNER;
      ui.WriteLine(title, row++, col, width, DefaultTextColour);
    }
    else
    {
      string topBorder = Constants.TOP_LEFT_CORNER.ToString().PadRight(width - 1, '─') + Constants.TOP_RIGHT_CORNER;
      ui.WriteLine(topBorder, row++, col, width, DefaultTextColour);
    }

    for (int i = 0; i < MenuItems.Count; i++)
    {
      string item = MenuItems[i];
      if (i == SelectedRow)
      {
        // Mild kludge: HILITE makes the tile transparent, so write a black background
        // before we draw highlighted line
        ui.WriteLine($"│ {" ".PadRight(width - 4)} │", row, col, width, DefaultTextColour);
        ui.WriteLine($"{item.PadRight(width - 8)}", row++, col + 2, width - 4, DefaultTextColour, Colours.HILITE);
      }
      else
      {
        ui.WriteLine($"│ {item.PadRight(width - 4)} │", row++, col, width, DefaultTextColour);
      }
    }

    string bottom = Constants.BOTTOM_LEFT_CORNER.ToString().PadRight(width - 1, '─') + Constants.BOTTOM_RIGHT_CORNER;
    ui.WriteLine(bottom, row, col, width, DefaultTextColour);
  }

  public void SetDefaultTextColour(Colour colour) => DefaultTextColour = colour;
}

class Hint(List<string> text, int row) : IPopup
{
  Colour DefaultTextColour { get; set; } = Colours.WHITE;
  List<string> Text { get; set; } = text;
  int Row { get; set; } = row;

  public void SetDefaultTextColour(Colour colour)
  {
    DefaultTextColour = colour;
  }

  public void Draw(UserInterface ui)
  {
    int widest = Text.Select(s => s.Length).Max();
    int col = (UserInterface.ViewWidth - widest) / 2;

    int row = Row;
    foreach (string s in Text)
    {
      ui.WriteText([(DefaultTextColour, s)], row++, col);
    }
  }  
}

class FullScreenPopup(Sqr[,] sqrs) : IPopup
{
  Sqr[,] Sqrs { get; set; } = sqrs;

  public void Draw(UserInterface ui)
  {
    int height = int.Min(UserInterface.ScreenHeight, Sqrs.GetLength(0));
    int width = int.Min(UserInterface.ScreenWidth, Sqrs.GetLength(1));

    ui.ClearScreen();

    for (int r = 0; r < height; r++)
    {
      for (int c = 0; c < width; c++)
      {
        ui.WriteSq(r, c, Sqrs[r, c]);
      }
    }
  }

  public void SetDefaultTextColour(Colour colour) { }
}

class TwoPanelPopup(string title, List<string> left, string right, char separator) : IPopup
{
  public int Selected { get; set; } = 0;
  
  // My intention is to use this for things wlike the help menu where you have
  // options on the right and then text on the left, so receive the options
  // as a list so I can calc how wide to make the left panel  
  List<string> LeftPanel { get; set; } = left;
  string RightPanel { get; set; } = right;
  char Separator { get; set; } = separator;
  string Title { get; set; } = title;
  int Page { get; set; } = 0;
  List<List<(Colour, string)>> CalculatedRightPanel = [];

  public void Draw(UserInterface ui)
  {
    LineScanner ls;
    int row = 1;
    int col = 1;
    int rows = UserInterface.ScreenHeight - 2;

    List<List<(Colour, string)>> options = [];
    int optionsWidth = 0;
    foreach (string opt in LeftPanel)
    {
      ls = new LineScanner(opt);
      var scanned = ls.Scan();
      int width = scanned.Sum(w => w.Item2.Length);
      if (width > optionsWidth)
        optionsWidth = width;
      options.Add(scanned);
    }

    string blank = " ".PadLeft(UserInterface.ScreenWidth - 2);
    ui.WriteText([(Colours.WHITE, blank)], row++, col);
    string title = Title.PadRight(UserInterface.ScreenWidth - 2);
    ui.WriteText([(Colours.WHITE, title)], row++, col);
    ui.WriteText([(Colours.WHITE, blank)], row++, col);

    int o = 0;
    foreach (var option in options)
    {
      if (o++ == Selected)
        ui.WriteText(option, row, col);
      else
        ui.WriteText([.. option.Select(opt => (Colours.GREY, opt.Item2))], row, col);

      int width = option.Sum(w => w.Item2.Length);
      if (width < optionsWidth)
        ui.WriteText([(Colours.WHITE, " ".PadRight(optionsWidth - width))], row, width + 1);
      ui.WriteText([(Colours.WHITE, $" {Separator} ")], row, optionsWidth + 1);

      ++row;
    }

    string spacer = $"{Separator}".PadLeft(optionsWidth + 2) + " ";
    while (row <= rows)
    {
      ui.WriteText([(Colours.WHITE, spacer)], row++, col);
    }

    CalculatedRightPanel = [];
    ls = new LineScanner(RightPanel);
    List<(Colour, string)> panel2 = ls.Scan();
    int w = 0;
    int rightPanelWidth = UserInterface.ScreenWidth - spacer.Length;
    int currWidth = 0;
    List<(Colour, string)> line = [];
    while (w < panel2.Count)
    {
      var (colour, word) = panel2[w++];

      if (word == "\r")
        continue;

      if (word == "\n")
      {
        BuildPaddedLine();
      }
      else if (word == "\t")
      {
        line.Add((Colours.BLACK, "   "));
        currWidth += 3;
      }
      else if (word.Length < rightPanelWidth - currWidth - 2)
      {
        line.Add((colour, word));
        currWidth += word.Length;
      }
      else
      {
        --w;
        BuildPaddedLine();
      }
    }
    BuildPaddedLine();

    int rightPanelRows = rows - 3;
    row = 4;
    col = spacer.Length + 1;

    bool pageinate = false;
    int pages = CalculatedRightPanel.Count / rightPanelRows + 1;
    if (CalculatedRightPanel.Count >= rightPanelRows)
    {
      pageinate = true;
    }

    int page = Page * rightPanelRows;
    if (page >= CalculatedRightPanel.Count)
    {
      Page = 0;
      page = 0;
    }

    IEnumerable<List<(Colour, string)>> rightPanelLines = [];
    if (!pageinate)
      rightPanelLines = CalculatedRightPanel;
    else
      rightPanelLines = CalculatedRightPanel.Skip(Page * (rightPanelRows - 2)).Take(rightPanelRows - 2);
    
    foreach (var rightLine in rightPanelLines)
    {
      ui.WriteText(rightLine, row++, col);
    }

    if (pageinate)
    {
      ui.WriteText([(Colours.BLACK, "".PadRight(rightPanelWidth))], row++, col);
      ui.WriteText([(Colours.GREY, "next page >".PadLeft(rightPanelWidth - 2))], row++, col);
    }

    while (row < rows)
    {
      ui.WriteText([(Colours.WHITE, "".PadLeft(rightPanelWidth))], row++, col);
    }
    
    void BuildPaddedLine()
    {
      // Calculate total width of all existing content
      int actualWidth = line.Sum(tuple => tuple.Item2.Length);

      // Pad out so that the right border lines up
      int padding = rightPanelWidth - actualWidth;
      if (padding > 0)
        line.Add((Colours.WHITE, "".PadLeft(padding, ' ')));
      CalculatedRightPanel.Add(line);
      line = [];
      currWidth = 0;
    }
  }

  public void NextPage() => ++Page;
  public void SetRightPanel(string txt) => RightPanel = txt;
  public void SetDefaultTextColour(Colour colour) { }
}
