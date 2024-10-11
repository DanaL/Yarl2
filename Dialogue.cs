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

enum TokenType
{
  LEFT_PAREN, RIGHT_PAREN, 
  IDENTIFIER, STRING,
  IF, GIVE, SAY, PICK, SET,
  EOF
}

class ScriptToken(TokenType type, string lexeme)
{
  public TokenType Type { get; set; } = type;
  public string Lexeme { get; set; } = lexeme;

  public override string ToString() => $"{Type} {Lexeme}";
}

class ScriptScanner(string src)
{
  readonly string Source = src;
  readonly List<ScriptToken> Tokens = [];
  int Start;
  int Current;

  public List<ScriptToken> ScanTokens()
  {
    while (!IsAtEnd())
    {
      Start = Current;
      ScanToken();
    }
    Tokens.Add(new ScriptToken(TokenType.EOF, ""));

    return Tokens;
  }

  void ScanToken()
  {
    char c = Advance();

    switch (c)
    {
      case '(':
        AddToken(TokenType.LEFT_PAREN, "(");
        break;
      case ')':
        AddToken(TokenType.RIGHT_PAREN, ")");
        break;
      case '"':
        String();
        break;
      case ' ':
      case '\r':
      case '\t':
      case '\n':
        break;
      default:
        if (IsAlpha(c))
          Identifier();
        else
          throw new Exception($"Unexpected character: {c}.");
        break;
    }
  }

  void Identifier()
  {
    while (IsAlpha(Peek()))
      Advance();

    string text = Source[Start..Current];
    TokenType type = text.ToLower() switch
    {
      "if" => TokenType.IF,
      "say" => TokenType.SAY,
      "pick" => TokenType.PICK,
      "give" => TokenType.GIVE,
      "set" => TokenType.SET,
      _ => TokenType.IDENTIFIER
    };

    AddToken(type, text);
  }

  void String()
  {
    while (Peek() != '"' && !IsAtEnd())
    {
      Advance();
    }

    if (IsAtEnd())
      throw new Exception("Unterminated string.");

    // Skip closing quote
    Advance();

    string? value = Source.Substring(Start + 1, Current - Start - 2);
    AddToken(TokenType.STRING, value);
  }

  void AddToken(TokenType type, string text) => Tokens.Add(new(type, text));

  char Peek() => IsAtEnd() ? '\0' : Source[Current];
  char Advance() => Source[Current++];
  bool IsAtEnd() => Current >= Source.Length;
  static bool IsAlpha(char c) => char.IsLetter(c) || c == '_';  
}

class ScriptParser(List<ScriptToken> tokens)
{
  readonly List<ScriptToken> Tokens = tokens;
  int Current;

  public DialogueScript Parse()
  {
    ScriptExpr script = NonAtomic();

    return new DialogueScript(script);
  }

  ScriptExpr NonAtomic()
  {
    Consume(TokenType.LEFT_PAREN);

    return Peek().Type switch
    {
      TokenType.IF => IfExpr(),
      TokenType.SAY => SayExpr(),
      TokenType.SET => SetExpr(),
      TokenType.GIVE => GiveExpr(),
      TokenType.PICK => PickExpr(),
      _ => ListExpr(),
    };
  }

  ScriptList ListExpr()
  {
    var list = new ScriptList();
    do
    {
      ScriptExpr expr = Expr();
      list.Items.Add(expr);
    }
    while (!Check(TokenType.RIGHT_PAREN) && !Check(TokenType.EOF));
    Consume(TokenType.RIGHT_PAREN);

    return list;
  }

  ScriptIf IfExpr()
  {
    Consume(TokenType.IF);

    if (!Check(TokenType.IDENTIFIER))
      throw new Exception($"Expected literal in if expression");

    string invariant = Tokens[Current].Lexeme;
    Advance();
    ScriptExpr exprTrue = Expr();
    ScriptExpr exprFalse = Expr();

    Consume(TokenType.RIGHT_PAREN);

    return new ScriptIf(invariant, exprTrue, exprFalse);
  }

  ScriptSay SayExpr()
  {
    Consume(TokenType.SAY);
    ScriptExpr expr = Expr();
    Consume(TokenType.RIGHT_PAREN);
    return new ScriptSay(expr);
  }

  ScriptPick PickExpr()
  {
    Consume(TokenType.PICK);
    ScriptExpr expr = NonAtomic();
    Consume(TokenType.RIGHT_PAREN);

    if (expr is ScriptList list)
      return new ScriptPick(list);
    
    throw new Exception("Expected list in Pick expression");    
  }

  ScriptSet SetExpr()
  {
    Consume(TokenType.SET);
    // At the moment, my variables are just on/off
    if (!Check(TokenType.IDENTIFIER))
      throw new Exception("Expected identifier in Set expression");
    ScriptLiteral lit = (ScriptLiteral)Expr();
    Consume(TokenType.RIGHT_PAREN);

    return new ScriptSet(lit.Name);
  }

  ScriptGive GiveExpr()
  {
    Consume(TokenType.GIVE);

    if (!Check(TokenType.IDENTIFIER))
      throw new Exception("Expected identifier in gift expression");
    string gift = Tokens[Current].Lexeme;
    Advance();

    if (!Check(TokenType.STRING))
      throw new Exception("Expected blurb in gift expression");
    string blurb = Tokens[Current].Lexeme;
    Advance();
    
    Consume(TokenType.RIGHT_PAREN);

    return new ScriptGive(gift, blurb);
  }

  ScriptExpr Expr()
  {    
    switch (Peek().Type)
    {
      case TokenType.LEFT_PAREN:
        return NonAtomic();      
      case TokenType.STRING:
        string val = Tokens[Current].Lexeme;
        Advance();
        return new ScriptString(val);
      case TokenType.IDENTIFIER:
        string name = Tokens[Current].Lexeme;
        Advance();
        return new ScriptLiteral(name);
      default:
        throw new Exception($"Unexpected token: {Peek().Type}");
    }   
  }

  ScriptToken Advance()
  {
    if (!IsAtEnd())
      ++Current;
    return Previous();
  }

  ScriptToken Consume(TokenType type)
  {
    if (Check(type))
      return Advance();

    throw new Exception($"Expected token: {type}");
  }

  bool Check(TokenType type)
  {
    if (IsAtEnd())
      return false;
    return Peek().Type == type;
  }

  ScriptToken Peek() => Tokens[Current];
  bool IsAtEnd() => Peek().Type == TokenType.EOF;
  ScriptToken Previous() => Tokens[Current - 1];
}

abstract class ScriptExpr { }

class ScriptList : ScriptExpr
{
  public List<ScriptExpr> Items = [];
}

class ScriptIf(string invariant, ScriptExpr left, ScriptExpr right) : ScriptExpr
{
  public string Invariant {get; set; } = invariant;
  public ScriptExpr Left { get; set; } = left;
  public ScriptExpr Right { get; set; } = right;
}

class ScriptPick(ScriptList list) : ScriptExpr
{
  public ScriptList List { get; set; } = list;
}

class ScriptSay(ScriptExpr dialogue) : ScriptExpr
{
  public ScriptExpr Dialogue { get; set; } = dialogue;
}

class ScriptGive(string gift, string blurb) : ScriptExpr
{
  public string Gift { get; set; } = gift;
  public string Blurb { get; set; } = blurb;
}

class ScriptSet(string name) : ScriptExpr
{
  public string Name { get; set; } = name;
}

class ScriptLiteral(string name) : ScriptExpr
{
  public string Name { get; set; } = name;
}

class ScriptString(string val) : ScriptExpr
{
  public string Value { get; set; } = val;
}

class ScriptVoid : ScriptExpr { }

class DialogueScript(ScriptExpr script)
{
  public ScriptExpr Script { get; set; } = script;
}

class DialogueLoader
{
  DialogueScript Script { get; set; }
  StringBuilder Sb { get; set; }

  public DialogueLoader(string filename)
  {
    var txt = File.ReadAllText($"data/{filename}");
    var scanner = new ScriptScanner(txt);
    var tokens = scanner.ScanTokens();
    var parser = new ScriptParser(tokens);
    Script = parser.Parse();

    Sb = new StringBuilder();
  }

  int CheckVal(string name, Actor mob, GameState gs)
  {
    if (name.Equals("met_player", StringComparison.CurrentCultureIgnoreCase))
    {
      if (mob.Stats.TryGetValue(Attribute.MetPlayer, out var stat))
        return stat.Curr;
      else 
        return 0;
    }

    throw new Exception($"Unknonw variable {name}");
  }

  string DoMadLibs(string s, GameState gs)
  {
    if (s.Contains("#TOWN_NAME"))
    {
      s = s.Replace("#TOWN_NAME", gs.Town.Name);
    }

    if (s.Contains("#EARLY_DENIZEN"))
    {
      string monsters = "";
      foreach (SimpleFact fact in gs.Facts.OfType<SimpleFact>())
      {
        if (fact.Name == "EarlyDenizen")
        {
          monsters = fact.Value;
          break;
        }
      }

      s = s.Replace("#EARLY_DENIZEN", monsters.Pluralize());
    }

    return s;
  }

  ScriptExpr Eval(ScriptExpr Expr, Actor mob, GameState gs)
  {
    ScriptExpr result = new ScriptVoid();

    if (Expr is ScriptIf ifExpr)
    {
      EvalIf(ifExpr, mob, gs);
    }
    else if (Expr is ScriptString str)
    {
      string s = DoMadLibs(str.Value, gs);

      result = new ScriptString(s);
    }
    else if (Expr is ScriptSay say)
    {
      ScriptExpr sayResult = Eval(say.Dialogue, mob, gs);
      if (sayResult is ScriptString s)
        Sb.Append(s.Value);
      else
        throw new Exception("Expected string value");
    }
    else if (Expr is ScriptPick pick)
    {
      var opts = pick.List.Items;
      ScriptExpr e = opts[gs.Rng.Next(opts.Count)];
      return Eval(e, mob, gs);
    }
    else if (Expr is ScriptGive give)
    {
      Item item = give.Gift switch
      {
        "MINOR_GIFT" => Treasure.MinorGift(gs.ObjDb, gs.Rng),
        _ => throw new Exception($"Unknown variable: {give.Gift}"),
      };
      
      Sb.Append("\n\n");
      Sb.Append(give.Blurb);
      Sb.Append("\n\n");
      Sb.Append(mob.FullName.Capitalize());
      Sb.Append(" gives you ");
      Sb.Append(item.Name.IndefArticle());
      Sb.Append('!');

      gs.Player.Inventory.Add(item, gs.Player.ID);
    }

    return result;
  }

  void EvalIf(ScriptIf expr, Actor mob, GameState gs)
  {
    int val = CheckVal(expr.Invariant, mob, gs);
    if (val == 1)
      Eval(expr.Left, mob, gs);
    else
      Eval(expr.Right, mob, gs);
  }

  public string Dialogue(Actor mob, GameState gs)
  {
    Eval(Script.Script, mob, gs);
    
    return Sb.ToString();
  }
}
