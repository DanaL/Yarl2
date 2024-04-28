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

class Popup
{
  readonly string _title;
  List<List<(Colour, string)>> _pieces;
  int _width, _preferredRow, _preferredCol;

  public Popup(string message, string title, int preferredRow, int preferredCol)
  {
    _title = title;
    _pieces = message.Split('\n').Select(Parse).ToList();

    int maxWidth = UserInterface.ViewWidth - 4;
    int widest = WidestPopupLine(_pieces);
    if (widest >= maxWidth)
      _pieces = ResizePopupLines(_pieces, maxWidth - 4);  
    _width = WidestPopupLine(_pieces);

    // preferredRow is the ideal row for the bottom of the box
    // and the preferredCol is the ideals col for the centre of it
    _preferredRow = preferredRow;
    _preferredCol = preferredCol;
  }

  List<(Colour, string)> SplitPopupPiece((Colour, string) piece, int maxWidth)
  {
    List<(Colour, string)> split = [];

    var sb = new StringBuilder();
    foreach (var word in piece.Item2.Split(' '))
    {
      if (sb.Length + word.Length < maxWidth)
      {
        sb.Append(word);
        sb.Append(' ');
      }
      else
      {
        split.Add((piece.Item1, sb.ToString()));
        sb = new StringBuilder(word);
        sb.Append(' ');
      }
    }
    if (sb.Length > 0)
      split.Add((piece.Item1, sb.ToString()));

    return split;
  }

  // This is going to look ugly if a message contains a long line
  // followed by a line break then short line but I don't know
  // if I'm ever going to need to worry about that in my game.
  List<List<(Colour, string)>> ResizePopupLines(List<List<(Colour, string)>> lines, int maxWidth)
  {
    List<List<(Colour, string)>> resized = [];
    foreach (var line in lines)
    {
      if (PopupLineWidth(line) < maxWidth)
      {
        resized.Add(line);
      }
      else
      {
        Queue<(Colour, string)> q = [];
        foreach (var p in line)
        {
          if (p.Item2.Length < maxWidth)
          {
            q.Enqueue(p);
          }
          else
          {
            foreach (var split in SplitPopupPiece(p, maxWidth))
              q.Enqueue(split);
          }
        }

        List<(Colour, string)> resizedLine = [];
        while (q.Count > 0)
        {
          var curr = q.Dequeue();
          if (PopupLineWidth(resizedLine) + curr.Item2.Length < maxWidth)
          {
            resizedLine.Add(curr);
          }
          else
          {
            resized.Add(resizedLine);
            resizedLine = [curr];
          }
        }
        if (resizedLine.Count > 0)
          resized.Add(resizedLine);
      }
    }

    return resized;
  }

  // I'm sure there is a much cleaner version of this using a stack, but I
  // just want to add some colour to the shopkeeper pop-up menu right now T_T
  List<(Colour, string)> Parse(string line)
  {
    List<(Colour, string)> pieces = [];
    int a = 0, s = 0;
    string txt;
    while (a < line.Length)
    {
      if (line[a] == '[')
      {
        txt = line.Substring(s, a - s);
        if (txt.Length > 0)
          pieces.Add((Colours.WHITE, txt));

        s = a;
        while (line[a] != ' ')
          ++a;
        string colourText = line.Substring(s + 1, a - s - 1).ToLower();
        Colour colour = Colours.TextToColour(colourText);
        s = ++a;
        while (line[a] != ']')
          a++;
        txt = line[s..a];
        pieces.Add((colour, txt));
        s = a + 1;
      }
      ++a;

    }

    txt = line.Substring(s, a - s);
    if (txt.Length > 0)
      pieces.Add((Colours.WHITE, txt));

    return pieces;
  }

  static int PopupLineWidth(List<(Colour, string)> line) => line.Select(p => p.Item2.Length).Sum();

  static int WidestPopupLine(List<List<(Colour, string)>> lines)
  {
    int bufferWidth = 0;
    foreach (var line in lines)
    {
      int length = PopupLineWidth(line);
      if (length > bufferWidth)
        bufferWidth = length;
    }

    return (bufferWidth > 20 ? bufferWidth : 20) + 4;
  }

  public void Draw(UserInterface ui)
  {
    int col, row;
    if (_preferredCol == -1)
      col = (UserInterface.ViewWidth - _width) / 2;
    else
      col = _preferredCol - (_width / 2);
    if (_preferredRow == -1)
      row = 5;
    else
      row = _preferredRow - _pieces.Count;

    if (row < 0)
      row = _preferredRow + 3;
    if (col < 0)
      col = 0;

    string border = "+".PadRight(_width - 1, '-') + "+";

    if (_title.Length > 0)
    {
      int left = (_width - _title.Length) / 2 - 2;
      string title = "+".PadRight(left, '-') + ' ';
      title += _title + ' ';
      title = title.PadRight(_width - 1, '-') + "+";
      ui.WriteLine(title, row++, col, _width, Colours.WHITE);
    }
    else
    {
      ui.WriteLine(border, row++, col, _width, Colours.WHITE);
    }

    foreach (var line in _pieces)
    {
      List<(Colour, string)> lt = [(Colours.WHITE, "| ")];
      lt.AddRange(line);
      var padding = (Colours.WHITE, "".PadRight(_width - PopupLineWidth(line) - 4));
      lt.Add(padding);
      lt.Add((Colours.WHITE, " |"));
      ui.WriteText(lt, row++, col, _width - 4);
    }

    ui.WriteLine(border, row, col, _width, Colours.WHITE);
  }
}