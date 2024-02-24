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
    Tool,
    Document
}

class Item : GameObj
{
    public ItemType Type { get; set; }
    public bool Stackable { get; set; }
    public char Slot { get; set; }
    public bool Equiped { get; set; } = false;
    public int Count { get; set; } = 1;
    public ulong ContainedBy { get; set; } = 0;

    public List<string> Adjectives { get; set; } = [];
    public List<ItemTrait> Traits { get; set; } = [];

    private string CalcFullName()
    {
        string name = Name;

        if (Adjectives.Count == 1)
            name = $"{Adjectives[0]} {Name}";
        else if (Adjectives.Count > 1)
            name = $"{string.Join(", ", Adjectives)} {Name}";

        string traitDescs = string.Join(' ', Traits.Select(t => t.Desc()));
        if (traitDescs.Length > 0)
            name = name + " " + traitDescs;

        return name;
    }

    public override string FullName => CalcFullName();

    public override List<(ulong, int)> EffectSources(TerrainFlags flags, GameState gs)
    {
        List<(ulong, int)> sources = [];

        if (flags == TerrainFlags.Lit)
        {
            foreach (var t in Traits)
            {
                if (t is LightSourceTrait light && light.Lit)
                    sources.Add((ID, light.Radius));                
            }
        }
        
        return sources;
    }

    public Item Duplicate(GameState gs)
    {
        var item = (Item)MemberwiseClone();
        item.ID = NextID;
        item.Traits = [];
        item.Adjectives = Adjectives.Select(s => s).ToList();

        // In the case of LightSourceTraits, we need to create a duplicate of
        // the trait and set it to point to the containing object. I'll need to
        // come up with something cleaner when I have more traits that have
        // changing fields (a la a Torch's fuel)
        foreach (var trait in Traits)
            item.Traits.Add(trait.Duplicate(item));
        
        gs.ObjDB.Add(item);

        return item;
    }

    // Active in the sense of being an IPerformer who needs to be in the 
    // turn order.
    public List<IPerformer> ActiveTraits()
    {
        return Traits.Where(i => i is IPerformer p && i.Acitve)
                     .Select(t => (IPerformer) t).ToList();
    }
}

class ItemFactory
{
    public static Item Get(string name, GameObjectDB objDB) 
    {
        Item item;

        switch (name)
        {
            case "spear":
                item = new Item() { Name = name, Type = ItemType.Weapon, Stackable = false, 
                                        Glyph = new Glyph(')', Colours.WHITE, Colours.GREY) };
                item.Traits.Add(new MeleeAttackTrait() { DamageDie = 6, NumOfDie = 1, Bonus = 0 });
                break;
            case "dagger":
                item = new Item() { Name = name, Type = ItemType.Weapon, Stackable = true,
                                        Glyph = new Glyph(')', Colours.WHITE, Colours.GREY) };
                item.Traits.Add(new MeleeAttackTrait() { DamageDie = 4, NumOfDie = 1, Bonus = 0 });
                break;
            case "leather armour":
                item = new Item() { Name = name, Type = ItemType.Armour, Stackable = false,
                                    Glyph = new Glyph('[', Colours.BROWN, Colours.LIGHT_BROWN) };
                item.Traits.Add(new ArmourTrait() { Part = ArmourParts.Shirt, ArmourMod = 1, Bonus = 0 });
                break;
            case "helmet":
                item = new Item() { Name = name, Type = ItemType.Armour, Stackable = false,
                                    Glyph = new Glyph('[', Colours.WHITE, Colours.GREY) };
                item.Traits.Add(new ArmourTrait() { Part = ArmourParts.Shirt, ArmourMod = 1, Bonus = 0 });
                break;
            case "torch":
                item = new Item() { Name = name, Type = ItemType.Tool, Stackable = true,
                                    Glyph = new Glyph('(', Colours.LIGHT_BROWN, Colours.BROWN) };
                item.Traits.Add(new LightSourceTrait() { ContainerID = item.ID, Fuel=500, Radius=5, Lit=false, 
                                         Energy=0.0, Recovery=1.0});
                break;
            default:
                throw new Exception($"{name} doesn't seem exist in yarl2 :(");
        }

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
                // I probably need to make a CanStack item method to handle cases where
                // we're trying to stack a dagger +1 and a dagger +2
                if (v.Type == item.Type && v.Name == item.Name) // && v.Bonus == item.Bonus) 
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

    // Active as in has a trait that needs to be in the turn order
    public List<IPerformer> ActiveItemTraits()
    {
        List<IPerformer> activeTraits = [];

        foreach (var item in _items.Values)
        {
            activeTraits.AddRange(item.ActiveTraits());
        }
        
        return activeTraits;
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