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
  IDENTIFIER, STRING, NUMBER,
  COND, GIVE, SAY, PICK, SET,
  AND, OR,
  EQ, NEQ, LT, LTE, GT, GTE, ELSE,
  TRUE, FALSE,
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
      case '=':
        AddToken(TokenType.EQ, "=");
        break;
      case '!':
        if (!Match('='))
          throw new Exception("Unknown operator !. Missing an =?");
        AddToken(TokenType.NEQ, "!=");
        break;
      case '<':
        AddToken(Match('=') ? TokenType.LTE : TokenType.LT, "");
        break;
      case '>':
        AddToken(Match('=') ? TokenType.GTE : TokenType.GT, "");
        break;
      case '"':
        String();
        break;
      case ';':
        // We're at a comment so skip to the end of the line
        while (Peek() != '\n' && !IsAtEnd())
          Advance();
        break;
      case ' ':
      case '\r':
      case '\t':
      case '\n':
        break;
      default:
        if (IsAlpha(c))
          Identifier();
        else if (IsDigit(c))
          Number();
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
      "say" => TokenType.SAY,
      "pick" => TokenType.PICK,
      "give" => TokenType.GIVE,
      "set" => TokenType.SET,
      "true" => TokenType.TRUE,
      "false" => TokenType.FALSE,
      "else" => TokenType.ELSE,
      "cond" => TokenType.COND,
      "and" => TokenType.AND,
      "or" => TokenType.OR,
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

  private void Number()
  {
    while (true)
    {
      char ch = Peek();
      if (IsDigit(ch))
        Advance();
      else
        break;
    }

    var sb = new StringBuilder();
    for (int j = Start; j < Current; j++)
    {      
      sb.Append(Source[j]);
    }
    
    AddToken(TokenType.NUMBER, sb.ToString());
  }

  void AddToken(TokenType type, string text) => Tokens.Add(new(type, text));

  char Peek() => IsAtEnd() ? '\0' : Source[Current];
  char Advance() => Source[Current++];
  bool IsAtEnd() => Current >= Source.Length;
  static bool IsAlpha(char c) => char.IsLetter(c) || c == '_';
  static bool IsDigit(char c) => char.IsAsciiDigit(c);

  private bool Match(char expected)
  {
    if (IsAtEnd())
      return false;
    if (Source[Current] != expected)
      return false;

    Current++;

    return true;
  }
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
      TokenType.SAY => SayExpr(),
      TokenType.SET => SetExpr(),
      TokenType.GIVE => GiveExpr(),
      TokenType.PICK => PickExpr(),
      TokenType.EQ => BooleanExpr(),
      TokenType.NEQ => BooleanExpr(),
      TokenType.LT => BooleanExpr(),
      TokenType.LTE => BooleanExpr(),
      TokenType.GT => BooleanExpr(),
      TokenType.GTE => BooleanExpr(),
      TokenType.COND => CondExpr(),
      TokenType.AND => AndExpr(),
      TokenType.OR => OrExpr(),
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

  ScriptAnd AndExpr()
  {
    Consume(TokenType.AND);
    List<ScriptExpr> conditions = [];

    do
    {
      if (IsAtEnd())
        throw new Exception("Unterminated and.");

      ScriptExpr expr = Expr();
      conditions.Add(expr);
    }
    while (!Check(TokenType.RIGHT_PAREN));
    Consume(TokenType.RIGHT_PAREN);

    return new ScriptAnd(conditions);
  }

  ScriptOr OrExpr()
  {
    Consume(TokenType.OR);
    List<ScriptExpr> conditions = [];

    do
    {
      if (IsAtEnd())
        throw new Exception("Unterminated and.");

      ScriptExpr expr = Expr();
      conditions.Add(expr);
    }
    while (!Check(TokenType.RIGHT_PAREN));
    Consume(TokenType.RIGHT_PAREN);

    return new ScriptOr(conditions);
  }

  ScriptCond CondExpr()
  {
    Consume(TokenType.COND);
    List<ScriptBranch> branches = [];

    ScriptBranch? elseClause = null;
    do
    {
      if (IsAtEnd())
        throw new Exception("Unterminated conditional.");

      Consume(TokenType.LEFT_PAREN);

      if (Peek().Type == TokenType.ELSE)
      {
        Advance();

        if (elseClause is not null)
          throw new Exception("Cannot have multiple else clauses in cond.");
        
        elseClause = new ScriptBranch(new ScriptBool(true), Expr());
        Consume(TokenType.RIGHT_PAREN);
      }
      else 
      {
        ScriptBranch branch = new(Expr(), Expr());
        branches.Add(branch);
        Consume(TokenType.RIGHT_PAREN);
      }      
    }
    while (!Check(TokenType.RIGHT_PAREN));
    Consume(TokenType.RIGHT_PAREN);

    if (elseClause is not null)
      branches.Add(elseClause);

    return new ScriptCond(branches);
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

  ScriptBooleanExpr BooleanExpr()
  {
    ScriptToken op = Peek();
    Advance();

    if (!Check(TokenType.IDENTIFIER))
      throw new Exception("Expected identifier in boolean expression");
    
    ScriptLiteral lit = (ScriptLiteral)Expr();
    ScriptExpr val = Expr();
    if (val is not ScriptAtomic atomic)
      throw new Exception("Expected value in boolean expression");
    
    Consume(TokenType.RIGHT_PAREN);

    return new ScriptBooleanExpr(op, lit.Name, atomic);
  }

  ScriptSet SetExpr()
  {
    Consume(TokenType.SET);
    
    if (!Check(TokenType.IDENTIFIER))
      throw new Exception("Expected identifier in Set expression");

    ScriptLiteral lit = (ScriptLiteral)Expr();    
    ScriptExpr val = Expr();
    
    Consume(TokenType.RIGHT_PAREN);

    return new ScriptSet(lit.Name, val);
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
      case TokenType.TRUE:
        Advance();
        return new ScriptBool(true);
      case TokenType.FALSE:
        Advance();
        return new ScriptBool(false);
      case TokenType.NUMBER:
        int number = int.Parse(Tokens[Current].Lexeme);
        Advance();
        return new ScriptNumber(number);
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
abstract class ScriptAtomic : ScriptExpr { }

class ScriptList : ScriptExpr
{
  public List<ScriptExpr> Items = [];
}

class ScriptBooleanExpr(ScriptToken op, string identifier, ScriptAtomic val) : ScriptExpr
{
  public ScriptToken Op { get; set; } = op;
  public string Identifier { get; set; } = identifier;
  public ScriptAtomic Value { get; set; } = val;
}

class ScriptPick(ScriptList list) : ScriptExpr
{
  public ScriptList List { get; set; } = list;
}

class ScriptSay(ScriptExpr dialogue) : ScriptExpr
{
  public ScriptExpr Dialogue { get; set; } = dialogue;
}

class ScriptBranch(ScriptExpr test, ScriptExpr action) : ScriptExpr
{
  public ScriptExpr Test { get; set; } = test;
  public ScriptExpr Action { get; set; } = action;
}

class ScriptAnd(List<ScriptExpr> conditions) : ScriptExpr
{
  public List<ScriptExpr> Conditions { get; set; } = conditions;
}

class ScriptOr(List<ScriptExpr> conditions) : ScriptExpr
{
  public List<ScriptExpr> Conditions { get; set; } = conditions;
}

class ScriptCond(List<ScriptBranch> branches) : ScriptExpr
{
  public List<ScriptBranch> Branches { get; set; } = branches;
}

class ScriptGive(string gift, string blurb) : ScriptExpr
{
  public string Gift { get; set; } = gift;
  public string Blurb { get; set; } = blurb;
}

class ScriptSet(string name, ScriptExpr val) : ScriptExpr
{
  public string Name { get; set; } = name;
  public ScriptExpr Value { get; set; } = val;
}

class ScriptLiteral(string name) : ScriptAtomic
{
  public string Name { get; set; } = name;
}

class ScriptString(string val) : ScriptAtomic
{
  public string Value { get; set; } = val;
}

class ScriptBool(bool val) : ScriptAtomic
{
  public bool Value { get; set; } = val;
}

class ScriptNumber(int val) : ScriptAtomic
{
  public int Value { get; set; } = val;
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
    var txt = File.ReadAllText($"dialogue/{filename}");
    var scanner = new ScriptScanner(txt);
    var tokens = scanner.ScanTokens();
    var parser = new ScriptParser(tokens);
    Script = parser.Parse();

    Sb = new StringBuilder();
  }

  object CheckVal(string name, Actor mob, GameState gs)
  {
    if (name == "MET_PLAYER")
    {
      if (mob.Stats.TryGetValue(Attribute.MetPlayer, out var stat))
        return stat.Curr != 0;
      else 
        return false;
    }
    else if (name == "PLAYER_DEPTH")
    {
      return gs.Player.Stats[Attribute.Depth].Max;
    }

    throw new Exception($"Unknown variable {name}");
  }

  static string DoMadLibs(string s, GameState gs)
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

    if (Expr is ScriptBooleanExpr bExpr)
    {
      return EvallBooleanExpr(bExpr, mob, gs);
    }
    else if (Expr is ScriptCond condExpr)
    {
      EvalCond(condExpr, mob, gs);
    }
    else if (Expr is ScriptAnd andExpr)
    {
      return EvalAnd(andExpr, mob, gs);
    }
    else if (Expr is ScriptOr orExpr)
    {
      return EvalOr(orExpr, mob, gs);
    }
    else if (Expr is ScriptString str)
    {
      string s = DoMadLibs(str.Value, gs);

      result = new ScriptString(s);
    }
    else if (Expr is ScriptBool boolean)
    {
      return boolean;
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
    else if (Expr is ScriptGive gift)
    {
      EvalGive(gift, mob, gs);
    }
    else if (Expr is ScriptSet set)
    {
      EvalSet(set, mob, gs);
    }
    else if (Expr is ScriptList list)
    {
      foreach (var item in list.Items)
        Eval(item, mob, gs);
    }
    
    return result;
  }

  static bool CompareBool(ScriptToken op, bool a, ScriptExpr b)
  {
    if (b is not ScriptBool c)
      throw new Exception("Expected boolean value");

    return op.Type switch
    {
      TokenType.EQ => a == c.Value,
      TokenType.NEQ => a != c.Value,
      _ => throw new Exception("Invalid comparison")
    };
  }

  static bool CompareStr(ScriptToken op, string a, ScriptExpr b)
  {
    if (b is not ScriptString s)
      throw new Exception("Expected string value");

    return op.Type switch
    {
      TokenType.EQ => a == s.Value,
      TokenType.NEQ => a != s.Value,
      _ => throw new Exception("Invalid comparison")
    };
  }

  static bool CompareInt(ScriptToken op, int a, ScriptExpr b)
  {
    if (b is not ScriptNumber n)
      throw new Exception("Expected int value");

    return op.Type switch
    {
      TokenType.EQ => a == n.Value,
      TokenType.NEQ => a != n.Value,
      TokenType.LT => a < n.Value,
      TokenType.LTE => a <= n.Value,
      TokenType.GT => a > n.Value,
      TokenType.GTE => a >= n.Value,
      _ => throw new Exception("Invalid comparison")
    };
  }

  ScriptBool EvallBooleanExpr(ScriptBooleanExpr expr, Actor mob, GameState gs)
  {
    object val = CheckVal(expr.Identifier, mob, gs);

    if (val is bool boolVal)
      return new ScriptBool(CompareBool(expr.Op, boolVal, expr.Value));
    else if (val is int intVal)
      return new ScriptBool(CompareInt(expr.Op, intVal, expr.Value));
    else if (val is string strVal)
      return new ScriptBool(CompareStr(expr.Op, strVal, expr.Value));
    
    throw new Exception("Variables must be int, bool, or string");
  }

  // At the moment, I only have on/off variables 
  // but I imagine that'll change 
  void EvalSet(ScriptSet set, Actor mob, GameState gs)
  {
    switch (set.Name)
    {
      case "MET_PLAYER":
        ScriptExpr result = Eval(set.Value, mob, gs);
        if (result is not ScriptBool boolean)
          throw new Exception("Expected boolean value for setting MET_PLAYER");

        int val = boolean.Value ? 1 : 0;
        if (mob.Stats.TryGetValue(Attribute.MetPlayer, out var stat))
          stat.SetMax(val);
        else
          mob.Stats.Add(Attribute.MetPlayer, new Stat(val));
        break;
      default:
        throw new Exception($"Unknown variable: {set.Name}");
    }
  }

  void EvalGive(ScriptGive gift, Actor mob, GameState gs)
  {
      Item item = gift.Gift switch
      {
        "MINOR_GIFT" => Treasure.MinorGift(gs.ObjDb, gs.Rng),
        _ => throw new Exception($"Unknown variable: {gift.Gift}"),
      };
      
      Sb.Append("\n\n");
      Sb.Append(gift.Blurb);
      Sb.Append("\n\n");
      Sb.Append(mob.FullName.Capitalize());
      Sb.Append(" gives you ");
      Sb.Append(item.Name.IndefArticle());
      Sb.Append('!');

      gs.Player.Inventory.Add(item, gs.Player.ID);
  }

  ScriptBool EvalAnd(ScriptAnd andExpr, Actor mob, GameState gs)
  {
    if (andExpr.Conditions.Count == 0)
      throw new Exception("And expressions must have at least one condition.");

    foreach (ScriptExpr cond in andExpr.Conditions)
    {
      ScriptExpr result = Eval(cond, mob, gs);
      if (result is ScriptBool boolResult && !boolResult.Value)
      {
        return boolResult;
      }
      else
      {
        throw new Exception("Expected boolean condition in and expression.");
      }
    }

    return new ScriptBool(true);
  }

  ScriptBool EvalOr(ScriptOr orExpr, Actor mob, GameState gs)
  {
    if (orExpr.Conditions.Count == 0)
      throw new Exception("Or expressions must have at least one condition.");

    foreach (ScriptExpr cond in orExpr.Conditions)
    {
      ScriptExpr result = Eval(cond, mob, gs);
      if (result is not ScriptBool boolResult)
      {
        throw new Exception("Expected boolean condition in or expression.");
      }
      else if (boolResult.Value)
      {
        return boolResult;        
      }
    }

    return new ScriptBool(false);
  }

  void EvalCond(ScriptCond cond, Actor mob, GameState gs)
  {
    if (cond.Branches.Count == 0)
      throw new Exception("cond expressions must have at least one branch.");

      foreach (ScriptBranch branch in cond.Branches)
      {
        ScriptExpr expr = Eval(branch.Test, mob, gs);
        if (expr is not ScriptBool result)
          throw new Exception("Expected boolean result in cond test.");
        if (result.Value)
        {
          Eval(branch.Action, mob, gs);
          break;
        }
      }
  }

  public string Dialogue(Actor mob, GameState gs)
  {
    Eval(Script.Script, mob, gs);
    
    return Sb.ToString();
  }
}
