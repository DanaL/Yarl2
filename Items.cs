using System.Diagnostics.Tracing;

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

class ItemFactory
{
    public static Item Get(string name) => name switch
    {
        "spear" => new Item() { Name = name, Type = ItemType.Weapon, Stackable = false },
        "leather armour" => new Item()
        {
            Name = name,
            Type = ItemType.Armour,
            Stackable = false,
            ArmourMod = 2,            
        },
        _ => throw new Exception($"{name} doesn't seem exist in yarl2 :()"),
    };
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

    public List<(char, Item)> ToKVP() => _items.Select(kvp => (kvp.Key, kvp.Value)).ToList();
}