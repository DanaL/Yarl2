﻿using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json;

namespace Yarl2;

abstract class Actor
{
    public int Row { get; set; }
    public int Col { get; set; }
    public int MaxVisionRadius { get; set; }
    public int CurrVisionRadius { get; set; }
}

internal class GameQuitException : Exception { }

// The queue of actors to act will likely need to go here.
internal class GameState
{
    public HashSet<(int, int, int)>? Remebered { get; set; }
    public HashSet<(int, int, int)>? Visible { get; set; }
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

internal class GameEngine(int visWidth, int visHeight, Display display, Options options)
{
    public readonly int VisibleWidth = visWidth;
    public readonly int VisibleHeight = visHeight;
    private readonly Display ui = display;
    private readonly Options _options = options;
    
    // I'm really just replacing everything in GameState with Campaign...
    private void UpdateView(Player player, GameState gameState)
    {
        var c = gameState.Campaign;
        int currLevel = gameState.CurrLevel;
        var dungeon = c.Dungeons[gameState.CurrDungeon];
        var map = dungeon.LevelMaps[currLevel];            
        var vs = FieldOfView.CalcVisible(player, map, currLevel);
        var toShow = vs.Select(v => (v.Item2, v.Item3)).ToHashSet();        
        dungeon.RememberedSqs = dungeon.RememberedSqs.Union(vs).ToHashSet();
        gameState.Visible = vs;
        gameState.Remebered = dungeon.RememberedSqs;
        gameState.Map = map;
        ui.UpdateDisplay(gameState);
    }

    static void SaveGame(Player player, Campaign campaign, GameState gameState)
    {
        var sgi = new SaveGameInfo(player, campaign, gameState.CurrLevel, gameState.CurrDungeon);
        string filename = $"{player.Name}.dat";
        
        var resultBytes = JsonSerializer.SerializeToUtf8Bytes(sgi,
                    new JsonSerializerOptions { WriteIndented = false, IncludeFields=true });
        File.WriteAllBytes(filename, resultBytes);

        var s = JsonSerializer.Serialize(sgi, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine(s);
    }

    public void Play(Player player, Campaign campaign, int currLevel, int currDungeon)
    {
        var currentLevel = 0;
        var gameState = new GameState()
        {
            Map = campaign.Dungeons[0].LevelMaps[currentLevel],
            Options = _options,
            Player = player,
            Campaign = campaign,
            CurrLevel = currLevel,
            CurrDungeon = currDungeon
        };
        
        bool playing = true;
        UpdateView(player, gameState);

        do 
        {
            var cmd = ui.GetCommand(gameState);
            if (cmd is QuitAction)
            {
                playing = false;
            }
            else if (cmd is SaveGameAction)
            {
                if (ui.QueryYesNo("Really quit and save? (y/n)"))
                {
                    SaveGame(player, campaign, gameState);
                    throw new GameQuitException();
                }
                else
                {
                    ui.WriteMessage("Nevermind.");
                }
            }
            else if (cmd is NullAction)
            {
                // Just idling...                
                Thread.Sleep(25);
            }
            else
            {
                ActionResult result;
                do 
                {
                    result = cmd!.Execute();
                    if (result.AltAction is not null)
                        result = result.AltAction.Execute();
                    if (result.Message is not null)
                        ui.WriteMessage(result.Message);                    
                }
                while (result.AltAction is not null);                

                UpdateView(player, gameState);
            }                
        }
        while (playing);
    }
}

internal record SaveGameInfo(Player? Player, Campaign? Campaign, int CurrentLevel, int CurrentDungeon);
