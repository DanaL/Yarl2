
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
          case TileType.Tree:
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
      5 => (Colours.BLUE, Colours.DARK_BLUE),
      6 => (Colours.PINK, Colours.DULL_RED),
      _ => (Colours.LIGHT_BROWN, Colours.BROWN)
    };
  }

  static string VillagerAppearance(Random rng)
  {
    string[] species = ["human", "elf", "half-elf", "gnome", "dwarf", "orc", "half-orc"];
    string[] eyes = ["bright", "deep", "dark", "sad", "distant", "piercing", "clear", "clouded"];
    string[] hair = ["bald",
      "long",
      "short",
      "frizzy",
      "stylish",
      "unkempt",
      "messy",
      "perfurmed",
      "unwashed",
      "tousled",
      "curly",
      "wavy"];

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

  static Mob GeneratePriest(Map map, Town town, NameGenerator ng, Random rng)
  {
    var (lit, unlit) = VillagerColour(rng);
    var cleric = new Mob()
    {
      Name = ng.GenerateName(rng.Next(5, 9)),
      Status = ActorStatus.Indifferent,
      Appearance = VillagerAppearance(rng),      
      Glyph = new Glyph('@', lit, unlit, Colours.BLACK)
    };
    cleric.Traits.Add(new NamedTrait());
    var sqs = town.Shrine.Where(sq => map.TileAt(sq.Row, sq.Col).Type == TileType.StoneFloor ||
                                      map.TileAt(sq.Row, sq.Col).Type == TileType.WoodFloor).ToList();
    cleric.Loc = sqs[rng.Next(sqs.Count)];
    cleric.SetBehaviour(new PriestBehaviour());
    cleric.MoveStrategy = new WallMoveStrategy();

    return cleric;
  }

  static Mob GenerateGrocer(Map map, Town town, NameGenerator ng, GameObjectDB objDb, Random rng)
  {
    var (lit, unlit) = VillagerColour(rng);
    var grocer = new Mob()
    {
      Name = ng.GenerateName(rng.Next(5, 9)),
      Status = ActorStatus.Indifferent,
      Appearance = VillagerAppearance(rng),
      Glyph = new Glyph('@', lit, unlit, Colours.BLACK)
    };
    grocer.Traits.Add(new NamedTrait());

    var sqs = town.Market.Where(sq => map.TileAt(sq.Row, sq.Col).Type == TileType.StoneFloor ||
                                      map.TileAt(sq.Row, sq.Col).Type == TileType.WoodFloor).ToList();
    grocer.Loc = sqs[rng.Next(sqs.Count)];
    grocer.MoveStrategy = new WallMoveStrategy();
    grocer.SetBehaviour(new GrocerBehaviour(1.25 + rng.NextDouble() / 2));

    grocer.Inventory = new Inventory(grocer.ID, objDb);
    grocer.Inventory.Add(ItemFactory.Get("torch", objDb), grocer.ID);
    grocer.Inventory.Add(ItemFactory.Get("torch", objDb), grocer.ID);
    grocer.Inventory.Add(ItemFactory.Get("torch", objDb), grocer.ID);
    grocer.Inventory.Add(ItemFactory.Get("torch", objDb), grocer.ID);
    for (int j = 0; j < rng.Next(4); j++)
      grocer.Inventory.Add(ItemFactory.Get("torch", objDb), grocer.ID);
    for (int j = 0; j < rng.Next(1, 4); j++)
      grocer.Inventory.Add(ItemFactory.Get("potion of healing", objDb), grocer.ID);

    return grocer;
  }

  static Mob GenerateSmith(Map map, Town town, NameGenerator ng, GameObjectDB objDb, Random rng)
  {
    var (lit, unlit) = VillagerColour(rng);
    var smith = new Mob()
    {
      Name = ng.GenerateName(rng.Next(5, 9)),
      Status = ActorStatus.Indifferent,
      Appearance = VillagerAppearance(rng),
      Glyph = new Glyph('@', lit, unlit, Colours.BLACK)
    };
    smith.Traits.Add(new NamedTrait());

    var sqs = town.Smithy.Where(sq => map.TileAt(sq.Row, sq.Col).Type == TileType.StoneFloor ||
                                      map.TileAt(sq.Row, sq.Col).Type == TileType.WoodFloor).ToList();
    smith.Loc = sqs[rng.Next(sqs.Count)];
    smith.SetBehaviour(new SmithBehaviour(1.5 + rng.NextDouble() / 2));
    smith.MoveStrategy = new WallMoveStrategy();

    smith.Inventory = new Inventory(smith.ID, objDb);
    smith.Inventory.Add(ItemFactory.Get("ringmail", objDb), smith.ID);
    smith.Inventory.Add(ItemFactory.Get("helmet", objDb), smith.ID);
    if (rng.NextDouble() < 0.25)
      smith.Inventory.Add(ItemFactory.Get("chainmail", objDb), smith.ID);
    smith.Inventory.Add(ItemFactory.Get("dagger", objDb), smith.ID);
    smith.Inventory.Add(ItemFactory.Get("dagger", objDb), smith.ID);
    smith.Inventory.Add(ItemFactory.Get("dagger", objDb), smith.ID);
    if (rng.NextDouble() < 0.33)
      smith.Inventory.Add(ItemFactory.Get("battle axe", objDb), smith.ID);
    if (rng.NextDouble() < 0.33)
      smith.Inventory.Add(ItemFactory.Get("mace", objDb), smith.ID);
    if (rng.NextDouble() < 0.33)
      smith.Inventory.Add(ItemFactory.Get("longsword", objDb), smith.ID);
    if (rng.NextDouble() < 0.33)
      smith.Inventory.Add(ItemFactory.Get("rapier", objDb), smith.ID);

    return smith;
  }

  static (int, List<Loc>) PickUnoccuppiedCottage(Town town, Random rng)
  {
    List<int> available = [];
    for (int i = 0; i < town.Homes.Count; i++) 
    { 
      if (!town.TakenHomes.Contains(i))
        available.Add(i);
    }

    if (available.Count == 0)
      return (-1, []);
    else
    {
      var c = available[rng.Next(available.Count)];
      town.TakenHomes.Add(c);
      return (c, town.Homes[c].ToList());
    }
  }

  static Mob GenerateWidower(Map map, Town town, NameGenerator ng, GameObjectDB objDb, FallenAdventurerFact fact, History history, Random rng)
  {
    var (lit, unlit) = VillagerColour(rng);
    var widower = new Mob()
    {
      Name = ng.GenerateName(rng.Next(6, 11)),
      Status = ActorStatus.Indifferent,
      Appearance = VillagerAppearance(rng),
      Glyph = new Glyph('@', lit, unlit, Colours.BLACK)
    };
    widower.Traits.Add(new NamedTrait());

    var (homeID, cottage) = PickUnoccuppiedCottage(town, rng);
    do
    {
      int i = rng.Next(cottage.Count);
      Loc loc = cottage[i];
      var tile = map.TileAt(loc.Row, loc.Col).Type;
      if (tile == TileType.WoodFloor || tile == TileType.StoneFloor)
      {
        widower.Loc = loc;
        break;
      }
    }
    while (true);

    widower.Stats.Add(Attribute.HomeID, new Stat(homeID));
    widower.MoveStrategy = new WallMoveStrategy();
    widower.SetBehaviour(new WidowerBehaviour());

    return widower;
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
      Status = ActorStatus.Indifferent,
      Appearance = $"{adj} {dogType} with {colourDesc} fur",
      Glyph = new Glyph('d', colour, colour, Colours.BLACK),
      Loc = RandomOutdoorLoc(map, town, rng)
    };
    pup.SetBehaviour(new VillagePupBehaviour());

    return pup;
  }

  public static void Populate(Map map, Town town, GameObjectDB objDb, History history, Random rng)
  {
    var ng = new NameGenerator(rng, "names.txt");

    var cleric = GeneratePriest(map, town, ng, rng);
    objDb.Add(cleric);
    objDb.AddToLoc(cleric.Loc, cleric);

    var smith = GenerateSmith(map, town, ng, objDb, rng);
    objDb.Add(smith);
    objDb.AddToLoc(smith.Loc, smith);

    var grocer = GenerateGrocer(map, town, ng, objDb, rng);
    objDb.Add(grocer);
    objDb.AddToLoc(grocer.Loc, grocer);

    var pup = GeneratePuppy(map, town, objDb, rng);
    objDb.Add(pup);
    objDb.AddToLoc(pup.Loc, pup);

    FallenAdventurerFact? fallen = null;
    foreach (var fact in history.Facts)
    {
      if (fact is FallenAdventurerFact fa)
      {
        fallen = fa;
      }
    }

    if (fallen is not null)
    {
      // Add a villager who knew the fallen adventurer. (Eventually this will
      // be a random chance of happening since we'll have several fallen 
      // adventurers over many levels and not ALL of them will have had 
      // relationships with villagers)
      var widower = GenerateWidower(map, town, ng, objDb, fallen, history, rng);
      objDb.Add(widower);
      objDb.AddToLoc(widower.Loc, widower);

      history.Facts.Add(new RelationshipFact() { Person1 = widower.ID, Person2 = fallen.ID, Desc = "romantic" });
    }
    
  }
}