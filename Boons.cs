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

class InfernalBoons
{
  public static void Sacrifice(GameState gs, Loc altarLoc)
  {
    Item? weapon = gs.Player.Inventory.ReadiedWeapon();
    Item? bow = gs.Player.Inventory.ReadiedBow();
    UserInterface ui = gs.UIRef();

    string txt = "";
    int i = weapon is not null || bow is not null ? 4 : 3;
    int roll = gs.Rng.Next(i);
    switch (roll)
    {
      case 0:
        Stat str = gs.Player.Stats[Attribute.Strength];
        str.SetMax(str.Curr + 1);
        Stat con = gs.Player.Stats[Attribute.Constitution];
        con.SetMax(con.Curr + 1);
        gs.Player.Stats[Attribute.HP].ChangeMax(5);
        gs.Player.Stats[Attribute.HP].Change(5);
        txt = "\"My supplicants must be mighty if they are to serve. I shall give you a sliver of my potency!\"\n\nYou feel flush with infernal vigour!";
        txt += "\n\nYou feel [BRIGHTRED stronger]! You feel [BRIGHTRED healther]!";
        ui.AlertPlayer("You feel stronger!");
        ui.AlertPlayer("You feel healthier!");        
        break;
      case 1:
        txt = "\"Those in my service shall not want for wealth!\"\n\nA pile of [YELLOW coins] appears on the altar!";
        Item zorkmids = ItemFactory.Get(ItemNames.ZORKMIDS, gs.ObjDb);
        zorkmids.Value = 100;
        gs.ObjDb.SetToLoc(altarLoc, zorkmids);
        break;
    }

    ui.SetPopup(new Popup(txt, "A harsh whisper", -1, -1));
    // stat boost
    // wealth
    // protection
    // weapon skill
  }
}