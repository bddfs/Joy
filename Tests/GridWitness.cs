using System;
using System.Collections.Generic;
using System.Linq;
using Joy.Core.GridPrototype;

namespace Joy.Tests;

/// <summary>Public-API-only campaign witness. No state injection is permitted here.</summary>
public static class GridWitness
{
    public static void Run(Action<bool, string> check)
    {
        var g = GridEngine.NewGame(413); var e = new GridEngine(g);
        var played = new HashSet<string>(); var turns = new List<string>();
        GridPrototypeTests.Need(e.LearnSkill("mapper", "a"));
        GridPrototypeTests.Need(e.LearnSkill("warden", "a"));
        GridPrototypeTests.Need(e.LearnSkill("engineer", "a"));
        Rebuild(e, "mapper", new[] { "sprint", "sprint", "shot", "shot", "mark", "mark", "survey", "lure" });
        Rebuild(e, "warden", new[] { "soothe", "soothe", "quick-capture", "quick-capture", "escort", "escort", "bind", "heal" });
        Rebuild(e, "engineer", new[] { "light", "light", "flash", "arc", "arc", "dash", "dash", "repair" });
        Mission(e, "hotel", played, turns);
        check(g.Site("hotel").Cleared && g.Assets.Any(a => a.Id == "bell"), "Grid witness: hotel anomaly captured and physically delivered through cards");
        GridPrototypeTests.Need(e.BuildSite("hotel"));
        GridPrototypeTests.Need(e.LearnSkill("warden", "a"));
        GridPrototypeTests.Need(e.ToggleHero("mapper")); GridPrototypeTests.Need(e.ToggleHero("engineer"));
        GridPrototypeTests.Need(e.SelectAsset("bell"));
        if (g.Heroes.Any(h => h.Health <= 7)) GridPrototypeTests.Need(e.Resupply());
        Mission(e, "hospital", played, turns);
        check(g.Site("hospital").Cleared && g.Assets.Any(a => a.Id == "wardrobe"), "Grid witness: a different field team captures the hospital anomaly");
        GridPrototypeTests.Need(e.BuildSite("hospital"));
        GridPrototypeTests.Need(e.ToggleHero("engineer")); GridPrototypeTests.Need(e.ToggleHero("mapper"));
        GridPrototypeTests.Need(e.LearnSkill("mapper", "a"));
        GridPrototypeTests.Need(e.SelectAsset("wardrobe"));
        if (g.Heroes.Any(h => h.Health <= 7)) GridPrototypeTests.Need(e.Resupply());
        Mission(e, "source", played, turns);
        check(g.Phase == "victory" && g.Site("source").Cleared && g.City.Count(t => t.Operating) == 2,
            "Grid witness: public API-only three-site campaign reaches victory with two operating sites");
        check(g.Assets.Select(a => a.Id).Distinct().Count() == 3 && g.Assets.Count == 3,
            "Grid witness: all three anomalies become unique company assets");
        check(played.Any(c => Catalog.Card(c).Owner == "mapper") && played.Any(c => Catalog.Card(c).Owner == "warden") && played.Any(c => Catalog.Card(c).Owner == "engineer") && played.Contains("asset-wardrobe"),
            "Grid witness: route actually uses all three role decks and a recovered anomaly ability");
        Console.WriteLine($"GRID_CAMPAIGN | day {g.Day} | money {g.Money} | assets {string.Join(',', g.Assets.Select(a => a.Id))} | {string.Join(" / ", turns)} | cards {string.Join(',', played.OrderBy(x => x))}");
    }

    private static void Rebuild(GridEngine engine, string heroId, string[] desired)
    {
        var deck = engine.Game.Hero(heroId).Deck;
        foreach (var id in desired.Distinct())
        {
            while (deck.Count(c => c == id) < desired.Count(c => c == id))
            {
                string remove = deck.First(c => deck.Count(x => x == c) > desired.Count(x => x == c));
                GridPrototypeTests.Need(engine.ChangeDeck(heroId, remove, id));
                deck = engine.Game.Hero(heroId).Deck;
            }
        }
    }

    private static void Mission(GridEngine e, string site, HashSet<string> played, List<string> turns)
    {
        GridPrototypeTests.Need(e.StartMission(site));
        var g = e.Game; int lastRound = 0; int guard = 0;
        while (g.Phase == "field" && guard++ < 100)
        {
            var f = g.Field!; lastRound = f.Round;
            if (Try(e, f.Actors[0], "retreat", f.Anchor, played, requireObjective: true)) break;
            bool acted = false;
            foreach (var actor in f.Actors.ToArray())
            {
                if (g.Phase != "field") break;
                if (Act(e, actor, played)) acted = true;
            }
            if (!acted && g.Phase == "field")
                GridPrototypeTests.Need(e.Play(f.Actors[0].HeroId, "end", f.Actors[0].Cell));
        }
        if (g.Phase == "field" || !g.Site(site).Cleared)
            throw new InvalidOperationException($"Campaign failed at {site}, round {lastRound}: {g.LastReport}\n{string.Join('\n', g.Field?.Log ?? new List<string>())}");
        turns.Add($"{site}:{lastRound} rounds");
    }

    private static bool Act(GridEngine e, ActorState a, HashSet<string> played)
    {
        var g = e.Game; var f = g.Field!;
        bool carrying = f.Cargo.Any(c => c is "bell" or "wardrobe" or "pain" or "contract");
        if (carrying)
        {
            if (a.Cell.Distance(f.Anchor) > 1 && Try(e, a, "asset-wardrobe", a.Cell, played)) return true;
            if (a.Actions <= 0) return false;
            if (a.Cell.Distance(f.Anchor) <= 1) return false;
            return MoveToward(e, a, a.HeroId == f.Actors[0].HeroId ? f.Anchor : new Cell(0, 4), played);
        }
        if (a.Actions <= 0) return false;
        foreach (var friend in f.Actors.OrderBy(x => g.Hero(x.HeroId).Health))
            if (g.Hero(friend.HeroId).Health <= g.Hero(friend.HeroId).MaxHealth - 4)
                foreach (var healing in new[] { "heal", "repair" }) if (Try(e, a, healing, friend.Cell, played)) return true;

        foreach (var enemy in f.Enemies.Where(x => x.Alive && x.IsObjective))
            foreach (var capture in new[] { "quick-capture", "capture" })
                if (Try(e, a, capture, enemy.Cell, played)) return true;

        if (!f.EvidenceKnown)
        {
            var clue = f.Tiles.First(t => t.Kind == "clue");
            foreach (var inspect in new[] { "probe", "survey", "search" }) if (Try(e, a, inspect, clue.Cell, played)) return true;
            return MoveToward(e, a, clue.Cell, played, stopAdjacent: true);
        }

        foreach (var enemy in f.Enemies.Where(x => x.Alive).OrderBy(x => x.Cell.Distance(a.Cell)))
        {
            bool controlled = enemy.MarkedUntil >= f.Round || enemy.StunnedUntil >= f.Round || (f.LightUntil >= f.Round && enemy.Cell.Distance(f.LightCell) <= 1);
            if (enemy.IsObjective && enemy.Health <= 4)
            {
                if (!controlled)
                    foreach (var control in new[] { "bind", "mark", "light" }) if (Try(e, a, control, enemy.Cell, played)) return true;
                // A restrained capture target is deliberately kept alive.
                continue;
            }
            foreach (var attack in new[] { "soothe", "arc", "shot", "attack", "flash" })
                if (Try(e, a, attack, enemy.Cell, played)) return true;
            if (!controlled && enemy.IsObjective)
                foreach (var control in new[] { "mark", "light", "bind" }) if (Try(e, a, control, enemy.Cell, played)) return true;
        }

        var main = f.Enemies.First(x => x.IsObjective);
        if (!main.Alive)
        {
            var token = f.Tiles.FirstOrDefault(t => t.ItemId == "contract");
            if (token is not null)
            {
                if (Try(e, a, "pickup", token.Cell, played)) return true;
                return MoveToward(e, a, token.Cell, played, true);
            }
        }
        if (main.Alive && MoveToward(e, a, main.Cell, played, true)) return true;
        if (main.Alive && !main.Alert && f.AssetId == "bell" && Try(e, a, "asset-bell", a.Cell, played)) return true;
        return false;
    }

    private static bool MoveToward(GridEngine e, ActorState a, Cell goal, HashSet<string> played, bool stopAdjacent = false)
    {
        var f = e.Game.Field!;
        // Route planning uses public map geometry; closed doors are potential interaction steps.
        int Distance(Cell from) => PathDistance(f, from, goal, a.HeroId, stopAdjacent);
        int current = Distance(a.Cell);
        foreach (var door in f.Tiles.Where(t => t.Kind == "door").OrderBy(t => t.Cell.Distance(goal)))
            if (a.Cell.Distance(door.Cell) <= 1 && Try(e, a, "search", door.Cell, played)) return true;
        foreach (var card in new[] { "sprint", "escort", "dash", "move" })
        {
            var candidates = f.Tiles.Where(t => t.Kind is not ("wall" or "door") && t.Cell != a.Cell)
                .Select(t => (t.Cell, Distance: Distance(t.Cell)))
                .Where(t => t.Distance < current)
                .OrderBy(t => t.Distance).ThenBy(t => t.Cell.Distance(a.Cell)).ThenBy(t => t.Cell.Y).ThenBy(t => t.Cell.X);
            foreach (var candidate in candidates)
                if (Try(e, a, card, candidate.Cell, played)) return true;
        }
        return false;
    }

    private static int PathDistance(Joy.Core.GridPrototype.FieldState f, Cell from, Cell goal, string heroId, bool adjacent)
    {
        var queue = new Queue<(Cell Cell, int Steps)>(); var seen = new HashSet<Cell> { from };
        queue.Enqueue((from, 0));
        while (queue.Count > 0)
        {
            var v = queue.Dequeue();
            if (v.Cell.Distance(goal) <= (adjacent ? 1 : 0)) return v.Steps;
            foreach (var next in new[] { new Cell(v.Cell.X + 1, v.Cell.Y), new Cell(v.Cell.X - 1, v.Cell.Y), new Cell(v.Cell.X, v.Cell.Y + 1), new Cell(v.Cell.X, v.Cell.Y - 1) })
            {
                if (seen.Contains(next) || f.Tile(next) is not { Kind: not "wall" }) continue;
                if (f.Actors.Any(x => x.HeroId != heroId && x.Cell == next) || f.Enemies.Any(x => x.Alive && x.Cell == next)) continue;
                seen.Add(next); queue.Enqueue((next, v.Steps + 1));
            }
        }
        return 1000;
    }

    private static bool Try(GridEngine e, ActorState actor, string card, Cell target, HashSet<string> played, bool requireObjective = false)
    {
        var f = e.Game.Field!;
        if (requireObjective && !f.Cargo.Any(c => c is "bell" or "wardrobe" or "pain" or "contract")) return false;
        if (!e.Preview(actor.HeroId, card, target).Success) return false;
        var result = e.Play(actor.HeroId, card, target);
        if (!result.Success) throw new InvalidOperationException("Preview/execute disagreement: " + result.Message);
        played.Add(card);
        if (Environment.GetCommandLineArgs().Contains("--grid-trace"))
            Console.WriteLine($"GRID_STEP | {f.SiteId} r{f.Round} | {actor.HeroId} {card} {target} | {result.Message}");
        return true;
    }
}
