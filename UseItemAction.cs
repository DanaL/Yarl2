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

class UseItemAction(GameState gs, Actor actor) : Action(gs, actor)
{
  public char Choice { get; set; }
  
  ActionResult UseVaultKey(Item key)
  {
    VaultKeyTrait? keyTrait = key.Traits.OfType<VaultKeyTrait>()
                                        .FirstOrDefault();
    if (keyTrait is not null)
    {
      Loc doorLoc = keyTrait.VaultLoc;
      bool adj = false;
      foreach (var loc in Util.Adj4Locs(Actor!.Loc))
      {
        if (loc == doorLoc) 
        {
          adj = true;
          break;
        }
      }

      if (!adj)
      {
        Message msg = new("You see nowhere to use that key.", Actor.Loc, false);
        return new ActionResult() { Messages = [msg], Complete = true, EnergyCost = 0.0 };
      }

      Message openMsg = new("The metal doors swing open.", Actor.Loc, false);
      var door = (VaultDoor) GameState!.CurrentMap.TileAt(doorLoc.Row, doorLoc.Col);
      door.Open = true;

      // Just remove the key from the game since it's now useless
      Actor.Inventory.RemoveByID(key.ID);
      GameState.ObjDb.RemoveItemFromGame(Actor.Loc, key);

      return new ActionResult() { Messages = [openMsg], Complete = true, EnergyCost = 1.0 };
    }

    throw new Exception("Attempted to use a vault key that isn't a vault key? This shouldn't happen!");
  }

  public override ActionResult Execute()
  {
    var (item, itemCount) = Actor!.Inventory.ItemAt(Choice);
    if (item is null)
      throw new Exception("Using item in inventory that doesn't exist :O This shouldn't happen :O");

    if (item.Type == ItemType.Bow)
    {
      GameState!.ClearMenu();
      ((Player)Actor).FireReadedBow(item, GameState);
      return new ActionResult() { Complete = false, EnergyCost = 0.0 };      
    }

    bool consumable = item.HasTrait<ConsumableTrait>();
    bool stackable = item.HasTrait<StackableTrait>();
    bool written = item.HasTrait<WrittenTrait>();
    bool vaultKey = item.HasTrait<VaultKeyTrait>();

    GameState!.ClearMenu();

    if (vaultKey)
      return UseVaultKey(item);

    var useableTraits = item.Traits.Where(t => t is IUSeable).ToList();
    if (useableTraits.Count != 0)
    {
      if (written)
      {
        // Eventually being blind will prevent you from reading things
        if (Actor.HasTrait<ConfusedTrait>())
        {
          string txt = $"{Actor.FullName} {Grammar.Conjugate(Actor, "is")} too confused to read that!";
          var msg = new Message(txt, Actor.Loc);
          return new ActionResult() { Complete = true, Messages = [msg], EnergyCost = 1.0 };
        }
      }

      if (consumable || stackable)
      {
        Actor.Inventory.RemoveByID(item.ID);

        // If we are using a stackable item (say, a Torch), get rid of the
        // stackable trait, then add it back to the inventory
        if (stackable && !consumable)
        {
          item.Traits = item.Traits.Where(t => t is not StackableTrait).ToList();
          Actor.Inventory.Add(item, Actor.ID);
        }
        
        // Sometimes, when a player with the scholar background reads a scroll,
        // it won't be consumed.
        if (consumable && written && Actor is Player player && player.Background == PlayerBackground.Scholar)
        {
          double roll = GameState.Rng.NextDouble();
          if (roll <= 0.2)
          {
            Actor.Inventory.Add(item, Actor.ID);
          }
        }
      }

      var result = new ActionResult() { Complete = true, EnergyCost = 1.0 };
      bool success = false;
      foreach (IUSeable trait in useableTraits)
      {
        var useResult = trait.Use(Actor, GameState, Actor.Loc.Row, Actor.Loc.Col);
        result.Complete = useResult.Successful;
        result.Messages.Add(new Message(useResult.Message, Actor.Loc));
        success = useResult.Successful;

        if (useResult.ReplacementAction is not null)
        {
          result.Complete = false;
          result.AltAction = useResult.ReplacementAction;
          result.EnergyCost = 0.0;
        }
      }

      return result;
    }
    else
    {
      var msg = new Message("You don't know a way to use that!", GameState.Player.Loc);
      return new ActionResult() { Complete = true, Messages = [msg], EnergyCost = 0.0 };
    }
  }

  public override void ReceiveUIResult(UIResult result) => Choice = ((MenuUIResult)result).Choice;
}
