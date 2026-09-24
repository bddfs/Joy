using System;
using System.Collections.Generic;
using System.Linq;

namespace Joy.Core;

public enum DayPhase { Preparation, Locked, Executing, Settlement, Victory, Failed }
public enum JobKind { Survey, Recovery, Manual, Build, Maintenance, Repair, Recruit, ContrastSurvey, Supply, HeadquartersSeal, Research }
public enum EmployeeStatus { Standby, Stationed, Deployed, Construction, Recovery }
public enum ItemLocation { Room, Backpack, Warehouse, Delivered, Consumed, Installed, Sold }
public enum CardKind { Investigate, Search, Knock, Light, Rest, Ward, Sprint, Mimic, Seal, Banish }
public enum RuleScope { City, Line, Local }
public enum RuleTrigger { SearchInDark, Knock, CarryUnsealed, MoveInDark, Rest, LightEnds, TransportUnsealed, MirrorSearch }
public enum FieldActionKind { Move, Search, Pickup, Drop, Seal, PlaceRoom, PlayCard, Reveal, SupportLight }

public sealed class GameState
{
    public int SchemaVersion { get; set; } = 2;
    public string Scenario { get; set; } = "M2";
    public bool PressureEnabled { get; set; }
    public List<string> LastDaySummary { get; set; } = new();
    public List<SettlementStage> SettlementTimeline { get; set; } = new();
    public int Seed { get; set; } = 413;
    public ulong RandomState { get; set; } = 413;
    public string CityName { get; set; } = "雾港";
    public int CityIndex { get; set; } = 1;
    public int Day { get; set; } = 1;
    public int Deadline { get; set; } = 5;
    public int Money { get; set; } = 16;
    public int DailyExpense { get; set; } = 2;
    public int TransactionLimit { get; set; } = 2;
    public int RequiredStations { get; set; } = 2;
    public DayPhase Phase { get; set; } = DayPhase.Preparation;
    public List<Employee> Employees { get; set; } = new();
    public List<CityNode> Nodes { get; set; } = new();
    public List<CityEdge> Edges { get; set; } = new();
    public List<RuleState> Rules { get; set; } = new();
    public List<ItemState> Items { get; set; } = new();
    public List<Assignment> Plan { get; set; } = new();
    public List<Assignment> LockedPlan { get; set; } = new();
    public Dictionary<string, string> Stations { get; set; } = new();
    public List<string> MorningOperatingNodes { get; set; } = new();
    public List<SpreadIntent> Forecast { get; set; } = new();
    public List<string> PaidRewards { get; set; } = new();
    public List<LedgerEntry> Ledger { get; set; } = new();
    public List<string> Log { get; set; } = new();
    public long LogSequence { get; set; }
    public bool SourceRecovered { get; set; }
    public bool EmergencySupportUsed { get; set; }
    public bool Expanded { get; set; }
    public string Outcome { get; set; } = "";
    public FieldState? Field { get; set; }
    public int SortieSequence { get; set; }
    public string LastCheckpoint { get; set; } = "新局 · 准备阶段";
    public DateTimeOffset? SavedAt { get; set; }
    public List<string> SeenHints { get; set; } = new();
    public List<CardKind> TeamDeck { get; set; } = new();

    public CityNode Node(string id) => Nodes.First(n => n.Id == id);
    public ItemState Item(string id) => Items.First(i => i.Id == id);
    public RuleState Rule(string id) => Rules.First(r => r.Id == id);
    public void Record(string text) { LogSequence++; Log.Add($"第 {Day} 日 · {text}"); if (Log.Count > 250) Log.RemoveAt(0); }
    public int Next(int max)
    {
        if (max <= 0) throw new ArgumentOutOfRangeException(nameof(max));
        ulong x = RandomState == 0 ? 0x9e3779b97f4a7c15UL : RandomState;
        x ^= x >> 12; x ^= x << 25; x ^= x >> 27;
        RandomState = x;
        return (int)((x * 2685821657736338717UL) % (ulong)max);
    }
}

public sealed class Employee
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public EmployeeStatus Status { get; set; }
    public int RecoverUntilDay { get; set; }
}

public sealed class CityNode
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "Hotel";
    public string Description { get; set; } = "";
    public float X { get; set; }
    public float Y { get; set; }
    public bool IsSource { get; set; }
    public bool IsHeadquarters { get; set; }
    public bool PublicCooperation { get; set; }
    public bool SurveyPaid { get; set; }
    public bool RecoveryPaid { get; set; }
    public bool Built { get; set; }
    public int BuiltDay { get; set; }
    public bool Invaded { get; set; }
    public bool Maintained { get; set; } = true;
    public int MaintenanceCount { get; set; }
    public string? BlockadeEdgeId { get; set; }
    public string TargetItemId { get; set; } = "";
    public int SurveyReward { get; set; } = 2;
    public int RecoveryReward { get; set; } = 5;
    public int TravelCost { get; set; }
    public List<string> RuleIds { get; set; } = new();
    public List<RoomState> Rooms { get; set; } = new();
    public List<string> InvasionSources { get; set; } = new();
}

public sealed class CityEdge
{
    public string Id { get; set; } = "";
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Passable { get; set; } = true;
    public string? RuleId { get; set; }
}

public sealed class Assignment
{
    public string Id { get; set; } = "";
    public JobKind Kind { get; set; }
    public string NodeId { get; set; } = "";
    public List<string> EmployeeIds { get; set; } = new();
    public List<string> ToolIds { get; set; } = new();
    public List<string> Route { get; set; } = new();
    public List<string> KnownRuleIds { get; set; } = new();
    public string TargetItemId { get; set; } = "";
    public string SupportItemId { get; set; } = "";
    public string SurveyTemplate { get; set; } = "";
    public int Cost { get; set; }
    public bool Completed { get; set; }
    public bool Successful { get; set; }
    public bool Paid { get; set; }
}

public sealed class SpreadIntent
{
    public string SourceId { get; set; } = "";
    public string EdgeId { get; set; } = "";
    public string TargetId { get; set; } = "";
    public string Description { get; set; } = "";
}

public sealed class LedgerEntry
{
    public int Day { get; set; }
    public string Category { get; set; } = "";
    public string Description { get; set; } = "";
    public int Amount { get; set; }
    public int Balance { get; set; }
}

public sealed class SettlementStage
{
    public int Sequence { get; set; }
    public int Day { get; set; }
    public string Title { get; set; } = "";
    public List<string> Events { get; set; } = new();
    public int MoneyBefore { get; set; }
    public int MoneyAfter { get; set; }
}

public sealed class RuleState
{
    public string Id { get; set; } = "";
    public string NodeId { get; set; } = "";
    public string Name { get; set; } = "未确认守则";
    public RuleScope Scope { get; set; }
    public int Order { get; set; }
    public RuleTrigger Trigger { get; set; }
    public int CorrectCandidate { get; set; }
    public List<string> Candidates { get; set; } = new();
    public string Notice { get; set; } = "";
    public string Clue { get; set; } = "";
    public string Answer { get; set; } = "";
    public int WillDamage { get; set; } = 2;
    public int DisturbanceGain { get; set; }
    public bool Wardable { get; set; } = true;
    public bool EvidenceFound { get; set; }
    public bool Archived { get; set; }
    public bool Observed { get; set; }
    public bool Confirmed { get; set; }
    public bool ConfirmationRewardClaimed { get; set; }
    public List<int> WrongCandidates { get; set; } = new();
    public int LastCityAttemptDay { get; set; }
    public string RevealedBy { get; set; } = "";
    public string Family { get; set; } = "";
    public bool MirrorVariantB { get; set; }
    public List<string> RoomIds { get; set; } = new();
    public List<string> EvidenceRecords { get; set; } = new();
    public bool HasContrastObservation { get; set; }
    public bool ArchivedContrastObservation { get; set; }
    public bool? ContrastTriggered { get; set; }
    public int ContrastSamplesRemaining { get; set; } = 1;
}

public sealed class ItemState
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string NodeId { get; set; } = "";
    public string RoomId { get; set; } = "";
    public string Kind { get; set; } = "Anomaly";
    public ItemLocation Location { get; set; } = ItemLocation.Room;
    public string OwnerId { get; set; } = "";
    public int Size { get; set; } = 1;
    public int Value { get; set; }
    public bool Discovered { get; set; }
    public bool Sealed { get; set; }
    public bool RequiresSealing { get; set; }
    public bool RequiresLightForSealing { get; set; }
    public bool Required { get; set; }
    public int Charges { get; set; } = 1;
    public int MaxCharges { get; set; } = 2;
    public int AvailableDay { get; set; } = 1;
    public string InstalledNodeId { get; set; } = "";
    public int SupportUsedDay { get; set; }
    public string Disposition { get; set; } = "";
    public string Description { get; set; } = "";
    public string ContractNodeId { get; set; } = "";
    public string? ReservedBy { get; set; }
}

public sealed class RoomState
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public bool IsEntrance { get; set; }
    public bool Entered { get; set; }
    public bool Placed { get; set; } = true;
    public int RestUses { get; set; }
    public int SearchUses { get; set; }
    public int KnockUses { get; set; }
    public bool AllowsKnock { get; set; }
    public bool IsMirrorRoom { get; set; }
    public bool HasArchiveCabinet { get; set; }
    public int ArchiveSearchRemaining { get; set; }
    public bool ContainerLocated { get; set; }
    public List<string> Neighbors { get; set; } = new();
}

public sealed class FieldState
{
    public string AssignmentId { get; set; } = "";
    public string NodeId { get; set; } = "";
    public string RoomId { get; set; } = "";
    public int SortieId { get; set; }
    public int Round { get; set; } = 1;
    public int RoundLimit { get; set; } = 12;
    public int Actions { get; set; } = 3;
    public int Will { get; set; } = 12;
    public int MaxWill { get; set; } = 12;
    public int Wards { get; set; } = 1;
    public int Disturbance { get; set; }
    public bool Light { get; set; }
    public int Ink { get; set; } = 2;
    public int Coins { get; set; }
    public int Capacity { get; set; } = 6;
    public int LastConfirmationRound { get; set; }
    public int RevealCharges { get; set; }
    public bool Finished { get; set; }
    public bool Escaped { get; set; }
    public string EndReason { get; set; } = "";
    public CardKind? MimicReference { get; set; }
    public List<CardKind> DrawPile { get; set; } = new();
    public List<CardKind> Hand { get; set; } = new();
    public List<CardKind?> HandSlots { get; set; } = new();
    public Dictionary<string, int> NormalSearchCounts { get; set; } = new();
    public string AssignedLightSupportId { get; set; } = "";
    public bool LightSupportUsed { get; set; }
    public List<CardKind> Discard { get; set; } = new();
    public List<CardKind> Exhausted { get; set; } = new();
    public List<ShadowState> Shadows { get; set; } = new();
    public List<string> AcquiredItemIds { get; set; } = new();
    public List<string> BroughtItemIds { get; set; } = new();
    public List<string> RevealedRuleIds { get; set; } = new();
    public List<string> TriggeredEffects { get; set; } = new();
}

public sealed class ShadowState
{
    public string RoomId { get; set; } = "";
    public int AnnouncedRound { get; set; }
    public int Damage { get; set; } = 2;
    public bool Banishable { get; set; } = true;
}

public sealed class FieldAction
{
    public FieldActionKind Kind { get; set; }
    public CardKind? Card { get; set; }
    public int HandSlotIndex { get; set; } = -1;
    public string TargetId { get; set; } = "";
    public bool AcceptItem { get; set; } = true;
    public List<string> Path { get; set; } = new();
}

public sealed record ActionResult(bool Success, string Message)
{
    public static ActionResult Ok(string message = "完成") => new(true, message);
    public static ActionResult Fail(string message) => new(false, message);
}

public sealed record ActionPreview(bool Allowed, int Cost, string Text, bool RequiresConfirmation = false);
