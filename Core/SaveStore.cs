using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Joy.Core;

public sealed record LoadResult(GameState? State, string Message, bool Success);

/// <summary>One complete checkpoint and one previous checkpoint. The caller chooses safe save boundaries.</summary>
public sealed class SaveStore
{
    private const int FormatVersion = 2;
    private readonly object gate = new();
    private readonly Action? beforeCommit;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public string SavePath { get; }
    public string BackupPath { get; }
    public bool Exists => File.Exists(SavePath) || File.Exists(BackupPath);

    public SaveStore(string directory, Action? beforeCommit = null)
    {
        if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("存档目录不能为空。", nameof(directory));
        SavePath = Path.Combine(Path.GetFullPath(directory), "save.json");
        BackupPath = SavePath + ".bak";
        this.beforeCommit = beforeCommit;
    }

    public ActionResult Save(GameState state)
    {
        lock (gate)
        {
            string? temporaryPath = null;
            try
            {
                // Do not mutate the live timestamp until an entire snapshot has been committed.
                Validate(state);
                var snapshot = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(state, JsonOptions), JsonOptions)
                    ?? throw new InvalidDataException("存档状态为空。");
                snapshot.SavedAt = DateTimeOffset.UtcNow;
                Validate(snapshot);

                var primary = ReadCandidate(SavePath);
                var backup = ReadCandidate(BackupPath);
                if (primary.Unsupported || backup.Unsupported)
                    return ActionResult.Fail("存档使用了不支持的版本，未覆盖原文件。请保留文件并使用相应版本读取。");

                string payload = JsonSerializer.Serialize(snapshot, JsonOptions);
                var envelope = new SaveEnvelope
                {
                    FormatVersion = FormatVersion,
                    Payload = payload,
                    Sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)))
                };
                byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(envelope, JsonOptions);
                Directory.CreateDirectory(Path.GetDirectoryName(SavePath)!);
                // A unique temporary file is never treated as a loadable checkpoint.
                temporaryPath = SavePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write,
                           FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(flushToDisk: true);
                }

                beforeCommit?.Invoke();
                if (File.Exists(SavePath))
                {
                    // A damaged primary must never displace the valid backup recovered by Load().
                    string? destinationBackup = primary.State is not null || backup.State is null ? BackupPath : null;
                    File.Replace(temporaryPath, SavePath, destinationBackup);
                }
                else
                {
                    File.Move(temporaryPath, SavePath);
                }
                temporaryPath = null;
                state.SavedAt = snapshot.SavedAt;
                return ActionResult.Ok("已保存完整检查点。继续游戏将恢复最近成功的完整检查点。");
            }
            catch (Exception ex) when (IsRecoverable(ex))
            {
                return ActionResult.Fail($"保存失败，未确认新的检查点；已有完整存档保留。{ex.Message}");
            }
            finally
            {
                if (temporaryPath is not null)
                {
                    try { File.Delete(temporaryPath); }
                    catch (Exception ex) when (IsRecoverable(ex)) { /* An abandoned .tmp is never loaded. */ }
                }
            }
        }
    }

    public LoadResult Load()
    {
        lock (gate)
        {
            var primary = ReadCandidate(SavePath);
            if (primary.State is not null) return new(primary.State, "已恢复完整检查点。", true);
            // An incompatible version is not corruption and must not silently select a different game.
            if (primary.Unsupported) return new(null, primary.Message + " 原存档未改动。", false);
            var backup = ReadCandidate(BackupPath);
            if (backup.State is not null)
                return new(backup.State, "主存档缺失或损坏，已恢复上一份完整备份；进度可能回退。" + primary.Message, true);
            if (backup.Unsupported) return new(null, backup.Message + " 原备份未改动。", false);
            if (!primary.Exists && !backup.Exists) return new(null, "尚无存档。", false);
            return new(null, "存档及可用备份无法读取，原文件均未改动。" + primary.Message + " " + backup.Message, false);
        }
    }

    private static Candidate ReadCandidate(string path)
    {
        if (!File.Exists(path)) return new(null, "文件不存在。", false, false);
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(path));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("FormatVersion", out var version) ||
                !version.TryGetInt32(out int format)) throw new InvalidDataException("缺少存档格式版本。");
            if (format != FormatVersion) return new(null, UnsupportedVersion("存档格式", format), true, true);
            if (!root.TryGetProperty("Payload", out var payloadValue) || payloadValue.ValueKind != JsonValueKind.String ||
                !root.TryGetProperty("Sha256", out var hashValue) || hashValue.ValueKind != JsonValueKind.String)
                throw new InvalidDataException("存档封装不完整。");
            string payload = payloadValue.GetString()!;
            string storedHash = hashValue.GetString()!;
            byte[] expectedHash = Convert.FromHexString(storedHash);
            byte[] actualHash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
            if (expectedHash.Length != actualHash.Length || !CryptographicOperations.FixedTimeEquals(expectedHash, actualHash))
                throw new InvalidDataException("存档校验失败。");
            using var payloadDocument = JsonDocument.Parse(payload);
            if (payloadDocument.RootElement.ValueKind != JsonValueKind.Object ||
                !payloadDocument.RootElement.TryGetProperty("SchemaVersion", out var schema) ||
                !schema.TryGetInt32(out int schemaVersion)) throw new InvalidDataException("缺少游戏状态版本。");
            if (schemaVersion != FormatVersion) return new(null, UnsupportedVersion("游戏状态", schemaVersion), true, true);
            var state = JsonSerializer.Deserialize<GameState>(payload, JsonOptions)
                ?? throw new InvalidDataException("游戏状态为空。");
            Validate(state);
            return new(state, "", true, false);
        }
        catch (Exception ex) when (IsRecoverable(ex))
        {
            return new(null, $"{Path.GetFileName(path)}：{ex.Message}", true, false);
        }
    }

    private static bool IsRecoverable(Exception ex) => ex is not OutOfMemoryException and not StackOverflowException;

    private static string UnsupportedVersion(string label, int version) => version == 1
        ? $"旧版 {label} v1 与 M2 存档 v2 不兼容，不执行自动迁移。"
        : $"不支持{label}版本 {version}；当前 M2 只读取 v{FormatVersion}。";

    private static void Require([DoesNotReturnIf(false)] bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }

    private static void ListExists<T>(IEnumerable<T>? values, string label) =>
        Require(values is not null && values.All(value => value is not null), $"{label}为空或包含空对象。");

    private static HashSet<string> Ids<T>(IEnumerable<T> values, Func<T, string> getId, string label)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            string id = getId(value);
            Require(!string.IsNullOrWhiteSpace(id) && result.Add(id), $"{label}标识为空或重复：{id}。");
        }
        return result;
    }

    private static void References(IEnumerable<string>? values, HashSet<string> ids, string label)
    {
        ListExists(values, label);
        foreach (string value in values!) Require(ids.Contains(value), $"{label}引用不存在：{value}。");
    }

    private static void OptionalReference(string? value, HashSet<string> ids, string label)
    {
        if (!string.IsNullOrEmpty(value)) Require(ids.Contains(value), $"{label}引用不存在：{value}。");
    }

    private static void Cards(IEnumerable<CardKind>? cards)
    {
        ListExists(cards, "牌组");
        foreach (var card in cards!) Require(Enum.IsDefined(card), "牌组包含未知卡牌。");
    }

    private static void Validate(GameState state)
    {
        Require(state is not null, "游戏状态为空。");
        Require(state.SchemaVersion == FormatVersion, "不支持游戏状态版本。");
        Require(!string.IsNullOrWhiteSpace(state.Scenario), "验证场景标识为空。");
        Require(Enum.IsDefined(state.Phase), "经营阶段无效。");
        Require(state.Day >= 1 && state.Deadline >= 1 && (long)state.Day <= (long)state.Deadline + 1, "经营日或期限无效。");
        Require(state.CityIndex >= 1 && state.DailyExpense >= 0 && state.TransactionLimit >= 1 &&
                state.RequiredStations >= 0 && state.SortieSequence >= 0, "城市参数无效。");
        ListExists(state.Employees, "员工"); ListExists(state.Nodes, "节点"); ListExists(state.Edges, "连接");
        ListExists(state.Rules, "守则"); ListExists(state.Items, "实物"); ListExists(state.Plan, "准备方案");
        ListExists(state.LockedPlan, "锁定方案"); ListExists(state.Forecast, "扩张预告");
        ListExists(state.Ledger, "账目"); ListExists(state.Log, "日志"); ListExists(state.SeenHints, "提示记录");
        ListExists(state.LastDaySummary, "日结摘要");
        ListExists(state.SettlementTimeline, "日结阶段记录");
        Require(state.LogSequence >= 0 && state.SettlementTimeline.Count <= 7, "日结记录参数无效。");
        for (int index = 0; index < state.SettlementTimeline.Count; index++)
        {
            var stage = state.SettlementTimeline[index];
            Require(stage is not null && stage.Sequence == index + 1 && stage.Day >= 1 && stage.Day <= state.Day && !string.IsNullOrWhiteSpace(stage.Title), "日结阶段顺序或归属无效。");
            ListExists(stage.Events, "日结事件");
        }
        ListExists(state.PaidRewards, "奖励台账"); ListExists(state.MorningOperatingNodes, "晨间营业记录");
        Require(state.Stations is not null, "驻站记录为空。");
        var employees = Ids(state.Employees, e => e.Id, "员工");
        var nodes = Ids(state.Nodes, n => n.Id, "节点");
        var edges = Ids(state.Edges, e => e.Id, "连接");
        var rules = Ids(state.Rules, r => r.Id, "守则");
        var items = Ids(state.Items, i => i.Id, "实物");
        Ids(state.PaidRewards, id => id, "奖励台账");
        Cards(state.TeamDeck);
        foreach (var employee in state.Employees)
            Require(Enum.IsDefined(employee.Status) && employee.RecoverUntilDay >= 0, "员工状态无效。");
        var roomsByNode = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var node in state.Nodes)
        {
            ListExists(node.Rooms, "房间"); ListExists(node.InvasionSources, "入侵来源");
            var rooms = Ids(node.Rooms, r => r.Id, "房间"); roomsByNode[node.Id] = rooms;
            References(node.RuleIds, rules, "节点守则");
            OptionalReference(node.TargetItemId, items, "合同目标");
            OptionalReference(node.BlockadeEdgeId, edges, "封控边");
            Require(float.IsFinite(node.X) && float.IsFinite(node.Y) && node.BuiltDay >= 0 && node.MaintenanceCount >= 0,
                "节点状态无效。");
            foreach (var room in node.Rooms)
            {
                References(room.Neighbors, rooms, "房间通路");
                Require(room.RestUses >= 0 && room.SearchUses >= 0 && room.KnockUses >= 0 &&
                        room.ArchiveSearchRemaining >= 0, "房间次数或有限采样库存无效。");
            }
        }
        foreach (var edge in state.Edges)
        {
            Require(nodes.Contains(edge.From) && nodes.Contains(edge.To), "连接端点不存在。");
            OptionalReference(edge.RuleId, rules, "线路守则");
        }
        foreach (var rule in state.Rules)
        {
            Require(Enum.IsDefined(rule.Scope) && Enum.IsDefined(rule.Trigger), "守则枚举值无效。");
            Require(string.IsNullOrEmpty(rule.NodeId) || nodes.Contains(rule.NodeId) || edges.Contains(rule.NodeId), "守则归属不存在。");
            ListExists(rule.Candidates, "规则候选"); ListExists(rule.WrongCandidates, "错误候选");
            ListExists(rule.EvidenceRecords, "可靠证据与观察记录");
            var ruleRooms = roomsByNode.TryGetValue(rule.NodeId, out var localRooms)
                ? localRooms : roomsByNode.Values.SelectMany(roomIds => roomIds).ToHashSet(StringComparer.Ordinal);
            References(rule.RoomIds, ruleRooms, "守则作用房间");
            Require(rule.Candidates.Count > 0 && rule.CorrectCandidate >= 0 && rule.CorrectCandidate < rule.Candidates.Count &&
                    rule.WrongCandidates.All(i => i >= 0 && i < rule.Candidates.Count) && rule.LastCityAttemptDay >= 0 &&
                    rule.LastCityAttemptDay <= state.Day && rule.ContrastSamplesRemaining >= 0,
                "规则候选或确认记录无效。");
        }
        foreach (var item in state.Items)
        {
            Require(Enum.IsDefined(item.Location) && item.Size >= 0 && item.Charges >= 0 && item.MaxCharges >= 0 &&
                    item.AvailableDay >= 1 && item.SupportUsedDay >= 0 && item.SupportUsedDay <= state.Day, "实物状态无效。");
            if (item.Kind is "SealKit" or "SupplyCharge" or "SourceCase")
                Require(item.Charges <= item.MaxCharges, "工具或供应耗材余量超过容量。");
            if (item.Kind == WardClipCatalog.Kind)
                Require(item.Id == WardClipCatalog.Id && item.MaxCharges == WardClipCatalog.MaximumCharges &&
                        item.Charges <= item.MaxCharges, "护符夹实例或整局余量无效。");
            OptionalReference(item.NodeId, nodes, "实物地点"); OptionalReference(item.ContractNodeId, nodes, "实物合同");
            OptionalReference(item.InstalledNodeId, nodes, "实物安装地点");
            Require(item.Location == ItemLocation.Installed ? !string.IsNullOrEmpty(item.InstalledNodeId) :
                    string.IsNullOrEmpty(item.InstalledNodeId), "实物安装状态与地点不一致。");
            if (!string.IsNullOrEmpty(item.RoomId))
                Require(roomsByNode.TryGetValue(item.NodeId, out var rooms) && rooms.Contains(item.RoomId), "实物房间不存在。");
        }
        ValidateAssignments(state.Plan, nodes, edges, employees, items, rules);
        ValidateAssignments(state.LockedPlan, nodes, edges, employees, items, rules);
        foreach (var station in state.Stations)
            Require(nodes.Contains(station.Key) && employees.Contains(station.Value), "驻站地点或员工不存在。");
        References(state.MorningOperatingNodes, nodes, "晨间营业记录");
        foreach (var intent in state.Forecast)
            Require(nodes.Contains(intent.SourceId) && nodes.Contains(intent.TargetId) && edges.Contains(intent.EdgeId), "扩张预告引用不存在。");
        foreach (var entry in state.Ledger) Require(entry.Day >= 1 && entry.Day <= state.Day, "账目日期无效。");

        if (state.Field is not { } field) return;
        Require(nodes.Contains(field.NodeId) && roomsByNode[field.NodeId].Contains(field.RoomId), "现场地点或房间不存在。");
        Require(field.Finished || state.LockedPlan.Any(a => a.Id == field.AssignmentId), "现场锁定任务不存在。");
        Require(field.Round >= 1 && field.RoundLimit >= 1 && (long)field.Round <= (long)field.RoundLimit + 1 &&
                field.Actions >= 0 && field.MaxWill > 0 && field.Will <= field.MaxWill && field.Wards >= 0 &&
                field.Disturbance >= 0 && field.Capacity >= 0 && field.Ink >= 0 && field.Coins >= 0 &&
                field.LastConfirmationRound >= 0 && field.RevealCharges >= 0, "现场资源或轮次无效。");
        Require(field.MimicReference is null || Enum.IsDefined(field.MimicReference.Value), "模仿参考无效。");
        Cards(field.DrawPile); Cards(field.Hand); Cards(field.Discard); Cards(field.Exhausted);
        Require(field.HandSlots is not null, "固定手牌槽为空。");
        Require(field.HandSlots.Count == 5 || (state.Scenario == "Legacy" && field.HandSlots.Count == 0), "固定手牌槽数量无效。");
        foreach (var card in field.HandSlots)
            Require(card is null || Enum.IsDefined(card.Value), "固定手牌槽包含未知卡牌。");
        Require(field.NormalSearchCounts is not null, "本次出勤正常搜查次数记录为空。");
        foreach (var count in field.NormalSearchCounts)
            Require(roomsByNode[field.NodeId].Contains(count.Key) && count.Value >= 0, "本次出勤正常搜查的房间或次数无效。");
        OptionalReference(field.AssignedLightSupportId, items, "现场灯具支援");
        References(field.AcquiredItemIds, items, "现场取得物"); References(field.BroughtItemIds, items, "现场带入物");
        References(field.RevealedRuleIds, rules, "揭晓记录"); ListExists(field.TriggeredEffects, "组合触发记录");
        ListExists(field.Shadows, "敌影");
        foreach (var shadow in field.Shadows)
            Require(roomsByNode[field.NodeId].Contains(shadow.RoomId) && shadow.AnnouncedRound >= 0 && shadow.Damage >= 0, "敌影记录无效。");
    }

    private static void ValidateAssignments(List<Assignment> assignments, HashSet<string> nodes,
        HashSet<string> edges, HashSet<string> employees, HashSet<string> items, HashSet<string> rules)
    {
        Ids(assignments, a => a.Id, "任务");
        foreach (var assignment in assignments)
        {
            Require(Enum.IsDefined(assignment.Kind) && assignment.Cost >= 0, "任务类型或费用无效。");
            Require(nodes.Contains(assignment.NodeId) ||
                    (assignment.Kind == JobKind.Repair && edges.Contains(assignment.NodeId)) ||
                    (assignment.Kind == JobKind.Recruit && string.IsNullOrEmpty(assignment.NodeId)), "任务目标不存在。");
            References(assignment.EmployeeIds, employees, "任务员工"); References(assignment.ToolIds, items, "任务工具");
            References(assignment.Route, nodes, "任务路线"); References(assignment.KnownRuleIds, rules, "锁定规程");
            OptionalReference(assignment.TargetItemId, items, "工程目标实物");
            OptionalReference(assignment.SupportItemId, items, "任务支援实物");
        }
    }

    private sealed record Candidate(GameState? State, string Message, bool Exists, bool Unsupported);

    private sealed class SaveEnvelope
    {
        public int FormatVersion { get; set; }
        public string Payload { get; set; } = "";
        public string Sha256 { get; set; } = "";
    }
}
