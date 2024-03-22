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
record struct Loc(int DungeonID, int Level, int Row, int Col)
{
    public static Loc Nowhere = new Loc(-1, -1, -1, -1);
    // A convenient method because this comes up a lot.
    public Loc Move(int RowDelta, int ColDelta)
    {
        return this with { Row = Row + RowDelta, Col = Col + ColDelta };
    }

    public readonly override string ToString() => $"{DungeonID},{Level},{Row},{Col}";

    public static Loc FromText(string text)
    {
        var digits = Util.DigitsRegex().Split(text)
                                       .Select(int.Parse).ToArray();
        return new Loc(digits[0], digits[1], digits[2], digits[3]);
    }    
}

record struct Glyph(char Ch, Colour Lit, Colour Unlit);

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
    void Alert(UIEventType eventType, GameState gs);
}

abstract class GameObj: IZLevel
{
    public const ulong PLAYER_ID = 1;
    private static ulong IDSeed = 2;
    public string Name { get; set; } = "";
    public virtual string FullName => Name;
    public virtual Glyph Glyph { get; set; }
    public Loc Loc { get; set; }
    
    public ulong ID { get; set; }

    public virtual List<(ulong, int, TerrainFlag)> Auras(GameState gs) => [];
    public GameObj() => ID = IDSeed++;
    public static ulong Seed => IDSeed;
    public static void SetSeed(ulong seed) => IDSeed = seed;

    public static ulong NextID => IDSeed++;

    public virtual int Z()
    {
        return 0;
    }
}

// Structure to store where items are in the world
class GameObjectDB
{
    public static readonly Glyph EMPTY = new('\0', Colours.BLACK, Colours.BLACK);

    public Dictionary<Loc, List<Item>> _itemLocs = [];
    public Dictionary<Loc, ulong> _actorLocs = [];
    public Dictionary<ulong, GameObj> _objs = [];

    public bool ItemsWithEffect(Loc loc, TerrainFlag flag)
    {
        if (_itemLocs.TryGetValue(loc, out var items))
        {
            foreach (var item in items)
            {
                if (item.Traits.Any(t => t.Effect == flag))
                    return true;
            }    
        }

        return false;
    }

    // I'm returning isItem because when remembering what glyphs were seen
    // (for displaying visited but out of site tiles) I want to remember items
    // but not actors
    public (Glyph, int, bool) TopGlyph(Loc loc)
    {
        var glyph = EMPTY;
        int z = 0;
        bool isItem = false;

        if (_actorLocs.TryGetValue(loc, out ulong id))
        {
            glyph = _objs[id].Glyph;
            z = _objs[id].Z();
        }
        
        if (_itemLocs.TryGetValue(loc, out var items))
        {
            foreach (var item in items)
            {
                if (item.Z() > z)
                {
                    glyph = item.Glyph;
                    z = item.Z();
                    isItem = true;
                }
            }
        }

        return (glyph, z, isItem);
    }

    // TODO: I think I can replace GlyphAt() and ItemGlyphAt() with TopGlyph()
    //   They're only used to querying what's below on chasm sqs
    // Basically, the sqr ignoring the occupant since we only want to remember
    // either the item stack or the tile
    public Glyph GlyphAt(Loc loc)
    {
        if (_actorLocs.TryGetValue(loc, out ulong id))
            return _objs[id].Glyph;
        else
            return ItemGlyphAt(loc);
    }

    public Glyph ItemGlyphAt(Loc loc)
    {
        if (_itemLocs.TryGetValue(loc, out var items))
        {
            if (items is not null && items.Count > 0 && items[0].Z() >= 0)
                return items[0].Glyph;
        }

        return EMPTY;
    }

    public void RemoveActor(Actor actor)
    {
        _objs.Remove(actor.ID);
        _actorLocs.Remove(actor.Loc);
    }

    public Actor? Occupant(Loc loc)
    {
        if (_actorLocs.TryGetValue(loc, out ulong objId))
        {
            if (_objs.TryGetValue(objId, out var actor))
            {
                return (Actor)actor;
            }
        }

        return null;        
    }

    public bool Occupied(Loc loc) => _actorLocs.ContainsKey(loc);

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

    public void AddToLoc(Loc loc, Actor actor)
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
            return stack.Where(i => i.Type != ItemType.Environment)
                        .ToList();
    }

    public List<Item> EnvironmentsAt(Loc loc)
    {
        if (!_itemLocs.TryGetValue(loc, out var stack))
            return [];
        else
            return stack.Where(i => i.Type == ItemType.Environment)
                        .ToList();
    }

    public void RemoveItem(Loc loc, Item item) => _itemLocs[loc].Remove(item);
    public void RemoveItemFromGame(Loc loc, Item item)
    {
        _itemLocs[loc].Remove(item);
        _objs.Remove(item.ID);
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
            var actor = _objs[_actorLocs[loc]] as Actor;
            if (actor is IPerformer performer)
                performers.Add(performer);

            performers.AddRange(actor.Inventory.ActiveItemTraits());
        }

        return performers;
    }
}
