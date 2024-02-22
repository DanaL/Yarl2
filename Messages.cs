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
    Hit,
    Miss,
    Pickup,
    Drop,
    Open,
    Close,
    Die,
    Kill,
    Use,
    Ready,
    Unready,
    BurnsOut
}

record Message(string Text, Loc loc);

class MessageFactory
{
    static string CalcVerb(GameObj subject, Verb verb)
    {
        bool fp = subject is Player;
        return verb switch 
        {
            Verb.Hit => fp ? "attack" : "attacks",
            Verb.Miss => fp ? "miss" : "misses",
            Verb.Pickup => fp ? "pick up" : "picks up",
            Verb.Drop => fp ? "drop" : "drops",
            Verb.Open => fp ? "open" : "opens",
            Verb.Close => fp ? "close" : "closes",
            Verb.Die => fp ? "die" : "dies",
            Verb.Kill => fp ? "kill" : "kills",
            Verb.Use => fp ? "use" : "uses",
            Verb.Ready => fp ? "ready" : "readies",
            Verb.Unready => fp ? "unequip" : "unequips",
            Verb.BurnsOut => fp ? "burnt out" : "burns out"
        };
    }

    static string CalcName(GameObj gobj, int amount = 0)
    {
        StringBuilder sb = new StringBuilder();
        if (gobj is Item item)
        {
            if (item.Count == 1)
                sb.Append(item.FullName.DefArticle());
            else 
            {
                sb.Append(amount);
                sb.Append(' ');
                sb.Append(item.FullName.Pluralize());
            }
        }
        else
        {
            sb.Append(gobj.FullName);
        }

        return sb.ToString();
    }

    public static Message Phrase(string text, Loc loc)
    {
        return new Message(text, loc);
    }

    static string Capitalize(string s)
    {
       if (s != "" && char.IsLetter(s[0]))
            return $"{char.ToUpper(s[0])}{s[1..]}";
        else
            return s;
    }

    public static Message Phrase(ulong subject, Verb verb, ulong obj, int amt, bool exciting, Loc loc, GameState gs)
    {
        var sb = new StringBuilder();
        GameObj? sub = gs.ObjDB.GetObj(subject);
        GameObj? victim = gs.ObjDB.GetObj(obj);

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

        return new Message(Capitalize(sb.ToString()), loc);
    }

    public static Message Phrase(ulong subject, Verb verb, string obj, bool exciting, Loc loc, GameState gs)
    {
        var sb = new StringBuilder();
        GameObj? sub = gs.ObjDB.GetObj(subject);
        
        if (sub is not null)
        {
            sb.Append(CalcName(sub));
            sb.Append(' ');
            sb.Append(CalcVerb(sub, verb));               
            sb.Append(' ');
            sb.Append(obj);
            sb.Append(exciting ? '!' : '.');
        }

        return new Message(Capitalize(sb.ToString()), loc);
    }
}

