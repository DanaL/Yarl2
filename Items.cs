// Delve - A roguelike computer RPG
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
  Weapon, Bow, Armour, Zorkmid, Tool, Document, Potion, Scroll, Trinket, Wand,
  Ring, Bone, Food, Talisman, Reagent, Environment, Fog, Landscape, Statue,
  Illusion, Device, Altar, Column, Ink, Arrow, Component
}

record ItemIDInfo(bool Known, string Desc);
sealed class Item : GameObj, IEquatable<Item>
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
  public override int Z() => _z;

  public bool Equipable() => HasTrait<EquipableTrait>();

  public bool IsUseableTool() =>
  Traits.Any(t => t is DiggingToolTrait or WoodChopperTrait or DoorKeyTrait
                    or CleansingTrait or ExplosiveTrait or EmergencyDoorTrait);

  public bool IsUseableItem()
  {
    if (IsUseableTool())
      return true;

    foreach (Trait t in Traits)
    {
      if (t is TorchTrait)
        return true;
      else if (t is ReadableTrait || t is ScrollTrait || t is BookTrait)
        return true;
      else if (t is IUSeable)
        return true;
      else if (t is CanApplyTrait)
        return true;
      else if (t is VaultKeyTrait)
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

    if (Type == ItemType.Zorkmid)
    {
      return Value > 1 ? $"{Value} zorkmids" : "1 zorkmid";
    }

    List<string> adjs = [];
    int bonus = 0;
    bool isBook = false;
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
      else if (trait is BookTrait)
        isBook = true;
    }

    string adjectives = string.Join(", ", adjs);
    string fullname = adjectives.Trim();
    if (bonus > 0)
      fullname += $" +{bonus}";
    else if (bonus < 0)
      fullname += $" {bonus}";

    if (isBook)
      fullname += " copy of";

    fullname += " " + name;

    var traitDescs = Traits.Where(t => t is IDesc id)
                           .Select(t => ((IDesc)t).Desc());
    if (traitDescs.Any())
      fullname += " " + string.Join(' ', traitDescs);

    return fullname.Trim();
  }

  public override string FullName => CalcFullName();

  public void ToggleGrantedTraits(Actor owner, GameState gs, EquipingResult equipped)
  {
    if (Traits.OfType<GrantsTrait>().FirstOrDefault() is not GrantsTrait grants)
    {
      return;
    }

    if (IDInfo.TryGetValue(Name, out ItemIDInfo? value))
        IDInfo[Name] = value with { Known = true };

    if (equipped == EquipingResult.Equipped)
    {
      foreach (string msg in grants.Grant(owner, gs, this))
        gs.UIRef().AlertPlayer(msg);
    }
    else if (equipped == EquipingResult.Unequipped)
    {
      grants.Remove(owner, gs, this);
    }
  }

  public Metals MetalType()
  {
    var t = Traits.OfType<MetalTrait>().FirstOrDefault();

    return t is not null ? t.Type : Metals.NotMetal;
  }

  public bool CanCorrode()
  {
    if (HasTrait<RustProofTrait>())
      return false;

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

  public static bool operator ==(Item? a, Item? b)
  {
    if (a is null)
    {
      if (b is null)
        return true;
      return false;
    }

    return a.Equals(b);
  }

  public static bool operator !=(Item? a, Item? b) => !(a == b);
}

// I did this to prevent myself from typo-ing item names in code, but I didn't
// really think about how many items there would eventually be...
enum ItemNames
{
  ALCHEMICAL_COMPOUND, ANTIDOTE, ANTISNAIL_SANDALS, APPLE, ARROW, ARROW_BANISHMENT, BATTLE_AXE, BEETLE_CARAPACE, BLACK_PEARL, 
  BLINDFOLD, BOMB, BONE,  BOOTS_OF_WATER_WALKING, CAMPFIRE, CHAINMAIL, CLAYMORE, CLOAK_OF_PROTECTION, COLUMN, CRIMSON_KING_WARD, 
  CROESUS_CHARM, CUTPURSE_CREST, DAGGER, DART, EMERGENCY_DOOR, FEARFUL_RUNE, FEATHERFALL_BOOTS, FIRE_GIANT_ESSENCE, FIREBOLT, 
  FIREBURST_ARROW, FLASK_OF_BOOZE, FROST_GIANT_ESSENCE, GARLIC, GASTON_BADGE, GAUNTLETS_OF_POWER, GENERIC_WAND, GHOSTCAP_MUSHROOM, 
  GINGERBREAD_MAN, GINSENG, GLOVES_OF_ARCHERY, GNOME_GNOISEMAKER, GOLDEN_APPLE, GREATSWORD, GUIDE_STABBY, GUIDE_AXES, GUIDE_BOWS, 
  GUIDE_SWORDS, GUISARME, HALFLING_CUPCAKE, HAND_AXE, HEARTY_SOUP, HEAVY_BOOTS, HELMET, HILL_GIANT_ESSENCE, HOLY_WATER, 
  KNOCKBACK_ARROW, LEATHER_ARMOUR, LEATHER_BOOTS, LEATHER_GLOVES, LEMBAS, LESSER_BURLY_CHARM, LESSER_GRACE_CHARM, 
  LESSER_HEALTH_CHARM, LOCK_PICK, LONGBOW, LONGSWORD, MACE, MEDITATION_CRYSTAL, MITHRIL_ORE, MOON_LYRE, MOON_MANTLE, 
  MUSHROOM_STEW, OGRE_LIVER, PHALANX_PHOLIO_2, PICKAXE, POTION_BLINDNESS, POTION_CLARITY, POTION_COLD_RES, POTION_DESCENT, 
  POTION_DRAGON_BREATH, POTION_FIRE_RES, POTION_FORGETFULNESS, POTION_HEALING, POTION_HEROISM, POTION_MIND_READING, 
  POTION_OF_LEVITATION, POTION_OBSCURITY, QUARTERSTAFF, RAPIER, RING_OF_ADORNMENT, RING_OF_PROTECTION, RING_OF_WATER_BREATHING, 
  RINGMAIL, RUBBLE, SCROLL_BLINK, RUNE_OF_LASHING, RUNE_OF_PARRYING, SCROLL_ENCHANTING, SCROLL_ESCAPE, SCROLL_DISARM, SCROLL_KNOCK, 
  SCROLL_MAGIC_MAP, SCROLL_PROTECTION, SCROLL_SCATTERING, SCROLL_STAINLESS, SCROLL_TRAP_DETECTION, SCROLL_TREASURE_DETECTION, 
  SEEWEED, SHIELD, SHORTBOW, SHORTSHORD, SILVER_ARROW, SILVER_DAGGER, SILVER_LONGSWORD, SKELETON_KEY, SKULL, SMOULDERING_CHARM, 
  SNEAKERS, SNOWBURST_ARROW, SPEAR, SPIDER_ARROW, SPIDER_SILK, STATUE, STONE_ALTAR, STUDDED_LEATHER_ARMOUR, SULPHUROUS_ASH, 
  TALISMAN_OF_CIRCUMSPECTION, TINCTURE_CELERITY, TORCH, TROLL_BROOCH, VIAL_OF_POISON, VIAL_SPRITE_BLOOD, WAND_DIGGING, WAND_FIREBALLS, 
  WAND_FROST, WAND_HEAL_MONSTER, WAND_MAGIC_MISSILES, WAND_SLEEP, WAND_SLOW_MONSTER, WAND_SUMMONING, WAND_SWAP, WIND_FAN, WOOL_CLOAK, 
  YENDORIAN_SODA, ZORKMIDS, ZORKMIDS_GOOD, ZORKMIDS_MEDIOCRE, ZORKMIDS_PITTANCE,

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

sealed class ItemTemplate
{
  public string Name { get; set; } = "";
  public ItemType Type { get; set; }
  public int Value { get; set; }
  public Glyph Glyph { get; set; }
  public List<string> TraitTemplates { get; set; } = [];
}

sealed class ItemFactory
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
      ItemTemplate template = new()
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

  public static (string, Glyph) MimicDetails(Rng rng)
  {
    ItemTemplate template = Items.Values.ElementAt(rng.Next(Items.Count));
    return (template.Name, template.Glyph);
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

      if (Item.IDInfo.TryGetValue(item.Name, out var idInfo))
      {
        string desc = idInfo.Desc;
        if (desc.StartsWith("gold ") || desc.StartsWith("golden "))
          item.Traits.Add(new MetalTrait() { SourceId = item.ID, Type = Metals.Gold });
        else if (desc.StartsWith("iron "))
          item.Traits.Add(new MetalTrait() { SourceId = item.ID, Type = Metals.Iron });
      }

      foreach (Trait t in item.Traits)
      {
        if (t is IGameEventListener listener && t.Active)
        {
          objDB.EndOfRoundListeners.Add(listener);
        }
      }

      // I defined the Equipable trait after my items.json file was gigantic
      // and I was too lazy to go back and add the trait to all the appropriate
      // item definitions...
      switch (item.Type)
      {
        case ItemType.Armour:
        case ItemType.Weapon:
        case ItemType.Bow:
        case ItemType.Ring:
        case ItemType.Talisman:
        case ItemType.Wand:
          item.Traits.Add(new EquipableTrait());
          break;
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
      "golden wand" => new Glyph('/', Colours.YELLOW, Colours.YELLOW_ORANGE, Colours.BLACK, false),
      "maple wand" => new Glyph('/', Colours.BROWN, Colours.DULL_RED, Colours.BLACK, false),
      "oak wand" => new Glyph('/', Colours.BROWN, Colours.DULL_RED, Colours.BLACK, false),
      "pine wand" => new Glyph('/', Colours.BEIGE, Colours.BROWN, Colours.BLACK, false),
      "birch wand" => new Glyph('/', Colours.LIGHT_BROWN, Colours.BROWN, Colours.BLACK, false),
      "granite wand" => new Glyph('/', Colours.GREY, Colours.DARK_GREY, Colours.BLACK, false),
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

  public static Item YellowMold()
  {
    Item mold = new()
    {
      Name = "patch of yellow mold",
      Type = ItemType.Environment,
      Value = 0,
      Glyph = new Glyph('%', Colours.YELLOW, Colours.YELLOW_ORANGE, Colours.BLACK, false)
    };
    mold.Traits.Add(new AffixedTrait());
    mold.Traits.Add(new FlammableTrait());
    mold.Traits.Add(new MoldSporesTrait());

    return mold;
  }

  public static Item Ink(GameState gs)
  {
    Item ink = new()
    {
      Name = "ink", Type = ItemType.Ink, Value = 0,
      Glyph = new Glyph('░', Colours.BLACK, Colours.BLACK, Colours.BLACK, false)
    };
    ink.SetZ(15);
    ink.Traits.Add(new OpaqueTrait() { Visibility = 0 });
    ink.Traits.Add(new CountdownTrait() { OwnerID = ink.ID, ExpiresOn = gs.Turn + 5 });

    return ink;
  }

  public static Item Fog(GameState gs)
  {
    Item mist = new()
    {
      Name = "fog",
      Type = ItemType.Fog,
      Value = 0,
      Glyph = new Glyph('*', Colours.GREY, Colours.GREY, Colours.DARK_GREY, false)
    };
    mist.SetZ(3);
    mist.Traits.Add(new OpaqueTrait() { Visibility = 0 });
    mist.Traits.Add(new CountdownTrait()
    {
      OwnerID = mist.ID,
      ExpiresOn = gs.Turn + 7
    });

    return mist;
  }

  // An item for when I want a light source on the map that I don't want the
  // player to be able to interact with.
  public static Item VirtualLight(Colour fg, Colour bg, GameState gs)
  {
    Item light = new()
    {
      Name = "light",
      Type = ItemType.Environment,
      Value = 0,
      Glyph = new(' ', Colours.BLACK, Colours.BLACK, Colours.BLACK, false)
    };
    light.SetZ(-100);

    light.Traits.Add(new LightSourceTrait()
    {
      Radius = 1,
      OwnerID = light.ID,
      FgColour = fg,
      BgColour = bg
    });

    gs.ObjDb.Add(light);

    return light;
  }

  public static void CreateTimedLight(int radius, int start, int end, Colour fg, Colour bg, GameObjectDB objDb, Loc loc)
  {
    Item light = new()
    {
      Name = "light", Type = ItemType.Environment, Value = 0,
      Glyph = new(' ', Colours.BLACK, Colours.BLACK, Colours.BLACK, false)
    };
    light.SetZ(-100);

    TimedLightTrait  tl = new()
    {
      OwnerID = light.ID, Radius = radius, Start = start, End = end,
      FgColour = fg, BgColour = bg
    };
    objDb.RegisterListener(GameEventType.EndOfRound, tl);
    light.Traits.Add(tl);

    objDb.Add(light);
    objDb.SetToLoc(loc, light);
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
      Type = ItemType.Statue,
      Value = 0,
      Glyph = new('☆', Colours.YELLOW, Colours.YELLOW_ORANGE, Colours.DARK_GREY, false)
    };
    target.Traits.Add(new AffixedTrait());
    target.Traits.Add(new BlockTrait());
    target.Traits.Add(new DescriptionTrait("A massive basalt block, ice-cold to the touch."));

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

  public static Item Darkness()
  {
    Item darkness = new()
    {
      Name = "darkness", Type = ItemType.Environment,
      Value = 0, Glyph = new(' ', Colours.BLACK, Colours.BLACK, Colours.BLACK, false)
    };

    darkness.Traits.Add(new AffixedTrait());
    darkness.Traits.Add(new OpaqueTrait());
    darkness.Traits.Add(new PluralTrait());

    return darkness;
  }

  public static Item MoonDaughterTile()
  {
    Item tile = new()
    {
      Name = "moon daughter tile", Type = ItemType.Environment,
      Value = 0, Glyph = new(' ', Colours.BLACK, Colours.BLACK, Colours.BLACK, false)
    };
    tile.SetZ(-100);

    tile.Traits.Add(new AffixedTrait());
    tile.Traits.Add(new LightSourceTrait()
    {
      Radius = 0,
      OwnerID = tile.ID,
      FgColour = Colours.WHITE,
      BgColour = Colours.WHITE
    });

    return tile;
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
      Radius = 0,
      OwnerID = ownerID,
      FgColour = Colours.YELLOW,
      BgColour = Colours.TORCH_YELLOW
    });

    gs.ObjDb.Add(photon);

    return photon;
  }

  public static Item Fire(GameState gs)
  {
    Item fire = new()
    {
      Name = "fire",
      Type = ItemType.Environment,
      Value = 0,
      Glyph = Util.FlameGlyph(gs.Rng),
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
    Item web = new()
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

  public static Item Chasm(GameState gs)
  {
    Item chasm = new()
    {
      Name = "chasm",
      Type = ItemType.Environment,
      Value = 0,
      Glyph = new Glyph('∷', Colours.FAR_BELOW, Colours.FAR_BELOW, Colours.BLACK, false)
    };
    chasm.SetZ(1);

    gs.ObjDb.Add(chasm);

    chasm.Traits.Add(new AffixedTrait());

    TemporaryChasmTrait tc = new();
    tc.Apply(chasm, gs);

    return chasm;
  }

  public static Item PuddleOfBooze()
  {
    Item booze = new()
    {
      Name = "puddle of booze",
      Type = ItemType.Environment,
      Value = 0,
      Glyph = new Glyph('~', Colours.WHITE, Colours.GREY, Colours.LIGHT_BROWN, false)
    };
    booze.SetZ(1);
    booze.Traits.Add(new FlammableTrait());

    return booze;
  }

  public static Item PuddleOfMud()
  {
    Item mud = new()
    {
      Name = "mud",
      Type = ItemType.Environment,
      Value = 0,
      Glyph = new Glyph('.', Colours.WHITE, Colours.GREY, Colours.LIGHT_BROWN, false)
    };
    mud.SetZ(1);

    return mud;
  }

  public static Item PuddleOfGrease()
  {
    Item grease = new()
    {
      Name = "grease",
      Type = ItemType.Environment,
      Value = 0,
      Glyph = new Glyph('.', Colours.DARK_GREY, Colours.BLACK, Colours.CREAM, false)
    };
    grease.SetZ(1);
    grease.Traits.Add(new FlammableTrait());

    return grease;
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

enum EquipingResult
{
  Equipped,
  Unequipped,
  Conflict,
  ShieldConflict,
  TwoHandedConflict,
  BowConflict,
  TooManyRings,
  TooManyTalismans,
  NoFreeHand,
  StackEquipped,
  StackUnequipped
}

class EmptyInventory : Inventory
{
  public EmptyInventory() : base(0, null!) { }

  public override List<Item> Items() => [];
  public override string ToText() => "";
  public override void RestoreFromText(string txt) { }
}
