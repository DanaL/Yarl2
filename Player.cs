
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

internal class Player : Actor, IPerformer, IItemHolder
{
    private InputAccumulator? _accumulator;
    private Action? _deferred;
    public Inventory Inventory { get; set; } = new();
    public double Energy { get; set; } = 0.0;
    public double Recovery { get; set; }

    public Player(string name, int row, int col)
    {
        Name = name;
        Row = row;
        Col = col;
        MaxHP = 20;
        CurrHP = 15;
        MaxVisionRadius = 15;
        CurrVisionRadius = MaxVisionRadius;
        Recovery = 1.0; // Do I want a 'NaturalRecovery' or such to track cases when
                        // when a Player's recover is bolstered by, like, a Potion of Speed or such?
    }

    public override List<(ulong, int)> EffectSources(TerrainFlags flags, GameState gs) 
    {
        int playerVisionRadius = gs.InWilderness ? MaxVisionRadius : 1;
        List<(ulong, int)> sources = [ (ID, playerVisionRadius) ];

        foreach (var item in Inventory.UsedSlots().Select(s => Inventory.ItemAt(s)))
        {
            var itemSources = item.EffectSources(flags, gs);
            if (itemSources.Count > 0)
                sources.AddRange(itemSources);
        }

        return sources;
    }
    
    private void ShowInventory(UserInterface ui, string title = "You are carrying:")
    {
        var slots = Inventory.UsedSlots();
        if (slots.Length == 0)
        {
            ui.AlertPlayer("You are empty handed!");
            return;
        }

        List<string> lines = [ title ];
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
        List<string> lines = [ "What do you pick up?"] ;
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
    
    public void CalcEquipmentModifiers()
    {
        
    }

    public Action TakeTurn(UserInterface ui, GameState gameState)
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
                        ui.Popup(_accumulator.Msg);
                    return new NullAction();
                }
                else
                {                    
                    if (_accumulator.Success)
                    {
                        _deferred.ReceiveAccResult(_accumulator.GetResult());
                        _accumulator = null;
                        ui.ClosePopup();
                        return _deferred;
                    }
                    else
                    {
                        _accumulator = null;
                        ui.CloseMenu();
                        ui.ClosePopup();
                        ui.AlertPlayer("Nevermind.");
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
                Loc loc = new(gameState.CurrDungeon, gameState.CurrLevel, Row, Col);
                var itemStack = gameState.ObjDB.ItemsAt(loc);

                if (itemStack is null || itemStack.Count == 0)
                {
                    ui.AlertPlayer("There's nothing there...");
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
                    var opts = ShowPickupMenu(ui, itemStack);
                    _accumulator = new MenuPickAccumulator(opts);
                    _deferred = new PickupItemAction(ui, this, gameState);
                }
            }
            else if (ch == 'a')
            {
                ShowInventory(ui, "Use which item?");
                _accumulator = new MenuPickAccumulator([.. Inventory.UsedSlots()]);
                _deferred = new UseItemAction(ui, this, gameState);
            }
            else if (ch == 'd')
            {
                ShowInventory(ui, "Drop what?");
                _accumulator = new MenuPickAccumulator([.. Inventory.UsedSlots()]);
                _deferred = new DropItemAction(ui, this, gameState);
            }
            else if (ch == 'e')
            {
                _accumulator = new MenuPickAccumulator([.. Inventory.UsedSlots()]);
                _deferred = new ToggleEquipedAction(ui, this);

                ShowInventory(ui, "Equip what?");
            }
            else if (ch == 'c')
            {
                _accumulator = new DirectionAccumulator();
                var action = new CloseDoorAction(ui.Player, gameState.Map, gameState);                
                _deferred = action;
                
                ui.AlertPlayer("Which way?");
            }
            else if (ch == 'o')
            {
                _accumulator = new DirectionAccumulator();
                var action = new OpenDoorAction(ui.Player, gameState.Map, gameState);
                _deferred = action;

                ui.AlertPlayer("Which way?");
            }
            else if (ch == 'Q')
            {
                _accumulator = new YesNoAccumulator();
                _deferred = new QuitAction();
                ui.Popup("Really quit?\n\nYour game won't be saved! (y/n)");
            }                
            else if (ch == 'S')
            {
                _accumulator = new YesNoAccumulator();
                _deferred = new SaveGameAction();
                ui.Popup("Quit & Save? (y/n)");
            }
            else if (ch == '*')
            {
                var lines = ui.MessageHistory.Select(m => m.Fmt);
                _accumulator = new LongMessageAccumulator(ui, lines);
                _deferred = new NullAction();
            }
            else
                return new PassAction(this);
        }

        return new NullAction();
    }
}
