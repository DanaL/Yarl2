// Delve - A roguelike computer RPG
// Written in 2026 by Dana Larose <ywg.dana@gmail.com>
//
// To the extent possible under law, the author(s) have dedicated all copyright
// and related and neighboring rights to this software to the public domain
// worldwide. This software is distributed without any warranty.
//
// You should have received a copy of the CC0 Public Domain Dedication along 
// with this software. If not, 
// see <http://creativecommons.org/publicdomain/zero/1.0/>.

namespace Yarl2;

class Planner
{
  static Sequence GoToBuilding(Actor actor, GameState gs, Map map, HashSet<Loc> area)
  {
    HashSet<Loc> floors = OnlyFloorsInArea(map, area);
    FindPathToArea pathBuilder = new(floors, gs);
    BehaviourNode movePlan = new WalkPath(pathBuilder.BuildPath(actor.Loc));

    BehaviourNode nightTest = new IsNight();
    return new Sequence([movePlan, new RepeatWhile(nightTest, new WanderInArea(floors))]);
  }

  static Sequence VisitTavern(Actor actor, GameState gs)
  {
    HashSet<Loc> tavernFloors = OnlyFloorsInArea(gs.Wilderness, gs.Town.Tavern);
    FindPathToArea pathBuilder = new(tavernFloors, gs);
    BehaviourNode movePlan = new WalkPath(pathBuilder.BuildPath(actor.Loc));

    BehaviourNode eveningTest = new IsEvening();
    return new Sequence(
      [movePlan, new RepeatWhile(eveningTest, new WanderInArea(tavernFloors))]
    );
  }

  static HashSet<Loc> OnlyFloorsInArea(Map map, HashSet<Loc> area)
  {
    static bool IsFloor(Map map, Loc loc)
    {
      TileType tile = map.TileAt(loc.Row, loc.Col).Type;
      return tile == TileType.WoodFloor || tile == TileType.StoneFloor;
    }

    return [.. area.Where(l => IsFloor(map, l))];
  }

  static Sequence CreateSmithPlan(Actor actor, GameState gs)
  {
    List<BehaviourNode> nodes = [];
    HashSet<Loc> smithy = OnlyFloorsInArea(gs.Wilderness, gs.Town.Smithy);

    var (hour, _) = gs.CurrTime();
    if (hour >= 7 && hour < 19)
    {
      if (!gs.Town.Smithy.Contains(actor.Loc))
      {
        FindPathToArea pathBuilder = new(smithy, gs);
        nodes.Add(new WalkPath(pathBuilder.BuildPath(actor.Loc)));
      }

      nodes.Add(new RepeatWhile(new IsDaytime(), new WanderInArea(smithy)));
      return new Sequence(nodes);
    }
    else if (hour >= 19 && hour < 22)
    {
      return VisitTavern(actor, gs);
    }
    else
    {
      return GoToBuilding(actor, gs, gs.Wilderness, gs.Town.Smithy);
    }
  }

  static BehaviourNode CreateMayorPlan(Actor actor, GameState gs)
  {
    var (hour, _) = gs.CurrTime();
    if (hour >= 7 && hour < 19)
    {
      // daytimeplan
      FindPathToArea pathBuilder = new(gs.Town.TownSquare, gs);
      BehaviourNode movePlan = new WalkPath(pathBuilder.BuildPath(actor.Loc));

      BehaviourNode daytimeTest = new IsDaytime();
      return new Sequence(
        [movePlan, new RepeatWhile(daytimeTest, new WanderInArea(gs.Town.TownSquare))]
      );
    }
    else if (hour >= 19 && hour < 22)
    {
      return VisitTavern(actor, gs);
    }
    else
    {
      int homeId = actor.Stats[Attribute.HomeID].Curr;
      return GoToBuilding(actor, gs, gs.Wilderness, gs.Town.Homes[homeId]);
    }
  }

  static BehaviourNode CreateGreedyMonster(Mob mob, GameState gs)
  {
    Sequence pickUpGold = new([
      new StandingOn("zorkmid"),
      new PickupItem("zorkmid")
    ]);

    FindNearbyItem goldFinder = new(mob, gs, "zorkmid");
    BehaviourNode seekGold = new FindGoal(goldFinder);
    Selector huntGold = new([
      seekGold,
      pickUpGold
    ]);

    Sequence gold = new([new Not(new InDanger()), huntGold]) { Label = "seekgold" };

    // Insert the seek gold node after inactive. The greedy monster will hunt
    // gold unless it is immediately adjacent to the player.
    Selector plan = CreateMonsterPlan(mob);
    int i = 0;
    foreach (BehaviourNode node in plan.Children)
    {
      if (node.Label == "inactive")
        break;
      ++i;
    }
    plan.Children.Insert(i + 1, gold);

    return plan;
  }

  static Selector CreateMimicPlan(Mob mimic)
  {    
    List<BehaviourNode> actions = [];
    List<BehaviourNode> passive = [];
    foreach (Power p in mimic.Powers)
    {
      // Some of the powers have slightly more complicated ways of
      // calculating if they are available to use so I am doing them as 
      // subclasses of UsePower. If I get too many of them and this gets gross,
      // I'll have to come up with something cleaner. An actual factory or such?
      BehaviourNode up = p.Name switch
      {
        "Gulp" => new GulpPower(p),
        "Crush" => new CrushPower(p),
        "HealAllies" => new HealAlliesPower(p),
        "Spores" => new SporesPower(p),
        "TurnIntoBats" => new UseTurnIntoBatsPower(p),
        "Nudity" or "FogCloud" => new SeeToTargetPower(p),
        "Whirlpool" => 
          new UseWhirlpoolPower(p),
        _ => new UsePower(p)
      };

      actions.Add(up);
      if (p.Type == PowerType.Passive)
        passive.Add(up);
    }

    List<BehaviourNode> plan = [
      new Sequence([new IsImmobilized(), new PassTurn()]) { Label = "immobilized" },
      new Sequence([new CheckMonsterAttitude(Mob.INACTIVE), new PassTurn()]) { Label = "inactive" },
      new Sequence([new IsFrightened(), new TryToEscape()]) { Label = "scared" }
    ];

    plan.Add(new Sequence([new IsDisguised(), new Selector([ new Selector(actions), new PassTurn()])]) { Label = "disguised" });
    plan.Add(new Selector([new Selector(actions), 
      new PickWithOdds([(new ChaseTarget(), 5), (new AssumeDisguise(), 2)]), 
      new PassTurn()]) { Label = "revealed" });

    return new Selector(plan);
  }

  // This is just the standard monster plan, but Powers selector is replaced
  // by a RandoSelector
  static BehaviourNode CreateRandoMonsterPlan(Mob mob)
  {
    BehaviourNode plan = CreateMonsterPlan(mob);
    ReplacePowersSelector(plan);
    return plan;
  }

  static void ReplacePowersSelector(BehaviourNode node)
  {
    if (node is Selector selector)
    {
      for (int i = 0; i < selector.Children.Count; i++)
      {
        if (selector.Children[i] is Selector childSelector && childSelector.Label == "powers")
        {
          selector.Children[i] = new RandoSelector(childSelector.Children) { Label = "powers" };
        }
        else
        {
          ReplacePowersSelector(selector.Children[i]);
        }
      }
    }
    else if (node is Sequence sequence)
    {
      for (int i = 0; i < sequence.Children.Count; i++)
      {
        if (sequence.Children[i] is Selector childSelector && childSelector.Label == "powers")
        {
          sequence.Children[i] = new RandoSelector(childSelector.Children) { Label = "powers" };
        }
        else
        {
          ReplacePowersSelector(sequence.Children[i]);
        }
      }
    }
  }

  static Selector CreateMonsterPlan(Mob mob)
  {
    bool immobile = mob.HasTrait<ImmobileTrait>();

    List<BehaviourNode> actions = [];
    List<BehaviourNode> passive = [];
    foreach (Power p in mob.Powers)
    {
      // Ugh a few of the powers have slightly more complicated ways of
      // calculating if they are available to use so I am doing them as 
      // subclasses of UsePower. If I get too many of them and this gets gross,
      // I'll have to come up with something cleaner. An actual factory or such?
      BehaviourNode up = p.Name switch
      {
        "Gulp" => new GulpPower(p),
        "Crush" => new CrushPower(p),
        "HealAllies" => new HealAlliesPower(p),
        "Spores" => new SporesPower(p),
        "TurnIntoBats" => new UseTurnIntoBatsPower(p),
        "Nudity" or "FogCloud" => new SeeToTargetPower(p),
        _ => new UsePower(p)
      };

      actions.Add(up);
      if (p.Type == PowerType.Passive)
        passive.Add(up);
    }

    // This will make the monster move to toward the player/target until they 
    // are adjacent and then just hang out
    if (actions.Count == 0 && passive.Count == 0)
      actions.Add(new LurkNearTarget());

    // Not yet handling confused monsters, etc

    List<BehaviourNode> plan = [];

    // A paralyzed monster will just pass its turn
    plan.Add(new Sequence([new IsImmobilized(), new PassTurn()]) { Label = "immobilized" });

    // As will an inactive one
    plan.Add(new Sequence([new CheckMonsterAttitude(Mob.INACTIVE), new PassTurn()]) { Label = "inactive" });

    // An indifferent monster might use Passive abilities and/or wander randomly
    // (if not immobile)
    List<BehaviourNode> indifferentNodes = [new CheckMonsterAttitude(Mob.INDIFFERENT)];
    if (passive.Count > 0)
      indifferentNodes.Add(new Selector(passive));
    if (immobile)
      indifferentNodes.Add(new PassTurn());
    else
      indifferentNodes.Add(new PickRandom([new PassTurn(), new RandomMove()]));
    plan.Add(new Sequence(indifferentNodes) { Label = "indifferent" });

    // An afraid monster tries to escape
    plan.Add(new Sequence([new IsFrightened(), new TryToEscape()]) { Label = "scared" });

    if (!mob.HasTrait<PassiveTrait>())
    {
      // Finally, try to attack the player or move toward them.
      if (!mob.HasTrait<ImmobileTrait>())
      {
        actions.Add(new KeepDistance());
        actions.Add(new ChaseTarget());
      }
      plan.Add(new Sequence([new CheckMonsterAttitude(Mob.AGGRESSIVE), new Selector(actions) { Label = "powers" }]) { Label = "aggressive" });

      plan.Add(new PassTurn() { Label = "default" });
    }
    else
    {
      plan.Add(new Sequence([new Selector(actions), new RandomMove() { Label = "default" }]));
    }

    return new Selector(plan);
  }

  static BehaviourNode CreatePrisonerPlan(Mob mob)
  {
    List<BehaviourNode> plan = [];

    // Prisoner trapped
    Sequence trapped = new([
      new CheckDialogueState(PrisonerBehaviour.DIALOGUE_CAPTIVE),
      new Selector([new RandomMove(), new PassTurn()])
    ]);
    plan.Add(trapped);

    // Prisoner trapped but hasn't yet rewarded the player
    Sequence free = new([
      new CheckDialogueState(PrisonerBehaviour.DIALOGUE_FREE),
      new RandomMove()
    ]);
    plan.Add(free);

    // Prisoner has given the player their boon
    Sequence afterBoon = new([
      new CheckDialogueState(PrisonerBehaviour.DIALOGUE_FREE_BOON),
      new FindUpStairs(),
      new PassTurn(),
      new SetDialogueState(PrisonerBehaviour.DIALOGUE_ESCAPING)
    ]);
    plan.Add(afterBoon);

    Sequence escape = new([
      new CheckDialogueState(PrisonerBehaviour.DIALOGUE_ESCAPING),
      new JumpToTavern(),
      new SetDialogueState(PrisonerBehaviour.DIALOGUE_AT_INN)
    ]);
    plan.Add(escape);

    Sequence atInn = new([
      new CheckDialogueState(PrisonerBehaviour.DIALOGUE_AT_INN),
      new WanderInTavern()
    ]);
    plan.Add(atInn);

    return new Selector(plan);
  }
  
  static BehaviourNode MoonDaughterCleric(GameState gs)
  {
    HashSet<Loc> sqs = [];
    if (gs.FactDb.FactCheck("Stone ring centre") is LocationFact ring)
    {
      for (int r = -3; r <= 3; r++)
      {
        for (int c = -3; c <= 3; c++)
        {
          Loc loc = ring.Loc with { Row = ring.Loc.Row + r, Col = ring.Loc.Col + c };
          if (gs.TileAt(loc).Passable())
            sqs.Add(loc);
        }
      }
    }

    WanderInArea wander = new(sqs);

    // If the time is between 4:00am and 8:00pm, the moon daughter
    // cleric will disappear
    Selector selector = new([
      new RepeatWhile(new TimeBetween(20, 0, 4, 0), wander),
      new GoAway()]);

    return selector;
  }

  static BehaviourNode WanderInHome(HashSet<Loc> home, GameState gs)
  {
    HashSet<Loc> sqs = [];

    foreach (Loc loc in home)
    {
      TileType tile = gs.TileAt(loc).Type;
      if (tile == TileType.WoodFloor || tile == TileType.StoneFloor)
        sqs.Add(loc);
    }

    return new WanderInArea(sqs);
  }

  static BehaviourNode BasicWander(Mob mob, GameState gs)
  {
    HashSet<Loc> locs = [];
    Map map = gs.MapForLoc(mob.Loc);
    for (int r = 1; r < map.Height - 1; r++)
    {
      for (int c = 1; c < map.Width - 1; c++)
      {
        switch (map.TileAt(r, c).Type)
        {
          case TileType.DungeonFloor:
          case TileType.IllusoryWall:
          case TileType.Upstairs:
          case TileType.Downstairs:
          case TileType.OpenDoor:
          case TileType.ClosedDoor:
          case TileType.BrokenDoor:        
            locs.Add(mob.Loc with { Row = r, Col = c });
            break;
        }
      }
    }

    return new WanderInArea(locs);
  }

  static BehaviourNode BasicVillager(Mob mob, GameState gs)
  {
    int homeId = mob.Stats[Attribute.HomeID].Curr;

    return WanderInHome(gs.Town.Homes[homeId], gs);
  }

  static BehaviourNode Pup(Mob mob,GameState gs)
  {
    HashSet<Loc> townSqs = [];

    for (int r = gs.Town.Row; r < gs.Town.Row + gs.Town.Height; r++)
    {
      for (int c = gs.Town.Col; c < gs.Town.Col + gs.Town.Width; c++)
      {
        townSqs.Add(new Loc(0, 0, r, c));
      }
    }

    RepeatWhile idleCondition = new RepeatWhile(new CheckMonsterAttitude(0), new WanderInArea(townSqs));

    Sequence pickupBone = new([
      new StandingOn("bone"),
      new PickupItem("bone")
    ]);

    FindNearbyItem boneFinder = new(mob, gs, "bone");
    BehaviourNode seekBone = new FindGoal(boneFinder);    
    Sequence fetchBone = new([new CheckMonsterAttitude(1), new Selector([seekBone, pickupBone]), new SetMonsterAttitude(2, "Arf!")]);

    string blurb = $"{mob.FullName.Capitalize()} wags its tail.";
    Sequence deliverBone = new([
      new CheckMonsterAttitude(2), 
      new Sequence([ new SeekPlayerAStar(), new DropItem("bone"), new SetMonsterAttitude(0, blurb)])
    ]);

    Selector plan = new([idleCondition, fetchBone, deliverBone]);

    return plan;
  }

  static BehaviourNode WitchPlan(GameState gs)
  {
    // If the witch is invisible, she'll just stay still. (Just to
    // make it easier for the character to talk to her)
    Sequence isInvisible = new([
      new HasTrait<InvisibleTrait>(),
      new PassTurn()
    ]);

    RepeatWhile daytime = new(new IsDaytime(), new WanderInArea(gs.Town.WitchesYard));

    HashSet<Loc> indoors = OnlyFloorsInArea(gs.Wilderness, gs.Town.WitchesCottage);
    Sequence evening = new([new FindWayToArea(indoors), new WanderInArea(indoors)]);

    return new Selector([isInvisible, daytime, evening]);
  }

  static BehaviourNode AlchemistPlan(GameState gs)
  {
    Sequence gardening = new([
      new TimeBetween(8, 0, 12, 59),
      new FindWayToArea(gs.Town.WitchesGarden),
      new PickRandom([new PassTurn(), new PassTurn(), new WanderInArea(gs.Town.WitchesGarden)])
    ]);

    RepeatWhile daytime = new(new IsDaytime(), new WanderInArea(gs.Town.WitchesYard));

    HashSet<Loc> indoors = OnlyFloorsInArea(gs.Wilderness, gs.Town.WitchesCottage);
    Sequence evening = new([new FindWayToArea(indoors), new WanderInArea(indoors)]);

    return new Selector([gardening, daytime, evening]);
  }

  // Maybe the Actor/Mob class returns its own plan, obviating the need for 
  // this function?
  public static BehaviourNode GetPlan(string plan, Mob mob, GameState gs) => plan switch
  {
    "MayorPlan" => CreateMayorPlan(mob, gs),
    "SmithPlan" => CreateSmithPlan(mob, gs),
    "MonsterPlan" => CreateMonsterPlan(mob),
    "MimicPlan" => CreateMimicPlan(mob),
    "Rando" => CreateRandoMonsterPlan(mob),
    "PrisonerPlan" => CreatePrisonerPlan(mob),
    "PriestPlan" => WanderInHome(gs.Town.Shrine, gs),
    "GrocerPlan" or "TailorPlan" => WanderInHome(gs.Town.Market, gs),
    "BasicVillagerPlan" => BasicVillager(mob, gs),
    "WitchPlan" => WitchPlan(gs),
    "AlchemistPlan" => AlchemistPlan(gs),
    "BarHoundPlan" => WanderInHome(gs.Town.Tavern, gs),
    "PupPlan" => Pup(mob, gs),
    "SimpleRandomPlan" => new Selector([new RandomMove(), new PassTurn()]),
    "MoonClericPlan" => MoonDaughterCleric(gs),
    "BasicIllusionPlan" => new Selector([new ChaseTarget(), new RandomMove()]),
    "Greedy" => CreateGreedyMonster(mob, gs),    
    "BasicWander" => BasicWander(mob, gs),
    "Decoy" => new Selector([new Sequence([new WithinRange(5), new PickRandom([new RandomMove(), new PassTurn(), new PassTurn()])]), new ChaseTarget() ]),
    _ => throw new Exception($"Unknown Behaviour Tree plan: {plan}")
  };
}
