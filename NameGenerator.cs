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

using System.Text;

// My low-budget, store-brand random name generator

namespace Yarl2
{
    class NameGenerator
    {
        private Random _rng;
        private List<(char, int)> _starts;
        private List<(string, int)> _pairs;
    
        private string[] _ranks = [ "king", "queen", "viscount", "marquess", "baron", "lord", "lady",
            "sovereign", "vizier", "duke", "grand duke", "archduke", "prince", "princess", "earl",
            "despot", "count", "countess", "baroness", "elder", "justice", "judge", "tyrant" ];
        private string[] _adjectives = [ "seeker", "ruler", "conqueror", "reaver", "saviour", "friend",
            "patron", "protector", "beloved"];
        private string[] _domains = [ "mists", "the heavens", "spring", "summer", "fall", "winter", "wisdom",
            "faith", "faith", "peace", "the west", "the north", "the south", "the skies", "the stars",
            "the provinces", "the faithful", "autmun rains", "the fearful" ];
        private string[] _titles = [ "strong", "mighty", "unbowed", "forgotten", "cursed", "twice cursed", 
            "thrice cursed", "beloved", "praised", "glorious", "sorrowful", "vengeful", "redeemer", "fallen", 
            "faithful", "beaufiful", "radiant", "awful", "terrible", "wise", "just", "reaver" ];

        // TODO: replace with something less lame...
        public static string TownName(Random rng)
        {
            string[] prefixes = [ "Upper ", "Lower ", "North ", "South ", "East ", "West ", "New ", "Old "];
            string[] main = [ "Stone", "Black", "Green", "Red", "Deer", "Bar", "Burr", "Cor", "Ar", "Gold", "Silver", "Iron"];
            string[] suffixes = [ "ton", "town", " By-the-Sea", " Shore", " Downs", " Woods"];

            string name = "";
            if (rng.NextDouble() < 0.5)
                name = prefixes[rng.Next(prefixes.Length)];
            name += main[rng.Next(main.Length)];
            if (rng.NextDouble() < 0.75)
                name += suffixes[rng.Next(suffixes.Length)];

            return name;
        }

        public NameGenerator(Random rng, string sourceFile)
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

            _starts = starts.Select(kvp => (kvp.Key, kvp.Value))
                            .OrderByDescending(p => p.Value)
                            .ToList();
            _pairs = pairFrequencies.Select(kvp => (kvp.Key, kvp.Value))
                                     .OrderByDescending(p => p.Value)
                                     .ToList();
        }

        private string RandomSyllable(char startsWith)
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
