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
  
  public abstract bool CondtionMet(GameState gs);
  public abstract void Fire(UserInterface ui);
}

class CanSeeLoc(Loc loc, string msg) : ConditionalEvent
{
  Loc Loc { get; set; } = loc;
  string Msg { get; set; } = msg;

  public override bool CondtionMet(GameState gs)
  {
    return gs.LastPlayerFoV.Contains(Loc);
  }

  public override void Fire(UserInterface ui)
  {
    ui.SetPopup(new Popup(Msg, "", -2, -1));
    ui.PauseForResponse = true;
  }
}

class PlayerAtLoc(Loc loc, string msg) : ConditionalEvent
{
  Loc Loc { get; set; } = loc;
  string Msg { get; set; } = msg;

  public override bool CondtionMet(GameState gs)
  {
    return gs.Player.Loc == Loc;
  }

  public override void Fire(UserInterface ui)
  {
    ui.SetPopup(new Popup(Msg, "", -2, -1));
    ui.PauseForResponse = true;
  }
}

// Used in the tutorial
class PlayerHasLitTorch : ConditionalEvent
{
  public override bool CondtionMet(GameState gs)
  {
    foreach (var item in gs.Player.Inventory.Items())
    {
      if (item.Traits.OfType<TorchTrait>().FirstOrDefault() is TorchTrait torch && torch.Lit)
        return true;
    }

    return false;
  }

  public override void Fire(UserInterface ui)
  {
    string txt = @"Great! Now you have some light and can see a little more of your surroundings.

    Nearby is some equipment that will be useful. Let's walk over, collect the items, and equip them.

    Movement in Delve can be done via the numpad or the arrow keys, but if you are a touch-typist or playing on a laptop, you may prefer the movement keys based on the home row. See the map at the bottom of the screen. 

    Tapping 'l' twice will move your character on top of the first piece of gear.

    
    ";

    ui.CheatSheetMode = CheatSheetMode.MvMixed;
    ui.SetPopup(new Popup(txt, "", -2, -1));
    ui.PauseForResponse = true;
  }  
}

class FullyEquipped(Loc loc) : ConditionalEvent
{
  Loc Loc { get; set; } = loc;
  public HashSet<ulong> IDs { get; set; } = [];

  public override bool CondtionMet(GameState gs)
  {
    if (gs.Player.Loc == Loc)
    {
      HashSet<ulong> equippedItems = gs.Player.Inventory.Items()
                                       .Where(i => i.Equipped)
                                       .Select(i => i.ID)
                                       .ToHashSet();
      foreach (var id in IDs)
      {
        if (!equippedItems.Contains(id))
          return true;
      }
    }
      
    return false;
  }

  public override void Fire(UserInterface ui)
  {
    string txt = @"Make sure both your armour and weapon are equipped before venturing further!";
    ui.SetPopup(new Popup(txt, "", -1, -1));
    ui.PauseForResponse = true;
  }
}
