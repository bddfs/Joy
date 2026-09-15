using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Joy.Core;

namespace Joy.Tests;

/// <summary>Persistence fixtures; real action execution and UI save-boundary selection belong to integration tests.</summary>
public static class M2SaveTests
{
    public static void Run(Action<bool, string> check)
    {
        string root = Path.Combine(Path.GetTempPath(), "JoyM2SaveTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            CheckSnapshots(root, check);
            CheckLatestCompleteCheckpoint(root, check);
            CheckValidation(root, check);
        }
        finally
        {
            string resolved = Path.GetFullPath(root);
            string tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (resolved.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) &&
                Path.GetFileName(resolved).StartsWith("JoyM2SaveTests-", StringComparison.Ordinal) && Directory.Exists(resolved))
                Directory.Delete(resolved, recursive: true);
        }
    }

    private static void CheckSnapshots(string root, Action<bool, string> check)
    {
        var cases = new (string Name, Action<GameState> Configure)[]
        {
            ("计划锁定及独立支援预留", state =>
            {
                state.Phase = DayPhase.Locked; state.Field = null;
                state.Item("lamp").SupportUsedDay = 0;
                state.LastCheckpoint = "第 3 日 · 计划锁定";
            }),
            ("轮界的固定空槽、A/B真值、观察与按房间次数", _ => { }),
            ("对照调查模板与有限采样", state =>
            {
                ConfigureEngineering(state, JobKind.ContrastSurvey, "", Array.Empty<string>());
                state.LockedPlan[0].NodeId = "hotel";
                state.LockedPlan[0].EmployeeIds = new() { "e1", "e2" };
                state.LockedPlan[0].SurveyTemplate = "first-dark-search";
                state.Rule("mirror").ContrastSamplesRemaining = 1;
                state.Rule("mirror").HasContrastObservation = false;
                state.Rule("mirror").ArchivedContrastObservation = false;
                state.Rule("mirror").ContrastTriggered = null;
            }),
            ("工具补充工程锁定的耗材及空工具预留", state =>
                ConfigureEngineering(state, JobKind.Supply, "kit", new[] { "kit", "supply" })),
            ("工具补充完成及次日可用", state =>
            {
                ConfigureEngineering(state, JobKind.Supply, "kit", new[] { "kit", "supply" });
                state.Item("kit").Charges = 1; state.Item("kit").AvailableDay = 4;
                state.Item("supply").Charges = 0; state.Item("supply").Location = ItemLocation.Consumed;
                CompleteEngineering(state, "耗材已消耗，隔离工具补入 1 次，次日可用。");
            }),
            ("总部补封装锁定及未封装暂存", state =>
                ConfigureEngineering(state, JobKind.HeadquartersSeal, "lamp", new[] { "kit" })),
            ("总部补封装完成及处置生效日", state =>
            {
                ConfigureEngineering(state, JobKind.HeadquartersSeal, "lamp", new[] { "kit" });
                state.Item("kit").Charges = 0; state.Item("lamp").Sealed = true;
                state.Item("lamp").AvailableDay = 4; state.Item("lamp").Disposition = "sealed-awaiting-next-preparation";
                CompleteEngineering(state, "灯具完成封装，次日准备阶段才可出售或安装。");
            }),
            ("灯具已用支援及实例日额度", state =>
            {
                state.Field!.Light = true; state.Field.LightSupportUsed = true;
                state.Item("lamp").SupportUsedDay = state.Day;
                state.LastCheckpoint = "第 3 日 · 支援点灯完整检查点";
            }),
            ("失败后的唯一遗落、已用次数与人员恢复", state =>
            {
                state.Field!.Finished = true; state.Field.Escaped = false; state.Field.Will = 0; state.Field.Coins = 0;
                foreach (string id in new[] { "kit", "cargo" })
                {
                    var item = state.Item(id); item.Location = ItemLocation.Room; item.NodeId = "hotel";
                    item.RoomId = "mirror-room"; item.OwnerId = ""; item.ReservedBy = null; item.Discovered = true;
                }
                state.Item("kit").Charges = 0;
                foreach (var employee in state.Employees.Take(2))
                { employee.Status = EmployeeStatus.Recovery; employee.RecoverUntilDay = 5; }
                state.LockedPlan[0].Completed = true; state.LockedPlan[0].Successful = false;
                state.LastCheckpoint = "第 3 日 · 失败出勤完整结算";
            }),
            ("施工研究占用及唯一研究对象", state =>
                ConfigureEngineering(state, JobKind.Research, "token", Array.Empty<string>())),
            ("日终摘要及尚待展示内容", state =>
            {
                CloseSortie(state); state.Day = 4; state.Phase = DayPhase.Preparation;
                state.LastDaySummary = new() { "带回：合同物已交付。", "支付：基本经营费 2。", "次日：补入工具可用。" };
                state.LastCheckpoint = "第 4 日 · 日结摘要待查看";
            }),
            ("交接后的灯具仍归当地且不复制", state =>
            {
                CloseSortie(state); state.Day = 5; state.Phase = DayPhase.Victory; state.SourceRecovered = true;
                state.Outcome = "单城验证交接完成"; state.LastCheckpoint = state.Outcome;
                state.Item("lamp").Disposition = "left-with-local-branch";
            })
        };

        for (int i = 0; i < cases.Length; i++)
        {
            var state = Fixture(); cases[i].Configure(state);
            var store = new SaveStore(Path.Combine(root, "snapshot-" + i));
            var saved = store.Save(state);
            check(saved.Success, $"AC-41 状态层：{cases[i].Name}可完整保存：{saved.Message}");
            if (!saved.Success) continue;
            // A fresh store instance simulates reopening the persistence layer, without reusing cached objects.
            var loaded = new SaveStore(Path.GetDirectoryName(store.SavePath)!).Load();
            check(loaded.Success && Json(loaded.State!) == Json(state), $"AC-41 状态层：{cases[i].Name}逐字段往返一致");
            if (!loaded.Success) continue;
            var control = Clone(state);
            check(Enumerable.Range(0, 16).All(_ => control.Next(101) == loaded.State!.Next(101)),
                $"AC-41 状态层：{cases[i].Name}后续同序列随机请求一致");
        }
    }

    private static void CheckLatestCompleteCheckpoint(string root, Action<bool, string> check)
    {
        string directory = Path.Combine(root, "latest-checkpoint");
        var store = new SaveStore(directory);
        var roundStart = Fixture();
        check(store.Save(roundStart).Success, "AC-41：建立完整轮界检查点");
        var live = Clone(roundStart);
        CompleteRewardSnapshot(live);
        var restoredStart = new SaveStore(directory).Load().State!;
        check(Json(restoredStart) == Json(roundStart) && restoredStart.Item("token").Location == ItemLocation.Room &&
              restoredStart.Item("kit").Charges == 1 && restoredStart.Field!.Actions == 3,
            "AC-41：本轮无较晚完整检查点时整体重做，不保留领取同时退还消耗");
        check(store.Save(live).Success, "AC-41：提交同一轮内更晚的完整奖励检查点");
        var rewardCheckpoint = Clone(live);
        live.Item("token").Location = ItemLocation.Sold; live.Money += 2;
        live.Field!.Actions = 0; live.Field.NormalSearchCounts["mirror-room"]++;
        live.Next(131);
        var restoredReward = new SaveStore(directory).Load().State!;
        check(Json(restoredReward) == Json(rewardCheckpoint) && restoredReward.Field!.Round == roundStart.Field!.Round &&
              restoredReward.Field.Actions == 1 && restoredReward.Item("kit").Charges == 0 &&
              restoredReward.Item("token").Location == ItemLocation.Backpack && restoredReward.PaidRewards.Contains("side-reward"),
            "AC-41：最近成功奖励检查点优先于轮初，之后未保存动作整体回退");
        byte[] current = File.ReadAllBytes(store.SavePath);
        byte[] backup = File.ReadAllBytes(store.BackupPath);
        var failingStore = new SaveStore(directory, () => throw new IOException("M2 提交前故障"));
        check(!failingStore.Save(live).Success && current.SequenceEqual(File.ReadAllBytes(store.SavePath)) &&
              backup.SequenceEqual(File.ReadAllBytes(store.BackupPath)) &&
              Json(new SaveStore(directory).Load().State!) == Json(rewardCheckpoint),
            "AC-41：更晚保存失败仍恢复已提交奖励，工具、固定槽、计数和台账不半写入");
    }

    private static void CheckValidation(string root, Action<bool, string> check)
    {
        var store = new SaveStore(Path.Combine(root, "m2-validation"));
        var valid = Fixture();
        check(store.Save(valid).Success, "M2：带合法空手牌槽的状态可保存");
        byte[] original = File.ReadAllBytes(store.SavePath);
        var mutations = new (string Name, Action<GameState> Mutate)[]
        {
            ("M2 缺少固定手牌槽", s => s.Field!.HandSlots.Clear()),
            ("固定槽数量错误", s => s.Field!.HandSlots.RemoveAt(0)),
            ("固定槽无效枚举", s => s.Field!.HandSlots[1] = (CardKind)999),
            ("搜查次数为负", s => s.Field!.NormalSearchCounts["mirror-room"] = -1),
            ("搜查房间不存在", s => s.Field!.NormalSearchCounts["missing"] = 1),
            ("守则作用房间不存在", s => s.Rule("mirror").RoomIds.Add("missing")),
            ("证据记录为空集合", s => s.Rule("mirror").EvidenceRecords = null!),
            ("有限对照采样为负", s => s.Rule("mirror").ContrastSamplesRemaining = -1),
            ("有限柜子库存为负", s => s.Node("hotel").Rooms[1].ArchiveSearchRemaining = -1),
            ("工具超过容量", s => s.Item("kit").Charges = 3),
            ("供应耗材超过容量", s => s.Item("supply").Charges = 2),
            ("工具容量为负", s => s.Item("kit").MaxCharges = -1),
            ("可用日非法", s => s.Item("kit").AvailableDay = 0),
            ("支援使用日在未来", s => s.Item("lamp").SupportUsedDay = s.Day + 1),
            ("安装地点不存在", s => s.Item("lamp").InstalledNodeId = "missing"),
            ("安装状态没有地点", s => s.Item("lamp").InstalledNodeId = ""),
            ("已回仓仍保留安装归属", s => s.Item("lamp").Location = ItemLocation.Warehouse),
            ("任务目标实物不存在", s => s.LockedPlan[0].TargetItemId = "missing"),
            ("任务支援实物不存在", s => s.LockedPlan[0].SupportItemId = "missing"),
            ("现场支援实物不存在", s => s.Field!.AssignedLightSupportId = "missing"),
            ("日结摘要为空集合", s => s.LastDaySummary = null!)
        };
        foreach (var mutation in mutations)
        {
            var invalid = Clone(valid); mutation.Mutate(invalid);
            check(!store.Save(invalid).Success && original.SequenceEqual(File.ReadAllBytes(store.SavePath)),
                $"AC-41 数据校验：拒绝{mutation.Name}并保留完整旧档");
        }
        var malformed = Clone(valid); malformed.Field!.NormalSearchCounts["missing"] = 7;
        string payload = Json(malformed);
        File.WriteAllText(store.SavePath, JsonSerializer.Serialize(new
        {
            FormatVersion = 2, Payload = payload,
            Sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)))
        }));
        check(!store.Load().Success, "AC-41：读档也验证新增字段，正确哈希不能掩盖非法引用");
    }

    private static void ConfigureEngineering(GameState state, JobKind kind, string target, string[] tools)
    {
        CloseSortie(state);
        state.Phase = DayPhase.Executing;
        var task = new Assignment
        {
            Id = "engineering-3", Kind = kind, NodeId = "hq", TargetItemId = target,
            EmployeeIds = new() { "e4" }, ToolIds = tools.ToList(), Route = new() { "hq" },
            Cost = kind == JobKind.Supply ? 3 : 0, Paid = true
        };
        state.LockedPlan.Add(task);
        foreach (string tool in tools) state.Item(tool).ReservedBy = task.Id;
        if (target.Length > 0) state.Item(target).ReservedBy = task.Id;
        state.Item("kit").Charges = kind == JobKind.Supply ? 0 : 1;
        if (kind == JobKind.HeadquartersSeal)
        {
            var lamp = state.Item("lamp"); lamp.Location = ItemLocation.Warehouse; lamp.InstalledNodeId = "";
            lamp.Sealed = false; lamp.Disposition = "stored-unsealed";
        }
        state.Employees.Single(e => e.Id == "e4").Status = EmployeeStatus.Construction;
        state.LastCheckpoint = "第 3 日 · 工程锁定";
    }

    private static void CompleteEngineering(GameState state, string summary)
    {
        state.LockedPlan[0].Completed = true; state.LockedPlan[0].Successful = true;
        foreach (var item in state.Items) item.ReservedBy = null;
        state.LastDaySummary = new() { summary };
        state.PaidRewards.Add("engineering-3-complete"); state.Phase = DayPhase.Settlement;
        state.LastCheckpoint = "第 3 日 · 工程完整结算";
    }

    private static void CloseSortie(GameState state)
    {
        foreach (var item in state.Items.Where(i => i.Location == ItemLocation.Backpack))
        { item.Location = item.Required ? ItemLocation.Delivered : ItemLocation.Warehouse; item.OwnerId = ""; item.ReservedBy = null; }
        state.Field = null; state.Plan.Clear(); state.LockedPlan.Clear();
    }

    private static void CompleteRewardSnapshot(GameState state)
    {
        var field = state.Field!;
        field.Actions = 1; field.HandSlots[0] = null; field.Hand.Remove(CardKind.Search); field.Discard.Add(CardKind.Search);
        field.NormalSearchCounts["mirror-room"] = 2;
        state.Node("hotel").Rooms[1].ContainerLocated = true;
        state.Node("hotel").Rooms[1].ArchiveSearchRemaining = 0;
        var token = state.Item("token"); token.Location = ItemLocation.Backpack; token.OwnerId = field.AssignmentId;
        token.Discovered = true; token.Sealed = true; field.AcquiredItemIds.Add(token.Id);
        state.Item("kit").Charges = 0; field.Coins += 2; state.PaidRewards.Add("side-reward");
        state.Next(131);
        state.LastCheckpoint = "第 3 日 · 第 2 轮完整奖励检查点";
    }

    private static GameState Fixture() => new()
    {
        SchemaVersion = 2, Scenario = "M2", PressureEnabled = true, Day = 3, Money = 14,
        Phase = DayPhase.Executing, RandomState = 6239045249857UL, SortieSequence = 4,
        LastCheckpoint = "第 3 日 · 旅馆第 2 轮轮界", LastDaySummary = new() { "第 2 日：旅馆建成，次日起营业。" },
        Employees = Enumerable.Range(1, 6).Select(i => new Employee { Id = "e" + i, Status = i < 3 ? EmployeeStatus.Deployed : EmployeeStatus.Standby }).ToList(),
        Nodes = new()
        {
            new CityNode { Id = "hq", IsHeadquarters = true },
            new CityNode
            {
                Id = "hotel", Built = true, BuiltDay = 2, TargetItemId = "cargo", RuleIds = new() { "mirror" },
                Rooms = new()
                {
                    new RoomState { Id = "entry", IsEntrance = true, Neighbors = new() { "mirror-room" } },
                    new RoomState { Id = "mirror-room", IsMirrorRoom = true, HasArchiveCabinet = true, ContainerLocated = true,
                        ArchiveSearchRemaining = 0, SearchUses = 1, Neighbors = new() { "entry" } }
                }
            },
            new CityNode { Id = "hospital", Built = true, BuiltDay = 1 }
        },
        Edges = new() { new CityEdge { Id = "hq-hotel", From = "hq", To = "hotel" }, new CityEdge { Id = "hotel-hospital", From = "hotel", To = "hospital" } },
        Rules = new()
        {
            new RuleState
            {
                Id = "mirror", NodeId = "hotel", Scope = RuleScope.Local, Trigger = RuleTrigger.MirrorSearch,
                Family = "mirror-resident", MirrorVariantB = true, RoomIds = new() { "mirror-room" },
                Candidates = new() { "暗处搜查", "第二次正常搜查", "暗处进入" }, CorrectCandidate = 1,
                EvidenceRecords = new() { "历史录像缺少第一次搜查。", "本次首次暗处搜查未观察到反噬。" },
                EvidenceFound = true, Archived = true, Observed = true, HasContrastObservation = true,
                ArchivedContrastObservation = true, ContrastTriggered = false, ContrastSamplesRemaining = 0,
                WrongCandidates = new() { 2 }, LastCityAttemptDay = 2
            }
        },
        Items = new()
        {
            new ItemState { Id = "kit", Kind = "SealKit", Location = ItemLocation.Backpack, NodeId = "hotel", RoomId = "entry",
                OwnerId = "sortie-4", ReservedBy = "sortie-4", Charges = 1, MaxCharges = 2, AvailableDay = 3 },
            new ItemState { Id = "supply", Kind = "SupplyCharge", Location = ItemLocation.Warehouse, Charges = 1, MaxCharges = 1 },
            new ItemState { Id = "lamp", Kind = "Lamp", Location = ItemLocation.Installed, InstalledNodeId = "hospital",
                AvailableDay = 3, SupportUsedDay = 3, Sealed = true, Disposition = "installed" },
            new ItemState { Id = "cargo", NodeId = "hotel", RoomId = "mirror-room", ContractNodeId = "hotel", Required = true,
                Location = ItemLocation.Backpack, OwnerId = "sortie-4", ReservedBy = "sortie-4", Sealed = true },
            new ItemState { Id = "token", NodeId = "hotel", RoomId = "mirror-room", Value = 2, Location = ItemLocation.Room }
        },
        LockedPlan = new()
        {
            new Assignment { Id = "sortie-4", Kind = JobKind.Manual, NodeId = "hotel", EmployeeIds = new() { "e1", "e2" },
                ToolIds = new() { "kit" }, TargetItemId = "cargo", SupportItemId = "lamp", KnownRuleIds = new() { "mirror" },
                Route = new() { "hq", "hotel" }, Cost = 1, Paid = true }
        },
        PaidRewards = new() { "survey:hospital" }, Ledger = new() { new LedgerEntry { Day = 3, Amount = -1, Balance = 14, Category = "预付" } },
        Stations = new() { ["hospital"] = "e3" }, MorningOperatingNodes = new() { "hospital" },
        TeamDeck = new() { CardKind.Search, CardKind.Light, CardKind.Sprint, CardKind.Mimic, CardKind.Seal },
        Field = new FieldState
        {
            AssignmentId = "sortie-4", NodeId = "hotel", RoomId = "entry", SortieId = 4, Round = 2,
            Actions = 3, Will = 10, Wards = 0, Light = true, Coins = 1, LastConfirmationRound = 1,
            HandSlots = new() { CardKind.Search, null, CardKind.Sprint, null, CardKind.Seal },
            Hand = new() { CardKind.Search, CardKind.Sprint, CardKind.Seal }, DrawPile = new() { CardKind.Light, CardKind.Investigate },
            Discard = new() { CardKind.Investigate }, Exhausted = new() { CardKind.Mimic }, MimicReference = CardKind.Search,
            NormalSearchCounts = new() { ["mirror-room"] = 1 }, AssignedLightSupportId = "lamp", LightSupportUsed = true,
            BroughtItemIds = new() { "kit" }, AcquiredItemIds = new() { "cargo" }, TriggeredEffects = new() { "lamp:day3:sortie4" }
        }
    };

    private static string Json(GameState state) => JsonSerializer.Serialize(state);
    private static GameState Clone(GameState state) => JsonSerializer.Deserialize<GameState>(Json(state))!;
}
