// Delve - A roguelike computer RPG
// Written in 2026 by Dana Larose <ywg.dana@gmail.com>
//
// To the extent possible under law, the author(s) have dedicated all copyright
// and related and neighboring rights to this software to the public domain
// worldwide. This software is distributed without any warranty.
//
// You should have received a copy of the CC0 Public Domain Dedication along 
// with this software. If not, 
// see <http://creativecommons.org/publicdomain/zero/1.0/>.

namespace Yarl2;

class MoonDaughter
{
  static void Cleric(Rng rng, GameObjectDB objDb)
  {
    NameGenerator ng = new(rng, Util.NamesFile);
    Mob cleric = new()
    {
      Name = ng.GenerateName(8),
      Appearance = "A cleric whose face is concealed by a deep hood. They are suffused with a faint silver glow.",
      Glyph = new Glyph('@', Colours.GREY, Colours.DARK_GREEN, Colours.BLACK, false)
    };
    cleric.Stats[Attribute.HP] = new Stat(50);
    cleric.Traits.Add(new VillagerTrait());
    cleric.Traits.Add(new NamedTrait());
    cleric.Traits.Add(new IntelligentTrait());
    cleric.Traits.Add(new DialogueScriptTrait() { ScriptFile = "moon_daughter_cleric.txt" });
    cleric.SetBehaviour(new MoonDaughterClericBehaviour());
    cleric.Traits.Add(new BehaviourTreeTrait() { Plan = "MoonClericPlan" });
    cleric.Traits.Add(new LightSourceTrait() { Radius = 1, OwnerID = cleric.ID, FgColour = Colours.ICE_BLUE, BgColour = Colours.MYSTIC_AURA });
    cleric.Loc = Loc.Nowhere;

    objDb.Add(cleric);
  }
}