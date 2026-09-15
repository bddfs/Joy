using Joy.Core;

namespace Joy.Tests;

public static class IntegrationTests
{
    public static void Run(Action<bool, string> check)
    {
        foreach (int seed in new[] { 413, 1, 20260915, 7829 })
        {
            var manager = ManagementRoute(seed);
            check(manager.Phase == DayPhase.Victory && manager.Day <= 5, $"AC08M management path seed {seed}");
            check(!manager.EmergencySupportUsed && manager.Money >= 0, "Management needs no undeclared support or negative money");
            var mixed = MixedRoute(seed);
            check(mixed.Phase == DayPhase.Victory && mixed.Day <= 5, $"AC08M mixed actual field path seed {seed}");
            check(mixed.SortieSequence > 0 && !mixed.EmergencySupportUsed, "Mixed route used actual field engine without top-up");
        }
        ActualHospitalRecovery(check);
        ResumeActualField(check);
    }

    public static GameState ManagementRoute(int seed)
    {
        var state = WorldFactory.CreateLegacy(seed); var city = new CityEngine(state);
        Must(city.AddTask(JobKind.Survey, "hotel")); Must(city.AddTask(JobKind.Survey, "source")); Day(city);
        ConfirmAvailable(state, city);
        Must(city.AddTask(JobKind.Survey, "hotel")); Must(city.AddTask(JobKind.Recovery, "source")); Day(city);
        ConfirmAvailable(state, city);
        Must(city.AddTask(JobKind.Recovery, "hotel")); Must(city.AddTask(JobKind.Build, "depot")); Day(city);
        Must(city.AddTask(JobKind.Build, "hotel")); Day(city);
        Must(city.AssignStation("hotel", "e1")); Must(city.AssignStation("depot", "e2")); Day(city);
        return state;
    }

    public static GameState MixedRoute(int seed)
    {
        var state = WorldFactory.CreateLegacy(seed); var city = new CityEngine(state);
        Must(city.AddTask(JobKind.Manual, "hotel")); Must(city.AddTask(JobKind.Survey, "source")); Must(city.LockDay());
        var field = new FieldEngine(state); Must(field.Start(state.LockedPlan.First(a => a.Kind == JobKind.Manual)));
        InvestigateAll(state, field);
        Must(field.Withdraw()); Must(city.CompleteManual()); Must(city.SettleDay());
        ConfirmAvailable(state, city);
        Must(city.AddTask(JobKind.Recovery, "hotel")); Must(city.AddTask(JobKind.Recovery, "source")); Day(city);
        Must(city.AddTask(JobKind.Build, "hotel")); Must(city.AddTask(JobKind.Build, "depot")); Day(city);
        Must(city.AssignStation("hotel", "e1")); Must(city.AssignStation("depot", "e2")); Day(city);
        return state;
    }

    private static void InvestigateAll(GameState state, FieldEngine field)
    {
        int guard = 0;
        while (field.ApplicableRules().Any(r => !r.EvidenceFound))
        {
            if (++guard > 20 || state.Field!.Finished) throw new Exception("Cannot acquire ordinary evidence through actual deck");
            if (state.Field.Actions > 0 && state.Field.Hand.Contains(CardKind.Investigate))
                Must(field.Execute(new FieldAction { Kind = FieldActionKind.PlayCard, Card = CardKind.Investigate }));
            else Must(field.EndRound());
        }
    }

    private static void ActualHospitalRecovery(Action<bool, string> check)
    {
        var state = WorldFactory.CreateLegacy(413); var city = new CityEngine(state);
        Must(city.AddTask(JobKind.Manual, "hospital")); Must(city.LockDay());
        var field = new FieldEngine(state); Must(field.Start(state.LockedPlan[0]));
        Must(field.Execute(new FieldAction { Kind = FieldActionKind.Move, TargetId = "hospital-room1" }));
        Must(field.Execute(new FieldAction { Kind = FieldActionKind.Search }));
        check(state.Item("hospital-vial").Location == ItemLocation.Backpack && !state.Node("hospital").RecoveryPaid, "AC28 carrying does not pay contract");
        Must(field.EndRound());
        Must(field.Execute(new FieldAction { Kind = FieldActionKind.Seal, TargetId = "hospital-vial" }));
        Must(field.Execute(new FieldAction { Kind = FieldActionKind.Move, TargetId = "hospital-entry" }));
        Must(field.Withdraw()); Must(city.CompleteManual());
        check(state.Item("hospital-vial").Location == ItemLocation.Warehouse, "Successful exit transfers unique item to warehouse before delivery");
        int balance = state.Money;
        Must(city.CompleteManual()); check(state.Money == balance, "Repeat CompleteManual does not pay twice");
        Must(city.SettleDay());
        check(state.Item("hospital-vial").Location == ItemLocation.Delivered && state.Node("hospital").RecoveryPaid, "AC28 actual physical hospital delivery after day settlement");
        check(state.Item("seal-a").Charges == 1, "Actual basic seal consumes one physical kit charge");
    }

    private static void ResumeActualField(Action<bool, string> check)
    {
        var directory = Path.Combine(Path.GetTempPath(), "JoyIntegration-" + Guid.NewGuid().ToString("N"));
        var store = new SaveStore(directory); var state = WorldFactory.CreateLegacy(52); var city = new CityEngine(state);
        Must(city.AddTask(JobKind.Manual, "hotel")); Must(city.LockDay()); var field = new FieldEngine(state);
        Must(field.Start(state.LockedPlan[0])); Must(field.EndRound()); Must(store.Save(state));
        var restored = store.Load(); check(restored.Success, "Save actual locked plan/physical tools/field snapshot");
        if (restored.State is not { } copy) throw new Exception(restored.Message);
        var resumed = new FieldEngine(copy);
        Must(field.EndRound()); Must(resumed.EndRound());
        check(state.RandomState == copy.RandomState && state.Field!.Hand.SequenceEqual(copy.Field!.Hand), "AC31 resumed next round matches RNG and actual hand");
        check(state.Money == copy.Money && copy.Items.Count(i => i.Id == "seal-a") == 1, "Read restores prepaid funds and unique tool without replay");
        // Delete only the two explicit files this case created, without recursive filesystem operations.
        File.Delete(store.SavePath); if (File.Exists(store.BackupPath)) File.Delete(store.BackupPath); Directory.Delete(directory);
    }

    private static void ConfirmAvailable(GameState state, CityEngine city)
    {
        // The witness chooses the candidate justified by the content's explicit reliable clue.
        // It does not grant evidence, change knowledge flags or modify any resource.
        foreach (var rule in state.Rules.Where(r => r.Archived && r.EvidenceFound && !r.Confirmed)) Must(city.ConfirmRule(rule.Id, rule.CorrectCandidate));
    }
    private static void Day(CityEngine city) { Must(city.LockDay()); Must(city.SettleDay()); }
    private static void Must(ActionResult result) { if (!result.Success) throw new Exception(result.Message); }
}
