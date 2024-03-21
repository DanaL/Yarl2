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
    public bool Complete { get; set; }
    public List<Message> Messages { get; set; } = [];
    public string MessageIfUnseen { get; set; } = "";
    public Action? AltAction { get; set; }
    public double EnergyCost { get; set; } = 0.0;
    public bool PlayerKilled { get; set; } = false;

    public ActionResult() { }
}

abstract class Action
{
    public abstract ActionResult Execute { get; }

    public virtual void ReceiveAccResult(AccumulatorResult result) {}
}

class MeleeAttackAction(Actor actor, Loc loc, GameState gs, Random rng) : Action
{
    GameState _gs = gs;
    Loc _loc = loc;
    Actor _actor = actor;
    Random _rng = rng;

    public override ActionResult Execute
    {
        get
        {
            var result = new ActionResult() { Complete = true };

            var target = _gs.ObjDB.Occupant(_loc);
            if (target is not null)
                result = Battle.MeleeAttack(_actor, target, _gs, _rng);

            return result;
        }
    }
}

class MissileAttackAction(Actor actor, Loc loc, GameState gs, Item ammo, Random rng) : Action
{
    GameState _gs = gs;
    Loc _loc = loc;
    Actor _actor = actor;
    Random _rng = rng;
    Item _ammo = ammo;

    public override ActionResult Execute
    {
        get
        {
            var result = new ActionResult() { Complete = true };

            var target = _gs.ObjDB.Occupant(_loc);
            if (target is not null)
                result = Battle.MissileAttack(_actor, target, _gs, _ammo, _rng);

            return result;
        }
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

        result.Complete = true;

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

    public override ActionResult Execute
    {
        get
        {
            var result = new ActionResult() { Complete = false };

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
}

class DownstairsAction(GameState gameState) : PortalAction(gameState)
{
    public override ActionResult Execute
    {
        get
        {
            var result = new ActionResult() { Complete = false };

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
}

class UpstairsAction(GameState gameState) : PortalAction(gameState)
{
    public override ActionResult Execute
    {
        get
        {
            var result = new ActionResult() { Complete = false };

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
}

class ShopAction(Villager shopkeeper, GameState gs) : Action
{    
    GameState _gs = gs;
    Villager _shopkeeper = shopkeeper;
    int _invoice;
    List<(char, int)> _selections = [];

    public override ActionResult Execute
    {
        get
        {
            var result = new ActionResult()
            {
                Complete = _invoice > 0,
                EnergyCost = 1.0
            };

            _gs.Player.Inventory.Zorkmids -= _invoice;

            foreach (var (slot, count) in _selections)
            {
                List<Item> bought = _shopkeeper.Inventory.Remove(slot, count);
                foreach (var item in bought)
                    _gs.Player.Inventory.Add(item, _gs.Player.ID);
            }

            string txt = $"You pay {_shopkeeper.FullName} {_invoice} zorkmid";
            if (_invoice > 1)
                txt += "s";
            txt += " and collect your goods.";

            var msg = new Message(txt, _gs.Player.Loc);
            result.Messages.Add(msg);

            return result;
        }
    }

    public override void ReceiveAccResult(AccumulatorResult result)
    {
        var shopResult = result as ShoppingAccumulatorResult;
        _invoice = shopResult.Zorkminds;
        _selections = shopResult.Selections;
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

    public override ActionResult Execute
    {
        get
        {
            var result = new ActionResult() { Complete = false };

            var other = _gs.ObjDB.Occupant(_loc);

            if (other is null)
            {
                result.Messages.Add(MessageFactory.Phrase("There's no one there!", _actor.Loc));
            }
            else
            {
                var txt = other.ChatText();
                if (string.IsNullOrEmpty(txt))
                {
                    result.Messages.Add(MessageFactory.Phrase("They aren't interested in chatting.", _actor.Loc));
                    result.Complete = true;
                    result.EnergyCost = 1.0;
                }
                else
                {
                    var (action, acc) = other.Chat(_gs);
                    _gs.Player.ReplacePendingAction(action, acc);
                }

                return new ActionResult() { Complete = false, EnergyCost = 0.0 };
            }

            return result;
        }
    }
}

class CloseDoorAction(Actor actor, Map map, GameState gs) : DirectionalAction(actor)
{
    private GameState _gs = gs;
    private readonly Map _map = map;

    public override ActionResult Execute
    {
        get
        {
            var result = new ActionResult() { Complete = false };
            var door = _map.TileAt(_loc.Row, _loc.Col);

            if (door is Door d)
            {
                if (d.Open)
                {
                    d.Open = false;
                    result.Complete = true;
                    result.EnergyCost = 1.0;

                    var msg = MessageFactory.Phrase(_actor.ID, Verb.Close, "the door", false, _loc, _gs);
                    result.Messages.Add(msg);
                    result.MessageIfUnseen = "You hear a door close.";

                    // Find any light sources tht were affecting the door and update them, since
                    // it's now open. (Eventually gotta extend it to any aura type effects)
                    foreach (var src in _gs.ObjsAffectingLoc(_loc, TerrainFlag.Lit))
                    {
                        _gs.CurrentMap.RemoveEffectFromMap(TerrainFlag.Lit, src.ID);
                        _gs.ToggleEffect(src, _actor.Loc, TerrainFlag.Lit, true);
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

    public override ActionResult Execute
    {
        get
        {
            var result = new ActionResult() { Complete = false };
            var door = _map.TileAt(_loc.Row, _loc.Col);

            if (door is Door d)
            {
                if (!d.Open)
                {
                    d.Open = true;
                    result.Complete = true;
                    result.EnergyCost = 1.0;

                    var msg = MessageFactory.Phrase(_actor.ID, Verb.Open, "door", false, _loc, _gs);
                    result.Messages.Add(msg);
                    result.MessageIfUnseen = "You hear a door open.";

                    // Find any light sources tht were affecting the door and update them, since
                    // it's now open. (Eventually gotta extend it to any aura type effects)

                    foreach (var src in _gs.ObjsAffectingLoc(_loc, TerrainFlag.Lit))
                    {
                        _gs.ToggleEffect(src, src.Loc, TerrainFlag.Lit, true);
                    }
                    _gs.ToggleEffect(_actor, _loc, TerrainFlag.Lit, true);
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
        //else if (items[0].Count > 1)
        //{
        //    return $"There are {items[0].Count} {items[0].FullName.Pluralize()} here.";
        //}
        else
        {
            return $"There is {items[0].FullName.IndefArticle()} here.";
        }
    }

    bool CanMoveTo()
    {
        var tile = _map.TileAt(_loc.Row, _loc.Col);

        if (tile.Passable())
            return true;
        else if (_actor.HasActiveTrait<FlyingTrait>() && tile.PassableByFlight())
            return true;

        return false;
    }

    public override ActionResult Execute
    {
        get
        {
            var result = new ActionResult();

            if (!_map.InBounds(_loc.Row, _loc.Col))
            {
                // in theory this shouldn't ever happen...
                result.Complete = false;
                if (_actor is Player)
                    result.Messages.Add(MessageFactory.Phrase("You cannot go that way!", _gs.Player.Loc));
            }
            else if (_gs.ObjDB.Occupied(_loc))
            {
                result.Complete = false;
                var occ = _gs.ObjDB.Occupant(_loc);
                if (occ is VillageAnimal)
                {
                    string msg;
                    if (_rng.NextDouble() < 0.5)
                        msg = $"You pat {occ.FullName}.";
                    else
                        msg = $"You give {occ.FullName} some scritches.";
                    _gs.UI.Popup(msg);
                    result.EnergyCost = 1.0;
                    result.Complete = true;
                    //result.Messages.Add(MessageFactory.Phrase(msg, _gs.Player.Loc));
                }
                else if (occ is not null && !occ.Hostile)
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
            else if (!CanMoveTo())
            {
                result.Complete = false;

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
                result.Complete = true;
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
                        var txt = _actor.HasActiveTrait<FlyingTrait>() ? "You hear softly beating wings..."
                                                                 : "You hear padding footsteps...";
                        result.Messages.Add(new Message(txt, _loc, true));
                    }
                }
            }

            return result;
        }
    }
}

class PickupItemAction(UserInterface ui, Actor actor, GameState gs) : Action
{
    public char Choice { get; set; }
    private UserInterface _ui = ui;
    private Actor _actor = actor;
    private GameState _gameState = gs;

    public override ActionResult Execute
    {
        get
        {
            _ui.CloseMenu();
            var itemStack = _gameState.ObjDB.ItemsAt(_actor.Loc);

            var inv = _actor.Inventory;
            bool freeSlot = inv.UsedSlots().Length < 26;
            Message msg;

            if (!freeSlot)
            {
                msg = MessageFactory.Phrase("There's no room in your inventory!", _gameState.Player.Loc);
                return new ActionResult() { Complete = false, Messages = [msg] };
            }

            int i = Choice - 'a';
            var item = itemStack[i];
            gs.ObjDB.RemoveItem(_actor.Loc, item);
            inv.Add(item, _actor.ID);

            msg = MessageFactory.Phrase(_actor.ID, Verb.Pickup, item.ID, 1, false, _actor.Loc, _gameState);
            return new ActionResult() { Complete = true, Messages = [msg], EnergyCost = 1.0 };
        }
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
    UserInterface _ui = ui;
    Actor _actor = actor;
    GameState _gs = gs;

    public override ActionResult Execute
    {
        get
        {
            var (item, itemCount) = _actor.Inventory.ItemAt(Choice);
            _ui.CloseMenu();

            var useableTraits = item.Traits.Where(t => t is IUSeable).ToList();
            if (useableTraits.Count != 0)
            {
                var result = new ActionResult() { Complete = true, EnergyCost = 1.0 };
                Item? toUse = _actor.Inventory.RemoveByID(item.ID)
                                ?? throw new Exception("Using item in inventory that doesn't exist :O This shouldn't happen :O");
                toUse.Stackable = false;
                if (!toUse.Consumable)
                    _actor.Inventory.Add(toUse, _actor.ID);

                bool success = false;
                foreach (IUSeable trait in useableTraits)
                {
                    var useResult = trait.Use(_actor, _gs, _actor.Loc.Row, _actor.Loc.Col);
                    result.Complete = useResult.Successful;
                    var alert = MessageFactory.Phrase(useResult.Message, _actor.Loc);
                    result.Messages.Add(alert);
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
                var msg = MessageFactory.Phrase("You don't know a way to use that!", _gs.Player.Loc);
                return new ActionResult() { Complete = true, Messages = [msg], EnergyCost = 0.0 };
            }
        }
    }

    public override void ReceiveAccResult(AccumulatorResult result)
    {
        var menuResult = (MenuAccumulatorResult)result;
        Choice = menuResult.Choice;
    }
}

class FogCloudAction(Actor caster, GameState gs, Loc target) : Action
{
    readonly ulong _casterID = caster.ID;
    readonly GameState _gs = gs;
    readonly Loc _target = target;

    public override ActionResult Execute
    {
        get
        {
            for (int r = _target.Row - 2; r < _target.Row + 3; r++)
            {
                for (int c = _target.Col - 2; c < _target.Col + 3; c++)
                {
                    if (!_gs.CurrentMap.InBounds(r, c))
                        continue;
                    var mist = ItemFactory.Get("mist", _gs.ObjDB);
                    var mistLoc = _target with { Row = r, Col = c };
                    _gs.ItemDropped(mist, mistLoc);
                    var t = (ExpiresTrait)mist.Traits.First(t => t is ExpiresTrait);
                    t.ExpiresOn = _gs.Turn + 10;
                    _gs.Performers.Add(t);
                }
            }

            var txt = MessageFactory.Phrase(_casterID, Verb.Cast, _target, _gs).Text;
            var msg = new Message(txt + " Fog Cloud!", _target, false);
            return new ActionResult() { Complete = true, Messages = [msg], EnergyCost = 1.0 };
        }
    }
}

class EntangleAction(Actor caster, GameState gs, Loc target) : Action
{
    readonly ulong _casterID = caster.ID;
    readonly GameState _gs = gs;
    readonly Loc _target = target;

    public override ActionResult Execute
    {
        get
        {
            foreach (var (r, c) in Util.Adj8Sqs(_target.Row, _target.Col))
            {
                var loc = _target with { Row = r, Col = c };
                var tile = _gs.TileAt(loc);
                if (tile.Type != TileType.Unknown && tile.Passable() && !_gs.ObjDB.Occupied(loc))
                {
                    Actor vines = MonsterFactory.Get("vines", _gs.UI.Rng);
                    vines.Loc = loc;
                    _gs.ObjDB.Add(vines);
                    _gs.ObjDB.AddToLoc(loc, vines);
                }
            }

            var txt = MessageFactory.Phrase(_casterID, Verb.Cast, _target, _gs).Text;
            var msg = new Message(txt + " Entangle!", _target, false);
            return new ActionResult() { Complete = true, Messages = [msg], EnergyCost = 1.0 };
        }
    }
}

class BlinkAction(Actor caster, GameState gs) : Action
{
    readonly Actor _caster = caster;
    readonly GameState _gs = gs;

    public override ActionResult Execute
    {
        get
        {
            List<Loc> sqs = [];
            var start = _caster.Loc;

            for (var r = start.Row - 12; r < start.Row + 12; r++)
            {
                for (var c = start.Col - 12; c < start.Col + 12; c++)
                {
                    var loc = start with { Row = r, Col = c };
                    int d = Util.Distance(start, loc);
                    if (d >= 8 && d <= 12 && _gs.TileAt(loc).Passable() && !_gs.ObjDB.Occupied(loc))
                    {
                        sqs.Add(loc);
                    }
                }
            }

            if (sqs.Count == 0)
            {
                var msg = new Message("A spell fizzles...", _caster.Loc);
                return new ActionResult() { Complete = true, Messages = [msg], EnergyCost = 1.0 };
            }
            else
            {
                var landingSpot = sqs[_gs.UI.Rng.Next(sqs.Count)];
                var mv = new MoveAction(_caster, landingSpot, _gs, _gs.UI.Rng);
                _gs.UI.RegisterAnimation(new SqAnimation(_gs, landingSpot, Colours.WHITE, Colours.LIGHT_PURPLE, '*'));
                _gs.UI.RegisterAnimation(new SqAnimation(_gs, start, Colours.WHITE, Colours.LIGHT_PURPLE, '*'));
                var msg = MessageFactory.Phrase(_caster.ID, Verb.Blink, _caster.Loc, _gs);
                var txt = $"Bamf! {msg.Text} away!";
                msg = new Message(txt, _caster.Loc);

                return new ActionResult() { Complete = false, Messages = [msg], EnergyCost = 0.0, AltAction = mv };
            }
        }
    }
}

class HealAction(Actor target, GameState gs, int healDie, int healDice) : Action
{
    readonly Actor _target = target;
    readonly GameState _gs = gs;
    readonly int _healDie = healDie;
    readonly int _healDice = healDice;

    public override ActionResult Execute
    {
        get
        {
            var hp = 0;
            for (int j = 0; j < _healDice; j++)
                hp += _gs.UI.Rng.Next(_healDie) + 1;
            _target.Stats[Attribute.HP].Change(hp);
            var plural = _target.HasTrait<PluralTrait>();
            var msg = MessageFactory.Phrase(_target.ID, Verb.Etre, Verb.Heal, plural, false, _target.Loc, _gs);
            var txt = msg.Text[..^1] + $" for {hp} HP.";

            return new ActionResult() { Complete = true, Messages = [new Message(txt, _target.Loc, false)], EnergyCost = 1.0 };
        }
    }
}

class DropZorkmidsAction(UserInterface ui, Actor actor, GameState gs) : Action
{
    readonly UserInterface _ui = ui;
    readonly Actor _actor = actor;
    readonly GameState _gs = gs;
    int _amount;

    public override ActionResult Execute
    {
        get
        {
            double cost = 1.0;
            bool successful = true;
            Message alert;

            var inventory = _actor.Inventory;
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

                var coins = ItemFactory.Get("zorkmids", _gs.ObjDB);
                _gs.ItemDropped(coins, _actor.Loc);
                coins.Value = _amount;
                string msg;
                if (_amount == 1)
                    msg = "a single zorkmid";
                else if (_amount == inventory.Zorkmids)
                    msg = "all your money";
                else
                    msg = $"{_amount} zorkmids";

                alert = MessageFactory.Phrase(_actor.ID, Verb.Drop, msg, false, _actor.Loc, _gs);

                inventory.Zorkmids -= _amount;
            }

            return new ActionResult() { Complete = successful, Messages = [alert], EnergyCost = cost };
        }
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
    private readonly GameState _gs = gs;
    private readonly char _slot = slot;
    private int _amount;

    public override ActionResult Execute
    {
        get
        {
            var (item, itemCount) = _actor.Inventory.ItemAt(_slot);
            _ui.ClosePopup();

            if (_amount == 0 || _amount > itemCount)
                _amount = itemCount;

            var droppedItems = _actor.Inventory.Remove(_slot, _amount);
            foreach (var droppedItem in droppedItems)
            {
                _gs.ItemDropped(droppedItem, _actor.Loc);
                droppedItem.Equiped = false;
            }

            _actor.CalcEquipmentModifiers();
            Message alert = MessageFactory.Phrase(_actor.ID, Verb.Drop, item.ID, _amount, false, _actor.Loc, _gs);

            return new ActionResult() { Complete = true, Messages = [alert], EnergyCost = 1.0 };
        }
    }

    public override void ReceiveAccResult(AccumulatorResult result)
    {
        var count = ((NumericAccumulatorResult)result).Amount;
        _amount = count;
    }
}

class ThrowAction(UserInterface ui, Actor actor, GameState gs, char slot) : Action
{    
    readonly UserInterface _ui = ui;
    readonly Actor _actor = actor;
    readonly GameState _gs = gs;
    readonly char _slot = slot;
    Loc _target { get; set; }

    Loc FinalLandingSpot (Loc loc)
    {
        var tile = _gs.TileAt(loc);

        while (tile.Type == TileType.Chasm)
        {
            loc = loc with { Level = loc.Level + 1 };
            tile = _gs.TileAt(loc);
        }

        return loc;
    }

    void ProjectileLands(List<Loc> pts, Item ammo, ActionResult result)
    {
        var landingPt = FinalLandingSpot(pts.Last());
        _gs.CheckMovedEffects(ammo, _actor.Loc, landingPt);
        _gs.ItemDropped(ammo, landingPt);
        ammo.Equiped = false;
        ammo.SetZ(-1);
        _actor.CalcEquipmentModifiers();

        var tile = _gs.TileAt(landingPt);
        if (tile.Type == TileType.Chasm)
        {
            string txt = $"{ammo.FullName.DefArticle().Capitalize()} tumbles into the darkness.";
            var msg = new Message(txt, landingPt);
            result.Messages.Add(msg);
        }

        var anim = new ThrownMissileAnimation(_ui, ammo.Glyph, pts, ammo);
        _ui.RegisterAnimation(anim);
    }

    public override ActionResult Execute
    {
        get
        {
            var result = new ActionResult() { Complete = true, EnergyCost = 1.0 };
            var ammo = _actor.Inventory.Remove(_slot, 1).First();
            if (ammo != null)
            {
                // Calculate where the projectile will actually stop
                var trajectory = Util.Bresenham(_actor.Loc.Row, _actor.Loc.Col, _target.Row, _target.Col)
                                     .Select(p => new Loc(_actor.Loc.DungeonID, _actor.Loc.Level, p.Item1, p.Item2))
                                     .ToList();
                List<Loc> pts = [];
                for (int j = 0; j < trajectory.Count; j++)
                {
                    var pt = trajectory[j];
                    var tile = _gs.TileAt(pt);
                    var occ = _gs.ObjDB.Occupant(pt);
                    if (j > 0 && occ != null)
                    {
                        pts.Add(pt);

                        // I'm not handling what happens if a projectile hits a friendly or 
                        // neutral NPCs
                        var attackResult = Battle.MissileAttack(_actor, occ, _gs, ammo, _ui.Rng);
                        result.Messages.AddRange(attackResult.Messages);
                        result.EnergyCost = attackResult.EnergyCost;
                        if (attackResult.Complete)
                        {
                            break;
                        }
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

                ProjectileLands(pts, ammo, result);
            }

            return result;
        }
    }

    public override void ReceiveAccResult(AccumulatorResult result)
    {
        var locResult = result as LocAccumulatorResult;
        _target = locResult.Loc;
    }
}

class ThrowSelectionAction(UserInterface ui, Player player, GameState gs) : Action
{
    public char Choice { get; set; }
    readonly UserInterface _ui = ui;
    readonly Player _player = player;
    readonly GameState _gs = gs;

    public override ActionResult Execute
    {
        get
        {
            _ui.CloseMenu();

            var (item, _) = _player.Inventory.ItemAt(Choice);
            if (item is null)
            {
                var msg = new Message("That doesn't make sense", _player.Loc);
                var result = new ActionResult() { Complete = false, EnergyCost = 0.0 };
                result.Messages.Add(msg);
                return result;
            }
            else if (item.Type == ItemType.Armour && item.Equiped)
            {
                var msg = new Message("You're wearing that!", _player.Loc);
                var result = new ActionResult() { Complete = false, EnergyCost = 0.0 };
                result.Messages.Add(msg);
                return result;
            }

            var action = new ThrowAction(_ui, _player, _gs, Choice);
            var range = 7 + _player.Stats[Attribute.Strength].Curr;
            if (range < 2)
                range = 2;
            var acc = new AimAccumulator(_ui, _player.Loc, range);
            _player.ReplacePendingAction(action, acc);

            return new ActionResult() { Complete = false, EnergyCost = 0.0 };
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
    readonly UserInterface _ui = ui;
    readonly Actor _actor = actor;
    readonly GameState _gameState = gs;

    public override ActionResult Execute
    {
        get
        {
            _ui.CloseMenu();

            if (Choice == '$')
            {
                var inventory = _actor.Inventory;
                if (inventory.Zorkmids == 0)
                {
                    var msg = MessageFactory.Phrase("You have no money!", _gameState.Player.Loc);
                    return new ActionResult() { Complete = false, Messages = [msg] };
                }
                var dropMoney = new DropZorkmidsAction(_ui, _actor, _gameState);
                _ui.Popup("How much?");
                var acc = new NumericAccumulator(_ui, "How much?");
                if (_actor is Player player)
                {
                    player.ReplacePendingAction(dropMoney, acc);
                    return new ActionResult() { Complete = false, EnergyCost = 0.0 };
                }
                else
                    // Will monsters ever just decide to drop money?
                    return new ActionResult() { Complete = true };
            }

            var (item, itemCount) = _actor.Inventory.ItemAt(Choice);
            if (item.Equiped && item.Type == ItemType.Armour)
            {
                var msg = MessageFactory.Phrase("You cannot drop something you're wearing.", _gameState.Player.Loc);
                return new ActionResult() { Complete = false, Messages = [msg] };
            }
            else if (itemCount > 1)
            {
                var dropStackAction = new DropStackAction(_ui, _actor, _gameState, Choice);
                var prompt = $"Drop how many {item.FullName.Pluralize()}?\n(enter for all)";
                _ui.Popup(prompt);
                var acc = new NumericAccumulator(_ui, prompt);
                if (_actor is Player player)
                {
                    player.ReplacePendingAction(dropStackAction, acc);
                    return new ActionResult() { Complete = false, EnergyCost = 0.0 };
                }
                else
                    // When monsters can drop stuff I guess I'll have to handle that here??
                    return new ActionResult() { Complete = true };
            }
            else
            {
                _actor.Inventory.Remove(Choice, 1);
                _gameState.ItemDropped(item, _actor.Loc);
                item.Equiped = false;
                _actor.CalcEquipmentModifiers();

                var alert = MessageFactory.Phrase(_actor.ID, Verb.Drop, item.ID, 1, false, _actor.Loc, _gameState);
                _ui.AlertPlayer([alert], "");
                return new ActionResult() { Complete = true, EnergyCost = 1.0 };
            }
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

    public override ActionResult Execute
    {
        get
        {
            ActionResult result;
            var (item, _) = _actor.Inventory.ItemAt(Choice);
            _ui.CloseMenu();

            if (!(item.Type == ItemType.Armour || item.Type == ItemType.Weapon || item.Type == ItemType.Tool))
            {
                var msg = MessageFactory.Phrase("You cannot equip that!", _gameState.Player.Loc);
                return new ActionResult() { Complete = false, Messages = [msg] };
            }

            var (equipResult, conflict) = ((Player)_actor).Inventory.ToggleEquipStatus(Choice);
            Message alert;
            switch (equipResult)
            {
                case EquipingResult.Equiped:
                    alert = MessageFactory.Phrase(_actor.ID, Verb.Ready, item.ID, 1, false, _actor.Loc, _gameState);
                    result = new ActionResult() { Complete = true, Messages = [alert], EnergyCost = 1.0 };
                    break;
                case EquipingResult.Unequiped:
                    alert = MessageFactory.Phrase(_actor.ID, Verb.Unready, item.ID, 1, false, _actor.Loc, _gameState);
                    result = new ActionResult() { Complete = true, Messages = [alert], EnergyCost = 1.0 };
                    break;
                default:
                    string msg = "You are already wearing ";
                    if (conflict == ArmourParts.Hat)
                        msg += "a helmet.";
                    else if (conflict == ArmourParts.Shirt)
                        msg += "some armour.";
                    alert = MessageFactory.Phrase(msg, _gameState.Player.Loc);
                    result = new ActionResult() { Complete = true, Messages = [alert] };
                    break;
            }

            _actor.CalcEquipmentModifiers();

            return result;
        }
    }

    public override void ReceiveAccResult(AccumulatorResult result)
    {
        var menuResult = (MenuAccumulatorResult)result;
        Choice = menuResult.Choice;
    }
}

sealed class PassAction : Action
{
    public sealed override ActionResult Execute 
        => new() { Complete = true, EnergyCost = 1.0 };
}

class CloseMenuAction(UserInterface ui, double energyCost = 0.0) : Action 
{
    private UserInterface _ui = ui;
    private double _energyCost = energyCost;

    public override ActionResult Execute
    {
        get
        {
            _ui.CloseMenu();
            return new ActionResult() { Complete = true, EnergyCost = _energyCost };
        }
    }
}

class ExtinguishAction(IPerformer performer, GameState gs) : Action
{
    IPerformer _performer = performer;
    GameState _gs = gs;

    public override ActionResult Execute
    {
        get
        {
            _performer.RemoveFromQueue = true; // signal to remove it from the performer queue

            // ExtinguishActions are performed on LightSourceTraits
            var src = (FlameLightSourceTrait)_performer;
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
                    ((Actor)owner).Inventory.Remove(item.Slot, 1);
                }
            }

            _gs.CurrentMap.RemoveEffectFromMap(TerrainFlag.Lit, (item).ID);

            var cb = item.ContainedBy;
            var msg = MessageFactory.Phrase(item.ID, Verb.BurnsOut, 0, 1, false, loc, _gs);
            return new ActionResult() { Complete = true, Messages = [msg], EnergyCost = 1.0 };
        }
    }
}

class ObjTraitExpiredAction(IPerformer performer, GameState gs) : Action
{
    IPerformer _performer = performer;
    GameState _gs = gs;

    public sealed override ActionResult Execute
    {
        get
        {
            var result = new ActionResult() { Complete = true, EnergyCost = 1.0 };

            _performer.RemoveFromQueue = true;
            var src = (ExpiresTrait)_performer;

            // item being null should always be a bug, actually, I think
            if (_gs.ObjDB.GetObj(src.ContainerID) is Item item)
            {
                Loc loc = item.Loc;

                // Alert! Alert! This is cut-and-pasted from ExtinguishAction()
                if (item.ContainedBy > 0)
                {
                    var owner = _gs.ObjDB.GetObj(item.ContainedBy);
                    if (owner is not null)
                    {
                        // I don't think owner should ever be null, barring a bug
                        // but this placates the warning in VS/VS Code
                        loc = owner.Loc;
                        ((Actor)owner).Inventory.Remove(item.Slot, 1);
                    }
                }

                _gs.ObjDB.RemoveItem(loc, item);

                var msg = MessageFactory.Phrase(item.ID, Verb.Dissipate, 0, 1, false, loc, _gs);
                result.Messages.Add(msg);
            }

            return result;
        }
    }
}

// I guess I can later add extra info about whether or not the player died, quit,
// or quit and saved?
class QuitAction : Action
{
    public override ActionResult Execute => throw new GameQuitException();
}

class SaveGameAction : Action
{
    public override ActionResult Execute => throw new Exception("Shouldn't actually try to execute a Save Game action!");
}

class NullAction : Action
{
    public override ActionResult Execute => throw new Exception("Hmm this should never happen");
}