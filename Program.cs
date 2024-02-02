
using Yarl2;

//var display = new BLDisplay("Yarl2 0.0.1");
var display = new SDLDisplay("Yarl2 0.0.1");

var map = new Map(75, 75);
map.SetRandomTestMap();
map.Dump();

try
{
    display.TitleScreen();

    // string playerName = display.QueryUser("Who are you?");
    // display.WriteMessage($"Hello, {playerName}.");
    // display.WaitForInput();
    // display.WriteMessage("");

    // var rnd = new Random();
    // ushort startRow = (ushort) rnd.Next(1, map.Height);
    // ushort startCol = (ushort)rnd.Next(1, map.Width);
    // var player = new Player(playerName, startRow, startCol);

    // var engine = new GameEngine(29, 29, display);
    // engine.Play(player, map);
}
catch (GameQuitException)
{
    // var msg = new List<string>()
    // {
    //     "",
    //     " Being seeing you..."
    // };
    // display.WriteLongMessage(msg);
}