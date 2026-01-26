// Delve - A roguelike computer RPG
// Written in 2026s by Dana Larose <ywg.dana@gmail.com>
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
}
