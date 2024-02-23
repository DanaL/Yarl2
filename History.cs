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

// Herein is where I generate the history of the dungeon and the town
// (or were I hope that code will be once I figure out how to do it...)

// The types of beings who originally built the dungoen
enum OGRulerType
{
    ElfLord,
    DwarfLord,
    MadWizard
}

class RulerInfo
{
    public OGRulerType Type { get; set; }
    public string Name { get; set; }
    public string PrefixTitle { get; set; }
    public string SuffixTitle { get; set; }

    public string FullName => $"{PrefixTitle} {Name} {SuffixTitle}".Trim();
}

class History 
{
    private Random _rng;

    public History(Random rng)
    {
        _rng = rng;
    }

    public void CalcDungeonHistory()
    {
        // Okay, we need to know:
        //  1) Who the ruler was/who founded the dungeon
        //  2) Generate a few events in their life
        //  3) How did the dungeon originally falll to ruin

        var type = _rng.Next(3) switch
        {
            0 => OGRulerType.ElfLord,
            1 => OGRulerType.DwarfLord,
            _ => OGRulerType.MadWizard
        };

        var nameGen = new NameGenerator(_rng);
        var name = nameGen.GenerateName(_rng.Next(5, 10)).Capitalize();

        var ruler = new RulerInfo()
        {
            Type = type,
            Name = name,
            PrefixTitle = nameGen.PickTitle(),
            SuffixTitle = nameGen.Suffix()
        };

        Console.WriteLine(ruler.FullName);
    }
}