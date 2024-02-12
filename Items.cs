using System.Xml;

namespace Yarl2;

enum ItemType
{
    Weapon,
    Armour,
    Zorkmid
}

class Item
{
    public string Name { get; set; } = "";
    public ItemType Type { get; set; }
    public int ArmourMod { get; set; }
    public int Weight { get; set;}
    public bool Stackable { get; set; }
    public char Slot { get; set; }
    public bool Equiped { get; set; } = false;
    public int Count { get; set; } = 1;
    public Glyph Glyph { get; set; }

    public List<string> Adjectives { get; set; } = [];

    public string FullName {
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

class ItemFactory
{
    public static Item Get(string name) => name switch
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
        _ => throw new Exception($"{name} doesn't seem exist in yarl2 :("),
    };
}

// Structure to store where items are in the world
class ItemDB
{
    public Dictionary<Loc, List<Item>> _items;

    public ItemDB() => _items = [];

    public void Add(Loc loc, Item item)
    {
        if (!_items.TryGetValue(loc, out var stack))
        {
            stack = [];
            _items.Add(loc, stack);
        }

        // I could have made _items Stack<Item> instead of a list, but there
        // are times when I want to iterate over the items in a location,
        // and sometimes the player will want to remove an item from the middle.
        stack.Insert(0, item);
    }

    // This is really just used for restoring an itemdb from serialization
    public void AddStack(Loc loc, List<Item> stack)
    {
       _items[loc] = stack;
    }

    // It's probably dangerous/bad practice to return the list of items
    // so other parts of the game can manipulate it directly, but hey
    // it's easy and convenient
    public List<Item> ItemsAt(Loc loc)
    {
        if (!_items.TryGetValue(loc, out var stack))
            return [];
        else
            return stack;
    }
    
    public List<(Loc, List<Item>)> Dump() => _items.Keys.Select(k => (k, _items[k])).ToList();
}

class Inventory
{
    public int Zorkmids { get; set; }
    Dictionary<char, Item> _items { get; set; } = [];
    public char NextSlot { get; set; }

    public Inventory() => NextSlot = 'a';

    void FindNextSlot() 
    {
        char start = NextSlot;

        while (true)
        {
            ++NextSlot;
            if (NextSlot == 123)
                NextSlot = 'a';
            //else if (_nextSlot == 91)
            //    _nextSlot = 'a';

            if (!_items.ContainsKey(NextSlot) || _items[NextSlot] is null)
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

    public void Add(Item item)
    {
        if (item.Type == ItemType.Zorkmid)
            Zorkmids += item.Count;
        
        // if the item has a slot and it's available, put it there
        // otherwise but it in the next available slot, if there is one
        bool slotAvailable = !_items.ContainsKey(item.Slot) || _items[item.Slot] is null;
        if (item.Slot != '\0' && slotAvailable)
        {
            _items[item.Slot] = item;
        }
        else if (NextSlot != '\0')
        {
            item.Slot = NextSlot;
            _items[NextSlot] = item;
            FindNextSlot();
        }
        else
        {
            // no space could be found for it :(
        }

        // TODO: handle stacks
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