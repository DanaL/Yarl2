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

 interface IBehaviour
{
  (Action, Inputer?) Chat(Mob actor, GameState gameState);
  string GetBark(Mob actor, GameState gs);
}

class NullBehaviour : IBehaviour
{
  static readonly NullBehaviour instance = new();
  public static NullBehaviour Instance() => instance;

  public (Action, Inputer?) Chat(Mob actor, GameState gameState) => throw new NotImplementedException();
  public string GetBark(Mob actor, GameState gs) => "";
}

// I think I'll likely eventually merge this into IBehaviour
interface IDialoguer
{
  void InitDialogue(Mob actor, GameState gs);
  (string, string, List<(string, char)>) CurrentText(Mob mob, GameState gs);
  void SelectOption(Mob actor, char opt, GameState gs);
}

class MonsterBehaviour : IBehaviour, IDialoguer
{
  readonly Dictionary<string, ulong> _lastUse = [];
  List<DialogueOption> Options { get; set; } = [];

  public string GetBark(Mob actor, GameState gs)
  {
    foreach (Trait t in actor.Traits)
    {
      if (t is WorshiperTrait worshipper && gs.Rng.Next(8) == 0)
        return worshipper.Chant;
    }

    return "";
  }

  public (Action, Inputer?) Chat(Mob actor, GameState gameState)
  {
    if (actor.HasTrait<DialogueScriptTrait>())
    {
      Dialoguer acc = new(actor, gameState);
      CloseMenuAction action = new(gameState, 1.0);

      return (action, acc);
    }

    string s = gameState.Rng.Next(3) switch
    {
      0 => $"{actor.FullName.Capitalize()} isn't here to chit-chat.",
      1 => $"{actor.FullName.Capitalize()} is curiously laconic.",
      _ => $"{actor.FullName.Capitalize()} isn't interested in conversation."
    };
    gameState.UIRef().AlertPlayer(s);

    return (new NullAction(), null);
  }

  public virtual (string, string, List<(string, char)>) CurrentText(Mob mob, GameState gs)
  {
    string scriptFile = mob.Traits.OfType<DialogueScriptTrait>().First().ScriptFile;
    var dialogue = new DialogueInterpreter();

    var (txt, footer) = dialogue.Run(scriptFile, mob, gs);
    Options = dialogue.Options;
    List<(string, char)> opts = [..Options.Select(o => (o.Text, o.Ch))];
    
    return (txt, footer, opts);
  }

  public void SelectOption(Mob mob, char choice, GameState gs)
  {
    foreach (DialogueOption opt in Options)
    {
      if (opt.Ch == choice)
      {
        var dialogue = new DialogueInterpreter();
        dialogue.Run(opt.Expr, mob, gs);
        break;
      }
    }
  }

  public void InitDialogue(Mob actor, GameState gs) {}
}
    
// Disguised monsters behave differently while they are disguised, but then act like a normal monster
// so it just seemed simple (or easy...) to extend MonsterBevaviour
class DisguisedMonsterBehaviour : MonsterBehaviour
{
  //public override Action CalcAction(Mob actor, GameState gs)
  //{
  //  bool disguised = actor.Stats[Attribute.InDisguise].Curr == 1;
  //  if (disguised && Util.Distance(actor.Loc, gs.Player.Loc) > 1)
  //    return new PassAction();

  //  if (disguised)
  //  {
  //    var disguise = actor.Traits.OfType<DisguiseTrait>().First();
  //    string txt = $"The {disguise.DisguiseForm} was really {actor.Name.IndefArticle()}!";
  //    gs.UIRef().AlertPlayer(txt);
  //    actor.Glyph = disguise.TrueForm;
  //    actor.Stats[Attribute.InDisguise].SetMax(0);
  //  }

  //  return base.CalcAction(actor, gs);
  //}
}

class VillagePupBehaviour : NPCBehaviour
{  
  public override string GetBark(Mob actor, GameState gs) => "";

  public override (Action, Inputer) Chat(Mob animal, GameState gs)
  {
    var sb = new StringBuilder(animal.Appearance.IndefArticle().Capitalize());
    sb.Append(".\n\n");

    // Eventually the dog might have different sounds based on mood, etc
    sb.Append("Arf! Arf!");

    gs.UIRef().SetPopup(new Popup(sb.ToString(), "", -1, -1));
    return (new PassAction(), new PauseForMoreInputer(gs));
  }
}

class InnkeeperBehaviour : NPCBehaviour
{
  public override (Action, Inputer?) Chat(Mob actor, GameState gameState)
  {
    var acc = new InnkeeperInputer(actor, gameState);
    var action = new InnkeeperServiceAction(gameState, actor);

    return (action, acc);    
  }
}

class MoonDaughtersClericBehaviour : NPCBehaviour
{
  DateTime _lastBark = new(1900, 1, 1);

  public override void InitDialogue(Mob mob, GameState gs)
  {
    int dialogueState = mob.Stats.TryGetValue(Attribute.DialogueState, out var ds) ? ds.Curr : 0;
    int lastGiftTime = mob.Stats.TryGetValue(Attribute.LastGiftTime, out var lgt) ? lgt.Curr : 0;
    int turn = (int)gs.Turn % int.MaxValue;
    
    if (dialogueState > 0 && turn - lastGiftTime > 1000)
    {
      mob.Stats[Attribute.DialogueState] = new Stat(0);
    }
  }

  public override string GetBark(Mob actor, GameState gs)
  {    
    if ((DateTime.UtcNow - _lastBark).TotalSeconds > 17)
    {
      _lastBark = DateTime.UtcNow;
      return "Darkness can protect as well as conceal.";
    }

    return "";
  }
}

class GnomeMerchantBehaviour : NPCBehaviour
{
  DateTime _lastBark = new(1900, 1, 1);

  public override void InitDialogue(Mob mob, GameState gs)
  {
    NumberListTrait selections = mob.Traits.OfType<NumberListTrait>()
                                           .Where(t => t.Name == "ShopSelections")
                                           .First();
    selections.Items = [];
    mob.Stats[Attribute.ShopInvoice] = new Stat(0);
  }

  public override string GetBark(Mob actor, GameState gs)
  {    
    if ((DateTime.UtcNow - _lastBark).TotalSeconds > 13)
    {
      _lastBark = DateTime.UtcNow;
      return gs.Rng.Next(4) switch
      {
        0 => "Priced to clear!",
        1 => "I thought this would be easy money!",
        2 => "The customer is always something, something...",
        _ => "Everything must go!"
      };
      
    }

    return "";
  }

  public override bool ConfirmChoices(Actor npc, GameState gs)
  {
    NumberListTrait selections = npc.Traits.OfType<NumberListTrait>()
                                           .Where(t => t.Name == "ShopSelections")
                                           .First();

    if (selections.Items.Count == 0 || npc.Stats[Attribute.ShopInvoice].Curr > gs.Player.Inventory.Zorkmids)
    {
      return false;
    }

    List<Item> inventory = npc.Inventory.Items();
    List<ulong> purchases = [];
    for (int i = 0; i < inventory.Count; i++)
    {
      if (selections.Items.Contains(i))
        purchases.Add(inventory[i].ID);
    }

    foreach (ulong id in purchases)
    {
      Item item = npc.Inventory.RemoveByID(id)!;
      gs.Player.AddToInventory(item, gs);
    }

    gs.Player.Inventory.Zorkmids -= npc.Stats[Attribute.ShopInvoice].Curr;

    gs.UIRef().AlertPlayer($"You hand over your money and {npc.FullName} gives you your goods.");
    
    selections.Items = [];

    return true;
  }
}

class PriestBehaviour : NPCBehaviour
{  
  DateTime _lastBark = new(1900, 1, 1);

  public override string GetBark(Mob actor, GameState gs)
  {    
    if ((DateTime.UtcNow - _lastBark).TotalSeconds > 13)
    {
      _lastBark = DateTime.UtcNow;
      return "Praise be to Huntokar!";
    }

    return "";
  }

  public override (Action, Inputer?) Chat(Mob actor, GameState gameState)
  {
    if (gameState.Player.HasTrait<ShunnedTrait>())
    {
      var acc = new PriestInputer(actor, "Oh my child, what you have done?\n\nAn offering to Huntokar is needed to wash clean the stain on you.", gameState);
      var action = new PriestServiceAction(gameState, actor);
      return (action, acc);
    }

    return base.Chat(actor, gameState);
  }

  public override void RefreshShop(Actor npc, GameState gs) 
  {
    int lastRefresh = npc.Stats[Attribute.InventoryRefresh].Curr;
    int turn = (int)(gs.Turn % int.MaxValue);

    if (Math.Abs(turn - lastRefresh) < 2500)
      return;

    List<int> newMenu = [];
    List<int> options = [ 1, 2, 3, 4, 5 ];
    int lastBlessing = gs.Player.Stats[Attribute.LastBlessing].Curr;
    if (lastBlessing > 0)
    {
      newMenu.Add(lastBlessing);
      options.Remove(lastBlessing);
    }
    while (newMenu.Count < 3)
    {
      int b = options[gs.Rng.Next(options.Count)];
      newMenu.Add(b);
      options.Remove(b);
    }
  
    NumberListTrait blessings = npc.Traits.OfType<NumberListTrait>().Where(t => t.Name == "Blessings").First();
    blessings.Items = newMenu;
    
    npc.Stats[Attribute.InventoryRefresh].SetMax(turn);
  }
}

class WitchBehaviour : NPCBehaviour
{
  DateTime _lastBark = new(1900, 1, 1);
  
  public override string GetBark(Mob mob, GameState gs)
  {
    if ((DateTime.UtcNow - _lastBark).TotalSeconds < 9)
      return "";

    _lastBark = DateTime.UtcNow;

    string grocerName = "";
    if (gs.FactDb.FactCheck("GrocerId") is SimpleFact fact)
    {
      ulong grocerId = ulong.Parse(fact.Value);
      if (gs.ObjDb.GetObj(grocerId) is Actor grocer)
        grocerName = grocer.FullName.Capitalize();
    }
    
    if (mob.HasTrait<InvisibleTrait>())
    {
      return gs.Rng.Next(3) switch
      {
        0 => "I'm over here.",
        1 => "Sophie's been trying invisibility potions again.",
        _ => "Is the potion working?"
      };
    }
    else
    {
      return gs.Rng.Next(4) switch
      {
        0 => "Sophie, did you see that sparrow?",
        1 => $"{grocerName} is charging HOW MUCH for mandrake root?",
        2 => "Do not tarry!",
        _ => "Dark augeries..."
      };
    }    
  }

  public override (Action, Inputer?) Chat(Mob actor, GameState gameState)
  {
    var acc = new WitchDialogue(actor, gameState);
    var action = new WitchServiceAction(gameState, actor);
    
    return (action, acc);
  }
}

class SmithBehaviour : NPCBehaviour
{
  DateTime _lastBark = new(1900, 1, 1);

  // Eventually just replace/merge GetPark and PickBark
  public override string GetBark(Mob actor, GameState gs) 
  {
    string bark = "";

    if ((DateTime.UtcNow - _lastBark).TotalSeconds > 13)
    {
      bark = PickBark(actor, gs);
      _lastBark = DateTime.UtcNow;
    }

    return bark;
  }

  static string PickBark(Mob smith, GameState gs)
  {
    var (hour, _) = gs.CurrTime();

    if (hour >= 19 && hour < 22) 
    {
      return "Nothing like a good ale after a day at the forge!";
    }
    else if (hour >= 7 && hour < 19) 
    {
      List<Item> items = [..smith.Inventory.UsedSlots()
                                .Select(smith.Inventory.ItemAt)
                                .Select(si => si.Item1)];
      Item? item;
      if (items.Count > 0)
        item = items[gs.Rng.Next(items.Count)];
      else
        item = null;

      int roll = gs.Rng.Next(2);
      if (roll == 0 && item is not null)
      {
        if (item.Type == ItemType.Weapon)
        {
          if (item.Traits.Any(t => t is DamageTrait trait && trait.DamageType == DamageType.Blunt))
            return $"A stout {item.Name} will serve you well!";
          else
            return $"A sharp {item.Name} will serve you well!";
        }
        else if (item.Name == "helmet" || item.Name == "shield")
          return $"A sturdy {item.Name} will serve you well!";
        else
          return $"Some sturdy {item.Name} will serve you well!";
      }
      else
      {
        return "More work...";
      }
    }
    
    return "";
  }

  static string Blurb(GameState gs)
  {
    var sb = new StringBuilder();
    sb.Append('"');

    string blurb;

    if (gs.FactDb.FactCheck("DwarfMine") is not null && gs.Rng.NextDouble() < 0.25)
    {
      blurb = "The ancient dwarves used to mine mithril in their tunnels. I could do some keen work with mithril!";
    }
    else
    {
      blurb = gs.Rng.Next(3) switch
      {
        0 => "If you're looking for arms or armour, I'm the only game in town!",
        1 => "Weapons or armour showing signs of wear and tear? I can help with that!",
        _ => "If you find weird gems or monster parts, I may be able to use them to spruce up your gear!"
      };
    }

    sb.Append(blurb);
    sb.Append('"');

    return sb.ToString();
  }

  public override (Action, Inputer) Chat(Mob actor, GameState gs)
  {
    if (gs.Player.HasTrait<ShunnedTrait>())
    {
      return (new NullAction(), new PauseForMoreInputer(gs));
    }

    var acc = new SmithyInputer(actor, Blurb(gs), gs);
    var action = new ShoppingCompletedAction(gs, actor);

    return (action, acc);
  }

  public override void RefreshShop(Actor npc, GameState gs) 
  {
    int lastRefresh = npc.Stats[Attribute.InventoryRefresh].Curr;
    int turn = (int)(gs.Turn % int.MaxValue);

    if (Math.Abs(turn - lastRefresh) < 750)
      return;
    npc.Stats[Attribute.InventoryRefresh].SetMax(turn);
    
    List<Item> currStock = npc.Inventory.Items();

    foreach (Item item in currStock)
    {
      if (gs.Rng.NextDouble() < 0.2)
      {
        npc.Inventory.RemoveByID(item.ID);
        gs.ObjDb.RemoveItemFromGame(Loc.Nowhere, item);
      }
    }

    int newStock = gs.Rng.Next(1, 5);
    for (int j = 0; j < newStock; j++)
    {
      int roll = gs.Rng.Next(12);
      if (roll == 0)
        npc.Inventory.Add(ItemFactory.Get(ItemNames.CHAINMAIL, gs.ObjDb), npc.ID);
      else if (roll == 1)
        npc.Inventory.Add(ItemFactory.Get(ItemNames.SHIELD, gs.ObjDb), npc.ID);
      else if (roll == 3)
        npc.Inventory.Add(ItemFactory.Get(ItemNames.BATTLE_AXE, gs.ObjDb), npc.ID);
      else if (roll == 4)
        npc.Inventory.Add(ItemFactory.Get(ItemNames.PICKAXE, gs.ObjDb), npc.ID);
      else if (roll == 5)
        npc.Inventory.Add(ItemFactory.Get(ItemNames.SILVER_DAGGER, gs.ObjDb), npc.ID);
      else if (roll == 6)
        npc.Inventory.Add(ItemFactory.Get(ItemNames.GUISARME, gs.ObjDb), npc.ID);
      else if (roll == 7)
        npc.Inventory.Add(ItemFactory.Get(ItemNames.RAPIER, gs.ObjDb), npc.ID);
      else if (roll == 8)
        npc.Inventory.Add(ItemFactory.Get(ItemNames.RINGMAIL, gs.ObjDb), npc.ID);
      else if (roll == 9)
        npc.Inventory.Add(ItemFactory.Get(ItemNames.HELMET, gs.ObjDb), npc.ID);
      else if (roll == 10)
        npc.Inventory.Add(ItemFactory.Get(ItemNames.LEATHER_GLOVES, gs.ObjDb), npc.ID);
      else if (roll == 11)
        npc.Inventory.Add(ItemFactory.Get(ItemNames.QUARTERSTAFF, gs.ObjDb), npc.ID);
    }
  }
}

class AlchemistBehaviour : NPCBehaviour
{
  DateTime _lastBark = new(1900, 1, 1);

  public override string GetBark(Mob actor, GameState gs)
  {
    if ((DateTime.UtcNow - _lastBark).TotalSeconds < 9)
      return "";

    _lastBark = DateTime.UtcNow;

    var (hour, _) = gs.CurrTime();

    List<string> barks;
    if (gs.Town.WitchesGarden.Contains(actor.Loc))
    {
      barks = ["Kylie, what do you want for dinner?", "Hey plant buddies, you'all doing great!", "Hello bee friends!", "Hmm you need a little more water.", "♪ Hmm mmm ♪♪"];
    }
    else if (hour < 7 || hour >= 19)
    {
      barks = ["I've been working on a new song!", "How was your day?", "I want to tweak that a recipe a bit."];
    }
    else
    {
      barks = ["I've been working on a new song!", "𝅘𝅥𝅯 Hmm mmm 𝅘𝅥𝅘𝅥", "Kylie, what do you want for dinner?"];
    }
    
    return barks[gs.Rng.Next(barks.Count)];
  }

  public override (Action, Inputer?) Chat(Mob actor, GameState gs)
  {
    string s = "Oh, I dabble in alchemy and potioncraft if you're interested. It pays the bills between gigs.";
    var acc = new ShopMenuInputer(actor, s, gs);
    var action = new ShoppingCompletedAction(gs, actor);

    return (action, acc);
  }

  public override void RefreshShop(Actor npc, GameState gs) 
  {
    int lastRefresh = npc.Stats[Attribute.InventoryRefresh].Curr;
    int turn = (int)(gs.Turn % int.MaxValue);

    if (Math.Abs(turn - lastRefresh) < 750)
      return;
    npc.Stats[Attribute.InventoryRefresh].SetMax(turn);

    List<Item> currStock = npc.Inventory.Items();
    foreach (Item item in currStock)
    {
      if (gs.Rng.NextDouble() < 0.2)
      {
        npc.Inventory.RemoveByID(item.ID);
        gs.ObjDb.RemoveItemFromGame(Loc.Nowhere, item);
      }
    }

    int newStock = gs.Rng.Next(1, 5);
    for (int j = 0; j < newStock; j++)
    {      
      ItemNames itemName = gs.Rng.Next(7) switch
      {
        0 => ItemNames.POTION_HEALING,
        1 => ItemNames.POTION_HEROISM,
        2 => ItemNames.POTION_OF_LEVITATION,
        3 => ItemNames.POTION_MIND_READING,
        4 => ItemNames.ANTIDOTE,
        5 => ItemNames.POTION_OBSCURITY,
        _ => ItemNames.MUSHROOM_STEW
      };
      Item item = ItemFactory.Get(itemName, gs.ObjDb);
      item.Traits.Add(new SideEffectTrait() { Odds = 10, Effect = "Confused#0#13#0" });
      npc.Inventory.Add(item, npc.ID);
    }
  }
}

class GrocerBehaviour : NPCBehaviour
{
  DateTime _lastBark = new(1900, 1, 1);

  public override string GetBark(Mob actor, GameState gs)
  {
    if ((DateTime.UtcNow - _lastBark).TotalSeconds < 10)
      return "";

    _lastBark = DateTime.UtcNow;

    return gs.Rng.Next(3) switch
    {
      0 => "Supplies for the prudent adventurer!",
      1 => "Check out our specials!",
      _ => "Store credit only."
    };
  }
  
  public override (Action, Inputer) Chat(Mob actor, GameState gs)
  {
    if (gs.Player.HasTrait<ShunnedTrait>())
    {
      return (new NullAction(), new PauseForMoreInputer(gs));
    }
    var sb = new StringBuilder();
    sb.Append("\"Welcome to the ");
    sb.Append(gs.Town.Name);
    sb.Append(" market!\"");

    var acc = new ShopMenuInputer(actor, sb.ToString(), gs);
    var action = new ShoppingCompletedAction(gs, actor);

    return (action, acc);
  }

  public override void RefreshShop(Actor npc, GameState gs) 
  {
    int lastRefresh = npc.Stats[Attribute.InventoryRefresh].Curr;
    int turn = (int)(gs.Turn % int.MaxValue);

    if (Math.Abs(turn - lastRefresh) < 750)
      return;
    npc.Stats[Attribute.InventoryRefresh].SetMax(turn);

    List<Item> currStock = npc.Inventory.Items();

    foreach (Item item in currStock)
    {
      if (gs.Rng.NextDouble() < 0.2)
      {
        npc.Inventory.RemoveByID(item.ID);
        gs.ObjDb.RemoveItemFromGame(Loc.Nowhere, item);
      }
    }

    int newStock = gs.Rng.Next(1, 4);
    for (int j = 0; j < newStock; j++)
    {
      int roll = gs.Rng.Next(15);
      if (roll < 3)
        npc.Inventory.Add(ItemFactory.Get(ItemNames.TORCH, gs.ObjDb), npc.ID);
      else if (roll < 5)
        npc.Inventory.Add(ItemFactory.Get(ItemNames.POTION_HEALING, gs.ObjDb), npc.ID);
      else if (roll == 6)
        npc.Inventory.Add(ItemFactory.Get(ItemNames.ANTIDOTE, gs.ObjDb), npc.ID);
      else if (roll == 7)
        npc.Inventory.Add(ItemFactory.Get(ItemNames.SCROLL_DISARM, gs.ObjDb), npc.ID);
      else if (roll == 8)
        npc.Inventory.Add(ItemFactory.Get(ItemNames.SCROLL_BLINK, gs.ObjDb), npc.ID);
      else if (roll == 9)
        npc.Inventory.Add(ItemFactory.Get(ItemNames.SCROLL_KNOCK, gs.ObjDb), npc.ID);
      else if (roll == 10)
        npc.Inventory.Add(ItemFactory.Get(ItemNames.SCROLL_PROTECTION, gs.ObjDb), npc.ID);
      else if (roll == 11)
        npc.Inventory.Add(ItemFactory.Get(ItemNames.POTION_MIND_READING, gs.ObjDb), npc.ID);
      else if (roll == 12)
        npc.Inventory.Add(ItemFactory.Get(ItemNames.POTION_OF_LEVITATION, gs.ObjDb), npc.ID);
      else if (roll == 13)
        npc.Inventory.Add(ItemFactory.Get(ItemNames.SCROLL_TREASURE_DETECTION, gs.ObjDb), npc.ID);
      else if (roll == 14)
        npc.Inventory.Add(ItemFactory.Get(ItemNames.SCROLL_TREASURE_DETECTION, gs.ObjDb), npc.ID);
    }

    // Grocer always keeps a few torches in stock
    bool torchesInStock = npc.Inventory.Items().Any(i => i.Name == "torch");
    if (!torchesInStock)
    {
      for (int j = 0; j < gs.Rng.Next(2, 5); j++)
      {
        npc.Inventory.Add(ItemFactory.Get(ItemNames.TORCH, gs.ObjDb), npc.ID);
      }
    }
  }
}

class NPCBehaviour : IBehaviour, IDialoguer
{
  List<DialogueOption> Options { get; set; } = [];

  public virtual void InitDialogue(Mob actor, GameState gs) {}
  public virtual string GetBark(Mob actor, GameState gs) => "";

  public virtual (Action, Inputer?) Chat(Mob actor, GameState gameState)
  {
    if (gameState.Player.HasTrait<ShunnedTrait>())
    {
      return (new NullAction(), new PauseForMoreInputer(gameState));
    }

    Dialoguer acc = new(actor, gameState);

    // If no popup was created, it's probably a problem where I didn't 
    // calculate dialogue correctly, but this will handle it somewhat
    // gracefully
    if (!gameState.UIRef().ActivePopup)
    {
      return (new NullAction(), new PauseForMoreInputer(gameState));
    }

    CloseMenuAction action = new(gameState, 1.0);

    return (action, acc);
  }

  public virtual (string, string, List<(string, char)>) CurrentText(Mob mob, GameState gs)
  {
    string scriptFile = mob.Traits.OfType<DialogueScriptTrait>().First().ScriptFile;
    var dialogue = new DialogueInterpreter();

    var (txt, footer) = dialogue.Run(scriptFile, mob, gs);
    Options = dialogue.Options;
    List<(string, char)> opts = [..Options.Select(o => (o.Text, o.Ch))];
    
    return (txt, footer, opts);
  }

  public void SelectOption(Mob mob, char choice, GameState gs)
  {
    foreach (DialogueOption opt in Options)
    {
      if (opt.Ch == choice)
      {
        var dialogue = new DialogueInterpreter();
        dialogue.Run(opt.Expr, mob, gs);
        break;
      }
    }
  }

  public virtual bool ConfirmChoices(Actor npc, GameState gs) 
  {
    bool hasSelections = npc.Traits.OfType<NumberListTrait>()
                                   .Where(t => t.Name == "ShopSelections")
                                   .Any();
    return !hasSelections;
  }

  public virtual void RefreshShop(Actor npc, GameState gs) { }
}

class MayorBehaviour : NPCBehaviour
{
  DateTime _lastBark = new(1900, 1, 1);
  
  public override string GetBark(Mob actor, GameState gs)
  {
    string bark = "";

    if ((DateTime.UtcNow - _lastBark).TotalSeconds > 10)
    {
      _lastBark = DateTime.UtcNow;
      var (hour, _) = gs.CurrTime();
      if (hour >= 7 && hour < 19)
      {
        bark = "Today at least seems peaceful";
      }
      else if (gs.Town.Tavern.Contains(actor.Loc))
      {
        bark = gs.Rng.Next(3) switch
        {
          0 => "Maybe we should have a music festival in town?",
          1 => "Ah the sounds of cheer and commerce!",
          _ => "Drink and be merry, friends!"
        };        
      }
    }
    
    return bark;
  }
}

class WidowerBehaviour: NPCBehaviour
{
  DateTime _lastBark = new(1900, 1, 1);

  public override string GetBark(Mob actor, GameState gs)
  {
    if ((DateTime.UtcNow - _lastBark).TotalSeconds < 15)
      return "";
    _lastBark = DateTime.UtcNow;

    int state;
    if (actor.Stats.TryGetValue(Attribute.DialogueState, out var stateState))
      state = stateState.Curr;
    else
      state = 0;

    List<string> barks = [];
    if (state >= 4)
    {
      barks.Add("I miss you so!");
      barks.Add("Oh why did you have to be an adventurer?");
    }
    else
    {
      barks.Add("Sigh...");
      barks.Add("Are you safe?");
      barks.Add("When will you return?");
    }

    return barks[gs.Rng.Next(barks.Count)];
  }
}

class PrisonerBehaviour : NPCBehaviour
{
  public const int DIALOGUE_CAPTIVE = 0;
  public const int DIALOGUE_FREE = 1;
  public const int DIALOGUE_FREE_BOON = 2;
  public const int DIALOGUE_ESCAPING = 3;
  public const int DIALOGUE_AT_INN = 4;

  DateTime _lastBark = new(1900, 1, 1);

  public override string GetBark(Mob actor, GameState gs)
  {
    if ((DateTime.UtcNow - _lastBark).TotalSeconds <= 10)
      return "";

    _lastBark = DateTime.UtcNow;

    int dialogueState = actor.Stats[Attribute.DialogueState].Curr;
    string capturedBy = ((SimpleFact) gs.FactDb.FactCheck("ImprisonedBy")!).Value;
    return dialogueState switch
    {
      DIALOGUE_FREE => "Thank you!",
      DIALOGUE_FREE_BOON => "Hmm...which way to the exit?",
      DIALOGUE_AT_INN => gs.Rng.Next(3) switch
        {
          0 => "Fresh air at last!",
          1 => "Adventuring is for suckers.",
          _ => "I'm hanging up my sword."
        },        
      _ => gs.Rng.Next(3) switch
            {
              0 => $"I was captured by {capturedBy}!",
              1 => "Help me!",
              _ => "Can you free me?"
            }
    };
  }
}