
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

record struct Dmg(int Num, int Dice, int Bonus);

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
    // TODO: gotta read this shit in from a file of some sort, yuck
    public static Actor Get(string name)
    {
        if (name == "skeleton")
        {
            var m = new Monster()
            {
                Name = name,                
                AttackBonus = 2,
                Dmg = new Dmg(1, 6, 1),
                Glyph = new Glyph('z', Colours.GREY, Colours.DARK_GREY),
                AIType = AIType.Basic,
                Recovery = 1.0
            };
            m.Stats.Add(Attribute.HP, new Stat(10));
            m.SetBehaviour(AIType.Basic);
            return m;
        }

        if (name == "goblin")
        {
            var m = new Monster()
            {
                Name = name,
                AttackBonus = 2,
                Dmg = new Dmg(1, 6, 1),
                Glyph = new Glyph('g', Colours.LIGHT_BROWN, Colours.BROWN),
                AIType = AIType.Basic,
                Recovery = 1.0
            };
            m.Stats.Add(Attribute.HP, new Stat(8));
            m.SetBehaviour(AIType.Basic);
            return m;
        }

        if (name == "goblin boss")
        {
            var m = new Monster()
            {
                Name = name,
                AttackBonus = 3,
                Dmg = new Dmg(1, 6, 2),
                Glyph = new Glyph('g', Colours.GREEN, Colours.DARK_GREEN),
                AIType = AIType.Basic,
                Recovery = 1.0
            };
            m.Stats.Add(Attribute.HP, new Stat(12));
            m.SetBehaviour(AIType.Basic);
            return m;
        }

        if (name == "hobgoblin")
        {
            var m = new Monster()
            {
                Name = name,
                AttackBonus = 3,
                Dmg = new Dmg(1, 8, 1),
                Glyph = new Glyph('g', Colours.GREY, Colours.DARK_GREY),
                AIType = AIType.Basic,
                Recovery = 1.0
            };
            m.Stats.Add(Attribute.HP, new Stat(16));
            m.SetBehaviour(AIType.Basic);
            return m;
        }

        if (name == "goblin archer")
        {
            var m = new Monster()
            {
                Name = name,
                AttackBonus = 3,
                Dmg = new Dmg(1, 6, 2),
                Glyph = new Glyph('g', Colours.BRIGHT_RED, Colours.LIGHT_BROWN),
                AIType = AIType.Basic,
                Recovery = 1.0
            };
            m.Stats.Add(Attribute.HP, new Stat(12));
            m.SetBehaviour(AIType.Basic);
            return m;
        }

        if (name == "wolf")
        {
            var m = new Monster()
            {
                Name = name,
                AttackBonus = 2,
                Dmg = new Dmg(1, 6, 2),
                Glyph = new Glyph('d', Colours.LIGHT_GREY, Colours.GREY),
                AIType = AIType.Basic,
                Recovery = 1.0
            };
            m.Stats.Add(Attribute.HP, new Stat(8));
            m.SetBehaviour(AIType.Basic);
            return m;
        }

        if (name == "giant rat")
        {
            var m = new Monster()
            {
                Name = name,
                AttackBonus = 2,
                Dmg = new Dmg(1, 6, 2),
                Glyph = new Glyph('r', Colours.LIGHT_GREY, Colours.GREY),
                AIType = AIType.Basic,
                Recovery = 1.0
            };
            m.Stats.Add(Attribute.HP, new Stat(5));
            m.SetBehaviour(AIType.Basic);
            return m;
        }

        if (name == "dire bat")
        {
            var m = new Monster()
            {
                Name = name,
                AttackBonus = 2,
                Dmg = new Dmg(1, 6, 2),
                Glyph = new Glyph('v', Colours.LIGHT_BROWN, Colours.BROWN),
                AIType = AIType.Basic,
                Recovery = 1.0
            };
            m.Stats.Add(Attribute.HP, new Stat(6));
            m.SetBehaviour(AIType.Basic);
            return m;
        }

        if (name == "zombie")
        {
            var m = new Monster()
            {
                Name = name,
                AttackBonus = 2,
                Dmg = new Dmg(1, 6, 1),
                Glyph = new Glyph('z', Colours.LIME_GREEN, Colours.DARK_GREEN),
                AIType = AIType.Basic,
                Recovery = 0.75
            };
            m.Stats.Add(Attribute.HP, new Stat(10));
            m.SetBehaviour(AIType.Basic);
            return m;
        }

        if (name == "kobold")
        {
            var m = new Monster()
            {
                Name = name,
                AttackBonus = 2,
                Dmg = new Dmg(1, 6, 1),
                Glyph = new Glyph('k', Colours.BRIGHT_RED, Colours.DULL_RED),
                AIType = AIType.Basic,
                Recovery = 1.0
            };
            m.Stats.Add(Attribute.HP, new Stat(8));
            m.SetBehaviour(AIType.Basic);
            return m;
        }

        if (name == "kobold trickster")
        {
            var m = new Monster()
            {
                Name = name,
                AttackBonus = 2,
                Dmg = new Dmg(1, 6, 1),
                Glyph = new Glyph('k', Colours.LIGHT_BLUE, Colours.BLUE),
                AIType = AIType.Basic,
                Recovery = 1.0
            };
            m.Stats.Add(Attribute.HP, new Stat(8));
            m.SetBehaviour(AIType.Basic);
            return m;
        }

        if (name == "kobold foreman")
        {
            var m = new Monster()
            {
                Name = name,
                AttackBonus = 2,
                Dmg = new Dmg(1, 6, 1),
                Glyph = new Glyph('k', Colours.YELLOW_ORANGE, Colours.DULL_RED),
                AIType = AIType.Basic,
                Recovery = 1.0
            };
            m.Stats.Add(Attribute.HP, new Stat(8));
            m.SetBehaviour(AIType.Basic);
            return m;
        }

        throw new Exception($"{name}s don't seem to exist in this world!");
    }
}

// Covers pretty much any actor that isn't the player. Villagers
// for instance are of type Monster even though it's a bit rude
// to call them that. Dunno why I don't like the term NPC for
// this class
class Monster : Actor, IPerformer
{
    public int AttackBonus { get; set; }
    public Dmg Dmg { get; set; }
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