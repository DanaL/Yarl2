
namespace Yarl2;

internal class Player : Actor
{
    public string Name { get; set; }
    public int MaxHP { get; set; }
    public int CurrHP { get; set; }

    public Player(string name, ushort row, ushort col)
    {
        Name = name;
        Row = row;
        Col = col;
        MaxHP = 20;
        CurrHP = 15;
        MaxVisionRadius = 15;
        CurrVisionRadius = MaxVisionRadius;
    }
}
