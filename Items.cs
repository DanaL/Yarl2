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

using System.Text.Json;

namespace Yarl2;

enum ItemType
{
  Weapon,
  Bow,
  Armour,
  Zorkmid,
  Tool,
  Document,
  Potion,
  Scroll,
  Trinket,
  Wand,
  Ring,
  Bone,
  Food,
  Talisman,
  Reagent,
  Environment, // I'm implementing things like mist as 'items'
  Landscape,
  Statue,
  Illusion
}

record ItemIDInfo(bool Known, string Desc);
class Item : GameObj, IEquatable<Item>
{
  public static Dictionary<string, ItemIDInfo> IDInfo { get; set; } = [];
  public static readonly int DEFAULT_Z = 2;
  public ItemType Type { get; set; }
  public char Slot { get; set; }
  public bool Equipped { get; set; } = false;
  public ulong ContainedBy { get; set; } = 0;
  public int Value { get; set; }
  int _z = DEFAULT_Z;

  public void SetZ(int z) => _z = z;
  public override int Z()
  {
    return _z;
  }

  string CalcFullName()
  {
    string name;
    if (IDInfo.TryGetValue(Name, out var idInfo) && idInfo.Known == false)
    {
      name = idInfo.Desc;
    }
    else
    {
      name = Name;
    }

    List<string> adjs = [];
    int bonus = 0;
    foreach (Trait trait in Traits)
    {
      if (trait is AdjectiveTrait adj)
        adjs.Add(adj.Adj.ToLower());
      else if (trait is ArmourTrait armour && armour.Bonus != 0)
        bonus = armour.Bonus;
      else if (trait is WeaponBonusTrait wb && wb.Bonus != 0)
        bonus = wb.Bonus;
      else if (trait is MetalTrait metal && Type == ItemType.Tool)
        adjs.Add(metal.Type.ToString().ToLower());
    }

    string adjectives = string.Join(", ", adjs);
    string fullname = adjectives.Trim();
    if (bonus > 0)
      fullname += $" +{bonus}";
    else if (bonus < 0)
      fullname += $" {bonus}";
    fullname += " " + name;
    
    var traitDescs = Traits.Where(t => t is IDesc id)
                           .Select(t => ((IDesc) t).Desc());
    if (traitDescs.Any())
      fullname += " " + string.Join(' ', traitDescs);

    return fullname.Trim();
  }

  public override string FullName => CalcFullName();

  public Metals MetalType()
  {
    var t = Traits.OfType<MetalTrait>().FirstOrDefault();
 
    return t is not null ? t.Type : Metals.NotMetal;
   }

  public bool CanCorrode()
  {
    var m = Traits.OfType<MetalTrait>().FirstOrDefault();

    if (m is null)
      return false;

    return m.Type.CanCorrode();
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

    return i.FullName == FullName && i.Type == Type;
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

enum ItemNames
{
  ANTIDOTE, ANTISNAIL_SANDALS, APPLE, ARROW, BATTLE_AXE, BEETLE_CARAPACE, BLINDFOLD, BOOTS_OF_WATER_WALKING, 
  CAMPFIRE, CHAINMAIL, CLAYMORE, CUTPURSE_CREST, DAGGER, DART, FIRE_GIANT_ESSENCE, FIREBOLT, FLASK_OF_BOOZE, 
  FROST_GIANT_ESSENCE, GASTON_BADGE, GHOSTCAP_MUSHROOM, GOLDEN_APPLE, GREATSWORD, GUIDE_STABBY, GUIDE_AXES, GUIDE_BOWS, 
  GUIDE_SWORDS, GUISARME, HAND_AXE, HEAVY_BOOTS, HELMET, HILL_GIANT_ESSENCE, LEATHER_ARMOUR, LEATHER_GLOVES, 
  LESSER_BURLY_CHARM, LESSER_GRACE_CHARM, LESSER_HEALTH_CHARM,  LOCK_PICK, LONGBOW, LONGSWORD, MACE, MITHRIL_ORE, 
  OGRE_LIVER, PICKAXE, POTION_BLINDNESS, POTION_COLD_RES, POTION_FIRE_RES, POTION_HEALING, POTION_MIND_READING, 
  POTION_OF_LEVITATION, RAPIER, RING_OF_ADORNMENT, RING_OF_AGGRESSION, RING_OF_FRAILITY, RING_OF_PROTECTION, RINGMAIL, 
  RUBBLE, SCROLL_BLINK, SCROLL_DISARM, SCROLL_IDENTIFY, SCROLL_KNOCK, SCROLL_MAGIC_MAP, SCROLL_PROTECTION, SCROLL_RECALL,
  SHIELD, SHORTSHORD, SILVER_DAGGER, SILVER_LONGSWORD, SKULL, SMOULDERING_CHARM, SPEAR, STATUE, STUDDED_LEATHER_ARMOUR, 
  TALISMAN_OF_CIRCUMSPECTION, TORCH, TROLL_BROOCH, VIAL_OF_POISON, WAND_FIREBALLS, WAND_FROST, WAND_HEAL_MONSTER, 
  WAND_MAGIC_MISSILES, WAND_SLOW_MONSTER, WAND_SWAP, ZORKMIDS, ZORKMIDS_GOOD, ZORKMIDS_MEDIOCRE, ZORKMIDS_PITTANCE
}

class JsonItem
{
  public string Name { get; set; } = "";
  public string DescriptiveName { get; set; } = "";
  public string Type { get; set; } = "";
  public int Value { get; set; }
  public string Glyph { get; set; } = "";
  public List<string> Traits { get; set; } = [];
}

class ItemTemplate
{
  public string Name { get; set; } = "";
  public ItemType Type { get; set; }
  public int Value { get; set; }
  public Glyph Glyph { get; set; }
  public List<string> TraitTemplates { get; set; } = [];  
}

class ItemFactory
{
  static Dictionary<ItemNames, ItemTemplate> Items { get; } = LoadItemDefs();

  static Dictionary<ItemNames, ItemTemplate> LoadItemDefs()
  {
    string jsonPath = ResourcePath.GetDataFilePath("items.json");
    string json = File.ReadAllText(jsonPath);
    var items = JsonSerializer.Deserialize<List<JsonItem>>(json) 
      ?? throw new Exception("Missing or corrupt items definition file!");
    Dictionary<ItemNames, ItemTemplate> templates = [];
    foreach (JsonItem item in items)
    {
      Enum.TryParse(item.Name, out ItemNames name);
      Enum.TryParse(item.Type, out ItemType type);
      Glyph glyph = Glyph.TextToGlyph(item.Glyph);
      ItemTemplate template = new ItemTemplate()
      {
        Name = item.DescriptiveName,
        Type = type,
        Value = item.Value,
        Glyph = glyph,
        TraitTemplates = item.Traits
      };

      templates.Add(name, template);
    }

    return templates;
  }

  static Item FromTemplate(ItemTemplate template)
  {
    Item item = new Item()
    {
      Name = template.Name,
      Type = template.Type,
      Value = template.Value,
      Glyph = template.Glyph
    };

    foreach (string trait in template.TraitTemplates)
    {
      item.Traits.Add(TraitFactory.FromText(trait, item));
    }

    return item;
  }

  public static Item Get(ItemNames name, GameObjectDB objDB)
  {
    Item item;

    if (Items.TryGetValue(name, out var template)) 
    {
      item = FromTemplate(template);
      objDB.Add(item);

      if (item.Type == ItemType.Wand)
        item.Glyph = GlyphForWand(item.Name);
      if (item.Type == ItemType.Ring)
        item.Glyph = GlyphForRing(item.Name);
      
      foreach (Trait t in item.Traits)
      {
        if (t is IGameEventListener listener && t.Active)
        {
          objDB.EndOfRoundListeners.Add(listener);          
        }
      }

      return item;
    }

    throw new Exception($"Item {name} is not defined!");
  }

  public static Item Illusion(ItemNames name, GameObjectDB objDB)
  {
    Item item = Get(name, objDB);
    item.Traits = [];
    item.Type = ItemType.Illusion;

    return item;    
  }

  static Glyph GlyphForRing(string name)
  {
    string material = Item.IDInfo.TryGetValue(name, out var itemIDInfo) ? itemIDInfo.Desc : "";

    return material switch
    {
      "silver ring" => new Glyph('o', Colours.WHITE, Colours.GREY, Colours.BLACK, Colours.BLACK),
      "iron ring" => new Glyph('o', Colours.GREY, Colours.DARK_GREY, Colours.BLACK, Colours.BLACK),
      "gold ring" => new Glyph('o', Colours.YELLOW, Colours.YELLOW_ORANGE, Colours.BLACK, Colours.BLACK),
      "ruby ring" => new Glyph('o', Colours.BRIGHT_RED, Colours.DULL_RED, Colours.BLACK, Colours.BLACK),
      "diamond ring" => new Glyph('o', Colours.LIGHT_BLUE, Colours.BLUE, Colours.BLACK, Colours.BLACK),
      "jade ring" => new Glyph('o', Colours.DARK_GREEN, Colours.DARK_GREEN, Colours.BLACK, Colours.BLACK),
      "wood ring" => new Glyph('o', Colours.LIGHT_BROWN, Colours.BROWN, Colours.BLACK, Colours.BLACK),
      _ => new Glyph('o', Colours.YELLOW, Colours.YELLOW_ORANGE, Colours.BLACK, Colours.BLACK)
    };
  }

  static Glyph GlyphForWand(string name)
  {
    string material = Item.IDInfo.TryGetValue(name, out var itemIDInfo) ? itemIDInfo.Desc : "";

    return material switch
    {
      "maple wand" => new Glyph('/', Colours.BROWN, Colours.DULL_RED, Colours.BLACK, Colours.BLACK),
      "oak wand" => new Glyph('/', Colours.BROWN, Colours.DULL_RED, Colours.BLACK, Colours.BLACK),
      "birch wand" => new Glyph('/', Colours.LIGHT_BROWN, Colours.BROWN, Colours.BLACK, Colours.BLACK),
      "balsa wand" => new Glyph('/', Colours.LIGHT_BROWN, Colours.BROWN, Colours.BLACK, Colours.BLACK),
      "glass wand" => new Glyph('/', Colours.LIGHT_BLUE, Colours.BLUE, Colours.BLACK, Colours.BLACK),
      "silver wand" => new Glyph('/', Colours.WHITE, Colours.LIGHT_GREY, Colours.BLACK, Colours.BLACK),
      "tin wand" => new Glyph('/', Colours.GREY, Colours.DARK_GREY, Colours.BLACK, Colours.BLACK),
      "iron wand" => new Glyph('/', Colours.GREY, Colours.DARK_GREY, Colours.BLACK, Colours.BLACK),
      "ebony wand" => new Glyph('/', Colours.DARK_BLUE, Colours.DARK_GREY, Colours.BLACK, Colours.BLACK),
      "cherrywood wand" => new Glyph('/', Colours.BRIGHT_RED, Colours.DULL_RED, Colours.BLACK, Colours.BLACK),
      _ => new Glyph('/', Colours.LIGHT_BLUE, Colours.BLUE, Colours.BLACK, Colours.BLACK)
    };
  }

  public static Item Mist(GameState gs)
  {
    var mist = new Item()
    {
      Name = "mist",
      Type = ItemType.Environment,
      Value = 0,
      Glyph = new Glyph('*', Colours.LIGHT_GREY, Colours.GREY, Colours.BLACK, Colours.BLACK)
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
      glyph = new Glyph('\u22CF', Colours.BRIGHT_RED, Colours.DULL_RED, Colours.TORCH_ORANGE, Colours.BLACK);
    else if (roll < 0.666)
      glyph = new Glyph('\u22CF', Colours.YELLOW, Colours.DULL_RED, Colours.TORCH_RED, Colours.BLACK);
    else
      glyph = new Glyph('\u22CF', Colours.YELLOW_ORANGE, Colours.DULL_RED, Colours.TORCH_YELLOW, Colours.BLACK);

    var fire = new Item()
    {
      Name = "fire",
      Type = ItemType.Environment,
      Value = 0,
      Glyph = glyph,      
    };
    fire.SetZ(7);
    gs.ObjDb.Add(fire);
    var onFire = new OnFireTrait() { Expired = false, OwnerID = fire.ID, Spreads = true };
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
      Glyph = new Glyph(':', Colours.WHITE, Colours.GREY, Colours.BLACK, Colours.BLACK)
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
  Shirt,
  Shield,
  Mask,
  Gloves
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

  public bool ShieldEquipped()
  {
    foreach (var item in Items().Where(i => i.Type == ItemType.Armour && i.Equipped))
    {
      if (item.Traits.OfType<ArmourTrait>().FirstOrDefault() is ArmourTrait at && at.Part == ArmourParts.Shield)
        return true;
    }

    return false;
  }

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
        if (other.Type == item.Type && other.FullName == item.FullName)
        {
          slotToUse = other.Slot;
          break;
        }
      }
    }
    
    if (slotToUse == '\0' && item.Slot != '\0' && !usedSlots.Contains(item.Slot))
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
      if (_objDb.GetObj(_items[j].Item2) is Item item && item.Slot == slot)
      {
        indexes.Add(j);
      }        
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
    bool EquippedTwoHandedWeapon() => ReadiedWeapon() is Item w && w.HasTrait<TwoHandedTrait>();

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

    if (item is not null)
    {
      if (item.Equipped)
      {
        if (item.HasTrait<CursedTrait>())
        {
          return (EquipingResult.Cursed, ArmourParts.None);
        }
        
        item.Equipped = false;
        return (EquipingResult.Unequipped, ArmourParts.None);
      }

      if (item.Type == ItemType.Weapon || item.Type == ItemType.Tool || item.Type == ItemType.Bow)
      {
        if (item.HasTrait<TwoHandedTrait>() && ShieldEquipped())
        {
          return (EquipingResult.ShieldConflict, ArmourParts.Shield);
        }

        // If there is a weapon already equipped, unequip it
        foreach (Item other in Items())
        {
          if ((other.Type == ItemType.Weapon || other.Type == ItemType.Tool) && other.Equipped)
            other.Equipped = false;
        }

        item.Equipped = true;
        return (EquipingResult.Equipped, ArmourParts.Shirt);
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
              return (EquipingResult.Conflict, part);
          }
        }

        if (part is ArmourParts.Shield && EquippedTwoHandedWeapon())
        {
          return (EquipingResult.TwoHandedConflict, part);
        }

        item.Equipped = !item.Equipped;

        return (EquipingResult.Equipped, ArmourParts.Shirt);
      }
      else if (item.Type == ItemType.Ring)
      {
        if (item.Equipped)
        {
          item.Equipped = false;
        }
        else
        {
          int ringCount = Items().Where(i => i.Type == ItemType.Ring && i.Equipped).Count();
          if (ringCount == 2)
            return (EquipingResult.TooManyRings, ArmourParts.None);
          
          item.Equipped = true;
          
          return (EquipingResult.Equipped, ArmourParts.None);
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
          int talismanCount = Items().Where(i => i.Type == ItemType.Talisman && i.Equipped).Count();
          if (talismanCount == 2)
            return (EquipingResult.TooManyTalismans, ArmourParts.None);
          
          item.Equipped = true;
          
          return (EquipingResult.Equipped, ArmourParts.None);
        }
      }
    }

    return (EquipingResult.Conflict, ArmourParts.Shirt);
  }

  public string ApplyEffectToInv(EffectFlag effect, GameState gs, Loc loc)
  {
    Actor? owner = (Actor?) gs.ObjDb.GetObj(OwnerID);
    List<string> msgs = [];

    foreach (var item in Items())
    {
      string s = EffectApplier.Apply(effect, gs, item, owner);
      if (s != "")
        msgs.Add(s);
    }

    return string.Join(' ', msgs).Trim();
  }

  public void ConsumeItem(Item item, Actor actor, Random rng)
  {
    // A character with the Scholar background has a chance of not actually consuming a scroll
    // when they read it.
    if (item.HasTrait<ScrollTrait>())
    {
      double roll = rng.NextDouble();
      if (actor is Player player && player.Background == PlayerBackground.Scholar && roll < 0.2)
        return;
    }
    
    RemoveByID(item.ID);
  }

  public void ShowMenu(UserInterface ui, InventoryOptions options)
  {
    var slots = UsedSlots().Order().ToArray();

    List<string> lines = [slots.Length == 0 ? "You are empty handed." : options.Title];
    foreach (var s in slots)
    {      
      var (item, count) = ItemAt(s);

      if (item is null)
        continue;

      if ((options.Options & InvOption.UnidentifiedOnly) == InvOption.UnidentifiedOnly)
      {
        if (!Item.IDInfo.TryGetValue(item.Name, out var idInfo) || idInfo.Known)
          continue;
      }
      
      string desc = "";
      try 
      {
        if (count > 1)
          desc = $"{count} {item.FullName.Pluralize()}";
        else if (item.HasTrait<NamedTrait>())
          desc = item.FullName;
        else if (item.HasTrait<PluralTrait>())
          desc = $"some {item.FullName}";
        else
          desc = item.FullName.IndefArticle();
      }
      catch (Exception ex)
      {
        Console.WriteLine(ex.Message);
      }

      if (item.Equipped)
      {
        if (item.HasTrait<CursedTrait>())
          desc += " *cursed";

        if (item.Type == ItemType.Weapon || item.Type == ItemType.Tool)
        {
          if (item.HasTrait<TwoHandedTrait>())
            desc += " (in hands)";
          else if (!item.HasTrait<VersatileTrait>())
            desc += " (in hand)";
          else if (ShieldEquipped())
            desc += " (in hand)";
          else
            desc += " (in hands)";
        }
        else if (item.Type == ItemType.Armour)
          desc += " (worn)";
        else if (item.Type == ItemType.Bow)
          desc += " (equipped)";
        else if (item.Type == ItemType.Ring)
          desc += " (wearing)";
        else if (item.Type == ItemType.Talisman)
          desc += " (equipped)";
      }
      lines.Add($"{s}) {desc}");
    }

    if ((options.Options & InvOption.MentionMoney) == InvOption.MentionMoney)
    {
      lines.Add("");
      if (Zorkmids == 0)
        lines.Add("You seem to be broke.");
      else if (Zorkmids == 1)
        lines.Add("You have a single zorkmid.");
      else
        lines.Add($"Your wallet contains {Zorkmids} zorkmids.");
    }

    if (!string.IsNullOrEmpty(options.Instructions))
    {
      lines.Add("");
      lines.AddRange(options.Instructions.Split('\n'));
    }

    ui.ShowDropDown(lines);
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

[Flags]
enum InvOption
{
  None = 0,
  MentionMoney = 1,
  UnidentifiedOnly = 2
}

class InventoryOptions
{
  public string Title { get; set; } = "";
  public string Instructions { get; set; } = "";
  public InvOption Options { get; set; } = InvOption.None;

  public InventoryOptions() { }
  public InventoryOptions(string title) => Title = title;  
}

enum EquipingResult
{
  Equipped,
  Unequipped,
  Conflict,
  ShieldConflict,
  TwoHandedConflict,
  TooManyRings,
  TooManyTalismans,
  Cursed
}

class EmptyInventory : Inventory 
{
    public EmptyInventory() : base(0, null!) { }

    public override List<Item> Items() => [];
    public override string ToText() => "";
    public override void RestoreFromText(string txt) { }
}