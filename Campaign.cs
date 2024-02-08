﻿
namespace Yarl2;

// A structure to store info about a dungeon
internal class Dungeon(int ID, string arrivalMessage)
{
    public int ID { get; init; } = ID;
    public HashSet<(int, int, int)> RememberedSqs = [];
    public Dictionary<int, Map> LevelMaps = new();
    public string ArrivalMessage { get; } = arrivalMessage;

    public void AddMap(Map map)
    {
        int id = LevelMaps.Count == 0 ? 0 : LevelMaps.Keys.Max() + 1;
        LevelMaps.Add(id, map);        
    }
}

// A data structure to store all the info about 
// the 'story' of the game. All the dungeon levels, etc
internal class Campaign
{
    public Dictionary<int, Dungeon> Dungeons = [];

    public void AddDungeon(Dungeon dungeon)
    {
        int id = Dungeons.Count == 0 ? 0 : Dungeons.Keys.Max() + 1;
        Dungeons.Add(id, dungeon);
    }
}
