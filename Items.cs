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
    char _nextSlot;

    public Inventory() => _nextSlot = 'a';

    void FindNextSlot() 
    {
        char start = _nextSlot;

        while (true)
        {
            ++_nextSlot;
            if (_nextSlot == 123)
                _nextSlot = 'a';
            //else if (_nextSlot == 91)
            //    _nextSlot = 'a';

            if (!_items.ContainsKey(_nextSlot) || _items[_nextSlot] is null)
            {
                break;
            }
            if (_nextSlot == start)
            {
                // there were no free slots
                _nextSlot = '\0';
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
        if (item.Slot != '\0' && _items[item.Slot] is null) 
        {
            _items[item.Slot] = item;
        }
        else if (_nextSlot != '\0')
        {
            item.Slot = _nextSlot;
            _items[_nextSlot] = item;
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
}