﻿// Yarl2 - A roguelike computer RPG
// Written in 2024 by Dana Larose <ywg.dana@gmail.com>
//
// To the extent possible under law, the author(s) have dedicated all copyright
// and related and neighboring rights to this software to the public domain
// worldwide. This software is distributed without any warranty.
//
// You should have received a copy of the CC0 Public Domain Dedication along 
// with this software. If not, 
// see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System.Text;

// My low-budget, store-brand random name generator

namespace Yarl2
{
  class NameGenerator
  {
    Rng _rng;
    List<(char, int)> _starts;
    List<(string, int)> _pairs;

    string[] _ranks = [ "king", "queen", "viscount", "marquess", "baron", "lord", "lady",
        "sovereign", "vizier", "duke", "grand duke", "archduke", "prince", "princess", "earl",
        "despot", "count", "countess", "baroness", "elder", "justice", "judge", "tyrant" ];
    string[] _adjectives = [ "seeker", "ruler", "conqueror", "reaver", "saviour", "friend",
        "patron", "protector", "beloved"];
    string[] _domains = [ "mists", "the heavens", "spring", "summer", "fall", "winter", "wisdom",
        "faith", "faith", "peace", "the west", "the north", "the south", "the skies", "the stars",
        "the provinces", "the faithful", "autmun rains", "the fearful" ];
    string[] _titles = [ "strong", "mighty", "unbowed", "forgotten", "cursed", "twice cursed",
         "thrice cursed", "beloved", "praised", "glorious", "sorrowful", "vengeful", "redeemer", "fallen",
         "faithful", "beautiful", "radiant", "awful", "terrible", "wise", "just", "reaver" ];

    string[] _villainPrefix = ["black", "salty", "mini-boss", "snarling", "stinky", "big", "little", 
         "ugly", "fishy", "one eye", "iron jaw", "merciless", "drooling", "haunted"];
    string[] _villainSuffix = ["the feared", "the terrible", "the cruel", "the drunk", "the vile",
         "the weasel", "the wolf", "the shadow", "the fox", "the butcher", "the blade", "the axe",
         "the shark", "the mad"];

    // TODO: replace with something less lame...
    public static string TownName(Rng rng)
    {
      string[] prefixes = [
          "Upper", "Lower", "North", "South", "East", "West",
                "High", "Low", "Mid", "Far", "Lost"
      ];

      string[] historicalPrefixes = [
          "New", "Old", "Great", "Little", "Kings", "Queens", "Grand"
      ];

      // Main elements that work both as prefixes and standalone
      string[] main = [
          "River", "Lake", "Wood", "Forest", "Hill", "Dale",
                "Green", "White", "Black",
                "Stone", "Iron", "Silver", "Golden",
                "Raven", "Deer", "Wolf", "Eagle",
                "Oak", "Elm", "Rose", "Thorn"
      ];

      // Suffixes with rules for spacing
      (string, bool)[] suffixes = [
          // Settlement suffixes (no space)
          ("ton", false), ("ford", false), ("burgh", false), ("vale", false),
                ("dale", false), ("wood", false), ("moor", false), ("field", false),
                // Descriptive suffixes (with space)
                ("Valley", true), ("Peak", true), ("Shore", true), ("Heath", true),
                ("Meadow", true), ("Creek", true), ("Ridge", true)
      ];

      string name = "";

      if (rng.NextDouble() < 0.6)
      {
        var allPrefixes = rng.NextDouble() < 0.6 ? prefixes : historicalPrefixes;
        name = allPrefixes[rng.Next(allPrefixes.Length)] + " ";
      }

      // Main name component (always present)
      name += main[rng.Next(main.Length)];

      // 70% chance of suffix, guaranteed if name has no space yet
      if (rng.NextDouble() < 0.7 || !name.Contains(' '))
      {
        var (suffix, needsSpace) = suffixes[rng.Next(suffixes.Length)];
        name += needsSpace ? " " + suffix : suffix;
      }

      return name;
    }

    public static string GenerateTavernName(Rng rng)
  {
    string[] adjectives = 
      [
        "Rusty", "Golden", "Silver", "Bronze", "Iron", "Wooden", "Broken",
        "Sleeping", "Dancing", "Laughing", "Drunken", "Thirsty", "Hungry",
        "Wandering", "Pickled", "Jolly", "Merry", "Lucky", "Brave", "Wild",
        "Soggy", "Rusty", "Black"
      ];

      string[] nouns = 
      [
        "Dragon", "Knight", "Sword", "Shield", "Barrel", "Duck", "Wizard", "Tortoise",
        "Dwarf", "Elf", "Ranger", "Warrior", "Rogue", "Bear", "Lion", "Pike",
        "Horse", "Stag", "Wolf", "Boar", "Eagle", "Hippo", "Rabbit", "Woodchuck"
      ];

      string[] endings = 
      [
        "Inn", "Tavern", "Home", "Lodge", "House", "Pub", "Arms",
        "& Flagon", "& Barrel", "& Tankard", "& Cup", "& Fork", "& Quill"
      ];

      // Different name patterns
      int pattern = rng.Next(4);
      return pattern switch
      {
          0 => $"the {adjectives[rng.Next(adjectives.Length)]} {nouns[rng.Next(nouns.Length)]}",
          1 => $"the {nouns[rng.Next(nouns.Length)]} {endings[rng.Next(endings.Length)]}",
          2 => $"the {adjectives[rng.Next(adjectives.Length)]} {endings[rng.Next(endings.Length)]}",
          _ => $"the {nouns[rng.Next(nouns.Length)]} & {nouns[rng.Next(nouns.Length)]}"
      };
    }

    public NameGenerator(Rng rng, string sourceFile)
    {
      _rng = rng;
      var names = File.ReadAllLines(sourceFile)
                      .Select(n => n.ToLower()).ToList();

      Dictionary<char, int> starts = [];
      Dictionary<string, int> pairFrequencies = [];
      foreach (var name in names)
      {
        char c = name[0];
        if (!starts.ContainsKey(c))
          starts[c] = 0;
        starts[c]++;

        for (int j = 1; j < name.Length; j++)
        {
          string pair = $"{name[j - 1]}{name[j]}";
          if (!pairFrequencies.ContainsKey(pair))
            pairFrequencies[pair] = 0;
          pairFrequencies[pair]++;
        }
      }

      _starts = [.. starts.Select(kvp => (kvp.Key, kvp.Value)).OrderByDescending(p => p.Value)];
      _pairs = [.. pairFrequencies.Select(kvp => (kvp.Key, kvp.Value)).OrderByDescending(p => p.Value)];
    }

    string RandomSyllable(char startsWith)
    {
      var candidates = _pairs.Where(p => p.Item1.StartsWith(startsWith))
                             .OrderByDescending(p => p.Item2)
                             .ToArray();
      var total = candidates.Select(p => p.Item2).Sum();
      var x = _rng.Next(total);

      var sum = 0;
      var j = -1;

      do
      {
        sum += candidates[++j].Item2;
      }
      while (sum < x);

      return candidates[j].Item1;
    }

    private char RandomStart()
    {
      var total = _starts.Select(p => p.Item2).Sum();
      var x = _rng.Next(total);

      var sum = 0;
      var j = -1;
      do
      {
        sum += _starts[++j].Item2;
      }
      while (sum < x);

      return _starts[j].Item1;
    }

    public string BossName()
    {
      StringBuilder sb = new();

      double roll = _rng.NextDouble();

      if (roll <= 0.5)
      {
        sb.Append(_villainPrefix[_rng.Next(_villainPrefix.Length)]);
        sb.Append(' ');
      }

      int len = _rng.Next(5, 9);
      sb.Append(GenerateName(len));

      if (roll > 0.5)
      {
        sb.Append(' ');
        sb.Append(_villainSuffix[_rng.Next(_villainSuffix.Length)]);
      }

      return sb.ToString().CapitalizeWords();
    }

    public string GenerateName(int length)
    {
      HashSet<char> vowels = ['a', 'e', 'i', 'o', 'u', 'y'];
      string first;
      do
      {
        first = RandomSyllable(RandomStart());
        if (vowels.Contains(first[0]) || vowels.Contains(first[1]) || first == "th")
          break;
      }
      while (true);

      var sb = new StringBuilder(first);
      char ch = first[1];

      do
      {
        var syll = RandomSyllable(ch);
        ch = syll[1];
        sb.Append(ch);
      }
      while (sb.Length < length);

      return sb.ToString();
    }

    public string PickTitle() => _ranks[_rng.Next(_ranks.Length)].Capitalize();

    public string PickEpithet()
    {
      if (_rng.NextDouble() < 0.333)
      {
        var adj = _adjectives[_rng.Next(_adjectives.Length)].Capitalize();
        var domain = _domains[_rng.Next(_domains.Length)];
        return $"{adj.Capitalize()} of {domain.Capitalize()}";
      }
      else
      {
        var title = _titles[_rng.Next(_titles.Length)];
        return $"the {title}";
      }
    }
  }
}
