// Delve - A roguelike computer RPG
// Written in 2025 by Dana Larose <ywg.dana@gmail.com>
//
// To the extent possible under law, the author(s) have dedicated all copyright
// and related and neighboring rights to this software to the public domain
// worldwide. This software is distributed without any warranty.
//
// You should have received a copy of the CC0 Public Domain Dedication along 
// with this software. If not, 
// see <http://creativecommons.org/publicdomain/zero/1.0/>.

namespace Yarl2;

// A lot of the code for processing user commands lives in the various
// Action sbuclasses, but some of the code is used in more than 
// one place, and I didn't want to keep shoving stuff into GameState.

class Cmd
{
  public static bool AddItemToInventory(Actor actor, Item item, GameState gs)
  {
    Inventory inv = actor.Inventory;

    if (item.Type == ItemType.Zorkmid)
    {
      inv.Add(item, actor.ID);
      gs.ObjDb.RemoveItemFromGame(item.Loc, item);
      return true;
    }

    char slot = inv.Add(item, actor.ID);
    if (slot == '\0')
    {
      gs.UIRef().AlertPlayer("There's no room left in your inventory!");
      gs.UIRef().AlertPlayer($"{item.FullName.DefArticle().Capitalize()} drops to the ground.");
      item.Equipped = false;
      gs.ItemDropped(item, actor.Loc);

      return false;
    }

    return true;
  }
  
  public static void CheckWear(Item tool, Actor actor, GameState gs)
  {
    if (tool.Traits.OfType<WearAndTearTrait>().FirstOrDefault() is WearAndTearTrait wear)
    {
      ++wear.Wear;

      if (gs.Rng.Next(40) < wear.Wear)
      {
        actor.Inventory.ConsumeItem(tool, actor, gs);
        string t = $"{Grammar.Possessive(actor).Capitalize()} {tool.Name} breaks!";
        gs.UIRef().AlertPlayer(t, gs, actor.Loc);

        if (actor is Player)
          gs.UIRef().SetPopup(new Popup(t, "", -1, -1));
      }
    }
  }
}