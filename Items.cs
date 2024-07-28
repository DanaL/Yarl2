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
  Bow,
  Armour,
  Zorkmid,
  Tool,
  Document,
  Potion,
  Scroll,
  Trinket,
  Wand,
  Environment // I'm implementing things like mist as 'items'
}

record ItemIDInfo(bool Known, string Desc);
class Item : GameObj, IEquatable<Item>
{
  public static Dictionary<string, ItemIDInfo> IDInfo { get; set; } = [];
  public static readonly int DEFAULT_Z = 2;
  public ItemType Type { get; set; }
  public char Slot { get; set; }
  public bool Equiped { get; set; } = false;
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
    }

    string adjectives = string.Join(", ", adjs);
    string fullname = adjectives.Trim();
    if (bonus > 0)
      fullname += $" +{bonus}";
    else if (bonus < 0)
      fullname += $" {bonus}";
    fullname += " " + name;
    
    string traitDescs = string.Join(' ', Traits.OfType<Trait>().Select(t => t.Desc()));
    if (traitDescs.Length > 0)
      fullname += " " + traitDescs;

    return fullname.Trim();
  }

  public override string FullName => CalcFullName();

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

  public bool ApplyRust()
  {
    Metals metal = IsMetal();
    if (metal == Metals.NotMetal || metal == Metals.Mithril)
      return false;

    RustedTrait? rusted = Traits.OfType<RustedTrait>().FirstOrDefault();
    
    if (rusted == null)
    {
      Traits.Add(new AdjectiveTrait("Rusted"));
      Traits.Add(new RustedTrait() { Amount = Rust.Rusted });
    }
    else if (rusted.Amount == Rust.Rusted)
    {
      // An already rusted item becomes corroded
      Traits = Traits.Where(t => !(t is AdjectiveTrait adj && adj.Adj == "Rusted")).ToList();
      Traits.Add(new AdjectiveTrait("Corroded"));
      rusted.Amount = Rust.Corroded;
    }
    else
    {
      // Right now we have only two degrees of rust: Rusted and Corroded and hence
      // a max penalty of -2 to item bonuses
      return false;
    }

    // Some items have their bonuses lowered by being rusted/corroded
    var armourTrait = Traits.OfType<ArmourTrait>().FirstOrDefault();
    if (armourTrait is not null)
    {
      armourTrait.Bonus -= 1;
    }
    
    if (Type == ItemType.Weapon)
    {
      var wb = Traits.OfType<WeaponBonusTrait>().FirstOrDefault();
      if (wb is null)
        Traits.Add(new WeaponBonusTrait() { Bonus = -1 });
      else       
        wb.Bonus -= 1;
    }

    return true;
  }

  public Metals IsMetal()
  {
    var t = Traits.OfType<MetalTrait>().FirstOrDefault();

    return t is not null ? t.Type : Metals.NotMetal;
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

enum ItemNames
{
  SPEAR, GUISARME, DAGGER, HAND_AXE, BATTLE_AXE, MACE, LONGSWORD, SHORTSHORD, 
  RAPIER, LONGBOW, ARROW, FIREBOLT, LEATHER_ARMOUR, STUDDED_LEATHER_ARMOUR,
  RINGMAIL, CHAINMAIL, HELMET, SHIELD, TORCH, POTION_HEALING,
  POTION_MIND_READING, ANTIDOTE, POTION_FIRE_RES, POTION_COLD_RES,
  SCROLL_BLINK, SCROLL_MAGIC_MAP, WAND_OF_MAGIC_MISSILES,
  WAND_SWAP, WAND_HEAL_MONSTER, WAND_FIREBALLS, WAND_FROST, SCROLL_RECALL,
  ZORKMIDS, ZORKMIDS_PITTANCE, ZORKMIDS_MEDIOCRE, ZORKMIDS_GOOD
}

class ItemFactory
{
  public static Item Get(ItemNames name, GameObjectDB objDB)
  {
    Item item;

    switch (name)
    {
      case ItemNames.SPEAR:
        item = new Item() { Name = "spear", Type = ItemType.Weapon, Value = 10,
          Glyph = new Glyph(')', Colours.LIGHT_BROWN, Colours.BROWN, Colours.BLACK, Colours.BLACK) };
        item.Traits.Add(new DamageTrait() { DamageDie = 6, NumOfDie = 1, DamageType = DamageType.Piercing });
        item.Traits.Add(new PolearmTrait());        
        break;
      case ItemNames.GUISARME:
        item = new Item() { Name = "guisarme", Type = ItemType.Weapon, Value = 15, Glyph = new Glyph(')', Colours.GREY, Colours.DARK_GREY, Colours.BLACK, Colours.BLACK) };
        item.Traits.Add(new DamageTrait() { DamageDie = 8, NumOfDie = 1, DamageType = DamageType.Piercing });
        item.Traits.Add(new PolearmTrait());
        item.Traits.Add(new ReachTrait());
        item.Traits.Add(new TwoHandedTrait());
        break;
      case ItemNames.DAGGER:
        item = new Item() { Name = "dagger", Type = ItemType.Weapon, Value = 10, Glyph = new Glyph(')', Colours.WHITE, Colours.GREY, Colours.BLACK, Colours.BLACK) };
        item.Traits.Add(new DamageTrait() { DamageDie = 4, NumOfDie = 1, DamageType = DamageType.Piercing });
        item.Traits.Add(new StackableTrait());
        item.Traits.Add(new FinesseTrait());
        item.Traits.Add(new MetalTrait() { Type = Metals.Steel });
        break;
      case ItemNames.HAND_AXE:
        item = new Item() { Name = "hand axe", Type = ItemType.Weapon, Value = 15, Glyph = new Glyph(')', Colours.LIGHT_BROWN, Colours.BROWN, Colours.BLACK, Colours.BLACK) };
        item.Traits.Add(new DamageTrait() { DamageDie = 6, NumOfDie = 1, DamageType = DamageType.Slashing });
        item.Traits.Add(new AxeTrait());
        item.Traits.Add(new MetalTrait() { Type = Metals.Iron });
        break;
      case ItemNames.BATTLE_AXE:
        item = new Item()
        {
          Name = "battle axe",
          Type = ItemType.Weapon,
          Value = 25,
          Glyph = new Glyph(')', Colours.LIGHT_BROWN, Colours.BROWN, Colours.BLACK, Colours.BLACK)
        };
        item.Traits.Add(new DamageTrait() { DamageDie = 3, NumOfDie = 3, DamageType = DamageType.Slashing });
        item.Traits.Add(new AxeTrait());
        item.Traits.Add(new MetalTrait() { Type = Metals.Steel });
        break;
      case ItemNames.MACE:
        item = new Item()
        {
          Name = "mace",
          Type = ItemType.Weapon,
          Value = 25,
          Glyph = new Glyph(')', Colours.LIGHT_GREY, Colours.GREY, Colours.BLACK, Colours.BLACK)
        };
        item.Traits.Add(new DamageTrait() { DamageDie = 4, NumOfDie = 2, DamageType = DamageType.Blunt });
        item.Traits.Add(new MetalTrait() { Type = Metals.Iron });
        break;
      case ItemNames.LONGSWORD:
        item = new Item() { Name = "longsword", Type = ItemType.Weapon, Value = 25, Glyph = new Glyph(')', Colours.WHITE, Colours.LIGHT_GREY, Colours.BLACK, Colours.BLACK) };
        item.Traits.Add(new DamageTrait() { DamageDie = 8, NumOfDie = 1, DamageType = DamageType.Slashing });
        item.Traits.Add(new SwordTrait());
        item.Traits.Add(new MetalTrait() { Type = Metals.Steel });
        break;
      case ItemNames.SHORTSHORD:
        item = new Item() { Name = "shortsword", Type = ItemType.Weapon, Value = 15, Glyph = new Glyph(')', Colours.WHITE, Colours.LIGHT_GREY, Colours.BLACK, Colours.BLACK) };
        item.Traits.Add(new DamageTrait() { DamageDie = 6, NumOfDie = 1, DamageType = DamageType.Slashing });
        item.Traits.Add(new SwordTrait());
        item.Traits.Add(new MetalTrait() { Type = Metals.Steel });
        break;
      case ItemNames.RAPIER:
        item = new Item() { Name = "rapier", Type = ItemType.Weapon, Value = 20, Glyph = new Glyph(')', Colours.WHITE, Colours.LIGHT_GREY, Colours.BLACK, Colours.BLACK) };
        item.Traits.Add(new DamageTrait() { DamageDie = 8, NumOfDie = 1, DamageType = DamageType.Piercing });
        item.Traits.Add(new FinesseTrait());
        item.Traits.Add(new StabbyTrait());
        item.Traits.Add(new MetalTrait() { Type = Metals.Steel });
        break;
      case ItemNames.LONGBOW:
        item = new Item()
        {
          Name = "longbow",
          Type = ItemType.Bow,
          Value = 30,
          Glyph = new Glyph(')', Colours.LIGHT_BROWN, Colours.BROWN, Colours.BLACK, Colours.BLACK)
        };
        // This is for if the player happens to be wielding the bow for some reason
        item.Traits.Add(new DamageTrait() { DamageDie = 1, NumOfDie = 1, DamageType = DamageType.Blunt });
        item.Traits.Add(new AmmoTrait() { DamageDie = 4, NumOfDie = 1, DamageType = DamageType.Piercing, Range = 9 });
        break;
      case ItemNames.ARROW:
        item = new Item()
        {
          Name = "arrow",
          Type = ItemType.Weapon,
          Value = 2,
          Glyph = new Glyph('-', Colours.LIGHT_BROWN, Colours.BROWN, Colours.BLACK, Colours.BLACK)
        };
        item.Traits.Add(new DamageTrait() { DamageDie = 6, NumOfDie = 1, DamageType = DamageType.Piercing });
        item.Traits.Add(new StackableTrait());
        break;
      case ItemNames.FIREBOLT:
        item = new Item()
        {
          Name = "firebolt",
          Type = ItemType.Weapon,
          Value = 0,
          Glyph = new Glyph('-', Colours.YELLOW, Colours.YELLOW_ORANGE, Colours.BLACK, Colours.BLACK)
        };
        item.Traits.Add(new DamageTrait() { DamageDie = 5, NumOfDie = 2, DamageType = DamageType.Fire });
        break;
      case ItemNames.LEATHER_ARMOUR:
        item = new Item() { Name = "leather armour", Type = ItemType.Armour, Value = 20, Glyph = new Glyph('[', Colours.BROWN, Colours.LIGHT_BROWN, Colours.BLACK, Colours.BLACK) };
        item.Traits.Add(new ArmourTrait() { Part = ArmourParts.Shirt, ArmourMod = 1, Bonus = 0 });
        break;
      case ItemNames.STUDDED_LEATHER_ARMOUR:
        item = new Item()
        {
          Name = "studded leather armour",
          Type = ItemType.Armour,
          Value = 25,
          Glyph = new Glyph('[', Colours.BROWN, Colours.LIGHT_BROWN, Colours.BLACK, Colours.BLACK)
        };
        item.Traits.Add(new ArmourTrait() { Part = ArmourParts.Shirt, ArmourMod = 2, Bonus = 0 });
        break;
      case ItemNames.RINGMAIL:
        item = new Item()
        {
          Name = "ringmail",
          Type = ItemType.Armour,
          Value = 45,
          Glyph = new Glyph('[', Colours.LIGHT_GREY, Colours.GREY, Colours.BLACK, Colours.BLACK)
        };
        item.Traits.Add(new ArmourTrait() { Part = ArmourParts.Shirt, ArmourMod = 3, Bonus = 0 });
        item.Traits.Add(new MetalTrait() { Type = Metals.Iron });
        break;
      case ItemNames.CHAINMAIL:
        item = new Item()
        {
          Name = "chainmail",
          Type = ItemType.Armour,
          Value = 75,
          Glyph = new Glyph('[', Colours.LIGHT_GREY, Colours.GREY, Colours.BLACK, Colours.BLACK)
        };
        item.Traits.Add(new ArmourTrait() { Part = ArmourParts.Shirt, ArmourMod = 4, Bonus = 0 });
        item.Traits.Add(new MetalTrait() { Type = Metals.Iron });
        break;
      case ItemNames.HELMET:
        item = new Item() { Name = "melmet", Type = ItemType.Armour, Value = 20, Glyph = new Glyph('[', Colours.WHITE, Colours.GREY, Colours.BLACK, Colours.BLACK) };
        item.Traits.Add(new ArmourTrait() { Part = ArmourParts.Hat, ArmourMod = 1, Bonus = 0 });
        item.Traits.Add(new MetalTrait() { Type = Metals.Iron });
        break;
      case ItemNames.SHIELD:
        item = new Item() { Name = "shield", Type = ItemType.Armour, Value = 20, Glyph = new Glyph('[', Colours.GREY, Colours.DARK_GREY, Colours.BLACK, Colours.BLACK) };
        item.Traits.Add(new ArmourTrait() { Part = ArmourParts.Shield, ArmourMod = 1, Bonus = 0 });
        break;
      case ItemNames.TORCH:
        item = new Item()
        {
          Name = "torch",
          Type = ItemType.Tool,
          Value = 2,
          Glyph = new Glyph('(', Colours.LIGHT_BROWN, Colours.BROWN, Colours.BLACK, Colours.BLACK)
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
      case ItemNames.ZORKMIDS:
        item = new Item()
        {
          Name = "zorkmid",
          Type = ItemType.Zorkmid,
          Glyph = new Glyph('$', Colours.YELLOW, Colours.YELLOW_ORANGE, Colours.BLACK, Colours.BLACK)
        };
        item.Traits.Add(new StackableTrait());
        break;        
      case ItemNames.POTION_HEALING:
        item = new Item()
        {
          Name = "potion of healing",
          Type = ItemType.Potion,
          Value = 75,
          Glyph = new Glyph('!', Colours.LIGHT_BLUE, Colours.BLUE, Colours.BLACK, Colours.BLACK)        
        };
        item.Traits.Add(new UseSimpleTrait("minorheal"));
        item.Traits.Add(new ConsumableTrait());
        item.Traits.Add(new StackableTrait());
        break;
      case ItemNames.POTION_MIND_READING:
        item = new Item()
        {
          Name = "potion of mind reading",
          Type = ItemType.Potion,
          Value = 125,
          Glyph = new Glyph('!', Colours.LIGHT_BLUE, Colours.BLUE, Colours.BLACK, Colours.BLACK)
        };
        item.Traits.Add(new UseSimpleTrait("telepathy"));
        item.Traits.Add(new ConsumableTrait());
        item.Traits.Add(new StackableTrait());
        break;
      case ItemNames.POTION_FIRE_RES:
        item = new Item()
        {
          Name = "potion of fire resistance",
          Type = ItemType.Potion,
          Value = 100,
          Glyph = new Glyph('!', Colours.LIGHT_BLUE, Colours.BLUE, Colours.BLACK, Colours.BLACK)
        };
        item.Traits.Add(new UseSimpleTrait("resistfire"));
        item.Traits.Add(new ConsumableTrait());
        item.Traits.Add(new StackableTrait());
        break;
      case ItemNames.POTION_COLD_RES:
        item = new Item()
        {
          Name = "potion of cold resistance",
          Type = ItemType.Potion,
          Value = 100,
          Glyph = new Glyph('!', Colours.LIGHT_BLUE, Colours.BLUE, Colours.BLACK, Colours.BLACK)
        };
        item.Traits.Add(new UseSimpleTrait("resistcold"));
        item.Traits.Add(new ConsumableTrait());
        item.Traits.Add(new StackableTrait());
        break;
      case ItemNames.ANTIDOTE:
        item = new Item()
        {
          Name = "antidote",
          Type = ItemType.Potion,
          Value = 50,
          Glyph = new Glyph('!', Colours.YELLOW, Colours.YELLOW_ORANGE, Colours.BLACK, Colours.BLACK)
        };
        item.Traits.Add(new UseSimpleTrait("antidote"));
        item.Traits.Add(new ConsumableTrait());
        item.Traits.Add(new StackableTrait());
        break;
      case ItemNames.SCROLL_BLINK:
        item = new Item()
        {
          Name = "scroll of blink",
          Type = ItemType.Scroll,
          Value = 125,
          Glyph = new Glyph('?', Colours.WHITE, Colours.GREY, Colours.BLACK, Colours.BLACK)
        };
        item.Traits.Add(new ConsumableTrait());
        item.Traits.Add(new UseSimpleTrait("blink"));
        item.Traits.Add(new FlammableTrait());
        item.Traits.Add(new WrittenTrait());
        item.Traits.Add(new StackableTrait());
        break;
      case ItemNames.SCROLL_MAGIC_MAP:
        item = new Item()
        {
          Name = "scroll of magic mapping",
          Type = ItemType.Scroll,
          Value = 100,
          Glyph = new Glyph('?', Colours.WHITE, Colours.GREY, Colours.BLACK, Colours.BLACK)
        };
        item.Traits.Add(new ConsumableTrait());
        item.Traits.Add(new UseSimpleTrait("magicmap"));
        item.Traits.Add(new FlammableTrait());
        item.Traits.Add(new WrittenTrait());
        item.Traits.Add(new StackableTrait());
        break;
      case ItemNames.SCROLL_RECALL:
        item = new Item() { Name = "scroll of word of recall", Type = ItemType.Scroll, Value = 100,
          Glyph = new Glyph('?', Colours.WHITE, Colours.GREY, Colours.BLACK, Colours.BLACK) };
        item.Traits.Add(new ConsumableTrait());
        item.Traits.Add(new UseSimpleTrait("recall"));
        item.Traits.Add(new FlammableTrait());
        item.Traits.Add(new WrittenTrait());
        item.Traits.Add(new StackableTrait());
        break;
      // Probably later I'll randomize how many charges wands have?
      case ItemNames.WAND_OF_MAGIC_MISSILES:
        item = new Item() { Name = "wand of magic missiles", Type = ItemType.Wand, Value = 150, Glyph = new Glyph('/', Colours.LIGHT_BLUE, Colours.BLUE, Colours.BLACK, Colours.BLACK) };
        item.Traits.Add(new WandTrait() 
        {
          Charges = 20, 
          Effect = "magicmissile",
          IDed = false
        });
        break;
      case ItemNames.WAND_SWAP:
        item = new Item() { Name = "wand of swap", Type = ItemType.Wand, Value = 175, Glyph = GlyphForWand("wand of swap") };
        item.Traits.Add(new WandTrait() { Charges = 15, Effect = "swap", IDed = false });
        break;
      case ItemNames.WAND_HEAL_MONSTER:
        item = new Item() { Name = "wand of heal monster", Type = ItemType.Wand, Value = 25, Glyph = GlyphForWand("wand of heal monster") };
        item.Traits.Add(new WandTrait() { Charges = 35, Effect = "healmonster", IDed = false });
        break;
      case ItemNames.WAND_FIREBALLS:
        item = new Item() { Name = "wand of fireballs", Type = ItemType.Wand, Value = 125, Glyph = GlyphForWand("wand of fireballs") };
        item.Traits.Add(new WandTrait() { Charges = 10, Effect = "fireball", IDed = false });
        break;
      case ItemNames.WAND_FROST:
        item = new Item() { Name = "wand of frost", Type = ItemType.Wand, Value = 125, Glyph = GlyphForWand("wand of frost") };
        item.Traits.Add(new WandTrait() { Charges = 10, Effect = "frost", IDed = false });
        break;
      default:
        throw new Exception($"{name} doesn't seem exist in yarl2 :(");
    }

    objDB.Add(item);

    return item;
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
  Shield
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

  public Item? ReadiedWeapon()
  {
    foreach (var item in Items())
    {
      if ((item.Type == ItemType.Weapon || item.Type == ItemType.Tool) && item.Equiped)
        return item;
    }

    return null;
  }

  public Item? ReadiedBow() 
  {
    foreach (var bow in Items().Where(i => i.Type == ItemType.Bow && i.Equiped))
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
        if (other.Type == item.Type && other.Name == item.Name)
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
    bool EquipedShield()
    {
      foreach (var item in Items().Where(i => i.Type == ItemType.Armour && i.Equiped))
      {
        if (item.Traits.OfType<ArmourTrait>().FirstOrDefault() is ArmourTrait at && at.Part == ArmourParts.Shield)
          return true;
      }

      return false;
    }

    bool EquipedTwoHandedWeapon() => ReadiedWeapon() is Item w && w.HasTrait<TwoHandedTrait>();

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
      if (item.Type == ItemType.Weapon || item.Type == ItemType.Tool || item.Type == ItemType.Bow)
      {
        if (item.HasTrait<TwoHandedTrait>() && EquipedShield())
        {
          return (EquipingResult.ShieldConflict, ArmourParts.Shield);
        }

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
        foreach (ArmourTrait t in item.Traits.OfType<ArmourTrait>())
        {          
          part = t.Part;
          break;          
        }

        // check to see if there's another piece in that slot
        foreach (var other in Items().Where(a => a.Type == ItemType.Armour && a.Equiped))
        {
          foreach (var t in other.Traits.OfType<ArmourTrait>())
          {
            if (t.Part == part)
              return (EquipingResult.Conflict, part);
          }
        }

        if (part is ArmourParts.Shield && EquipedTwoHandedWeapon())
        {
          return (EquipingResult.TwoHandedConflict, part);
        }

        item.Equiped = !item.Equiped;

        return (EquipingResult.Equiped, ArmourParts.Shirt);
      }
    }

    return (EquipingResult.Conflict, ArmourParts.Shirt);
  }

  public string ApplyEffect(TerrainFlag effect, GameState gs, Loc loc)
  {
    List<string> msgs = [];

    foreach (var item in Items())
    {
      string m = item.ApplyEffect(effect, gs, loc);
      msgs.Add(m);
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
  public override List<Item> Items() => [];
  public override string ToText() => "";
  public override void RestoreFromText(string txt) { }
}

enum EquipingResult
{
  Equiped,
  Unequiped,
  Conflict,
  ShieldConflict,
  TwoHandedConflict
}