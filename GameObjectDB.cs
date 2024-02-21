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

using System.Text.RegularExpressions;

namespace Yarl2;

// I'm only doing this because the JSONSerializer can't handle
// tuples and I'd have to convert the DB keys to something else
// anyhow.
// Although it does make a nicer parameter to pass around to methods
record struct Loc(int DungeonID, int Level, int Row, int Col)
{
    public override string ToString() => $"{DungeonID},{Level},{Row},{Col}";
    public static Loc FromText(string text)
    {
        var digits = Regex.Split(text, @"\D+")
                                    .Select(int.Parse).ToArray();
        return new Loc(digits[0], digits[1], digits[2], digits[3]);
    }
}

record struct Glyph(char Ch, Colour Lit, Colour Unlit);

abstract class GameObj
{
    public const ulong PLAYER_ID = 1;
    private static ulong IDSeed = 1;
    public string Name { get; set; }
    public virtual string FullName { get; } 
    public virtual Glyph Glyph { get; set; }
    public Loc Loc { get; set; }
    
    public ulong ID { get; set; }

    public virtual List<(ulong, int)> EffectSources(TerrainFlags flags, GameState gs) => [];
    public GameObj() => ID = IDSeed++;
    public static ulong Seed => IDSeed;
    public static void SetSeed(ulong seed) => IDSeed = seed;

    public static ulong NextID => IDSeed++;
}

// Structure to store where items are in the world
class GameObjectDB
{
    public static readonly Glyph EMPTY = new Glyph('@', Colours.BLACK, Colours.BLACK);

    public Dictionary<Loc, List<Item>> _itemLocs = [];
    public Dictionary<Loc, ulong> _actorLocs = [];
    public Dictionary<ulong, GameObj> _objs = [];

    public Glyph GlyphAt(Loc loc)
    {
        if (_actorLocs.TryGetValue(loc, out ulong id))
            return _objs[id].Glyph;
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
        _actorLocs[loc] = actor.ID;
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
        if (_actorLocs[from] == a.ID)
        {
            _actorLocs.Remove(from);
            _actorLocs[to] = a.ID;
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
                performers.AddRange(item.ActiveTraits());                
            }
        }

        foreach (var loc in _actorLocs.Keys.Where(k => k.DungeonID == dungeonID && k.Level == level))
        {
            var actor = _objs[_actorLocs[loc]];
            if (actor is IPerformer performer)
                performers.Add(performer);

            if (actor is IItemHolder holder)
            {
                performers.AddRange(holder.Inventory.ActiveItemTraits());
            }
        }

        return performers;
    }
}
