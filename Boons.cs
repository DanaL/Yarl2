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
    CorruptionTrait? corruption = gs.Player.Traits.OfType<CorruptionTrait>().FirstOrDefault();

    // Your first boon is free so that you get a taste for it
    if (gs.Rng.Next(7) == 0 || corruption is null)
    {
      SacrificialBoon(gs, altarLoc);
    }
    else
    {
      string s = "Your sacrifice vanishes in a puff of sulphur and ash.";
      gs.UIRef().SetPopup(new Popup(s, "", -1, -1));
    }

    if (corruption is not null)
    {
      corruption.Amt += 1;
    }
    else
    {
      gs.Player.Traits.Add(new CorruptionTrait() { Amt = 1});
    }
  }

  static void SacrificialBoon(GameState gs, Loc altarLoc)
  {
    Item? weapon = gs.Player.Inventory.ReadiedWeapon();
    Item? bow = gs.Player.Inventory.ReadiedBow();
    UserInterface ui = gs.UIRef();

    string txt = "";
    int i = weapon is not null || bow is not null ? 4 : 3;
    switch (gs.Rng.Next(i))
    {
      case 0:
        Stat str = gs.Player.Stats[Attribute.Strength];
        str.SetMax(str.Curr + 1);
        Stat con = gs.Player.Stats[Attribute.Constitution];
        con.SetMax(con.Curr + 1);
        gs.Player.Stats[Attribute.HP].ChangeMax(5);
        gs.Player.Stats[Attribute.HP].Change(5);
        txt = "\"My supplicants must be mighty if they are to serve. I grant you a sliver of my potency!\"\n\nYou feel flush with infernal vigour!";
        txt += "\n\nYou feel [BRIGHTRED stronger]! You feel [BRIGHTRED healther]!";
        ui.AlertPlayer("You feel stronger!");
        ui.AlertPlayer("You feel healthier!");        
        break;
      case 1:
        txt = "\"Those in my service shall not want for lucre!\"\n\nA pile of [YELLOW coins] appears on the altar!";
        Item zorkmids = ItemFactory.Get(ItemNames.ZORKMIDS, gs.ObjDb);
        zorkmids.Value = 100;
        gs.ObjDb.SetToLoc(altarLoc, zorkmids);
        break;
      case 2:
        txt = "\"I shall ward you from the assaults of those who would stand in our way!\"\n\nYou are surrounded by a shimmering aura.";
        AuraOfProtectionTrait aura = new() { HP = 50 };
        aura.Apply(gs.Player, gs);
        break;
      case 3:
        txt = "Your mind is filled with visions of battle and conflict, of armies clashing, and soldiers killing and being killed.";
        txt += "\n\nYou feel more skilled with your weapon.";
        Attribute attr = Attribute.BowUse;
        if (weapon is not null)
        {
          foreach (Trait t in weapon.Traits)
          {            
            switch (t)
            {            
              case AxeTrait:
                attr = Attribute.AxeUse;
                break;
              case CudgelTrait:
                attr = Attribute.CudgelUse;
                break;
              case FinesseTrait:
                attr = Attribute.FinesseUse;
                break;
              case PolearmTrait:
                attr = Attribute.PolearmsUse;
                break;
              case SwordTrait:
                attr = Attribute.SwordUse;
                break;
            }
          }        
        }
        
        if (gs.Player.Stats.TryGetValue(attr, out var stat))
          stat.SetMax(stat.Curr + 100);
        else
          gs.Player.Stats[attr] = new Stat(100);
        break;
    }

    ui.SetPopup(new Popup(txt, "A harsh whisper", -1, -1));
  }
}