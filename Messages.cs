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

abstract class Message(Loc loc) 
{
    public Loc Loc { get; set; } = loc;

    public abstract string AsPhrase(GameState gs);

    protected string CalcVerb(GameObj subject, Verb verb)
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

    protected string CalcName(GameObj gobj, int amount = 0)
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
}

class SimpleMessage(string msg, Loc loc) : Message(loc)
{
    public string _msg = msg;

    public override string AsPhrase(GameState gs) => _msg;    
}

// Subject-verb-object message, where object is another gameobj
class SVOMessage(ulong Subject, Verb Verb, ulong Obj, int Amt, bool Exciting, Loc Loc) : Message(Loc)
{
    public override string AsPhrase(GameState gs)
    {
        var sb = new StringBuilder();
        GameObj? subject = gs.ObjDB.GetObj(Subject);
        GameObj? victim = gs.ObjDB.GetObj(Obj);

        if (subject is not null)
        {
            sb.Append(CalcName(subject));
            sb.Append(' ');
            sb.Append(CalcVerb(subject, Verb));

            if (Obj != 0 && victim is not null)
            {                
                sb.Append(' ');
                sb.Append(CalcName(victim, Amt));
            }

            sb.Append(Exciting ? '!' : '.');
        }

        return sb.ToString();
    }
}

// Sometimes the object will just be a string, when the object isn't a game object.
// Ie., "You open the door." etc
class SVSMessage(ulong subject, Verb verb, string obj, bool exciting, Loc loc) : Message(loc)
{
    ulong _subject = subject;
    Verb _verb = verb;
    string _object = obj;
    bool _exciting = exciting;

    public override string AsPhrase(GameState gs)
    {
        var sb = new StringBuilder();
        GameObj? subject = gs.ObjDB.GetObj(_subject);
        
        if (subject is not null)
        {
            sb.Append(CalcName(subject));
            sb.Append(' ');
            sb.Append(CalcVerb(subject, _verb));               
            sb.Append(' ');
            sb.Append(_object);
            sb.Append(_exciting ? '!' : '.');
        }

        return sb.ToString();
    }
}