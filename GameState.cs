
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
class GameState(Player p, Campaign c, Options opts, UserInterface ui, Random rng, int seed)
{
    public int Seed { get; init; } = seed;
    public Random Rng { get; set; } = rng;
    public Map? Map { get; set; }
    public Options? Options { get; set; } = opts;
    public Player Player { get; set; } = p;
    public int CurrLevel { get; set; }
    public int CurrDungeon { get; set; }
    public Campaign Campaign { get; set; } = c;
    public GameObjectDB ObjDB { get; set; } = new GameObjectDB();
    public List<IPerformer> Performers { get; set; } = [];
    public ulong Turn { get; set; }
    public DjikstraMap? DMap { get; private set; }
    public DjikstraMap? DMapDoors { get; private set; }
    public DjikstraMap? DMapFlight { get; private set; }
    public HashSet<Loc> RecentlySeen { get; set; } = [];    
    public ulong LastTarget { get; set; } = 0;

    // This might (probably will) expand into a hashtable of 
    // UIEventType mapped to a list of listeners
    List<IGameEventListener> _endOfRoundListeners { get; set; } = [];

    private UserInterface UI { get; set; } = ui;

    private int _currPerformer = 0;
        
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

    static readonly Dictionary<TileType, int> _passableFlying = new() {
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
    public void WritePopup(string msg, string title) => UI.Popup(msg, title);
    public void ClearMenu() => UI.CloseMenu();
    public UserInterface UIRef() => UI;

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

    // I made life difficult for myself by deciding that Turn 0 of the game is 
    // 8:00am T_T 1 turn is 10 seconds (setting aside all concerns about 
    // realism and how the amount of stuff one can do in 10 seconds will in no 
    // way correspond to one action in the game...) so an hour is  360 turns
    public (int, int) CurrTime()
    {
        // There are 1440 turns/day
        int normalized = (int) (Turn + 480) % 1440;
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
        var (d, level, row, col) = viewer.Loc;
        var map = Campaign.Dungeons[d].LevelMaps[level];
        var fov = FieldOfView.CalcVisible(radius, row, col, map, d, level, ObjDB);

        return fov.Contains((level, loc.Row, loc.Col));
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
        item.Loc = loc;
        item.ContainedBy = 0;
        
        var tile = TileAt(loc);
        List<Message> msgs = [];
        foreach (var flag in tile.TerrainFlags().Where(t => t != TerrainFlag.None))
        {
            string msg = item.ApplyEffect(flag, this, loc);
            if (msg != "")
            {
                msgs.Add(new Message(msg, loc));
            }
        }

        foreach (var t in item.Traits)
        {
            if (t is DamageTrait dt && dt.DamageType == DamageType.Fire)
                ApplyDamageEffectToLoc(loc, DamageType.Fire);
        }

        ObjDB.SetToLoc(loc, item);
        
        if (msgs.Count > 0)
            UI.AlertPlayer(msgs, "", this);        
    }

    public void ItemDestroyed(Item item, Loc loc)
    {
        var map = Campaign.Dungeons[loc.DungeonID].LevelMaps[loc.Level];
        map.RemoveEffectsFor(item.ID);
        ObjDB.RemoveItemFromGame(loc, item);
    
        foreach (var listener in item.Traits.OfType<IGameEventListener>())
        {
            RemoveListener(listener);
        }
    }

    public void ApplyDamageEffectToLoc(Loc loc, DamageType damageType)
    {
        List<Item> items = [];
        items.AddRange(ObjDB.ItemsAt(loc));
        items.AddRange(ObjDB.EnvironmentsAt(loc));
        List<Message> messages = [];
        var tile = TileAt(loc);
        bool fireStarted = false;

        switch (damageType)
        {
            case DamageType.Fire:
                if (tile.Flammable() && Rng.NextDouble() < 0.15)
                    fireStarted = true;
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
            default:
                break;
        }

        if (fireStarted)
        {
            var fire = ItemFactory.Fire(this);
            fire.Loc = loc;
            ObjDB.SetToLoc(loc, fire);
            CheckMovedEffects(fire, Loc.Nowhere, loc);

            var map = Campaign.Dungeons[loc.DungeonID].LevelMaps[loc.Level];
            if (tile.Type == TileType.Grass)
            {
                map.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.CharredGrass));
            }
            else if (tile.Type == TileType.Tree)
            {
                map.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.CharredStump));
            }
        }

        if (messages.Count > 0)
            UI.AlertPlayer(messages, "", this);
    }

    public void ActorKilled(Actor victim)
    {
        ((IPerformer)victim).RemoveFromQueue = true;
        ObjDB.RemoveActor(victim);

        if (victim == Player)
        {
            UI.KillScreen("You died :(", this);
            throw new PlayerKilledException();
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

        PlayerCreator.CheckLevelUp(Player, UI, Rng);

        var listeners = _endOfRoundListeners.Where(l => !l.Expired).ToList();
        foreach (var listener in listeners)
        {
            listener.Alert(UIEventType.EndOfRound, this);
        }
        _endOfRoundListeners = _endOfRoundListeners.Where(l => !l.Expired).ToList();
    }

    public void ActorMoved(Actor actor, Loc start, Loc dest)
    {        
        ObjDB.ActorMoved(actor, start, dest);
        
        // It might be more efficient to actually calculate the squares covered
        // by the old and new locations and toggle their set difference? But
        // maybe not enough for the more complicated code?        
        CheckMovedEffects(actor, start, dest);

        // Not making djikstra maps for the otherworld just yet.
        // Eventually I need to take into account whether or not
        // monsters can open doors, fly, etc. Multiple maps??
        if (actor is Player && dest.DungeonID > 0)
        {
            //long startTime = Stopwatch.GetTimestamp();

            DMap = new DjikstraMap(CurrentMap, 0, CurrentMap.Height, 0, CurrentMap.Width);
            DMap.Generate(_passableBasic, (dest.Row, dest.Col), 25);

            // I wonder how complicated it would be to generate the maps in parallel
            DMapDoors = new DjikstraMap(CurrentMap, 0, CurrentMap.Height, 0, CurrentMap.Width);
            DMapDoors.Generate(_passableWithDoors, (dest.Row, dest.Col), 25);

            DMapFlight = new DjikstraMap(CurrentMap, 0, CurrentMap.Height, 0, CurrentMap.Width);
            DMapFlight.Generate(_passableFlying, (dest.Row, dest.Col), 25);

            //var elapsed = Stopwatch.GetElapsedTime(startTime);
            //Console.WriteLine($"djikstra map time: {elapsed.TotalMicroseconds}");
        }
    }

    // Find all the game objects affecting a square with a particular
    // effect
    public List<GameObj> ObjsAffectingLoc(Loc loc, TerrainFlag effect)
    {        
        var objs = new List<GameObj>();

        var (dungeon, level, row, col) = loc;
        var map = Campaign.Dungeons[dungeon].LevelMaps[level];
        if (map.Effects.ContainsKey((row, col)) && map.Effects[(row, col)].Count > 0)
        {
            var effects = map.Effects[(row, col)];
            foreach (var k in effects.Keys)
            {
                if ((effects[k] & effect) != TerrainFlag.None) 
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

    public void CheckMovedEffects(GameObj obj, Loc start, Loc dest)
    {
        Map? startMap = null;
        // first we want to clear out the old effects
        if (start != Loc.Nowhere)
        {
            startMap = Campaign.Dungeons[start.DungeonID].LevelMaps[start.Level];
            startMap.RemoveEffectsFor(obj.ID);
        }

        var destMap = Campaign.Dungeons[dest.DungeonID].LevelMaps[dest.Level];
        var auras = obj.Auras(this).Where(a => a.Item2 > 0);
        foreach (var aura in auras)
        {
            ulong id = aura.Item1;

            startMap?.RemoveEffectsFor(id);
            int radius = aura.Item2;
            TerrainFlag effect = aura.Item3;
            
            foreach (var sq in FieldOfView.CalcVisible(radius, dest.Row, dest.Col, destMap, dest.DungeonID, dest.Level, ObjDB))
            {
                destMap.ApplyEffectAt(effect, sq.Item2, sq.Item3, id);
            }
        }
    }

    // I only have light effects in the game right now, but I also have ambitions
    public void ToggleEffect(GameObj obj, Loc loc, TerrainFlag effect, bool on)
    {
        var (dungeon, level, row, col) = loc;
        var currDungeon = Campaign.Dungeons[dungeon];
        var map = currDungeon.LevelMaps[level];

        foreach (var aura in obj.Auras(this))
        {
            if (aura.Item3 == effect)
            {
                var sqs = FieldOfView.CalcVisible(aura.Item2, row, col, map, dungeon, level, ObjDB);
                foreach (var sq in sqs)
                {
                    if (on)
                        map.ApplyEffectAt(effect, sq.Item2, sq.Item3, aura.Item1);
                    else
                        map.RemoveEffectFromMap(effect, aura.Item1);
                }
            }
        }
    }

    public void RegisterForEvent(UIEventType eventType, IGameEventListener listener)
    {
        if (eventType == UIEventType.EndOfRound)
            _endOfRoundListeners.Add(listener);
        else
            throw new NotImplementedException("I haven't created any other event listeners yet :o");
    }

    public void StopListening(UIEventType eventType, IGameEventListener listener)
    {
        if (eventType == UIEventType.EndOfRound)
            _endOfRoundListeners.Remove(listener);
        else
            throw new NotImplementedException("I haven't created any other event listeners yet :o");
    }

    // Remove listener from all events it might be listening for, although I
    // have only one type right now
    public void RemoveListener(IGameEventListener listener)
    {
        _endOfRoundListeners.Remove(listener);
    }
}