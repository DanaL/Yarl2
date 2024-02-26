
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

class PlayerCreator
{
    static PlayerClass PickClass(UserInterface ui)
    {
        List<string> menu = [
"Which role will you assume this time?",
"",
" (1) Orcish Reaver                 (2) Dwarven Stalwart",
"",
" An intimidating orc warrior,      A dwarf paladin oath-sworn",
" trained to defend their clan in   to root out evil. Strong knights",
" battle. Fierce and tough.         who can call upon holy magic.",
"                                "                                 
        ];
        var options = new HashSet<char>() { '1', '2' };
        char choice = ui.FullScreenMenu(menu, options);

        return choice switch
        {
            '1' => PlayerClass.OrcReaver,
            _ => PlayerClass.DwarfStalwart
        };

    }

    // I am very bravely breaking from D&D traidtion and I'm just going to 
    // store the stat's modifier instead of the score from 3-18 :O
    static int StatRollToMod(int roll)
    {
        if (roll < 4)
            return -4;
        else if (roll == 4 || roll == 5)
            return -3;
        else if (roll == 6 || roll == 7)
            return -2;
        else if (roll == 8 || roll == 9)
            return -1;
        else if (roll == 10 || roll == 11)
            return 0;
        else if (roll == 12 || roll == 13)
            return 1;
        else if (roll == 14 || roll == 15)
            return 2;
        else if (roll == 16 || roll == 17)
            return 3;
        else
            return 4;
    }

    // If I use this enough, move it to Utils?
    static int Roll3d6(Random rng) => rng.Next(1, 7) + rng.Next(1, 7) + rng.Next(1, 7);
    static int StatRoll(Random rng) => StatRollToMod(Roll3d6(rng));
    
    static Dictionary<Attribute, Stat> RollStats(PlayerClass charClass, Random rng)
    {
        // First, roll the basic stats
        var stats = new Dictionary<Attribute, Stat>()
        {
            { Attribute.Strength, new Stat(StatRoll(rng)) },
            { Attribute.Constituion, new Stat(StatRoll(rng)) },
            { Attribute.Dexterity, new Stat(StatRoll(rng)) },
            { Attribute.Piety, new Stat(StatRoll(rng)) }
        };
        
        // Now the class-specific stuff
        int roll, hp = 1;
        switch (charClass)
        {            
            case PlayerClass.OrcReaver:
                roll = StatRollToMod(6 + rng.Next(1, 7) + rng.Next(1, 7));
                if (roll > stats[Attribute.Strength].Curr)
                    stats[Attribute.Strength].SetMax(roll);
                hp = 15 + stats[Attribute.Constituion].Curr;
                stats.Add(Attribute.MeleeAttackBonus, new Stat(3));
                break;
            case PlayerClass.DwarfStalwart:
                // Should Stalwarts also be strength based?
                roll = StatRollToMod(6 + rng.Next(1, 7) + rng.Next(1, 7));
                if (roll > stats[Attribute.Strength].Curr)
                    stats[Attribute.Strength].SetMax(roll);

                if (stats[Attribute.Piety].Curr < 0)
                    stats[Attribute.Piety].SetMax(0);
                    
                hp = 12 + stats[Attribute.Constituion].Curr;
                stats.Add(Attribute.MeleeAttackBonus, new Stat(2));                
                break;
        }
        
        if (hp < 1)
            hp = 1;
        stats.Add(Attribute.HP, new Stat(hp));

        return stats;
    }

    public static Player NewPlayer(string playerName, GameObjectDB objDb, int startRow, int startCol, UserInterface ui, Random rng)
    {
        Player player = new(playerName)
        {
            Loc = new Loc(0, 0, startRow, startCol),
            CharClass = PickClass(ui)
        };
        player.Stats = RollStats(player.CharClass, rng);

        objDb.Add(player);
        var spear = ItemFactory.Get("spear", objDb);
        spear.Adjectives.Add("old");
        spear.Equiped = true;
        player.Inventory.Add(spear, player.ID);
        var armour = ItemFactory.Get("leather armour", objDb);
        armour.Adjectives.Add("battered");
        armour.Equiped = true;
        player.Inventory.Add(armour, player.ID);
        player.Inventory.Add(ItemFactory.Get("dagger", objDb), player.ID);
        player.Inventory.Add(ItemFactory.Get("dagger", objDb), player.ID);
        player.Inventory.Add(ItemFactory.Get("dagger", objDb), player.ID);

        for (int i = 0; i < 10; i++)
        {
            player.Inventory.Add(ItemFactory.Get("torch", objDb), player.ID);
        }

        return player;
    }
}