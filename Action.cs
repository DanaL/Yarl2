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

class ActionResult
{
    public bool Successful { get; set; }
    public List<Message> Messages { get; set; } = [];
    public string MessageIfUnseen { get; set; } = "";
    public Action? AltAction { get; set; }
    public double EnergyCost { get; set; } = 0.0;
    public bool PlayerKilled { get; set; } = false;

    public ActionResult() { }
}

abstract class Action
{
    public abstract ActionResult Execute();
    public virtual void ReceiveAccResult(AccumulatorResult result) {}
}

class MeleeAttackAction(Actor actor, Loc loc, GameState gs, Random rng) : Action
{
    GameState _gs = gs;
    Loc _loc = loc;
    Actor _actor = actor;
    Random _rng = rng;
    
    public override ActionResult Execute()
    {
        var result = new ActionResult() { Successful = true };

        var target = _gs.ObjDB.Occupant(_loc);
        if (target is not null) 
        {
            result = Battle.MeleeAttack(_actor, target, _gs, _rng);
        }

        return result;
    }
}

class PortalAction(GameState gameState) : Action
{
    protected readonly GameState _gameState = gameState;

    protected void UsePortal(Portal portal, ActionResult result)
    {
        var start = _gameState.Player!.Loc;
        var (dungeon, level, _, _) = portal.Destination;
        _gameState.EnterLevel(dungeon, level);
        _gameState.Player!.Loc = portal.Destination;
        _gameState.ActorMoved(_gameState.Player!, start, portal.Destination);
        _gameState.RefreshPerformers();

        result.Successful = true;

        if (start.DungeonID != portal.Destination.DungeonID)
            result.Messages.Add(MessageFactory.Phrase(_gameState.CurrentDungeon.ArrivalMessage, portal.Destination));
    
        if (portal.Destination.DungeonID > 0)
        {
            int maxDepth = _gameState.Player!.Stats[Attribute.Depth].Max;
            if (portal.Destination.Level + 1 > maxDepth)
                _gameState.Player!.Stats[Attribute.Depth].SetMax(portal.Destination.Level + 1);
        }

        result.EnergyCost = 1.0;
    }

    public override ActionResult Execute()
    {
        var result = new ActionResult() { Successful = false };
        
        var p = _gameState.Player!;        
        var t = _gameState.CurrentMap.TileAt(p.Loc.Row, p.Loc.Col);

        if (t.Type == TileType.Portal) 
        {
            UsePortal((Portal)t, result);
        }
        else
        {
            result.Messages.Add(MessageFactory.Phrase("There is nowhere to go here.", _gameState.Player.Loc));
        }

        return result;
    }
}

class DownstairsAction(GameState gameState) : PortalAction(gameState)
{
    public override ActionResult Execute()
    {
        var result = new ActionResult() { Successful = false };

        var p = _gameState.Player!;        
        var t = _gameState.CurrentMap.TileAt(p.Loc.Row, p.Loc.Col);

        if (t.Type == TileType.Downstairs) 
        {
            UsePortal((Portal)t, result);
        }
        else
        {
            result.Messages.Add(MessageFactory.Phrase("You cannot go down here.", _gameState.Player.Loc));
        }

        return result;
    }
}

class UpstairsAction(GameState gameState) : PortalAction(gameState)
{
    public override ActionResult Execute()
    {
        var result = new ActionResult() { Successful = false };

        var p = _gameState.Player!;        
        var t = _gameState.CurrentMap.TileAt(p.Loc.Row, p.Loc.Col);

        if (t.Type == TileType.Upstairs) 
        {
            UsePortal((Portal)t, result);
        }
        else
        {
            result.Messages.Add(MessageFactory.Phrase("You cannot go up here.", _gameState.Player.Loc));
        }

        return result;
    }
}

abstract class DirectionalAction(Actor actor) : Action
{
    protected readonly Actor _actor = actor;
    protected Loc _loc { get; set; }
    
    public override void ReceiveAccResult(AccumulatorResult result)
    {
        var dirResult = (DirectionAccumulatorResult)result;
        _loc = _actor.Loc with { Row = _actor.Loc.Row + dirResult.Row, Col = _actor.Loc.Col + dirResult.Col };        
    }
}

class ChatAction(Actor actor, GameState gs) : DirectionalAction(actor)
{
    private GameState _gs = gs;

    public override ActionResult Execute()
    {
        var result = new ActionResult() { Successful = false };

        var other = _gs.ObjDB.Occupant(_loc);

        if (other is null)
        {
            result.Messages.Add(MessageFactory.Phrase("There's no one there!", _actor.Loc));
        }
        else if (other is Villager villager)
        {
            _gs.UI.Popup($"{villager.Appearance.IndefArticle().Capitalize()}.", villager.FullName);

            result.Successful = true;
            result.EnergyCost = 1.0;
        }
        else
        {
            result.Messages.Add(MessageFactory.Phrase("They aren't interested in chatting.", _actor.Loc));

            result.Successful = true;
            result.EnergyCost = 1.0;
        }

        return result;
    }
}

class CloseDoorAction(Actor actor, Map map, GameState gs) : DirectionalAction(actor)
{
    private GameState _gs = gs;
    private readonly Map _map = map;

    public override ActionResult Execute()
    {
        var result = new ActionResult() { Successful = false };
        var door = _map.TileAt(_loc.Row, _loc.Col);

        if (door is Door d)
        {
            if (d.Open)
            {                
                d.Open = false;
                result.Successful = true;
                result.EnergyCost = 1.0;

                var msg = MessageFactory.Phrase(_actor.ID, Verb.Close, "the door", false, _loc, _gs);
                result.Messages.Add(msg);
                result.MessageIfUnseen = "You hear a door close.";

                // Find any light sources tht were affecting the door and update them, since
                // it's now open. (Eventually gotta extend it to any aura type effects)
                foreach (var src in _gs.ObjsAffectingLoc(_loc, TerrainFlags.Lit))
                {
                    _gs.CurrentMap.RemoveEffectFromMap(TerrainFlags.Lit, src.ID);
                    _gs.ToggleEffect(src, _actor.Loc, TerrainFlags.Lit, true);
                }
            }
            else if (_actor is Player)
            {
                result.Messages.Add(MessageFactory.Phrase("The door is already closed.", _gs.Player.Loc));
            }
        }
        else if (_actor is Player)
        {
            result.Messages.Add(MessageFactory.Phrase("There's no door there!", _gs.Player.Loc));
        }

        return result;
    }
}

class OpenDoorAction : DirectionalAction
{
    private GameState _gs;
    private readonly Map _map;

    public OpenDoorAction(Actor actor, Map map, GameState gs) : base(actor)
    {
        _map = map;
        _gs = gs;
    }

    public OpenDoorAction(Actor actor, Map map, Loc loc, GameState gs) : base(actor)
    {
        _map = map;
        _loc = loc;
        _gs = gs;
    }

    public override ActionResult Execute()
    {
        var result = new ActionResult() { Successful = false };
        var door = _map.TileAt(_loc.Row, _loc.Col);
        
        if (door is Door d)
        {
            if (!d.Open)
            {
                d.Open = true;
                result.Successful = true;
                result.EnergyCost = 1.0;

                var msg = MessageFactory.Phrase(_actor.ID, Verb.Open, "door", false, _loc, _gs);
                result.Messages.Add(msg);
                result.MessageIfUnseen = "You hear a door open.";

                // Find any light sources tht were affecting the door and update them, since
                // it's now open. (Eventually gotta extend it to any aura type effects)

                foreach (var src in _gs.ObjsAffectingLoc(_loc, TerrainFlags.Lit))
                {
                    _gs.ToggleEffect(src, src.Loc, TerrainFlags.Lit, true);
                }
                _gs.ToggleEffect(_actor, _loc, TerrainFlags.Lit, true);
            }
            else if (_actor is Player)
            {
                result.Messages.Add(MessageFactory.Phrase("The door is already open.", _gs.Player.Loc));
            }
        }
        else if (_actor is Player)
        {
            result.Messages.Add(MessageFactory.Phrase("There's no door there!", _gs.Player.Loc));
        }

        return result;
    }
}

class MoveAction(Actor actor,  Loc loc, GameState gameState, Random rng) : Action
{
    readonly Actor _actor = actor;
    readonly Loc _loc = loc;
    readonly Map _map = gameState.Map!;
    readonly bool _bumpToOpen = gameState.Options!.BumpToOpen;
    readonly GameState _gs = gameState;
    readonly Random _rng = rng;

    static string BlockedMessage(Tile tile)
    {
        return tile.Type switch
        {
            TileType.DeepWater => "The water seems deep and cold.",
            TileType.Mountain or TileType.SnowPeak => "You cannot scale the mountain!",
            TileType.Chasm => "Do you really want to jump into the chasm?",
            _ => "You cannot go that way!"
        }; ;
    }

    string CalcDesc()
    {
        if (_actor is not Player)
            return "";

        var items = _gs.ObjDB.ItemsAt(_loc);

        if (items.Count == 0) 
        {
            return _map.TileAt(_loc.Row, _loc.Col).StepMessage;
        }
        else if (items.Count > 1)
        {
            return "There are several items here.";
        }
        else if (items[0].Count > 1)
        {
            return $"There are {items[0].Count} {items[0].FullName.Pluralize()} here.";
        }
        else
        {
            return $"There is {items[0].FullName.IndefArticle()} here.";
        }
    }

    public override ActionResult Execute()
    {
        var result = new ActionResult();

        if (!_map.InBounds(_loc.Row, _loc.Col))
        {
            // in theory this shouldn't ever happen...
            result.Successful = false;
            if (_actor is Player)
                result.Messages.Add(MessageFactory.Phrase("You cannot go that way!", _gs.Player.Loc));
        }
        else if (_gs.ObjDB.Occupied(_loc))
        {
            result.Successful = false;
            var occ = _gs.ObjDB.Occupant(_loc);
            if (occ is not null && !occ.Hostile)
            {
                string msg = $"You don't want to attack {occ.FullName}!";
                result.Messages.Add(MessageFactory.Phrase(msg, _gs.Player.Loc));
            }
            else
            {
                var attackAction = new MeleeAttackAction(_actor, _loc, _gs, _rng);
                result.AltAction = attackAction;
            }
        }
        else if (!_map.TileAt(_loc.Row, _loc.Col).Passable())
        {
            result.Successful = false;

            if (_actor is Player)
            {
                var tile = _map.TileAt(_loc.Row, _loc.Col);
                if (_bumpToOpen && tile.Type == TileType.ClosedDoor)
                {
                    var openAction = new OpenDoorAction(_actor, _map, _loc, _gs);
                    result.AltAction = openAction;
                }
                else
                {
                    result.Messages.Add(MessageFactory.Phrase(BlockedMessage(tile), _gs.Player.Loc));
                }
            }
        }
        else
        {            
            result.Successful = true;
            result.EnergyCost = 1.0;

            _gs.ActorMoved(_actor, _actor.Loc, _loc);
            _actor.Loc = _loc;

            if (_actor is Player)
            {
                result.Messages.Add(MessageFactory.Phrase(CalcDesc(), _loc));
                _gs.Noise(_actor.ID, _loc.Row, _loc.Col, 12);
            }
            else
            {                
                var alerted = _gs.Noise(_actor.ID, _loc.Row, _loc.Col, 10);
                if (alerted.Contains(_gs.Player.ID))
                {
                    result.Messages.Add(new Message("You hear padding footsteps...", _loc, true));
                }
            }
        }

        return result;
    }
}

class PickupItemAction(UserInterface ui, Actor actor, GameState gs) : Action
{
    public char Choice { get; set; }
    private UserInterface _ui = ui;
    private Actor _actor = actor;
    private GameState _gameState = gs;

    public override ActionResult Execute()
    {
        _ui.CloseMenu();
        var itemStack = _gameState.ObjDB.ItemsAt(_actor.Loc);

        var inv = ((IItemHolder)_actor).Inventory;
        bool freeSlot = inv.UsedSlots().Length < 26;
        Message msg;

        if (!freeSlot) 
        {
            msg = MessageFactory.Phrase("There's no room in your inventory!", _gameState.Player.Loc);
            return new ActionResult() { Successful=false, Messages = [msg] };                
        }

        int i = Choice - 'a';
        var item = itemStack[i];
        itemStack.RemoveAt(i);
        inv.Add(item, _actor.ID);

        msg = MessageFactory.Phrase(_actor.ID, Verb.Pickup, item.ID, item.Count, false, _actor.Loc, _gameState);
        return new ActionResult() { Successful=true, Messages = [msg], EnergyCost = 1.0 };
    }

    public override void ReceiveAccResult(AccumulatorResult result)
    {
        var menuResult = (MenuAccumulatorResult)result;
        Choice = menuResult.Choice;
    }
}

class UseItemAction(UserInterface ui, Actor actor, GameState gs) : Action
{
    public char Choice { get; set; }
    private UserInterface _ui = ui;
    private Actor _actor = actor;
    private GameState _gameState = gs;

    public override ActionResult Execute()
    {
         var item = ((IItemHolder)_actor).Inventory.ItemAt(Choice);
        _ui.CloseMenu();

        var useableTraits = item.Traits.Where(t => t is IUSeable);
        if (useableTraits.Any()) 
        {
            Item toUse = item;
            if (item.Count > 1)
            {
                --item.Count;
                toUse = item.Duplicate(_gameState);
                toUse.Count = 1;
                toUse.Stackable = false;
                if (!toUse.Consumable)
                    ((IItemHolder)_actor).Inventory.Add(toUse, _actor.ID);                
                useableTraits = toUse.Traits.Where(t => t is IUSeable);
            }
            else if (item.Consumable)
            {
                ((IItemHolder)_actor).Inventory.Remove(Choice, 0);
            }

            bool success = false;
            string msg = "";
            foreach (IUSeable trait in useableTraits)
            {
                (success, msg) = trait.Use(_actor, _gameState, _actor.Loc.Row, _actor.Loc.Col);
            }
            var alert = MessageFactory.Phrase(msg, _actor.Loc);
            return new ActionResult() { Successful = success, Messages = [alert], EnergyCost = 1.0 };
        }
        else
        {
            var msg = MessageFactory.Phrase("You don't know a way to use that!", _gameState.Player.Loc);
            return new ActionResult() { Successful = false, Messages = [msg] };
        }
    }

    public override void ReceiveAccResult(AccumulatorResult result)
    {
        var menuResult = (MenuAccumulatorResult)result;
        Choice = menuResult.Choice;
    }
}

class DropZorkmidsAction(UserInterface ui, Actor actor, GameState gs) : Action
{
    readonly UserInterface _ui = ui;
    readonly Actor _actor = actor;
    readonly GameState _gameState = gs;
    int _amount;

    public override ActionResult Execute()
    {
        double cost = 1.0;
        bool successful = true;
        Message alert;

        var inventory = ((IItemHolder)_actor).Inventory;
        if (_amount > inventory.Zorkmids)
        {
            _amount = inventory.Zorkmids;
        }
        
        if (_amount == 0)
        {
            cost = 0.0; // we won't make the player spend an action if they drop nothing
            successful = false;
            alert = MessageFactory.Phrase("You hold onto your zorkminds.", _actor.Loc);
        }
        else
        {
            
            var coins = ItemFactory.Get("zorkmids", _gameState.ObjDB);
            _gameState.ItemDropped(coins, _actor.Loc.Row, _actor.Loc.Col);
            coins.Count = _amount;
            string msg;
            if (_amount == 1)
                msg = "a single zorkmid";
            else if (_amount == inventory.Zorkmids)
                msg = "all your money";
            else
                msg = $"{_amount} zorkmids";
            
            alert = MessageFactory.Phrase(_actor.ID, Verb.Drop, msg, false, _actor.Loc, _gameState);

            inventory.Zorkmids -= _amount;
        }

        return new ActionResult() { Successful=successful, Messages = [alert], EnergyCost = cost };  
    }

    public override void ReceiveAccResult(AccumulatorResult result)
    {
        var count = ((NumericAccumulatorResult)result).Amount;
        _amount = count;
    }
}

class DropStackAction(UserInterface ui, Actor actor, GameState gs, char slot) : Action
{
    private readonly UserInterface _ui = ui;
    private readonly Actor _actor = actor;
    private readonly GameState _gameState = gs;
    private readonly char _slot = slot;
    private int _amount;

    public override ActionResult Execute()
    {
        Message alert;
        var item = ((IItemHolder)_actor).Inventory.ItemAt(_slot);        
        _ui.ClosePopup();

        if (_amount == 0 || _amount > item.Count)
        {
            // drop entire stack
            ((IItemHolder) _actor).Inventory.Remove(_slot, 1);
            _gameState.ItemDropped(item, _actor.Loc.Row, _actor.Loc.Col);
            item.Equiped = false;
            ((IItemHolder)_actor).CalcEquipmentModifiers();
            alert = MessageFactory.Phrase(_actor.ID, Verb.Drop, item.ID, item.Count, false, _actor.Loc, _gameState);
        }
        else
        {
            item.Count -= _amount;
            var dropped = item.Duplicate(_gameState);
            dropped.Count = _amount;
            ((IItemHolder)_actor).CalcEquipmentModifiers();
            _gameState.ItemDropped(dropped, _actor.Loc.Row, _actor.Loc.Col);
            alert = MessageFactory.Phrase(_actor.ID, Verb.Drop, dropped.ID, dropped.Count, false, _actor.Loc, _gameState);                      
        }

        return new ActionResult() { Successful=true, Messages = [alert], EnergyCost = 1.0 };        
    }

    public override void ReceiveAccResult(AccumulatorResult result)
    {
        var count = ((NumericAccumulatorResult)result).Amount;
        _amount = count;
    }
}

// Reading a magic scroll will need to work a little different than just
// reading a text document
class ReadItemAction(UserInterface ui, Actor actor, GameState gs) : Action
{
    public char Choice { get; set; }
    readonly UserInterface _ui = ui;
    readonly Actor _actor = actor;
    readonly GameState _gs = gs;

    public override ActionResult Execute()
    {
        var item = ((IItemHolder)_actor).Inventory.ItemAt(Choice);        
        _ui.CloseMenu();

        var readables = item.Traits.Where(t => t is IReadable);
        if (readables.Any())
        {
            IReadable document = (IReadable)readables.First();
            document.Read(_actor, _ui, item);
            
            if (_actor is Player player)
            {
                var acc = new PauseForMoreAccumulator();
                var action = new CloseMenuAction(_ui, 1.0);
                player.ReplacePendingAction(action, acc);
            }
            return new ActionResult() { Successful = true };
        }
        else
        {
            var msg = MessageFactory.Phrase("There's nothing to read on that!", _gs.Player.Loc);
            return new ActionResult() { Successful = false, Messages = [msg] };
        }
    }

    public override void ReceiveAccResult(AccumulatorResult result)
    {
        var menuResult = (MenuAccumulatorResult)result;
        Choice = menuResult.Choice;
    }
}

class DropItemAction(UserInterface ui, Actor actor, GameState gs) : Action
{
    public char Choice { get; set; }
    private readonly UserInterface _ui = ui;
    private readonly Actor _actor = actor;
    private readonly GameState _gameState = gs;

    public override ActionResult Execute() 
    {
        _ui.CloseMenu();

         if (Choice == '$')
        {
            var inventory = ((IItemHolder)_actor).Inventory;
            if (inventory.Zorkmids == 0)
            {
                var msg = MessageFactory.Phrase("You have no money!", _gameState.Player.Loc);
                return new ActionResult() { Successful=false, Messages = [msg] };
            }
            var dropMoney = new DropZorkmidsAction(_ui, _actor, _gameState);
            _ui.Popup("How much?");
            var acc = new NumericAccumulator(_ui, "How much?");
            if (_actor is Player player)
            {
                player.ReplacePendingAction(dropMoney, acc);
                return new ActionResult() { Successful = false, EnergyCost = 0.0 };
            }
            else
                // Will monsters ever just decide to drop money?
                return new ActionResult() { Successful = true };
        }
        
        var item = ((IItemHolder)_actor).Inventory.ItemAt(Choice);        
        if (item.Equiped && item.Type == ItemType.Armour)
        {
            var msg = MessageFactory.Phrase("You cannot drop something you're wearing.", _gameState.Player.Loc);
            return new ActionResult() { Successful=false, Messages = [msg] };
        }
        else if (item.Count > 1)
        {
            var dropStackAction = new DropStackAction(_ui, _actor, _gameState, Choice);
            var prompt = $"Drop how many {item.FullName.Pluralize()}?\n(enter for all)";
            _ui.Popup(prompt);
            var acc = new NumericAccumulator(_ui, prompt);
            if (_actor is Player player)
            {
                player.ReplacePendingAction(dropStackAction, acc);
                return new ActionResult() { Successful = false, EnergyCost = 0.0 };
            }
            else
                // When monsters can drop stuff I guess I'll have to handle that here??
                return new ActionResult() { Successful = true };
        }
        else 
        {
            ((IItemHolder) _actor).Inventory.Remove(Choice, 1);
            _gameState.ItemDropped(item, _actor.Loc.Row, _actor.Loc.Col);
            item.Equiped = false;
            ((IItemHolder)_actor).CalcEquipmentModifiers();

            var alert = MessageFactory.Phrase(_actor.ID, Verb.Drop, item.ID, 1, false, _actor.Loc, _gameState);
            _ui.AlertPlayer([alert], "");
            return new ActionResult() { Successful=true, EnergyCost = 1.0 };
        }
    }

    public override void ReceiveAccResult(AccumulatorResult result)
    {
        var menuResult = (MenuAccumulatorResult)result;
        Choice = menuResult.Choice;
    }
}

class ToggleEquipedAction(UserInterface ui, Actor actor, GameState gs) : Action
{
    public char Choice { get; set; }
    private UserInterface _ui = ui;
    private Actor _actor = actor;
    private GameState _gameState = gs;

    public override ActionResult Execute() 
    {
        ActionResult result;
        var item = ((IItemHolder)_actor).Inventory.ItemAt(Choice);
        _ui.CloseMenu();

        if (item.Type != ItemType.Armour && item.Type != ItemType.Weapon)
        {
            var msg = MessageFactory.Phrase("You cannot equip that!", _gameState.Player.Loc);
            return new ActionResult() { Successful = false, Messages = [msg] };
        }
        
        var (equipResult, conflict) = ((Player) _actor).Inventory.ToggleEquipStatus(Choice);
        Message alert;
        switch (equipResult)
        {
            case EquipingResult.Equiped:
                alert = MessageFactory.Phrase(_actor.ID, Verb.Ready, item.ID, 1, false, _actor.Loc, _gameState);
                result = new ActionResult() { Successful=true, Messages = [alert], EnergyCost = 1.0 };
                break;
            case EquipingResult.Unequiped:
                alert = MessageFactory.Phrase(_actor.ID, Verb.Ready, item.ID, 1, false, _actor.Loc, _gameState);
                result = new ActionResult() { Successful=true, Messages = [alert], EnergyCost = 1.0 };
                break;
            default:
                string msg = "You are already wearing ";
                if (conflict == ArmourParts.Helmet)
                    msg += "a helmet.";
                else if (conflict == ArmourParts.Shirt)
                    msg += "some armour.";
                alert = MessageFactory.Phrase(msg, _gameState.Player.Loc);
                result = new ActionResult() { Successful=true, Messages = [alert] };
                break;
        }            
        
        ((IItemHolder)_actor).CalcEquipmentModifiers();

        return result;
    }

    public override void ReceiveAccResult(AccumulatorResult result)
    {
        var menuResult = (MenuAccumulatorResult)result;
        Choice = menuResult.Choice;
    }
}

class PassAction(IPerformer performer) : Action
{
    private IPerformer _perfmer = performer;

    public override ActionResult Execute() 
    {
        // do nothing for now but eventually there will be an energy cost to passing
        // (ie., time will pass in game)
        return new ActionResult() { Successful = true, EnergyCost = 1.0 };
    }
}

class CloseMenuAction(UserInterface ui, double energyCost = 0.0) : Action 
{
    private UserInterface _ui = ui;
    private double _energyCost = energyCost;

    public override ActionResult Execute() 
    { 
        _ui.CloseMenu();
        return new ActionResult() { Successful=true, EnergyCost = _energyCost };
    }
}

class ExtinguishAction(IPerformer performer, GameState gs) : Action
{
    private IPerformer _performer = performer;
    private GameState _gs = gs;

    public override ActionResult Execute()
    {        
        _performer.RemoveFromQueue = true; // signal to remove it from the performer queue

        // ExtinguishActions are performed on LightSourceTraits
        var src = (LightSourceTrait)_performer;
        Item item = _gs.ObjDB.GetObj(src.ContainerID) as Item;
        Loc loc = item.Loc;

        if (item.ContainedBy > 0)
        {
            var owner = _gs.ObjDB.GetObj(item.ContainedBy);
            if (owner is not null)
            {
                // I don't think owner should ever be null, barring a bug
                // but this placates the warning in VS/VS Code
                loc = owner.Loc;
                ((IItemHolder)owner).Inventory.Remove(item.Slot, 1);
            }
        }
        
        _gs.CurrentMap.RemoveEffectFromMap(TerrainFlags.Lit, (item).ID);

        var cb = item.ContainedBy;
        var msg = MessageFactory.Phrase(item.ID, Verb.BurnsOut, 0, 1, false, loc, _gs);
        return new ActionResult() { Successful = true, Messages = [msg], EnergyCost = 1.0 };
    }
}

// I guess I can later add extra info about whether or not the player died, quit,
// or quit and saved?
class QuitAction : Action
{
    public override ActionResult Execute() => throw new GameQuitException();
}

class SaveGameAction : Action
{
    public override ActionResult Execute() => throw new Exception("Shouldn't actually try to execute a Save Game action!");
}

class NullAction : Action
{
    public override ActionResult Execute() => throw new Exception("Hmm this should never happen");
}