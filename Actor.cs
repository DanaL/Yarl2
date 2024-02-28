
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

// Interface for anything that will get a turn in the game. The Player,
// NPCs/Monsters, even things like torches that are burning or a cursed
// item that zaps the player once in a while
interface IPerformer
{
    double Energy { get; set; }
    double Recovery { get; set; }
    bool RemoveFromQueue { get; set; }
    Action TakeTurn(UserInterface ui, GameState gameState);
}

// I think not ever monster will have an inventory, I've split out
// an interface to hold the functions the ones who do will need
interface IItemHolder
{
    public Inventory Inventory { get; set; } 

    void CalcEquipmentModifiers();
}

enum AIType 
{
    Basic,
    Village
}

class Actor : GameObj
{
    public Dictionary<Attribute, Stat> Stats { get; set; } = [];

    public Actor() {}

    public override string FullName => Name.DefArticle();

    public virtual int AC => 10;
}

class MonsterFactory 
{ 
    static Dictionary<string, string> _catalog = [];

    static void LoadCatalog()
    {
        foreach (var line in File.ReadAllLines("monsters.txt"))
        {
            int i = line.IndexOf('|');
            string name = line[..i].Trim();
            string val = line[(i + 1)..];
            _catalog.Add(name, val);
        }
    }

    public static Actor? Get(string name)
    {
        if (_catalog.Count == 0)
            LoadCatalog();

        if (!_catalog.TryGetValue(name, out string? template))
            throw new Exception($"{name}s don't seem to exist in this world!");

        var fields = template.Split('|').Select(f => f.Trim()).ToArray();

        var glyph = new Glyph(fields[0][0], ColourSave.TextToColour(fields[1]),
                                            ColourSave.TextToColour(fields[2]));
        Enum.TryParse(fields[10], out AIType ai);                                            
        var m = new Monster()
        {
            Name = name,
            Glyph = glyph,
            AIType = ai,
            Recovery = double.Parse(fields[6])
        };
        m.SetBehaviour(ai);

        int hp = int.Parse(fields[4]);
        m.Stats.Add(Attribute.HP, new Stat(hp));
        int attBonus = int.Parse(fields[5]);
        m.Stats.Add(Attribute.MeleeAttackBonus, new Stat(attBonus));
        int ac = int.Parse(fields[3]);
        m.Stats.Add(Attribute.AC, new Stat(ac));
        int str = int.Parse(fields[9]);
        m.Stats.Add(Attribute.Strength, new Stat(str));
        int dmgDie = int.Parse(fields[7]);
        m.Stats.Add(Attribute.DmgDie, new Stat(dmgDie));
        int dmgRolls = int.Parse(fields[8]);
        m.Stats.Add(Attribute.DmgRolls, new Stat(dmgRolls));

        return m;
    }
}

// Covers pretty much any actor that isn't the player. Villagers
// for instance are of type Monster even though it's a bit rude
// to call them that. Dunno why I don't like the term NPC for
// this class
class Monster : Actor, IPerformer
{
    public AIType AIType { get; set;}
    public double Energy { get; set; } = 0.0;
    public double Recovery { get; set; }

    public bool RemoveFromQueue { get; set; }

    private IBehaviour _behaviour;

    public Monster() {}
    
    public Action TakeTurn(UserInterface ui, GameState gameState)
    {
        return _behaviour.CalcAction(this, gameState);
    }

    public void SetBehaviour(AIType aiType)
    {
        IBehaviour behaviour = aiType switch 
        {
            AIType.Basic => new BasicMonsterBehaviour(),
            AIType.Village => new VillagerBehaviour()
        };

        _behaviour = behaviour;
        AIType = aiType;
    }
}

interface IBehaviour 
{ 
    Action CalcAction(Actor actor, GameState gameState);
}

class BasicMonsterBehaviour : IBehaviour
{
    public Action CalcAction(Actor actor, GameState gameState)
    {
        return new PassAction((IPerformer)actor);
        //return new MoveAction(actor, actor.Loc.Move(-1, 0), gameState);
    }
}

class VillagerBehaviour : IBehaviour
{
    public Action CalcAction(Actor actor, GameState gameState)
    {
        return new PassAction((IPerformer)actor);
    }
}