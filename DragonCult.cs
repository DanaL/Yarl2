
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

using System.Security.Cryptography;
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

  static void MakeCultist(Actor cultist, Random rng)
  {
    NameGenerator ng = new(rng, Util.KoboldNamesFile);
    cultist.Name = ng.GenerateName(rng.Next(4, 7)).Capitalize();
    cultist.Traits.Add(new DialogueScriptTrait() { ScriptFile = "kobold_cultist.txt" });
    cultist.Traits.Add(new NamedTrait());
    cultist.Appearance = Appearance(rng);
    cultist.Glyph = cultist.Glyph with { Lit = Colours.SOFT_RED };
  }

  public static void MakeCultLeader(Actor leader, Random rng)
  {
    NameGenerator ng = new(rng, Util.KoboldNamesFile);
    leader.Name = ng.GenerateName(rng.Next(4, 7)).Capitalize();
    leader.Traits.Add(new DialogueScriptTrait() { ScriptFile = "kobold_cultist_priest.txt" });
    leader.Traits.Add(new NamedTrait());
    leader.Appearance = Appearance(rng);
  }

  public static bool OfferGold(GameState gs, Item zorkmids, Loc loc)
  {
    Loc effigyLoc = Loc.Nowhere;
    foreach (Loc adj in Util.Adj4Locs(loc))
    {
      if (gs.ObjDb.ItemsAt(adj).Where(i => i.Name == "dragon effigy").Any())
      {
        effigyLoc = adj;
        break;
      }
    }

    if (effigyLoc == Loc.Nowhere)
      return false;

    gs.UIRef().AlertPlayer("The coins disappear and you hear a pleased growl!");
    gs.ObjDb.RemoveItemFromGame(loc, zorkmids);

    if (gs.Player.Stats.TryGetValue(Attribute.GoldSacrificed, out var donationStat))
    {
      donationStat.SetMax(donationStat.Curr + zorkmids.Value);
    }
    else
    {
      gs.Player.Stats[Attribute.GoldSacrificed] = new Stat(zorkmids.Value);
    }

    int cultLevel = 0;
    if (gs.Player.Stats.TryGetValue(Attribute.KoboldCultLevel, out var cultLevelStat))
    {
      cultLevel = cultLevelStat.Curr;
    }

    int goldDonated = gs.Player.Stats[Attribute.GoldSacrificed].Curr;

    if (goldDonated > 100 && cultLevel == 0)
    {
      gs.UIRef().AlertPlayer("We appreciate the pledging of your soul and service!");
      gs.Player.Stats[Attribute.KoboldCultLevel] = new Stat(1);

      foreach (Actor actor in gs.ObjDb.AllActors())
      {
        if (actor.Traits.OfType<WorshiperTrait>().FirstOrDefault() is WorshiperTrait wt && wt.AltarLoc == effigyLoc)
        {
          actor.Traits.Add(new FriendlyMonsterTrait());

          if (actor.Name == "kobold")
          {
            MakeCultist(actor, gs.Rng);
          }
          else if (actor.Name == "kobold soothsayer")
          {
            MakeCultLeader(actor, gs.Rng);
          }
        }
      }

      return true;
    }

    if (goldDonated > 150 && cultLevel == 1 && gs.Rng.NextDouble() < 0.333)
    {
      gs.UIRef().AlertPlayer("My beloved servant!");
      gs.Player.Stats[Attribute.KoboldCultLevel] = new Stat(2);
    }

    if (cultLevel >= 2 && zorkmids.Value >= 25 && !gs.Player.HasActiveTrait<DragonCultBlessingTrait>())
    {
      gs.UIRef().AlertPlayer("Savour this taste of the power of dragonkind!");
      gs.UIRef().SetPopup(new Popup("Savour this taste of the power of dragonkind!", "", -1, -1));

      DragonCultBlessingTrait cultBlessing = new() 
      { 
        SourceId = Constants.DRAGON_GOD_ID, ExpiresOn = gs.Turn + 2000, 
        OwnerID = gs.Player.ID
      };

      cultBlessing.Apply(gs.Player, gs);
    }

    return true;
  }

  public static (string, int) CreateQuest(GameState gs)
  {
    NameGenerator ng = new(gs.Rng, Util.KoboldNamesFile);
    string ogreName = ng.GenerateName(gs.Rng.Next(5, 8)).Capitalize();

    Actor ogre = MonsterFactory.Get("ogre", gs.ObjDb, gs.Rng);
    ogre.Name = ogreName;
    ogre.Traits.Add(new NamedTrait());
    ogre.Glyph = ogre.Glyph with { Lit = Colours.LIGHT_BLUE, Unlit = Colours.BLUE };

    // Let's guarantee the quest boss always drops an ogre liver
    foreach (Trait t in ogre.Traits)
    {
      if (t is DropTrait drop)
        drop.Chance = 100;
    }

    int level = gs.CurrLevel + gs.Rng.Next(1, 3);
    Map map = gs.CurrentDungeon.LevelMaps[level];
    List<Loc> locs = map.ClearFloors(gs.CurrDungeonID, level, gs.ObjDb);
    Loc ogreLoc = locs[gs.Rng.Next(locs.Count)];
    gs.ObjDb.AddNewActor(ogre, ogreLoc);

    return (ogreName, level);
  }
}