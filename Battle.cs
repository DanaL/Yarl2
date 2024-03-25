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

using System.Numerics;

namespace Yarl2;

enum DamageType
{
    Slashing,
    Piercing,
    Blunt,
    Fire,
    Cold,
    Poison
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

    static bool ResolveImpale(Actor attacker, Actor target, int attackRoll, GameState gs, ActionResult result, Random rng)
    {
        bool success = false;

        // is there an opponent behind the primary target to impale?
        int diffRow = (attacker.Loc.Row - target.Loc.Row) * 2;
        int diffCol = (attacker.Loc.Col - target.Loc.Col) * 2;
        Loc checkLoc = attacker.Loc with { Row = attacker.Loc.Row - diffRow, Col = attacker.Loc.Col - diffCol };
        Actor? occ = gs.ObjDB.Occupant(checkLoc);
        if (occ is not null && attackRoll >= occ.AC)
        {
            ResolveMeleeHit(attacker, occ, gs, result, Verb.Impale, rng);
            success = true;
        }

        return success;
    }

    static bool ResolveCleave(Actor attacker, Actor target, int attackRoll, GameState gs, ActionResult result, Random rng)
    {
        bool success = false;
        // Check for any cleave targets Adj4 to main target and Adj to attacker
        var adjToAtt = new HashSet<(int, int)>(Util.Adj8Sqs(attacker.Loc.Row, attacker.Loc.Col));
        foreach (var sq in Util.Adj4Sqs(target.Loc.Row, target.Loc.Col))
        {
            var loc = target.Loc with { Row = sq.Item1, Col = sq.Item2 };
            var occ = gs.ObjDB.Occupant(loc);
            if (occ is not null && occ.ID != attacker.ID && adjToAtt.Contains((occ.Loc.Row, occ.Loc.Col)))
            {
                if (attackRoll >= occ.AC)
                {
                    ResolveMeleeHit(attacker, occ, gs, result, Verb.Cleave, rng);
                    success = true;
                }
            }
        }

        return success;
    }

    static void ResolveMissileHit(Actor attacker, Actor target, Item ammo, GameState gs, ActionResult result, Random rng)
    {
        List<(int, DamageType)> dmg = [];
        foreach (var trait in ammo.Traits)
        {
            if (trait is DamageTrait dt)
            {
                var d = new Damage(dt.DamageDie, dt.NumOfDie, dt.DamageType);
                dmg.Add(DamageRoll(d, rng));
            }
            if (trait is PoisonerTrait poison)
            {
                ApplyPoison(poison, target, gs, rng);
            }
        }

        int bonusDamage = 0;
        if (attacker.Stats.TryGetValue(Attribute.Dexterity, out var dex))
            bonusDamage += dex.Curr;
        if (attacker.Stats.TryGetValue(Attribute.MissileDmgBonus, out var mdb))
            bonusDamage += mdb.Curr;

        Message msg = MessageFactory.Phrase(ammo.ID, Verb.Hit, target.ID, 0, true, target.Loc, gs);
        int hpLeft = target.ReceiveDmg(dmg, bonusDamage);
        ResolveHit(attacker, target, hpLeft, result, msg, gs);
    }

    static void ApplyPoison(PoisonerTrait source, Actor victim, GameState gs, Random rng)
    {
        // We won't apply multiple poison statuses to one victim. Although maybe I
        // should replace the weaker poison with the stronger one?
        if (victim.HasTrait<PoisonedTrait>())
            return;

        bool conCheck = victim.AbilityCheck(Attribute.Constitution, source.DC, rng);
        if (!conCheck)
        {
            var poisoned = new PoisonedTrait()
            {
                DC = source.DC,
                Strength = source.Strength,
                VictimID = victim.ID
            };
            victim.Traits.Add(poisoned);
            gs.RegisterForEvent(UIEventType.EndOfRound, poisoned);
        }
    }

    static void ResolveMeleeHit(Actor attacker, Actor target, GameState gs, ActionResult result, Verb attackVerb, Random rng)
    {
        // Need to handle the case where the player isn't currently wielding a weapon...
        List<(int, DamageType)> dmg = [];
        foreach (var d in attacker.MeleeDamage())
        {
            var dr = DamageRoll(d, rng);
            dmg.Add(dr);
        }
 
        int bonusDamage = 0; // this is separate from the damage types because, say,
                                // a flaming sword that does 1d8 slashing, 1d6 fire has
                                // two damage types but we only want to add the player's
                                // strength modifier once
        if (attacker.Stats.TryGetValue(Attribute.Strength, out var str))
            bonusDamage += str.Curr;
        if (attacker.Stats.TryGetValue(Attribute.MeleeDmgBonus, out var mdb))
            bonusDamage += mdb.Curr;
        if (attacker.HasActiveTrait<RageTrait>())
            bonusDamage += rng.Next(1, 7) + rng.Next(1, 7);

        if (attacker.HasTrait<PoisonerTrait>())
        {
            var poison = attacker.Traits.OfType<PoisonerTrait>().First();
            ApplyPoison(poison, target, gs, rng);            
        }

        Message msg = MessageFactory.Phrase(attacker.ID, attackVerb, target.ID, 0, true, target.Loc, gs);
        int hpLeft = target.ReceiveDmg(dmg, bonusDamage);
        ResolveHit(attacker, target, hpLeft, result, msg, gs);
    }

    static void ResolveHit(Actor attacker, Actor target, int hpLeft, ActionResult result, Message msg, GameState gs)
    {        
        if (hpLeft < 1)
        {
            if (target is Player)
            {
                result.PlayerKilled = true;
                msg = new Message(msg.Text + $" Oh noes you've been killed by {attacker.Name.IndefArticle()} :(", target.Loc);                    
            }
            else 
            {
                var verb = target.HasTrait<PlantTrait>() ? Verb.Destroy : Verb.Kill;
                var plural = target.HasTrait<PluralTrait>();
                Message killMsg = MessageFactory.Phrase(target.ID, Verb.Etre, verb, plural, true, target.Loc, gs);
                msg = new Message(msg.Text + " " + killMsg.Text, target.Loc);

                if (attacker.ID == gs.Player.ID && target is Monster m)
                {
                    int xpv = m.Stats[Attribute.XPValue].Curr;
                    attacker.Stats[Attribute.XP].ChangeMax(xpv);                    
                }
            }

            gs.ActorKilled(target);
        }

        var hitAnim = new HitAnimation(target.ID, gs, target.Loc, Colours.FX_RED);
        gs.UI.RegisterAnimation(hitAnim);

        result.Messages.Add(msg);
    }

    public static ActionResult MeleeAttack(Actor attacker, Actor target, GameState gs, Random rng)
    {
        var result = new ActionResult() { Complete = true, EnergyCost = 1.0 };

        int roll = AttackRoll(rng) + attacker.TotalMeleeAttackModifier();
        if (roll >= target.AC)
        {
            ResolveMeleeHit(attacker, target, gs, result, Verb.Hit, rng);
            
            // in the future I'll need to make sure the other targets aren't friendly/allies
            // should I limit Impale and Cleave to weapon types? Maybe Slashing and Bludgeoning
            // can Cleave and Piercing can Impale?            
            bool specialAttack = false;
            if (attacker.HasActiveTrait<CleaveTrait>()) // && rng.NextDouble() < 0.3333)
            {
                specialAttack = ResolveCleave(attacker, target, roll, gs, result, rng);
            }
            if (!specialAttack && attacker.HasActiveTrait<ImpaleTrait>())
            {
                specialAttack = ResolveImpale(attacker, target, roll, gs, result, rng);
            }
        }
        else
        {
            Message msg = MessageFactory.Phrase(attacker.ID, Verb.Miss, target.ID, 0, true, target.Loc, gs);
            result.Messages.Add(msg);
        }

        return result;
    }

    public static ActionResult MissileAttack(Actor attacker, Actor target, GameState gs, Item ammo, Random rng)
    {
        var result = new ActionResult() { Complete = false, EnergyCost = 1.0 };

        int roll = AttackRoll(rng) + attacker.TotalMissileAttackModifier(ammo);
        if (roll >= target.AC)
        {
            ResolveMissileHit(attacker, target, ammo, gs, result, rng);
            result.Complete = true;
        }
        else
        {
            Message msg = MessageFactory.Phrase(ammo.ID, Verb.Miss, target.ID, 0, true, target.Loc, gs);
            result.Messages.Add(msg);
        }

        return result;
    }
}
