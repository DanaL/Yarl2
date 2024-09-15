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

class PickLockAction(GameState gs, Actor actor) : Action(gs, actor)
{
  int Row;
  int Col;

  public override ActionResult Execute()
  {
    ActionResult result = base.Execute();
    
    Loc loc = Actor!.Loc with { Row = Actor.Loc.Row + Row, Col = Actor.Loc.Col + Col };
    Tile tile = GameState!.TileAt(loc);

    if (tile.Type == TileType.VaultDoor)
    {
      result.Messages.Add(new Message("That door requires a special key.", loc));
      result.Complete = false;
      result.EnergyCost = 0.0;
    }
    else if (tile.Type == TileType.OpenDoor)
    {
      result.Messages.Add(new Message("That door is not closed.", loc));
      result.Complete = false;
      result.EnergyCost = 0.0;
    }
    else if (!(tile.Type == TileType.LockedDoor || tile.Type == TileType.ClosedDoor))
    {
      result.Messages.Add(new Message("You find no lock there.", loc));
      result.Complete = false;
      result.EnergyCost = 0.0;
    }
    else 
    {
      result.Complete = true;
      result.EnergyCost = 1.0;

      bool rogue = gs.Player.Background == PlayerBackground.Skullduggery;
      int dc = 12 + gs.CurrLevel + 1;
      if (rogue)
        dc -= 5;
      int roll = GameState.Rng.Next(1, 21);
      if (roll + Actor.Stats[Attribute.Dexterity].Curr > dc)
      {
        if (tile.Type == TileType.LockedDoor)
        {
          result.Messages.Add(new Message("The lock releases with a click.", loc));
          GameState.CurrentMap.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.ClosedDoor));
        }
        else
        {
          result.Messages.Add(new Message("You lock the door.", loc));
          GameState.CurrentMap.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.LockedDoor));
        }
      }
      else
      {
        result.Messages.Add(new Message("You fumble at the lock.", loc));
      }
    }

    return result;
  }

  public override void ReceiveUIResult(UIResult result) 
  {
    var dir = (DirectionUIResult)result;
    Row = dir.Row;
    Col = dir.Col;
  }
}

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

    if (item.Type == ItemType.Tool && item.Name == "lock pick")
    {
      GameState!.ClearMenu();
      UserInterface ui = GameState.UIRef();
      ui.SetPopup(new Popup("Which direction?", "", ui.PlayerScreenRow - 3, -1));

      ((Player)Actor).ReplacePendingAction(new PickLockAction(GameState, Actor), new DirectionalInputer());
      return new ActionResult() { Complete = false, EnergyCost = 0.0 };
    }

    bool consumable = item.HasTrait<ConsumableTrait>();
    bool torch = item.HasTrait<TorchTrait>();
    bool written = item.HasTrait<WrittenTrait>();
    bool vaultKey = item.HasTrait<VaultKeyTrait>();

    GameState!.ClearMenu();

    if (vaultKey)
      return UseVaultKey(item);

    if (item.Type == ItemType.Food)
    {
      
    }

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

      // When a torch has been lit, we want to remove it from the stack of 
      // other torches
      if (torch)
      {
        Actor.Inventory.RemoveByID(item.ID);        
        item.Traits = item.Traits.Where(t => t is not StackableTrait).ToList();
        Actor.Inventory.Add(item, Actor.ID);
      }

      var result = new ActionResult() { Complete = true, EnergyCost = 1.0 };
      if (item.Type == ItemType.Food)
      {
        string s = $"{Actor.FullName.Capitalize()} {Grammar.Conjugate(Actor, "eat")} {item.FullName.DefArticle()}";
        result.Messages.Add(new Message(s, Actor.Loc, false));
      }

      bool success = false;
      foreach (IUSeable trait in useableTraits)
      {
        var useResult = trait.Use(Actor, GameState, Actor.Loc.Row, Actor.Loc.Col, item);
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
