
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
        ToggleEffect(actor, start, TerrainFlags.Lit, false);
        ToggleEffect(actor, dest, TerrainFlags.Lit, true);        
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

    // I only have light effects in the game right now, but I also have ambitions        
    public void ToggleEffect(GameObj obj, Loc loc, TerrainFlags effect, bool on)
    {
        var (dungeon, level, row, col) = loc;
        var currDungeon = Campaign.Dungeons[dungeon];
        var map = currDungeon.LevelMaps[level];
        var isPlayer = obj is Player;

        foreach (var (sourceID, radius) in obj.EffectSources(effect, this)) 
        {
            var sqs = FieldOfView.CalcVisible(radius, row, col, map, level);
            foreach (var sq in sqs)
            {
                if (on)
                    map.ApplyEffect(effect, sq.Item2, sq.Item3, sourceID);
                else
                    map.RemoveEffect(effect, sourceID);

                // I guess maybe move this back to UI, or the Move action?
                if (isPlayer)
                    currDungeon.RememberedSqs.Add(sq);
            }
        }
    }
}