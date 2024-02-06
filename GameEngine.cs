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
    public HashSet<(int, int)>? Remebered { get; set; }
    public HashSet<(int, int)>? Visible { get; set; }
    public Map? Map { get; set; }
    public Options? Options { get; set;}
    public Player? Player { get; set; }
}

internal class GameEngine(int visWidth, int visHeight, Display display, Options options)
{
    public readonly int VisibleWidth = visWidth;
    public readonly int VisibleHeight = visHeight;
    private readonly Display ui = display;
    private readonly Options _options = options;
    private HashSet<(int, int)> _rememberedSqs = [];

    private void UpdateView(Player player, Map map)
    {
        var vs = FieldOfView.CalcVisible(player, map);
        _rememberedSqs = _rememberedSqs.Union(vs).ToHashSet();
        var gameState = new GameState()
        {
            Visible = vs,
            Remebered = _rememberedSqs,
            Map = map,
            Options = _options,
            Player = player
        }; 
        ui.UpdateDisplay(gameState);
    }

    public void Play(Player player, Map map)
    {
        bool playing = true;
        UpdateView(player, map);

        do 
        {            
            var gameState = new GameState()
            {
                Map = map,
                Options = _options,
                Player = player
            };
            var cmd = ui.GetCommand(gameState);
            if (cmd is QuitAction)
            {
                playing = false;
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

                UpdateView(player, map);
            }                
        }
        while (playing);
    }
}
