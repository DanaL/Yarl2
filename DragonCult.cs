
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

namespace Yarl2;

class Kobold
{
  static string Appearance(Random rng)
  {
    StringBuilder sb = new();

    int roll = rng.Next(4);
    if (roll == 0)
      sb.Append("A kobold ");
    else if (roll == 1)
      sb.Append("A young kobold ");
    else if (roll == 2)
      sb.Append("An adult kobold ");
    else
      sb.Append("An ageing kobold ");

    roll = rng.Next(5);
    if (roll == 0)
      sb.Append("with striped scales ");
    else if (roll == 1)
      sb.Append("with mottled scales ");
    else if (roll == 2)
      sb.Append("with calico scales ");
    else if (roll == 3)
      sb.Append("with spotted scales ");
    else if (roll == 4)
      sb.Append("with glittering scales ");

    roll = rng.Next(6);
    if (roll == 0)
      sb.Append("and an eye-patch.");
    else if (roll == 1)
      sb.Append("and a curled tail.");
    else if (roll == 2)
      sb.Append("and a missing fang.");
    else if (roll == 3)
      sb.Append("and a long tail.");
    else if (roll == 4)
      sb.Append("and an angry scar.");
    else if (roll == 5)
      sb.Append("and a stubby snout.");

    return sb.ToString();
  }

  public static void MakeCultist(Actor cultist, Random rng)
  {
    NameGenerator ng = new(rng, Util.KoboldNamesFile);
    cultist.Name = ng.GenerateName(rng.Next(4, 7)).Capitalize();
    cultist.Traits.Add(new DialogueScriptTrait() { ScriptFile = "kobold_cultist.txt" });
    cultist.Traits.Add(new NamedTrait());
    cultist.Appearance = Appearance(rng);
  }

  public static void MakeCultLeader(Actor leader, Random rng)
  {
    NameGenerator ng = new(rng, Util.KoboldNamesFile);
    leader.Name = ng.GenerateName(rng.Next(4, 7)).Capitalize();
    leader.Traits.Add(new DialogueScriptTrait() { ScriptFile = "kobold_cultist_priest.txt" });
    leader.Traits.Add(new NamedTrait());
    leader.Appearance = Appearance(rng);
  }
}