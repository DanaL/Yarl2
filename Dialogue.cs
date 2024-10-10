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

enum TokenType
{
  LEFT_PAREN, RIGHT_PAREN, COMMA,
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
      case ',':
        AddToken(TokenType.COMMA, ",");
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

    if (Peek().Type == TokenType.IF)
      return IfExpr();

    ScriptList list = new();

    do
    {
      ScriptExpr expr = Expr();
      list.Items.Add(expr);

      if (Check(TokenType.COMMA)) 
      {
        Advance();
        continue;
      }
      if (Check(TokenType.RIGHT_PAREN))
      {
        Advance();
        break;
      }

      throw new Exception("Unexpected token");
    }
    while (!IsAtEnd());

    return list;
  }

  ScriptIf IfExpr()
  {
    Consume(TokenType.IF);

    if (!Check(TokenType.IDENTIFIER))
      throw new Exception($"Expected literal in if expression");

    string invariant = Tokens[Current].Lexeme;
    Advance();

    return new ScriptIf(invariant, Expr(), Expr());
  }

  ScriptExpr Expr()
  {
    if (Check(TokenType.LEFT_PAREN))
      return NonAtomic();
    if (Check(TokenType.SAY))
    {
      Consume(TokenType.SAY);
      ScriptExpr expr = Expr();
      Consume(TokenType.RIGHT_PAREN);
      return new ScriptSay(expr);
    }
    if (Check(TokenType.STRING))
    {
      string val = Tokens[Current].Lexeme;
      Advance();
      return new ScriptString(val);
    }

    throw new Exception("Unexpected token");
    //if (Check(TokenType.STRING))
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

class ScriptSay(ScriptExpr dialogue) : ScriptExpr
{
  public ScriptExpr Dialogue { get; set; } = dialogue;
}

class ScriptLiteral(string name) : ScriptExpr
{
  public string Name { get; set; } = name;
}

class ScriptString(string val) : ScriptExpr
{
  public string Value { get; set; } = val;
}

class DialogueScript(ScriptExpr script)
{
  public ScriptExpr Script = script;
}

class DialogueLoader
{

}
