namespace Yarl2;

abstract class Actor
{
    public ushort Row { get; set; }
    public ushort Col { get; set; }
    public short MaxVisionRadius { get; set; }
    public short CurrVisionRadius { get; set; }
}

internal class GameQuitException : Exception { }

internal class GameEngine
{
    public readonly ushort VisibleWidth;
    public readonly ushort VisibleHeight;
    private readonly Display ui;

    public GameEngine(ushort visWidth, ushort visHeight, Display display)
    {
        VisibleWidth = visWidth;
        VisibleHeight = visHeight;
        ui = display;
    }

    Dictionary<(short, short), Tile> CalcVisible(Player player, Map map)
    {
        var visible = new Dictionary<(short, short), Tile>();
        var vs = FieldOfView.CalcVisible(player, map);
        
        foreach (var tile in vs)
        {
            var r = tile.Item1;
            var c = tile.Item2;
            visible.Add(((short)r, (short)c), map.TileAt(r, c));
        }
        
        return visible;
    }

    public void Play(Player player, Map map)
    {
        bool playing = true;
        bool update = true;

        do 
        {
            if (update)
            {
                var visible = CalcVisible(player, map);
                ui.UpdateDisplay(player, visible);
            }

            update = true;
            var cmd = ui.GetCommand(player, map);

            if (cmd is NullCommand)
            {
                update = false;
                Thread.Sleep(25);
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
            }                
        }
        while (playing);
    }
}
