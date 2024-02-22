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
        private int _totalPairs;

        public NameGenerator(Random rng)
        {
            _rng = rng;
            var names = File.ReadAllLines("names.txt")
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
    }
}
