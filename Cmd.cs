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

  public static void DefuseBomb(Item bomb, Loc loc, GameState gs)
  {
    if (bomb.Traits.OfType<ExplosionCountdownTrait>().FirstOrDefault() is ExplosionCountdownTrait explosion)
    {
      gs.RemoveListener(explosion);
      bomb.Traits = [.. bomb.Traits.Where(t => t is not ExplosionCountdownTrait)];
      bomb.Traits.Add(new StackableTrait());
      gs.UIRef().AlertPlayer($"The bomb's fuse is extinguished.", gs, loc);
    }
  }

  public static void SetExplosive(Item bomb, GameState gs)
  {    
    if (bomb.Traits.OfType<ExplosiveTrait>().FirstOrDefault() is ExplosiveTrait explosive)
    {
      ExplosionCountdownTrait ect = new() { Fuse = explosive.Fuse, DmgDie = explosive.DmgDie, NumOfDice = explosive.NumOfDice };
      ect.Apply(bomb, gs);
      bomb.Traits = [.. bomb.Traits.Where(t => t is not StackableTrait)];
    }
  }

  public static void ThrowItem(Actor chucker, Item item, Loc target, GameState gs)
  {
    List<Loc> trajectory = Util.Trajectory(chucker.Loc, target);
    List<Loc> pts = [];
    for (int j = 0; j < trajectory.Count; j++)
    {
      var pt = trajectory[j];
      var tile = gs.TileAt(pt);
      var occ = gs.ObjDb.Occupant(pt);
      if (j > 0 && occ != null)
      {
        pts.Add(pt);

        // I'm not handling what happens if a projectile hits a friendly or 
        // neutral NPCs
        bool attackSuccessful = Battle.MissileAttack(chucker, occ, gs, item, 0, null);
        if (attackSuccessful)
        {
          break;
        }
      }
      else if (gs.ObjDb.AreBlockersAtLoc(pt))
      {
        break;
      }
      else if (tile.Passable() || tile.PassableByFlight())
      {
        pts.Add(pt);
      }
      else
      {
        break;
      }
    }

    ThrownMissileAnimation anim = new(gs, item.Glyph, pts, item);
    gs.UIRef().PlayAnimation(anim, gs);

    Loc landingPt = pts.Last();
    gs.ItemDropped(item, landingPt, true);
    item.Equipped = false;
  }
}