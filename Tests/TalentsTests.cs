using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Joy.Core.GridPrototype;

namespace Joy.Tests;

/// <summary>Talent behaviour checks use explicit controlled fixtures; the campaign witness separately uses only public game commands.</summary>
public static class TalentsTests
{
    public static void Run(Action<bool, string> check)
    {
        foreach (var suite in new (string Name, Action<Action<bool, string>> Run)[]
        {
            ("tree and purchase", TreeAndPurchase), ("unlock and deck", UnlockAndDeck),
            ("effective cards", EffectiveCards), ("signature actions", SignatureActions), ("persistence", Persistence),
            ("earned signature witness", GridWitness.RunTalentUnlock)
        })
        {
            try { suite.Run(check); }
            catch (Exception e) { check(false, $"Talents {suite.Name}: {e}"); }
        }
    }

    private static void TreeAndPurchase(Action<bool, string> check)
    {
        var g = GridEngine.NewGame(); var e = new GridEngine(g);
        check(g.Version == 2 && g.Heroes.All(h => h.SkillPoints == 2 && h.LearnedTalents.Count == 0),
            "Talents: new campaign has two unspent choices per hero and versioned empty trees");
        check(TalentCatalog.Nodes.Count == 27 && TalentCatalog.Nodes.Select(n => n.Id).Distinct().Count() == 27,
            "Talents: three distinct trees contain 27 uniquely identified purchase nodes");
        foreach (var h in g.Heroes)
        {
            var nodes = TalentCatalog.ForHero(h.Id);
            check(nodes.Count == 9 && nodes.Select(n => n.Column).Distinct().Count() == 3 && nodes.All(n => n.Cost > 0),
                $"Talents: {h.Id} has three progression branches with real costs");
            check(nodes.All(n => n.Prerequisites.All(p => nodes.Any(parent => parent.Id == p && parent.Row < n.Row))),
                $"Talents: {h.Id} prerequisite connections stay inside its own acyclic tree");
            check(new[] { "stat", "mechanic", "card" }.All(kind => nodes.Any(n => n.Kind == kind)),
                $"Talents: {h.Id} has number, mechanism and new-card node effects");
        }
        string before = Json(g);
        check(!e.LearnTalent("mapper", "mapper-a3").Success && Json(g) == before,
            "Talents: a deep node cannot skip prerequisites or consume points on rejection");
        check(!e.LearnTalent("mapper", "engineer-a1").Success && Json(g) == before,
            "Talents: a hero cannot purchase another hero's tree node");
        check(!e.LearnTalent("mapper", "missing").Success && Json(g) == before,
            "Talents: an unknown node leaves the campaign unchanged");
        check(e.PreviewTalent("mapper", "mapper-a1").Success && Json(g) == before,
            "Talents: available-node preview is entirely read only");
        Need(e.LearnTalent("mapper", "mapper-a1"));
        check(g.Hero("mapper").SkillPoints == 1 && g.Hero("mapper").LearnedTalents.SequenceEqual(new[] { "mapper-a1" }),
            "Talents: learning lights exactly one node and charges exactly one point");
        before = Json(g);
        check(!e.LearnTalent("mapper", "mapper-a1").Success && Json(g) == before,
            "Talents: repeated clicks cannot re-buy or duplicate a learned node");
        Need(e.LearnTalent("mapper", "mapper-b1"));
        before = Json(g);
        check(!e.LearnTalent("mapper", "mapper-a2").Success && Json(g) == before,
            "Talents: prerequisites alone do not bypass insufficient point cost");
        Need(e.StartMission("hotel"));
        before = Json(g);
        check(!e.LearnTalent("warden", "warden-a1").Success && Json(g) == before,
            "Talents: points cannot be spent to reconfigure talents in the middle of a sortie");
    }

    private static void UnlockAndDeck(Action<bool, string> check)
    {
        foreach (var definition in Catalog.Heroes)
        {
            var g = GridEngine.NewGame(); var e = new GridEngine(g); var hero = g.Hero(definition.Id);
            var node = TalentCatalog.ForHero(hero.Id).First(n => n.UnlockCardId.Length > 0);
            string card = node.UnlockCardId;
            check(!e.GetAvailableDeckCards(hero.Id).Any(c => c.Id == card) && !hero.Deck.Contains(card),
                $"Talents: {hero.Id} signature cards are absent from initial deck choices");
            string before = Json(g);
            check(!e.ChangeDeck(hero.Id, hero.Deck[0], card).Success && Json(g) == before,
                $"Talents: {hero.Id} cannot install a locked signature card by its ID");
            hero.SkillPoints = 3; // Isolate purchasing, not the separate campaign earning witness.
            LearnPath(e, hero.Id, node.Id);
            int originalCount = hero.Deck.Count;
            check(e.GetAvailableDeckCards(hero.Id).Count(c => c.Id == card) == 1 && hero.Deck.Count == originalCount && !hero.Deck.Contains(card),
                $"Talents: {hero.Id} unlocks a library option without silently growing the deck");
            Need(e.ChangeDeck(hero.Id, hero.Deck[0], card));
            check(hero.Deck.Contains(card) && hero.Deck.Count == originalCount,
                $"Talents: {hero.Id} may deliberately replace a card with the newly learned ability");
            Need(e.ChangeDeck(hero.Id, "", card));
            before = Json(g);
            check(!e.ChangeDeck(hero.Id, "", card).Success && Json(g) == before,
                $"Talents: {hero.Id} unlock does not bypass the two-copy limit");
        }
    }

    private static void EffectiveCards(Action<bool, string> check)
    {
        foreach (var (hero, card, baseDamage) in new[] { ("mapper", "shot", 2), ("engineer", "arc", 3), ("warden", "soothe", 2) })
        {
            var (g, e, f, a) = Arena(hero);
            var enemy = Enemy("damage", new(2, 3), 10); f.Enemies.Add(enemy); a.Hand.Add(card);
            string oldDescription = e.GetCardView(hero, card).Description;
            Need(e.Play(hero, card, enemy.Cell));
            check(enemy.Health == 10 - baseDamage, $"Talents: {hero} existing attack retains its untrained base damage");
            (g, e, f, a) = Arena(hero, hero + "-c1");
            enemy = Enemy("damage", new(2, 3), 10); f.Enemies.Add(enemy); a.Hand.Add(card);
            string before = Json(g); var preview = e.Preview(hero, card, enemy.Cell);
            check(preview.Success && Json(g) == before && preview.Message.Contains((baseDamage + 1).ToString()),
                $"Talents: {hero} upgraded attack preview reports the extra damage without applying it");
            var shown = e.GetCardView(hero, card);
            Need(e.Play(hero, card, enemy.Cell));
            check(enemy.Health == 10 - baseDamage - 1 && shown.Description != oldDescription && shown.Description.Contains((baseDamage + 1).ToString()),
                $"Talents: {hero} damage increase exists in the actual card view and actual combat");
        }
        {
            var (g, e, f, a) = Arena("mapper", "mapper-a2");
            check(e.GetCardView("mapper", "move").Range == 2 && e.GetCardView("mapper", "sprint").Range == 4,
                "Talents: effective card range includes both movement nodes");
            Need(e.Play("mapper", "move", new Cell(3, 3)));
            check(a.Cell == new Cell(3, 3) && a.Actions == 1, "Talents: movement node changes the reachable grid at the original action cost");
        }
        {
            var (g, e, f, a) = Arena("mapper", "mapper-c2");
            var enemy = Enemy("capture-subject", new(2, 3), 2); enemy.MarkedUntil = f.Round + 1; f.Enemies.Add(enemy); a.Hand.Add("shot");
            var preview = e.Preview("mapper", "shot", enemy.Cell);
            Need(e.Play("mapper", "shot", enemy.Cell));
            check(enemy.Health == 1 && enemy.Alive && preview.Message.Contains("1"),
                "Talents: altered shot preserves a marked capture subject at one health and predicts that result");
            (g, e, f, a) = Arena("mapper", "mapper-c2"); enemy = Enemy("unmarked", new(2, 3), 2); f.Enemies.Add(enemy); a.Hand.Add("shot");
            Need(e.Play("mapper", "shot", enemy.Cell));
            check(!enemy.Alive, "Talents: nonlethal shot mechanism requires a real mark instead of protecting every target");
        }
        {
            var (g, e, f, a) = Arena("engineer", "engineer-c2"); var ally = f.Actors.Single(x => x.HeroId != "engineer");
            a.Hand.Add("repair"); int hp = g.Hero(ally.HeroId).Health; Need(e.Play("engineer", "repair", ally.Cell));
            check(g.Hero(ally.HeroId).Health == hp && ally.Block == 2,
                "Talents: modified maintenance creates protection even when its patient is already healthy");
        }
        {
            var (g, e, f, a) = Arena("warden", "warden-c2"); var crate = f.Tile(new Cell(2, 3))!; crate.Kind = "crate"; a.Hand.Add("probe");
            Need(e.Play("warden", "probe", crate.Cell));
            check(f.Cargo.Count == 1 && f.Loot == 3 && crate.ItemId == "" && a.Block == 2 && a.Actions == 1,
                "Talents: upgraded probe opens, collects and protects in one paid action");
            (g, e, f, a) = Arena("warden", "warden-c2"); crate = f.Tile(new Cell(2, 3))!; crate.Kind = "crate";
            f.Cargo.AddRange(new[] { "loot:full1", "loot:full2", "loot:full3" }); a.Hand.Add("probe");
            Need(e.Play("warden", "probe", crate.Cell));
            check(f.Cargo.Count == 3 && crate.ItemId.StartsWith("loot:") && f.Loot == 0,
                "Talents: automatic probe collection respects capacity and leaves the real item for later pickup");
        }
    }

    private static void SignatureActions(Action<bool, string> check)
    {
        {
            var (g, e, f, a) = Arena("mapper", "mapper-a3"); a.Hand.Add("trailblaze"); var clue = f.Tile(new Cell(4, 2))!; clue.Kind = "clue";
            Need(e.Play("mapper", "trailblaze", new Cell(4, 3)));
            check(a.Cell == new Cell(4, 3) && f.EvidenceKnown && clue.Used && a.Actions == 1,
                "Talents: trailblaze moves through the grid and investigates a neighboring clue in one card");
        }
        {
            var (g, e, f, a) = Arena("mapper", "mapper-b3"); a.Hand.Add("chain-mark");
            var first = Enemy("first", new(3, 3)); var adjacent = Enemy("adjacent", new(3, 4)); var far = Enemy("far", new(6, 3));
            f.Enemies.AddRange(new[] { first, adjacent, far }); Need(e.Play("mapper", "chain-mark", first.Cell));
            check(first.MarkedUntil >= f.Round && adjacent.MarkedUntil >= f.Round && first.StunnedUntil >= f.Round && far.MarkedUntil < f.Round && f.Actors.All(x => x.DamageBonus == 1),
                "Talents: chain mark controls only the target cluster and inherits the learned cooperation mechanisms");
        }
        {
            var (g, e, f, a) = Arena("mapper", "mapper-c3"); a.Hand.Add("piercing-shot");
            var front = Enemy("front", new(3, 3), 10); var rear = Enemy("rear", new(4, 3), 10); var side = Enemy("side", new(3, 4), 10);
            f.Enemies.AddRange(new[] { front, rear, side }); Need(e.Play("mapper", "piercing-shot", front.Cell));
            check(front.Health == 7 && rear.Health == 7 && side.Health == 10 && a.Actions == 0,
                "Talents: piercing shot damages the selected target and its rear cell, not a generic area");
        }
        {
            var (g, e, f, a) = Arena("engineer", "engineer-a3"); a.Hand.Add("mobile-light");
            Need(e.Play("engineer", "mobile-light", new Cell(3, 3)));
            check(a.Cell == new Cell(3, 3) && f.LightCell == a.Cell && f.LightUntil >= f.Round + 2 && a.Actions == 1,
                "Talents: mobile light carries the engineer to a real tile and deploys the learned lasting illumination");
        }
        {
            var (g, e, f, a) = Arena("engineer", "engineer-b3"); a.Hand.Add("light-dome");
            Need(e.Play("engineer", "light-dome", a.Cell));
            check(f.LightCell == a.Cell && f.Actors.All(x => x.Block == 3) && f.LightUntil >= f.Round + 2,
                "Talents: light dome protects nearby actual actors and inherits extended light duration");
        }
        {
            var (g, e, f, a) = Arena("engineer", "engineer-c3"); a.Hand.Add("chain-arc");
            var first = Enemy("first", new(3, 3), 10); var adjacent = Enemy("adjacent", new(3, 4), 10); var far = Enemy("far", new(6, 3), 10);
            f.Enemies.AddRange(new[] { first, adjacent, far }); Need(e.Play("engineer", "chain-arc", first.Cell));
            check(first.Health == 8 && adjacent.Health == 8 && far.Health == 10 && a.Actions == 0,
                "Talents: chain arc spends both actions to damage only the visible neighboring enemy cluster");
        }
        {
            var (g, e, f, a) = Arena("warden", "warden-a3"); a.Hand.Add("pact"); var target = Enemy("subject", new(2, 3), 2); f.Enemies.Add(target);
            Need(e.Play("warden", "pact", target.Cell));
            check(target.Health == 1 && target.Alive && target.MarkedUntil >= f.Round,
                "Talents: pact creates a living, marked capture opportunity instead of killing a weak subject");
        }
        {
            var (g, e, f, a) = Arena("warden", "warden-b3"); a.Hand.Add("seal-circle");
            var first = Enemy("first", new(3, 3)); var adjacent = Enemy("adjacent", new(3, 4)); var far = Enemy("far", new(6, 3));
            f.Enemies.AddRange(new[] { first, adjacent, far }); Need(e.Play("warden", "seal-circle", first.Cell));
            check(first.StunnedUntil >= f.Round + 1 && adjacent.StunnedUntil >= f.Round + 1 && far.StunnedUntil < f.Round,
                "Talents: seal circle establishes two-turn control of its actual grid area");
        }
        {
            var (g, e, f, a) = Arena("warden", "warden-c3"); a.Hand.Add("team-aid");
            foreach (var actor in f.Actors) g.Hero(actor.HeroId).Health = 3;
            Need(e.Play("warden", "team-aid", a.Cell));
            check(f.Actors.All(x => g.Hero(x.HeroId).Health == 7 && x.Block == 1) && a.Actions == 0,
                "Talents: team aid spends two actions to restore and shield both deployed people");
        }
    }

    private static void Persistence(Action<bool, string> check)
    {
        string directory = Path.Combine(Path.GetTempPath(), "Joy-TalentsTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory); string path = Path.Combine(directory, "save.json");
        try
        {
            var g = GridEngine.NewGame(); var e = new GridEngine(g);
            g.Hero("mapper").SkillPoints = 3;
            LearnPath(e, "mapper", "mapper-a3");
            string card = TalentCatalog.Node("mapper-a3").UnlockCardId;
            Need(e.ChangeDeck("mapper", g.Hero("mapper").Deck[0], card)); Need(e.StartMission("hotel"));
            Need(GridSave.Save(path, g)); string initial = Json(g);
            var restored = GridSave.Load(path, out var message);
            check(restored is not null && Json(restored) == initial,
                "Talents: learned nodes, signature deck, hand and draw sequence round trip together");
            Need(e.Play("mapper", "end", g.Field!.Actor("mapper").Cell));
            var resumed = new GridEngine(restored!); Need(resumed.Play("mapper", "end", restored!.Field!.Actor("mapper").Cell));
            check(Json(g) == Json(restored), "Talents: continuing a talent save preserves deterministic draws and effects");

            var legacy = GridEngine.NewGame(); legacy.Version = 1;
            foreach (var h in legacy.Heroes) { h.SkillPoints = 0; h.LearnedTalents.Clear(); h.Specialization = h.Id == "engineer" ? "b" : "a"; h.SkillLevel = 2; }
            WriteEnvelope(path, legacy, omitTalents: true);
            string oldFile = File.ReadAllText(path);
            var migrated = GridSave.Load(path, out message);
            check(migrated?.Version == 2 && migrated.Heroes.All(h => h.SkillPoints == 0 && h.LearnedTalents.Count == 2 &&
                    h.LearnedTalents.Contains(h.Id + "-" + h.Specialization + "1") && h.LearnedTalents.Contains(h.Id + "-" + h.Specialization + "2")),
                "Talents: original v1 branch levels migrate to exact matching nodes without free points");
            check(File.ReadAllText(path) == oldFile, "Talents: loading migration does not overwrite its source checkpoint");
            Need(GridSave.Save(path, migrated!));
            check(File.Exists(path + ".v1-backup") && File.ReadAllText(path + ".v1-backup") == oldFile,
                "Talents: first migrated save preserves the original v1 checkpoint separately");
            Need(GridSave.Save(path, migrated!));
            check(File.ReadAllText(path + ".v1-backup") == oldFile,
                "Talents: subsequent saves never replace the original v1 migration backup");
            string validFile = File.ReadAllText(path);
            var invalid = GridEngine.NewGame(); invalid.Hero("mapper").LearnedTalents.Add("mapper-a3");
            check(!GridSave.Save(path, invalid).Success && File.ReadAllText(path) == validFile,
                "Talents: a deep node lacking its prerequisites cannot overwrite a valid save");
            invalid = GridEngine.NewGame(); invalid.Hero("mapper").LearnedTalents.Add("missing");
            check(!GridSave.Save(path, invalid).Success && File.ReadAllText(path) == validFile,
                "Talents: unknown node IDs are rejected by persistence");
            invalid = GridEngine.NewGame(); invalid.Hero("mapper").Deck[0] = card;
            check(!GridSave.Save(path, invalid).Success && File.ReadAllText(path) == validFile,
                "Talents: persistence cannot launder an unearned signature card into a deck");
            var future = GridEngine.NewGame(); future.Version = 999; WriteEnvelope(path, future);
            string futureFile = File.ReadAllText(path);
            check(GridSave.Load(path, out message) is null && File.ReadAllText(path) == futureFile,
                "Talents: unsupported future game versions refuse fallback and preserve the original file");
        }
        finally
        {
            string resolved = Path.GetFullPath(directory);
            string root = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (resolved.StartsWith(root, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(resolved).StartsWith("Joy-TalentsTests-", StringComparison.Ordinal) && Directory.Exists(resolved))
                Directory.Delete(resolved, true);
        }
    }

    private static void LearnPath(GridEngine e, string heroId, string nodeId)
    {
        var node = TalentCatalog.Node(nodeId);
        foreach (var prerequisite in node.Prerequisites)
            if (!e.Game.Hero(heroId).LearnedTalents.Contains(prerequisite)) LearnPath(e, heroId, prerequisite);
        if (!e.Game.Hero(heroId).LearnedTalents.Contains(nodeId)) Need(e.LearnTalent(heroId, nodeId));
    }
    private static (GridGame G, GridEngine E, Joy.Core.GridPrototype.FieldState F, ActorState A) Arena(string heroId, params string[] nodes)
    {
        var g = GridEngine.NewGame(); var e = new GridEngine(g);
        g.Hero(heroId).SkillPoints = 9;
        foreach (var node in nodes) LearnPath(e, heroId, node);
        if (!g.SelectedHeroes.Contains(heroId)) { Need(e.ToggleHero("mapper")); Need(e.ToggleHero(heroId)); }
        Need(e.StartMission("hotel")); var f = g.Field!;
        foreach (var tile in f.Tiles) { tile.Kind = "floor"; tile.ItemId = ""; tile.Used = false; }
        f.Enemies.Clear();
        var a = f.Actor(heroId); a.X = 1; a.Y = 3; a.Hand.Clear();
        var partner = f.Actors.Single(x => x.HeroId != heroId); partner.X = 1; partner.Y = 4;
        return (g, e, f, a);
    }
    private static EnemyState Enemy(string id, Cell cell, int hp = 6) => new() { Id = id, Name = id, X = cell.X, Y = cell.Y, Health = hp, MaxHealth = hp, Kind = "shadow" };
    private static void WriteEnvelope(string path, GridGame game, bool omitTalents = false)
    {
        var node = JsonNode.Parse(Json(game))!;
        if (omitTalents) foreach (var hero in node["Heroes"]!.AsArray()) hero!.AsObject().Remove("LearnedTalents");
        string payload = node.ToJsonString();
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        File.WriteAllText(path, JsonSerializer.Serialize(new { Format = 1, Payload = payload, Hash = hash }));
    }
    private static string Json(GridGame g) => GridPrototypeTests.Json(g);
    private static void Need(Result r) => GridPrototypeTests.Need(r);
}
