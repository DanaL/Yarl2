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
// Action sbuclasses, but some of the code is usied more more than 
// one place, and I didn't want to keep shoving stuff into GameState.

class Cmd
{
  public static bool AddItemToInventory(Actor actor, Item item, GameState gs)
  {
    Inventory inv = actor.Inventory;

    if (item.Type == ItemType.Zorkmid)
    {
      inv.Zorkmids += item.Value;
      gs.ObjDb.RemoveItemFromGame(item.Loc, item);
      return true;
    }

    bool freeSlot = inv.UsedSlots().Length < 26;
    if (!freeSlot)
    {
      gs.UIRef().AlertPlayer("There's no room left in your inventory!");
      gs.UIRef().AlertPlayer($"{item.FullName.Capitalize()} drops to the ground.");
      item.Equipped = false;
      gs.ItemDropped(item, actor.Loc);
      return false;
    }

    inv.Add(item, actor.ID);

    return true;
  }
}