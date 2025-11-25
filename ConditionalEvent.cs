// Delve - A roguelike computer RPG
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
  public abstract void Fire(GameState gs);
  public abstract string AsText();

  public static ConditionalEvent FromText(string txt)
  {
    string[] pieces = txt.Split(Constants.SEPARATOR);

    if (pieces.Length == 0)
      throw new Exception("Invalid ConditionalEvent serialization");

    return pieces[0] switch
    {
      "CanSeeLoc" => new CanSeeLoc(Loc.FromStr(pieces[1]), pieces[2]),
      "SetQuestStateAtLoc" => new SetQuestStateAtLoc(Loc.FromStr(pieces[1]), int.Parse(pieces[2])),
      "PlayerHasLitTorch" => new PlayerHasLitTorch(),
      "MessageAtLoc" => new MessageAtLoc(Loc.FromStr(pieces[1]), pieces[2]),
      _ => throw new Exception("Invalid ConditionalEvent serialization")
    };
  }
}

class CanSeeLoc(Loc loc, string msg) : ConditionalEvent
{
  Loc Loc { get; set; } = loc;
  string Msg { get; set; } = msg;

  public override bool CondtionMet(GameState gs) => gs.LastPlayerFoV.ContainsKey(Loc);

  public override void Fire(GameState gs)
  {
    gs.UIRef().SetPopup(new Popup(Msg, "", -2, -1));
    gs.UIRef().PauseForResponse = true;
  }

  public override string AsText() => $"CanSeeLoc{Constants.SEPARATOR}{Loc}{Constants.SEPARATOR}{Msg}";
}

class SetQuestStateAtLoc(Loc loc, int questState) : ConditionalEvent
{
  Loc Loc { get; set; } = loc;
  int QuestState { get; set; } = questState;

  public override bool CondtionMet(GameState gs) => gs.Player.Loc == Loc;

  public override void Fire(GameState gs)
  {
    // The quest state should alaways go up. If the player skips a trigger we
    // don't want to accidentally move progress backwards later on
    if (gs.MainQuestState < QuestState)
      gs.Player.Stats[Attribute.MainQuestState] = new Stat(QuestState);
  }

  public override string AsText() => $"SetQuestStateAtLoc{Constants.SEPARATOR}{Loc}{Constants.SEPARATOR}{QuestState}";
}

class MessageAtLoc(Loc loc, string msg) : ConditionalEvent
{
  Loc Loc { get; set; } = loc;
  string Msg { get; set; } = msg;

  public override bool CondtionMet(GameState gs) => gs.Player.Loc == Loc;

  public override void Fire(GameState gs)
  {
    gs.UIRef().SetPopup(new Popup(Msg, "", -2, -1));
    gs.UIRef().PauseForResponse = true;
  }

  public override string AsText() => $"MessageAtLoc{Constants.SEPARATOR}{Loc}{Constants.SEPARATOR}{Msg}";
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

  public override void Fire(GameState gs)
  {
    string txt = @"Great! Now you have some light and can see a little more of your surroundings.

    Nearby is some equipment that will be useful. Let's walk over, collect the items, and equip them.

    Movement in Delve can be done via the numpad or the arrow keys, but if you are a touch-typist or playing on a laptop, you may prefer the movement keys based on the home row. See the map at the bottom of the screen. 

    Tapping 'l' twice will move your character on top of the first piece of gear.

    [LIGHTBLUE Within the tutorial, tap ENTER or SPACE to dismiss pop-ups.]    
    ";

    gs.UIRef().CheatSheetMode = CheatSheetMode.MvMixed;
    gs.UIRef().SetPopup(new Popup(txt, "", -2, -1));
    gs.UIRef().PauseForResponse = true;
  }

  public override string AsText() => "PlayerHasLitTorch";
}

class FullyEquipped(Loc loc) : ConditionalEvent
{
  Loc Loc { get; set; } = loc;
  public HashSet<ulong> IDs { get; set; } = [];

  public override bool CondtionMet(GameState gs)
  {
    if (gs.Player.Loc == Loc)
    {
      HashSet<ulong> equippedItems = [.. gs.Player.Inventory.Items()
                                       .Where(i => i.Equipped)
                                       .Select(i => i.ID)];
      foreach (var id in IDs)
      {
        if (!equippedItems.Contains(id))
          return true;
      }
    }
      
    return false;
  }

  public override void Fire(GameState gs)
  {
    string txt = @"Make sure both your armour and weapon are equipped before venturing further!";
    gs.UIRef().SetPopup(new Popup(txt, "", -1, -1));
    gs.UIRef().PauseForResponse = true;
  }

  public override string AsText() => throw new NotImplementedException();
}
