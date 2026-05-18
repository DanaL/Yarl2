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

class Faiths
{
  public static void RemoveOtherFaithBlessings<T>(GameState gs) where T : BlessingTrait
  {    
    List<Trait> currBlessings = [.. gs.Player.Traits.Where(t => t is BlessingTrait)];
    foreach (var t in currBlessings)
    {
      if (t is BlessingTrait bt && t is not T)
      {
        bt.Remove(gs);
      }
    }
  }

  public static void TricksterBlessing(Actor cleric, GameState gs)
  {
    RemoveOtherFaithBlessings<MoonDaughtersBlessingTrait>(gs);
    
    var blessing = new TricksterBlessingTrait() { OwnerID = gs.Player.ID };
    blessing.Apply(cleric, gs);
  }

  public static void DeceiverBlessing(Actor cleric, GameState gs)
  {
    RemoveOtherFaithBlessings<MoonDaughtersBlessingTrait>(gs);

    DeceiverBlessingTrait blessing = new() { OwnerID = gs.Player.ID };
    blessing.Apply(cleric, gs);
  }

  public static void VisitMoonDaughterLocation(GameState gs)
  {    
    if (gs.FactDb.FactCheck("MDSpotLastVisit") is SimpleFact mdlv)
    {
      ulong lastVisit = ulong.Parse(mdlv.Value);
      if (gs.Turn - lastVisit < 103)
        return;
    }
    else
    {
      mdlv = new SimpleFact() { Name = "MDSpotLastVisit", Value = "0" };
      gs.FactDb.Add(mdlv);
    }
    
    int visits = 0;
    if (gs.FactDb.FactCheck("MDSpotVisits") is SimpleFact mdv)
    {
      visits = int.Parse(mdv.Value);
    }
    else
    {
      mdv = new SimpleFact() { Name = "MDSpotVisits", Value = "0" };
      gs.FactDb.Add(mdv);
    }

    string msg;
    switch (visits)
    {
      case 0:
        msg = "You think you hear a faint whispering.";
        break;
      case 1:
        msg = "We too oppose Arioch, via our own means.";
        break;
      case 2:
        msg = "Step through shadows, draw not the attention of your foes.";
        Item potion = ItemFactory.Get(ItemNames.POTION_OBSCURITY, gs.ObjDb);
        gs.ObjDb.SetToLoc(gs.Player.Loc, potion);
        break;
      default:
        msg = "";
        break;
    }

    if (msg != "")    
    {
      gs.UIRef().AlertPlayer(msg);
      gs.UIRef().SetPopup(new Popup(msg, "", -1, -1));
      ++visits;
      mdv.Value = visits.ToString();
      mdlv.Value = gs.Turn.ToString();
    }
  }

  // Passing actor who is wielding the Crimson King's blade but I don't imagine
  // a scenario where it isn't the player
  public static void CrimsonKingSacrifice(Actor actor, GameState gs)
  {
    int totalSacrifices;
    if (actor.Stats.TryGetValue(Attribute.CrimsonKingSacrifice, out var sacrifices))
    {
      totalSacrifices = sacrifices.ChangeMax(1);
    }
    else
    {
      actor.Stats[Attribute.CrimsonKingSacrifice] = new Stat(1);
      totalSacrifices = 1;
    }

    if (gs.FactDb.FactCheck("CrimsonKingBladeId") is not SimpleFact bid)
      return;
    if (gs.ObjDb.GetObj(ulong.Parse(bid.Value)) is not Item blade)
      return;

    if (totalSacrifices == 25)
    {
      gs.UIRef().SetPopup(new Popup("A voice booms:\n\nThose who battle in My name are rewarded with greater power!", "", -1, -1));
      
      blade.Traits.Add(new DamageTrait() { DamageDie = 8, NumOfDie = 1, DamageType = DamageType.Electricity });
      gs.UIRef().AlertPlayer("The Blade now crackles with electricity.");
    }
    else if (totalSacrifices == 50)
    {
      gs.UIRef().SetPopup(new Popup("A voice booms:\n\nMy Blade shall now cast your foes aside!", "", -1, -1));
      if (!gs.Player.Traits.Any(t => t is KnockBackTrait && t.SourceId == blade.ID))      
        gs.Player.Traits.Add(new KnockBackTrait() { SourceId = blade.ID });      
    }
    else if (totalSacrifices == 75)
    {
      gs.UIRef().SetPopup(new Popup("A voice booms:\n\nMortal, the path of destruction thou has carved pleases me!", "", -1, -1));
      
      blade.Traits.Add(new ViciousTrait() { Scale = 1.25 });
      gs.UIRef().AlertPlayer("The Blade shudders and grows more keen.");
    }
  }

  public static void CrimsonKingShrineFoyer(GameState gs)
  {
    string s = "So a mortal enters my shrine!";
    gs.UIRef().AlertPlayer(s);
    gs.UIRef().SetPopup(new Popup(s, "", -1, -1));

    string religion = gs.Player.Religion;
    int faith = gs.Player.Faith;

    if (religion != "Crimson King" && faith > 1)
    {
      ulong bladeId = 0;
      if (gs.FactDb.FactCheck("CrimsonKingBladeId") is SimpleFact bid)
      {
        bladeId = ulong.Parse(bid.Value);
      }
      Loc altarLoc = Loc.Nowhere;
      if (gs.FactDb.FactCheck("CrimsonKingAltar") is LocationFact altar)
      {
        altarLoc = altar.Loc;
      }

      bool removeBlade = gs.ObjDb.ItemsAt(altarLoc).Any(i => i.ID == bladeId);
      if (gs.ObjDb.ItemsAt(altarLoc).Any(i => i.ID == bladeId) && gs.ObjDb.GetObj(bladeId) is Item blade)
      {
        gs.ObjDb.RemoveItemFromGame(altarLoc, blade);
      }
    }
  }

  public static void CKBladeRejectsPlayer(GameState gs, Loc loc, Item blade)
  {
    gs.UIRef().AlertPlayer("A voice booms: you are not worthy to wield My blade!");
    gs.Player.Inventory.RemoveByID(blade.ID, gs);
    gs.ItemDropped(blade, loc);
    gs.UIRef().AlertPlayer($"{blade.FullName.DefArticle().Capitalize()} falls to the ground.", gs, loc);
  }
}
