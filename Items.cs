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
  Environment,
  Fog,
  Landscape,
  Statue,
  Illusion,
  Device,
  Altar
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

  public bool Equipable() => Type switch
  {
    ItemType.Armour => true,
    ItemType.Weapon => true,
    ItemType.Tool => true,
    ItemType.Bow => true,
    ItemType.Ring => true,
    ItemType.Talisman => true,
    ItemType.Wand => true,
    _ => false
  };

  public bool IsUseableTool()
  {
    foreach (Trait t in Traits)
    {
      if (t is DiggingToolTrait)
        return true;
      else if (t is DoorKeyTrait)
        return true;
    }

    return false;
  }

  public void Identify()
  {
    if (IDInfo.TryGetValue(Name, out var idInfo))
      IDInfo[Name] = idInfo with { Known = true };
    
    foreach (Trait t in Traits)
    {
      if (t is WandTrait wt)
        wt.IDed = true;
    }
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
  CAMPFIRE, CHAINMAIL, CLAYMORE, CLOAK_OF_PROTECTION, CROESUS_CHARM, CUTPURSE_CREST, DAGGER, DART, FEATHERFALL_BOOTS,
  FIRE_GIANT_ESSENCE, FIREBOLT, FLASK_OF_BOOZE, FROST_GIANT_ESSENCE, GASTON_BADGE, GAUNTLETS_OF_POWER, GENERIC_WAND, 
  GHOSTCAP_MUSHROOM, GOLDEN_APPLE, GREATSWORD, GUIDE_STABBY, GUIDE_AXES, GUIDE_BOWS, GUIDE_SWORDS, GUISARME, HAND_AXE, 
  HEAVY_BOOTS, HELMET, HILL_GIANT_ESSENCE, LEATHER_ARMOUR, LEATHER_GLOVES, LESSER_BURLY_CHARM, LESSER_GRACE_CHARM, 
  LESSER_HEALTH_CHARM,  LOCK_PICK, LONGBOW, LONGSWORD, MACE, MEDITATION_CRYSTAL, MITHRIL_ORE, MUSHROOM_STEW, 
  OGRE_LIVER, PICKAXE, POTION_BLINDNESS, POTION_COLD_RES, POTION_FIRE_RES, POTION_HEALING, POTION_HEROISM, 
  POTION_MIND_READING, POTION_OF_LEVITATION, POTION_OBSCURITY, QUARTERSTAFF, RAPIER, RING_OF_ADORNMENT, RING_OF_AGGRESSION,
  RING_OF_FRAILITY, RING_OF_PROTECTION, RINGMAIL, RUBBLE, SCROLL_BLINK, SCROLL_DISARM, SCROLL_IDENTIFY, SCROLL_KNOCK, 
  SCROLL_MAGIC_MAP, SCROLL_PROTECTION, SCROLL_RECALL, SCROLL_SCATTERING, SCROLL_TRAP_DETECTION, SCROLL_TREASURE_DETECTION, 
  SEEWEED, SHIELD, SHORTSHORD, SILVER_DAGGER, SILVER_LONGSWORD, SKELETON_KEY, SKULL, SMOULDERING_CHARM, SPEAR, STATUE, 
  STONE_ALTAR, STUDDED_LEATHER_ARMOUR, TALISMAN_OF_CIRCUMSPECTION, TORCH, TROLL_BROOCH, VIAL_OF_POISON, WAND_DIGGING, 
  WAND_FIREBALLS, WAND_FROST, WAND_HEAL_MONSTER, WAND_MAGIC_MISSILES, WAND_SLOW_MONSTER, WAND_SUMMONING, WAND_SWAP, 
  WIND_FAN, ZORKMIDS, ZORKMIDS_GOOD, ZORKMIDS_MEDIOCRE, ZORKMIDS_PITTANCE,

  RED_CRYSTAL, BLUE_CRYSTAL
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
    Item item = new()
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
      "silver ring" => new Glyph('o', Colours.WHITE, Colours.GREY, Colours.BLACK, false),
      "iron ring" => new Glyph('o', Colours.GREY, Colours.DARK_GREY, Colours.BLACK, false),
      "gold ring" => new Glyph('o', Colours.YELLOW, Colours.YELLOW_ORANGE, Colours.BLACK, false),
      "ruby ring" => new Glyph('o', Colours.BRIGHT_RED, Colours.DULL_RED, Colours.BLACK, false),
      "diamond ring" => new Glyph('o', Colours.LIGHT_BLUE, Colours.BLUE, Colours.BLACK, false),
      "jade ring" => new Glyph('o', Colours.DARK_GREEN, Colours.DARK_GREEN, Colours.BLACK, false),
      "wood ring" => new Glyph('o', Colours.LIGHT_BROWN, Colours.BROWN, Colours.BLACK, false),
      _ => new Glyph('o', Colours.YELLOW, Colours.YELLOW_ORANGE, Colours.BLACK, false)
    };
  }

  static Glyph GlyphForWand(string name)
  {
    string material = Item.IDInfo.TryGetValue(name, out var itemIDInfo) ? itemIDInfo.Desc : "";

    return material switch
    {
      "maple wand" => new Glyph('/', Colours.BROWN, Colours.DULL_RED, Colours.BLACK, false),
      "oak wand" => new Glyph('/', Colours.BROWN, Colours.DULL_RED, Colours.BLACK, false),
      "birch wand" => new Glyph('/', Colours.LIGHT_BROWN, Colours.BROWN, Colours.BLACK, false),
      "balsa wand" => new Glyph('/', Colours.LIGHT_BROWN, Colours.BROWN, Colours.BLACK, false),
      "glass wand" => new Glyph('/', Colours.LIGHT_BLUE, Colours.BLUE, Colours.BLACK, false),
      "silver wand" => new Glyph('/', Colours.WHITE, Colours.LIGHT_GREY, Colours.BLACK, false),
      "tin wand" => new Glyph('/', Colours.GREY, Colours.DARK_GREY, Colours.BLACK, false),
      "iron wand" => new Glyph('/', Colours.GREY, Colours.DARK_GREY, Colours.BLACK, false),
      "ebony wand" => new Glyph('/', Colours.DARK_BLUE, Colours.DARK_GREY, Colours.BLACK, false),
      "cherrywood wand" => new Glyph('/', Colours.BRIGHT_RED, Colours.DULL_RED, Colours.BLACK, false),
      "jade wand" => new Glyph('/', Colours.DARK_GREEN, Colours.DARK_GREEN, Colours.BLACK, false),
      _ => new Glyph('/', Colours.LIGHT_BLUE, Colours.BLUE, Colours.BLACK, false)
    };
  }

  public static Item Mist(GameState gs)
  {
    var mist = new Item()
    {
      Name = "mist",
      Type = ItemType.Fog,
      Value = 0,
      Glyph = new Glyph('≈', Colours.GREY, Colours.GREY, Colours.DARK_GREY, true)
    };
    mist.SetZ(10);
    mist.Traits.Add(new OpaqueTrait() { Visibility = 3 });
    
    return mist;
  }

  public static Item Fog(GameState gs)
  {
    var mist = new Item()
    {
      Name = "fog",
      Type = ItemType.Fog,
      Value = 0,
      Glyph = new Glyph('*', Colours.GREY, Colours.GREY, Colours.DARK_GREY, true)
    };
    mist.SetZ(10);
    mist.Traits.Add(new OpaqueTrait() { Visibility = 0 });
    mist.Traits.Add(new CountdownTrait()
    {
      OwnerID = mist.ID,
      ExpiresOn = gs.Turn + 7
    });

    return mist;
  }

  public static Item Lamp(GameObjectDB objDb, Dir dir)
  {
    char ch = dir switch
    {
      Dir.North => '◓',
      Dir.South => '◒',
      Dir.West => '◐',
      Dir.East => '◑',
      _ => '◠'
    };

    Item lamp = new()
    {
      Name = "lamp",
      Type = ItemType.Device,
      Value = 0,
      Glyph = new(ch, Colours.YELLOW, Colours.YELLOW_ORANGE, Colours.BLACK, false)
    };
    
    lamp.Traits.Add(new DirectionTrait() { Dir = dir });
    lamp.Traits.Add(new AffixedTrait());

    LightBeamTrait beam = new() { SourceId = lamp.ID };
    lamp.Traits.Add(beam);

    objDb.Add(lamp);
    objDb.EndOfRoundListeners.Add(beam);

    return lamp;
  }

  public static Item BeamTarget(GameObjectDB objDb)
  {
    Item target = new()
    {
      Name = "stone block",
      Type = ItemType.Device,
      Value = 0,
      Glyph = new('☆', Colours.YELLOW, Colours.YELLOW_ORANGE, Colours.DARK_GREY, false)
    };
    target.Traits.Add(new AffixedTrait());
    target.Traits.Add(new BlockTrait());

    objDb.Add(target);

    return target;
  }

  public static Item Mirror(GameObjectDB objDb, bool left)
  {
    char ch = left ? '\\' : '/';
    Item mirror = new()
    {
      Name = "mirror",
      Type = ItemType.Device,
      Value = 0,
      Glyph = new(ch, Colours.WHITE, Colours.LIGHT_GREY, Colours.BLACK, false)
    };
    mirror.Traits.Add(new BoolTrait() { Name = "Tilt", Value = left });

    objDb.Add(mirror);

    return mirror;
  }

  public static Item Photon(GameState gs, ulong ownerID)
  {
    Item photon = new()
    {
      Name = "photon",
      Type = ItemType.Environment,
      Value = 0,
      Glyph = new(' ', Colours.BLACK, Colours.BLACK, Colours.BLACK, false)
    };
    photon.SetZ(-100);

    photon.Traits.Add(new LightSourceTrait() 
    {
      Radius = 0, OwnerID = ownerID, FgColour = Colours.YELLOW, BgColour = Colours.TORCH_YELLOW
    });

    gs.ObjDb.Add(photon);

    return photon;
  }

  public static Item Fire(GameState gs)
  {
    Glyph glyph;
    var roll = gs.Rng.NextDouble();
    if (roll < 0.333)
      glyph = new Glyph('\u22CF', Colours.BRIGHT_RED, Colours.DULL_RED, Colours.TORCH_ORANGE, false);
    else if (roll < 0.666)
      glyph = new Glyph('\u22CF', Colours.YELLOW, Colours.DULL_RED, Colours.TORCH_RED, false);
    else
      glyph = new Glyph('\u22CF', Colours.YELLOW_ORANGE, Colours.DULL_RED, Colours.TORCH_YELLOW, false);

    Item fire = new()
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
    fire.Traits.Add(new LightSourceTrait() 
    { 
      Radius = 1, 
      OwnerID = fire.ID, 
      FgColour = Colours.YELLOW, 
      BgColour = Colours.TORCH_ORANGE
    });
    
    return fire;
  }

  public static Item Web()
  {
    var web = new Item()
    {
      Name = "webs",
      Type = ItemType.Environment,
      Value = 0,
      Glyph = new Glyph(':', Colours.WHITE, Colours.GREY, Colours.BLACK, false)
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
  readonly List<(char, ulong)> _items = [];
  public char LastSlot { get; set; } = '\0';
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

  bool PlayerInventory() => _objDb.GetObj(OwnerID) is Player;

  public char[] UsedSlots() =>[..  _items.Select(i => i.Item1).Distinct()];

  char[] AvailableSlots()
  {
    var allSlots = Enumerable.Range('a', 26).Select(i => (char)i);
    char[] usedSlots = UsedSlots();
    
    return [.. allSlots.Where(c => !usedSlots.Contains(c))];
  }

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

  public char Add(Item item, ulong ownerID)
  {
    if (item.Type == ItemType.Zorkmid)
    {
      Zorkmids += item.Value;
      return '\0';
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

  public Item? RemoveByID(ulong id)
  {
    for (int j = 0; j < _items.Count; j++)
    {
      if (_items[j].Item2 == id)
      {
        var item = _objDb.GetObj(_items[j].Item2) as Item;
        _items.RemoveAt(j);
        if (!PlayerInventory())
          item!.Slot = '\0';
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
    bool playerInventory = PlayerInventory();
    for (int j = 0; j < totalToRemove; j++)
    {
      int index = indexes[j];
      var item = _objDb.GetObj(_items[index].Item2) as Item;
      if (item is not null)
      {
        if (!playerInventory)
          item.Slot = '\0';
        removed.Add(item);
        _items.RemoveAt(index);
      }
    }
        
    return removed;
  }

  static (EquipingResult, ArmourParts) UnequipItem(Item item)
  {
  if (item.HasTrait<CursedItemTrait>())
  {
    return (EquipingResult.Cursed, ArmourParts.None);
  }
      
    item.Equipped = false;
    return (EquipingResult.Unequipped, ArmourParts.None);
  }

  (EquipingResult, ArmourParts) ToggleWand(Item wand, int freeHands)
  {
    if (wand.Equipped)
    {
      return UnequipItem(wand);
    }
   
    if (freeHands > 0)
    {
      wand.Equipped = true;
      return (EquipingResult.Equipped, ArmourParts.None);
    }
    
    return (EquipingResult.NoFreeHand, ArmourParts.None);
  }

  // This toggles the equip status of gear only and recalculation of stuff
  // like armour class has to be done elsewhere because it felt icky to 
  // have a reference back to the inventory's owner in the inventory object
  public (EquipingResult, ArmourParts) ToggleEquipStatus(char slot)
  {
    bool twoHandedWeapon = ReadiedWeapon() is Item w && w.HasTrait<TwoHandedTrait>();
    bool bowEquiped = ReadiedBow() is not null;

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

      if (item.Equipped)
       return UnequipItem(item);
      
      if (item.Type == ItemType.Weapon || item.Type == ItemType.Tool)
      {
        if (freeHands == 0 && shield)
          return (EquipingResult.NoFreeHand, ArmourParts.None);
        
        // If there is a weapon already equipped, unequip it
        foreach (Item other in Items())
        {
          if ((other.Type == ItemType.Weapon || other.Type == ItemType.Tool) && other.Equipped)
            other.Equipped = false;
        }

        item.Equipped = true;
        
        return (EquipingResult.Equipped, ArmourParts.None);
      }
      else if (item.Type == ItemType.Bow)
      {
        if (ShieldEquipped())
          return (EquipingResult.NoFreeHand, ArmourParts.None);

        foreach (Item other in Items())
        {
          if ((other.Type == ItemType.Bow) && other.Equipped)
            other.Equipped = false;
        }

        item.Equipped = true;
        return (EquipingResult.Equipped, ArmourParts.None);
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

        if (part is ArmourParts.Shield && freeHands == 0)
          return (EquipingResult.NoFreeHand, part);
        
        if (part is ArmourParts.Shield && (twoHandedWeapon|| bowEquiped))
          return (EquipingResult.TwoHandedConflict, part);
        
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

    return (EquipingResult.Conflict, ArmourParts.None);
  }

  public string ApplyEffectToInv(DamageType damageType, GameState gs, Loc loc)
  {
    Actor? owner = (Actor?) gs.ObjDb.GetObj(OwnerID);
    List<string> msgs = [];

    foreach (var item in Items())
    {
      var (s, destroyed) = EffectApplier.Apply(damageType, gs, item, owner);
      if (s != "")
        msgs.Add(s);
      if (destroyed)
      {
        RemoveByID(item.ID);
        gs.ItemDestroyed(item, loc);
      }
    }

    return string.Join(' ', msgs).Trim();
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
    
    RemoveByID(item.ID);
    gs.ObjDb.RemoveItemFromGame(actor.Loc, item);
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
      try 
      {
        if (item.HasTrait<PluralTrait>())
          desc = $"some {item.FullName}";
        else if (count > 1)
          desc = $"{count} {item.FullName.Pluralize()}";
        else if (item.HasTrait<NamedTrait>())
          desc = item.FullName;        
        else
          desc = item.FullName.IndefArticle();
      }
      catch (Exception ex)
      {
        Console.WriteLine(ex.Message);
      }

      if (item.Equipped)
      {
        if (item.HasTrait<CursedItemTrait>())
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
        else if (item.Type == ItemType.Wand)
          desc += " (focus)";
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
      ulong id = ulong.Parse(i[2..]);
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
  Cursed,
  NoFreeHand
}

class EmptyInventory : Inventory 
{
    public EmptyInventory() : base(0, null!) { }

    public override List<Item> Items() => [];
    public override string ToText() => "";
    public override void RestoreFromText(string txt) { }
}