
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

using System.Data;

namespace Yarl2;

enum PlayerClass
{
    OrcReaver,
    DwarfStalwart
}

class Player : Actor, IPerformer, IItemHolder
{
    public int MaxVisionRadius { get; set; }
    public int CurrVisionRadius { get; set; }    
    public Inventory Inventory { get; set; } = new(PLAYER_ID);
    public double Energy { get; set; } = 0.0;
    public double Recovery { get; set; }
    public PlayerClass CharClass { get; set; }
    
    InputAccumulator? _accumulator;
    Action? _deferred;

    public Player(string name)
    {
        Name = name;
        MaxVisionRadius = 25;
        CurrVisionRadius = MaxVisionRadius;
        Recovery = 1.0; // Do I want a 'NaturalRecovery' or such to track cases when
                        // when a Player's recover is bolstered by, like, a Potion of Speed or such?
        Stats.Add(Attribute.HP, new Stat(20));
        Stats[Attribute.HP].Change(-3);
    }

    public override string FullName => "you";

    public override int AC 
    {
        get 
        {
            int ac = 10 + Stats[Attribute.Dexterity].Curr;

            int armour = 0;
            foreach (var slot in Inventory.UsedSlots())
            {
                var item = Inventory.ItemAt(slot);
                if (item.Equiped)
                {
                    armour += item.Traits.Where(t => t is ArmourTrait)
                                         .Select(t => (t as ArmourTrait).ArmourMod + (t as ArmourTrait).Bonus)
                                         .Sum();
                }
            }
            
            ac += Features.Where(f => f.Attribute == Attribute.ACMod)
                          .Select(f => f.Mod)
                          .Sum();
                          
            return ac + armour;
        }
    }

    public override int TotalMeleeAttackModifier()
    {
        int mod = Stats[Attribute.Strength].Curr;
               
        var weapon = Inventory.ReadiedWeapon();
        if (weapon is not null)
        {
            MeleeAttackTrait? attackTrait = (MeleeAttackTrait?) weapon.Traits
                                                                      .Where(t => t is MeleeAttackTrait)
                                                                      .FirstOrDefault() 
                                            ?? throw new Exception("Why would we have a weapon without an attack trait??");
            mod += attackTrait.Bonus;
        }

        if (Stats.TryGetValue(Attribute.MeleeAttackBonus, out var meleeAttackBonus))
            mod += meleeAttackBonus.Curr;

        return mod;
    }

    public override List<Damage> MeleeDamage()
    {
        List<Damage> dmgs = [];

        Item? weapon = Inventory.ReadiedWeapon();
        if (weapon is not null)
        {
            foreach (var trait in weapon.Traits)
            {
                if (trait is DamageTrait dmg)
                {
                    dmgs.Add(new Damage(dmg.DamageDie, dmg.NumOfDie, dmg.DamageType));
                }
            }            
        }
        else
        {
            // Perhaps eventually there will be a Monk Role, or one 
            // with claws or such
            dmgs.Add(new Damage(1, 1, DamageType.Blunt));
        }

        return dmgs;
    }

    // This doesn't really mean anything for the Player. An Exception will be tossed when
    // they are killed.
    public bool RemoveFromQueue { get; set; }

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
    
    void ShowInventory(UserInterface ui, string title, bool mentionMoney = false)
    {
        var slots = Inventory.UsedSlots();
        if (slots.Length == 0)
        {
            //ui.AlertPlayer("You are empty handed!");
            return;
        }

        List<string> lines = [ title ];
        foreach (var s in slots)
        {
            var item = Inventory.ItemAt(s);

            string desc;
            if (item.Count == 1)
                desc = item.FullName.IndefArticle();
            else
                desc = $"{item.Count} {item.FullName.Pluralize()}";

            if (item.Equiped)
            {
                if (item.Type == ItemType.Weapon)
                    desc += " (in hand)";
                else if (item.Type == ItemType.Armour)
                    desc += " (worn)";
            }
            lines.Add($"{s}) {desc}");
        }

        if (mentionMoney)
        {
            lines.Add("");
            if (Inventory.Zorkmids == 0)
                lines.Add("You seem to be broke.");
            else if (Inventory.Zorkmids == 1)
                lines.Add("You have a single zorkmid.");
            else
                lines.Add($"You wallet contains {Inventory.Zorkmids} zorkmids.");                
        }

        ui.ShowDropDown(lines);
    }

    static HashSet<char> ShowPickupMenu(UserInterface ui, List<Item> items)
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

    string PrintStat(Attribute attr)
    {
        int val = Stats[attr].Curr;
        return val > 0 ? $"+{val}" : $"{val}";
    }

    List<string> CharacterSheet()
    {
        List<string> lines = [];

        lines.Add($"{Name}, a level {Stats[Attribute.Level].Curr} {Util.PlayerClassToStr(CharClass)}");
        lines.Add("");
        lines.Add($"Str: {PrintStat(Attribute.Strength)}  Con: {PrintStat(Attribute.Constitution)}  Dex: {PrintStat(Attribute.Dexterity)}  Piety: {PrintStat(Attribute.Piety)}");
        lines.Add("");
        lines.Add($"You have earned {Stats[Attribute.XP].Max} XP.");
        lines.Add("");

        if (Stats[Attribute.Depth].Max == 0)
            lines.Add("You have yet to venture into the Dungeon.");
        else
            lines.Add($"You have ventured as deep as level {Stats[Attribute.Depth].Max}.");

        return lines;
    }

    public void ReplacePendingAction(Action newAction, InputAccumulator newAccumulator)
    {
        _deferred = newAction;
        _accumulator = newAccumulator;
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
                        ui.AlertPlayer(MessageFactory.Phrase("Nevermind.", gameState.Player.Loc), "");
                        return new NullAction();
                    }
                }
            }

            ui.ClosePopup();

            if (ch == 'h')
                return new MoveAction(this, Loc.Move(0, -1), gameState, ui.Rng);
            else if (ch == 'j')
                return new MoveAction(this, Loc.Move(1, 0), gameState, ui.Rng);
            else if (ch == 'k')
                return new MoveAction(this, Loc.Move(-1, 0), gameState, ui.Rng);
            else if (ch == 'l')
                return new MoveAction(this, Loc.Move(0, 1), gameState, ui.Rng);
            else if (ch == 'y')
                return new MoveAction(this,Loc.Move(-1, -1), gameState, ui.Rng);
            else if (ch == 'u')
                return new MoveAction(this, Loc.Move(-1, 1), gameState, ui.Rng);
            else if (ch == 'b')
                return new MoveAction(this, Loc.Move(1, -1), gameState, ui.Rng);
            else if (ch == 'n')
                return new MoveAction(this, Loc.Move(1, 1), gameState, ui.Rng);
            else if (ch == 'E')
                return new PortalAction(gameState);
            else if (ch == '>')
                return new DownstairsAction(gameState);
            else if (ch == '<')
                return new UpstairsAction(gameState);
            else if (ch == 'i')
            {
                ShowInventory(ui, "You are carrying:", true);
                _accumulator = new PauseForMoreAccumulator();
                _deferred = new CloseMenuAction(ui);
            }
            else if (ch == ',')
            {
                var itemStack = gameState.ObjDB.ItemsAt(Loc);

                if (itemStack is null || itemStack.Count == 0)
                {
                    ui.AlertPlayer(MessageFactory.Phrase("There's nothing there...", gameState.Player.Loc), "");
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
                ShowInventory(ui, "Drop what?", true);
                HashSet<char> slots = [.. Inventory.UsedSlots()];
                slots.Add('$');
                _accumulator = new MenuPickAccumulator(slots);
                _deferred = new DropItemAction(ui, this, gameState);
            }
            else if (ch == 'r')
            {
                ShowInventory(ui, "Read what?");
                _accumulator = new MenuPickAccumulator([.. Inventory.UsedSlots()]);
                _deferred = new ReadItemAction(ui, this, gameState);
            }
            else if (ch == 'e')
            {
                _accumulator = new MenuPickAccumulator([.. Inventory.UsedSlots()]);
                _deferred = new ToggleEquipedAction(ui, this, gameState);

                ShowInventory(ui, "Equip what?");
            }
            else if (ch == 'c')
            {
                _accumulator = new DirectionAccumulator();
                var action = new CloseDoorAction(ui.Player, gameState.Map, gameState);                
                _deferred = action;
                
                ui.AlertPlayer(MessageFactory.Phrase("Which way?", gameState.Player.Loc), "");
            }
            else if (ch == 'o')
            {
                _accumulator = new DirectionAccumulator();
                var action = new OpenDoorAction(ui.Player, gameState.Map, gameState);
                _deferred = action;

                ui.AlertPlayer(MessageFactory.Phrase("Which way?", gameState.Player.Loc), "");
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
            else if (ch == '@')
            {
                var lines = CharacterSheet();
                _accumulator = new LongMessageAccumulator(ui, lines);
                _deferred = new NullAction();
            }
            else
                return new PassAction(this);
        }

        return new NullAction();
    }
}
