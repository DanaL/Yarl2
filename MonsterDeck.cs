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

  public void Reshuffle(Random rng)
  {
    List<int> indexes = [];
    for (int i = 0; i < Monsters.Count; i++)
      indexes.Add(i);
    indexes.Shuffle(rng);
    Indexes = new Queue<int>(indexes);
  }
}

class DeckBulder
{
  public static string EarlyMainOccupant { get; set; } = "";

  // The upper levels won't really follow theme, but we will choose a preference 
  // for goblin dominated or kobold dominated

  // I wonder if it would make more sense to have these in a text file and read them in?
  static MonsterDeck EarlyLevelDeck(int level, Random rng)
  {
    MonsterDeck deck = new();
    if (EarlyMainOccupant == "")
    {
      EarlyMainOccupant = rng.NextDouble() < 0.5 ? "kobold" : "goblin";
    }

    // Someday in the future I'll need to check for invalid data files...
    var lines = File.ReadAllLines($"data/{EarlyMainOccupant}.txt");
    int j = 0;
    while (j < lines.Length && !lines[j].Equals($"LEVEL {level}", StringComparison.CurrentCultureIgnoreCase))
    {
      ++j;
    }
    ++j;

    while (j < lines.Length && !lines[j].StartsWith("level", StringComparison.CurrentCultureIgnoreCase))
    {
      int k = lines[j].LastIndexOf(' ');
      string monster = lines[j][..k];
      if (!int.TryParse(lines[j][k..], out int count))
        count = 1;
      for (int i = 0; i < count; i++)
        deck.Monsters.Add(monster);
      ++j;
    }

    return deck;
  }

  public static List<MonsterDeck> MakeDecks(int startLevel, int depth, VillainType villain, Random rng)
  {
    List<MonsterDeck> decks = [];

    int lvl = startLevel;
    while (lvl < startLevel + depth)
    {
      var deck = EarlyLevelDeck(lvl, rng);
      deck.Reshuffle(rng);
      decks.Add(deck);

      ++lvl;
    }

    return decks;
  }
}