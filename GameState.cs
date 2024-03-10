
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

namespace Yarl2;

// The queue of actors to act will likely need to go here.
class GameState(Player p, Campaign c, Options opts, UserInterface ui)
{    
    public Map? Map { get; set; }
    public Options? Options { get; set; } = opts;
    public Player Player { get; set; } = p;
    public int CurrLevel { get; set; }
    public int CurrDungeon { get; set; }
    public Campaign Campaign { get; set; } = c;
    public GameObjectDB ObjDB { get; set; } = new GameObjectDB();
    public List<IPerformer> Performers { get; set; } = [];
    public ulong Turn { get; set; }
    private int _currPerformer = 0;
    public DjikstraMap? DMap { get; private set; }
    public DjikstraMap? DMapDoors { get; private set; }
    public HashSet<Loc> RecentlySeen { get; set; } = [];
    public UserInterface UI { get; set; } = ui;
    
    static readonly Dictionary<TileType, int> _passableBasic = new() { 
        { TileType.DungeonFloor, 1 },
        { TileType.Landmark, 1 },
        { TileType.Upstairs, 1 },
        { TileType.Downstairs, 1 },
        { TileType.OpenDoor, 1 },
        { TileType.BrokenDoor, 1 },
        { TileType.WoodBridge, 1 }
    };

    static readonly Dictionary<TileType, int> _passableWithDoors = new() {
        { TileType.DungeonFloor, 1 },
        { TileType.Landmark, 1 },
        { TileType.Upstairs, 1 },
        { TileType.Downstairs, 1 },
        { TileType.OpenDoor, 1 },
        { TileType.BrokenDoor, 1 },
        { TileType.ClosedDoor, 1 },
        { TileType.WoodBridge, 1 }
    };

    public void EnterLevel(int dungeon, int level)
    {
        CurrLevel = level;
        CurrDungeon = dungeon;

        // Once the queue of actors is implemented, we will need to switch them
        // out here.
    }

    public Dungeon CurrentDungeon => Campaign!.Dungeons[CurrDungeon];
    public Map CurrentMap => Campaign!.Dungeons[CurrDungeon].LevelMaps[CurrLevel];
    public bool InWilderness => CurrDungeon == 0;

    public Tile TileAt(Loc loc)
    {
        var d = Campaign!.Dungeons[loc.DungeonID];
        var map = d.LevelMaps[loc.Level];
        return map.TileAt(loc.Row, loc.Col);
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

    public void ItemDropped(Item item, int row, int col)
    {
        var loc = new Loc(CurrDungeon, CurrLevel, row, col);
        item.Loc = loc;
        item.ContainedBy = 0;
        ObjDB.SetToLoc(loc, item);
    }

    public void ActorKilled(Actor victim)
    {
        ((IPerformer)victim).RemoveFromQueue = true;
        ObjDB.RemoveActor(victim);
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
    
    public void RefreshPerformers()
    {
        IPerformer? curr = null;
        if (Performers.Count > 0)
        {
             curr = Performers[_currPerformer];
        }

        Performers.Clear();
        Performers.AddRange(ObjDB.GetPerformers(CurrDungeon, CurrLevel));

        if (curr is not null)
        {
            _currPerformer = Performers.IndexOf(curr);
        }
    }

    public IPerformer NextPerformer()
    {
        do
        {
            // This is slightly different than a monster being killed because this just
            // removes them from the queue. A burnt out torch still exists as an item in
            // the game but a dead monster needs to be removed from the GameObjDb as well
            if (Performers[_currPerformer].RemoveFromQueue)
            {
                // Don't need to increment p here, because removing the 'dead'
                // performer will set up the next one
                Performers.RemoveAt(_currPerformer);
                if (_currPerformer >= Performers.Count) 
                {
                    ++Turn;
                    _currPerformer = 0;
                    EndOfTurn();
                }
            }
            
            if (Performers[_currPerformer].Energy < 1.0)
            {
                Performers[_currPerformer].Energy += Performers[_currPerformer].Recovery;
                ++_currPerformer;
            }

            if (_currPerformer >= Performers.Count)
            {                
                ++Turn;
                _currPerformer = 0;
                EndOfTurn();
            }

            if (Performers[_currPerformer].Energy >= 1.0 && !Performers[_currPerformer].RemoveFromQueue)
                return Performers[_currPerformer];
        }
        while (Performers.Count > 0);

        throw new Exception("Hmm we should never run out of performers");
    }

    // Not sure if this is the right spot for this.  Maybe the player should have a feature/trait
    // that's countdown timer for healing. Then its period can be tweaked by effects and items.
    // I don't what to have every single effect have its own turn like light sources do, but 
    // maybe Actors can have a list of effects I check for each turn?
    //
    // Also not sure how often monsters should regenerate.
    void EndOfTurn()
    {
        if (Turn % 23 == 0)
        {
            Player.Stats[Attribute.HP].Change(1);
        }

        PlayerCreator.CheckLevelUp(Player, UI, UI.Rng);
    }

    public void ActorMoved(Actor actor, Loc start, Loc dest)
    {        
        ObjDB.ActorMoved(actor, start, dest);
        
        // It might be more efficient to actually calculate the squares covered
        // by the old and new locations and toggle their set difference? But
        // maybe not enough for the more complicated code?        
        CheckMovedEffects(actor, start, dest, TerrainFlags.Lit);

        // Not making djikstra maps for the otherworld just yet.
        // Eventually I need to take into account whether or not
        // monsters can open doors, fly, etc. Multiple maps??
        if (actor is Player && dest.DungeonID > 0)
        {
            long startTime = Stopwatch.GetTimestamp();

            DMap = new DjikstraMap(CurrentMap, 0, CurrentMap.Height, 0, CurrentMap.Width);
            DMap.Generate(_passableBasic, (dest.Row, dest.Col));

            // I wonder how complicated it would be to generate the maps in parallel
            DMapDoors = new DjikstraMap(CurrentMap, 0, CurrentMap.Height, 0, CurrentMap.Width);
            DMapDoors.Generate(_passableWithDoors, (dest.Row, dest.Col));

            var elapsed = Stopwatch.GetElapsedTime(startTime);
            Console.WriteLine($"djikstra map time: {elapsed.TotalMicroseconds}");
        }
    }

    // Find all the game objects affecting a square with a particular
    // effect
    public List<GameObj> ObjsAffectingLoc(Loc loc, TerrainFlags effect)
    {        
        var objs = new List<GameObj>();

        var (dungeon, level, row, col) = loc;
        var map = Campaign.Dungeons[dungeon].LevelMaps[level];
        if (map.Effects.ContainsKey((row, col)) && map.Effects[(row, col)].Count > 0)
        {
            var effects = map.Effects[(row, col)];
            foreach (var k in effects.Keys)
            {
                if ((effects[k] & effect) != TerrainFlags.None) 
                {
                    var o = ObjDB.GetObj(k);
                    if (o is not null)
                        objs.Add(o);
                }
                    
            }
        }

        return objs;
    }

    // Make a noise in the dungeon, start at the source and flood-fill out 
    // decrementing the volume until we hit 0. We'll alert any Actors found
    // the noise
    public HashSet<ulong> Noise(ulong sourceID, int startRow, int startCol, int volume)
    {
        long startTime = Stopwatch.GetTimestamp();
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
                var occ = ObjDB.Occupant(new Loc(CurrDungeon, CurrLevel, n.Item1, n.Item2));
                if (occ is not null)
                {
                    occ.HearNoise(sourceID, startRow, startCol, this);
                    alerted.Add(occ.ID);
                }

                if (curr.Item3 > 1)
                    q.Enqueue((n.Item1, n.Item2, curr.Item3 - 1));
            }
        }

        var elapsed = Stopwatch.GetElapsedTime(startTime);
        //Console.WriteLine($"noise time ({sourceID}): {elapsed.TotalMicroseconds}");

        return alerted;
    }

    // This is still an extremely inefficient way to handle updating a moving source
    // such as a light :/
    public void CheckMovedEffects(GameObj obj, Loc start, Loc dest, TerrainFlags effect)
    {        
        var newSqs = new HashSet<(ulong, int, int, int, int)>();
        var oldSqs = new HashSet<(ulong, int, int, int, int)>();
        
        foreach (var (sourceID, radius) in obj.EffectSources(effect, this))
        {
            var (dungeon, level, row, col) = start;
            var currDungeon = Campaign.Dungeons[dungeon];
            var map = currDungeon.LevelMaps[level];
            foreach (var sq in FieldOfView.CalcVisible(radius, row, col, map, level))
                oldSqs.Add((sourceID, dungeon, level, sq.Item2, sq.Item3));
            (dungeon, level, row, col) = dest;
             currDungeon = Campaign.Dungeons[dungeon];
            map = currDungeon.LevelMaps[level];
            foreach (var sq in FieldOfView.CalcVisible(radius, row, col, map, level))
                newSqs.Add((sourceID, dungeon, level, sq.Item2, sq.Item3));
        }

        List<(ulong, int, int, int, int)> toAdd = [];
        List<(ulong, int, int, int, int)> toClear = [];
        foreach (var sq in newSqs) 
        {
            if (!oldSqs.Contains(sq))
                toAdd.Add(sq);
        }
        foreach (var sq in oldSqs)
        {
            if (!newSqs.Contains(sq))
                toClear.Add(sq);
        }

        foreach (var (objID, dungeon, level, r, c) in toAdd)
        {
            var map = Campaign.Dungeons[dungeon].LevelMaps[level];
            map.ApplyEffect(effect, r, c, objID);
        }
        foreach (var (objID, dungeon, level, r, c) in toClear)
        {
            var map = Campaign.Dungeons[dungeon].LevelMaps[level];
            map.RemoveAtLoc(effect, r, c, objID);
        }        
    }

    // I only have light effects in the game right now, but I also have ambitions
    public void ToggleEffect(GameObj obj, Loc loc, TerrainFlags effect, bool on)
    {
        var (dungeon, level, row, col) = loc;
        var currDungeon = Campaign.Dungeons[dungeon];
        var map = currDungeon.LevelMaps[level];

        foreach (var (sourceID, radius) in obj.EffectSources(effect, this))
        {
            var sqs = FieldOfView.CalcVisible(radius, row, col, map, level);
            foreach (var sq in sqs)
            {
                if (on)
                    map.ApplyEffect(effect, sq.Item2, sq.Item3, sourceID);
                else
                    map.RemoveEffectFromMap(effect, sourceID);
            }
        }
    }
}