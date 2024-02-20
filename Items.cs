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

enum ItemType
{
    Weapon,
    Armour,
    Zorkmid,
    Tool
}

interface IUseableItem
{
    (bool, string) Use(GameState gs, int row, int col);
}

class Item : GameObj
{
    public ItemType Type { get; set; }
    public int ArmourMod { get; set; }
    public bool Stackable { get; set; }
    public char Slot { get; set; }
    public bool Equiped { get; set; } = false;
    public int Count { get; set; } = 1;
    public int Bonus { get; set; } = 0;
    public ulong ContainedBy { get; set; } = 0;

    public List<string> Adjectives { get; set; } = [];

    public virtual string FullName {
        get 
        {
            if (Adjectives.Count == 0)
                return Name;
            else if (Adjectives.Count == 1)
                return $"{Adjectives[0]} {Name}";
            else
                return $"{string.Join(", ", Adjectives)} {Name}"; 
        }
    }

    public Item Duplicate(GameState gs)
    {
        var item = (Item)MemberwiseClone();
        item.ID = NextID;
        gs.ObjDB.Add(item);
        return item;
    }
}

enum ArmourParts
{
    Helmet,
    Boots,
    Cloak,
    Shirt
}

class Armour : Item
{
    public ArmourParts Piece { get; set; }
}

class Torch : Item, IPerformer, IUseableItem
{
    public bool Lit { get; set; }
    public int Fuel { get; set; }
    public bool RemoveFromQueue { get; set; }
    public double Energy { get; set; }
    public double Recovery
    {
        get => 1.0;
        set { } // I don't think the recovery value for torches will change
    }

    public override string FullName => Lit ? base.FullName + " (lit)" : base.FullName;
    
    public override Glyph Glyph
    {
        get => Lit ? new Glyph('(', Colours.DULL_RED, Colours.YELLOW_ORANGE)
                   : new Glyph('(', Colours.LIGHT_BROWN, Colours.BROWN);
    }

    public override List<(ulong, int)> EffectSources(TerrainFlags flags, GameState gs) => Lit ? [(ID, 5)] : [];

    public (bool, string) Use(GameState gs, int row, int col)
    {
        var loc = new Loc(gs.CurrDungeon, gs.CurrLevel, row, col);
        if (Lit)
        {            
            gs.CurrPerformers.Remove(this);

            // Gotta set the lighting level before we extinguish the torch
            // so it's radius is still 5 when calculating which squares to 
            // affect
            gs.ToggleEffect(this, loc, TerrainFlags.Lit, false);
            Lit = false;

            return (true, $"You extinguish {Name.DefArticle()}.");
        }
        else if (Fuel > 0)
        {
            Lit = true;
            Stackable = false;
            Energy = Recovery;
            gs.CurrPerformers.Add(this);
            gs.ToggleEffect(this, loc, TerrainFlags.Lit, true);

            return (true, $"The {Name} sparks to life!");
        }
        else
        {
            return (false, $"That {Name} is burnt out!");
        }
    }

    public Action TakeTurn(UserInterface ui, GameState gameState)
    {
        if (!Lit)
            return new PassAction(this);

        if (--Fuel > 0)
        {
            // I could also alert the player here that the torch is flickering, about to go out, etc            
            return new PassAction(this);
        }
        else
        {
            Lit = false;
            return new ExtinguishAction(this, gameState);
        }
    }
}

class ItemFactory
{
    public static Item Get(string name, GameObjectDB objDB) 
    {
        var item = name switch 
        { 
            "spear" => new Item() { Name = name, Type = ItemType.Weapon, Stackable = false,
                Glyph = new Glyph(')', Colours.WHITE, Colours.GREY) },
            "dagger" => new Item() { Name = name, Type = ItemType.Weapon, Stackable = true,
                                        Glyph = new Glyph(')', Colours.WHITE, Colours.GREY) },
            "leather armour" => new Armour()
            {
                Name = name,
                Type = ItemType.Armour,
                Stackable = false,
                ArmourMod = 2,
                Piece = ArmourParts.Shirt,
                Glyph = new Glyph('[', Colours.BROWN, Colours.LIGHT_BROWN)
            },
            "helmet" => new Armour()
            {
                Name = name,
                Type = ItemType.Armour,
                Stackable = false,
                ArmourMod = 1,
                Piece = ArmourParts.Helmet,
                Glyph = new Glyph('[', Colours.WHITE, Colours.GREY)
            },
            "torch" => new Torch()
            {
                Name = name, Type = ItemType.Tool, Stackable = true, Fuel = 500, Lit = false
            },
            _ => throw new Exception($"{name} doesn't seem exist in yarl2 :(")
        };

        objDB.Add(item);

        return item;
    }
}

class Inventory(ulong ownerID)
{
    public ulong OwnerID { get; init; } = ownerID;
    public int Zorkmids { get; set; }
    Dictionary<char, Item> _items { get; set; } = [];
    public char NextSlot { get; set; } = 'a';

    void FindNextSlot() 
    {
        char start = NextSlot;

        while (true)
        {
            ++NextSlot;
            if (NextSlot == 123)
                NextSlot = 'a';
            
            if (!_items.TryGetValue(NextSlot, out Item? value) || value is null)
            {
                break;
            }
            if (NextSlot == start)
            {
                // there were no free slots
                NextSlot = '\0';
                break;
            }
        }
    }

    public char[] UsedSlots() => [.._items.Keys.Where(k => _items[k] != null).Order()];
    public Item ItemAt(char slot) => _items[slot];

    public void Add(Item item, ulong ownerID)
    {
        if (item.Type == ItemType.Zorkmid)
            Zorkmids += item.Count;
        
        // if the item is stackable, see if there's anything to stack it with
        if (item.Stackable)
        {
            foreach (var v in _items.Values.Where(v => v is not null))
            {
                if (v.Type == item.Type && v.Name == item.Name && v.Bonus == item.Bonus) 
                {
                    v.Count += item.Count;
                    return;
                }
            }
        }

        // if the item has a slot and it's available, put it there
        // otherwise but it in the next available slot, if there is one
        bool slotAvailable = !_items.ContainsKey(item.Slot) || _items[item.Slot] is null;
        if (item.Slot != '\0' && slotAvailable)
        {
            item.ContainedBy = ownerID;
            _items[item.Slot] = item;
        }
        else if (NextSlot != '\0')
        {
            item.Slot = NextSlot;
            item.ContainedBy = ownerID;
            _items[NextSlot] = item;
            FindNextSlot();
        }
        else
        {
            // no space could be found for it :(
        }
    }

    public void Remove(char slot, int count)
    {
        _items[slot] = null;
    }

    // This toggles the equip status of gear only and recalculation of stuff
    // like armour class has to be done elsewhere because it felt icky to 
    // have a reference back to the inventory's owner in the inventory object
    public (EquipingResult, ArmourParts) ToggleEquipStatus(char slot)
    {
        // I suppose at some point I'll have items that can't be equiped
        // (or like it doesn't make sense for them to be) and I'll have
        // to check for that
        if (_items.TryGetValue(slot, out Item item))
        {
            if (item.Equiped) 
            {
                // No cursed items or such yet to check for...
                item.Equiped = false;
                return (EquipingResult.Unequiped, ArmourParts.Shirt);
            }

            // Okay we are equiping new gear, which is a little more complicated
            if (item.Type == ItemType.Weapon)
            {
                // If there is a weapon already equiped, unequip it
                foreach (char c in UsedSlots())
                {
                    if (_items[c].Type == ItemType.Weapon && _items[c].Equiped)
                        _items[c].Equiped = false;
                }

                item.Equiped = true;
                return (EquipingResult.Equiped, ArmourParts.Shirt);
            }
            else if (item.Type == ItemType.Armour)
            {
                var armour = item as Armour;
                // check to see if there's another piece in that slot
                var b = _items.Values.Where(i => i.Type == ItemType.Armour && i.Equiped)
                                     .Any(a => ((Armour)a).Piece == armour.Piece);
                if (b)
                {
                    // alert about already wearing a piece
                    return (EquipingResult.Conflict, armour.Piece);
                }

                armour.Equiped = !armour.Equiped;
                return (EquipingResult.Equiped, ArmourParts.Shirt);
            }
        }

        return (EquipingResult.Conflict, ArmourParts.Shirt);
    }

    public List<(char, Item)> ToKVP() => _items.Select(kvp => (kvp.Key, kvp.Value))
                                               .Where(p => p.Item2 is not null)
                                               .ToList();
}

enum EquipingResult 
{
    Equiped,
    Unequiped,
    Conflict
}