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

enum DamageType
{
    Slashing,
    Piercing,
    Blunt,
    Fire,
    Cold
}

record struct Damage(int Die, int NumOfDie, DamageType Type);

class Battle
{
    // We'll average two d20 rolls to make combat rolls a bit less swinging/
    // evenly distributed
    static int AttackRoll(Random rng) => (rng.Next(1, 21) + rng.Next(1, 21)) / 2;

    static (int, DamageType) DamageRoll(Damage dmg, Random rng)
    {
        int total = 0;
        for (int r = 0; r < dmg.NumOfDie; r++)
            total += rng.Next(dmg.Die) + 1;
        return (total, dmg.Type);
    }

    public static ActionResult MeleeAttack(Actor attacker, Actor target, GameState gs, Random rng)
    {
        var result = new ActionResult() { Successful = true, EnergyCost = 1.0 };

        int roll = AttackRoll(rng) + attacker.TotalMeleeAttackModifier();
        if (roll >= target.AC)
        {
            // Need to handle the case where the player isn't currently wielding a weapon...
            var dmg = attacker.MeleeDamage()
                              .Select(d => DamageRoll(d, rng));

            int bonusDamage = 0; // this is separate from the damage types because, say,
                                 // a flaming sword that does 1d8 slashing, 1d6 fire has
                                 // two damage types but we only want to add the player's
                                 // strength modifier once
            if (attacker.Stats.TryGetValue(Attribute.Strength, out var str))
                bonusDamage += str.Curr;

            Message msg = MessageFactory.Phrase(attacker.ID, Verb.Hit, target.ID, 0, true, target.Loc, gs);

            int hpLeft = target.ReceiveDmg(dmg, bonusDamage);

            if (hpLeft < 1)
            {
                Message killMsg = MessageFactory.Phrase(target.ID, Verb.Etre, Verb.Kill, true, target.Loc, gs);
                msg = new Message(msg.Text + " " + killMsg.Text, target.Loc);

                Console.WriteLine($"{target.Name} {target.ID} is killed!");

                gs.ActorKilled(target);
            }

            Console.WriteLine($"{target.Name} has {hpLeft} HP left...");
            
            result.Message = msg;
        }
        else
        {
            Message msg = MessageFactory.Phrase(attacker.ID, Verb.Miss, target.ID, 0, true, target.Loc, gs);
            result.Message = msg;
        }

        return result;
    }
}
