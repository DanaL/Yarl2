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
  }

  public static void CrimsonKingShrineFoyer(GameState gs)
  {
    string s = "So a mortal enters my shrime!";
    gs.UIRef().AlertPlayer(s);
    gs.UIRef().SetPopup(new Popup(s, "", -1, -1));
  }
}
