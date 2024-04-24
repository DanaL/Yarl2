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

// Bits and bobs for tracking the messages that occur in the game.
// At the moment it's mainly to alert the player but I have in mind
// eventually NPCs/monsters can react to things that happen in game
// Ie., a shepherd could see "wolf attacks sheep" and can react to
// that.

// Is this dumb? should I just store a string in the Messages?
enum Verb
{
    Break,
    Close,
    Drop,
    Hit,
    Miss,
    Pickup,    
    Open,    
    Use,
    Ready,
    Unready,
    BurnsOut,
    Kill,
    Die,
    Etre,
    Hear,
    Cleave,
    Impale,
    Drink,
    Heal,
    Dissipate,
    Blink,
    Cast,
    Destroy,
    Tear,
    Feel,
    Summon,
    Stumble
}

record Message(string Text, Loc Loc, bool Sound=false);

// I think my verb enum and CalcVerb(), and maybe the whole MessageFactory
// class were dumb ideas and I can just replace them with this 
class Grammar
{
  public static string Conjugate(GameObj subject, string verb)
  {
    var player = subject is Player;

    if (verb == "is" && player)
      return "are";
    else if (verb == "is")
      return verb;

    if (subject is Player)
      return verb;
    else if (verb.EndsWith("ss") || verb.EndsWith("sh"))
      return verb + "es";
    else if (verb == "ready")
      return "readies";
    else
      return verb + "s";
  }

  public static string Possessive(GameObj subject)
  {
    if (subject is Player)
      return "your";
    else if (subject.Traits.OfType<NamedTrait>().Any())
      return "their";
    else
      return "its";
  }
}

class MsgFactory
{
    public static string CalcVerb(GameObj subject, Verb verb, bool thirdP = false)
    {
        bool fp = subject is Player;
        return verb switch 
        {
            Verb.Break => fp ? "break" : "breaks",
            Verb.Close => fp ? "close" : "closes",
            Verb.Drop => fp ? "drop" : "drops",
            Verb.Hit => fp ? "hit" : "hits",
            Verb.Miss => fp ? "miss" : "misses",
            Verb.Pickup => fp ? "pick up" : "picks up",
            Verb.Open => fp ? "open" : "opens",            
            Verb.Use => fp ? "use" : "uses",
            Verb.Ready => fp ? "ready" : "readies",
            Verb.Unready => fp ? "unequip" : "unequips",
            Verb.BurnsOut => fp ? "burnt out" : "burns out",
            Verb.Kill => fp ? "kill" : thirdP ? "killed" : "kills",
            Verb.Die => fp ? "die" : thirdP ? "died" : "dies",
            Verb.Etre => fp ? "are" : thirdP ? "are" : "is",
            Verb.Hear => fp ? "hear" : "hears",
            Verb.Cleave => fp ? "cleave" : "cleaves",
            Verb.Impale => fp ? "impale" : "impales",
            Verb.Drink => fp ? "drink" : "drinks",
            Verb.Heal => fp ? "heal" : "heals",
            Verb.Dissipate => fp ? "dissipate" : "dissipates",
            Verb.Blink => fp ? "blink" : "blinks",
            Verb.Cast => fp ? "cast" : "casts",
            Verb.Destroy => fp ? "destroy" : "destroys",
            Verb.Tear => fp ? "tear" : "tears",
            Verb.Feel => fp ? "feel" : "feels",
            Verb.Summon => fp ? "summon" : "summons",
            Verb.Stumble => fp ? "stumble" : "stubmles"
        };
    }

    static string PastParticiple(Verb verb) => verb switch
    {
        Verb.Hit => "hit",
        Verb.Miss => "missed",
        Verb.Pickup => "picked up",
        Verb.Drop => "dropped",
        Verb.Open => "opened",
        Verb.Close => "closed",
        Verb.Use => "used",
        Verb.Ready => "readied",
        Verb.Unready => "unequiped",
        Verb.BurnsOut => "burnt out",
        Verb.Kill => "killed",
        Verb.Etre => "been",
        Verb.Hear => "heard",
        Verb.Cleave => "cleaved",
        Verb.Impale => "impaled",
        Verb.Drink => "drunk",
        Verb.Heal => "healed",
        Verb.Dissipate => "dissipated",
        Verb.Blink => "blinked",
        Verb.Cast => "cast",
        Verb.Destroy => "destroyed",
        Verb.Tear => "teared"
    };

    public static string CalcName(GameObj gobj, int amount = 0)
    {
        StringBuilder sb = new StringBuilder();
        if (gobj is Item item)
        {
            if (amount > 1)
            {
                sb.Append(amount);
                sb.Append(' ');
                sb.Append(item.FullName.Pluralize());
            }
            else
            {
                sb.Append(item.FullName.DefArticle());
            }            
        }
        else
        {
            sb.Append(gobj.FullName);
        }

        return sb.ToString();
    }

    public static Message Phrase(ulong subject, Verb verb, ulong obj, int amt, bool exciting, Loc loc, GameState gs)
    {
        var sb = new StringBuilder();
        GameObj? sub = gs.ObjDb.GetObj(subject);
        GameObj? victim = gs.ObjDb.GetObj(obj);

        if (sub is not null)
        {
            sb.Append(CalcName(sub));
            sb.Append(' ');
            sb.Append(CalcVerb(sub, verb));

            if (obj != 0 && victim is not null)
            {                
                sb.Append(' ');
                sb.Append(CalcName(victim, amt));
            }

            sb.Append(exciting ? '!' : '.');
        }

        return new Message(sb.ToString().Capitalize(), loc);
    }

    public static Message Phrase(ulong subject, Verb verb, Loc loc, GameState gs)
    {
        var sb = new StringBuilder();
        GameObj? sub = gs.ObjDb.GetObj(subject);

        if (sub is not null)
        {
            sb.Append(CalcName(sub));
            sb.Append(' ');
            sb.Append(CalcVerb(sub, verb));            
        }

        return new Message(sb.ToString().Capitalize(), loc);
    }

    public static Message Phrase(ulong subject, Verb verb, string obj, bool exciting, Loc loc, GameState gs)
    {
        var sb = new StringBuilder();
        GameObj? sub = gs.ObjDb.GetObj(subject);
        
        if (sub is not null)
        {
            sb.Append(CalcName(sub));
            sb.Append(' ');
            sb.Append(CalcVerb(sub, verb));
            sb.Append(' ');
            sb.Append(obj.DefArticle());
            sb.Append(exciting ? '!' : '.');
        }

        return new Message(sb.ToString().Capitalize(), loc);
    }

    public static Message Phrase(ulong subject, Verb verb, Verb pastParticiple, bool thirdP, bool exciting, Loc loc, GameState gs)
    {
        var sb = new StringBuilder();
        GameObj? sub = gs.ObjDb.GetObj(subject);

        if (sub is not null)
        {
            sb.Append(CalcName(sub));
            sb.Append(' ');
            sb.Append(CalcVerb(sub, verb, thirdP));
            sb.Append(' ');
            sb.Append(PastParticiple(pastParticiple));
            sb.Append(exciting ? '!' : '.');
        }

        return new Message(sb.ToString().Capitalize(), loc);
    }
}

