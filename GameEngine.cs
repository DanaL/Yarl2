namespace Yarl2;

abstract class Actor
{
    public ushort Row { get; set; }
    public ushort Col { get; set; }
    public short MaxVisionRadius { get; set; }
    public short CurrVisionRadius { get; set; }
}

internal class GameQuitException : Exception { }

internal class GameState
{
    public HashSet<(ushort, ushort)>? Remebered { get; set; }
    public HashSet<(ushort, ushort)>? Visible { get; set; }
    public Map? Map { get; set; }
}

internal class GameEngine(ushort visWidth, ushort visHeight, Display display)
{
    public readonly ushort VisibleWidth = visWidth;
    public readonly ushort VisibleHeight = visHeight;
    private readonly Display ui = display;
    private HashSet<(ushort, ushort)> _rememberedSqs = [];

    public void Play(Player player, Map map)
    {
        bool playing = true;
        
        do 
        {            
            var cmd = ui.GetCommand(player, map);

            if (cmd is NullCommand)
            {
                Thread.Sleep(10);
            }
            else if (cmd is QuitCommand)
            {
                playing = false;
            }
            else
            {
                var result = cmd.Execute();

                if (result.Message is not null)
                    ui.WriteMessage(result.Message);
    
                var vs = FieldOfView.CalcVisible(player, map);
                _rememberedSqs = _rememberedSqs.Union(vs).ToHashSet();

                var gameState = new GameState()
                {
                    Visible = vs,
                    Remebered = _rememberedSqs,
                    Map = map
                }; 
                ui.UpdateDisplay(gameState);
            }                
        }
        while (playing);
    }
}
