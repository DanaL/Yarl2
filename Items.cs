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

using System.Text;
using System.Xml.Linq;

namespace Yarl2;

enum ItemType
{
    Weapon,
    Armour,
    Zorkmid,
    Tool,
    Document,
    Potion,
    Scroll,
    Environment // I'm implementing things like mist as 'items'
}

class Item : GameObj
{
    public static readonly int DEFAULT_Z = 2;
    public ItemType Type { get; set; }
    public bool Stackable { get; set; }
    public char Slot { get; set; }
    public bool Equiped { get; set; } = false;
    public ulong ContainedBy { get; set; } = 0;
    public bool Consumable { get; set; } = false;
    public List<string> Adjectives { get; set; } = [];
    public List<Trait> Traits { get; set; } = [];
    public int Value { get; set; }
    int _z = DEFAULT_Z;

    public void SetZ(int z) => _z = z;
    public override int Z()
    {
        return _z;
    }

    string CalcFullName()
    {
        string name = Name;

        if (Adjectives.Count == 1)
            name = $"{Adjectives[0]} {Name}";
        else if (Adjectives.Count > 1)
            name = $"{string.Join(", ", Adjectives)} {Name}";

        string traitDescs = string.Join(' ', Traits.Select(t => t.Desc()));
        if (traitDescs.Length > 0)
            name = name + " " + traitDescs;

        return name.Trim();
    }

    public override string FullName => CalcFullName();

    public override List<(ulong, int, TerrainFlag)> Auras(GameState gs)
    {        
        return Traits.Where(t => t.Aura)
                     .Select(t => (ID, t.Radius, t.Effect))
                     .ToList();
    }
    
    // Active in the sense of being an IPerformer who needs to be in the 
    // turn order.
    public List<IPerformer> ActiveTraits()
    {
        return Traits.Where(i => i is IPerformer p && i.Active)
                     .Select(t => (IPerformer) t).ToList();
    }

    public string ApplyEffect(TerrainFlag flag, GameState gs, Loc loc)
    {
        var sb = new StringBuilder();
        var uts = Traits.OfType<IUSeable>().ToList();
        foreach (var t in uts) 
        { 
            sb.Append(t.ApplyEffect(flag, gs, this, loc));
        }

        return sb.ToString();
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
                item = new Item() { Name = name, Type = ItemType.Weapon, Stackable = false, Value = 10,
                                        Glyph = new Glyph(')', Colours.WHITE, Colours.GREY) };
                item.Traits.Add(new AttackTrait() { Bonus = 0 });
                item.Traits.Add(new DamageTrait() { DamageDie = 6, NumOfDie = 1, DamageType = DamageType.Piercing });
                break;
            case "dagger":
                item = new Item() { Name = name, Type = ItemType.Weapon, Stackable = true, Value = 10,
                                        Glyph = new Glyph(')', Colours.WHITE, Colours.GREY) };
                item.Traits.Add(new AttackTrait() { Bonus = 0 });
                item.Traits.Add(new DamageTrait() { DamageDie = 4, NumOfDie = 1, DamageType = DamageType.Piercing });
                break;
            case "hand axe":
                item = new Item() { Name = name, Type = ItemType.Weapon, Stackable = false, Value = 15,
                                        Glyph = new Glyph(')', Colours.LIGHT_BROWN, Colours.BROWN) };
                item.Traits.Add(new AttackTrait() { Bonus = 0 });
                item.Traits.Add(new DamageTrait() { DamageDie = 6, NumOfDie = 1, DamageType = DamageType.Slashing });                
                break;
            case "battle axe":
                item = new Item() { Name = name, Type = ItemType.Weapon, Stackable = false, Value = 25,
                                        Glyph = new Glyph(')', Colours.LIGHT_BROWN, Colours.BROWN) };
                item.Traits.Add(new AttackTrait() { Bonus = 0 });
                item.Traits.Add(new DamageTrait() { DamageDie = 4, NumOfDie = 2, DamageType = DamageType.Slashing });                
                break;
            case "mace":
                item = new Item() { Name = name, Type = ItemType.Weapon, Stackable = false, Value = 25,
                                        Glyph = new Glyph(')', Colours.LIGHT_GREY, Colours.GREY) };
                item.Traits.Add(new AttackTrait() { Bonus = 0 });
                item.Traits.Add(new DamageTrait() { DamageDie = 4, NumOfDie = 2, DamageType = DamageType.Blunt });                
                break;
            case "longsword":
                item = new Item() { Name = name, Type = ItemType.Weapon, Stackable = false, Value = 25,
                                        Glyph = new Glyph(')', Colours.WHITE, Colours.LIGHT_GREY) };
                item.Traits.Add(new AttackTrait() { Bonus = 0 });
                item.Traits.Add(new DamageTrait() { DamageDie = 8, NumOfDie = 1, DamageType = DamageType.Slashing });                
                break;
            case "rapier":
                item = new Item() { Name = name, Type = ItemType.Weapon, Stackable = false, Value = 20,
                                        Glyph = new Glyph(')', Colours.WHITE, Colours.LIGHT_GREY) };
                item.Traits.Add(new AttackTrait() { Bonus = 0 });
                item.Traits.Add(new DamageTrait() { DamageDie = 8, NumOfDie = 1, DamageType = DamageType.Piercing });                
                break;
            case "arrow":
                item = new Item() { Name = name, Type = ItemType.Weapon, Stackable = true, Value = 2,
                    Glyph = new Glyph('-', Colours.LIGHT_BROWN, Colours.BROWN) };
                item.Traits.Add(new AttackTrait() { Bonus = 0 });
                item.Traits.Add(new DamageTrait() { DamageDie = 6, NumOfDie = 1, DamageType = DamageType.Piercing });
                break;
            case "firebolt":
                item = new Item() { Name = name, Type = ItemType.Weapon, Stackable = false, Value = 0,
                    Glyph = new Glyph('-', Colours.YELLOW, Colours.YELLOW_ORANGE) };
                item.Traits.Add(new AttackTrait() { Bonus = 0 });
                item.Traits.Add(new DamageTrait() { DamageDie = 8, NumOfDie = 1, DamageType = DamageType.Fire });
                break;
            case "leather armour":
                item = new Item() { Name = name, Type = ItemType.Armour, Stackable = false, Value = 20,
                                    Glyph = new Glyph('[', Colours.BROWN, Colours.LIGHT_BROWN) };
                item.Traits.Add(new ArmourTrait() { Part = ArmourParts.Shirt, ArmourMod = 1, Bonus = 0 });
                break;
            case "studded leather armour":
                item = new Item() { Name = name, Type = ItemType.Armour, Stackable = false, Value = 25,
                                    Glyph = new Glyph('[', Colours.BROWN, Colours.LIGHT_BROWN) };
                item.Traits.Add(new ArmourTrait() { Part = ArmourParts.Shirt, ArmourMod = 2, Bonus = 0 });
                break;
            case "ringmail":
                item = new Item() { Name = name, Type = ItemType.Armour, Stackable = false, Value = 45,
                                    Glyph = new Glyph('[', Colours.LIGHT_GREY, Colours.GREY) };
                item.Traits.Add(new ArmourTrait() { Part = ArmourParts.Shirt, ArmourMod = 3, Bonus = 0 });
                break;
            case "chainmail":
                item = new Item() { Name = name, Type = ItemType.Armour, Stackable = false, Value = 75,
                                    Glyph = new Glyph('[', Colours.LIGHT_GREY, Colours.GREY) };
                item.Traits.Add(new ArmourTrait() { Part = ArmourParts.Shirt, ArmourMod = 4, Bonus = 0 });
                break;
            case "helmet":
                item = new Item() { Name = name, Type = ItemType.Armour, Stackable = false, Value = 20,
                                    Glyph = new Glyph('[', Colours.WHITE, Colours.GREY) };
                item.Traits.Add(new ArmourTrait() { Part = ArmourParts.Hat, ArmourMod = 1, Bonus = 0 });
                break;
            case "torch":
                item = new Item() { Name = name, Type = ItemType.Tool, Stackable = true, Value = 2,
                                    Glyph = new Glyph('(', Colours.LIGHT_BROWN, Colours.BROWN) };
                var ls = new FlameLightSourceTrait() {
                    ContainerID = item.ID, Fuel = 500, Lit = false };
                ls.Stats[Attribute.Radius] = new Stat(5);
                item.Traits.Add(ls);
                break;
            case "zorkmids":
                item = new Item() { Name = "zorkmid", Type = ItemType.Zorkmid, Stackable = true,
                                    Glyph = new Glyph('$', Colours.YELLOW, Colours.YELLOW_ORANGE) };
                break;
            case "potion of healing":
                item = new Item() { Name = "potion of healing", Type = ItemType.Potion, Stackable = true, Value = 75,
                                    Glyph = new Glyph('!', Colours.LIGHT_BLUE, Colours.BLUE),
                                    Consumable = true };
                item.Traits.Add(new CastMinorHealTrait());
                break;
            case "antidote":
                item = new Item()
                {
                    Name = "antidote", Type = ItemType.Potion, Stackable = true, Value = 50,
                    Glyph = new Glyph('!', Colours.YELLOW, Colours.YELLOW_ORANGE), Consumable = true
                };
                item.Traits.Add(new CastAntidoteTrait());
                break;
            case "scroll of blink":
                item = new Item()
                {
                    Name = "scroll of blink", Type = ItemType.Scroll, Stackable = true, Value = 125,
                                    Glyph = new Glyph('?', Colours.WHITE, Colours.GREY), Consumable = true };
                item.Traits.Add(new CastBlinkTrait());
                break;
            default:
                throw new Exception($"{name} doesn't seem exist in yarl2 :(");
        }

        objDB.Add(item);

        return item;
    }

    public static Item Mist(GameState gs)
    {
        var mist = new Item()
        {
            Name = "mist",
            Type = ItemType.Environment,
            Stackable = false,
            Value = 0,
            Glyph = new Glyph('*', Colours.LIGHT_GREY, Colours.GREY)
        };
        mist.SetZ(10);
        mist.Traits.Add(new OpaqueTrait());
        mist.Traits.Add(new CountdownTrait()
        {
            ContainerID = mist.ID,
            ExpiresOn = gs.Turn + 7
        });

        return mist;
    }

    public static Item Web()
    {
        var web = new Item()
        {
            Name = "webs",
            Type = ItemType.Environment,
            Stackable = false,
            Value = 0,
            Glyph = new Glyph(':', Colours.WHITE, Colours.GREY)
        };
        web.Traits.Add(new StickyTrait());

        return web;
    }
}

enum ArmourParts
{
    None,
    Hat,
    Boots,
    Cloak,
    Shirt
}

class Armour : Item
{
    public ArmourParts Piece { get; set; }
}

class Inventory(ulong ownerID)
{
    public ulong OwnerID { get; init; } = ownerID;
    public int Zorkmids { get; set; }
    List<Item> _items = [];
    public char NextSlot { get; set; } = 'a';

    void FindNextSlot() 
    {
        char start = NextSlot;
        var slots = UsedSlots().ToHashSet();

        while (true)
        {
            ++NextSlot;
            if (NextSlot == 123)
                NextSlot = 'a';

            if (!slots.Contains(NextSlot))
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

    public char[] UsedSlots() => _items.Select(i => i.Slot).Distinct().ToArray();

    public (Item, int) ItemAt(char slot)
    {
        var inSlot = _items.Where(i => i.Slot == slot);

        return (inSlot.First(), inSlot.Count());
    }

    public Item? ReadiedWeapon()
    {        
        foreach (var item in _items)
        {
            if ((item.Type == ItemType.Weapon || item.Type == ItemType.Tool) && item.Equiped)
                return item;
        }

        return null;
    }

    public void Add(Item item, ulong ownerID)
    {
        if (item.Type == ItemType.Zorkmid) 
        {
            Zorkmids += item.Value;
            return;
        }

        // Find the slot for the item
        var usedSlots = UsedSlots().ToHashSet();
        char slotToUse = '\0';

        // If the item is stackable and there are others of the same item, use
        // that slot. Otherwise, if the item has a previously assigned slot and
        // it's still available, use that slot. Finally, look for the next
        // available slot
        if (item.Stackable)
        {
            foreach (var other in _items)
            {
                // Not yet worrying about +1 dagger vs +2 dagger which probably shouldn't stack together
                // Maybe a CanStack() method on Item
                if (other.Type == item.Type && other.Name == item.Name)
                {
                    slotToUse = other.Slot;
                    break;
                }
            }
        }
        else if (item.Slot != '\0' && !usedSlots.Contains(item.Slot))
        {
            slotToUse = item.Slot;
        }
        
        
        if (slotToUse == '\0')
        {
            slotToUse = NextSlot;
            FindNextSlot();
        }

        if (slotToUse != '\0')
        {
            item.Slot = slotToUse;
            item.ContainedBy = ownerID;
            _items.Add(item);
        }
        else
        {
            // There was no free slot, which I am not currently handling...
        }        
    }

    public Item? RemoveByID(ulong id) 
    {
        Item? item = null;

        for (int j = 0; j < _items.Count; j++)
        {
            if (_items[j].ID == id)
            {
                item = _items[j];
                _items.RemoveAt(j);
                break;
            }
        }

        return item;
    }

    public List<Item> Remove(char slot, int count)
    {
        List<int> indexes = [];
        for (int j = _items.Count - 1; j >= 0; j--)
        {
            if (_items[j].Slot == slot)
                indexes.Add(j);
        }

        List<Item> removed = [];
        int totalToRemove = int.Min(count, indexes.Count);
        for (int j = 0; j < totalToRemove; j++)
        {
            int index = indexes[j];
            removed.Add(_items[index]);
            _items.RemoveAt(index);
        }

        return removed;
    }

    // This toggles the equip status of gear only and recalculation of stuff
    // like armour class has to be done elsewhere because it felt icky to 
    // have a reference back to the inventory's owner in the inventory object
    public (EquipingResult, ArmourParts) ToggleEquipStatus(char slot)
    {
        // I suppose at some point I'll have items that can't be equiped
        // (or like it doesn't make sense for them to be) and I'll have
        // to check for that
        Item? item = null;
        foreach (var i in  _items)
        {
            if (i.Slot == slot)
            {
                item = i;
                break;
            }
        }

        if (item is not null)
        {
            if (item.Equiped) 
            {
                // No cursed items or such yet to check for...
                item.Equiped = false;
                return (EquipingResult.Unequiped, ArmourParts.Shirt);
            }

            // Okay we are equiping new gear, which is a little more complicated
            if (item.Type == ItemType.Weapon || item.Type == ItemType.Tool)
            {
                // If there is a weapon already equiped, unequip it
                foreach (Item other in _items)
                {
                    if (other.Type == ItemType.Weapon && other.Equiped)
                        other.Equiped = false;
                }
 
                item.Equiped = true;
                return (EquipingResult.Equiped, ArmourParts.Shirt);
            }
            else if (item.Type == ItemType.Armour)
            {
                ArmourParts part = ArmourParts.None;
                foreach (var t in item.Traits)
                {
                    if (t is ArmourTrait at)
                    {
                        part = at.Part;
                    }
                }

                // check to see if there's another piece in that slot
                foreach (var other  in _items.Where(a => a.Type == ItemType.Armour && a.Equiped))
                {
                    foreach (var t in other.Traits)
                    {
                        if (t is ArmourTrait at && at.Part == part)
                            return (EquipingResult.Conflict, part);
                    }                    
                }
                
                item.Equiped = !item.Equiped;

                return (EquipingResult.Equiped, ArmourParts.Shirt);
            }
        }

        return (EquipingResult.Conflict, ArmourParts.Shirt);
    }

    // Active as in has a trait that needs to be in the turn order
    public List<IPerformer> ActiveItemTraits()
    {
        List<IPerformer> activeTraits = [];

        foreach (var item in _items)
        {
            activeTraits.AddRange(item.ActiveTraits());
        }
        
        return activeTraits;
    }

    //public List<(char, Item)> ToKVP() => _items.Select(i => (i.Slot, i.Value))
    //                                           .Where(p => p.Item2 is not null)
    //                                           .ToList();
}

enum EquipingResult 
{
    Equiped,
    Unequiped,
    Conflict
}