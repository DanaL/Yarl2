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

using System.ComponentModel;

namespace Yarl2;

// I'm only doing this because the JSONSerializer can't handle
// tuples and I'd have to convert the DB keys to something else
// anyhow.
// Although it does make a nicer parameter to pass around to methods
record struct Loc(int DungeonID, int Level, int Row, int Col);

record struct Glyph(char Ch, Colour Lit, Colour Unlit);

abstract class GameObj
{
    public const ulong PLAYER_ID = 1;
    private static ulong IDSeed = 1;
    public string Name { get; set; }
    public virtual string FullName { get; } 
    public virtual Glyph Glyph { get; set; }
    public Loc Loc { get; set; }
    
    private ulong _id;
    public ulong ID => _id;

    public virtual List<(ulong, int)> EffectSources(TerrainFlags flags, GameState gs) => [];
    
    public GameObj() => _id = IDSeed++;
}

// Structure to store where items are in the world
class GameObjectDB
{
    public static readonly Glyph EMPTY = new Glyph('@', Colours.BLACK, Colours.BLACK);

    public Dictionary<Loc, List<Item>> _itemLocs = [];
    public Dictionary<Loc, Actor> _actorLocs = [];
    public Dictionary<ulong, GameObj> _objs = [];

    public Glyph GlyphAt(Loc loc)
    {
        if (_actorLocs.TryGetValue(loc, out Actor actor))
            return actor.Glyph;
        else if (_itemLocs.TryGetValue(loc, out List<Item> items))
        {
            if (items is not null && items.Count > 0)
                return items[0].Glyph;
        }

        return EMPTY;
    }

    public GameObj? GetObj(ulong id) 
    {
        if (!_objs.TryGetValue(id, out GameObj? val))
            return null;
        return val;
    }

    public void Add(GameObj obj)
    {
        _objs[obj.ID] = obj;
    }

    public void SetToLoc(Loc loc, Actor actor)
    {
        _actorLocs[loc] = actor;
    }

    public void SetToLoc(Loc loc, Item item)
    {
        if (!_itemLocs.TryGetValue(loc, out var stack))
        {
            stack = [];
            _itemLocs.Add(loc, stack);
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
        else
            return stack;
    }
    
    public void ActorMoved(Actor a, Loc from, Loc to)
    {
        if (_actorLocs[from] == a)
        {
            _actorLocs.Remove(from);
            _actorLocs[to] = a;
        }
    }

    public List<IPerformer> GetPerformers(int dungeonID, int level)
    {
        List<IPerformer> performers = [];
        
        // I wonder if it's worth building 'indexes' of the objects by level and maybe dungeon?
        // To speed stuff like this up when there's lots of game objects
        foreach (var loc in _itemLocs.Keys.Where(k => k.DungeonID == dungeonID && k.Level == level)) 
        { 
            foreach (var item in _itemLocs[loc]) 
            {
                if (item is IPerformer)
                    performers.Add((IPerformer)item);
            }
        }

        foreach (var loc in _actorLocs.Keys.Where(k => k.DungeonID == dungeonID && k.Level == level))
        {
            if (_actorLocs[loc] is IPerformer performer)
                performers.Add(performer);
        }

        return performers;
    }

    public List<(Loc, List<Item>)> ItemDump() => _itemLocs.Keys.Select(k => (k, _itemLocs[k])).ToList();
    public List<(Loc, Actor)> ActorDump() => _actorLocs.Keys.Select(k => (k, _actorLocs[k])).ToList();
}
