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

namespace Yarl2;

// I'm only doing this because the JSONSerializer can't handle
// tuples and I'd have to convert the DB keys to something else
// anyhow.
// Although it does make a nicer parameter to pass around to methods
record struct Loc(int DungeonID, int Level, int Row, int Col);

record struct Glyph(char Ch, Colour Lit, Colour Unlit);

abstract class GameObj
{
    private static ulong IDSeed = 0;
    public string Name { get; set; }
    public virtual Glyph Glyph { get; set; }

    private ulong _id;
    public ulong ID => _id;

    public virtual int LightRadius(GameState gs) => 0;

    public GameObj() => _id = IDSeed++;
}

// Structure to store where items are in the world
class GameObjectDB
{
    public static readonly Glyph EMPTY = new Glyph('@', Colours.BLACK, Colours.BLACK);

    public Dictionary<Loc, List<Item>> _items = [];
    public Dictionary<Loc, Monster> _monsters = [];

    public Glyph GlyphAt(Loc loc)
    {
        if (_monsters.TryGetValue(loc, out Monster monster))
            return monster.Glyph;
        else if (_items.TryGetValue(loc, out List<Item> items))
        {
            if (items is not null && items.Count > 0)
                return items[0].Glyph;
        }

        return EMPTY;
    }

    public void Add(Loc loc, GameObj obj)
    {
        if (obj is Monster)
        {
            _monsters.Add(loc, (Monster) obj);
        }
        else
        {
            if (!_items.TryGetValue(loc, out var stack))
            {
                stack = [];
                _items.Add(loc, stack);
            }

            // I could have made _items Stack<Item> instead of a list, but there
            // are times when I want to iterate over the items in a location,
            // and sometimes the player will want to remove an item from the middle.
            stack.Insert(0, (Item) obj);
        }
    }

    // This is really just used for restoring an itemdb from serialization
    public void AddStack(Loc loc, List<Item> stack)
    {
       _items[loc] = stack;
    }

    // It's probably dangerous/bad practice to return the list of items
    // so other parts of the game can manipulate it directly, but hey
    // it's easy and convenient
    public List<Item> ItemsAt(Loc loc)
    {
        if (!_items.TryGetValue(loc, out var stack))
            return [];
        else
            return stack;
    }
    
    public void MonsterMoved(Monster m, Loc from, Loc to)
    {
        if (_monsters[from] == m)
        {
            _monsters.Remove(from);
            _monsters[to] = m;
        }
    }

    public List<IPerformer> GetPerformers(int dungeonID, int level)
    {
        List<IPerformer> performers = [];
        
        // I wonder if it's worth building 'indexes' of the objects by level and maybe dungeon?
        // To speed stuff like this up when there's lots of game objects
        foreach (var loc in _items.Keys.Where(k => k.DungeonID == dungeonID && k.Level == level)) 
        { 
            foreach (var item in _items[loc]) 
            {
                if (item is IPerformer)
                    performers.Add((IPerformer)item);
            }
        }

        foreach (var loc in _monsters.Keys.Where(k => k.DungeonID == dungeonID && k.Level == level))
        {
            if (_monsters[loc] is IPerformer)
                performers.Add(_monsters[loc]);
        }

        return performers;
    }

    public List<(Loc, List<Item>)> ItemDump() => _items.Keys.Select(k => (k, _items[k])).ToList();
    public List<(Loc, Monster)> MonsterDump() => _monsters.Keys.Select(k => (k, _monsters[k])).ToList();
}
