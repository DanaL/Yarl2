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

// This class is for generating the 'decks' of monsters used for random
// encounters. My idea is to have monsters pulled from a set/deck. When
// the deck is empty, shuffle the 'used' monsters back in. This way,
// the monsters are random but can stick to a them.

// Deeper in the dungoen, the decks will be themed closer to whoever
// the big bad is. Ie., if it's a balrog, have more fire monsters and
// hell hounds and such. 

// A level with a miniboss can add cards to a deck. An undead miniboss
// will add more undead to the deck for instance.

// (This is currently a pie-in-the-sky idea)

class MonsterDeck
{
  public List<string> Monsters { get; set; } = [];
  public Queue<int> Indexes { get; set; } = [];

  public void Reshuffle(Rng rng)
  {
    List<int> indexes = [];
    for (int i = 0; i < Monsters.Count; i++)
      indexes.Add(i);
    indexes.Shuffle(rng);
    Indexes = new Queue<int>(indexes);
  }

  public override string ToString()
  {
    return $"{string.Join(',', Monsters)}|{string.Join(',', Indexes)}";
  }

  public static MonsterDeck FromString(string str)
  {
    var parts = str.Split('|');
    return new MonsterDeck 
    { 
      Monsters = [.. parts[0].Split(',')],
      Indexes = parts[1] == "" ? [] : new Queue<int>(parts[1].Split(',').Select(int.Parse)) 
    };
  }
}

class DeckBuilder
{  
  // The upper levels won't really follow theme, but we will choose a preference 
  // for goblin dominated or kobold dominated

  static MonsterDeck ReadDeck(string deckname, int level)
  {
    MonsterDeck deck = new();
   
    // Someday in the future I'll need to check for invalid data files...
    var lines = File.ReadAllLines(ResourcePath.GetDataFilePath($"{deckname}.txt"));
    int j = 0;
    while (j < lines.Length && !lines[j].Equals($"LEVEL {level}", StringComparison.CurrentCultureIgnoreCase))
    {
      ++j;
    }
    ++j;

    string[] monsters = lines[j].Split(',');
    foreach (string m in monsters)
    {
      int k = m.Trim().LastIndexOf(' ');
      string monster = m[..k];
      if (!int.TryParse(m[k..], out int count))
        count = 1;
      for (int i = 0; i < count; ++i)
        deck.Monsters.Add(monster);
    }
    
    return deck;
  }

  public static List<MonsterDeck> MakeDecks(string earlyMainOccupant, VillainType villain, Rng rng)
  {    
    List<MonsterDeck> decks = [];

    // Sorry, I just think of dungeon levels as 1-indexed instead of 0-indexed
    for (int lvl = 1; lvl <= 5; lvl++)
    {
      MonsterDeck deck = ReadDeck(earlyMainOccupant, lvl);
      deck.Reshuffle(rng);
      decks.Add(deck);
    }

    for (int lvl = 6; lvl <= 10; lvl++)
    {
      MonsterDeck deck = ReadDeck("midlevel", lvl);

      if (villain == VillainType.FieryDemon)
      {
        deck.Monsters.Add("flame beetle");
        deck.Monsters.Add("flame beetle");
      }
      
      deck.Reshuffle(rng);
      decks.Add(deck);
    }

    return decks;
  }
}