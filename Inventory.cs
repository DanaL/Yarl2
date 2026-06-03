// Delve - A roguelike computer RPG
// Written in 2026 by Dana Larose <ywg.dana@gmail.com>
//
// To the extent possible under law, the author(s) have dedicated all copyright
// and related and neighboring rights to this software to the public domain
// worldwide. This software is distributed without any warranty.
//
// You should have received a copy of the CC0 Public Domain Dedication along
// with this software. If not,
// see <http://creativecommons.org/publicdomain/zero/1.0/>.

namespace Yarl2;

enum Component
{
  SulphurousAsh, Ginseng, Garlic, SpiderSilk, BloodMoss, BlackPearl, Nightshade, MandrakeRoot
}

class Inventory(ulong ownerID, GameObjectDB objDb)
{
  public ulong OwnerID { get; init; } = ownerID;
  public int Zorkmids { get; set; }
  readonly List<(char, ulong)> _items = [];
  public char LastSlot { get; set; } = '\0';
  readonly GameObjectDB _objDb = objDb;
  Dictionary<Component, int> _components = Enum.GetValues<Component>().ToDictionary(c => c, _ => 0);

  public bool Contains(ulong itemID)
  {
    foreach (var item in _items)
    {
      if (item.Item2 == itemID)
        return true;
    }

    return false;
  }

  public virtual List<Item> Items()
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

  bool PlayerInventory() => _objDb.GetObj(OwnerID) is Player;

  public char[] UsedSlots() => [.. _items.Select(i => i.Item1).Distinct()];

  char[] AvailableSlots()
  {
    var allSlots = Enumerable.Range('a', 26).Select(i => (char)i);
    char[] usedSlots = UsedSlots();

    return [.. allSlots.Where(c => !usedSlots.Contains(c))];
  }

  public (Item?, int) ItemAt(char slot)
  {
    List<(char, ulong)> inSlot = [.. _items.Where(i => i.Item1 == slot)];
    var item = _objDb.GetObj(inSlot.First().Item2) as Item;

    return (item, inSlot.Count);
  }

  public bool ShieldEquipped()
  {
    foreach (var item in Items().Where(i => i.Type == ItemType.Armour && i.Equipped))
    {
      if (item.Traits.OfType<ArmourTrait>().FirstOrDefault() is ArmourTrait at && at.Part == ArmourParts.Shield)
        return true;
    }

    return false;
  }

  public bool FocusEquipped() => Items().Any(i => i.Type == ItemType.Wand && i.Equipped);

  public Item? ReadiedWeapon()
  {
    foreach (var item in Items())
    {
      if ((item.Type == ItemType.Weapon || item.Type == ItemType.Tool) && item.Equipped)
        return item;
    }

    return null;
  }

  public Item? ReadiedBow()
  {
    foreach (var bow in Items().Where(i => i.Type == ItemType.Bow && i.Equipped))
      return bow;

    return null;
  }

  static Component TxtToComponent(string s) => s switch
  {
    "sulphurous ash" => Component.SulphurousAsh,
    "black pearl" => Component.BlackPearl,
    _ => throw new Exception($"Unknown spell component: {s}")
  };

  public char Add(Item item, ulong ownerID)
  {
    if (item.Type == ItemType.Zorkmid)
    {
      Zorkmids += item.Value;
      return '$';
    }
    else if (item.Type == ItemType.Component)
    {
      _components[TxtToComponent(item.Name)]++;
      return '$';
    }

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
        if (other.Type == item.Type && other.FullName == item.FullName)
        {
          slotToUse = other.Slot;
          break;
        }
      }
    }

    // If there are no slots available, and we didn't find an item to stack with
    // then the inventory is full, so return a null character
    char[] availableSlots = AvailableSlots();
    if (slotToUse == '\0' && availableSlots.Length == 0)
    {
      return '\0';
    }

    // Find the slot for the item
    HashSet<char> usedSlots = [.. UsedSlots()];
    if (slotToUse == '\0' && item.Slot != '\0' && !usedSlots.Contains(item.Slot))
    {
      slotToUse = item.Slot;
    }

    if (slotToUse == '\0')
    {
      char nextSlot = availableSlots.FirstOrDefault(c => c > LastSlot);
      slotToUse = nextSlot == '\0' ? availableSlots[0] : nextSlot;
    }

    item.Slot = slotToUse;
    item.ContainedBy = ownerID;
    _items.Add((slotToUse, item.ID));

    LastSlot = slotToUse;

    return slotToUse;
  }

  void CheckForGrantsToRemove(Item item, GameState gs)
  {
    if (gs.ObjDb.GetObj(OwnerID) is not Actor owner)
      return;

    foreach (GrantsTrait trait in item.Traits.OfType<GrantsTrait>())
    {
      trait.Remove(owner, gs, item);
    }
  }

  public Item? RemoveByID(ulong id, GameState gs)
  {
    for (int j = 0; j < _items.Count; j++)
    {
      if (_items[j].Item2 == id && _objDb.GetObj(_items[j].Item2) is Item item)
      {
        _items.RemoveAt(j);
        if (!PlayerInventory())
          item!.Slot = '\0';
        item!.ContainedBy = 0;

        if (item.Equipped)
        {
          item.Equipped = false;
          CheckForGrantsToRemove(item, gs);
        }

        return item;
      }
    }

    return null;
  }

  public List<Item> Remove(char slot, int count, GameState gs)
  {
    List<int> indexes = [];
    for (int j = _items.Count - 1; j >= 0; j--)
    {
      if (_objDb.GetObj(_items[j].Item2) is Item item && item.Slot == slot)
      {
        indexes.Add(j);
      }
    }

    List<Item> removed = [];
    int totalToRemove = int.Min(count, indexes.Count);
    bool playerInventory = PlayerInventory();
    for (int j = 0; j < totalToRemove; j++)
    {
      int index = indexes[j];
      var item = _objDb.GetObj(_items[index].Item2) as Item;
      if (item is not null)
      {
        if (!playerInventory)
          item.Slot = '\0';
        item.ContainedBy = 0;
        removed.Add(item);

        if (item.Equipped)
        {
          item.Equipped = false;
          CheckForGrantsToRemove(item, gs);
        }

        _items.RemoveAt(index);
      }
    }

    return removed;
  }

  static (EquipingResult, ArmourParts, Item?) UnequipItem(Item item)
  {
    item.Equipped = false;

    return (EquipingResult.Unequipped, ArmourParts.None, null);
  }

  static (EquipingResult, ArmourParts, Item?) ToggleWand(Item wand, int freeHands)
  {
    if (wand.Equipped)
    {
      return UnequipItem(wand);
    }

    if (freeHands > 0)
    {
      wand.Equipped = true;
      return (EquipingResult.Equipped, ArmourParts.None, null);
    }

    return (EquipingResult.NoFreeHand, ArmourParts.None, null);
  }

  // This toggles the equip status of gear only and recalculation of stuff
  // like armour class has to be done elsewhere because it felt icky to
  // have a reference back to the inventory's owner in the inventory object
  public (EquipingResult, ArmourParts, Item?) ToggleEquipStatus(char slot)
  {
    bool twoHandedWeapon = ReadiedWeapon() is Item w && w.HasTrait<TwoHandedTrait>();
    bool bowEquipped = ReadiedBow() is not null;

    // I suppose at some point I'll have items that can't be equipped
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

    bool shield = ShieldEquipped();

    if (item is not null)
    {
      int freeHands = 2;
      if (twoHandedWeapon)
        freeHands = 0;
      else
      {
        if (ReadiedWeapon() is not null)
          --freeHands;
        if (shield)
          --freeHands;
        if (FocusEquipped())
          --freeHands;
      }

      if (item.Type == ItemType.Wand)
        return ToggleWand(item, freeHands);

      if (item.Equipped && item.Type == ItemType.Arrow)
      {
        int count = EquipStackable(item, false);
        return (count > 1 ? EquipingResult.StackUnequipped : EquipingResult.Unequipped, ArmourParts.None, null);
      }
      else if (item.Equipped)
      {
        return UnequipItem(item);
      }

      if (item.Type == ItemType.Weapon || item.Type == ItemType.Tool)
      {
        if (freeHands == 0 && shield && item.HasTrait<TwoHandedTrait>())
          return (EquipingResult.NoFreeHand, ArmourParts.None, null);


        Item? swappedItem = null;
        // If there is a weapon already equipped, unequip it
        foreach (Item other in Items())
        {
          if ((other.Type == ItemType.Weapon || other.Type == ItemType.Tool) && other.Equipped)
          {
            other.Equipped = false;
            swappedItem = other;
            break;
          }
        }

        item.Equipped = true;

        return (EquipingResult.Equipped, ArmourParts.None, swappedItem);
      }
      else if (item.Type == ItemType.Bow)
      {
        if (ShieldEquipped())
          return (EquipingResult.NoFreeHand, ArmourParts.None, null);

        Item? swapped = null;
        foreach (Item other in Items())
        {
          if ((other.Type == ItemType.Bow) && other.Equipped)
          {
            other.Equipped = false;
            swapped = other;
            break;
          }
        }

        item.Equipped = true;
        return (EquipingResult.Equipped, ArmourParts.None, swapped);
      }
      else if (item.Type == ItemType.Armour)
      {
        ArmourParts part = ArmourParts.None;
        foreach (ArmourTrait t in item.Traits.OfType<ArmourTrait>())
        {
          part = t.Part;
          break;
        }

        // check to see if there's another piece in that slot
        foreach (var other in Items().Where(a => a.Type == ItemType.Armour && a.Equipped))
        {
          foreach (var t in other.Traits.OfType<ArmourTrait>())
          {
            if (t.Part == part)
              return (EquipingResult.Conflict, part, null);
          }
        }

        if (part is ArmourParts.Shield && freeHands == 0)
          return (EquipingResult.NoFreeHand, part, null);

        if (part is ArmourParts.Shield && twoHandedWeapon)
          return (EquipingResult.TwoHandedConflict, part, null);
        else if (part is ArmourParts.Shield && bowEquipped)
          return (EquipingResult.BowConflict, part, null);

        item.Equipped = !item.Equipped;

        return (EquipingResult.Equipped, ArmourParts.Shirt, null);
      }
      else if (item.Type == ItemType.Ring)
      {
        if (item.Equipped)
        {
          item.Equipped = false;
        }
        else
        {
          int ringCount = Items().Count(i => i.Type == ItemType.Ring && i.Equipped);
          if (ringCount == 2)
            return (EquipingResult.TooManyRings, ArmourParts.None, null);

          item.Equipped = true;

          return (EquipingResult.Equipped, ArmourParts.None, null);
        }
      }
      else if (item.Type == ItemType.Talisman)
      {
        if (item.Equipped)
        {
          item.Equipped = false;
        }
        else
        {
          int talismanCount = Items().Count(i => i.Type == ItemType.Talisman && i.Equipped);
          if (talismanCount == 2)
            return (EquipingResult.TooManyTalismans, ArmourParts.None, null);

          item.Equipped = true;

          return (EquipingResult.Equipped, ArmourParts.None, null);
        }
      }
      else if (item.Type == ItemType.Arrow)
      {
        int count = EquipStackable(item, !item.Equipped);
        return (count > 1 ? EquipingResult.StackEquipped : EquipingResult.Equipped, ArmourParts.None, null);
      }
      // I think by this point it would be error for an item to not have the
      // equipable trait, but check for it here because and item that does not
      // with get flagged with a conflict
      else if (item.HasTrait<EquipableTrait>())
      {
        item.Equipped = !item.Equipped;
        return (EquipingResult.Equipped, ArmourParts.None, null);
      }
    }

    return (EquipingResult.Conflict, ArmourParts.None, null);

    int EquipStackable(Item item, bool status)
    {
      var items = Items().Where(i => i.Name == item.Name);
      foreach (Item other in items)
        other.Equipped = status;
      return items.Count();
    }
  }

  public string ApplyEffectToInv(DamageType damageType, GameState gs, Loc loc)
  {
    Actor? owner = (Actor?)gs.ObjDb.GetObj(OwnerID);
    List<string> msgs = [];

    var items = Items();
    bool cloaked = items.Any(i => i.Equipped && i.Type == ItemType.Armour && i.Traits.Any(t => t is ArmourTrait at && at.Part == ArmourParts.Cloak));
    foreach (var item in items)
    {
      bool shielded = cloaked && ProtectedByCloak(item);
      var (s, destroyed) = Effects.Apply(damageType, gs, item, owner, shielded);
      if (s != "")
        msgs.Add(s);
      if (destroyed)
        gs.ItemDestroyed(item, loc);
    }

    return string.Join(' ', msgs).Trim();

    static bool ProtectedByCloak(Item item)
    {
      if (item.Type == ItemType.Weapon && item.Equipped)
        return false;
      else if (item.Type == ItemType.Tool && item.Equipped)
        return false;
      else if (item.Type == ItemType.Armour && item.Equipped
            && item.Traits.Any(t => t is ArmourTrait at && (at.Part == ArmourParts.Shield || at.Part == ArmourParts.Hat)))
        return false;

      return true;
    }
  }

  public void ConsumeItem(Item item, Actor actor, GameState gs)
  {
    // A character with the Scholar background has a chance of not actually consuming a scroll
    // when they read it.
    if (item.HasTrait<ScrollTrait>())
    {
      double roll = gs.Rng.NextDouble();
      if (actor is Player player && player.Background == PlayerBackground.Scholar && roll < 0.2)
        return;
    }

    RemoveByID(item.ID, gs);
    gs.ObjDb.RemoveItemFromGame(actor.Loc, item);
  }

  List<char> SortedSlots()
  {
    List<char> weapons = [];
    List<char> armour = [];
    List<char> arrows = [];
    List<char> scrolls = [];
    List<char> potions = [];
    List<char> wands = [];
    List<char> tools = [];
    List<char> talisman = [];
    List<char> rings = [];
    List<char> other = [];

    var ussss = UsedSlots();
    foreach (char s in UsedSlots().Order())
    {
      var (item, _) = ItemAt(s);
      switch (item!.Type)
      {
        case ItemType.Weapon:
        case ItemType.Bow:
          weapons.Add(s);
          break;
        case ItemType.Arrow:
          arrows.Add(s);
          break;
        case ItemType.Armour:
          armour.Add(s);
          break;
        case ItemType.Scroll:
          scrolls.Add(s);
          break;
        case ItemType.Potion:
          potions.Add(s);
          break;
        case ItemType.Wand:
          wands.Add(s);
          break;
        case ItemType.Tool:
          tools.Add(s);
          break;
        case ItemType.Talisman:
          talisman.Add(s);
          break;
        case ItemType.Ring:
          rings.Add(s);
          break;
        default:
          other.Add(s);
          break;
      }
    }

    List<char> slots = [];
    slots.AddRange(SortSlots(weapons));
    slots.AddRange(SortSlots(arrows));
    slots.AddRange(SortSlots(armour));
    slots.AddRange(SortSlots(scrolls)); // You can't equip scrolls or potions
    slots.AddRange(SortSlots(potions)); // but who knows what the future holds
    slots.AddRange(SortSlots(wands));
    slots.AddRange(SortSlots(talisman));
    slots.AddRange(SortSlots(rings));
    slots.AddRange(SortSlots(tools));
    slots.AddRange(SortSlots(other));

    return slots;

    List<char> SortSlots(List<char> slots) => [.. slots.OrderByDescending(s => ItemAt(s).Item1!.Equipped)];
  }

  public void ShowMenu(UserInterface ui, InventoryOptions options)
  {
    List<char> slots = SortedSlots();

    List<string> lines = [slots.Count == 0 ? "You are empty handed." : options.Title];
    foreach (var s in slots)
    {
      var (item, count) = ItemAt(s);

      if (item is null)
        continue;

      if ((options.Options & InvOption.OnlyEquipable) != InvOption.None && !item.HasTrait<EquipableTrait>())
        continue;

      if ((options.Options & InvOption.OnlyUseable) != InvOption.None && !item.IsUseableItem())
        continue;

      if ((options.Options & InvOption.UnidentifiedOnly) == InvOption.UnidentifiedOnly)
      {
        bool known = !Item.IDInfo.TryGetValue(item.Name, out var idInfo) || idInfo.Known;
        foreach (Trait t in item.Traits)
        {
          if (t is WandTrait wt && !wt.IDed)
            known = false;
        }

        if (known)
          continue;
      }

      string desc = "";
      if (item.HasTrait<PluralTrait>())
        desc = $"some {item.FullName}";
      else if (item.HasTrait<ArtifactTrait>())
        desc = item.FullName.DefArticle();
      else if (count > 1)
        desc = $"{count} {item.FullName.Pluralize()}";
      else if (item.HasTrait<NamedTrait>())
        desc = item.FullName;
      else
        desc = item.FullName.IndefArticle();

      if (item.Equipped)
      {
        if (item.Type == ItemType.Weapon || item.Type == ItemType.Tool)
        {
          if (item.HasTrait<TwoHandedTrait>())
            desc += " [GREY (in hands)]";
          else if (!item.HasTrait<VersatileTrait>())
            desc += " [GREY (in hand)]";
          else if (ShieldEquipped())
            desc += " [GREY (in hand)]";
          else
            desc += " [GREY (in hands)]";
        }
        else if (item.Type == ItemType.Armour)
          desc += " [GREY (worn)]";
        else if (item.Type == ItemType.Bow)
          desc += " [GREY (equipped)]";
        else if (item.Type == ItemType.Ring)
          desc += " [GREY (wearing)]";
        else if (item.Type == ItemType.Wand)
          desc += " [LIGHTBLUE (focus)]";
        else if (item.Type == ItemType.Arrow)
          desc += " [GREY (readied)]";
        else
          desc += " [GREY (equipped)]";
      }

      if (desc.Contains(" poisoned"))
        desc = desc.Replace(" poisoned", " [GREEN poisoned]");
      else if (desc.Contains(" poison"))
        desc = desc.Replace(" poison", " [GREEN poison]");
      desc = desc.Replace(" holy water", " [ICEBLUE holy water]");
      desc = desc.Replace(" (lit)", " ([YELLOWORANGE lit][BROWN )]");

      lines.Add($"{s}) [{PickColour(item)} {desc}]");
    }

    if ((options.Options & InvOption.MentionMoney) == InvOption.MentionMoney)
    {
      lines.Add("");
      if (Zorkmids == 0)
        lines.Add("You seem to be broke.");
      else if (Zorkmids == 1)
        lines.Add("You have a single zorkmid.");
      else
        lines.Add($"Your wallet contains [YELLOW {Zorkmids}] zorkmids.");
    }

    if (!string.IsNullOrEmpty(options.Instructions))
    {
      lines.Add("");
      lines.AddRange(options.Instructions.Split('\n'));
    }

    ui.ShowDropDown(lines);

    static string PickColour(Item item) => item.Type switch
    {
      ItemType.Weapon => "LIGHTGREY",
      ItemType.Armour => "SOFTRED",
      ItemType.Scroll => "CREAM",
      ItemType.Potion => "LIGHTBLUE",
      ItemType.Wand => "GREEN",
      ItemType.Talisman => "LIGHTPURPLE",
      ItemType.Tool => "BROWN",
      ItemType.Ring => "YELLOW",
      ItemType.Arrow => "LIGHTBROWN",
      _ => "WHITE"
    };
  }

  public virtual string ToText()
  {
    string items = string.Join(',', _items.Select(i => $"{i.Item1}#{i.Item2}"));
    string components = string.Join(',', _components.Select(kvp => $"{(int)kvp.Key}:{kvp.Value}"));
    return $"{items}|{components}";
  }

  public virtual void RestoreFromText(string txt)
  {
    if (txt == "")
      return;

    string[] sections = txt.Split('|');
    if (sections[0] != "")
    {
      foreach (var i in sections[0].Split(','))
      {
        char slot = i[0];
        ulong id = ulong.Parse(i[2..]);
        _items.Add((slot, id));
      }
    }

    if (sections.Length > 1 && sections[1] != "")
    {
      foreach (var pair in sections[1].Split(','))
      {
        var parts = pair.Split(':');
        _components[(Component)int.Parse(parts[0])] = int.Parse(parts[1]);
      }
    }
  }
}

[Flags]
enum InvOption
{
  None = 0,
  MentionMoney = 1,
  UnidentifiedOnly = 2,
  OnlyUseable = 4,
  OnlyEquipable = 8
}

class InventoryOptions
{
  public string Title { get; set; } = "";
  public string Instructions { get; set; } = "";
  public InvOption Options { get; set; } = InvOption.None;

  public InventoryOptions() { }
  public InventoryOptions(string title) => Title = title;
}
