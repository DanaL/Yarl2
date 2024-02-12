
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

internal class Player : Actor
{
    public string Name { get; set; }
    public int MaxHP { get; set; }
    public int CurrHP { get; set; }
    private InputAccumulator? _accumulator;
    private Action _deferred;
    public Inventory Inventory { get; set; } = new();

    public Player(string name, int row, int col)
    {
        Name = name;
        Row = row;
        Col = col;
        MaxHP = 20;
        CurrHP = 15;
        MaxVisionRadius = 15;
        CurrVisionRadius = MaxVisionRadius;
    }

    private void ShowInventory(UserInterface ui)
    {
        var slots = Inventory.UsedSlots();
        if (slots.Length == 0)
        {
            ui.WriteMessage("You are empty handed!");
            return;
        }

        List<string> lines = [ "You are carrying:" ];
        foreach (var s in slots)
        {
            var item = Inventory.ItemAt(s);
            var desc = item.FullName.IndefArticle();
            if (item.Equiped)
            {
                if (item.Type == ItemType.Weapon)
                    desc += " (in hand)";
                else if (item.Type == ItemType.Armour)
                    desc += " (worn)";
            }
            lines.Add($"{s}) {desc}");
        }
        ui.ShowDropDown(lines);
    }

    private HashSet<char> ShowPickupMenu(UserInterface ui, List<Item> items)
    {
        HashSet<char> options = [];
        List<string> lines = [ "You see:"] ;
        char slot = 'a';
        foreach (var item in items)
        {
            options.Add(slot);
            var desc = item.FullName.IndefArticle();
            lines.Add($"{slot++}) {desc}");
        }
        ui.ShowDropDown(lines);

        return options;
    }
    
    public override void CalcEquipmentModifiers()
    {
        // I think this will get pulled up into a super class shared with monsters
        // or to Actor itself if I decide all Actors can have inventories
    }

    public override Action TakeTurn(UserInterface ui, GameState gameState)
    {
        if (ui.InputBuffer.Count > 0)
        {
            char ch = ui.InputBuffer.Dequeue();
            
            if (_accumulator is not null)
            {
                _accumulator.Input(ch);
                if (!_accumulator.Done)
                {
                    if (_accumulator.Msg != "")
                        ui.WriteMessage(_accumulator.Msg);
                    return new NullAction();
                }
                else
                {                    
                    if (_accumulator.Success)
                    {
                        _deferred.ReceiveAccResult(_accumulator.GetResult());
                        _accumulator = null;
                        return _deferred;
                    }
                    else
                    {
                        _accumulator = null;
                        ui.CloseMenu();
                        ui.WriteMessage("Nevermind.");
                        return new NullAction();
                    }
                }
            }

            if (ch == 'h')
                return new MoveAction(this, Row, Col - 1, gameState);
            else if (ch == 'j')
                return new MoveAction(this, Row + 1, Col, gameState);
            else if (ch == 'k')
                return new MoveAction(this, Row - 1, Col, gameState);
            else if (ch == 'l')
                return new MoveAction(this, Row, Col + 1, gameState);
            else if (ch == 'y')
                return new MoveAction(this, Row - 1, Col - 1, gameState);
            else if (ch == 'u')
                return new MoveAction(this, Row - 1, Col + 1, gameState);
            else if (ch == 'b')
                return new MoveAction(this, Row + 1, Col - 1, gameState);
            else if (ch == 'n')
                return new MoveAction(this, Row + 1, Col + 1, gameState);
            else if (ch == 'E')
                return new PortalAction(gameState);
            else if (ch == '>')
                return new DownstairsAction(gameState);
            else if (ch == '<')
                return new UpstairsAction(gameState);
            else if (ch == 'i')
            {
                ShowInventory(ui);
                _accumulator = new PauseForMoreAccumulator();
                _deferred = new CloseMenuAction(ui);
            }
            else if (ch == ',')
            {
                Loc loc = new Loc(gameState.CurrDungeon, gameState.CurrLevel, Row, Col);
                var itemStack = gameState.ItemDB.ItemsAt(loc);

                if (itemStack is null || itemStack.Count == 0)
                {
                    ui.WriteMessage("There's nothing there...");
                    return new NullAction();
                }
                else if (itemStack.Count == 1) 
                {
                    var a = new PickupItemAction(ui, this, gameState);
                    // A bit kludgy but this sets up the Action as though
                    // the player had selected the first item in a list of one
                    var mr = new MenuAccumulatorResult() { Choice = 'a' };                
                    a.ReceiveAccResult(mr); 
                    return a;
                }
                else 
                {
                    ui.WriteMessage("What do you pick up?");
                    var opts = ShowPickupMenu(ui, itemStack);
                    _accumulator = new MenuPickAccumulator(opts);
                    _deferred = new PickupItemAction(ui, this, gameState);
                }
            }
            else if (ch == 'd')
            {
                ui.WriteMessage("Drop what?");
                ShowInventory(ui);
                _accumulator = new MenuPickAccumulator([.. Inventory.UsedSlots()]);
                _deferred = new DropItemAction(ui, this, gameState);
            }
            else if (ch == 'e')
            {
                ui.WriteMessage("Equip what?");
                _accumulator = new MenuPickAccumulator([.. Inventory.UsedSlots()]);
                _deferred = new ToggleEquipedAction(ui, this);

                ShowInventory(ui);
            }
            else if (ch == 'c')
            {
                _accumulator = new DirectionAccumulator();
                var action = new CloseDoorAction(ui.Player, gameState.Map);                
                _deferred = action;
                
                ui.WriteMessage("Which way?");
            }
            else if (ch == 'o')
            {
                _accumulator = new DirectionAccumulator();
                var action = new OpenDoorAction(ui.Player, gameState.Map);
                _deferred = action;

                ui.WriteMessage("Which way?");
            }
            else if (ch == 'Q')
                return new QuitAction();
            else if (ch == 'S')
            {
                _accumulator = new YesNoAccumulator();
                _deferred = new SaveGameAction();
                ui.WriteMessage("Really quit and save? (y/n)");
            }
            else
                return new PassAction(this);
        }

        return new NullAction();
    }
}
