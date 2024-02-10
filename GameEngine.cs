﻿
namespace Yarl2;

// These two classes don't really belong here anymore, now that the
// GameEngine class is gone...

abstract class Actor
{
    public int Row { get; set; }
    public int Col { get; set; }
    public int MaxVisionRadius { get; set; }
    public int CurrVisionRadius { get; set; }

    public abstract Action TakeTurn(UserInterface ui, GameState gameState);
}

internal class GameQuitException : Exception { }

// The queue of actors to act will likely need to go here.
internal class GameState
{
    public Map? Map { get; set; }
    public Options? Options { get; set;}
    public Player? Player { get; set; }
    public int CurrLevel { get; set; }
    public int CurrDungeon { get; set; }
    public Campaign? Campaign { get; set; }

    public void EnterLevel(int dungeon, int level)
    {
        CurrLevel = level;
        CurrDungeon = dungeon;

        // Once the queue of actors is implemented, we will need to switch them
        // out here.
    }

    public Dungeon CurrentDungeon => Campaign!.Dungeons[CurrDungeon];
    public Map CurrentMap => Campaign!.Dungeons[CurrDungeon].LevelMaps[CurrLevel];
}

