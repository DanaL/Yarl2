
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

// A structure to store info about a dungeon
class Dungeon(int ID, string arrivalMessage)
{
  public int ID { get; init; } = ID;
  public Dictionary<Loc, Glyph> RememberedLocs = [];
  public Dictionary<int, Map> LevelMaps = [];
  public string ArrivalMessage { get; } = arrivalMessage;

  public void AddMap(Map map)
  {
    int id = LevelMaps.Count == 0 ? 0 : LevelMaps.Keys.Max() + 1;
    LevelMaps.Add(id, map);
  }
}

// A data structure to store all the info about 
// the 'story' of the game. All the dungeon levels, etc
class Campaign
{
  public Town? Town { get; set; }
  public FactDb? FactDb { get; set; }
  public Dictionary<int, Dungeon> Dungeons = [];
  public List<MonsterDeck> MonsterDecks { get; set; } = [];
  
  public void AddDungeon(Dungeon dungeon)
  {
    int id = Dungeons.Count == 0 ? 0 : Dungeons.Keys.Max() + 1;
    Dungeons.Add(id, dungeon);
  }
}
