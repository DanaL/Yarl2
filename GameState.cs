
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

using System.Diagnostics;
using System.Text;

namespace Yarl2;

// The queue of actors to act will likely need to go here.
class GameState(Player p, Campaign c, Options opts, UserInterface ui, Random rng, int seed)
{
  public int Seed { get; init; } = seed;
  public Random Rng { get; set; } = rng;
  public Map? CurrMap { get; set; }
  public Options Options { get; set; } = opts;
  public Player Player { get; set; } = p;
  public int CurrLevel { get; set; }
  public int CurrDungeonID { get; set; }
  public Campaign Campaign { get; set; } = c;
  public GameObjectDB ObjDb { get; set; } = new GameObjectDB();
  public List<IPerformer> Performers { get; set; } = [];
  public ulong Turn { get; set; }

  public HashSet<ulong> RecentlySeenMonsters { get; set; } = [];
  public HashSet<Loc> LastPlayerFoV = [];
  DjikstraMap? DMap { get; set; }
  DjikstraMap? DMapDoors { get; set; }
  DjikstraMap? DMapFlight { get; set; }
  public DjikstraMap GetDMap(string map = "")
  {
    if (DMap is null || DMapDoors is null || DMapFlight is null)
      SetDMaps(Player.Loc);

    if (map == "doors")
      return DMapDoors;  
    else if (map == "flying")
      return DMapFlight;
    else    
      return DMap;    
  }
  
  public ulong LastTarget { get; set; } = 0;
  public List<Fact> Facts => Campaign.History != null ? Campaign.History.Facts : [];

  // This might (probably will) expand into a hashtable of 
  // UIEventType mapped to a list of listeners
  List<IGameEventListener> _endOfRoundListeners { get; set; } = [];
  List<(ulong, IGameEventListener)> _deathWatchListeners { get; set; } = [];

  private UserInterface UI { get; set; } = ui;

  private int _currPerformer = 0;

  static readonly Dictionary<TileType, int> _passableBasic = new()
    {
        { TileType.DungeonFloor, 1 },
        { TileType.Landmark, 1 },
        { TileType.Upstairs, 1 },
        { TileType.Downstairs, 1 },
        { TileType.OpenDoor, 1 },
        { TileType.BrokenDoor, 1 },
        { TileType.WoodBridge, 1 }
    };

  static readonly Dictionary<TileType, int> _passableWithDoors = new()
    {
        { TileType.DungeonFloor, 1 },
        { TileType.Landmark, 1 },
        { TileType.Upstairs, 1 },
        { TileType.Downstairs, 1 },
        { TileType.OpenDoor, 1 },
        { TileType.BrokenDoor, 1 },
        { TileType.ClosedDoor, 1 },
        { TileType.WoodBridge, 1 }
    };

  static readonly Dictionary<TileType, int> _passableFlying = new()
    {
        { TileType.DungeonFloor, 1 },
        { TileType.Landmark, 1 },
        { TileType.Upstairs, 1 },
        { TileType.Downstairs, 1 },
        { TileType.OpenDoor, 1 },
        { TileType.BrokenDoor, 1 },
        { TileType.WoodBridge, 1 },
        { TileType.DeepWater, 1 },
        { TileType.Water, 1 },
        { TileType.Chasm, 1 }
    };

  public void WriteMessages(List<Message> alerts, string ifNotSeen) => UI.AlertPlayer(alerts, ifNotSeen, this);
  public void ClearMenu() => UI.CloseMenu();
  public UserInterface UIRef() => UI;

  public void EnterLevel(Actor actor, int dungeon, int level)
  {
    CurrLevel = level;
    CurrDungeonID = dungeon;

    if (dungeon == 1 && actor is Player)
    {
      int maxDepth = Player.Stats[Attribute.Depth].Max;
      if (level + 1 > maxDepth)
        Player.Stats[Attribute.Depth].SetMax(level + 1);
    }
  }

  public Town Town => Campaign!.Town!;
  public Dungeon CurrentDungeon => Campaign!.Dungeons[CurrDungeonID];
  public Map CurrentMap => Campaign!.Dungeons[CurrDungeonID].LevelMaps[CurrLevel];
  public bool InWilderness => CurrDungeonID == 0;
  public Map Wilderness => Campaign.Dungeons[0].LevelMaps[0];
  
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
    var d = Campaign!.Dungeons[loc.DungeonID];
    var map = d.LevelMaps[loc.Level];

    return map.InBounds(loc.Row, loc.Col)
                ? map.TileAt(loc.Row, loc.Col)
                : TileFactory.Get(TileType.Unknown);
  }

  public bool CanSeeLoc(Actor viewer, Loc loc, int radius)
  {
    var map = Campaign.Dungeons[loc.DungeonID].LevelMaps[loc.Level];
    var fov = FieldOfView.CalcVisible(radius, loc, map, ObjDb);

    return fov.Contains(loc);
  }

  public bool LOSBetween(Loc a, Loc b)
  {
    if (a.DungeonID != b.DungeonID || a.Level != b.Level)
      return false;

    var map = Campaign.Dungeons[a.DungeonID].LevelMaps[a.Level];
    foreach (var sq in Util.Bresenham(a.Row, a.Col, b.Row, b.Col))
    {
      if (!map.InBounds(sq) || map.TileAt(sq).Opaque())
        return false;
    }

    return true;
  }

  public void ItemDropped(Item item, Loc loc)
  {
    item.ContainedBy = 0;

    var tile = TileAt(loc);
    List<Message> msgs = [];
    foreach (var flag in tile.TerrainFlags().Where(t => t != EffectFlag.None))
    {
      string s = EffectApplier.Apply(flag, this, item, null);
      if (s != "")
      {
        msgs.Add(new Message(s, loc));
      }
    }

    foreach (var t in item.Traits)
    {
      if (t is DamageTrait dt && dt.DamageType == DamageType.Fire)
        ApplyDamageEffectToLoc(loc, DamageType.Fire);
    }

    ObjDb.SetToLoc(loc, item);
    Message? msg = ThingAddedToLoc(loc);
    if (msg != null)
      msgs.Add(msg);

    if (msgs.Count > 0)
      UI.AlertPlayer(msgs, "", this);
  }

  public void ItemDestroyed(Item item, Loc loc)
  {
    var map = Campaign.Dungeons[loc.DungeonID].LevelMaps[loc.Level];
    ObjDb.RemoveItemFromGame(loc, item);

    foreach (var listener in item.Traits.OfType<IGameEventListener>())
    {
      RemoveListener(listener);
    }
  }

  // I don't have a way to track what square is below a bridge, so I have to do 
  // something kludgy. Maybe in the future I should turn bridges into Items
  // that can be walked on?
  static TileType SquareBelowBridge(Map map, Loc start)
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
        var tileType = map.TileAt(adj.Row, adj.Col).Type;

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

  public void ApplyDamageEffectToLoc(Loc loc, DamageType damageType)
  {
    List<Item> items = [];
    items.AddRange(ObjDb.ItemsAt(loc));
    items.AddRange(ObjDb.EnvironmentsAt(loc));
    List<Message> messages = [];
    var tile = TileAt(loc);
    bool fireStarted = false;

    switch (damageType)
    {
      case DamageType.Fire:
        // Wooden bridges always burn for comedy reasons
        if (tile.Flammable() && (tile.Type == TileType.WoodBridge || Rng.NextDouble() < 0.15))
          fireStarted = true;

        if (tile.Type == TileType.FrozenWater)
        {
          var map = Campaign.Dungeons[loc.DungeonID].LevelMaps[loc.Level];
          map.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.Water));
          UI.AlertPlayer(new Message("The ice melts!", loc), "You hear a hiss!", this);
        }
        else if (tile.Type == TileType.FrozenDeepWater)
        {
          var map = Campaign.Dungeons[loc.DungeonID].LevelMaps[loc.Level];
          map.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.DeepWater));
          UI.AlertPlayer(new Message("The ice melts!", loc), "You hear a hiss!", this);
          BridgeDestroyedOverWater(loc);
        }

        foreach (var item in items)
        {
          if (item.HasTrait<FlammableTrait>())
          {
            messages.Add(new Message($"{item.FullName.DefArticle().Capitalize()} burns up!", loc));
            ItemDestroyed(item, loc);
            fireStarted = true;
          }
        }
        break;
      case DamageType.Cold:
        // Perhaps Cold can destroy poitions on the ground and such?

        if (tile.Type == TileType.Water)
        {
          var map = Campaign.Dungeons[loc.DungeonID].LevelMaps[loc.Level];
          map.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.FrozenWater));
          UI.AlertPlayer(new Message("The water freezes!", loc), "You hear a cracking sound!", this);
        }
        else if (tile.Type == TileType.DeepWater)
        {
          var map = Campaign.Dungeons[loc.DungeonID].LevelMaps[loc.Level];
          map.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.FrozenDeepWater));
          UI.AlertPlayer(new Message("The water freezes!", loc), "You hear a cracking sound!", this);
        }
        break;
      default:
        break;
    }

    if (fireStarted)
    {
      var fire = ItemFactory.Fire(this);
      ObjDb.SetToLoc(loc, fire);
      
      var map = Campaign.Dungeons[loc.DungeonID].LevelMaps[loc.Level];
      if (tile.Type == TileType.Grass)
      {
        map.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.CharredGrass));
      }
      else if (tile.Type == TileType.Tree)
      {
        map.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.CharredStump));
      }
      else if (tile.Type == TileType.WoodBridge)
      {
        var type = SquareBelowBridge(map, loc);
        map.SetTile(loc.Row, loc.Col, TileFactory.Get(type));
        
        if (type == TileType.Chasm)
          BridgeCollapseOverChasm(loc);
        else if (type == TileType.DeepWater)
          BridgeDestroyedOverWater(loc);
      }      
    }

    if (messages.Count > 0)
      UI.AlertPlayer(messages, "", this);
  }

  void BridgeDestroyedOverWater(Loc loc)
  {
  if (ObjDb.Occupant(loc) is Actor actor && !(actor.HasActiveTrait<FlyingTrait>() || actor.HasActiveTrait<FloatingTrait>()))
    {
      UI.AlertPlayer(new Message($"{actor.FullName.Capitalize()} {Grammar.Conjugate(actor, "fall")} into the water!", actor.Loc), "", this);
      
      string msg = FallIntoWater(actor, loc);
      if (msg.Length > 0)
        UI.AlertPlayer(new Message(msg, loc), "", this);
    }

    var itemsToFall = ObjDb.ItemsAt(loc);
    foreach (var item in itemsToFall)
    {
      UI.AlertPlayer(new Message($"{item.Name.DefArticle().Capitalize()} sinks!", loc), "", this);
      ObjDb.RemoveItem(loc, item);      
      ItemDropped(item, loc);
    }
  }

  void BridgeCollapseOverChasm(Loc loc)
  {
    var landingSpot = loc with { Level = loc.Level + 1 };

    if (ObjDb.Occupant(loc) is Actor actor && !(actor.HasActiveTrait<FlyingTrait>() || actor.HasActiveTrait<FloatingTrait>()))
    {
      UI.AlertPlayer(new Message($"{actor.FullName.Capitalize()} {Grammar.Conjugate(actor, "fall")} into the chasm!", actor.Loc), "", this);
      
      string msg = FallIntoChasm(actor, landingSpot);
      if (msg.Length > 0)
        UI.AlertPlayer(new Message(msg, landingSpot), "", this);
    }

    var itemsToFall = ObjDb.ItemsAt(loc);
    foreach (var item in itemsToFall)
    {
      UI.AlertPlayer(new Message($"{item.Name.DefArticle().Capitalize()} tumbles into darkness!", loc), "", this);
      ObjDb.RemoveItem(loc, item);
      ItemDropped(item, landingSpot);
    }
    UpdateFoV();
  }

  public string FallIntoWater(Actor actor, Loc loc)
  {
    List<string> messages = [];

    // When someone jumps/falls into water, they wash ashore at a random loc
    // and incur the Exhausted condition first, find candidate shore sqs
    var q = new Queue<Loc>();
    q.Enqueue(loc);
    HashSet<Loc> visited = [];
    HashSet<Loc> shores = [];

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
      var candidates = shores.ToList();
      var destination = candidates[Rng.Next(candidates.Count)];
      Message? moveMsg = ResolveActorMove(actor, actor.Loc, destination);
      if (moveMsg is not null)
      {
        messages.Add(moveMsg.Text);
      }
      actor.Loc = destination;

      string invMsgs = actor.Inventory.ApplyEffectToInv(EffectFlag.Wet, this, actor.Loc);
      if (invMsgs.Length > 0)
      {
        messages.Add(invMsgs);
      }
      
      UpdateFoV();

      int conMod;
      if (actor.Stats.TryGetValue(Attribute.Constitution, out var stat))
        conMod = stat.Curr;
      else
        conMod = 0;
      ulong endsOn = Turn + (ulong)(250 - 10 * conMod);
      var exhausted = new ExhaustedTrait()
      {
        VictimID = actor.ID,
        EndsOn = endsOn
      };
      if (exhausted.IsAffected(actor, this))
      {
        string msg = exhausted.Apply(actor, this);
        if (msg.Length > 0) 
          messages.Add(msg);
      }

      messages.Add($"{actor.FullName.Capitalize()} {Grammar.Conjugate(actor, "wash")} ashore, gasping for breath!");
    }
    else
    {
      // What happens if there are somehow no free shore sqs? Does the mob drown??
    }

    return string.Join(' ', messages).Trim();
  }

  public string FallIntoChasm(Actor actor, Loc landingSpot)
  {
    EnterLevel(actor, landingSpot.DungeonID, landingSpot.Level);
    Message? moveMsg = ResolveActorMove(actor, actor.Loc, landingSpot);
    if (moveMsg is not null)
    {
      WriteMessages([moveMsg], "");
    }
    actor.Loc = landingSpot;

    if (actor is Player)
    {
      RefreshPerformers();
      UpdateFoV();
    }

    int fallDamage = Rng.Next(6) + Rng.Next(6) + 2;
    var (hpLeft, _) = actor.ReceiveDmg([(fallDamage, DamageType.Blunt)], 0, this);
    if (hpLeft < 1)
    {
      ActorKilled(actor, "a fall", null);
    }

    return $"{actor.FullName.Capitalize()} {Grammar.Conjugate(actor, "is")} injured by the fall!";
  }

  public void ActorKilled(Actor victim, string killedBy, ActionResult? result)
  {
    if (victim is Player)
    {            
      if (result is not null && result.Messages.Count > 0)
      {
        UI.AlertPlayer(result.Messages, result.MessageIfUnseen, this);
      }

      // Play any queued explosions in case it was one of the explosions
      // that killed the player
      UI.PlayQueuedExplosions(this);

      Message msg = new($"Oh noes you've been killed by {killedBy} :(", victim.Loc);
      UI.AlertPlayer(msg, "", this);
      UI.KillScreen("You died :(", this);
      throw new PlayerKilledException();
    }
    else if (victim.HasTrait<FinalBossTrait>())
    {
      UI.VictoryScreen(victim.FullName, this);
      throw new VictoryException();
    }
    else if (result is not null)
    {      
      
        var verb = victim.HasTrait<PlantTrait>() ? Verb.Destroy : Verb.Kill;
        var plural = victim.HasTrait<PluralTrait>();

        Message killMsg = MsgFactory.Phrase(victim.ID, Verb.Etre, verb, plural, true, victim.Loc, this);
        result.Messages.Add(killMsg);
    }

    if (victim.ID != Player.ID && victim is Mob m)
    {
      int xpv = m.Stats[Attribute.XPValue].Curr;
      Player.Stats[Attribute.XP].ChangeMax(xpv);
    }

    ObjDb.RemoveActor(victim);
    
    // Need to remove the victim from the Performer queue but also update 
    // current performer pointer if necessary. If _currPerformer > index
    // of victim, we want to decrement it
    var performerIndex = Performers.IndexOf(victim);
    if (_currPerformer > performerIndex)
      --_currPerformer;
    Performers.Remove(victim);

    if (victim.HasTrait<PoorLootTrait>())
    {
      var loot = Treasure.PoorTreasure(1, Rng, ObjDb);
      if (loot.Count > 0)
        ItemDropped(loot[0], victim.Loc);
    }

    foreach (RetributionTrait rt in victim.Traits.OfType<RetributionTrait>())
    {
      RetributionDamage(victim, rt, result);
    }

    // Was anything listening for the the victims death?
    foreach (var (targetID, listener) in _deathWatchListeners)
    {
      if (targetID == victim.ID) 
        listener.EventAlert(GameEventType.Death, this);
    }
    ClearDeathWatch(victim.ID);

    // For illusory mobs, their death message is being displayed before the
    // death message of the caster, which I find unsatisfying but it's a bit
    // of work to change that. I'd have to return the message from here and
    // then add it to the result message of whatever action called this message
    // (Or create a more centralized queue of messages to display)
    var deathMessage = victim.Traits.OfType<DeathMessageTrait>().FirstOrDefault();
    if (deathMessage != null)
    {
      var msg = new Message(deathMessage.Message, victim.Loc);
      UI.AlertPlayer([msg], "", this);
    }
  }

  void RetributionDamage(Actor src, RetributionTrait retribution, ActionResult? result)
  {
    string dmgDesc = retribution.Type.ToString().ToLower();

    if (result is not null)
    {      
      string txt = $"{src.FullName.Capitalize()} {Grammar.Conjugate(src, "explode")} in a blast of {dmgDesc}!";
      result.Messages.Add(new Message(txt, src.Loc));
    }

    int dmg = 0;
    for (int i = 0; i < retribution.NumOfDice; i++)
      dmg += Rng.Next(retribution.DmgDie) + 1;
    HashSet<Loc> pts = [ src.Loc ];
    foreach (Loc adj in Util.Adj8Locs(src.Loc))
    {
      if (ObjDb.Occupant(adj) is Actor actor)
      {
        result?.Messages.Add(new Message($"{actor.FullName.Capitalize()} {Grammar.Conjugate(actor, "is")} caught in the blast!", adj));
        var (hpLeft, msg) = actor.ReceiveDmg([(dmg, retribution.Type)], 0, this);
        result?.Messages.Add(new Message(msg, actor.Loc));
        if (hpLeft < 1)
           ActorKilled(actor, dmgDesc, result);
      }
      ApplyDamageEffectToLoc(adj, retribution.Type);
      pts.Add(adj);
    }

    switch (retribution.Type)
    {
      case DamageType.Cold:
        var anim = new ExplosionAnimation(this)
        {
          MainColour = Colours.LIGHT_BLUE,
          AltColour1 = Colours.ICE_BLUE,
          AltColour2 = Colours.BLUE,
          Highlight = Colours.WHITE,
          Centre = src.Loc,
          Sqs = pts
        };
        UI.PlayAnimation(anim, this);
        break;
    }    
  }

  void ClearDeathWatch(ulong victimID)
  {
    Stack<int> indexes = [];
    for (int j = 0; j < _deathWatchListeners.Count; j++)
    {
      if (_deathWatchListeners[j].Item1 == victimID)
        indexes.Push(j);
    }
    while (indexes.Count > 0)
    {      
      _deathWatchListeners.RemoveAt(indexes.Pop());
    }
  }

  public void BuildPerformersList()
  {    
    RefreshPerformers();

    foreach (var performer in Performers)
    {
      performer.Energy = performer.Recovery;
    }

    // Let the player go first when starting a session
    var i = Performers.FindIndex(p => p is Player);
    var player = Performers[i];
    Performers.RemoveAt(i);
    Performers.Insert(0, player);
  }

  public void AddPerformer(IPerformer performer)
  {
    performer.Energy = performer.Recovery;
    Performers.Add(performer);
  }

  public void RefreshPerformers()
  {
    IPerformer? curr = null;
    if (Performers.Count > 0)
    {
      curr = Performers[_currPerformer];
    }

    Performers.Clear();
    Performers.AddRange(ObjDb.GetPerformers(CurrDungeonID, CurrLevel));

    if (curr is not null)
    {
      // If the player changes dungeons/level on someone else's turn
      // _currPerformer may no longer be in the list, so default to
      // index 0 in that case.
      int i = Performers.IndexOf(curr);
      _currPerformer = i > -1 ? i : 0;
    }
  }

  public IPerformer NextPerformer()
  {
    IPerformer nextPerformer;

    do
    {
      nextPerformer = Performers[_currPerformer];

      if (nextPerformer.Energy < 1.0)
      {
        nextPerformer.Energy += nextPerformer.Recovery;
        ++_currPerformer;
      }
      else
      {
        return nextPerformer;
      }

      if (_currPerformer >= Performers.Count)
      {
        ++Turn;
        _currPerformer = 0;
        EndOfTurn();
      }
    }
    while (true);
  }

  // Not sure if this is the right spot for this.  Maybe the player should have a feature/trait
  // that's countdown timer for healing. Then its period can be tweaked by effects and items.
  // I don't what to have every single effect have its own turn like light sources do, but 
  // maybe Actors can have a list of effects I check for each turn?
  //
  // Also not sure how often monsters should regenerate.
  //
  // TODO: gotta eventually add monster respawns
  void EndOfTurn()
  {
    if (Turn % 11 == 0)
    {
      Player.Stats[Attribute.HP].Change(1);
    }

    PlayerCreator.CheckLevelUp(this);

    var listeners = _endOfRoundListeners.Where(l => !l.Expired).ToList();
    foreach (var listener in listeners)
    {
      listener.EventAlert(GameEventType.EndOfRound, this);
    }
    _endOfRoundListeners = _endOfRoundListeners.Where(l => !l.Expired).ToList();
  }

  void SetDMaps(Loc loc)
  {
    DMap = new DjikstraMap(CurrentMap, CurrentMap.Height, CurrentMap.Width);
    DMap.Generate(_passableBasic, (loc.Row, loc.Col), 25);

    // I wonder how complicated it would be to generate the maps in parallel...
    DMapDoors = new DjikstraMap(CurrentMap, CurrentMap.Height, CurrentMap.Width);
    DMapDoors.Generate(_passableWithDoors, (loc.Row, loc.Col), 25);

    DMapFlight = new DjikstraMap(CurrentMap, CurrentMap.Height, CurrentMap.Width);
    DMapFlight.Generate(_passableFlying, (loc.Row, loc.Col), 25);
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
    
    if (a is Player && startB.DungeonID > 0)
    {
      SetDMaps(startB);
    }
    else if (b is Player && startA.DungeonID > 0)
    {
      SetDMaps(startA);
    }
  }

  public Message? ResolveActorMove(Actor actor, Loc start, Loc dest)
  {
    ObjDb.ActorMoved(actor, start, dest);
    
    // Not making djikstra maps for the otherworld just yet.
    // Eventually I need to take into account whether or not
    // monsters can open doors, fly, etc. Multiple maps??
    if (actor is Player && dest.DungeonID > 0)
    {
      SetDMaps(dest);      
    }

    return ThingAddedToLoc(dest);
  }

  public Message? ThingAddedToLoc(Loc loc)
  {
    Tile tile = CurrentMap.TileAt(loc.Row, loc.Col);

    if (tile is GateTrigger trigger)
    {
      Loc gateLoc = ((GateTrigger)tile).Gate;
      if (CurrentMap.TileAt(gateLoc.Row, gateLoc.Col) is Portcullis portcullis)
      {        
        portcullis.Trigger();
        Message msg = new Message("You hear a metallic grinding!", loc, false);
        return msg;        
      }
    }

    return null;
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
      else if (count != 1)
      {
        return ("are", $"{count} {item.FullName.Pluralize()}");
      }
      else
      {
        return ("is", $"{item.FullName.IndefArticle()}");
      }
    }

    var map = Campaign.Dungeons[loc.DungeonID].LevelMaps[loc.Level];
    var sb = new StringBuilder();
    sb.Append(map.TileAt(loc.Row, loc.Col).StepMessage);
    
    Dictionary<Item, int> items = new Dictionary<Item, int>();
    foreach (var item in ObjDb.ItemsAt(loc))
    {
      if (items.ContainsKey(item))
        items[item] += 1;
      else
        items[item] = 1;
    }

    if (items.Count == 2)
    {
      var keys = items.Keys.ToList();

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
      else if (count == 1)
      {
        sb.Append($" There is {item.FullName.IndefArticle()} here.");
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
  public HashSet<Loc> Flood(Loc start, int radius)
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
          continue;

        affected.Add(n);

        q.Enqueue(n);
      }
    }

    return affected;
  }

  // Make a noise in the dungeon, start at the source and flood-fill out 
  // decrementing the volume until we hit 0. We'll alert any Actors found
  // the noise
  public HashSet<ulong> Noise(ulong sourceID, int startRow, int startCol, int volume)
  {
    var alerted = new HashSet<ulong>();
    var map = CurrentMap;
    var q = new Queue<(int, int, int)>();
    q.Enqueue((startRow, startCol, volume + 1));
    var visited = new HashSet<(int, int)>() { (startRow, startCol) };

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
          occ.HearNoise(volume, sourceID, startRow, startCol, this);
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
      _endOfRoundListeners.Add(listener);
    else if (eventType == GameEventType.Death)
      _deathWatchListeners.Add((targetID, listener));
    else
      throw new NotImplementedException("I haven't created any other event listeners yet :o");
  }

  public void StopListening(GameEventType eventType, IGameEventListener listener, ulong targetID = 0)
  {
    if (eventType == GameEventType.EndOfRound) 
    {
      _endOfRoundListeners.Remove(listener);
    }
    else if (eventType == GameEventType.Death)
    {
      _deathWatchListeners.Remove((targetID, listener));
    }
    else
    {
      throw new NotImplementedException("I haven't created any other event listeners yet :o");
    }
  }

  // Remove listener from all events it might be listening for,
  public void RemoveListener(IGameEventListener listener)
  {
    _endOfRoundListeners.Remove(listener);

    Stack<int> indexes = [];
    for (int j = 0; j < _deathWatchListeners.Count; j++)
    {
      if (_deathWatchListeners[j].Item2 == listener)
        indexes.Push(j);
    }

    while (indexes.Count > 0)
      _deathWatchListeners.RemoveAt(indexes.Pop());
  }

  public List<Message> OwnedItemPickedUp(List<ulong> ownerIDs, Actor picker, ulong itemID)
  {
    List<Message> messages = [];

    foreach (ulong id in ownerIDs)
    {
      if (ObjDb.GetObj(id) is Actor actor)
      {
        Message? msg = actor.PossessionPickedUp(itemID, picker, this);
        if (msg is not null)
          messages.Add(msg);
      }
    }

    return messages;
  }

  HashSet<Loc> LitLocations(int dungeonID, int level)
  {
    HashSet<Loc> lit = [];

    foreach (GameObj obj in ObjDb.ObjectsOnLevel(dungeonID, level)) 
    {
      int lightRadius = obj.LightRadius();
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
        if (lightRadius == 0)
          lightRadius = 1;
      }
      
      if (lightRadius > 0)
      {
        lit.UnionWith(FieldOfView.CalcVisible(lightRadius, obj.Loc, CurrentMap, ObjDb));
      }
    }

    return lit;
  }

  public void UpdateFoV()
  {
    CurrMap = CurrentDungeon.LevelMaps[CurrLevel];

    HashSet<Loc> litLocations = LitLocations(CurrDungeonID, CurrLevel);

    var fov = FieldOfView.CalcVisible(Player.MAX_VISION_RADIUS, Player.Loc, CurrentMap, ObjDb)
                         .Intersect(litLocations).ToHashSet();
    LastPlayerFoV = fov;

    // Calculate which squares are newly viewed and check if there are
    // monsters in any of them. If so, we alert the Player (mainly to 
    // halt running when a monster comes into view)
    var prevSeenMonsters = RecentlySeenMonsters.Select(id => id).ToHashSet();
    RecentlySeenMonsters = [ Player.ID ];
    foreach (var loc in fov)
    {
      if (ObjDb.Occupant(loc) is Actor occ)
        RecentlySeenMonsters.Add(occ.ID);
    }

    if (RecentlySeenMonsters.Except(prevSeenMonsters).Any())
    {
      Player.EventAlert(GameEventType.MobSpotted, this);
    }
    RecentlySeenMonsters = prevSeenMonsters;
    
    foreach (var loc in fov)
    {
      Tile tile = CurrMap.TileAt(loc.Row, loc.Col);
      var (glyph, z) = ObjDb.ItemGlyph(loc);
      if (glyph == GameObjectDB.EMPTY || z < tile.Z())
      {
        // Remember the terrain tile if there's nothing visible the square
        
        // If it's a chasm, we display the tile from the level below
        if (tile.Type != TileType.Chasm)
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
            var belowTile = Util.TileToGlyph(CurrentDungeon.LevelMaps[CurrLevel + 1].TileAt(loc.Row, loc.Col));
            ch = belowTile.Ch;
          }
          glyph = new Glyph(ch, Colours.FAR_BELOW, Colours.FAR_BELOW, Colours.BLACK, Colours.BLACK);
        }
      }
    
      CurrentDungeon.RememberedLocs[loc] = glyph;
    }
  }
}