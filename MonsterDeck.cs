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
    Indexes = new(indexes);
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
  public static List<MonsterDeck> ReadDeck(string deckname, Rng rng)
  {
    List<MonsterDeck> decks = [];
   
    // Someday in the future I'll need to check for invalid data files...
    int j = -1;
    foreach (string line in  File.ReadAllLines(ResourcePath.GetDataFilePath($"{deckname}.txt")))
    {
      if (line.StartsWith("LEVEL "))
      {
        ++j;
        decks.Add(new MonsterDeck());
      }
      else
      {        
        string[] monsters = line.Split(',');
        foreach (string m in monsters)
        {          
          int k = m.Trim().LastIndexOf(' ');
          string monster = m[..k];
          if (!int.TryParse(m[k..], out int count))
            count = 1;
          for (int i = 0; i < count; ++i)
            decks[j].Monsters.Add(monster);
        }
      }
    }

    foreach (MonsterDeck deck in decks)
      deck.Reshuffle(rng);
      
    return decks;
  }
}