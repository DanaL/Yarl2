
using System.Security.Cryptography;

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

        List<string> lines = [ "You are carrying: "];
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
                        if (_accumulator is DirectionAccumulator)
                        {
                            var acc = _accumulator as DirectionAccumulator;
                            (_deferred as DirectionalAction).Row += acc.Result.Item1;
                            (_deferred as DirectionalAction).Col += acc.Result.Item2;
                        }
                        else if (_accumulator is MenuPickAccumulator)
                        {
                            var acc = _accumulator as MenuPickAccumulator;
                            (_deferred as IMenuAction).Choice = acc.Choice;
                        }
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
                var action = new CloseDoorAction(ui.Player, gameState.Map)
                {
                    Row = ui.Player.Row,
                    Col = ui.Player.Col
                };
                _deferred = action;
                
                ui.WriteMessage("Which way?");
            }
            else if (ch == 'o')
            {
                _accumulator = new DirectionAccumulator();
                var action = new OpenDoorAction(ui.Player, gameState.Map);
                action.Row = ui.Player.Row;
                action.Col = ui.Player.Col;
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
