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

using System.Text;

namespace Yarl2;

// The queue of actors to act will likely need to go here.
class GameState(Campaign c, Options opts, UserInterface ui, Rng rng)
{
  public Rng Rng { get; set; } = rng;
  public Options Options { get; set; } = opts;

  Player? _player { get; set; }
  public void SetPlayer(Player p) => _player = p;
  public Player Player => _player ?? throw new Exception("Player not set");

  public int CurrLevel { get; set; }
  public int CurrDungeonID { get; set; }
  public Campaign Campaign { get; set; } = c;
  public GameObjectDB ObjDb { get; set; } = new GameObjectDB();
  public ulong Turn { get; set; }
  public bool Tutorial { get; set; }
  public bool PlayerAFK { get; set; } = false; // Player is resting at inn, or paralyzed
  
  public Dictionary<Loc, (Colour, Colour, int, int)> LitSqs = [];
  public List<(Loc, Colour, Colour, int)> Lights { get; set; } = [];

  PerformersStack Performers { get; set; } = new();

  public HashSet<ulong> RecentlySeenMonsters { get; set; } = [];
  public HashSet<Loc> LastPlayerFoV = [];
  DijkstraMap? DMap { get; set; }
  DijkstraMap? DMapDoors { get; set; }
  DijkstraMap? DMapFlight { get; set; }
  DijkstraMap? DMapSwimming { get; set; }

  public DijkstraMap? GetDMap(string map = "")
  {
    if (DMap is null || DMapDoors is null || DMapFlight is null)
      SetDMaps(Player.Loc);

    return map switch
    {
      "doors" => DMapDoors,
      "flying" => DMapFlight,
      "swim" => DMapSwimming,
      _ => DMap
    };
  }

  public ulong LastTarget { get; set; } = 0;
  public FactDb FactDb => Campaign.FactDb ?? throw new Exception("FactDb should never be null!");

  UserInterface UI { get; set; } = ui;

  public void ClearMenu() => UI.CloseMenu();
  public UserInterface UIRef() => UI;

  public int MainQuestState => Player.Stats[Attribute.MainQuestState].Curr;

  public void ActorEntersLevel(Actor actor, int dungeonId, int level)
  {
    Map prevMap = CurrentMap;
    if (actor is Player && (prevMap.Features & MapFeatures.Foggy) != MapFeatures.None)
    {
      UI.ClearFoggyAnimation();
    }
    
    CurrLevel = level;
    CurrDungeonID = dungeonId;
    int maxDepth = Player.Stats[Attribute.Depth].Max;

    if (!InWilderness && level + 1 > maxDepth)
    {
      Player.Stats[Attribute.Depth] = new Stat(level + 1);
    }

    if (dungeonId == 1 && actor is Player)
    {
      // When the player reaches certain details for the first time, raise
      // their nerve.
      // Or maybe I should do it 50/level?
      if (level + 1 == 5 && maxDepth < 5)
      {
        Player.Stats[Attribute.Nerve].ChangeMax(250);
        Player.Stats[Attribute.Nerve].Change(250);
      }
    }

    if (CurrentMap.Alerts.Count > 0)
    {
      string alerts = string.Join(' ', CurrentMap.Alerts).Trim();
      UIRef().SetPopup(new Popup(alerts, "", 6, -1, alerts.Length));
      UIRef().AlertPlayer(alerts);
      CurrentMap.Alerts = [];
    }

    if (actor is Player && (CurrentMap.Features & MapFeatures.Foggy) != MapFeatures.None)
    {
      UI.RegisterAnimation(new FogAnimation(UI, this, CurrentMap.Height, CurrentMap.Width));
    }
    
    // If the player is returning to the overworld, is there any maintenance we need to do?
    if (actor is Player && dungeonId == 0)
    {
      RefreshOverworld();
    }
  }

  void RefreshOverworld()
  {
    SimpleFact fact = FactDb.FactCheck("SmithId") as SimpleFact ?? throw new Exception("SmithId should never be null!");
    ulong smithId = ulong.Parse(fact.Value);
    if (ObjDb.GetObj(smithId) is Mob smith)
    {
      ((NPCBehaviour)smith.Behaviour).RefreshShop(smith, this);
    }

    fact = FactDb.FactCheck("GrocerId") as SimpleFact ?? throw new Exception("GrocerId should never be null!");
    ulong grocerId = ulong.Parse(fact.Value);
    if (ObjDb.GetObj(grocerId) is Mob grocer)
    {
      ((NPCBehaviour)grocer.Behaviour).RefreshShop(grocer, this);
    }

    // Sometimes the witch is invisible after experimenting with one of their
    // partner's potions
    fact = FactDb.FactCheck("WitchId") as SimpleFact ?? throw new Exception("WitchId should not be null!");
    ulong witchId = ulong.Parse(fact.Value);
    if (ObjDb.GetObj(witchId) is Mob witch)
    {
      if (!witch.HasTrait<InvisibleTrait>() && Rng.NextDouble() < 0.2)
      {
        InvisibleTrait it = new() { ExpiresOn = Turn + (ulong)Rng.Next(500, 1000) };
        it.Apply(witch, this);

        witch.ClearPlan();
      }
    }

    fact = FactDb.FactCheck("AlchemistId") as SimpleFact ?? throw new Exception("AlchemistId should not be null!");
    ulong alchemistId = ulong.Parse(fact.Value);
    if (ObjDb.GetObj(alchemistId) is Mob alchemist)
    {
      ((NPCBehaviour)alchemist.Behaviour).RefreshShop(alchemist, this);
    }

    fact = FactDb.FactCheck("PeddlerId") as SimpleFact ?? throw new Exception("PeddlerId should not be null!");
    ulong peddlerId = ulong.Parse(fact.Value);
    if (ObjDb.GetObj(peddlerId) is Mob peddler)
    {
      // Sometimes the peddler will be out of town when the player returns from a dungeon
      // and sometimes he'll be back with more money to spend
      int timeSinceLastVisit = ((int)Turn % int.MaxValue) - peddler.Stats[Attribute.LastVisit].Curr;
      if (timeSinceLastVisit > 250 && peddler.Loc.DungeonID == 0 && Rng.NextDouble() < 0.5)
      {
        ObjDb.ClearActorLoc(peddler.Loc);
        peddler.Loc = Loc.Nowhere;
      }
      else if (timeSinceLastVisit > 250 && peddler.Loc == Loc.Nowhere && Rng.NextDouble() < 0.75)
      {
        List<Loc> tavernSqs = [.. Town.Tavern.Where(l => TileAt(l).Type == TileType.WoodFloor && !ObjDb.Occupied(l))];
        tavernSqs.Shuffle(Rng);
        peddler.Loc = tavernSqs[Rng.Next(tavernSqs.Count)];
        ObjDb.SetActorToLoc(peddler.Loc, peddler.ID);

        if (peddler.Inventory.Zorkmids < 50)
        {
          peddler.Inventory.Zorkmids = Rng.Next(51, 101);
        }
      }
    }
  }

  public Town Town => Campaign!.Town!;
  public Dungeon CurrentDungeon => Campaign!.Dungeons[CurrDungeonID];
  public Map CurrentMap => Campaign!.Dungeons[CurrDungeonID].LevelMaps[CurrLevel];
  public bool InWilderness => CurrDungeonID == 0;
  public Map Wilderness => Campaign.Dungeons[0].LevelMaps[0];
  public Map MapForLoc(Loc loc) => Campaign.Dungeons[loc.DungeonID].LevelMaps[loc.Level];
  public Map MapForActor(Actor a) => Campaign.Dungeons[a.Loc.DungeonID].LevelMaps[a.Loc.Level];

  public void RememberLoc(Loc loc, Tile tile)
  {
    Glyph glyph = Util.TileToGlyph(tile);
    CurrentDungeon.RememberedLocs[loc] = new(glyph, 0);
  }

  // I made life difficult for myself by deciding that Turn 0 of the game is 
  // 8:00am T_T 1 turn is 10 seconds (setting aside all concerns about 
  // realism and how the amount of stuff one can do in 10 seconds will in no 
  // way correspond to one action in the game...) so an hour is 360 turns
  public (int, int) CurrTime()
  {
    // There are 1440 turns/day
    int normalized = (int)(Turn + 480) % 1440;
    int hour = normalized / 60;
    int minute = normalized - (hour * 60);

    return (hour, minute);
  }

  public Tile TileAt(Loc loc)
  {
    Dungeon d = Campaign!.Dungeons[loc.DungeonID];
    Map map = d.LevelMaps[loc.Level];

    return map.InBounds(loc.Row, loc.Col)
                ? map.TileAt(loc.Row, loc.Col)
                : TileFactory.Get(TileType.Unknown);
  }

  public bool LocOpen(Loc loc)
  {
    if (!TileAt(loc).Passable())
      return false;

    if (ObjDb.Occupied(loc))
      return false;

    foreach (Item item in ObjDb.ItemsAt(loc))
    {
      if (item.HasTrait<BlockTrait>())
        return false;
    }

    return true;
  }

  public bool CanSeeLoc(Loc loc, int radius)
  {
    Map map = Campaign.Dungeons[loc.DungeonID].LevelMaps[loc.Level];
    Dictionary<Loc, int> fov = FieldOfView.CalcVisible(radius, loc, map, ObjDb);

    return fov.ContainsKey(loc) && fov[loc] != Illumination.None;
  }

  public bool LOSBetween(Loc a, Loc b)
  {
    if (a.DungeonID != b.DungeonID || a.Level != b.Level)
      return false;

    Map map = Campaign.Dungeons[a.DungeonID].LevelMaps[a.Level];
    foreach (var sq in Util.Bresenham(a.Row, a.Col, b.Row, b.Col))
    {
      if (!map.InBounds(sq) || map.TileAt(sq).Opaque())
        return false;
    }

    return true;
  }

  void SacrificeGoldToHuntokar(int amount, Loc loc)
  {
    if (amount >= 25)
    {
      string s = "Your sacrifice is accepted...and you are healed!";
      UI.AlertPlayer(s, this, loc);
      UI.SetPopup(new Popup(s, "", -1, -1));

      Player.Stats[Attribute.HP].Change(25);
    }
    else
    {
      UI.AlertPlayer("Your parsimony has been noted.", this, loc);
      UI.SetPopup(new Popup("Your parsimony has been noted.", "", -1, -1));
    }
  }

  void MakeTemporaryChasm(Loc loc)
  {
    Map map = Campaign.Dungeons[loc.DungeonID].LevelMaps[loc.Level];
    if (loc.DungeonID == 0 || (map.Features & MapFeatures.UndiggableFloor) != MapFeatures.None)
    {
      UI.AlertPlayer("A small ripple seems to pass over the terrain.", this, loc);
      return;
    }

    HashSet<Loc> tiles = [loc];
    foreach (Loc adj in Util.Adj8Locs(loc))
    {
      if (Rng.Next(5) < 4)
      {
        tiles.Add(adj);
        foreach (Loc aadj in Util.Adj8Locs(adj))
        {
          if (Rng.Next(6) <= 1)
            tiles.Add(aadj);
        }
      }
    }


    foreach (Loc puddleLoc in tiles)
    {
      Tile tile = TileAt(puddleLoc);
      
      switch (tile.Type)
      {
        case TileType.DungeonFloor:
        case TileType.Grass:
        case TileType.StoneFloor:
        case TileType.OpenDoor:
        case TileType.WoodBridge:
        case TileType.Bridge:
        case TileType.Dirt:
        case TileType.DeepWater:
        case TileType.FrozenDeepWater:
          Item chasm = ItemFactory.Chasm(this);
          ObjDb.SetToLoc(puddleLoc, chasm);
          ChasmCreated(puddleLoc);
          break;
        default:
          continue;
      }      
    }
  }

  void MakePuddleOfBooze(Loc loc)
  {
    HashSet<Loc> tiles = [loc];
    foreach (Loc adj in Util.Adj8Locs(loc))
    {
      if (Rng.Next(3) < 2)
      {
        tiles.Add(adj);
        foreach (Loc aadj in Util.Adj8Locs(adj))
        {
          if (Rng.Next(6) == 0)
            tiles.Add(aadj);
        }
      }
    }

    foreach (Loc puddleLoc in tiles)
    {
      if (!TileAt(puddleLoc).PassableByFlight())
        continue;

      Item booze = ItemFactory.PuddleOfBooze();
      ObjDb.Add(booze);
      ItemDropped(booze, puddleLoc);
    }
  }

  public void ItemDropped(Item item, Loc loc, bool thrown = false)
  {
    item.ContainedBy = 0;

    Tile tile = TileAt(loc);

    if (!tile.PassableByFlight())
    {
      loc = Util.NearestOpen(this, loc);
    }

    if (tile.Type == TileType.Chasm || ObjDb.EnvironmentsAt(loc).Where(e => e.HasTrait<TemporaryChasmTrait>()).Any())
    {
      string s = item.FullName.DefArticle().Capitalize();
      string v = item.Type == ItemType.Zorkmid && item.Value > 1 ? "tumble" : "tumbles";
      UI.AlertPlayer($"{s} {v} into darkness!", this, loc);

      int delta = CurrentDungeon.Descending ? 1 : -1;
      ItemDropped(item, loc with { Level = loc.Level + delta });
      return;
    }

    foreach (Item altar in ObjDb.ItemsAt(loc).Where(a => a.Type == ItemType.Altar))
    {
      if (altar.HasTrait<KoboldAltarTrait>() && loc == Player.Loc && Kobold.OfferGold(this, item, loc))
      {
        return;
      }
      else if (altar.HasTrait<HolyTrait>() && loc == Player.Loc)
      {
        SacrificeGoldToHuntokar(item.Value, loc);
        return;
      }
    }

    if (thrown && item.Name == "potion of descent")
    {
      ObjDb.RemoveItemFromGame(item.Loc, item);
      UI.AlertPlayer("The bottle shatters!", this, loc);
      MakeTemporaryChasm(loc);
      return;
    }

    if (thrown && item.Name == "flask of booze")
    {
      ObjDb.RemoveItemFromGame(item.Loc, item);
      UI.AlertPlayer("The bottle shatters!", this, loc);
      MakePuddleOfBooze(loc);
      return;
    }


    if (thrown && item.HasTrait<ExplosiveTrait>())
    {
      if (tile.Type == TileType.Water || tile.Type == TileType.DeepWater || tile.Type == TileType.Underwater)
      {
        Cmd.DefuseBomb(item, loc, this);
      }
      else 
      {
        Cmd.SetExplosive(item, this); 
      }
    }

    ObjDb.SetToLoc(loc, item);
    string msg = ThingTouchesFloor(loc);
    UI.AlertPlayer(msg);

    foreach (DamageType effect in tile.TerrainEffects())
    {
      var (s, _) = EffectApplier.Apply(effect, this, item, null);
      UI.AlertPlayer(s);
    }

    List<Trait> itemTraits = [.. item.Traits];
    foreach (Trait t in itemTraits)
    {
      if (t is DamageTrait dt && dt.DamageType == DamageType.Fire)
        ApplyDamageEffectToLoc(loc, DamageType.Fire);
    }

    if (tile.Type == TileType.Pit || tile.Type == TileType.HiddenPit)
    {
      item.Traits.Add(new InPitTrait());
    }

    if (tile is IdolAltar idolAltar && item.ID == idolAltar.IdolID)
    {
      Loc wallLoc = idolAltar.Wall;
      if (CurrentMap.TileAt(wallLoc.Row, wallLoc.Col).Type == TileType.DungeonWall)
      {
        CurrentMap.SetTile(wallLoc.Row, wallLoc.Col, TileFactory.Get(TileType.DungeonFloor));
        if (LastPlayerFoV.Contains(wallLoc))
          UI.AlertPlayer("As the idol touches the altar, a wall slides aside with a rumble.");
        else
          UI.AlertPlayer("You hear grinding stone.");
      }
    }

    // Special case to handle playing fetch with the village pup; it seemed like a lot of
    // code for something that might not have other use cases
    if (InWilderness && item.Name == "bone")
    {
      ulong pupId = FactDb.FactCheck("PupId") is SimpleFact pupFact ? ulong.Parse(pupFact.Value) : 0;
      if (ObjDb.GetObj(pupId) is Actor pup && CanSeeLoc(loc, 7))
      {
        pup.Stats[Attribute.MobAttitude] = new Stat(1);
      }
    }
  }

  public void ItemDestroyed(Item item, Loc loc)
  {
    if (item.ContainedBy > 0 && ObjDb.GetObj(item.ContainedBy) is Actor owner)
    {
      owner.Inventory.RemoveByID(item.ID, this);
    }

    ObjDb.RemoveItemFromGame(loc, item);

    if (item.Name == "dragon effigy")
    {
      Item zorkmids = ItemFactory.Get(ItemNames.ZORKMIDS, ObjDb);
      zorkmids.Value = Rng.Next(25, 76);
      ItemDropped(zorkmids, loc);
    }
  }

  // I don't have a way to track what square is below a bridge, so I have to do 
  // something kludgy. Maybe in the future I should turn bridges into Items
  // that can be walked on?
  TileType SquareBelowBridge(Loc start)
  {
    var q = new Queue<Loc>();
    q.Enqueue(start);
    HashSet<Loc> visited = [];

    while (q.Count > 0)
    {
      var curr = q.Dequeue();
      visited.Add(curr);

      foreach (var adj in Util.Adj4Locs(curr))
      {
        var tileType = TileAt(adj).Type;
        if (tileType == TileType.Chasm || tileType == TileType.DeepWater)
          return tileType;
        else if (tileType == TileType.WoodBridge && !visited.Contains(adj))
          q.Enqueue(adj);
      }
    }

    // This might happen if we had a bridge that was over exactly 1 square I guess?
    // I don't think that would/could be generated by my dungeon drawing alg though
    return TileType.DeepWater;
  }

  public void BridgeDestroyed(Loc loc)
  {
    TileType tile = SquareBelowBridge(loc);
    CurrentMap.SetTile(loc.Row, loc.Col, TileFactory.Get(tile));

    if (tile == TileType.Chasm)
      ChasmCreated(loc);
    else if (tile == TileType.DeepWater)
      BridgeDestroyedOverWater(loc);
  }

  public void ApplyDamageEffectToLoc(Loc loc, DamageType damageType)
  {
    List<Item> items = [];
    items.AddRange(ObjDb.ItemsAt(loc));
    items.AddRange(ObjDb.EnvironmentsAt(loc));
    var tile = TileAt(loc);
    bool fireStarted = false;

    Map map = Campaign.Dungeons[loc.DungeonID].LevelMaps[loc.Level];

    switch (damageType)
    {
      case DamageType.Fire:
        // Wooden bridges always burn for comedy reasons
        if (tile.Flammable() && (tile.Type == TileType.WoodBridge || Rng.NextDouble() < 0.15))
          fireStarted = true;

        if (tile.Type == TileType.FrozenWater)
        {
          map.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.Water));
          UI.AlertPlayer("The ice melts!", this, loc);
        }
        else if (tile.Type == TileType.FrozenDeepWater)
        {
          map.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.DeepWater));
          UI.AlertPlayer("The ice melts!", this, loc);
          BridgeDestroyedOverWater(loc);
        }
        else if (tile.Type == TileType.FrozenPool)
        {
          map.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.Pool));
          UI.AlertPlayer("The ice melts!", this, loc);
        }
        else if (tile.Type == TileType.FrozenLake)
        {
          map.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.Lake));
          UI.AlertPlayer("The ice melts!", this, loc);
        }

        foreach (var item in items)
        {
          if (item.HasTrait<FlammableTrait>())
          {
            UI.AlertPlayer($"{item.FullName.DefArticle().Capitalize()} burns up!", this, loc);
            ItemDestroyed(item, loc);
            fireStarted = true;
          }

          if (item.Name == "mud")
          {
            UI.AlertPlayer("The mud dries up.", this, loc);
            ItemDestroyed(item, loc);
          }
        }
        break;
      case DamageType.Cold:
        // Perhaps Cold can destroy poitions on the ground and such?

        if (tile.Type == TileType.Water)
        {
          map.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.FrozenWater));
          UI.AlertPlayer("The water freezes!");
        }
        else if (tile.Type == TileType.DeepWater)
        {
          map.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.FrozenDeepWater));
          UI.AlertPlayer("The water freezes!");
        }
        else if (tile.Type == TileType.Pool)
        {
          map.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.FrozenPool));
          UI.AlertPlayer("The pool freezes!");
        }
        else if (tile.Type == TileType.Lake)
        {
          map.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.FrozenLake));
          UI.AlertPlayer("The water freezes!");
        }
        break;
      default:
        break;
    }

    if (fireStarted)
    {
      var fire = ItemFactory.Fire(this);
      ObjDb.SetToLoc(loc, fire);

      if (tile.Type == TileType.Grass)
      {
        map.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.CharredGrass));
      }
      else if (tile.IsTree())
      {
        map.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.CharredStump));
      }
      else if (tile.Type == TileType.WoodBridge)
      {
        if (LastPlayerFoV.Contains(Player.Loc))
          UI.AlertPlayer("The bridge burns up and collapses!");

        BridgeDestroyed(loc);
      }
    }
  }

  void ActorFallsIntoWater(Actor actor, Loc loc)
  {
    if (actor.HasTrait<IllusionTrait>())
      return;

    UI.AlertPlayer($"{actor.FullName.Capitalize()} {Grammar.Conjugate(actor, "fall")} into the water!");

    FallIntoWater(actor, loc);
  }

  void BridgeDestroyedOverWater(Loc loc)
  {
    if (ObjDb.Occupant(loc) is Actor actor)
    {// && !(actoractor.HasActiveTrait<FlyingTrait>() || actor.HasActiveTrait<FloatingTrait>())
      bool fallsIn = true;
      foreach (Trait t in actor.Traits)
      {
        if (t is FlyingTrait || t is FloatingTrait || t is WaterWalkingTrait)
        {
          fallsIn = false;
          break;
        }
      }

      if (fallsIn)
        ActorFallsIntoWater(actor, loc);
    }

    var itemsToFall = ObjDb.ItemsAt(loc);
    foreach (var item in itemsToFall)
    {
      UI.AlertPlayer($"{item.Name.DefArticle().Capitalize()} sinks!");
      ObjDb.RemoveItemFromLoc(loc, item);
      ItemDropped(item, loc);
    }
  }

  public void ChasmCreated(Loc loc)
  {
    Loc landingSpot = loc with { Level = loc.Level + 1 };

    if (ObjDb.Occupant(loc) is Actor actor && !(actor.HasActiveTrait<FlyingTrait>() || actor.HasActiveTrait<FloatingTrait>()))
    {
      FallIntoChasm(actor, landingSpot);
    }

    var itemsToFall = ObjDb.ItemsAt(loc);
    foreach (var item in itemsToFall)
    {
      ObjDb.RemoveItemFromLoc(loc, item);
      // Actually 'touching' the chasm and falling to the next level are
      // handled in ItemDropped()
      ItemDropped(item, loc);
    }
  }

  void BreakGrapple(Actor actor)
  {
    GrappledTrait? grappled = actor.Traits.OfType<GrappledTrait>().FirstOrDefault();
    grappled?.Remove(this);
    GrapplingTrait? grappling = actor.Traits.OfType<GrapplingTrait>().FirstOrDefault();
    if (grappling is not null && ObjDb.GetObj(grappling.VictimId) is Actor victim)
    {
      grappled = victim.Traits.OfType<GrappledTrait>().FirstOrDefault();
      grappled?.Remove(this);
    }
  }

  public void FallIntoWater(Actor actor, Loc loc)
  {
    List<string> messages = [];

    // When someone jumps/falls into water, they wash ashore at a random loc
    // and incur the Exhausted condition first, find candidate shore sqs
    Queue<Loc> q = new();
    q.Enqueue(loc);
    HashSet<Loc> visited = [];
    HashSet<Loc> shores = [];

    // if the actor falls into water, break any grapple they are under
    BreakGrapple(actor);

    // Build set of potential places for the actor to wash ashore
    while (q.Count > 0)
    {
      var curr = q.Dequeue();

      if (visited.Contains(curr))
        continue;

      visited.Add(curr);
      foreach (var adj in Util.Adj8Locs(curr))
      {
        var tile = TileAt(adj);
        if (tile.Passable() && !ObjDb.Occupied(adj))
        {
          shores.Add(adj);
        }
        else if (tile.Type == TileType.DeepWater && !visited.Contains(adj))
        {
          q.Enqueue(adj);
        }
      }
    }

    if (shores.Count > 0)
    {
      List<Loc> candidates = [.. shores];
      var destination = candidates[Rng.Next(candidates.Count)];
      ResolveActorMove(actor, actor.Loc, destination);
      actor.Loc = destination;

      string invMsgs = actor.Inventory.ApplyEffectToInv(DamageType.Wet, this, actor.Loc);
      if (invMsgs.Length > 0)
      {
        messages.Add(invMsgs);
      }

      int conMod;
      if (actor.Stats.TryGetValue(Attribute.Constitution, out var stat))
        conMod = stat.Curr;
      else
        conMod = 0;
      ulong endsOn = Turn + (ulong)(250 - 10 * conMod);
      ExhaustedTrait exhausted = new()
      {
        OwnerID = actor.ID,
        ExpiresOn = endsOn
      };
      List<string> msgs = exhausted.Apply(actor, this);
      foreach (string msg in msgs)
      {
        messages.Add(msg);
      }

      if (LastPlayerFoV.Contains(destination))
        messages.Add($"{actor.FullName.Capitalize()} {Grammar.Conjugate(actor, "wash")} ashore, gasping for breath!");
    }
    else
    {
      // What happens if there are somehow no free shore sqs? Does the mob drown??
    }

    UI.AlertPlayer(string.Join(' ', messages).Trim());
  }

  Loc NearestUnoccupied(Loc loc)
  {
    HashSet<Loc> visited = [];
    Queue<Loc> spots = [];
    spots.Enqueue(loc);

    while (spots.Count > 0)
    {
      Loc spot = spots.Dequeue();
      visited.Add(spot);
      if (!ObjDb.Occupied(spot))
        return spot;

      foreach (Loc adj in Util.Adj8Locs(spot))
      {
        if (TileAt(adj).Passable() && !visited.Contains(adj))
          spots.Enqueue(adj);
      }
    }

    return Loc.Nowhere;
  }

  public void FallIntoChasm(Actor actor, Loc landingSpot)
  {
    Loc CalcFinalLandingSpot(Loc landingSpot)
    {
      Dungeon dungeon = Campaign.Dungeons[landingSpot.DungeonID];
      int delta = dungeon.Descending ? 1 : -1;
      while (true)
      {
        Map map = dungeon.LevelMaps[landingSpot.Level];
        landingSpot = Util.NearestOpen(this, landingSpot);

        if (map.TileAt(landingSpot.Row, landingSpot.Col).Type != TileType.Chasm)
          return landingSpot;
        landingSpot = landingSpot with { Level = landingSpot.Level + delta };

        if (dungeon.Descending && landingSpot.Level == dungeon.LevelMaps.Count)
          break;
        else if (!dungeon.Descending && landingSpot.Level == 0)
          break;
      }

      // Possibly I should just throw an exception here? This would be an error
      // condition, most likely in level generation
      return Loc.Nowhere;
    }

    if (actor.HasTrait<IllusionTrait>())
      return;

    // if the actor falls into a chasm, break any grapple they are participating in
    BreakGrapple(actor);

    bool featherFalling = actor.HasTrait<FeatherFallTrait>();

    landingSpot = CalcFinalLandingSpot(landingSpot);
    int levelsFallen = Math.Abs(landingSpot.Level - actor.Loc.Level);

    if (featherFalling)
      UI.AlertPlayer($"{actor.FullName.Capitalize()} {Grammar.Conjugate(actor, "drift")} downward into the darkness.");
    else
      UI.AlertPlayer($"{actor.FullName.Capitalize()} {Grammar.Conjugate(actor, "fall")} into the chasm!");

    if (actor is Player)
    {
      if (levelsFallen > 1 && !featherFalling)
        UI.AlertPlayer("You plummet a great distance!");
      ActorEntersLevel(actor, landingSpot.DungeonID, landingSpot.Level);
    }

    if (ObjDb.Occupied(landingSpot))
    {
      landingSpot = NearestUnoccupied(landingSpot);
    }

    ResolveActorMove(actor, actor.Loc, landingSpot);

    if (actor is Player)
    {
      FlushPerformers();
    }

    if (!featherFalling)
    {
      CalculateFallDamage(actor, levelsFallen);
    }
  }

  public void ActorKilled(Actor victim, string killedBy, GameObj? attacker)
  {
    bool locVisible = false;
    if (LastPlayerFoV.Contains(victim.Loc))
    {
      locVisible = true;
    }
    else if (attacker is not null && attacker.HasTrait<SwallowedTrait>() && victim.Traits.OfType<FullBellyTrait>().FirstOrDefault() is FullBellyTrait fbt)
    {
      if (fbt.VictimID == attacker.ID)
        locVisible = true;
    }

    if (victim is Player)
    {
      // Play any queued explosions in case it was one of the explosions
      // that killed the player
      UI.PlayQueuedExplosions(this);

      PlayerKilledException pke = new();
      pke.Messages.Add(killedBy);
      throw pke;
    }
    else if (victim.HasTrait<FinalBossTrait>())
    {
      UI.VictoryScreen(victim.FullName, this);
      throw new VictoryException();
    }
    else if (locVisible && victim.Traits.OfType<DeathMessageTrait>().FirstOrDefault() is DeathMessageTrait dmt)
    {
      UI.AlertPlayer(dmt.Message);
    }
    else if (locVisible)
    {
      UI.AlertPlayer(MsgFactory.MobKilledMessage(victim, attacker, this));
    }

    // Was anything listening for the the victims death?
    // Making a copy is the easiest way to deal with the collection being
    // modified by the alert
    List<(ulong, IGameEventListener)> deathListeners = [.. ObjDb.DeathWatchListeners];
    foreach (var (targetID, listener) in deathListeners)
    {
      if (targetID == victim.ID)
      {
        listener.EventAlert(GameEventType.Death, this, victim.Loc);
        ObjDb.DeathWatchListeners = [.. ObjDb.DeathWatchListeners.Where(w => w.Item1 != victim.ID)];
      }
    }

    RemovePerformerFromGame(victim);

    bool villager = false;
    foreach (Trait t in victim.Traits)
    {
      if (t is DropTrait d)
      {
        if (Rng.Next(100) < d.Chance && Enum.TryParse(d.ItemName.ToUpper(), out ItemNames itemName))
        {
          Item item = ItemFactory.Get(itemName, ObjDb);
          ItemDropped(item, victim.Loc);
        }
      }
      else if (t is RetributionTrait rt)
      {
        HandleRetribution(victim, rt);
      }
      else if (t is VillagerTrait)
      {
        villager = true;
      }

      if (t is IGameEventListener el)
        RemoveListener(el);
    }

    if (!villager)
    {
      if (victim.Inventory.Zorkmids > 0)
      {
        Item zorkmids = ItemFactory.Get(ItemNames.ZORKMIDS, ObjDb);
        zorkmids.Value = victim.Inventory.Zorkmids;
        ItemDropped(zorkmids, victim.Loc);
      }

      foreach (Item item in victim.Inventory.Items())
      {
        item.Slot = '\0';
        item.Equipped = false;
        ItemDropped(item, victim.Loc);
      }
    }

    if (victim.Traits.OfType<PolymorphedTrait>().FirstOrDefault() is PolymorphedTrait pt)
    {
      if (ObjDb.GetObj(pt.OriginalId) is Actor originalForm)
      {
        ObjDb.ActorMoved(originalForm, originalForm.Loc, victim.Loc);
        RefreshPerformers();

        if (LastPlayerFoV.Contains(victim.Loc))
        {
          string s = $"{victim.FullName.Capitalize()} {Grammar.Conjugate(victim, "turn")} back into {originalForm.Name.IndefArticle()}!";
          UI.AlertPlayer(s);
          UI.SetPopup(new Popup(s, "", -1, -1));
        }
      }
      else
      {
        throw new Exception("Can't find original form for polymorphed creature");
      }
    }

    if (attacker is not null && attacker.HasTrait<CroesusTouchTrait>() && Rng.NextDouble() < 0.33)
    {
      Item zorkmids = ItemFactory.Get(ItemNames.ZORKMIDS, ObjDb);
      zorkmids.Value = Rng.Next(10, 21);
      ItemDropped(zorkmids, victim.Loc);

      if (LastPlayerFoV.Contains(victim.Loc))
      {
        UI.AlertPlayer("Coins tumble to the ground.");
      }
    }

    foreach (Item item in ObjDb.ItemsAt(victim.Loc))
    {
      if (item.HasTrait<MolochAltarTrait>())
      {
        HandleSacrifice(victim, victim.Loc);
        break;
      }
    }
  }

  public void RemovePerformerFromGame(Actor performer)
  {
    ObjDb.RemoveActor(performer);
    Performers.Remove(performer.ID);
  }

  void HandleSacrifice(Actor victim, Loc altarLoc)
  {
    bool rejected = false;
    foreach (Trait t in victim.Traits)
    {
      if (t is UndeadTrait)
      {
        rejected = true;
        break;
      }
      else if (t is PlantTrait)
      {
        rejected = true;
        break;
      }
      else if (t is BrainlessTrait)
      {
        rejected = true;
        break;
      }
    }

    if (rejected)
    {
      UIRef().SetPopup(new Popup("Insult me not with this dross!", "", -1, -1));
      return;
    }

    // So long as the player is adjacent to the altar, they'll get the credit
    // for the sacrifice
    bool playerAdj = false; ;
    foreach (Loc adj in Util.Adj8Locs(altarLoc))
    {
      if (adj == Player.Loc)
      {
        playerAdj = true;
        break;
      }
    }

    if (!playerAdj)
      return;

    InfernalBoons.Sacrifice(this, altarLoc);
  }

  void HandleDissolveToPuddle(Actor src, DamageType dmgType, string name)
  {
    UI.AlertPlayer($"{src.FullName.Capitalize()} {Grammar.Conjugate(src, "collapse")} into a puddle of {name}!");

    List<Loc> sqs = [src.Loc];
    foreach (Loc adj in Util.Adj8Locs(src.Loc))
    {
      if (Rng.Next(3) < 2)
        sqs.Add(adj);
    }

    foreach (Loc loc in sqs)
    {
      if (!TileAt(loc).Passable())
        continue;

      Item puddle = dmgType switch
      {
        DamageType.Grease => ItemFactory.PuddleOfGrease(),
        _ => ItemFactory.PuddleOfMud()
      };

      ObjDb.Add(puddle);
      ItemDropped(puddle, loc);
    }
  }
  
  void HandleRetribution(Actor src, RetributionTrait retribution)
  {
    if (retribution.Type == DamageType.Mud || retribution.Type == DamageType.Grease)
    {
      HandleDissolveToPuddle(src, retribution.Type, retribution.Type.ToString().ToLower());

      return;
    }

    string dmgDesc = retribution.Type.ToString().ToLower();
    string txt = $"{src.FullName.Capitalize()} {Grammar.Conjugate(src, "explode")}";
    if (dmgDesc == "cold" || dmgDesc == "fire")
      txt += $" in a blast of {dmgDesc}!";
    txt += "!";

    UI.AlertPlayer(txt, this, src.Loc);

    int dmg = 0;
    for (int i = 0; i < retribution.NumOfDice; i++)
      dmg += Rng.Next(retribution.DmgDie) + 1;
    HashSet<Loc> pts = Util.FloodFill(this, src.Loc, retribution.Radius, []);
    Animation anim = retribution.Type switch
    {
      DamageType.Cold => new ExplosionAnimation(this) { MainColour = Colours.LIGHT_BLUE, AltColour1 = Colours.ICE_BLUE, AltColour2 = Colours.BLUE, Highlight = Colours.WHITE, Centre = src.Loc, Sqs = pts },
      DamageType.Fire => new ExplosionAnimation(this) { MainColour = Colours.BRIGHT_RED, AltColour1 = Colours.YELLOW, AltColour2 = Colours.YELLOW_ORANGE, Highlight = Colours.WHITE, Centre = src.Loc, Sqs = pts },
      _ => new ExplosionAnimation(this) { MainColour = Colours.GREY, AltColour1 = Colours.LIGHT_GREY, AltColour2 = Colours.BROWN, Highlight = Colours.WHITE, Centre = src.Loc, Sqs = pts },
    };
    UI.PlayAnimation(anim, this);

    foreach (Loc pt in pts)
    {
      if (ObjDb.Occupant(pt) is Actor actor)
      {
        UI.AlertPlayer($"{actor.FullName.Capitalize()} {Grammar.Conjugate(actor, "is")} caught in the blast!");
        var (hpLeft, msg, _) = actor.ReceiveDmg([(dmg, retribution.Type)], 0, this, null, 1.0);
        UI.AlertPlayer(msg);

        if (hpLeft < 1)
        {
          ActorKilled(actor, MsgFactory.KillerName(src, Player), src);
        }
      }
      ApplyDamageEffectToLoc(pt, retribution.Type);
    }
  }

  public void PushPerformer(Actor actor) => Performers.Push(actor);
  public void FlushPerformers() => Performers.Flush();

  void RefreshPerformers()
  {
    Performers.Flush();
    foreach (Actor actor in ObjDb.GetPerformers(CurrDungeonID, CurrLevel))
    {
      // I cannot remember why I am doing the double.Max() call here D:  
      // Was I worried status effects might end up giving someone negative
      // recovery?
      actor.Energy += double.Max(0.0, actor.Recovery);

      if (actor.Energy >= 1.0)
        Performers.Push(actor);
    }
  }

  public Actor? NextPerformer()
  {
    if (Performers.Count == 0)
    {
      EndOfTurn();
      RefreshPerformers();
    }

    Actor? next = null;
    if (Performers.Pop() is Actor a)
    {
      next = a;
    }

    // next might be null in a weird situation like every single actor on the 
    // level has recovery of less than 1.0. So RefreshPerformers() will keep
    // getting called until *someone* has enough energy to act.

    return next;
  }

  void EndOfTurn()
  {
    bool IsActiveListener(GameObj obj)
    {
      if (obj.Loc == Loc.Nowhere)
        return true;
      if (obj.Loc.DungeonID == CurrDungeonID && obj.Loc.Level == CurrLevel)
        return true;

      return false;
    }

    ++Turn;

    // I'm not sure yet what a good monster gen rate is, and what in-game
    // conditions should affect it
    if ((CurrentMap.Features & MapFeatures.NoRandomEncounters) != MapFeatures.None && Rng.Next(60) == 0)
    {
      SpawnMonster();
    }

    // Note to self: you build the list like this because as part of their
    // EventAlert() call, a listener might remove itself from the list of
    // listeners
    List<IGameEventListener> listeners = [];
    foreach (var listener in ObjDb.EndOfRoundListeners.Where(l => !l.Expired))
    {
      if (ObjDb.GetObj(listener.ObjId) is GameObj obj && IsActiveListener(obj))
      {
        listeners.Add(listener);
      }
    }

    foreach (var listener in listeners)
    {
      listener.EventAlert(GameEventType.EndOfRound, this, Loc.Nowhere);
    }
    ObjDb.EndOfRoundListeners = [.. ObjDb.EndOfRoundListeners.Where(l => !l.Expired)];

    foreach (var ce in ObjDb.ConditionalEvents)
    {
      if (ce.CondtionMet(this))
      {
        ce.Fire(this);
        ce.Complete = true;
      }
    }

    if (!UI.InTutorial)
      CheckForStress();

    ObjDb.ConditionalEvents = [.. ObjDb.ConditionalEvents.Where(ce => !ce.Complete)];

    PrepareFieldOfView();

    if (UI.PauseForResponse)
    {
      UI.BlockFoResponse(this);
      UI.PauseForResponse = false;
      UI.ClosePopup();
    }
  }

  void CheckForStress()
  {
    if (InWilderness)
    {
      var (hour, _) = CurrTime();
      if (hour >= 5 && hour < 21)
        Player.Stats[Attribute.Nerve].Change(1);
    }
    else if (!Player.HasTrait<HeroismTrait>())
    {
      // The player accrues stress more slowly on levels they've already
      // explored
      int maxDepth = Player.Stats[Attribute.Depth].Curr - 1;
      if (CurrLevel < maxDepth && Turn % 4 == 0)
        return;

      // limit how stressed the player will get depending on how deep we are
      int stresssFloor = CurrLevel switch
      {
        0 or 1 or 2 => 601,
        3 or 4 or 5 => 301,
        6 or 7 or 8 => 151,
        _ => 0
      };
      int curr = Player.Stats[Attribute.Nerve].Curr;
      if (curr > stresssFloor)
      {
        int delta = Player.TotalLightRadius() < 2 ? -2 : -1;
        Player.Stats[Attribute.Nerve].Change(delta);
      }
    }

    Player.CalcStress();
  }

  void SpawnMonster()
  {
    if (LevelAppropriateMonster(CurrDungeonID, CurrLevel) is not Actor monster)
      return;

    List<Loc> openLoc = [];
    Map map = CurrentMap;
    for (int r = 0; r < map.Height; r++)
    {
      for (int c = 0; c < map.Width; c++)
      {
        Loc loc = new(CurrDungeonID, CurrLevel, r, c);

        if (map.TileAt(r, c).Type != TileType.DungeonFloor)
          continue;
        if (ObjDb.Occupied(loc) || ObjDb.AreBlockersAtLoc(loc))
          continue;

        // This prevents a monster from being spawned on top of a campfire, lol
        var items = ObjDb.ItemsAt(loc);
        if (items.Count > 0 && items.Any(i => i.HasTrait<AffixedTrait>()))
          continue;

        openLoc.Add(loc);
      }
    }

    if (openLoc.Count == 0)
      return;

    // prefer spawning the monster where the player can't see it
    List<Loc> outOfSight = [.. openLoc.Where(l => !LastPlayerFoV.Contains(l))];
    if (outOfSight.Count > 0)
      openLoc = outOfSight;

    Loc spawnPoint = openLoc[Rng.Next(openLoc.Count)];
    monster.Loc = spawnPoint;
    ObjDb.AddNewActor(monster, spawnPoint);
  }

  public Actor? LevelAppropriateMonster(int dungeonId, int level)
  {
    Dungeon dungeon = Campaign.Dungeons[dungeonId];

    int monsterLevel = level;
    if (monsterLevel > 0)
    {
      double roll = Rng.NextDouble();
      if (roll > 0.95)
        monsterLevel += 2;
      else if (roll > 0.8)
        monsterLevel += 1;
      if (monsterLevel > dungeon.LevelMaps.Count)
        monsterLevel = dungeon.LevelMaps.Count;
    }

    monsterLevel = int.Min(monsterLevel, dungeon.MonsterDecks.Count - 1);
    if (monsterLevel == -1 || monsterLevel >= dungeon.MonsterDecks.Count)
      return null;

    MonsterDeck deck = dungeon.MonsterDecks[monsterLevel];
    if (deck.Indexes.Count == 0)
      deck.Reshuffle(Rng);
    string m = deck.Monsters[deck.Indexes.Dequeue()];

    return MonsterFactory.Get(m, ObjDb, Rng);
  }

  public string RandomMonster(int dungeonId)
  {
    if (dungeonId == 0)
    {
      // I don't yet have a monster deck for the wildneress
      return Rng.NextDouble() < 0.5 ? "wolf" : "dire bat";
    }

    Dungeon dungeon = Campaign.Dungeons[dungeonId];
    MonsterDeck deck = dungeon.MonsterDecks[Rng.Next(dungeon.MonsterDecks.Count)];

    return deck.Monsters[Rng.Next(deck.Monsters.Count)];
  }

  Dictionary<(int, int), int> _extraCosts = [];
  public void SetDMaps(Loc loc)
  {    
    // Mold, fire, etc shouldn't incur extra costs for brainless monsters
    // but I'd have to do individual pathfinding at that point
    foreach (GameObj obj in ObjDb.ObjectsOnLevel(loc.DungeonID, loc.Level))
    {
      foreach (Trait t in obj.Traits)
      {
        if (t is BlockTrait)
        {
          _extraCosts[(obj.Loc.Row, obj.Loc.Col)] = DijkstraMap.IMPASSABLE;
        }
        else if (t is OnFireTrait)
        {
          (int, int) sq = (obj.Loc.Row, obj.Loc.Col);
          _extraCosts[sq] = _extraCosts.GetValueOrDefault(sq, 0) + 15;
        }
        else if (t is MoldSporesTrait)
        {
          (int, int) sq = (obj.Loc.Row, obj.Loc.Col);
          _extraCosts[sq] = _extraCosts.GetValueOrDefault(sq, 0) + 5;
        }
      }
    }

    foreach (Loc occ in ObjDb.OccupantsOnLevel(loc.DungeonID, loc.Level))
    {
      _extraCosts[(occ.Row, occ.Col)] = DijkstraMap.IMPASSABLE;
    }

    DMap = new(CurrentMap, _extraCosts, CurrentMap.Height, CurrentMap.Width, false);
    DMap.Generate(DijkstraMap.Cost, (loc.Row, loc.Col), 25);

    // I wonder how complicated it would be to generate the maps in parallel...
    DMapDoors = new(CurrentMap, _extraCosts, CurrentMap.Height, CurrentMap.Width, false);
    DMapDoors.Generate(DijkstraMap.CostWithDoors, (loc.Row, loc.Col), 25);

    DMapFlight = new(CurrentMap, _extraCosts, CurrentMap.Height, CurrentMap.Width, false);
    DMapFlight.Generate(DijkstraMap.CostByFlight, (loc.Row, loc.Col), 25);

    DMapSwimming = new(CurrentMap, _extraCosts, CurrentMap.Height, CurrentMap.Width, false);
    DMapSwimming.Generate(DijkstraMap.CostForSwimming, (loc.Row, loc.Col), 25);
  }

  // At the moment I can't use ResolveActorMove because it calls
  // ObjDb.ActorMoved() which clears out GameObjDb's memory of who
  // is at a particular location, which doesn't work while trying to
  // swap two Mobs. If I change that, I can get rid of some repeated
  // code in this method
  public void SwapActors(Actor a, Actor b)
  {
    Loc startA = a.Loc;
    Loc startB = b.Loc;

    ObjDb.ClearActorLoc(a.Loc);
    ObjDb.ClearActorLoc(b.Loc);

    a.Loc = startB;
    ObjDb.SetActorToLoc(startB, a.ID);

    b.Loc = startA;
    ObjDb.SetActorToLoc(startA, b.ID);
  }

  public Loc FallIntoTrapdoor(Actor actor, Loc trapdoorLoc)
  {
    // Find candidates for the landing spot. We'll look for tiles that are 
    // reachable from the stairs up (to avoid situations where the player 
    // lands in a locked vault, etc) and are unoccupied.
    int levelDelta = CurrentDungeon.Descending ? 1 : -1;
    Map lowerLevel = CurrentDungeon.LevelMaps[trapdoorLoc.Level + levelDelta];
    TileType egressStairs = CurrentDungeon.Descending ? TileType.Upstairs : TileType.Downstairs;
    Loc stairs = Loc.Nowhere;
    bool found = false;
    for (int r = 0; r < lowerLevel.Height; r++)
    {
      for (int c = 0; !found && c < lowerLevel.Width; c++)
      {
        if (lowerLevel.TileAt(r, c).Type == egressStairs)
        {
          stairs = new(CurrDungeonID, trapdoorLoc.Level + levelDelta, r, c);
          found = true;
          break;
        }
      }
    }

    HashSet<TileType> doors = [TileType.ClosedDoor, TileType.LockedDoor, TileType.SecretDoor];
    List<Loc> candidateSpots = [..Util.FloodFill(this, stairs, lowerLevel.Height, doors)
                                      .Where(loc => !ObjDb.Occupied(loc))];

    if (candidateSpots.Count > 0)
    {
      // NB, this code is basically the same as in FaillIntoChasm
      Loc landingSpot = candidateSpots[Rng.Next(candidateSpots.Count)];

      if (actor is Player)
      {
        ActorEntersLevel(actor, landingSpot.DungeonID, landingSpot.Level);
      }

      ResolveActorMove(actor, actor.Loc, landingSpot);

      CalculateFallDamage(actor, 1);

      FlushPerformers();
    }
    else
    {
      // huh, what should happen if the level below is completely full??
    }

    return actor.Loc;
  }

  void CalculateFallDamage(Actor actor, int levelsFallen)
  {
    int fallDamage = Rng.Next(1, 7);
    for (int j = 0; j < levelsFallen; j++)
    {
      fallDamage += Rng.Next(1, 7);
    }

    LameTrait lame = new() { OwnerID = actor.ID, ExpiresOn = Turn + (ulong)Rng.Next(100, 151) };
    lame.Apply(actor, this);

    var (hpLeft, _, _) = actor.ReceiveDmg([(fallDamage, DamageType.Blunt)], 0, this, null, 1.0);
    if (hpLeft < 1)
    {
      ActorKilled(actor, "a fall", null);
    }

    if (LastPlayerFoV.Contains(actor.Loc) || actor is Player)
    {
      string s = $"{actor.FullName.Capitalize()} {Grammar.Conjugate(actor, "is")} injured by the fall!";
      UI.AlertPlayer(s);
    }
  }

  public void ResolveActorMove(Actor actor, Loc start, Loc dest)
  {
    ObjDb.ActorMoved(actor, start, dest);

    // Not making dijkstra maps for the otherworld just yet.
    // Eventually I need to take into account whether or not
    // monsters can open doors, fly, etc. Multiple maps??
    if (actor is Player && dest.DungeonID > 0)
    {
      SetDMaps(dest);
    }

    Map map = Campaign.Dungeons[dest.DungeonID].LevelMaps[dest.Level];
    Tile tile = map.TileAt(dest.Row, dest.Col);
    bool flying = actor.HasActiveTrait<FlyingTrait>() || actor.HasActiveTrait<FloatingTrait>();
    bool waterWalking = actor.HasActiveTrait<WaterWalkingTrait>();

    // At the moment ThingTouchesFLoor (formerly ThingAddedToLoc) only handles
    // floor triggers, but if it starts doing more I'll have to split it into
    // seperate moethods, or actually pass the object in
    if (!flying)
    {
      string s = ThingTouchesFloor(dest);
      UI.AlertPlayer(s);
    }

    if (actor is Player && tile.Type == TileType.MistyPortal)
    {
      Loc exitPoint = CurrentDungeon.ExitLoc;
      ActorEntersLevel(Player, 0, 0);
      ResolveActorMove(Player, Player.Loc, exitPoint);      
      FlushPerformers();
      PrepareFieldOfView();

      UI.AlertPlayer("You step into the misty portal and become momentarily disoeriented.");
      UI.SetPopup(new Popup("You step into the misty portal and become momentarily disoeriented.", "", -1, -1));
    }

    if (actor is Player && tile.Type == TileType.MysteriousMirror)
    {
      MysteriousMirror mm = (MysteriousMirror)TileAt(dest);
      Loc mirrorDest = mm.Destination;
      Loc actualDest = Util.NearestUnoccupiedLoc(this, mirrorDest);
      ActorEntersLevel(Player, actualDest.DungeonID, actualDest.Level);
      ResolveActorMove(Player, Player.Loc, actualDest);      
      FlushPerformers();
      PrepareFieldOfView();

      string s = "The strange mirror draws you inside it!";
      UI.AlertPlayer(s);
      if (start.DungeonID != mirrorDest.DungeonID && CurrentDungeon.ArrivalMessage != "")
      {
        s += $"\n\n{CurrentDungeon.ArrivalMessage}";
        UI.AlertPlayer(CurrentDungeon.ArrivalMessage);
      }
      
      UI.SetPopup(new Popup(s, "", -1, -1));
    }
    
    if (tile.IsTrap())
    {
      Traps.TriggerTrap(this, actor, dest, tile, flying);
    }
    else if (tile.Type == TileType.Chasm && !flying)
    {
      Loc landingSpot = dest with { Level = dest.Level + 1 };
      FallIntoChasm(actor, landingSpot);
    }
    else if (tile.Type == TileType.DeepWater && !(flying || waterWalking))
    {
      ActorFallsIntoWater(actor, dest);
    }
    else if ((map.Features & MapFeatures.Submerged) != MapFeatures.None)
    {
      string msg = actor.Inventory.ApplyEffectToInv(DamageType.Wet, this, actor.Loc);
      UI.AlertPlayer(msg);
    }
    else if (tile.Type == TileType.Lake && !(flying || waterWalking))
    {
      string msg = actor.Inventory.ApplyEffectToInv(DamageType.Wet, this, actor.Loc);
      UI.AlertPlayer(msg);
    }
  }

  public string ThingTouchesFloor(Loc loc)
  {
    List<string> messages = [];

    if (ObjDb.LocListeners.Contains(loc) && CurrentMap.TileAt(loc.Row, loc.Col) is IGameEventListener trigger)
    {
      trigger.EventAlert(GameEventType.LocChanged, this, loc);
    }

    if (LastPlayerFoV.Contains(loc))
    {
      // If there are illusory objects and the player can see the square, the
      // illusion will fade. I'm assuming the player can see the 'interaction' 
      // with the mirage and realize there's an illusion. I'm not doing a 
      // perception check or intelligence check here. (I want the player to be 
      // able to check for illusions by, like, tossing a rock onto a square.      
      foreach (var item in ObjDb.ItemsAt(loc).Where(i => i.Type == ItemType.Illusion))
      {
        ObjDb.RemoveItemFromGame(loc, item);
        messages.Add($"An illusion! {item.FullName.DefArticle().Capitalize()} disappears!");
      }
    }

    foreach (Item item in ObjDb.EnvironmentsAt(loc))
    {
      if (item.HasTrait<MoldSporesTrait>())
      {
        UI.AlertPlayer($"{item.Name.DefArticle().Capitalize()} explodes in a cloud of spores!", this, loc);

        HashSet<Loc> affected = [.. Util.Adj8Locs(loc).Where(l => TileAt(l).PassableByFlight())];
        affected.Add(loc);
        ExplosionAnimation explosion = new(this)
        {
          MainColour = Colours.LIME_GREEN,
          AltColour1 = Colours.YELLOW,
          AltColour2 = Colours.YELLOW_ORANGE,
          Highlight = Colours.DARK_GREEN,
          Centre = loc,
          Sqs = [.. affected.Where(l => LastPlayerFoV.Contains(l))],
          Ch = '*'
        };
        UI.PlayAnimation(explosion, this);

        foreach (Loc affectedLoc in affected)
        {
          if (ObjDb.Occupant(affectedLoc) is Actor actor && !actor.HasTrait<ConstructTrait>() && !actor.HasTrait<UndeadTrait>())
          {
            List<string> msgs = [$"{actor.FullName.Capitalize()} {Grammar.Conjugate(actor, "cough")}!" ];            
            DiseasedTrait disease = new() { SourceId = item.ID };
            msgs.AddRange(disease.Apply(actor, this));
            UI.AlertPlayer(string.Join(" ", msgs).Trim(), this, affectedLoc);
          }
        }

        ObjDb.RemoveItemFromGame(loc, item);
      }
    }

    return messages.Count > 0 ? string.Join(" ", messages) : "";
  }

  public string LocDesc(Loc loc)
  {
    static (string, string) ItemText(Item item, int count)
    {
      if (item.Type == ItemType.Zorkmid)
      {
        if (item.Value == 1)
          return ("a", "lone zorkmid");
        else
          return ("are", $"{item.Value} zorkmids");
      }
      else if (item.HasTrait<ArtifactTrait>())
      {
        return ("is", $"{item.FullName.DefArticle()}");
      }
      else if (count != 1)
      {
        return ("are", $"{count} {item.FullName.Pluralize()}");
      }
      else
      {
        return ("is", $"{item.FullName.IndefArticle()}");
      }
    }

    Map map = Campaign.Dungeons[loc.DungeonID].LevelMaps[loc.Level];
    StringBuilder sb = new();
    Tile tile = map.TileAt(loc.Row, loc.Col);
    sb.Append(tile.StepMessage);
    if (tile.Type == TileType.BusinessSign)
    {
      UIRef().SetPopup(new Popup(tile.StepMessage, "", 6, -1));
    }

    foreach (Item item in ObjDb.ItemsAt(loc))
    {
      if (item.HasTrait<MolochAltarTrait>())
      {
        string s, t;
        if (Player.HasTrait<CorruptionTrait>())
        {
          (s, t) = Rng.Next(3) switch
          {
            0 => ("\nMy hunger is endless, my servant.\n", "A low growl"),
            _ => ("\nHave you returned with blood and souls?\n", "A voice in your mind")
          };
        }
        else
        {
          (s, t) = Rng.Next(4) switch
          {
            0 => ("\nI yearn for blood. Bring me a sacrifice.\n", "A raspy whisper"),
            1 => ("\nBring me souls!\n", "A low growl"),
            2 => ("\nI would trade you flesh for power!\n", "A seductive murmur"),
            _ => ("\nI can grant you power! But you must proffer blood.\n", "A seductive murmur")
          };
        }

        UI.SetPopup(new Popup(s, t, 6, -1));

        break;
      }
    }

    Dictionary<Item, int> items = [];
    foreach (var item in ObjDb.VisibleItemsAt(loc))
    {
      if (items.ContainsKey(item))
        items[item] += 1;
      else
        items[item] = 1;
    }

    if (items.Count == 2)
    {
      List<Item> keys = [.. items.Keys];

      var (v, str) = ItemText(keys[0], items[keys[0]]);
      sb.Append(" There ");
      sb.Append(v);
      sb.Append(' ');
      sb.Append(str);
      sb.Append(" and ");
      (_, str) = ItemText(keys[1], items[keys[1]]);
      sb.Append(str);
      sb.Append(" here.");
    }
    else if (items.Count > 1)
    {
      sb.Append(" There are several items here.");
    }
    else if (items.Count == 1)
    {
      Item item = items.Keys.First();
      int count = items[item];
      if (item.Type == ItemType.Zorkmid)
      {
        if (item.Value == 1)
          sb.Append($" There is a lone zorkmid here.");
        else
          sb.Append($" There are {item.Value} zorkmids here!");
      }
      else if (item.HasTrait<ArtifactTrait>())
      {
        sb.Append($" {item.FullName.DefArticle().Capitalize()} is here.");
      }
      else if (item.HasTrait<PluralTrait>())
      {
        sb.Append($" There are some {item.FullName} here.");
      }
      else if (count == 1)
      {
        string s = MsgFactory.CalcName(item, Player, Article.InDef);
        sb.Append($" There is {s} here.");
      }
      else
      {
        sb.Append($" There are {count} {item.FullName.Pluralize()} here.");
      }
    }

    foreach (var env in ObjDb.EnvironmentsAt(loc))
    {
      if (env.Traits.OfType<StickyTrait>().Any())
      {
        sb.Append(" There are some sticky ");
        sb.Append(env.Name);
        sb.Append(" here.");
      }
    }

    return sb.ToString().Trim();
  }

  // Sort of the same as Noise. I can probably DRY them?
  public HashSet<Loc> Flood(Loc start, int radius, bool inclusive = false)
  {
    HashSet<Loc> affected = [];
    var map = CurrentMap;
    var q = new Queue<Loc>();
    q.Enqueue(start);
    var visited = new HashSet<Loc>() { start };

    while (q.Count > 0)
    {
      var curr = q.Dequeue();

      foreach (var n in Util.Adj8Locs(curr))
      {
        if (Util.Distance(start, n) > radius || !map.InBounds(n.Row, n.Col))
          continue;
        if (visited.Contains(n))
          continue;

        visited.Add(n);

        var tile = map.TileAt(n.Row, n.Col);
        if (!tile.PassableByFlight())
        {
          if (inclusive)
            affected.Add(n);
          continue;
        }

        affected.Add(n);

        q.Enqueue(n);
      }
    }

    return affected;
  }

  // Make a noise in the dungeon, start at the source and flood-fill out 
  // decrementing the volume until we hit 0. We'll alert any Actors found
  // the noise
  public HashSet<ulong> Noise(int startRow, int startCol, int volume)
  {
    HashSet<ulong> alerted = [];
    Map map = CurrentMap;
    Queue<(int, int, int)> q = new();
    q.Enqueue((startRow, startCol, volume + 1));
    HashSet<(int, int)> visited = [(startRow, startCol)];

    while (q.Count > 0)
    {
      var curr = q.Dequeue();

      foreach (var n in Util.Adj8Sqs(curr.Item1, curr.Item2))
      {
        if (visited.Contains((n.Item1, n.Item2)))
          continue;

        visited.Add((n.Item1, n.Item2));

        if (!map.InBounds(n.Item1, n.Item2))
          continue;

        // Stop at walls, closed doors, and other tiles that block sound
        // (I could instead cut volume for wood things, etc, but I'm not
        // going THAT far down the simulationist rabbit hole!)
        if (map.TileAt(n.Item1, n.Item2).SoundProof())
        {
          continue;
        }

        // alert actors
        var occ = ObjDb.Occupant(new Loc(CurrDungeonID, CurrLevel, n.Item1, n.Item2));
        if (occ is not null)
        {
          occ.HearNoise(volume, startRow, startCol, this);
          alerted.Add(occ.ID);
        }

        if (curr.Item3 > 1)
          q.Enqueue((n.Item1, n.Item2, curr.Item3 - 1));
      }
    }

    return alerted;
  }

  public void RegisterForEvent(GameEventType eventType, IGameEventListener listener, ulong targetID = 0)
  {
    if (eventType == GameEventType.EndOfRound)
      ObjDb.EndOfRoundListeners.Add(listener);
    else if (eventType == GameEventType.Death)
      ObjDb.DeathWatchListeners.Add((targetID, listener));
    else
      throw new NotImplementedException("I haven't created any other event listeners yet :o");
  }

  public void StopListening(GameEventType eventType, IGameEventListener listener, ulong targetID = 0)
  {
    if (eventType == GameEventType.EndOfRound)
    {
      ObjDb.EndOfRoundListeners.Remove(listener);
    }
    else if (eventType == GameEventType.Death)
    {
      ObjDb.DeathWatchListeners.Remove((targetID, listener));
    }
    else
    {
      throw new NotImplementedException("I haven't created any other event listeners yet :o");
    }
  }

  public void RemoveListenersBySourceId(ulong srcId)
  {
    ObjDb.EndOfRoundListeners = [.. ObjDb.EndOfRoundListeners.Where(l => l.SourceId != srcId)];
  }

  // Remove listener from all events it might be listening for,
  public void RemoveListener(IGameEventListener listener)
  {
    ObjDb.EndOfRoundListeners.Remove(listener);

    Stack<int> indexes = [];
    for (int j = 0; j < ObjDb.DeathWatchListeners.Count; j++)
    {
      if (ObjDb.DeathWatchListeners[j].Item2 == listener)
        indexes.Push(j);
    }

    while (indexes.Count > 0)
      ObjDb.DeathWatchListeners.RemoveAt(indexes.Pop());
  }

  public List<string> OwnedItemPickedUp(List<ulong> ownerIDs, Actor picker, ulong itemID)
  {
    List<string> messages = [];

    foreach (ulong id in ownerIDs)
    {
      if (ObjDb.GetObj(id) is Actor actor)
      {
        messages.Add(actor.PossessionPickedUp(itemID, picker, this));
      }
    }

    return messages;
  }

  readonly Dictionary<Loc, int> _litPool = [];
  Dictionary<Loc, int> CalcLitLocations(Dictionary<Loc, int> playerFoV, int dungeonID, int level)
  {
    _litPool.Clear();
    LitSqs.Clear();

    foreach (GameObj obj in ObjDb.ObjectsOnLevel(dungeonID, level))
    {
      (int lightRadius, Colour fgLightColour, Colour bgLightColour) = FindLight(obj);

      if (obj.HasTrait<InPitTrait>())
        lightRadius = int.Min(lightRadius, 1);

      if (lightRadius < 0)
        continue;

      Dictionary<Loc, int> fov = FieldOfView.CalcVisible(lightRadius, obj.Loc, CurrentMap, ObjDb);

      foreach (var sq in fov)
      {
        if (!playerFoV.TryGetValue(sq.Key, out var pIllum) || (pIllum & sq.Value) == Illumination.None)
          continue;

        if (!_litPool.TryAdd(sq.Key, sq.Value))
          _litPool[sq.Key] |= sq.Value;

        double scale;
        if (InWilderness)
        {
          scale = 1.0;
          fgLightColour = Colours.BLACK;
          bgLightColour = Colours.BLACK;
        }       
        else
        {
          int d = int.Max(0, Util.Distance(sq.Key, obj.Loc) - 1);
          scale = double.Max(0.15, 1.0 - d * 0.125);
        }

        LitSqs.TryGetValue(sq.Key, out (Colour Fg, Colour Bg, int FgAlpha, int BgAlpha) existing);
        if (existing.Fg == Constants.NO_COLOUR || existing.Bg == Colours.PLAYER_LIGHT)
        {
          int fga = int.Min(255, (int)(fgLightColour.Alpha * scale));
          int bga = int.Min(100, (int)(bgLightColour.Alpha * scale));
          LitSqs[sq.Key] = (fgLightColour, bgLightColour, fga, bga);
        }
        else if (bgLightColour == Colours.PLAYER_LIGHT)
        {
          int fga = int.Min(255, (int)(existing.Fg.Alpha * scale));
          int bga = int.Min(100, (int)(existing.Bg.Alpha * scale));
          LitSqs[sq.Key] = (existing.Fg, existing.Bg, fga, bga);          
        }
        else
        {
          Colour blendedFg = Colours.Blend(fgLightColour, existing.Fg);
          Colour blendedBg = Colours.Blend(bgLightColour, existing.Bg);
          int fga = int.Min(255, existing.FgAlpha + (int)(blendedFg.Alpha * scale));
          int bga = int.Min(100, existing.BgAlpha + (int)(blendedBg.Alpha * scale));
          LitSqs[sq.Key] = (blendedFg, blendedBg, fga, bga);
        }
      }
    }

    return _litPool;
  }

  (int, Colour, Colour) FindLight(GameObj obj)
  {
    int lightRadius = -1;
    Colour bgLightColour = Colours.BLACK;
    Colour fgLightColour = Colours.BLACK;

    // If an object (most likely the player) has more than one light source
    // I'm just going to use the one with the largest radius
    foreach (var (fgcolour, bgcolour, radius) in obj.Lights())
    {
      if (radius > lightRadius)
      {
        lightRadius = radius;
        Lights.Add((obj.Loc, fgcolour, bgcolour, radius));
        bgLightColour = bgcolour;
        fgLightColour = fgcolour;
      }
    }

    if (obj.ID == Player.ID)
    {
      if (InWilderness)
      {
        var (hour, _) = CurrTime();
        int daylight;
        if (hour >= 6 && hour <= 19)
          daylight = Player.MAX_VISION_RADIUS;
        else if (hour >= 20 && hour <= 21)
          daylight = 7;
        else if (hour >= 21 && hour <= 23)
          daylight = 3;
        else if (hour < 4)
          daylight = 2;
        else if (hour == 4)
          daylight = 3;
        else
          daylight = 7;

        lightRadius = int.Max(lightRadius, daylight);
      }
      else if ((CurrentMap.Features & MapFeatures.Submerged) != MapFeatures.None)
      {
        lightRadius = 3;
        fgLightColour = Colours.UNDERWATER;
        bgLightColour = Colours.UNDERWATER;
      }

      if (lightRadius == -1)
      {
        lightRadius = 1;
        fgLightColour = Colours.YELLOW;
        bgLightColour = Colours.PLAYER_LIGHT;
      }
    }

    return (lightRadius, fgLightColour, bgLightColour);
  }

  Glyph Hallucination()
  {
    char ch = (char)(Rng.Next(2) == 0 ?
      Rng.Next('A', 'Z' + 1) :
      Rng.Next('a', 'z' + 1));

    Colour colour = Rng.Next(10) switch
    {
      0 => Colours.WHITE,
      1 => Colours.GREEN,
      2 => Colours.LIGHT_BLUE,
      3 => Colours.BLUE,
      4 => Colours.YELLOW_ORANGE,
      5 => Colours.LIGHT_PURPLE,
      6 => Colours.PINK,
      7 => Colours.LIGHT_BROWN,
      8 => Colours.YELLOW,
      _ => Colours.TORCH_YELLOW
    };

    return new Glyph(ch, colour, colour, Colours.BLACK, false);
  }

  public void PrepareFieldOfView()
  {
    //var stackTrace = new System.Diagnostics.StackTrace();
    //var callingMethod = stackTrace.GetFrame(1)?.GetMethod()?.Name;
    bool blind = Player.HasTrait<BlindTrait>();
    int radius = blind ? 0 : Player.MAX_VISION_RADIUS;

    Dictionary<Loc, int> playerFoV = FieldOfView.CalcVisible(radius, Player.Loc, CurrentMap, ObjDb);

    // if the player is not blind, let them see adj sqs regardless of 
    // illumination status. (If the player is surrounded by a fog cloud or such
    // they could come back as not illumination)
    LastPlayerFoV.Clear();
    if (!blind)
    {
      foreach (Loc loc in Util.Adj8Locs(Player.Loc))
        LastPlayerFoV.Add(loc);
    }

    Dictionary<Loc, int> lit = CalcLitLocations(playerFoV, CurrDungeonID, CurrLevel);
    foreach (var sq in playerFoV)
    {
      int playerIllum = sq.Value;
      if (lit.TryGetValue(sq.Key, out var illum) && (illum & playerIllum) != Illumination.None)
        LastPlayerFoV.Add(sq.Key);
    }

    // Calculate which squares are newly viewed and check if there are
    // monsters in any of them. If so, we alert the Player (mainly to 
    // halt running when a monster comes into view)
    HashSet<ulong> prevSeenMonsters = RecentlySeenMonsters.Select(id => id).ToHashSet();
    RecentlySeenMonsters = [Player.ID];
    foreach (Loc loc in LastPlayerFoV)
    {
      if (ObjDb.Occupant(loc) is Actor occ && occ.VisibleTo(Player))
        RecentlySeenMonsters.Add(occ.ID);
    }

    if (RecentlySeenMonsters.Except(prevSeenMonsters).Any())
    {
      Player.EventAlert(GameEventType.MobSpotted, this, Loc.Nowhere);
    }
    RecentlySeenMonsters = prevSeenMonsters;

    // an extremely stressed character may see hallucinations
    HashSet<Loc> hallucinations = [];
    if (!InWilderness && Player.Traits.OfType<StressTrait>().FirstOrDefault() is StressTrait stress)
    {
      int hallucinationCount = 0;
      if (stress.Stress == StressLevel.Paranoid)
        hallucinationCount = Rng.Next(1, 4);
      else if (stress.Stress == StressLevel.Hystrical)
        hallucinationCount = Rng.Next(2, 6);

      if (hallucinationCount > 0)
      {
        List<Loc> fovLocs = [.. LastPlayerFoV];
        for (int j = 0; j < hallucinationCount && LastPlayerFoV.Count > 0; j++)
        {
          int i = Rng.Next(fovLocs.Count);
          hallucinations.Add(fovLocs[i]);
          fovLocs.RemoveAt(i);
        }
      }
    }

    foreach (Loc loc in LastPlayerFoV)
    {
      Tile tile = CurrentMap.TileAt(loc.Row, loc.Col);
      var (glyph, z, objId) = ObjDb.ItemGlyph(loc, Player.Loc);
      
      if (glyph == GameObjectDB.EMPTY || z < tile.Z())
      {
        // Remember the terrain tile if there's nothing visible on the square

        // If it's a chasm, we display the tile from the level below
        if (hallucinations.Contains(loc))
        {
          glyph = Hallucination();
        }
        else if (tile.Type != TileType.Chasm)
        {
          glyph = Util.TileToGlyph(tile);
        }
        else
        {
          Loc below = loc with { Level = CurrLevel + 1 };
          Glyph glyphBelow = ObjDb.GlyphAt(below);
          char ch;
          if (glyphBelow != GameObjectDB.EMPTY)
          {
            ch = glyphBelow.Ch;
          }
          else
          {
            Glyph belowTile = Util.TileToGlyph(CurrentDungeon.LevelMaps[CurrLevel + 1].TileAt(loc.Row, loc.Col));
            ch = belowTile.Ch;
          }

          glyph = new Glyph(ch, Colours.FAR_BELOW, Colours.FAR_BELOW, Colours.BLACK, false);
        }
      }

      CurrentDungeon.RememberedLocs[loc] = new(glyph, objId);
    }
  }
}

class PerformersStack
{
  List<Actor> performers = [];

  public int Count => performers.Count;
  public void Push(Actor a) => performers.Add(a);

  public void Flush()
  {
    foreach (Actor a in performers)
    {
      a.Energy = 0.0;
    }

    performers = [];
  }

  public Actor? Pop()
  {
    if (Count == 0)
      return null;

    Actor a = performers[Count - 1];
    performers.RemoveAt(Count - 1);

    return a;
  }

  public void Remove(ulong id)
  {
    int i = -1;
    for (int j = 0; j < performers.Count; j++)
    {
      if (performers[j].ID == id)
      {
        i = j;
        break;
      }
    }

    if (i > -1)
      performers.RemoveAt(i);
  }
}