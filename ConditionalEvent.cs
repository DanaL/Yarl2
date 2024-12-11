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

abstract class ConditionalEvent
{
  public bool Complete { get; set; }
  
  public abstract bool CondtionMet();
  public abstract void Fire();
}

class CanSeeLoc(GameState gs, UserInterface ui, Loc loc, string msg) : ConditionalEvent
{
  GameState GS { get; set; } = gs;
  UserInterface UI { get; set; } = ui;
  Loc Loc { get; set; } = loc;
  string Msg { get; set; } = msg;

  public override bool CondtionMet()
  {
    return GS.LastPlayerFoV.Contains(Loc);
  }

  public override void Fire()
  {
    UI.SetPopup(new Popup(Msg, "", -2, -1));
  }
}

class PlayerAtLoc(GameState gs, UserInterface ui, Loc loc, string msg) : ConditionalEvent
{
  GameState GS { get; set; } = gs;
  UserInterface UI { get; set; } = ui;
  Loc Loc { get; set; } = loc;
  string Msg { get; set; } = msg;

  public override bool CondtionMet()
  {
    return GS.Player.Loc == Loc;
  }

  public override void Fire()
  {
    UI.SetPopup(new Popup(Msg, "", -1, -1));
  }
}

// Used in the tutorial
class PlayerHasLitTorch(GameState gs, UserInterface ui) : ConditionalEvent
{
  GameState GS { get; set; } = gs;
  UserInterface UI { get; set; } = ui;

  public override bool CondtionMet()
  {
    foreach (var item in GS.Player.Inventory.Items())
    {
      if (item.Traits.OfType<TorchTrait>().FirstOrDefault() is TorchTrait torch && torch.Lit)
        return true;
    }

    return false;
  }

  public override void Fire()
  {
    string txt = @"Great! Now you have some light and can see a little more of your surroundings.

    Nearby is some equipment that will be useful. Let's walk over, collect the items, and equip them.

    Movement in Delve can be done via the numpad or the arrow keys, but if you are a touch-typist or playing on a laptop, you may prefer the movement keys based on the home row. See the map at the bottom of the screen. 

    Tapping 'l' twice will move your character on top of the first piece of gear.

    
    ";

    UI.CheatSheetMode = CheatSheetMode.MvMixed;
    UI.SetPopup(new Popup(txt, "", -2, -1));
  }  
}

class FullyEquiped(GameState gs, UserInterface ui, Loc loc) : ConditionalEvent
{
  GameState GS { get; set; } = gs;
  UserInterface UI { get; set; } = ui;
  Loc Loc { get; set; } = loc;
  public HashSet<ulong> IDs { get; set; } = [];

  public override bool CondtionMet()
  {
    if (GS.Player.Loc == Loc)
    {
      HashSet<ulong> equipedItems = GS.Player.Inventory.Items()
                                    .Where(i => i.Equiped)
                                    .Select(i => i.ID)
                                    .ToHashSet();
      foreach (var id in IDs)
      {
        if (!equipedItems.Contains(id))
          return true;
      }
    }
      
    return false;
  }

  public override void Fire()
  {
    string txt = @"Make sure both your armour and weapon are equiped before venturing further!";
    UI.SetPopup(new Popup(txt, "", -1, -1));
  }
}
