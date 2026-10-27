using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Joy.Core.GridPrototype;
using GridField = Joy.Core.GridPrototype.FieldState;

namespace Joy.Tests;

/// <summary>Player-facing contracts for the grid/card prototype. Constructed boundary states are
/// separate from the public-API-only campaign witness in GridWitness.</summary>
public static class GridPrototypeTests
{
    public static void Run(Action<bool, string> check)
    {
        foreach (var suite in new (string, Action<Action<bool, string>>)[]
        {
            ("planning", Planning), ("cards", Cards), ("capture", Capture), ("enemies and assignment", EnemiesAndAssignment),
            ("persistence", Persistence), ("campaign", GridWitness.Run)
        })
        {
            try { suite.Item2(check); }
            catch (Exception e) { check(false, $"Grid {suite.Item1}: {e}"); }
        }
    }

    private static void Planning(Action<bool, string> check)
    {
        var g = GridEngine.NewGame(); var e = new GridEngine(g);
        check(g.City.Count == 35 && g.City.Select(t => t.Cell).Distinct().Count() == 35,
            "Grid: city has 35 unique spatial cells");
        check(g.Heroes.Count == 3 && g.Heroes.Select(h => string.Join(",", h.Deck)).Distinct().Count() == 3,
            "Grid: three heroes start with genuinely different decks");
        check(g.Heroes.All(h => h.Deck.Count >= 6 && h.Deck.All(c => Catalog.Card(c).Owner == h.Id)),
            "Grid: each initial deck consists of its owner's role cards");
        check(Catalog.BasicIds.All(id => Catalog.Card(id).Owner == "basic") && Catalog.BasicIds.Contains("move") && Catalog.BasicIds.Contains("retreat"),
            "Grid: movement and extraction are always available card actions");
        check(e.RouteTo("hotel").Count > 0, "Grid: initial hotel has an actual connected city route");
        string before = Json(g);
        check(!e.StartMission("source").Success && Json(g) == before,
            "Grid: source prerequisites reject the mission before payment");
        check(!e.BuildSite("hotel").Success && Json(g) == before,
            "Grid: cannot build an uncleared site or lose money on rejection");
        check(!e.SelectAsset("missing-asset").Success && Json(g) == before,
            "Grid: unknown or unowned assets cannot be assigned");
        check(!e.ToggleHero("missing-hero").Success && Json(g) == before,
            "Grid: invalid hero does not alter dispatch");
        Need(e.LearnSkill("mapper", "a"));
        check(g.Hero("mapper").SkillPoints == 1 && g.Hero("mapper").LearnedTalents.Contains("mapper-a1"),
            "Grid: a talent spends one of two starting points and records its node");
        Need(e.LearnSkill("mapper", "b"));
        check(g.Hero("mapper").SkillPoints == 0 && g.Hero("mapper").LearnedTalents.Contains("mapper-b1"),
            "Grid: separate tree branches can receive points in the same build");
        before = Json(g);
        check(!e.LearnSkill("mapper", "b").Success && Json(g) == before,
            "Grid: exhausted talent points cannot silently grant the next node");
        string removed = g.Hero("mapper").Deck.First();
        check(!e.ChangeDeck("mapper", removed, "bind").Success && Json(g) == before,
            "Grid: deck editing cannot insert another character's ability");
        string added = Catalog.DeckOptions("mapper").First(c => g.Hero("mapper").Deck.Count(id => id == c.Id) < 2).Id;
        int count = g.Hero("mapper").Deck.Count;
        Need(e.ChangeDeck("mapper", removed, added));
        check(g.Hero("mapper").Deck.Count == count && g.Hero("mapper").Deck.Contains(added),
            "Grid: actual deck replacement preserves deck size");

        // An explicit planning fixture isolates connectivity from economic constraints.
        g = GridEngine.NewGame(); e = new GridEngine(g);
        var route = e.RouteTo("hotel");
        var roads = g.City.Where(t => t.Terrain == "road").ToArray();
        foreach (var road in roads) road.Blocked = true;
        g.Money = 100;
        before = Json(g);
        check(e.RouteTo("hotel").Count == 0 && !e.StartMission("hotel").Success && Json(g) == before,
            "Grid: blocked street connectivity prevents dispatch without charging");
        foreach (var cell in route)
        {
            var road = g.City.Single(t => t.Cell == cell);
            if (road.Terrain == "road" && road.Blocked) Need(e.RepairRoad(cell));
        }
        check(e.RouteTo("hotel").Count > 0 && g.Money < 100,
            "Grid: repairing the physical route restores access and costs money");
    }

    private static void Cards(Action<bool, string> check)
    {
        var (g, e, f) = Arena();
        var a = f.Actor("mapper"); var b = f.Actor("warden");
        string before = Json(g);
        check(e.Preview(a.HeroId, "move", new Cell(2, 3)).Success && Json(g) == before,
            "Grid: valid movement preview leaves all state and RNG untouched");
        check(!e.Play(a.HeroId, "move", b.Cell).Success && Json(g) == before,
            "Grid: movement cannot overlap another actor and costs nothing on failure");
        check(!e.Play(a.HeroId, "move", new Cell(5, 3)).Success && Json(g) == before,
            "Grid: basic movement respects its range");
        f.Tile(new Cell(2, 3))!.Kind = "wall";
        before = Json(g);
        check(!e.Play(a.HeroId, "move", new Cell(2, 3)).Success && Json(g) == before,
            "Grid: walls are actual movement obstacles");
        f.Tile(new Cell(2, 3))!.Kind = "floor";
        a.Hand.Clear();
        Need(e.Play(a.HeroId, "move", new Cell(2, 3)));
        check(a.Cell == new Cell(2, 3) && a.Actions == 1 && a.Hand.Count == 0,
            "Grid: movement is a paid card action available even with an empty random hand");
        before = Json(g);
        check(!e.Play(a.HeroId, "shot", new Cell(4, 3)).Success && Json(g) == before,
            "Grid: a role card not drawn cannot be played");
        check(!e.Play(a.HeroId, "does-not-exist", a.Cell).Success && Json(g) == before,
            "Grid: unknown cards are rejected safely");
        a.Hand.Add("bind");
        f.Enemies.Add(Enemy("dummy", new Cell(3, 3)));
        before = Json(g);
        check(!e.Play(a.HeroId, "bind", new Cell(3, 3)).Success && Json(g) == before,
            "Grid: even an invalid cross-role hand cannot bypass card ownership");
        a.Hand.Clear(); a.Hand.Add("mark");
        int enemyHp = f.Enemies[0].Health;
        Need(e.Play(a.HeroId, "mark", new Cell(3, 3)));
        check(a.Actions == 0 && !a.Hand.Contains("mark") && a.Discard.Contains("mark") && f.Enemies[0].MarkedUntil >= f.Round,
            "Grid: mark consumes the actual card and action while applying a combat condition");
        before = Json(g);
        check(!e.Play(a.HeroId, "attack", new Cell(3, 3)).Success && Json(g) == before,
            "Grid: exhausted actor cannot spend the partner's actions");
        check(b.Actions == 2 && f.Enemies[0].Health == enemyHp,
            "Grid: targeting and marking do not charge or attack with the other character");

        (g, e, f) = Arena(); a = f.Actor("mapper");
        before = Json(g);
        check(!e.Preview(a.HeroId, "move", new Cell(3, 3)).Success && Json(g) == before,
            "Grid: untrained actor cannot move two cells with basic move");
        g.Hero("mapper").LearnedTalents.Add("mapper-a1");
        Need(e.Play(a.HeroId, "move", new Cell(3, 3)));
        check(a.Cell == new Cell(3, 3) && a.Actions == 1,
            "Grid: mapper travel specialization actually changes movement reach");
    }

    private static void Capture(Action<bool, string> check)
    {
        var (g, e, f) = Arena();
        var warden = f.Actor("warden");
        var target = Enemy("anomaly", new Cell(2, 4)); target.CapturableAsset = "bell"; target.IsObjective = true; target.Health = 2;
        f.Enemies.Add(target);
        string before = Json(g);
        check(!e.Play("warden", "capture", target.Cell).Success && Json(g) == before,
            "Grid: low health alone does not allow capture without evidence");
        f.EvidenceKnown = true;
        before = Json(g);
        check(!e.Play("warden", "capture", target.Cell).Success && Json(g) == before,
            "Grid: known evidence still requires actual restraint, mark or light");
        target.MarkedUntil = f.Round + 1;
        int charges = g.SealCharges;
        Need(e.Play("warden", "capture", target.Cell));
        check(!target.Alive && f.Cargo.Contains("bell") && g.Assets.All(a => a.Id != "bell") && g.SealCharges == charges - 1,
            "Grid: capture creates real cargo and consumes one tool, with no premature company asset");
        before = Json(g);
        check(!e.Play("warden", "capture", target.Cell).Success && Json(g) == before,
            "Grid: a captured target cannot be claimed twice");
        f.Actor("mapper").X = 0; f.Actor("mapper").Y = 3;
        warden.X = 0; warden.Y = 4;
        Need(e.Play("mapper", "retreat", new Cell(0, 3)));
        check(g.Phase == "city" && g.Assets.Count(a => a.Id == "bell") == 1 && g.Site("hotel").Cleared,
            "Grid: physical extraction delivers the single captured asset and clears the contract");
        check(g.Day == 2, "Grid: extraction advances one company day");

        (g, e, f) = Arena();
        target = Enemy("anomaly", new Cell(2, 3)); target.CapturableAsset = "bell"; target.IsObjective = true; target.Health = 2;
        f.Enemies.Add(target);
        Need(e.Play("mapper", "attack", target.Cell));
        check(!target.Alive && !f.Cargo.Contains("bell") && g.Assets.All(a => a.Id != "bell"),
            "Grid: defeating an anomaly cannot also award its captured ability");
        check(f.ObjectiveDone && f.Tiles.Any(t => t.ItemId == "contract") && !f.Cargo.Contains("contract"),
            "Grid: defeated objective leaves a physical contract token that still requires pickup");
        Need(e.Play("mapper", "pickup", target.Cell));
        f.Actor("mapper").X = 0; f.Actor("mapper").Y = 3;
        f.Actor("warden").X = 0; f.Actor("warden").Y = 4;
        Need(e.Play("mapper", "retreat", f.Anchor));
        check(g.Site("hotel").Cleared && g.Assets.Count == 0,
            "Grid: kill-and-recover completes the contract without duplicating a capture reward");
        before = Json(g);
        check(!e.StartMission("hotel").Success && Json(g) == before,
            "Grid: completed content cannot refresh its unique enemies and rewards");

        (g, e, f) = Arena();
        target = Enemy("anomaly", new Cell(2, 4)); target.CapturableAsset = "bell"; target.IsObjective = true; target.Health = 2; target.MarkedUntil = f.Round + 1;
        f.Enemies.Add(target); f.EvidenceKnown = true; charges = g.SealCharges;
        Need(e.Play("warden", "capture", target.Cell));
        f.Round = f.RoundLimit;
        check(e.Preview("mapper", "end", f.Actor("mapper").Cell).Message.Contains("封锁"),
            "Grid: final-turn end card warns that the field will close");
        Need(e.Play("mapper", "end", f.Actor("mapper").Cell));
        check(g.Phase == "city" && g.Field is null && !g.Site("hotel").Cleared && g.Assets.Count == 0 && g.SealCharges == charges - 1,
            "Grid: timeout loses undelivered cargo but does not refund spent capture tools");
        check(g.Heroes.All(h => h.Health >= 3) && e.StartMission("hotel").Success,
            "Grid: an early mission failure leaves a legitimate retry route");
    }

    private static void EnemiesAndAssignment(Action<bool, string> check)
    {
        var (g, e, f) = Arena();
        var listener = Enemy("listener", new Cell(4, 3)); listener.Kind = "listener";
        var watcher = Enemy("watcher", new Cell(5, 4)); watcher.Kind = "watcher";
        var hunger = Enemy("hunger", new Cell(6, 4)); hunger.Kind = "hunger";
        f.Enemies.AddRange(new[] { listener, watcher, hunger });
        f.Tile(new Cell(2, 4))!.Kind = "bell";
        int beforeDistance = listener.Cell.Distance(new Cell(2, 4));
        Need(e.Play("warden", "ring", new Cell(2, 4)));
        check(listener.Cell.Distance(new Cell(2, 4)) < beforeDistance && listener.Lure == new Cell(2, 4) && f.EvidenceKnown,
            "Grid: ringing a physical bell changes a hearing enemy's position and supplies a real observation");
        check(watcher.Cell == new Cell(5, 4) && hunger.Cell == new Cell(6, 4),
            "Grid: non-hearing enemy types do not share the same lure response");

        (g, e, f) = Arena();
        watcher = Enemy("watcher", new Cell(3, 3)); watcher.Kind = "watcher"; watcher.Alert = true;
        f.Enemies.Add(watcher); int hp = g.Hero("mapper").Health;
        check(e.EnemyIntent(watcher).Contains("攻击") && e.EnemyIntent(watcher).Contains("2"),
            "Grid: a visible ranged attacker announces its damage before turn end");
        Need(e.Play("mapper", "guard", f.Actor("mapper").Cell));
        Need(e.Play("mapper", "end", f.Actor("mapper").Cell));
        check(g.Hero("mapper").Health == hp && watcher.Cell == new Cell(3, 3) && f.Actor("mapper").Block == 0,
            "Grid: watcher attacks at range, guard absorbs the announced damage and expires");
        Need(e.Play("mapper", "end", f.Actor("mapper").Cell));
        check(g.Hero("mapper").Health == hp - 2, "Grid: an unprotected second enemy attack has the announced cost");

        (g, e, f) = Arena();
        hunger = Enemy("hunger", new Cell(2, 3)); hunger.Kind = "hunger"; hunger.Alert = true; f.Enemies.Add(hunger);
        hp = g.Hero("mapper").Health;
        Need(e.Play("mapper", "end", f.Actor("mapper").Cell));
        check(g.Hero("mapper").Health == hp - 3, "Grid: hunger enemy has a distinct heavy attack rather than a name-only variant");

        (g, e, f) = Arena();
        watcher = Enemy("watcher", new Cell(3, 3)); watcher.Kind = "watcher"; watcher.Alert = true; f.Enemies.Add(watcher);
        var engineer = f.Actor("mapper"); engineer.HeroId = "engineer"; engineer.Hand = new() { "light" };
        hp = g.Hero("engineer").Health;
        Need(e.Play("engineer", "light", watcher.Cell));
        check(e.EnemyIntent(watcher).Contains("停顿"), "Grid: light changes a photophobic enemy's announced intent");
        Need(e.Play("engineer", "end", engineer.Cell));
        check(g.Hero("engineer").Health == hp, "Grid: actual watcher turn respects its illumination rule");

        // Same initial company, same empty sortie; only the anomaly assignment changes.
        GridGame Assignment(bool carry)
        {
            var state = GridEngine.NewGame(); state.Assets.Add(new AssetState { Id = "bell" });
            var engine = new GridEngine(state);
            Need(engine.SelectAsset(carry ? "bell" : "")); Need(engine.StartMission("hotel"));
            if (carry)
            {
                string before = Json(state);
                check(!engine.SelectAsset("").Success && Json(state) == before,
                    "Grid: anomaly assignment cannot change after departure");
                Need(engine.Play(state.Field!.Actors[0].HeroId, "asset-bell", state.Field.Actors[0].Cell));
                check(state.Assets.Single().Charges == 1, "Grid: field anomaly card spends its one real shared charge");
            }
            Need(engine.Play(state.Field!.Actors[0].HeroId, "retreat", state.Field.Anchor));
            return state;
        }
        var kept = Assignment(false); var carried = Assignment(true);
        check(kept.Money == carried.Money + 2,
            "Grid: a carried bell cannot also earn its headquarters income in the same sortie");
        check(kept.Assets.Single().Charges == 2 && carried.Assets.Single().Charges == 1,
            "Grid: extraction never refreshes consumed anomaly charges");

        g = GridEngine.NewGame(); e = new GridEngine(g);
        g.Assets.Add(new AssetState { Id = "wardrobe" });
        Need(e.StartMission("hotel"));
        check(g.Field!.Capacity == 4, "Grid: wardrobe left in company adds real cargo capacity");
        Need(e.Play(g.Field.Actors[0].HeroId, "retreat", g.Field.Anchor));
        Need(e.SelectAsset("wardrobe")); Need(e.StartMission("hotel"));
        check(g.Field!.Capacity == 3, "Grid: carrying the wardrobe gives up its storage bonus");

        g = GridEngine.NewGame(); e = new GridEngine(g);
        Need(e.ToggleHero("warden")); Need(e.ToggleHero("engineer"));
        g.Hero("mapper").Health = 5; g.SealCharges = 1;
        Need(e.StartMission("hotel")); Need(e.Play(g.Field!.Actors[0].HeroId, "retreat", g.Field.Anchor));
        check(g.Hero("mapper").Health == 9 && g.SealCharges == 2,
            "Grid: warden staying at headquarters heals and replenishes actual resources");

        (g, e, f) = Arena();
        var first = Enemy("first", new Cell(3, 3)); first.Alert = true;
        var second = Enemy("second", new Cell(2, 2)); second.Alert = true;
        f.Enemies.AddRange(new[] { first, second });
        var intents = f.Enemies.ToDictionary(x => x.Id, e.EnemyIntent);
        Need(e.Play("mapper", "end", f.Actor("mapper").Cell));
        check(f.Enemies.Where(x => x.Alive).Select(x => x.Cell).Distinct().Count() == 2 && !f.Enemies.Any(x => f.Actors.Any(a => a.Cell == x.Cell)),
            "Grid: simultaneous enemy movement never overlaps actors or other enemies");
        check(intents[first.Id].Contains(first.Cell.ToString()) && intents[second.Id].Contains(second.Cell.ToString()),
            "Grid: contested movement resolves to the cells announced in the intent preview");

        (g, e, f) = Arena();
        listener = Enemy("listener", new Cell(2, 3)); listener.Kind = "listener"; listener.Alert = true; listener.Lure = f.Actor("mapper").Cell;
        f.Enemies.Add(listener); hp = g.Hero("mapper").Health;
        check(e.EnemyIntent(listener).Contains("攻击"), "Grid: a listener reaching the hero at its sound source announces an attack");
        Need(e.Play("mapper", "end", f.Actor("mapper").Cell));
        check(g.Hero("mapper").Health == hp - 2 && listener.Lure.X < 0,
            "Grid: occupied sound source cannot immobilize a listener forever without danger");

        (g, e, f) = Arena();
        listener = Enemy("listener", new Cell(4, 3)); listener.Kind = "listener"; listener.Alert = true; listener.Lure = new Cell(6, 3);
        f.Enemies.Add(listener);
        foreach (var cell in new[] { new Cell(5, 3), new Cell(6, 2), new Cell(7, 3), new Cell(6, 4) }) f.Tile(cell)!.Kind = "wall";
        Need(e.Play("mapper", "end", f.Actor("mapper").Cell));
        check(listener.Lure.X < 0, "Grid: unreachable sound source is abandoned after the declared blocked attempt");
    }

    private static void Persistence(Action<bool, string> check)
    {
        string directory = Path.Combine(Path.GetTempPath(), "Joy-GridTests-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "save.json");
        try
        {
            var g = GridEngine.NewGame(); var e = new GridEngine(g);
            Need(e.LearnSkill("mapper", "a")); Need(e.StartMission("hotel"));
            string initial = Json(g);
            Need(GridSave.Save(path, g));
            var restored = GridSave.Load(path, out var message);
            check(restored is not null && Json(restored) == initial,
                "Grid: save restores field, hand, random state, personal deck and learned specialization exactly");
            Need(e.Play(g.Field!.Actors[0].HeroId, "end", g.Field.Actors[0].Cell));
            var resumed = new GridEngine(restored!);
            Need(resumed.Play(restored!.Field!.Actors[0].HeroId, "end", restored.Field.Actors[0].Cell));
            check(Json(g) == Json(restored), "Grid: continued saved game draws the same cards and resolves the same enemy turn");
            Need(GridSave.Save(path, g));
            File.WriteAllText(path, "{ damaged");
            var backup = GridSave.Load(path, out message);
            check(backup is not null && Json(backup) == initial && message.Contains("备份"),
                "Grid: corrupted primary restores the prior complete checkpoint with an explicit message");
            string future = "{\"Format\":999,\"Payload\":\"\",\"Hash\":\"\"}";
            File.WriteAllText(path, future);
            check(GridSave.Load(path, out message) is null && File.ReadAllText(path) == future,
                "Grid: future format is refused without silently loading an older backup or modifying the file");
            Need(GridSave.Save(path, g)); string validFile = File.ReadAllText(path);
            g.Field!.Actors[0].X = -1;
            check(!GridSave.Save(path, g).Success && File.ReadAllText(path) == validFile,
                "Grid: invalid actor coordinates cannot overwrite the previous valid checkpoint");
            g.Field.Actors[0].X = 0;
            g.Heroes[0].Deck.Add("missing-card");
            check(!GridSave.Save(path, g).Success && File.ReadAllText(path) == validFile,
                "Grid: unknown card identifiers are rejected before saving");
        }
        finally
        {
            string resolved = Path.GetFullPath(directory);
            string temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (resolved.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(resolved).StartsWith("Joy-GridTests-", StringComparison.Ordinal) && Directory.Exists(resolved))
                Directory.Delete(resolved, true);
        }
    }

    internal static (GridGame G, GridEngine E, GridField F) Arena()
    {
        var g = GridEngine.NewGame(); var e = new GridEngine(g); Need(e.StartMission("hotel"));
        var f = g.Field!;
        foreach (var tile in f.Tiles) { tile.Kind = "floor"; tile.Used = false; }
        f.Tile(new Cell(0, 3))!.Kind = "exit";
        f.Enemies.Clear();
        f.Actor("mapper").X = 1; f.Actor("mapper").Y = 3;
        f.Actor("warden").X = 1; f.Actor("warden").Y = 4;
        return (g, e, f);
    }
    private static EnemyState Enemy(string id, Cell cell) => new() { Id = id, Name = "测试敌影", Kind = "shadow", X = cell.X, Y = cell.Y, Health = 5, MaxHealth = 5 };
    internal static string Json(GridGame game) => JsonSerializer.Serialize(game);
    internal static void Need(Result result)
    {
        if (!result.Success) throw new InvalidOperationException(result.Message);
    }
}
