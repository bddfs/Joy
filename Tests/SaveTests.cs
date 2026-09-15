using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Joy.Core;

namespace Joy.Tests;

public static class SaveTests
{
    public static void Run(Action<bool, string> check)
    {
        string testRoot = Path.Combine(Path.GetTempPath(), "JoySaveTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);
        try
        {
            CheckRoundTripAndCommit(testRoot, check);
            CheckBackupRecovery(testRoot, check);
            CheckVersionAndContentGuards(testRoot, check);
            CheckFirstSaveFailure(testRoot, check);
            M2SaveTests.Run(check);
        }
        finally
        {
            // Delete only this run's generated test directory beneath the OS temporary directory.
            string resolved = Path.GetFullPath(testRoot);
            string tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (resolved.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) &&
                Path.GetFileName(resolved).StartsWith("JoySaveTests-", StringComparison.Ordinal) && Directory.Exists(resolved))
                Directory.Delete(resolved, recursive: true);
        }
    }

    private static void CheckRoundTripAndCommit(string root, Action<bool, string> check)
    {
        string directory = Path.Combine(root, "round-trip");
        var store = new SaveStore(directory);
        check(!store.Exists && !store.Load().Success, "AC-31：无存档时继续入口可判定为不可用");
        var state = Fixture();
        check(store.Save(state).Success && state.SavedAt.HasValue, "AC-31：首存完整提交后更新保存时间");
        var loaded = store.Load();
        check(loaded.Success && loaded.State is not null && Json(state) == Json(loaded.State),
            "AC-18/31：完整状态往返保留牌序、库存、候选、计数、预留、账目与时间");
        check(loaded.State!.LockedPlan.Single().Paid && loaded.State.PaidRewards.SequenceEqual(state.PaidRewards) &&
              loaded.State.Items.Single(i => i.Id == "cargo").Location == ItemLocation.Backpack &&
              loaded.State.Items.Count == 2 && loaded.State.Money == 23,
            "AC-18：读取不再次扣费、付款或复制唯一实物");
        var randomControl = Clone(state);
        check(Enumerable.Range(0, 12).All(_ => randomControl.Next(97) == loaded.State.Next(97)),
            "AC-18：读取恢复实际 PRNG 进度，相同后续操作得到相同序列");

        byte[] originalBytes = File.ReadAllBytes(store.SavePath);
        DateTimeOffset? originalTime = state.SavedAt;
        state.Money += 8;
        state.PaidRewards.Add("source-contract");
        state.Items.Single(i => i.Id == "cargo").Location = ItemLocation.Delivered;
        state.Items.Single(i => i.Id == "cargo").OwnerId = "company";
        state.Field!.AcquiredItemIds.Clear();
        state.Field.Finished = true;
        state.Field.Escaped = true;
        state.LockedPlan[0].Completed = true;
        state.LockedPlan[0].Successful = true;
        state.Ledger.Add(new LedgerEntry { Day = 3, Category = "合同", Amount = 8, Balance = 31 });
        state.Next(29);
        state.LastCheckpoint = "出勤交付完成";
        var failedStore = new SaveStore(directory, () => throw new IOException("测试：持久化提交前中断"));
        check(!failedStore.Save(state).Success && state.SavedAt == originalTime,
            "AC-31：提交前失败不更新现场对象的保存时间");
        check(originalBytes.SequenceEqual(File.ReadAllBytes(store.SavePath)) &&
              !Directory.EnumerateFiles(directory, "*.tmp").Any(),
            "AC-31：提交前故障保留原始文件并清理临时文件");
        var old = store.Load().State!;
        check(old.Money == 23 && !old.PaidRewards.Contains("source-contract") &&
              old.Items.Single(i => i.Id == "cargo").Location == ItemLocation.Backpack &&
              old.RandomState != state.RandomState && old.Ledger.Count == 1,
            "AC-31：提交中断只恢复完整旧事务，付款/实物/随机/账目不部分提交");
        check(store.Save(state).Success && File.Exists(store.BackupPath), "AC-31：后续保存原子替换并保留一份备份");
        check(Json(store.Load().State!) == Json(state) && originalBytes.SequenceEqual(File.ReadAllBytes(store.BackupPath)),
            "AC-31：成功提交恢复完整新事务，备份为前一个完整状态");
        byte[] newBytes = File.ReadAllBytes(store.SavePath);
        byte[] backupBytes = File.ReadAllBytes(store.BackupPath);
        state.Money++;
        check(!failedStore.Save(state).Success && newBytes.SequenceEqual(File.ReadAllBytes(store.SavePath)) &&
              backupBytes.SequenceEqual(File.ReadAllBytes(store.BackupPath)),
            "AC-31：已有主档和备份时提交中断均不改写");
    }

    private static void CheckBackupRecovery(string root, Action<bool, string> check)
    {
        var store = new SaveStore(Path.Combine(root, "backup"));
        var state = Fixture();
        check(store.Save(state).Success, "备份测试：建立旧状态");
        state.Money = 30;
        check(store.Save(state).Success, "备份测试：建立新状态");
        byte[] backup = File.ReadAllBytes(store.BackupPath);
        // Keep well-formed JSON but alter the payload without its checksum.
        string originalEnvelope = File.ReadAllText(store.SavePath);
        using (var envelope = JsonDocument.Parse(originalEnvelope))
        {
            string payload = envelope.RootElement.GetProperty("Payload").GetString()!;
            File.WriteAllText(store.SavePath, JsonSerializer.Serialize(new
            {
                FormatVersion = 2, Payload = payload.Replace("\"Money\":30", "\"Money\":9999", StringComparison.Ordinal),
                Sha256 = envelope.RootElement.GetProperty("Sha256").GetString()
            }));
        }
        var recovered = store.Load();
        check(recovered.Success && recovered.State!.Money == 23 && recovered.Message.Contains("备份", StringComparison.Ordinal),
            "AC-23：校验失败回退有效备份，并明确提示进度回退");
        recovered.State!.Money = 25;
        check(store.Save(recovered.State).Success && backup.SequenceEqual(File.ReadAllBytes(store.BackupPath)) &&
              store.Load().State!.Money == 25,
            "AC-23：损坏主档后的保存保留有效备份，不把坏主档覆盖到备份");
        File.Delete(store.SavePath);
        check(store.Exists && store.Load().Success && store.Load().State!.Money == 23,
            "AC-23：主档缺失时仍识别并读取备份");
        File.WriteAllText(store.BackupPath, "{ broken");
        check(store.Exists && !store.Load().Success && store.Load().State is null,
            "AC-23：有档但全损坏时仍可提示错误，不伪装成没有存档");
    }

    private static void CheckVersionAndContentGuards(string root, Action<bool, string> check)
    {
        var store = new SaveStore(Path.Combine(root, "validation"));
        var state = Fixture();
        check(store.Save(state).Success, "存档校验测试：建立有效检查点");
        byte[] original = File.ReadAllBytes(store.SavePath);
        var changes = new (string Label, Action<GameState> Change)[]
        {
            ("重复实物 ID", s => s.Items.Add(new ItemState { Id = "cargo" })),
            ("重复员工 ID", s => s.Employees.Add(new Employee { Id = "employee-1" })),
            ("重复房间 ID", s => s.Nodes[1].Rooms.Add(new RoomState { Id = "entry" })),
            ("不存在的连接端点", s => s.Edges[0].To = "missing"),
            ("不存在的合同目标", s => s.Nodes[1].TargetItemId = "missing"),
            ("不存在的任务员工", s => s.LockedPlan[0].EmployeeIds.Add("missing")),
            ("不存在的现场房间", s => s.Field!.RoomId = "missing"),
            ("无效经营阶段", s => s.Phase = (DayPhase)999),
            ("无效员工状态", s => s.Employees[0].Status = (EmployeeStatus)999),
            ("无效物品枚举", s => s.Items[0].Location = (ItemLocation)999),
            ("无效卡牌枚举", s => s.Field!.Hand.Add((CardKind)999)),
            ("非法经营日", s => s.Day = 0),
            ("超出期限的日期", s => s.Day = s.Deadline + 2),
            ("越界规则候选", s => s.Rules[0].CorrectCandidate = 50),
            ("空集合", s => s.Items = null!),
            ("未知游戏状态版本", s => s.SchemaVersion = 3)
        };
        foreach (var entry in changes)
        {
            var invalid = Clone(state);
            entry.Change(invalid);
            check(!store.Save(invalid).Success && original.SequenceEqual(File.ReadAllBytes(store.SavePath)),
                $"AC-23：拒绝{entry.Label}，保留旧完整检查点");
        }
        // Content validation must run on Load as well, even when the file's checksum matches.
        var invalidPayload = Clone(state);
        invalidPayload.Day = 0;
        WriteEnvelope(store.SavePath, invalidPayload);
        check(!store.Load().Success, "AC-23：完整哈希不能使非法内容通过读取校验");
        var futureState = Clone(state);
        futureState.SchemaVersion = 3;
        WriteEnvelope(store.SavePath, futureState);
        byte[] futureBytes = File.ReadAllBytes(store.SavePath);
        check(!store.Load().Success && !store.Save(state).Success && futureBytes.SequenceEqual(File.ReadAllBytes(store.SavePath)),
            "AC-23：未知状态版本既不读取也不覆盖");
        // A valid old backup does not justify falling back from a newer primary format.
        WriteEnvelope(store.BackupPath, state);
        File.WriteAllText(store.SavePath, "{\"FormatVersion\":3,\"FutureState\":{\"Money\":999}}");
        futureBytes = File.ReadAllBytes(store.SavePath);
        byte[] oldBackup = File.ReadAllBytes(store.BackupPath);
        check(!store.Load().Success && store.Load().Message.Contains("版本", StringComparison.Ordinal) &&
              !store.Save(state).Success && futureBytes.SequenceEqual(File.ReadAllBytes(store.SavePath)) &&
              oldBackup.SequenceEqual(File.ReadAllBytes(store.BackupPath)),
            "AC-23：未知封装版本不回退旧备份、不覆盖任何存档");

        var legacy = Clone(state);
        legacy.SchemaVersion = 1;
        WriteEnvelope(store.SavePath, legacy, formatVersion: 1);
        byte[] legacyBytes = File.ReadAllBytes(store.SavePath);
        check(!store.Load().Success && store.Load().Message.Contains("不兼容", StringComparison.Ordinal) &&
              !store.Save(state).Success && legacyBytes.SequenceEqual(File.ReadAllBytes(store.SavePath)) &&
              oldBackup.SequenceEqual(File.ReadAllBytes(store.BackupPath)),
            "M2：旧 v1 封装明确不兼容，不迁移、不回退覆盖原存档");
        WriteEnvelope(store.SavePath, legacy);
        legacyBytes = File.ReadAllBytes(store.SavePath);
        check(!store.Load().Success && !store.Save(state).Success &&
              legacyBytes.SequenceEqual(File.ReadAllBytes(store.SavePath)),
            "M2：即使外层标成 v2，也不将 v1 游戏状态默认升级");
        WriteEnvelope(store.SavePath, state);
        WriteEnvelope(store.BackupPath, legacy, formatVersion: 1);
        legacyBytes = File.ReadAllBytes(store.BackupPath);
        check(store.Load().Success && !store.Save(state).Success &&
              legacyBytes.SequenceEqual(File.ReadAllBytes(store.BackupPath)),
            "M2：混入旧格式备份时禁止覆盖旧档，当前可读主档仍可继续读取");

        var permissive = new SaveStore(Path.Combine(root, "preparation"));
        var draft = Fixture();
        draft.Phase = DayPhase.Preparation;
        draft.Field = null;
        draft.Plan.Add(new Assignment { Id = "draft-a", Kind = JobKind.Survey, NodeId = "site", EmployeeIds = { "employee-1" } });
        draft.Plan.Add(new Assignment { Id = "draft-b", Kind = JobKind.Survey, NodeId = "site", EmployeeIds = { "employee-1" } });
        check(permissive.Save(draft).Success,
            "准备中的人力冲突可保存，由城市锁定校验提示，不误判为损坏存档");
    }

    private static void CheckFirstSaveFailure(string root, Action<bool, string> check)
    {
        string directory = Path.Combine(root, "first-failure");
        var store = new SaveStore(directory, () => throw new IOException("首存提交前故障"));
        var state = Fixture();
        check(!store.Save(state).Success && !store.Exists && state.SavedAt is null &&
              !Directory.EnumerateFiles(directory, "*.tmp").Any(),
            "AC-31：首存提交前故障不留下可误读的半份存档");
        string obstacle = Path.Combine(root, "not-a-directory");
        File.WriteAllText(obstacle, "file");
        check(!new SaveStore(obstacle).Save(state).Success && state.SavedAt is null,
            "AC-31：存档路径不可写时明确失败，不显示保存成功");
    }

    private static GameState Fixture()
    {
        return new GameState
        {
            SchemaVersion = 2, Scenario = "Legacy", Day = 3, Deadline = 5, Phase = DayPhase.Executing, Money = 23, RandomState = 73902847592843UL,
            SortieSequence = 4, LastCheckpoint = "旅馆 · 第 2 轮", EmergencySupportUsed = true,
            TeamDeck = { CardKind.Search, CardKind.Investigate, CardKind.Mimic, CardKind.Sprint },
            Employees =
            {
                new Employee { Id = "employee-1", Status = EmployeeStatus.Deployed },
                new Employee { Id = "employee-2", Status = EmployeeStatus.Recovery, RecoverUntilDay = 4 }
            },
            Nodes =
            {
                new CityNode { Id = "hq", IsHeadquarters = true },
                new CityNode
                {
                    Id = "site", TargetItemId = "cargo", RuleIds = { "rule-1" }, Built = true, BuiltDay = 2,
                    Rooms =
                    {
                        new RoomState { Id = "entry", IsEntrance = true, Entered = true, Neighbors = { "storage" } },
                        new RoomState { Id = "storage", RestUses = 1, SearchUses = 1, Neighbors = { "entry" } }
                    }
                }
            },
            Edges = { new CityEdge { Id = "edge-1", From = "hq", To = "site", RuleId = "rule-1" } },
            Rules =
            {
                new RuleState
                {
                    Id = "rule-1", NodeId = "site", Scope = RuleScope.Local, Trigger = RuleTrigger.SearchInDark,
                    Candidates = { "甲", "乙", "丙" }, CorrectCandidate = 1, WrongCandidates = { 0 },
                    EvidenceFound = true, Archived = true, Confirmed = true, ConfirmationRewardClaimed = true,
                    LastCityAttemptDay = 2
                }
            },
            Items =
            {
                new ItemState
                {
                    Id = "cargo", NodeId = "site", RoomId = "storage", Location = ItemLocation.Backpack,
                    OwnerId = "sortie-4", ReservedBy = "sortie-4", ContractNodeId = "site", Sealed = true, Required = true
                },
                new ItemState { Id = "tool", Kind = "Tool", Location = ItemLocation.Warehouse, Charges = 2, ReservedBy = "sortie-4" }
            },
            LockedPlan =
            {
                new Assignment
                {
                    Id = "sortie-4", Kind = JobKind.Manual, NodeId = "site", EmployeeIds = { "employee-1" },
                    ToolIds = { "tool" }, Route = { "hq", "site" }, KnownRuleIds = { "rule-1" }, Cost = 1, Paid = true
                }
            },
            PaidRewards = { "survey-site", "rule-1-first-confirmation" },
            Ledger = { new LedgerEntry { Day = 3, Category = "预付", Amount = -1, Balance = 23 } },
            Log = { "已预付并预留唯一工具。" }, SeenHints = { "first-investigation" },
            MorningOperatingNodes = { "site" },
            Field = new FieldState
            {
                AssignmentId = "sortie-4", NodeId = "site", RoomId = "entry", SortieId = 4, Round = 2,
                Actions = 3, Will = 9, Wards = 2, Disturbance = 5, Light = false, Coins = 1,
                LastConfirmationRound = 1, MimicReference = CardKind.Search,
                DrawPile = { CardKind.Search, CardKind.Light }, Hand = { CardKind.Sprint, CardKind.Seal },
                Discard = { CardKind.Investigate }, Exhausted = { CardKind.Mimic },
                AcquiredItemIds = { "cargo" }, BroughtItemIds = { "tool" }, RevealedRuleIds = { "rule-1" },
                TriggeredEffects = { "tool:first-search:4" },
                Shadows = { new ShadowState { RoomId = "storage", AnnouncedRound = 1 } }
            }
        };
    }

    private static string Json(GameState state) => JsonSerializer.Serialize(state);
    private static GameState Clone(GameState state) => JsonSerializer.Deserialize<GameState>(Json(state))!;

    private static void WriteEnvelope(string path, GameState state, int formatVersion = 2)
    {
        string payload = Json(state);
        File.WriteAllText(path, JsonSerializer.Serialize(new
        {
            FormatVersion = formatVersion, Payload = payload,
            Sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)))
        }));
    }
}
