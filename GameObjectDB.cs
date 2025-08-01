﻿// Yarl2 - A roguelike computer RPG
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

// I'm only doing this because the JSONSerializer can't handle
// tuples and I'd have to convert the DB keys to something else
// anyhow.
// Although it does make a nicer parameter to pass around to methods
record struct Loc(int DungeonID, int Level, int Row, int Col)
{
  public static Loc Nowhere = new(-1, -1, -1, -1);
  public static Loc Zero = new(0, 0, 0, 0);
  // A convenient method because this comes up a lot.
  public Loc Move(int RowDelta, int ColDelta)
  {
    return this with { Row = Row + RowDelta, Col = Col + ColDelta };
  }

  public override readonly string ToString() => $"{DungeonID},{Level},{Row},{Col}";

  public static Loc FromStr(string text)
  {    
    var digits = Util.ToNums(text);
    return new Loc(digits[0], digits[1], digits[2], digits[3]);   
  }
}

enum GlyphType { Terrain, Item, Mob }

record struct Glyph(char Ch, Colour Lit, Colour Unlit, Colour BG, bool Illuminate)
{
  public override readonly string ToString()
  {    
    return $"{Ch},{Colours.ColourToText(Lit)},{Colours.ColourToText(Unlit)},{Colours.ColourToText(BG)},{Illuminate}";
  }

  public static Glyph TextToGlyph(string text)
  {
    // I wnated to use , for food as in angband (vs % in nethack), which made
    // storing Glyphs as comma-separated strings a bit gross
    char ch = text[0];
    if (text[0] == ',')
      text = text[1..];
    var p = text.Split(',');

    return new Glyph(ch, Colours.TextToColour(p[1]), Colours.TextToColour(p[2]), Colours.TextToColour(p[3]), Convert.ToBoolean(p[4]));
  }
}

// This feels like a bit of a hack, but quite a bit into
// development I decided to have a Z-level for both game objects
// and tiles, to simplify the code for what glyph to display for a 
// given square. Tiles are normally the lowest Z, but water tiles
// are higher so that most items are 'underwater' Flying monsters
// are in the air above but swimmers probably (they don't exist
// as I type this) below. Then, I wanted to add fog, which 
// should entirely obscure the tile and which I want to implement
// as an Item type
//
// So anyhow, I'm reluctant to add a superclass of both GameObj and 
// Tile and I am reluctant to make all Items/Actors a subclass of 
// Tile so I've decided to go with an interface. If more shared
// stuff ends up inside this interface I guess I'll think about
// making a proper superclass.
interface IZLevel
{
  int Z();
}

interface IGameEventListener
{
  public bool Expired { get; set; }
  public bool Listening { get; }
  public ulong ObjId { get; }
  public ulong SourceId { get; set; }
  public GameEventType EventType { get; }

  void EventAlert(GameEventType eventType, GameState gs, Loc loc);
}

abstract class GameObj : IZLevel
{
  static ulong IDSeed = 2;
  public string Name { get; set; } = "";
  public virtual string FullName => Name;
  public virtual Glyph Glyph { get; set; }
  public Loc Loc { get; set; } = Loc.Nowhere;
  public List<Trait> Traits { get; set; } = [];

  public ulong ID { get; set; }

  public GameObj() => ID = IDSeed++;
  public static ulong Seed => IDSeed;
  public static void SetSeed(ulong seed) => IDSeed = seed;

  public static ulong NextID => IDSeed++;

  public virtual int Z()
  {
    return 0;
  }

  public bool HasActiveTrait<T>() => Traits.Where(t => t.Active)
                                     .OfType<T>().Any();
  public bool HasTrait<T>() where T : Trait
  {
    foreach (Trait trait in Traits)
    {
      if (trait is T)
        return true;
    }

    return false;
  }

  public override string ToString()
  {
    var sb = new StringBuilder();
    sb.Append(Name);
    sb.Append(Constants.SEPARATOR);
    sb.Append(ID);
    sb.Append(Constants.SEPARATOR);
    sb.Append(Glyph.ToString());
    sb.Append(Constants.SEPARATOR);
    sb.Append(Loc.ToString());
    sb.Append(Constants.SEPARATOR);
    string traits = string.Join("`", Traits.Select(t => t.AsText()));
    sb.Append(traits);

    return sb.ToString();
  }

  public virtual List<(Colour, Colour, int)> Lights()
  {
    List<(Colour, Colour, int)> lights = [];

    foreach (Trait t in Traits)
    {
      if (t is LightSourceTrait ls)
      {
        lights.Add((ls.FgColour, ls.BgColour, ls.Radius));
      }
    }

    return lights;
  }

  public virtual int TotalLightRadius()
  {
    int lightRadius = 0;
    foreach (Trait t in Traits)
    {
      if (t is LightSourceTrait ls && ls.Radius > lightRadius)
        lightRadius = ls.Radius;
    }

    return lightRadius;
  }
}

// Structure to store where items are in the world
class GameObjectDB
{
  public static readonly Glyph EMPTY = new('\0', Colours.BLACK, Colours.BLACK, Colours.BLACK, false);

  public Dictionary<Loc, List<Item>> _itemLocs = [];
  public Dictionary<Loc, ulong> _actorLocs = [];
  public Dictionary<ulong, GameObj> Objs = [];

  // This might (probably will) expand into a hashtable of 
  // UIEventType mapped to a list of listeners
  public List<IGameEventListener> EndOfRoundListeners { get; set; } = [];
  public List<(ulong, IGameEventListener)> DeathWatchListeners { get; set; } = [];
  public HashSet<Loc> LocListeners { get; set; } = [];
  public List<ConditionalEvent> ConditionalEvents { get; set; } = [];

  public Player? FindPlayer()
  {
    foreach (var obj in Objs.Values)
    {
      if (obj is Player player)
        return player;
    }

    return null;
  }

  public (Glyph, int) ItemGlyph(Loc loc, Loc playerLoc)
  {
    static bool Disguised(Actor mob)
    {
      if (!mob.HasActiveTrait<DisguiseTrait>())
        return false;

      if (mob.Stats.TryGetValue(Attribute.InDisguise, out var stat) && stat.Curr == 1)
        return true;

      return false;
    }

    int z = 0;
    Glyph glyph = EMPTY;

    // If there is a Mob disguised as an item, we'll return that glyph
    if (_actorLocs.TryGetValue(loc, out ulong id) && Objs[id] is Actor mob)
    {
      if (Disguised(mob))
      {
        glyph = mob.Glyph;
        z = mob.Z();
      }
    }
    else if (_itemLocs.TryGetValue(loc, out var items))
    {
      // Some tiles we don't want to end up in remembered locations      
      int d = Util.Distance(loc, playerLoc);
      foreach (var item in items)
      {
        if (item.Type == ItemType.Fog)
          continue;

        bool hidden = false;
        foreach (Trait t in item.Traits)
        {
          if (t is BlockTrait)
            return (item.Glyph, item.Z());

          if (t is HiddenTrait)
          {
            hidden = true;
            break;
          }

          if (t is OpaqueTrait opaque && d < opaque.Visibility)
          {
            hidden = true;            
            break;
          }
        }

        if (hidden)
          continue;

        if (item.Z() > z)
        {
          glyph = item.Glyph;
          z = item.Z();          
        }
      }
    }

    return (glyph, z);
  }

  // TODO: I think I can replace GlyphAt() and ItemGlyphAt() with a TopGlyph() method
  // They're only used to querying what's below on chasm sqs
  // Basically, the sqr ignoring the occupant since we only want to remember
  // either the item stack or the tile
  public Glyph GlyphAt(Loc loc)
  {
    if (_actorLocs.TryGetValue(loc, out ulong id))
      return Objs[id].Glyph;
    else
      return ItemGlyphAt(loc);
  }

  public Glyph ItemGlyphAt(Loc loc)
  {
    if (_itemLocs.TryGetValue(loc, out var items))
    {
      if (items is not null && items.Count > 0 && items[0].Z() >= 0)
      {
        // Always display rubble, boulders, etc on top of the other items
        foreach (Item item in items)
        {
          if (item.HasTrait<BlockTrait>())
            return item.Glyph;
        }

        return items[0].Glyph;
      }      
    }

    return EMPTY;
  }

  public IEnumerable<Item> BlockersAtLoc(Loc loc)
  {
    if (_itemLocs.TryGetValue(loc, out var items))
    {
      foreach (Item item in items)
      {
        if (item.HasTrait<BlockTrait>())
          yield return item;
      }
    }
  }

  public bool AreBlockersAtLoc(Loc loc)
  {
    if (_itemLocs.TryGetValue(loc, out var items))
    {
      foreach (Item item in items)
      {
        if (item.HasTrait<BlockTrait>())
          return true;
      }
    }
    
    return false;
  }

  public bool HazardsAtLoc(Loc loc)
  {
    if (_itemLocs.TryGetValue(loc, out var items))
    {
      foreach (Item item in items)
      {
        if (item.Name == "campfire")
          return true;
      }
    }

    return false;
  }

  public void RemoveActor(Actor actor)
  {
    Objs.Remove(actor.ID);
    _actorLocs.Remove(actor.Loc);

    foreach (Trait t in actor.Traits.Where(t => t is IGameEventListener))
    {
      EndOfRoundListeners.Remove((IGameEventListener)t);            
    }

    var toRemove = DeathWatchListeners.Where(dw => dw.Item1 == actor.ID).ToList();
    foreach (var dwl in toRemove)
      DeathWatchListeners.Remove(dwl);

    // should probably check their invetory too...
  }

  public Actor? Occupant(Loc loc)
  {
    if (_actorLocs.TryGetValue(loc, out ulong objId))
    {
      if (Objs.TryGetValue(objId, out var actor))
      {
        return (Actor)actor;
      }
    }

    return null;
  }

  public bool Occupied(Loc loc) => _actorLocs.ContainsKey(loc);

  public GameObj? GetObj(ulong id)
  {
    if (!Objs.TryGetValue(id, out GameObj? val))
      return null;
    return val;
  }

  // If this ever becomes too slow, I guess I can add indexing for items/actors
  // on a level?
  public List<GameObj> ObjectsOnLevel(int dungeonID, int level)
  {
    List<GameObj> objs = [];

    foreach (var loc in _actorLocs.Keys)
    {
      if (loc.DungeonID == dungeonID && loc.Level == level)
        objs.Add(Objs[_actorLocs[loc]]);
    }

    foreach (var loc in _itemLocs.Keys)
    {
      if (loc.DungeonID == dungeonID && loc.Level == level && _itemLocs.TryGetValue(loc, out var itemStack) && itemStack.Count > 0) 
      {
        objs.AddRange(itemStack);
      }
    }

    return objs;
  }

  public List<Actor> ActorsWithin(Loc loc, int range)
  {
    static bool InRange(Loc a, Loc b, int r)
    {
      return a.DungeonID == b.DungeonID && a.Level == b.Level && Util.Distance(a, b) <= r;
    }

    List<Actor> actors = [];
    foreach (var actorLoc in _actorLocs.Keys)
    {
      if (InRange(loc, actorLoc, range) && Objs[_actorLocs[actorLoc]] is Actor actor)
      {
        actors.Add(actor);
      }
    }

    return actors;
  }

  public IEnumerable<Actor> AllActors()
  {
    foreach (GameObj obj in Objs.Values)
    {
      if (obj is Actor actor)
        yield return actor;
    }    
  }

  public void AddNewActor(Actor actor, Loc loc)
  {
    actor.Loc = loc;
    Add(actor);
    AddToLoc(loc, actor);
  }

  public void Add(GameObj obj) => Objs[obj.ID] = obj;

  public void AddToLoc(Loc loc, Actor actor) => _actorLocs[loc] = actor.ID;
  
  public void SetToLoc(Loc loc, Item item)
  {
    item.Loc = loc;
    if (!_itemLocs.TryGetValue(loc, out var stack))
    {
      stack = [];
      _itemLocs.Add(loc, stack);
    }

    if (item.Type == ItemType.Zorkmid)
    {
      foreach (Item other in stack)
      {
        if (other.Type == ItemType.Zorkmid)
        {
          other.Value += item.Value;
          return;
        }
      }
    }
    
    // I could have made _items Stack<Item> instead of a list, but there
    // are times when I want to iterate over the items in a location,
    // and sometimes the player will want to remove an item from the middle.
    stack.Insert(0, item);
  }

  // This is really just used for restoring an itemdb from serialization
  public void AddStack(Loc loc, List<Item> stack)
  {
    _itemLocs[loc] = stack;
  }

  // It's probably dangerous/bad practice to return the list of items
  // so other parts of the game can manipulate it directly, but hey
  // it's easy and convenient
  public List<Item> ItemsAt(Loc loc)
  {
    if (!_itemLocs.TryGetValue(loc, out var stack))
      return [];
    
    return [..stack.Where(i => i.Type != ItemType.Environment && i.Type != ItemType.Fog)];
  }

  public List<Item> VisibleItemsAt(Loc loc)
  {
    if (!_itemLocs.TryGetValue(loc, out var stack))
      return [];
    
    return [..stack.Where(i => i.Type != ItemType.Environment && i.Type != ItemType.Fog && !i.HasTrait<HiddenTrait>())];
  }

  public List<Item> EnvironmentsAt(Loc loc)
  {
    if (!_itemLocs.TryGetValue(loc, out var stack))
      return [];
    return [..stack.Where(i => i.Type == ItemType.Environment || i.Type == ItemType.Fog)];
  }

  public Glyph? FogAtLoc(Loc loc, Loc playerLoc)
  {
    int d = Util.Distance(loc, playerLoc);
    if (_itemLocs.TryGetValue(loc, out var stack))
    {
      foreach (Item item in stack)
      {
        if (item.Type != ItemType.Fog)
          continue;

        foreach (Trait t in item.Traits)
        {
          if (t is OpaqueTrait opaque && d >= opaque.Visibility)
            return item.Glyph;
        }
      }
    }

    return null;
  }

  public int VisibilityAtLocation(Loc loc)
  {
    int v = int.MaxValue;

    if (_itemLocs.TryGetValue(loc, out var stack))
    {
      foreach (Item item in stack)
      {
        foreach (Trait t in item.Traits)
        {
          if (t is OpaqueTrait opaque && opaque.Visibility < v)
            v = opaque.Visibility;
        }
      }
    }

    return v;
  }

  public HashSet<Loc> OccupantsOnLevel(int dungeonID, int level) => 
    [.. _actorLocs.Keys.Where(k => k.DungeonID == dungeonID && k.Level == level)];

  public int LevelCensus(int dungeonID, int level) =>
    _actorLocs.Keys.Where(k => k.DungeonID == dungeonID && k.Level == level)
                   .Count();

  public void RemoveItemFromLoc(Loc loc, Item item)
  {
    _itemLocs[loc].Remove(item);
    item.Loc = Loc.Nowhere;
  }

  public void RemoveItemFromGame(Loc loc, Item item)
  {
    if (_itemLocs.TryGetValue(loc, out var value))
      _itemLocs[loc] = [.. value.Where(i => i.ID != item.ID)];
    Objs.Remove(item.ID);

    if (_itemLocs.TryGetValue(item.Loc, out var items))
      _itemLocs[item.Loc] = [.. items.Where(i => i.ID != item.ID)];

    foreach (Trait t in item.Traits.Where(t => t is IGameEventListener))
    {
      EndOfRoundListeners.Remove((IGameEventListener)t);            
    }

    var toRemove = DeathWatchListeners.Where(dw => dw.Item1 == item.ID).ToList();
    foreach (var dwl in toRemove)
      DeathWatchListeners.Remove(dwl);
  }

  public void ClearActorLoc(Loc loc) => _actorLocs.Remove(loc);
  public void SetActorToLoc(Loc loc, ulong id) => _actorLocs[loc] = id;

  public void ActorMoved(Actor a, Loc from, Loc to)
  {
    _actorLocs.Remove(from);
    _actorLocs[to] = a.ID;
    a.Loc = to;
  }

  public List<Actor> GetPerformers(int dungeonID, int level)
  {
    List<Actor> performers = [];

    foreach (var loc in _actorLocs.Keys.Where(k => k.DungeonID == dungeonID && k.Level == level))
    {      
      if (Objs[_actorLocs[loc]] is Actor actor)
        performers.Add(actor);
    }

    return performers;
  }

  public List<IGameEventListener> ActiveListeners()
  {
    List<IGameEventListener> listeners = [];

    foreach (var obj in Objs.Values)
    {
      listeners.AddRange(obj.Traits.OfType<IGameEventListener>()
                                   .Where(l => l.Listening));
    }

    return listeners;
  }
}

// A structure to store info about a dungeon
class Dungeon(int ID, string arrivalMessage)
{
  public int ID { get; init; } = ID;
  public Dictionary<Loc, Glyph> RememberedLocs = [];
  public Dictionary<int, Map> LevelMaps = [];
  public string ArrivalMessage { get; } = arrivalMessage;
  public List<MonsterDeck> MonsterDecks { get; set; } = [];

  public void AddMap(Map map)
  {
    int id = LevelMaps.Count == 0 ? 0 : LevelMaps.Keys.Max() + 1;
    LevelMaps.Add(id, map);
  }
}

// A data structure to store all the info about 
// the 'story' of the game. All the dungeon levels, etc
class Campaign
{
  public Town? Town { get; set; }
  public FactDb? FactDb { get; set; }
  public Dictionary<int, Dungeon> Dungeons = [];
  
  public void AddDungeon(Dungeon dungeon)
  {
    int id = Dungeons.Count == 0 ? 0 : Dungeons.Keys.Max() + 1;
    Dungeons.Add(id, dungeon);
  }

  public void AddDungeon(Dungeon dungeon, int id)
  {
    Dungeons[id] = dungeon;
  }
}