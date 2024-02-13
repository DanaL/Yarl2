
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
    Action TakeTurn(UserInterface ui, GameState gameState);
}

// I think not ever monster will have an inventory, I've split out
// an interface to hold the functions the ones who do will need
interface IItemHolder
{
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
    public int Row { get; set; }
    public int Col { get; set; }
    public int MaxHP { get; set; }
    public int CurrHP { get; set; }
    public int MaxVisionRadius { get; set; }
    public int CurrVisionRadius { get; set; }
    
    public Actor() {}
}

class MonsterFactory 
{ 
    public static Actor Get(string name, AIType aiType)
    {
        var m = new Monster()
        {
            Name = name,
            MaxHP = 10,
            CurrHP = 10,
            AttackBonus = 3,
            Dmg = new Dmg(1, 6, 1),
            Glyph = new Glyph('z', Colours.GREY, Colours.DARK_GREY),
            AIType = aiType
        };
        m.SetBehaviour(aiType);

        return m;
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
    private IBehaviour _behaviour;

    public Monster() {}
    
    public Action TakeTurn(UserInterface ui, GameState gameState)
    {
        Console.WriteLine($"{Name.IndefArticle()} takes its turn.");
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
        return new PassAction(actor);
    }
}

class VillagerBehaviour : IBehaviour
{
    public Action CalcAction(Actor actor, GameState gameState)
    {
        return new PassAction(actor);
    }
}