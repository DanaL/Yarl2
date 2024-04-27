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
  Trinket,
  Environment // I'm implementing things like mist as 'items'
}

class Item : GameObj, IEquatable<Item>
{
  public static readonly int DEFAULT_Z = 2;
  public ItemType Type { get; set; }
  public char Slot { get; set; }
  public bool Equiped { get; set; } = false;
  public ulong ContainedBy { get; set; } = 0;
  public List<string> Adjectives { get; set; } = [];
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

    string traitDescs = string.Join(' ', Traits.OfType<BasicTrait>().Select(t => t.Desc()));
    if (traitDescs.Length > 0)
      name = name + " " + traitDescs;

    return name.Trim();
  }

  public override string FullName => CalcFullName();

  public override List<(ulong, int, TerrainFlag)> Auras(GameState gs)
  {
    return Traits.OfType<BasicTrait>()
                  .Where(t => t.Aura)
                 .Select(t => (ID, t.Radius, t.Effect))
                 .ToList();
  }

  // Active in the sense of being an IPerformer who needs to be in the 
  // turn order.
  public List<IPerformer> ActiveTraits()
  {
    return Traits.Where(i => i is IPerformer p && i.Active)
                 .Select(t => (IPerformer)t).ToList();
  }

  public string ApplyEffect(TerrainFlag flag, GameState gs, Loc loc)
  {
    var sb = new StringBuilder();
    var uts = Traits.OfType<IEffectApplier>().ToList();
    foreach (var t in uts)
    {
      sb.Append(t.ApplyEffect(flag, gs, this, loc));
    }

    return sb.ToString();
  }

  public override int GetHashCode() => $"{Type}+{Name}+{HasTrait<StackableTrait>()}".GetHashCode();
  public override bool Equals(object? obj) => Equals(obj as Item);
  
  public bool Equals(Item? i)
  {
    if (i is null)
      return false;

    if (ReferenceEquals(this, i)) 
      return true;

    if (GetType() != i.GetType()) 
      return false;

    if (HasTrait<StackableTrait>() != i.HasTrait<StackableTrait>()) 
      return false;

    return i.Name == Name && i.Type == Type;
  }

  public static bool operator==(Item? a, Item? b)
  {
    if (a is null)
    {
      if (b is null)
        return true;
      return false;
    }
    
    return a.Equals(b);
  }

  public static bool operator!=(Item? a, Item? b) => !(a == b);
}

class ItemFactory
{
  public static Item Get(string name, GameObjectDB objDB)
  {
    Item item;

    switch (name)
    {
      case "spear":
        item = new Item()
        {
          Name = name,
          Type = ItemType.Weapon,
          Value = 10,
          Glyph = new Glyph(')', Colours.WHITE, Colours.GREY)
        };
        item.Traits.Add(new AttackTrait() { Bonus = 0 });
        item.Traits.Add(new DamageTrait() { DamageDie = 6, NumOfDie = 1, DamageType = DamageType.Piercing });
        break;
      case "dagger":
        item = new Item()
        {
          Name = name,
          Type = ItemType.Weapon,
          Value = 10,
          Glyph = new Glyph(')', Colours.WHITE, Colours.GREY)
        };
        item.Traits.Add(new AttackTrait() { Bonus = 0 });
        item.Traits.Add(new DamageTrait() { DamageDie = 4, NumOfDie = 1, DamageType = DamageType.Piercing });
        item.Traits.Add(new StackableTrait());
        break;
      case "hand axe":
        item = new Item()
        {
          Name = name,
          Type = ItemType.Weapon,
          Value = 15,
          Glyph = new Glyph(')', Colours.LIGHT_BROWN, Colours.BROWN)
        };
        item.Traits.Add(new AttackTrait() { Bonus = 0 });
        item.Traits.Add(new DamageTrait() { DamageDie = 6, NumOfDie = 1, DamageType = DamageType.Slashing });
        break;
      case "battle axe":
        item = new Item()
        {
          Name = name,
          Type = ItemType.Weapon,
          Value = 25,
          Glyph = new Glyph(')', Colours.LIGHT_BROWN, Colours.BROWN)
        };
        item.Traits.Add(new AttackTrait() { Bonus = 0 });
        item.Traits.Add(new DamageTrait() { DamageDie = 4, NumOfDie = 2, DamageType = DamageType.Slashing });
        break;
      case "mace":
        item = new Item()
        {
          Name = name,
          Type = ItemType.Weapon,
          Value = 25,
          Glyph = new Glyph(')', Colours.LIGHT_GREY, Colours.GREY)
        };
        item.Traits.Add(new AttackTrait() { Bonus = 0 });
        item.Traits.Add(new DamageTrait() { DamageDie = 4, NumOfDie = 2, DamageType = DamageType.Blunt });
        break;
      case "longsword":
        item = new Item()
        {
          Name = name,
          Type = ItemType.Weapon,
          Value = 25,
          Glyph = new Glyph(')', Colours.WHITE, Colours.LIGHT_GREY)
        };
        item.Traits.Add(new AttackTrait() { Bonus = 0 });
        item.Traits.Add(new DamageTrait() { DamageDie = 8, NumOfDie = 1, DamageType = DamageType.Slashing });
        break;
      case "rapier":
        item = new Item()
        {
          Name = name,
          Type = ItemType.Weapon,
          Value = 20,
          Glyph = new Glyph(')', Colours.WHITE, Colours.LIGHT_GREY)
        };
        item.Traits.Add(new AttackTrait() { Bonus = 0 });
        item.Traits.Add(new DamageTrait() { DamageDie = 8, NumOfDie = 1, DamageType = DamageType.Piercing });
        break;
      case "arrow":
        item = new Item()
        {
          Name = name,
          Type = ItemType.Weapon,
          Value = 2,
          Glyph = new Glyph('-', Colours.LIGHT_BROWN, Colours.BROWN)
        };
        item.Traits.Add(new AttackTrait() { Bonus = 0 });
        item.Traits.Add(new DamageTrait() { DamageDie = 6, NumOfDie = 1, DamageType = DamageType.Piercing });
        item.Traits.Add(new StackableTrait());
        break;
      case "firebolt":
        item = new Item()
        {
          Name = name,
          Type = ItemType.Weapon,
          Value = 0,
          Glyph = new Glyph('-', Colours.YELLOW, Colours.YELLOW_ORANGE)
        };
        item.Traits.Add(new AttackTrait() { Bonus = 0 });
        item.Traits.Add(new DamageTrait() { DamageDie = 5, NumOfDie = 2, DamageType = DamageType.Fire });
        break;
      case "leather armour":
        item = new Item()
        {
          Name = name,
          Type = ItemType.Armour,
          Value = 20,
          Glyph = new Glyph('[', Colours.BROWN, Colours.LIGHT_BROWN)
        };
        item.Traits.Add(new ArmourTrait() { Part = ArmourParts.Shirt, ArmourMod = 1, Bonus = 0 });
        break;
      case "studded leather armour":
        item = new Item()
        {
          Name = name,
          Type = ItemType.Armour,
          Value = 25,
          Glyph = new Glyph('[', Colours.BROWN, Colours.LIGHT_BROWN)
        };
        item.Traits.Add(new ArmourTrait() { Part = ArmourParts.Shirt, ArmourMod = 2, Bonus = 0 });
        break;
      case "ringmail":
        item = new Item()
        {
          Name = name,
          Type = ItemType.Armour,
          Value = 45,
          Glyph = new Glyph('[', Colours.LIGHT_GREY, Colours.GREY)
        };
        item.Traits.Add(new ArmourTrait() { Part = ArmourParts.Shirt, ArmourMod = 3, Bonus = 0 });
        break;
      case "chainmail":
        item = new Item()
        {
          Name = name,
          Type = ItemType.Armour,
          Value = 75,
          Glyph = new Glyph('[', Colours.LIGHT_GREY, Colours.GREY)
        };
        item.Traits.Add(new ArmourTrait() { Part = ArmourParts.Shirt, ArmourMod = 4, Bonus = 0 });
        break;
      case "helmet":
        item = new Item()
        {
          Name = name,
          Type = ItemType.Armour,
          Value = 20,
          Glyph = new Glyph('[', Colours.WHITE, Colours.GREY)
        };
        item.Traits.Add(new ArmourTrait() { Part = ArmourParts.Hat, ArmourMod = 1, Bonus = 0 });
        break;
      case "torch":
        item = new Item()
        {
          Name = name,
          Type = ItemType.Tool,
          Value = 2,
          Glyph = new Glyph('(', Colours.LIGHT_BROWN, Colours.BROWN)
        };
        var ls = new TorchTrait()
        {
          OwnerID = item.ID,
          Fuel = 1500,
          Lit = false
        };
        item.Traits.Add(ls);
        item.Traits.Add(new FlammableTrait());
        item.Traits.Add(new StackableTrait());
        break;
      case "zorkmids":
        item = new Item()
        {
          Name = "zorkmid",
          Type = ItemType.Zorkmid,
          Glyph = new Glyph('$', Colours.YELLOW, Colours.YELLOW_ORANGE)
        };
        item.Traits.Add(new StackableTrait());
        break;        
      case "potion of healing":
        item = new Item()
        {
          Name = "potion of healing",
          Type = ItemType.Potion,
          Value = 75,
          Glyph = new Glyph('!', Colours.LIGHT_BLUE, Colours.BLUE)        
        };
        item.Traits.Add(new UseSimpleTrait("minorheal"));
        item.Traits.Add(new ConsumableTrait());
        item.Traits.Add(new StackableTrait());
        break;
      case "potion of mind reading":
        item = new Item()
        {
          Name = "potion of mind reading",
          Type = ItemType.Potion,
          Value = 100,
          Glyph = new Glyph('!', Colours.LIGHT_BLUE, Colours.BLUE)
        };
        item.Traits.Add(new UseSimpleTrait("telepathy"));
        item.Traits.Add(new ConsumableTrait());
        item.Traits.Add(new StackableTrait());
        break;
      case "antidote":
        item = new Item()
        {
          Name = "antidote",
          Type = ItemType.Potion,
          Value = 50,
          Glyph = new Glyph('!', Colours.YELLOW, Colours.YELLOW_ORANGE)
        };
        item.Traits.Add(new UseSimpleTrait("antidote"));
        item.Traits.Add(new ConsumableTrait());
        item.Traits.Add(new StackableTrait());
        break;
      case "scroll of blink":
        item = new Item()
        {
          Name = "scroll of blink",
          Type = ItemType.Scroll,
          Value = 125,
          Glyph = new Glyph('?', Colours.WHITE, Colours.GREY)
        };
        item.Traits.Add(new ConsumableTrait());
        item.Traits.Add(new UseSimpleTrait("blink"));
        item.Traits.Add(new FlammableTrait());
        item.Traits.Add(new WrittenTrait());
        item.Traits.Add(new StackableTrait());
        break;
      case "scroll of magic mapping":
        item = new Item()
        {
          Name = "scroll of magic mapping",
          Type = ItemType.Scroll,
          Value = 100,
          Glyph = new Glyph('?', Colours.WHITE, Colours.GREY)
        };
        item.Traits.Add(new ConsumableTrait());
        item.Traits.Add(new UseSimpleTrait("magicmap"));
        item.Traits.Add(new FlammableTrait());
        item.Traits.Add(new WrittenTrait());
        item.Traits.Add(new StackableTrait());
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
      Value = 0,
      Glyph = new Glyph('*', Colours.LIGHT_GREY, Colours.GREY)
    };
    mist.SetZ(10);
    mist.Traits.Add(new OpaqueTrait());
    mist.Traits.Add(new CountdownTrait()
    {
      OwnerID = mist.ID,
      ExpiresOn = gs.Turn + 7
    });

    return mist;
  }

  public static Item Fire(GameState gs)
  {
    Glyph glyph;
    var roll = gs.Rng.NextDouble();
    if (roll < 0.333)
      glyph = new Glyph('\u22CF', Colours.BRIGHT_RED, Colours.DULL_RED);
    else if (roll < 0.666)
      glyph = new Glyph('\u22CF', Colours.YELLOW, Colours.DULL_RED);
    else
      glyph = new Glyph('\u22CF', Colours.YELLOW_ORANGE, Colours.DULL_RED);

    var fire = new Item()
    {
      Name = "fire",
      Type = ItemType.Environment,
      Value = 0,
      Glyph = glyph
    };
    fire.SetZ(7);
    gs.ObjDb.Add(fire);
    var onFire = new OnFireTrait() { Expired = false, OwnerID = fire.ID };
    gs.RegisterForEvent(GameEventType.EndOfRound, onFire);
    fire.Traits.Add(onFire);
    fire.Traits.Add(new LightSourceTrait() { Radius = 1, OwnerID = fire.ID });
    return fire;
  }

  public static Item Web()
  {
    var web = new Item()
    {
      Name = "webs",
      Type = ItemType.Environment,
      Value = 0,
      Glyph = new Glyph(':', Colours.WHITE, Colours.GREY)
    };
    web.Traits.Add(new StickyTrait());
    web.Traits.Add(new FlammableTrait());
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

class Inventory(ulong ownerID, GameObjectDB objDb)
{
  public ulong OwnerID { get; init; } = ownerID;
  public int Zorkmids { get; set; }
  List<(char, ulong)> _items = [];
  public char NextSlot { get; set; } = 'a';
  readonly GameObjectDB _objDb = objDb;

  public bool Contains(ulong itemID)
  {
    foreach (var  item in _items)
    {
      if (item.Item2 == itemID)
        return true;
    }

    return false;
  }

  protected virtual List<Item> Items()
  {
    List<Item> items = [];
    foreach (var itemID in _items.Select(i => i.Item2))
    {
      var item = _objDb.GetObj(itemID) as Item;
      if (item is not null)
        items.Add(item);
    }

    return items;
  }

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

  public char[] UsedSlots() => _items.Select(i => i.Item1).Distinct().ToArray();

  public (Item?, int) ItemAt(char slot)
  {
    var inSlot = _items.Where(i => i.Item1 == slot).ToList();
    var item = _objDb.GetObj(inSlot.First().Item2) as Item;

    return (item, inSlot.Count);
  }

  public Item? ReadiedWeapon()
  {
    foreach (var item in Items())
    {
      if ((item.Type == ItemType.Weapon || item.Type == ItemType.Tool) && item.Equiped)
        return item;
    }

    return null;
  }

  public char Add(Item item, ulong ownerID)
  {
    if (item.Type == ItemType.Zorkmid)
    {
      Zorkmids += item.Value;
      return '\0';
    }

    // Find the slot for the item
    var usedSlots = UsedSlots().ToHashSet();
    char slotToUse = '\0';

    // If the item is stackable and there are others of the same item, use
    // that slot. Otherwise, if the item has a previously assigned slot and
    // it's still available, use that slot. Finally, look for the next
    // available slot
    if (item.HasTrait<StackableTrait>())
    {
      foreach (var other in Items())
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
      _items.Add((slotToUse, item.ID));
    }
    else
    {
      // There was no free slot, which I am not currently handling...
    }

    return slotToUse;
  }

  public Item? RemoveByID(ulong id)
  {
    for (int j = 0; j < _items.Count; j++)
    {
      if (_items[j].Item2 == id)
      {
        var item = _objDb.GetObj(_items[j].Item2) as Item;
        _items.RemoveAt(j);
        return item;
      }
    }

    return null;
  }

  public List<Item> Remove(char slot, int count)
  {
    List<int> indexes = [];
    for (int j = _items.Count - 1; j >= 0; j--)
    {
      var item = _objDb.GetObj(_items[j].Item2) as Item;
      if (item.Slot == slot)
        indexes.Add(j);
    }

    List<Item> removed = [];
    int totalToRemove = int.Min(count, indexes.Count);
    for (int j = 0; j < totalToRemove; j++)
    {
      int index = indexes[j];
      var item = _objDb.GetObj(_items[index].Item2) as Item;
      if (item is not null)
      {
        removed.Add(item);
        _items.RemoveAt(index);
      }
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
    foreach (var (s, id) in _items)
    {

      if (s == slot)
      {
        item = _objDb.GetObj(id) as Item;
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
        foreach (Item other in Items())
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
        foreach (var other in Items().Where(a => a.Type == ItemType.Armour && a.Equiped))
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

    foreach (var item in Items())
    {
      activeTraits.AddRange(item.ActiveTraits());
    }

    return activeTraits;
  }

  public string ApplyEffect(TerrainFlag effect, GameState gs, Loc loc)
  {
    List<string> msgs = [];

    foreach (var (_, itemID) in _items)
    {
      var i = gs.ObjDb.GetObj(itemID);
      if (i is Item item)
      {
        string m = item.ApplyEffect(effect, gs, loc);
        msgs.Add(m);
      }
    }

    return string.Join(' ', msgs).Trim();
  }

  public virtual string ToText() => string.Join(',', _items.Select(i => $"{i.Item1}#{i.Item2}"));

  public virtual void RestoreFromText(string txt)
  {
    foreach (var i in txt.Split(','))
    {
      char slot = i[0];
      ulong id = ulong.Parse(i.Substring(2));
      _items.Add((slot, id));
    }
  }
}

class EmptyInventory(ulong ownerID) : Inventory(ownerID, null)
{
  protected override List<Item> Items() => [];
  public override string ToText() => "";
  public override void RestoreFromText(string txt) { }
}

enum EquipingResult
{
  Equiped,
  Unequiped,
  Conflict
}