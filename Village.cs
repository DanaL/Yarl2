
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

// Code for populating the town with NPCs and some decorations

class Village
{
  static Loc RandomOutdoorLoc(Map map, Town town, Random rng)
  {
    List<(int, int)> sqs = [];
    for (int r = town.Row; r < town.Row + town.Height; r++)
    {
      for (int c = town.Col; c < town.Col + town.Width; c++)
      {
        switch (map.TileAt(r, c).Type)
        {
          case TileType.Grass:
          case TileType.GreenTree:
          case TileType.RedTree:
          case TileType.YellowTree:
          case TileType.OrangeTree:
          case TileType.Sand:
          case TileType.Dirt:
            sqs.Add((r, c));
            break;
        }
      }
    }

    var sq = sqs[rng.Next(sqs.Count)];

    return new Loc(0, 0, sq.Item1, sq.Item2);
  }

  static (Colour, Colour) VillagerColour(Random rng)
  {
    var roll = rng.Next(8);
    return roll switch
    {
      0 => (Colours.WHITE, Colours.LIGHT_GREY),
      1 => (Colours.YELLOW, Colours.YELLOW_ORANGE),
      2 => (Colours.YELLOW_ORANGE, Colours.TORCH_YELLOW),
      3 => (Colours.BRIGHT_RED, Colours.DULL_RED),
      4 => (Colours.GREEN, Colours.DARK_GREEN),
      5 => (Colours.LIGHT_BLUE, Colours.BLUE),
      6 => (Colours.PINK, Colours.DULL_RED),
      _ => (Colours.LIGHT_BROWN, Colours.BROWN)
    };
  }

  static string VillagerAppearance(Random rng)
  {
    string[] species = ["human", "elf", "half-elf", "gnome", "dwarf", "orc", "half-orc"];
    string[] eyes = ["bright", "deep", "dark", "sad", "distant", "piercing", "clear", "clouded", "watery", "cold", "twinkling"];
    string[] hair = ["bald", "long", "short", "frizzy", "stylish", "unkempt", "messy", "fashionable", "thinning",
      "perfurmed", "unwashed", "tousled", "curly", "wavy"];

    int r = rng.Next(3);
    var appearance = new StringBuilder();
    if (r == 0)
      appearance.Append("tall ");
    else if (r == 1)
      appearance.Append("short ");
    appearance.Append(species[rng.Next(species.Length)]);

    string villagerHair = hair[rng.Next(hair.Length)];
    if (villagerHair == "bald")
    {
      appearance.Append(", bald, with ");
    }
    else
    {
      appearance.Append(" with ");
      appearance.Append(villagerHair);
      appearance.Append(" hair, and ");
    }

    appearance.Append(eyes[rng.Next(eyes.Length)]);
    appearance.Append(" eyes");

    return appearance.ToString();
  }

  static Mob BaseVillager(NameGenerator ng, Random rng)
  {
    var (lit, unlit) = VillagerColour(rng);
    Mob mob = new()
    {
      Name = ng.GenerateName(rng.Next(5, 9)),
      Appearance = VillagerAppearance(rng),
      Glyph = new Glyph('@', lit, unlit, Colours.BLACK, Colours.BLACK)
    };
    mob.Traits.Add(new VillagerTrait());
    mob.Traits.Add(new NamedTrait());

    return mob;
  }

  // Pick a location inside a building (based on it being a floor screen) 
  // which isn't adjacent to the door
  static Loc LocForVillager(Map map, HashSet<Loc> sqs, Random rng)
  {
    List<Loc> locs = [];

    foreach (var sq in sqs)
    {
      Tile tile = map.TileAt(sq.Row, sq.Col);
      if (!(tile.Type == TileType.StoneFloor || tile.Type == TileType.WoodFloor))
        continue;

      bool adjToDoor = false;
      foreach (var (ar, ac) in Util.Adj4Sqs(sq.Row, sq.Col))
      {
        TileType adjTile = map.TileAt(ar, ac).Type;
        if (adjTile == TileType.ClosedDoor || adjTile == TileType.OpenDoor || adjTile == TileType.LockedDoor)
        {
          adjToDoor = true;
          break;
        }
      }

      if (!adjToDoor)
        locs.Add(sq);
    }

    return locs[rng.Next(locs.Count)];
  }

  static Mob GenerateInnkeeper(Map map, Town town, NameGenerator ng, GameObjectDB objDb, Random rng)
  {
    Mob innkeeper = BaseVillager(ng, rng);
    innkeeper.MoveStrategy = new WallMoveStrategy();
    innkeeper.SetBehaviour(new InnkeeperBehaviour());
    
    var tavernSqs = town.Tavern.ToList();
    do
    {
      Loc loc = tavernSqs[rng.Next(tavernSqs.Count)];
      var tile = map.TileAt(loc.Row, loc.Col).Type;
      if ((tile == TileType.WoodFloor || tile == TileType.StoneFloor) && !objDb.Occupied(loc))
      {
        innkeeper.Loc = loc;
        break;
      }
    }
    while (true);

    return innkeeper;
  }

  static Mob GeneratePriest(Map map, Town town, NameGenerator ng, GameObjectDB objDb, Random rng)
  {
    Mob cleric = BaseVillager(ng, rng);
    cleric.Traits.Add(new DialogueScriptTrait() { ScriptFile = "priest.txt" });

    cleric.Loc = LocForVillager(map, town.Shrine, rng);
    cleric.SetBehaviour(new PriestBehaviour());
    cleric.MoveStrategy = new WallMoveStrategy();

    int minR = int.MaxValue, maxR = 0, minC = int.MaxValue, maxC = 0;
    foreach (Loc loc in town.Shrine)
    {
      if (loc.Row < minR) minR = loc.Row;
      if (loc.Row > maxR) maxR = loc.Row;
      if (loc.Col < minC) minC = loc.Col;
      if (loc.Col > maxC) maxC = loc.Col;
    }

    int statueR = (minR + maxR) / 2;
    int statueC = (minC + maxC) / 2;

    Item statue = ItemFactory.Get(ItemNames.STATUE, objDb);
    Loc statueLoc = new(0, 0, statueR, statueC);
    statue.Traits.Add(new DescriptionTrait($"A myserious figure gazes warmly out at {town.Name}"));
    objDb.SetToLoc(statueLoc, statue);
    
    return cleric;
  }

  static Mob GenerateGrocer(Map map, Town town, NameGenerator ng, GameObjectDB objDb, Random rng)
  {
    Mob grocer = BaseVillager(ng, rng);
    
    grocer.Loc = LocForVillager(map, town.Market, rng);
    grocer.MoveStrategy = new WallMoveStrategy();
    grocer.Stats[Attribute.Markup] = new Stat(125 + rng.Next(76));
    grocer.Stats[Attribute.InventoryRefresh] = new Stat(1);
    grocer.SetBehaviour(new GrocerBehaviour());

    grocer.Inventory = new Inventory(grocer.ID, objDb);
    grocer.Inventory.Add(ItemFactory.Get(ItemNames.TORCH, objDb), grocer.ID);
    grocer.Inventory.Add(ItemFactory.Get(ItemNames.TORCH, objDb), grocer.ID);
    grocer.Inventory.Add(ItemFactory.Get(ItemNames.TORCH, objDb), grocer.ID);
    grocer.Inventory.Add(ItemFactory.Get(ItemNames.TORCH, objDb), grocer.ID);
    for (int j = 0; j < rng.Next(4); j++)
      grocer.Inventory.Add(ItemFactory.Get(ItemNames.TORCH, objDb), grocer.ID);
    for (int j = 0; j < rng.Next(1, 4); j++)
      grocer.Inventory.Add(ItemFactory.Get(ItemNames.POTION_HEALING, objDb), grocer.ID);
    if (rng.NextDouble() < 0.2)
      grocer.Inventory.Add(ItemFactory.Get(ItemNames.PICKAXE, objDb), grocer.ID);
    if (rng.NextDouble() < 0.5)
    {
      for (int j = 0; j < rng.Next(1, 4); j++)
        grocer.Inventory.Add(ItemFactory.Get(ItemNames.ANTIDOTE, objDb), grocer.ID);
    }
    if (rng.NextDouble() < 0.5)
    {
      for (int j = 0; j < rng.Next(1, 4); j++)
        grocer.Inventory.Add(ItemFactory.Get(ItemNames.SCROLL_DISARM, objDb), grocer.ID);
    }

    return grocer;
  }

  static Mob GenerateSmith(Map map, Town town, NameGenerator ng, GameObjectDB objDb, Random rng)
  {
    Mob smith = BaseVillager(ng, rng);
    
    smith.Loc = LocForVillager(map, town.Smithy, rng);
    smith.Stats[Attribute.Markup] = new Stat(125 + rng.Next(76));
    smith.Stats[Attribute.ShopMenu] = new Stat(0);
    smith.Stats[Attribute.InventoryRefresh] = new Stat(1);
    smith.SetBehaviour(new SmithBehaviour());
    smith.MoveStrategy = new WallMoveStrategy();

    smith.Inventory = new Inventory(smith.ID, objDb);
    smith.Inventory.Add(ItemFactory.Get(ItemNames.RINGMAIL, objDb), smith.ID);
    smith.Inventory.Add(ItemFactory.Get(ItemNames.HELMET, objDb), smith.ID);
    if (rng.NextDouble() < 0.25)
      smith.Inventory.Add(ItemFactory.Get(ItemNames.CHAINMAIL, objDb), smith.ID);
    smith.Inventory.Add(ItemFactory.Get(ItemNames.DAGGER, objDb), smith.ID);
    smith.Inventory.Add(ItemFactory.Get(ItemNames.DAGGER, objDb), smith.ID);
    smith.Inventory.Add(ItemFactory.Get(ItemNames.DAGGER, objDb), smith.ID);
    if (rng.NextDouble() < 0.33)
      smith.Inventory.Add(ItemFactory.Get(ItemNames.BATTLE_AXE, objDb), smith.ID);
    if (rng.NextDouble() < 0.33)
      smith.Inventory.Add(ItemFactory.Get(ItemNames.MACE, objDb), smith.ID);
    if (rng.NextDouble() < 0.33)
      smith.Inventory.Add(ItemFactory.Get(ItemNames.LONGSWORD, objDb), smith.ID);
    if (rng.NextDouble() < 0.33)
      smith.Inventory.Add(ItemFactory.Get(ItemNames.RAPIER, objDb), smith.ID);
    if (rng.NextDouble() < 0.2)
      smith.Inventory.Add(ItemFactory.Get(ItemNames.PICKAXE, objDb), smith.ID);
    if (rng.NextDouble() < 0.2)
      smith.Inventory.Add(ItemFactory.Get(ItemNames.LEATHER_GLOVES, objDb), smith.ID);

    return smith;
  }

  static int PickUnoccuppiedCottage(Town town, Random rng)
  {
    List<int> available = [];
    for (int i = 0; i < town.Homes.Count; i++) 
    { 
      if (!town.TakenHomes.Contains(i))
        available.Add(i);
    }

    if (available.Count == 0)
      return -1;
    else
    {
      var c = available[rng.Next(available.Count)];
      town.TakenHomes.Add(c);
      return c;
    }
  }

  static Mob GenerateMayor(Map map, Town town, NameGenerator ng, Random rng)
  {
    Mob mayor = BaseVillager(ng, rng);
    mayor.Traits.Add(new DialogueScriptTrait() { ScriptFile = "mayor.txt" });

    var homeID = PickUnoccuppiedCottage(town, rng);
    mayor.Loc = LocForVillager(map, town.Homes[homeID], rng);
    mayor.Stats.Add(Attribute.HomeID, new Stat(homeID));
    mayor.MoveStrategy = new WallMoveStrategy();
    mayor.SetBehaviour(new MayorBehaviour());

    return mayor;
  }

  static Mob GenerateVeteran(Map map, Town town, NameGenerator ng, GameObjectDB objDb, Random rng)
  {
    Mob veteran = BaseVillager(ng, rng);
    veteran.Traits.Add(new DialogueScriptTrait() { ScriptFile = "veteran.txt" });

    veteran.MoveStrategy = new WallMoveStrategy();
    veteran.SetBehaviour(new NPCBehaviour());

    var tavernSqs = town.Tavern.ToList();
    do
    {
      Loc loc = tavernSqs[rng.Next(tavernSqs.Count)];
      var tile = map.TileAt(loc.Row, loc.Col).Type;
      if ((tile == TileType.WoodFloor || tile == TileType.StoneFloor) && !objDb.Occupied(loc))
      {
        veteran.Loc = loc;
        break;
      }
    }
    while (true);

    return veteran;
  }

  static Mob GenerateVillager1(Map map, Town town, NameGenerator ng, Random rng)
  {
    Mob villager = BaseVillager(ng, rng);
    villager.Traits.Add(new DialogueScriptTrait() { ScriptFile = "villager1.txt" });

    int homeID  = PickUnoccuppiedCottage(town, rng);
    villager.Loc = LocForVillager(map, town.Homes[homeID], rng);
    villager.Stats.Add(Attribute.HomeID, new Stat(homeID));
    villager.MoveStrategy = new WallMoveStrategy();
    villager.SetBehaviour(new NPCBehaviour());

    return villager;
  }

  static Mob GenerateWidower(Map map, Town town, NameGenerator ng, Random rng)
  {
    Mob widower = BaseVillager(ng, rng);
    widower.Traits.Add(new DialogueScriptTrait() { ScriptFile = "widower.txt" });

    int homeID = PickUnoccuppiedCottage(town, rng);
    widower.Loc = LocForVillager(map, town.Homes[homeID], rng);
    
    widower.Stats.Add(Attribute.HomeID, new Stat(homeID));
    widower.MoveStrategy = new WallMoveStrategy();
    widower.SetBehaviour(new WidowerBehaviour());

    return widower;
  }

  static void GenerateWitches(Map map, Town town, GameObjectDB objDb, FactDb factDb, Random rng)
  {
     Mob kylie = new()
    {
      Name = "Kylie",
      Appearance = "The local, stylish witch.",
      Glyph = new Glyph('@', Colours.LIGHT_BLUE , Colours.DARK_BLUE, Colours.BLACK, Colours.BLACK)
    };
    kylie.Stats[Attribute.DialogueState] = new Stat(0);
    kylie.Traits.Add(new VillagerTrait());
    kylie.Traits.Add(new NamedTrait());
    
    var sqs = town.WitchesCottage.ToList();
    kylie.Loc = sqs[rng.Next(sqs.Count)];
    kylie.SetBehaviour(new WitchBehaviour());
    objDb.Add(kylie);
    objDb.AddToLoc(kylie.Loc, kylie);
  }

  static Mob GeneratePuppy(Map map, Town town, GameObjectDB objDb, Random rng)
  {
    int roll = rng.Next(4);
    var (colourDesc, colour) = roll switch
    {
      0 => ("grey", Colours.GREY),
      1 => ("brown", Colours.LIGHT_BROWN),
      2 => ("black", Colours.DARK_GREY),
      _ => ("rusty", Colours.DULL_RED)
    };

    roll = rng.Next(5);
    string adj = roll switch
    {
      0 => "scruffy",
      1 => "furry",
      2 => "fluffy",
      3 => "playful",
      _ => "friendly"
    };

    roll = rng.Next(4);
    string dogType = roll switch
    {
      0 => "pup",
      1 => "dog",
      2 => "puppy",
      _ => "hound"
    };

    var pup = new Mob()
    {
      Name = $"{adj} {dogType}",
      Appearance = $"{adj} {dogType} with {colourDesc} fur",
      Glyph = new Glyph('d', colour, colour, Colours.BLACK, Colours.BLACK),
      Loc = RandomOutdoorLoc(map, town, rng)
    };
    pup.SetBehaviour(new VillagePupBehaviour());
    pup.Traits.Add(new VillagerTrait());

    return pup;
  }

  public static void RefreshGrocerInventory(Mob grocer, GameObjectDB objDb, Random rng)
  {
    List<Item> currStock = grocer.Inventory.Items();

    foreach (Item item in currStock)
    {
      if (rng.NextDouble() < 0.2)
      {
        grocer.Inventory.RemoveByID(item.ID);
        objDb.RemoveItemFromGame(Loc.Nowhere, item);
      }
    }

    int newStock = rng.Next(1, 4);
    for (int j = 0; j < newStock; j++)
    {
      int roll = rng.Next(13);
      if (roll < 3)
        grocer.Inventory.Add(ItemFactory.Get(ItemNames.TORCH, objDb), grocer.ID);
      else if (roll < 5)
        grocer.Inventory.Add(ItemFactory.Get(ItemNames.POTION_HEALING, objDb), grocer.ID);
      else if (roll == 6)
        grocer.Inventory.Add(ItemFactory.Get(ItemNames.ANTIDOTE, objDb), grocer.ID);
      else if (roll == 7)
        grocer.Inventory.Add(ItemFactory.Get(ItemNames.SCROLL_DISARM, objDb), grocer.ID);
      else if (roll == 8)
        grocer.Inventory.Add(ItemFactory.Get(ItemNames.SCROLL_BLINK, objDb), grocer.ID);
      else if (roll == 9)
        grocer.Inventory.Add(ItemFactory.Get(ItemNames.SCROLL_KNOCK, objDb), grocer.ID);
      else if (roll == 10)
        grocer.Inventory.Add(ItemFactory.Get(ItemNames.SCROLL_PROTECTION, objDb), grocer.ID);
      else if (roll == 11)
        grocer.Inventory.Add(ItemFactory.Get(ItemNames.POTION_MIND_READING, objDb), grocer.ID);
      else if (roll == 12)
        grocer.Inventory.Add(ItemFactory.Get(ItemNames.POTION_OF_LEVITATION, objDb), grocer.ID);
    }

    // Grocer always keeps a few torches in stock
    bool torchesInStock = grocer.Inventory.Items().Any(i => i.Name == "torch");
    if (!torchesInStock)
    {
      for (int j = 0; j < rng.Next(2, 5); j++)
      {
        grocer.Inventory.Add(ItemFactory.Get(ItemNames.TORCH, objDb), grocer.ID);
      }
    }
  }

  public static void RefreshSmithInventory(Mob smith, GameObjectDB objDb, Random rng)
  {
     List<Item> currStock = smith.Inventory.Items();

    foreach (Item item in currStock)
    {
      if (rng.NextDouble() < 0.2)
      {
        smith.Inventory.RemoveByID(item.ID);
        objDb.RemoveItemFromGame(Loc.Nowhere, item);
      }
    }

    int newStock = rng.Next(1, 5);
    for (int j = 0; j < newStock; j++)
    {
      int roll = rng.Next(11);
      if (roll == 0)
        smith.Inventory.Add(ItemFactory.Get(ItemNames.CHAINMAIL, objDb), smith.ID);
      else if (roll == 1)
        smith.Inventory.Add(ItemFactory.Get(ItemNames.SHIELD, objDb), smith.ID);
      else if (roll == 3)
        smith.Inventory.Add(ItemFactory.Get(ItemNames.BATTLE_AXE, objDb), smith.ID);
      else if (roll == 4)
        smith.Inventory.Add(ItemFactory.Get(ItemNames.PICKAXE, objDb), smith.ID);
      else if (roll == 5)
        smith.Inventory.Add(ItemFactory.Get(ItemNames.SILVER_DAGGER, objDb), smith.ID);
      else if (roll == 6)
        smith.Inventory.Add(ItemFactory.Get(ItemNames.GUISARME, objDb), smith.ID);
      else if (roll == 7)
        smith.Inventory.Add(ItemFactory.Get(ItemNames.RAPIER, objDb), smith.ID);
      else if (roll == 8)
        smith.Inventory.Add(ItemFactory.Get(ItemNames.RINGMAIL, objDb), smith.ID);
      else if (roll == 9)
        smith.Inventory.Add(ItemFactory.Get(ItemNames.HELMET, objDb), smith.ID);
      else if (roll == 10)
        smith.Inventory.Add(ItemFactory.Get(ItemNames.LEATHER_GLOVES, objDb), smith.ID);
    }
  }

  public static void Populate(Map map, Town town, GameObjectDB objDb, FactDb factDb, Random rng)
  {
    var ng = new NameGenerator(rng, Util.NamesFile);

    var cleric = GeneratePriest(map, town, ng, objDb, rng);
    objDb.Add(cleric);
    objDb.AddToLoc(cleric.Loc, cleric);

    var smith = GenerateSmith(map, town, ng, objDb, rng);
    objDb.Add(smith);
    objDb.AddToLoc(smith.Loc, smith);
    factDb.Add(new SimpleFact() { Name = "SmithId", Value = smith.ID.ToString() });

    var grocer = GenerateGrocer(map, town, ng, objDb, rng);
    objDb.Add(grocer);
    objDb.AddToLoc(grocer.Loc, grocer);
    factDb.Add(new SimpleFact() { Name = "GrocerId", Value = grocer.ID.ToString() });

    var innkeeper = GenerateInnkeeper(map, town, ng, objDb, rng);
    objDb.Add(innkeeper);
    objDb.AddToLoc(innkeeper.Loc, innkeeper);
    factDb.Add(new SimpleFact() { Name = "TavernName", Value = NameGenerator.GenerateTavernName(rng) });

    var pup = GeneratePuppy(map, town, objDb, rng);
    objDb.Add(pup);
    objDb.AddToLoc(pup.Loc, pup);

    var mayor = GenerateMayor(map, town, ng, rng);
    objDb.Add(mayor);
    objDb.AddToLoc(mayor.Loc, mayor);

    var v1 = GenerateVillager1(map, town, ng, rng);
    objDb.Add(v1);
    objDb.AddToLoc(v1.Loc, v1);

    var vet = GenerateVeteran(map, town, ng, objDb, rng);
    objDb.Add(vet);
    objDb.AddToLoc(vet.Loc, vet);

    GenerateWitches(map, town, objDb, factDb, rng);

    ulong fallenAdventurerID = ulong.MaxValue;
    foreach (GameObj obj in objDb.Objs.Values)
    {
      if (obj.HasTrait<FallenAdventurerTrait>())
      {
        fallenAdventurerID = obj.ID;
        break;
      }
    }

    if (fallenAdventurerID != ulong.MaxValue)
    {
      // Add a villager who knew the fallen adventurer. (Eventually this will
      // be a random chance of happening since we'll have several fallen 
      // adventurers over many levels and not ALL of them will have had 
      // relationships with villagers)
      var widower = GenerateWidower(map, town, ng, rng);
      objDb.Add(widower);
      objDb.AddToLoc(widower.Loc, widower);
      widower.Traits.Add(new RelationshipTrait() { Person1ID = widower.ID, Person2ID = fallenAdventurerID, Label = "romantic" });
    }    
  }
}