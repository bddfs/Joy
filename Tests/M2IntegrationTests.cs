using Joy.Core;
using System.Text.Json;

namespace Joy.Tests;

/// <summary>AC42 witnesses: only real planning, field actions, delivery and settlement APIs alter the world.</summary>
public static class M2IntegrationTests
{
    public sealed record DayWitness(int Day, int Money, int KitCharges, int StaffUsed, string[] Tasks, string[] Stations);
    public sealed record RouteWitness(string Route, bool VariantB, int LampPrice, int Seed, DayWitness[] Days, string Outcome);
    public static readonly List<RouteWitness> Witnesses = new();

    public static void Run(Action<bool, string> check)
    {
        Witnesses.Clear();
        foreach (bool variantB in new[] { false, true })
        foreach (int price in new[] { 6, 2 })
        {
            RunRoute("A", variantB, price, 413, check);
            RunRoute("B", variantB, price, 413, check);
            RunRoute("Install", variantB, price, 413, check);
            RunRoute("Delegated", variantB, price, 413, check);
            RunRoute("Unsealed", variantB, price, 413, check);
        }
        foreach (int seed in new[] { 1, 20260915, 7829 })
            RunRoute("B", seed % 2 == 0, 6, seed, check);
        ResumeRealM2(check);
    }

    private static void RunRoute(string route, bool variantB, int price, int seed, Action<bool, string> check)
    {
        var g = WorldFactory.Create(seed, variantB, price); var city = new CityEngine(g);
        var days = new List<DayWitness>();
        string context = $"AC42 {route}, truth {(variantB ? "B" : "A")}, price {price}, seed {seed}";
        void Settle()
        {
            int day = g.Day;
            var tasks = g.LockedPlan.Count > 0 ? g.LockedPlan : g.Plan;
            int people = tasks.SelectMany(t => t.EmployeeIds).Concat(g.Stations.Values).Distinct().Count();
            var jobs = tasks.Select(t => CityEngine.JobName(t.Kind) + " · " + g.Node(t.NodeId).Name).ToArray();
            var stations = g.Stations.Keys.Select(id => g.Node(id).Name).ToArray();
            Must(city.SettleDay(), context + " settle day " + day);
            days.Add(new(day, g.Money, g.Item("seal-a").Charges, people, jobs, stations));
            check(g.Money >= 0 && !g.EmergencySupportUsed, context + " respects real funds and no support");
            check(people <= 6, context + " has at most six actual people");
        }
        void Day() { Must(city.LockDay(), context + " lock"); Settle(); }
        void Job(JobKind kind, string node) => Must(city.AddTask(kind, node), context + " plan " + kind);
        void Staff(string node, string person) => Must(city.AssignStation(node, person), context + " staff");
        void Confirm(string id)
        {
            var rule = g.Rule(id);
            if (!rule.Archived) throw new Exception(context + ": evidence is not yet archived");
            int candidate = id == "hotel-mirror" ? InferMirror(rule) : rule.Candidates.FindIndex(c => c == rule.Answer);
            Must(city.ConfirmRule(id, candidate), context + " confirm archived evidence");
        }

        if (route == "Delegated")
        {
            Job(JobKind.Survey, "hotel"); Job(JobKind.Survey, "source"); Day();
            check(!g.Rule("hotel-mirror").HasContrastObservation && !g.Rule("hotel-mirror").Confirmed, context + " history does not choose a truth");
            Confirm("source-carry"); Job(JobKind.ContrastSurvey, "hotel"); Job(JobKind.Build, "depot"); Day();
            check(g.Item("hotel-clock").Location == ItemLocation.Room && !g.Item("hotel-clock").Discovered, context + " contrast leaves real contract inventory");
            Confirm("hotel-mirror"); Staff("depot", "e6"); Job(JobKind.Recovery, "hotel"); Job(JobKind.Recovery, "source"); Day();
            Job(JobKind.Build, "hotel"); Day();
            Staff("hotel", "e5"); Day();
        }
        else
        {
            Job(JobKind.Manual, "hotel"); Job(JobKind.Survey, "hospital"); Must(city.LockDay(), context);
            var field = new FieldEngine(g); Must(field.Start(g.LockedPlan.First(t => t.Kind == JobKind.Manual)), context);
            RecoverHotel(g, field, route != "A", route is "B" or "Install", check, context);
            Must(city.CompleteManual(), context);
            if (route == "B") Must(city.SellItem("lamp"), context + " actual returned lamp sale");
            Settle();
            Confirm("hospital-dark");
            if (route is "A" or "Unsealed")
            {
                Job(JobKind.Build, "hotel"); Job(JobKind.Recovery, "hospital"); Day();
                Staff("hotel", "e6"); Job(JobKind.Build, "hospital"); Job(JobKind.Survey, "source"); Day();
                Confirm("source-carry"); Staff("hospital", "e5"); Job(JobKind.Recovery, "source");
                if (route == "Unsealed") Must(city.ScheduleSupply(), context + " day4 supply");
                Day();
            }
            else
            {
                Job(JobKind.Build, "hotel"); Must(city.ScheduleSupply(), context + " day2 supply"); Day();
                Staff("hotel", "e6");
                if (route == "Install")
                {
                    Must(city.InstallLamp("lamp", "hotel"), context + " actual installation");
                    Job(JobKind.Manual, "hospital"); Job(JobKind.Survey, "source");
                    Must(city.AssignSupport(g.Plan.First(t => t.Kind == JobKind.Manual).Id, "lamp"), context);
                    Must(city.LockDay(), context);
                    var hospital = new FieldEngine(g); Must(hospital.Start(g.LockedPlan.First(t => t.Kind == JobKind.Manual)), context);
                    Step(g, hospital, new() { Kind = FieldActionKind.Move, TargetId = "hospital-room1" });
                    Step(g, hospital, new() { Kind = FieldActionKind.Search });
                    Must(hospital.EndRound(), context);
                    int ap = g.Field!.Actions; var reference = g.Field.MimicReference;
                    Step(g, hospital, new() { Kind = FieldActionKind.SupportLight });
                    check(g.Field.Actions == ap && g.Field.Light && g.Field.MimicReference == reference && g.Item("lamp").SupportUsedDay == 3, context + " installed lamp really supplies free light with retained reference");
                    Step(g, hospital, new() { Kind = FieldActionKind.Seal, TargetId = "hospital-vial" });
                    Step(g, hospital, new() { Kind = FieldActionKind.Move, TargetId = "hospital-entry" });
                    Must(hospital.Withdraw(), context); Must(city.CompleteManual(), context); Settle();
                }
                else { Job(JobKind.Recovery, "hospital"); Job(JobKind.Survey, "source"); Day(); }
                Confirm("source-carry"); Job(JobKind.Build, "hospital"); Job(JobKind.Recovery, "source"); Day();
                Staff("hospital", "e5"); Day();
            }
        }

        int[] expected = route switch
        {
            "A" => new[] { 19, 17, 13, 20 },
            "B" when price == 6 => new[] { 25, 16, 20, 22, 22 },
            "B" => new[] { 21, 12, 16, 18, 18 },
            "Install" => new[] { 19, 10, 14, 16, 16 },
            "Delegated" => new[] { 14, 7, 16, 11, 11 },
            _ => new[] { 19, 17, 13, 17 }
        };
        int[] charges = route switch
        {
            "A" => new[] { 1, 0, 0, 0 }, "Unsealed" => new[] { 1, 0, 0, 1 },
            "Delegated" => new[] { 2, 2, 1, 1, 1 }, _ => new[] { 0, 1, 0, 0, 0 }
        };
        check(days.Select(d => d.Money).SequenceEqual(expected), context + " exact daily money: " + string.Join(",", days.Select(d => d.Money)));
        check(days.Select(d => d.KitCharges).SequenceEqual(charges), context + " exact daily unique tool charges");
        check(g.Phase == DayPhase.Victory && g.Day == expected.Length, context + " actual handoff on intended day");
        check(g.SourceRecovered && g.Item("source-box").Location == ItemLocation.Delivered && g.Item("source-case").Location == ItemLocation.Delivered, context + " source and dedicated case physically delivered");
        check(g.Items.Count(i => i.Kind == "SealKit") == 1 && g.SortieSequence == (route == "Delegated" ? 0 : route == "Install" ? 2 : 1), context + " unique kit / correct manual sorties");
        if (route == "Unsealed")
            check(g.Item("lamp").Location == ItemLocation.Warehouse && !g.Item("lamp").Sealed && g.Item("lamp").Disposition == "", context + " retained unsealed lamp never monetized or installed");
        if (route == "Install") check(g.Item("lamp").Location == ItemLocation.Installed && g.Item("lamp").SupportUsedDay == 3, context + " lamp remains a local installed asset");
        if (route == "B") check(g.Item("lamp").Location == ItemLocation.Sold, context + " only one real sale");
        Witnesses.Add(new(route, variantB, price, seed, days.ToArray(), g.Outcome));
        Console.WriteLine($"M2_ROUTE | {route} | truth {(variantB ? "B" : "A")} | price {price} | seed {seed} | money {string.Join("/", days.Select(d => d.Money))} | kit {string.Join("/", days.Select(d => d.KitCharges))}");
    }

    private static void RecoverHotel(GameState g, FieldEngine field, bool takeLamp, bool sealLamp, Action<bool, string> check, string context)
    {
        Step(g, field, new() { Kind = FieldActionKind.Move, TargetId = "hotel-room1" });
        int wards = g.Field!.Wards;
        Step(g, field, new() { Kind = FieldActionKind.Search });
        check(g.Rule("hotel-mirror").HasContrastObservation && !g.Item("hotel-clock").Discovered && g.Item("hotel-clock").Location == ItemLocation.Room, context + " actual first dark cabinet search is distinguishing evidence, not target pickup");
        // Infer from this experiment's public event: the shield can cancel harm, not the observed reaction.
        bool reacted = g.Field.Wards < wards;
        int candidate = reacted ? 0 : 1;
        Must(field.Confirm("hotel-mirror", candidate), context + " evidence-based field judgment");
        check(g.Rule("hotel-mirror").Confirmed && g.Field.Coins == 1, context + " confirmation once from actual reaction");
        Step(g, field, new() { Kind = FieldActionKind.Search });
        Step(g, field, new() { Kind = FieldActionKind.Seal, TargetId = "hotel-clock" });
        if (takeLamp)
        {
            Step(g, field, new() { Kind = FieldActionKind.PlaceRoom, TargetId = "hotel-annex" });
            Step(g, field, new() { Kind = FieldActionKind.Move, TargetId = "hotel-annex" });
            Step(g, field, new() { Kind = FieldActionKind.Search });
            if (sealLamp) Step(g, field, new() { Kind = FieldActionKind.Seal, TargetId = "lamp" });
            Step(g, field, new() { Kind = FieldActionKind.Move, TargetId = "hotel-room1" });
            check(g.Item("lamp").Location == ItemLocation.Backpack && g.Item("lamp").Sealed == sealLamp, context + " optional room costs and physical lamp state paid");
        }
        Step(g, field, new() { Kind = FieldActionKind.Move, TargetId = "hotel-entry" });
        Must(field.Withdraw(), context);
        check(g.Field!.Escaped && g.Field.Will > 0, context + " actual legal return with positive will");
    }

    private static void Step(GameState g, FieldEngine engine, FieldAction action)
    {
        int needed = action.Kind is FieldActionKind.Search or FieldActionKind.Seal ? 2 : action.Kind is FieldActionKind.Drop or FieldActionKind.SupportLight ? 0 : 1;
        if (g.Field!.Actions < needed) Must(engine.EndRound(), "refill actual AP");
        if (g.Field.Finished) throw new Exception("Witness failed before " + action.Kind + ": " + g.Field.EndReason);
        Must(engine.Execute(action), "execute " + action.Kind + " in " + g.Field.RoomId);
    }

    private static int InferMirror(RuleState rule)
    {
        if (!rule.ArchivedContrastObservation || rule.ContrastTriggered is null) throw new Exception("Cannot infer mirror truth without actual archived contrast observation");
        return rule.ContrastTriggered.Value ? 0 : 1;
    }

    private static void ResumeRealM2(Action<bool, string> check)
    {
        string directory = Path.Combine(Path.GetTempPath(), "JoyM2Integration-" + Guid.NewGuid().ToString("N"));
        var save = new SaveStore(directory); var g = WorldFactory.Create(414, true); var city = new CityEngine(g);
        Must(city.AddTask(JobKind.Manual, "hotel")); Must(city.LockDay()); var field = new FieldEngine(g);
        Must(field.Start(g.LockedPlan[0])); Must(save.Save(g));
        Step(g, field, new() { Kind = FieldActionKind.Move, TargetId = "hotel-room1" });
        Step(g, field, new() { Kind = FieldActionKind.Search });
        Must(field.Confirm("hotel-mirror", 1)); g.LastCheckpoint = "镜面房 · 判断奖励后完整检查点";
        Must(save.Save(g)); var loaded = save.Load();
        check(loaded.Success && loaded.State is { Field.Coins: 1 }, "AC41 actual later reward checkpoint restores earned coin");
        if (loaded.State is not { } copy) throw new Exception(loaded.Message);
        check(copy.Field!.NormalSearchCounts.GetValueOrDefault("hotel-room1") == 1 && copy.Node("hotel").Rooms.First(r => r.Id == "hotel-room1").ArchiveSearchRemaining == 0 && !copy.Item("hotel-clock").Discovered, "AC41 actual consumed cabinet and untouched second container restored");
        var resumed = new FieldEngine(copy); Must(field.EndRound()); Must(resumed.EndRound());
        check(g.RandomState == copy.RandomState && g.Field!.HandSlots.SequenceEqual(copy.Field!.HandSlots), "AC41 actual M2 next draw and stable slots survive resume");
        Step(g, field, new() { Kind = FieldActionKind.Search }); Step(copy, resumed, new() { Kind = FieldActionKind.Search });
        check(g.Item("hotel-clock").Location == copy.Item("hotel-clock").Location && g.Field!.Will == copy.Field!.Will && g.Field.Wards == copy.Field.Wards, "AC41 restored second normal search produces identical physical/rule outcomes");
        File.Delete(save.SavePath); if (File.Exists(save.BackupPath)) File.Delete(save.BackupPath); Directory.Delete(directory);
    }

    private static void Must(ActionResult result, string context = "M2 integration")
    { if (!result.Success) throw new Exception(context + ": " + result.Message); }
}
