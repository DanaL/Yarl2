
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

using System.Reflection.Emit;

namespace Yarl2;

// The queue of actors to act will likely need to go here.
internal class GameState
{
    public Map? Map { get; set; }
    public Options? Options { get; set;}
    public Player? Player { get; set; }
    public int CurrLevel { get; set; }
    public int CurrDungeon { get; set; }
    public Campaign Campaign { get; set; }
    public GameObjectDB ObjDB { get; set; } = new GameObjectDB();
    public List<IPerformer> CurrPerformers { get; set; } = [];
    
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

    public void ItemDropped(Item item, int row, int col)
    {
        var loc = new Loc(CurrDungeon, CurrLevel, row, col);
        item.Loc = loc;
        ObjDB.Add(loc, item);
    }

    public void RefreshPerformers()
    {
        CurrPerformers.Clear();
        CurrPerformers.Add(Player);
        CurrPerformers.AddRange(ObjDB.GetPerformers(CurrDungeon, CurrLevel));

        foreach (var performer in CurrPerformers)
        {
            performer.Energy = performer.Recovery;
        }
    }

    public void ActorMoved(Actor actor, Loc start, Loc dest)
    {
        if (actor is not Yarl2.Player)
        {
            var m = (Monster)actor;            
            ObjDB.MonsterMoved(m, start, dest);
        }

        // It might be more efficient to actually calculate the squares covered
        // by the old and new locations and toggle their set difference? But
        // maybe not enough for the more complicated code?        
        CheckMovedEffects(actor, start, dest, TerrainFlags.Lit);
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

        oldSqs.ExceptWith(newSqs);
        foreach (var (objID, dungeon, level, _, _) in oldSqs)
        {
            var map = Campaign.Dungeons[dungeon].LevelMaps[level];
            map.RemoveEffect(effect, objID);
        }

        foreach (var (objID, dungeon, level, r, c) in newSqs)
        {
            var map = Campaign.Dungeons[dungeon].LevelMaps[level];
            map.ApplyEffect(effect, r, c, objID);
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
                    map.RemoveEffect(effect, sourceID);
            }
        }
    }
}