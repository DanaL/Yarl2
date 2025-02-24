// Yarl2 - A roguelike computer RPG
// Written in 2024 by Dana Larose <ywg.dana@gmail.com>
//
// To the extent possible under law, the author(s) have dedicated all copyright
// and related and neighboring rights to this software to the public domain
// worldwide. This software is distributed without any warranty.
//
// You should have received a copy of the CC0 Public Domain Dedication along 
// with this software. If not, f
// see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System.Text;

namespace Yarl2;

enum TokenType
{
  LEFT_PAREN, RIGHT_PAREN, 
  IDENTIFIER, STRING, NUMBER,
  COND, GIVE, OFFER, SAY, PICK, SET,
  AND, OR,
  EQ, NEQ, LT, LTE, GT, GTE, ELSE,
  TRUE, FALSE,
  OPTION, SPEND, END, 
  BLESSINGS, GRANT_CHAMP_BLESSING, GRANT_REAVER_BLESSING,
  GRANT_EMBER_BLESSING, GRANT_TRICKSTER_BLESSSING, GRANT_WINTER_BLESSING,
  SHOP_MENU, SHOP_SELECTION,
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
    while (IsAlpha(Peek()) || IsDigit(Peek()))
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
      "option" => TokenType.OPTION,
      "spend" => TokenType.SPEND,
      "end" => TokenType.END,
      "offer" => TokenType.OFFER,
      "blessings-options" => TokenType.BLESSINGS,
      "grant-champion-blessing" => TokenType.GRANT_CHAMP_BLESSING,
      "grant-reaver-blessing" => TokenType.GRANT_REAVER_BLESSING,
      "ember-blessing" => TokenType.GRANT_EMBER_BLESSING,
      "trickster-blessing" => TokenType.GRANT_TRICKSTER_BLESSSING,
      "winter-blessing" => TokenType.GRANT_WINTER_BLESSING,
      "shop-menu" => TokenType.SHOP_MENU,
      "shop-selection" => TokenType.SHOP_SELECTION,
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
  static bool IsAlpha(char c) => char.IsLetter(c) || c == '_' || c == '-';
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

  public ScriptExpr Parse()
  {    
    return NonAtomic();
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
      TokenType.OPTION => OptionExpr(),
      TokenType.BLESSINGS => BlessingsExpr(),
      TokenType.GRANT_CHAMP_BLESSING => GrantChampionBlessingExpr(),
      TokenType.GRANT_REAVER_BLESSING => GrantReaverBlessingExpr(),
      TokenType.GRANT_EMBER_BLESSING => GrantEmberBlessingExpr(),
      TokenType.GRANT_TRICKSTER_BLESSSING => GrantTricksterBlessingExpr(),
      TokenType.GRANT_WINTER_BLESSING => GrantWinterBlessing(),
      TokenType.SHOP_MENU => ShopMenuExpr(),
      TokenType.SPEND => SpendExpr(),
      TokenType.END => EndExpr(),
      TokenType.OFFER => OfferExpr(),
      _ => ListExpr(),
    };
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

  ScriptOption OptionExpr()
  {
    Consume(TokenType.OPTION);

    if (!Check(TokenType.STRING))
      throw new Exception("Expected text for option.");
    
    string str = Peek().Lexeme;
    Advance();
    
    ScriptExpr expr = Expr();

    if (expr is ScriptAtomic)
      throw new Exception("Cannot have atomic expr as action for option.");
    
    Consume(TokenType.RIGHT_PAREN);

    return new ScriptOption(str, expr);
  }
  
  ScriptShopMenu ShopMenuExpr()
  {
    Consume(TokenType.SHOP_MENU);
    Consume(TokenType.RIGHT_PAREN);

    return new ScriptShopMenu();
  } 

  ScriptBlessings BlessingsExpr()
  {
    Consume(TokenType.BLESSINGS);
    Consume(TokenType.RIGHT_PAREN);

    return new ScriptBlessings();
  }

  ScriptChampionBlessing GrantChampionBlessingExpr()
  {
    Consume(TokenType.GRANT_CHAMP_BLESSING);
    Consume(TokenType.RIGHT_PAREN);

    return new ScriptChampionBlessing();
  }

  ScriptReaverBlessing GrantReaverBlessingExpr()
  {
    Consume(TokenType.GRANT_REAVER_BLESSING);
    Consume(TokenType.RIGHT_PAREN);

    return new ScriptReaverBlessing();
  }

  ScriptEmberBlessing GrantEmberBlessingExpr()
  {
    Consume(TokenType.GRANT_EMBER_BLESSING);
    Consume(TokenType.RIGHT_PAREN);

    return new ScriptEmberBlessing();
  }

  ScriptTricksterBlessing GrantTricksterBlessingExpr()
  {
    Consume(TokenType.GRANT_TRICKSTER_BLESSSING);
    Consume(TokenType.RIGHT_PAREN);

    return new ScriptTricksterBlessing();
  }

  ScriptWinterBlessing GrantWinterBlessing()
  {
    Consume(TokenType.GRANT_WINTER_BLESSING);
    Consume(TokenType.RIGHT_PAREN);

    return new ScriptWinterBlessing();
  }

  ScriptSpend SpendExpr()
  {
    Consume(TokenType.SPEND);

    int amount;
    if (Check(TokenType.NUMBER))
    {
      amount = int.Parse(Peek().Lexeme);
      Advance();
    }
    else
    {
      throw new Exception("Expected number in spend expression");
    }

    Consume(TokenType.RIGHT_PAREN);

    return new ScriptSpend(amount);
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

  ScriptEnd EndExpr()
  {
    Consume(TokenType.END);
    if (!Check(TokenType.STRING))
      throw new Exception("Expected string in End expression.");
    string text = Peek().Lexeme;
    Advance();
    Consume(TokenType.RIGHT_PAREN);

    return new ScriptEnd(text);
  }

  ScriptOffer OfferExpr()
  {
    Consume(TokenType.OFFER);
    if (!Check(TokenType.IDENTIFIER))
      throw new Exception("Expected identifier in Offer expression.");
    string name = Peek().Lexeme;
    Advance();
    Consume(TokenType.RIGHT_PAREN);

    return new ScriptOffer(new ScriptLiteral(name));
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

class ScriptEnd(string text) : ScriptExpr
{
  public string Text { get; set; } = text;
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

class ScriptVoid : ScriptAtomic { }

class ScriptOption(string text, ScriptExpr expr) : ScriptExpr
{
  public string Text { get; set; } = text;
  public ScriptExpr Expr { get; set; } = expr;  
}

class ScriptBlessings : ScriptExpr {}
class ScriptShopMenu : ScriptExpr {}
class ScriptShopSelection(char opt) : ScriptExpr 
{
  public char Choice { get; set; } = opt;
}

class ScriptSpend(int amount) : ScriptExpr
{
  public int Amount { get; set; } = amount;
}

class ScriptChampionBlessing : ScriptExpr {}
class ScriptReaverBlessing : ScriptExpr {}
class ScriptEmberBlessing : ScriptExpr {}
class ScriptTricksterBlessing : ScriptExpr {}
class ScriptWinterBlessing : ScriptExpr {}

class ScriptOffer(ScriptLiteral identifier) : ScriptExpr
{
  public ScriptLiteral Identifier { get; set; } = identifier;
}

record DialogueOption(string Text, char Ch, ScriptExpr Expr);

class DialogueInterpreter
{
  public List<DialogueOption> Options = [];
  StringBuilder Sb { get; set; } = new();
  StringBuilder Footer { get; set; } = new();
  public DialogueInterpreter() { }

  public (string, string) Run(string filename, Actor mob, GameState gs)
  {
    string path = ResourcePath.GetDialogueFilePath(filename);
    var txt = File.ReadAllText(path);
    var scanner = new ScriptScanner(txt);
    var tokens = scanner.ScanTokens();
    var parser = new ScriptParser(tokens);

    ScriptExpr expr = parser.Parse();
    Eval(expr, mob, gs);

    return (Sb.ToString(), Footer.ToString());
  }

  public string Run(ScriptExpr expr, Actor mob, GameState gs)
  {
    Eval(expr, mob, gs);

    return Sb.ToString();
  }

  static ulong TrinketID(Actor partner, GameState gs)
  {
    foreach (Trait trait in partner.Traits)
    {
      if (trait is OwnsItemTrait owns)
        return owns.ItemID;
    }

    return 0;
  }

  static Actor? MobPartner(Actor mob, GameState gs)
  {
    foreach (Trait trait  in mob.Traits)
    {
      if (trait is RelationshipTrait relationship)
      {
        return (Actor?)gs.ObjDb.GetObj(relationship.Person2ID);
      }
    }
    
    return null;
  }
  
  static object CheckVal(string name, Actor mob, GameState gs)
  {
    Actor? partner;
    ulong trinketId;

    switch (name)
    {
      case "MET_PLAYER":
        if (mob.Stats.TryGetValue(Attribute.MetPlayer, out var stat))
          return stat.Curr != 0;
        else
          return false;
      case "PLAYER_DEPTH":
        return gs.Player.Stats[Attribute.Depth].Max;
      case "DIALOGUE_STATE":
        return mob.Stats.TryGetValue(Attribute.DialogueState, out var dialogueState) ? dialogueState.Curr : 0;
      case "PLAYER_WALLET":
        return gs.Player.Inventory.Zorkmids;
      case "PARTNER_NAME":
        partner = MobPartner(mob, gs);
        string partnerName;
        if (partner is null)
          partnerName = "";
        else
          partnerName = partner.Name.Capitalize();        
        return partnerName;
      case "HAS_TRINKET":
        trinketId = ulong.MaxValue;
        partner = MobPartner(mob, gs);
        if (partner is not null)
         trinketId = TrinketID(partner, gs);
        return gs.Player.Inventory.Contains(trinketId);
      case "TRINKET":
        trinketId = ulong.MaxValue;
        partner = MobPartner(mob, gs);
        if (partner is not null)
          trinketId = TrinketID(partner, gs);
        return trinketId;
      case "TRINKET_NAME":
        trinketId = ulong.MaxValue;
        partner = MobPartner(mob, gs);
        if (partner is not null)
          trinketId = TrinketID(partner, gs);
        if (gs.ObjDb.GetObj(trinketId) is Item item)        
          return item.Name;        
        return "";
      case "LEVEL_FIVE_KEY_GIVEN":
        if (gs.FactDb.FactCheck("Level 5 Key Given") is SimpleFact f && f.Value == "true")
          return true;
        return false;
      case "LEVEL_FIVE_BOSS":
        if (gs.FactDb.FactCheck("Level 5 Boss") is SimpleFact f2)
          return f2.Value;
        return "";
      case "LEVEL_FIVE_BOSS_KILLED":
        if (gs.FactDb.FactCheck("Level 5 Boss Killed") is SimpleFact)
          return true;
        return false;
      case "DUNGEON_DIR":
        Loc dungoenLoc = Loc.Nowhere;
        if (gs.FactDb.FactCheck("Dungeon Entrance") is LocationFact loc)
          dungoenLoc = loc.Loc;
        return Util.RelativeDir(mob.Loc, dungoenLoc);
      case "ORCHARD_EXISTS":
        return gs.FactDb.FactCheck("OrchardExists") is SimpleFact;
      case "MAGIC101":
        return gs.Player.Stats.ContainsKey(Attribute.MagicPoints);
      case "IMPRISONED_BY":
        if (gs.FactDb.FactCheck("ImprisonedBy") is SimpleFact fact)
          return fact.Value;
        else
          return "";
      default:
        throw new Exception($"Unknown variable {name}");
    }    
  }

  static string DoMadLibs(string s, Actor mob, GameState gs)
  {
    if (s.Contains("#TOWN_NAME"))
    {
      s = s.Replace("#TOWN_NAME", gs.Town.Name);
    }

    if (s.Contains("#NPC_NAME"))
    {
      s = s.Replace("#NPC_NAME", mob.FullName);
    }

    if (s.Contains("#EARLY_DENIZEN"))
    {
      string monsters = "";
      if (gs.FactDb!.FactCheck("EarlyDenizen") is SimpleFact ed)
        monsters = ed.Value;

      s = s.Replace("#EARLY_DENIZEN", monsters.Pluralize());
    }

    if (s.Contains("#PARTNER_NAME"))
    {
      s = s.Replace("#PARTNER_NAME", CheckVal("PARTNER_NAME", mob, gs).ToString());
    }

    if (s.Contains("#DUNGEON_DIR"))
    {
      s = s.Replace("#DUNGEON_DIR", CheckVal("DUNGEON_DIR", mob, gs).ToString());
    }

    if (s.Contains("#TRINKET_NAME"))
    {
      s = s.Replace("#TRINKET_NAME", CheckVal("TRINKET_NAME", mob, gs).ToString());
    }

    if (s.Contains("#LEVEL_FIVE_BOSS"))
    {
      s = s.Replace("#LEVEL_FIVE_BOSS", CheckVal("LEVEL_FIVE_BOSS", mob, gs).ToString());
    }

    if (s.Contains("#IMPRISONED_BY"))
    {
      string imprisonedBy = gs.FactDb.FactCheck("ImprisonedBy") is SimpleFact fact ? fact.Value : "";
      s = s.Replace("#IMPRISONED_BY", imprisonedBy);
    }

    s = s.Replace(@"\n", Environment.NewLine);

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
      string s = DoMadLibs(str.Value, mob, gs);

      result = new ScriptString(s);
    }
    else if (Expr is ScriptBool boolean)
    {
      return boolean;
    }
    else if (Expr is ScriptNumber number)
    {
      return number;
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
    else if (Expr is ScriptOption opt)
    {
      EvalOption(opt, mob, gs);
    }
    else if (Expr is ScriptBlessings)
    {
      EvalBlessings(mob, gs);
    }
    else if (Expr is ScriptShopMenu)
    {
      EvalShopMenu(mob, gs);
    }
    else if (Expr is ScriptShopSelection selection)
    {
      EvalShopSelection(selection.Choice, mob, gs);
    }
    else if (Expr is ScriptChampionBlessing)
    {
      EvalChampionBlessing(mob, gs);
    }
    else if (Expr is ScriptReaverBlessing)
    {
      EvalReaverBlessing(mob, gs);
    }
    else if (Expr is ScriptEmberBlessing)
    {
      EvalEmberBlessing(mob, gs);
    }
    else if (Expr is ScriptTricksterBlessing)
    {
      EvalTricksterBlessing(mob, gs);
    }
    else if (Expr is ScriptWinterBlessing)
    {
      EvalWinterBlessing(mob, gs);
    }
    else if (Expr is ScriptSpend spend)
    {
      EvalSpend(spend.Amount, gs);
    }
    else if (Expr is ScriptEnd end)
    {
      EvalEnd(end, mob, gs);
    }
    else if (Expr is ScriptOffer offer)
    {
      EvalOffer(offer, mob, gs);
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

  static void EvalEnd(ScriptEnd end, Actor mob, GameState gs)
  {
    string msg = DoMadLibs(end.Text, mob, gs);

    throw new ConversationEnded(msg);
  }

  static ScriptBool EvallBooleanExpr(ScriptBooleanExpr expr, Actor mob, GameState gs)
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
    ScriptExpr result;
    Stat? stat;

    switch (set.Name)
    {
      case "MET_PLAYER":
        result = Eval(set.Value, mob, gs);
        if (result is not ScriptBool boolean)
          throw new Exception("Expected boolean value for setting MET_PLAYER");

        int val = boolean.Value ? 1 : 0;
        if (mob.Stats.TryGetValue(Attribute.MetPlayer, out stat))
          stat.SetMax(val);
        else
          mob.Stats.Add(Attribute.MetPlayer, new Stat(val));
        break;
      case "DIALOGUE_STATE":
        result = Eval(set.Value, mob, gs);
        if (result is not ScriptNumber number)
          throw new Exception("Expected number value for setting DIALOGUE_STATE");

        if (mob.Stats.TryGetValue(Attribute.DialogueState, out stat))
          stat.SetMax(number.Value);
        else
          mob.Stats.Add(Attribute.DialogueState, new Stat(number.Value));
        break;
      case "LEVEL_FIVE_KEY_GIVEN":
        result = Eval(set.Value, mob, gs);
        if (result is not ScriptBool boolVal)
          throw new Exception("Expected number value for setting LEVEL_FIVE_KEY_GIVEN");

        string setValue =  boolVal.Value ? "true" : "false";

        if (gs.FactDb.FactCheck("Level 5 Key Given") is SimpleFact key5)
          key5.Value = setValue;

        gs.FactDb.Add(new SimpleFact() { Name = "Level 5 Key Given", Value = setValue });        
        break;
      default:
        throw new Exception($"Unknown variable: {set.Name}");
    }
  }

  static Item LevelFiveKey(GameState gs)
  {
    var (fg, bg) = Util.MetallicColour(Metals.Iron);
    Item key = new() { 
      Name = "key", Type = ItemType.Tool, Value = 1,
      Glyph = new Glyph(';', fg, bg, Colours.BLACK, Colours.BLACK)
    };
    key.Traits.Add(new MetalTrait() { Type = Metals.Iron });

    if (gs.FactDb.FactCheck("Level 5 Gate Loc") is LocationFact loc)
    {
      key.Traits.Add(new VaultKeyTrait(loc.Loc));
    }
    gs.ObjDb.Add(key);

    return key;
  }

  void EvalGive(ScriptGive gift, Actor mob, GameState gs)
  {
    if (gift.Gift == "BOON_STR")
    {
      Stat str = gs.Player.Stats[Attribute.Strength];
      str.SetMax(str.Curr + 3);
      gs.UIRef().AlertPlayer($"\"{gift.Blurb}\"");
      gs.UIRef().AlertPlayer("You feel stronger!");      
      return;
    }
    else if (gift.Gift == "BOON_DEX")
    {
      Stat dex = gs.Player.Stats[Attribute.Dexterity];
      dex.SetMax(dex.Curr + 3);
      gs.UIRef().AlertPlayer($"\"{gift.Blurb}\"");
      gs.UIRef().AlertPlayer("You feel more agile!");
      return;
    }
    else if (gift.Gift == "BOON_CON")
    {
      Stat con = gs.Player.Stats[Attribute.Constitution];
      con.SetMax(con.Curr + 3);
      gs.Player.Stats[Attribute.HP].ChangeMax(15);
      gs.Player.Stats[Attribute.HP].Change(15);
      gs.UIRef().AlertPlayer($"\"{gift.Blurb}\"");
      gs.UIRef().AlertPlayer("You feel healthier!");
      return;      
    }

    Item item = gift.Gift switch
    {
      "MINOR_GIFT" => Treasure.MinorGift(gs.ObjDb, gs.Rng),
      "LEVEL_FIVE_KEY" => LevelFiveKey(gs),
      _ => throw new Exception($"Unknown variable: {gift.Gift}"),
    };
    
    Sb.Append("\n\n");
    Sb.Append(gift.Blurb);
    Sb.Append("\n\n[GREEN ");
    Sb.Append(mob.FullName.Capitalize());
    Sb.Append(" gives you ");
    Sb.Append(item.Name.IndefArticle());
    Sb.Append("!]");

    gs.Player.Inventory.Add(item, gs.Player.ID);
  }

  void EvalOffer(ScriptOffer offer, Actor mob, GameState gs)
  {
    ulong itemId = (ulong) CheckVal(offer.Identifier.Name, mob, gs);
    if (itemId == ulong.MaxValue)
      throw new Exception($"Unknown item in dialogue: {offer.Identifier.Name}.");
    
    Item? item = gs.Player.Inventory.RemoveByID(itemId);

    // I think for now the item is just gone? If I actually implement NPC/monster 
    // inventories we could put it there.
    if (item is not null)
    {
      gs.ObjDb.RemoveItemFromGame(Loc.Nowhere, item);
    }
  }

  ScriptBool EvalAnd(ScriptAnd andExpr, Actor mob, GameState gs)
  {
    if (andExpr.Conditions.Count == 0)
      throw new Exception("And expressions must have at least one condition.");

    foreach (ScriptExpr cond in andExpr.Conditions)
    {
      ScriptExpr result = Eval(cond, mob, gs);
      if (result is not ScriptBool boolResult)
        throw new Exception("Expected boolean condition in and expression.");
      
      if (!boolResult.Value)
        return boolResult;
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
        throw new Exception("Expected boolean condition in or expression.");

      if (boolResult.Value)      
        return boolResult;
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

  void EvalOption(ScriptOption opt, Actor mob, GameState gs)
  {
    char ch = (char) ('a' + Options.Count);
    string s = DoMadLibs(opt.Text, mob, gs);

    Options.Add(new DialogueOption(s, ch, opt.Expr));
  }

  static void EvalSpend(int amount, GameState gs)
  {
    int purse = gs.Player.Inventory.Zorkmids;
    gs.Player.Inventory.Zorkmids = int.Max(0, purse - amount);
  }

  void EvalShopMenu(Actor mob, GameState gs)
  {
    NumberListTrait selections = mob.Traits.OfType<NumberListTrait>()
                                           .Where(t => t.Name == "ShopSelections")
                                           .First();

    Sb.Append("\n\nHere's what I have left:");

    int bill = 0;
    char opt = 'a';
    foreach (Item item in mob.Inventory.Items())
    {
      string s = $"{item.FullName.IndefArticle()} - [YELLOW $]20";
      if (selections.Items.Contains(opt - 'a')) 
      {
        s += " [GREEN *]";
        bill += 20;
      }
      Options.Add(new DialogueOption(s, opt, new ScriptShopSelection(opt)));
      ++opt;
    }

    mob.Stats[Attribute.ShopInvoice] = new Stat(bill);
    
    if (bill > 0)
    {
      Footer.Append("\nTotal bill: [YELLOW $]");
      Footer.Append(bill);

      if (bill <= gs.Player.Inventory.Zorkmids)
        Footer.Append("\n\n(Enter to accept)");
      else
        Footer.Append("\n\n[BRIGHTRED You don't have enough money for all that!]");
    }
  }

  static void EvalShopSelection(char choice, Actor mob, GameState gs)
  {
    int itemNum = choice - 'a';
    NumberListTrait selections = mob.Traits.OfType<NumberListTrait>()
                                           .Where(t => t.Name == "ShopSelections")
                                           .First();
    if (!selections.Items.Remove(itemNum))
      selections.Items.Add(itemNum);
  }

  void EvalBlessings(Actor mob, GameState gs)
  {
    bool blessed = gs.Player.HasTrait<BlessingTrait>();
    
    if (!blessed)
    {
      ((NPCBehaviour) mob.Behaviour).RefreshShop(mob, gs);
      Sb.Append("\n\nIf you would seek to drive back the darkness, I can offer you a blessing!");
      Sb.Append("\n\n(Blessings are buffs that will eventually wear off, and you'll need to return if you want another)");
      char opt = 'a';
      NumberListTrait blessings = mob.Traits.OfType<NumberListTrait>().Where(t => t.Name == "Blessings").First();
      foreach (int blessing in blessings.Items)
      {
        switch (blessing)
        {
          case 1:
            Options.Add(new DialogueOption("The [ICEBLUE Blessing of the Champion]: Huntokar's will shall protect you and lead your blade to strike true!", opt++, new ScriptChampionBlessing()));
            break;
          case 2:
            Options.Add(new DialogueOption("The [ICEBLUE Blessing of the Reaver]: Bring Huntokar's wrath to your foes, turning you into a frightening presence!", opt++, new ScriptReaverBlessing()));    
            break;
          case 3:
            Options.Add(new DialogueOption("The [ICEBLUE Blessing of Embers]: Huntokar will surround you in holy fire and immolate evil you face!", opt++, new ScriptEmberBlessing()));
            break;
          case 4:
            Options.Add(new DialogueOption("The [ICEBLUE Blessing of the Trickster]: Draw upon Huntokars's mischevious aspects and elude your foes!", opt++, new ScriptTricksterBlessing()));
            break;
          case 5:
            Options.Add(new DialogueOption("The [ICEBLUE Winter's Blessing]: Use the power of arctic storms to aid your quest!", opt++, new ScriptWinterBlessing()));
            break;
        }
      }
    }
    else
    {
      Sb.Append("\n\nYou go forth with Huntokar's blessing and protection!");
    }
  }

  static void EvalChampionBlessing(Actor mob, GameState gs)
  {
    ChampionBlessingTrait blessing = new() { SourceId = mob.ID, ExpiresOn = gs.Turn + 2000, OwnerID = gs.Player.ID };
    blessing.Apply(mob, gs);
    gs.Player.Stats[Attribute.LastBlessing].SetMax(1);
    
    throw new ConversationEnded("You are bathed in holy light!");
  }

  static void EvalReaverBlessing(Actor mob, GameState gs)
  {
    ReaverBlessingTrait reaver = new() { SourceId = mob.ID, ExpiresOn = gs.Turn + 2000, OwnerID = gs.Player.ID };
    reaver.Apply(mob, gs);
    gs.Player.Stats[Attribute.LastBlessing].SetMax(1);

    throw new ConversationEnded("You are bathed in holy light!");
  }

  static void EvalEmberBlessing(Actor mob, GameState gs)
  {
    EmberBlessingTrait ember = new() { SourceId = mob.ID, ExpiresOn = gs.Turn + 2000, OwnerID = gs.Player.ID };
    ember.Apply(mob, gs);
    gs.Player.Stats[Attribute.LastBlessing].SetMax(2);

    throw new ConversationEnded("You are bathed in holy light!");
  }

  static void EvalTricksterBlessing(Actor mob, GameState gs)
  {
    TricksterBlessingTrait trickster = new() { SourceId = mob.ID, ExpiresOn = gs.Turn + 2000, OwnerID = gs.Player.ID };
    trickster.Apply(mob, gs);
    gs.Player.Stats[Attribute.LastBlessing].SetMax(4);

    throw new ConversationEnded("You are bathed in holy light!");
  }

  static void EvalWinterBlessing(Actor mob, GameState gs)
  {
    WinterBlessingTrait winter = new() { SourceId = mob.ID, ExpiresOn = gs.Turn + 2000, OwnerID = gs.Player.ID };
    winter.Apply(mob, gs);
    gs.Player.Stats[Attribute.LastBlessing].SetMax(5);
    
    throw new ConversationEnded("You are bathed in holy light!");
  }
}

// Used when the NPC ends the conversation (probably in response
// to a dialogue option)
class ConversationEnded(string message) : Exception(message) { }
