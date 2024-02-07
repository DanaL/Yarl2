using System.Linq;

namespace Yarl2;

abstract class Actor
{
    public int Row { get; set; }
    public int Col { get; set; }
    public int MaxVisionRadius { get; set; }
    public int CurrVisionRadius { get; set; }
}

internal class GameQuitException : Exception { }

internal class GameState
{
    public HashSet<(int, int, int)>? Remebered { get; set; }
    public HashSet<(int, int, int)>? Visible { get; set; }
    public Map? Map { get; set; }
    public Options? Options { get; set;}
    public Player? Player { get; set; }
    public int CurrLevel { get; set; }
}

internal class GameEngine(int visWidth, int visHeight, Display display, Options options)
{
    public readonly int VisibleWidth = visWidth;
    public readonly int VisibleHeight = visHeight;
    private readonly Display ui = display;
    private readonly Options _options = options;
    
    private void UpdateView(Player player, Dungeon dungeon, int currLevel)
    {
        var map = dungeon.LevelMaps[currLevel];
        var vs = FieldOfView.CalcVisible(player, map, currLevel);
        var toShow = vs.Select(v => (v.Item2, v.Item3)).ToHashSet();
        dungeon.RememberedSqs = dungeon.RememberedSqs.Union(vs).ToHashSet();
        
        var gameState = new GameState()
        {
            Visible = vs,
            Remebered = dungeon.RememberedSqs,
            Map = map,
            Options = _options,
            Player = player,
            CurrLevel = currLevel
        }; 
        ui.UpdateDisplay(gameState);
    }

    public void Play(Player player, Campaign campaign)
    {
        var currentDungeon = campaign.Dungeons[0];
        var currentLevel = 0;

        bool playing = true;
        UpdateView(player, currentDungeon, currentLevel);

        do 
        {            
            var gameState = new GameState()
            {
                Map = currentDungeon.LevelMaps[currentLevel],
                Options = _options,
                Player = player
            };
            var cmd = ui.GetCommand(gameState);
            if (cmd is QuitAction)
            {
                playing = false;
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

                UpdateView(player, currentDungeon, currentLevel);
            }                
        }
        while (playing);
    }
}
