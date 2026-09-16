using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Joy.Core;

namespace Joy.Tests;

/// <summary>Second M2 combination: a finite physical tool converts ink into search protection.</summary>
public static class WardClipTests
{
    private const string ClipId = "ward-clip";

    public static void Run(Action<bool, string> check)
    {
        CheckAcquisition(check);
        CheckSearchBoundaries(check);
        CheckOtherActions(check);
        CheckPreviewAndConsequences(check);
        CheckPersistence(check);
        CheckFailureAndRecovery(check);
        CheckCityContracts(check);
        CheckRealRecoveryRoute(check);
    }

    private static void CheckAcquisition(Action<bool, string> check)
    {
        var (state, engine) = Start();
        var clip = state.Item(ClipId);
        check(state.Items.Count(i => i.Kind == "WardClip") == 1 && clip.Location == ItemLocation.Room &&
            clip.RoomId == "hotel-room2" && clip.Size == 1 && clip.Charges == 2 && clip.MaxCharges == 2 &&
            clip.Sealed && !clip.RequiresSealing && clip.Value == 0,
            "护符夹：新局提供一件安全封装、占一格、两次永久余量的可选实物");
        Step(state, engine, Move("hotel-room1")); Step(state, engine, Move("hotel-room2"));
        Need(engine.EndRound());
        string beforeTarget = Json(state);
        var skipNote = new FieldAction { Kind = FieldActionKind.Search, TargetId = ClipId };
        check(!engine.Preview(skipNote).Allowed && !engine.Execute(skipNote).Success && Json(state) == beforeTarget,
            "护符夹：指定装备 ID 不能绕过尚未发现的值班便条或省掉第一次搜查");
        Step(state, engine, Search());
        check(state.Item("safe-note").Location == ItemLocation.Backpack && !clip.Discovered,
            "护符夹：安全室第一次默认搜查仍取得原有便条，装备不是自动赠品");
        state.Field!.Wards = 0;
        int ink = state.Field.Ink;
        Need(engine.EndRound());
        string beforePreview = Json(state);
        var candidate = engine.Preview(Search());
        check(candidate.Allowed && candidate.Text.Contains(clip.Name) && candidate.Text.Contains("1 格") &&
            candidate.Text.Contains("2/2") && candidate.Text.Contains("墨水") && candidate.Text.Contains("模仿") &&
            candidate.Text.Contains("拒绝") && Json(state) == beforePreview && !clip.Discovered,
            "护符夹：取得前可读候选名称、容量、余量与拒绝选项，预览不提前发现或领取");
        Step(state, engine, Search(accept: false));
        check(clip.Discovered && clip.Location == ItemLocation.Room && clip.Charges == 2 &&
            state.Field.Wards == 0 && state.Field.Ink == ink,
            "护符夹：拒绝入包只发现唯一装备，不扣余量或提前得到能力");
        Need(engine.EndRound());
        int actions = state.Field.Actions;
        check(!engine.Execute(Search()).Success && state.Field.Actions == actions && clip.Charges == 2,
            "护符夹：已经发现的装备不能通过重复搜查重抽或重领");
        Step(state, engine, new() { Kind = FieldActionKind.Pickup, TargetId = ClipId });
        check(clip.Location == ItemLocation.Backpack && clip.Charges == 2 && state.Field.Wards == 0 && state.Field.Ink == ink,
            "护符夹：后续拾取取得同一实物，但拾取不触发搜查组合");

        (state, engine) = Start();
        Step(state, engine, Move("hotel-room1")); Step(state, engine, Move("hotel-room2"));
        Step(state, engine, Search());
        state.Field!.Wards = 0; ink = state.Field.Ink;
        Step(state, engine, Search());
        check(state.Item(ClipId).Location == ItemLocation.Backpack && state.Item(ClipId).Charges == 2 &&
            state.Field.Wards == 0 && state.Field.Ink == ink,
            "护符夹：搜查前尚未在包内，取得当次不回溯提供护符");

        (state, engine) = Start();
        Step(state, engine, Move("hotel-room1")); Step(state, engine, Move("hotel-room2"));
        Step(state, engine, Search());
        state.Field!.Capacity = engine.Backpack().Sum(i => i.Size);
        Step(state, engine, Search());
        check(state.Item(ClipId).Discovered && state.Item(ClipId).Location == ItemLocation.Room && state.Item(ClipId).Charges == 2,
            "护符夹：背包不足时发现后留在原房，不绕过真实容量");
    }

    private static void CheckSearchBoundaries(Action<bool, string> check)
    {
        foreach (bool asCard in new[] { false, true })
        {
            var (state, engine) = Equipped();
            var action = Search();
            if (asCard) { SetHand(state, CardKind.Search); action = Play(CardKind.Search); }
            Need(engine.Execute(action));
            check(state.Item(ClipId).Charges == 1 && state.Field!.Ink == 1 && state.Field.Wards == 0 && state.Field.Will == 12,
                $"护符夹：{(asCard ? "搜查牌" : "徒手搜查")}在档案柜正常生效，护符先生成再抵挡实际反噬");
            check(state.Node("hotel").Rooms.Single(r => r.Id == "hotel-room1").ArchiveSearchRemaining == 0 &&
                !state.Item("hotel-clock").Discovered && state.Field!.NormalSearchCounts["hotel-room1"] == 1,
                "护符夹：组合不替代正常计数、档案库存及合同物的后续搜查");
            check(state.Field!.MimicReference == (asCard ? CardKind.Search : null),
                "护符夹：组合自身不创建或覆盖模仿参考");
        }

        foreach (var sample in new (int Wards, int Ink, int Charges, string Name)[]
        { (1, 2, 2, "已有护符"), (3, 2, 2, "护符已满"), (0, 0, 2, "没有墨水"), (0, 2, 0, "装备耗尽") })
        {
            var (state, engine) = Equipped();
            state.Field!.Wards = sample.Wards; state.Field.Ink = sample.Ink; state.Item(ClipId).Charges = sample.Charges;
            Need(engine.Execute(Search()));
            check(state.Item(ClipId).Charges == sample.Charges && state.Field.Ink == sample.Ink &&
                state.Node("hotel").Rooms.Single(r => r.Id == "hotel-room1").ArchiveSearchRemaining == 0,
                "护符夹：" + sample.Name + "时仍可正常搜查，但不额外扣费");
        }

        {
            var (state, engine) = Equipped();
            state.Field!.Actions = 1;
            string before = Json(state);
            check(!engine.Preview(Search()).Allowed && !engine.Execute(Search()).Success && Json(state) == before,
                "护符夹：无效动作不会扣墨水、次数、行动或消耗真实库存");
        }
        {
            var (state, engine) = Equipped();
            state.Items.Add(new ItemState { Id = "clip-stacking-fixture", Kind = "WardClip", Location = ItemLocation.Backpack,
                OwnerId = state.Field!.AssignmentId, Charges = 2, MaxCharges = 2, Sealed = true });
            Need(engine.Execute(Search()));
            check(state.Items.Where(i => i.Kind == "WardClip").Sum(i => i.Charges) == 3 && state.Field.Ink == 1 && state.Field.Wards == 0,
                "护符夹：单次搜查至多转换一次，不因多件装备叠加护符或扣费");
        }
    }

    private static void CheckOtherActions(Action<bool, string> check)
    {
        {
            var (state, engine) = Equipped();
            SetHand(state, CardKind.Mimic); state.Field!.MimicReference = CardKind.Search;
            Need(engine.Execute(Play(CardKind.Mimic)));
            check(state.Item(ClipId).Charges == 2 && state.Field.Ink == 2 && state.Field.Wards == 0 &&
                state.Field.NormalSearchCounts.GetValueOrDefault("hotel-room1") == 0 && state.Field.Exhausted.Contains(CardKind.Mimic),
                "护符夹：模仿搜查不触发装备、不累计正常次数，但按原规则耗尽");
            check(state.Node("hotel").Rooms.Single(r => r.Id == "hotel-room1").ArchiveSearchRemaining == 0 &&
                !state.Item("hotel-clock").Discovered,
                "护符夹：模仿继续消耗同一档案柜库存，不复制合同物");
        }
        {
            var (state, engine) = Equipped();
            state.Node("hotel").Rooms.Single(r => r.Id == "hotel-room1").AllowsKnock = true;
            SetHand(state, CardKind.Knock);
            Need(engine.Execute(Play(CardKind.Knock)));
            check(state.Item(ClipId).Charges == 2 && state.Field!.Ink == 2 && state.Field.Wards == 0,
                "护符夹：敲门即使取得实际库存也不当成正常搜查");
        }
        {
            var (state, engine) = Equipped();
            var target = state.Item("hotel-clock"); target.Discovered = true;
            Need(engine.Execute(new() { Kind = FieldActionKind.Pickup, TargetId = target.Id }));
            check(target.Location == ItemLocation.Backpack && state.Item(ClipId).Charges == 2 && state.Field!.Ink == 2 && state.Field.Wards == 0,
                "护符夹：直接拾取已发现目标不触发装备");
        }
        {
            var (state, engine) = Equipped();
            Need(engine.Execute(new() { Kind = FieldActionKind.Drop, TargetId = ClipId }));
            Need(engine.Execute(Search()));
            check(state.Item(ClipId).Charges == 2 && state.Field!.Ink == 2 && state.Field.Will == 10,
                "护符夹：放下后不再保护，房间中的装备不能替代随身实物");
        }
    }

    private static void CheckPreviewAndConsequences(Action<bool, string> check)
    {
        var (a, ea) = Equipped(false); var (b, eb) = Equipped(true);
        string before = Json(a);
        var preview = ea.Preview(Search());
        check(preview == eb.Preview(Search()) && !preview.RequiresConfirmation && Json(a) == before,
            "护符夹：相同公开状态的 A/B 预览完全一致，预览不消耗装备或墨水");
        Need(ea.Execute(Search())); Need(eb.Execute(Search()));
        check(a.Item(ClipId).Charges == 1 && b.Item(ClipId).Charges == 1 && a.Field!.Ink == 1 && b.Field!.Ink == 1 &&
            a.Field.Wards == 0 && b.Field.Wards == 1,
            "护符夹：无反噬时也按公开触发条件付费，不能根据隐藏真值决定是否消耗");
        check(a.Rule("hotel-mirror").ContrastTriggered == true && b.Rule("hotel-mirror").ContrastTriggered == false &&
            a.Field!.Will == 12 && b.Field!.Will == 12,
            "护符夹：保护不抹去真实反噬观察，不会把未掉血当成未触发");

        (a, ea) = Equipped(); a.Rule("hotel-mirror").Confirmed = true; a.Field!.Will = 2;
        preview = ea.Preview(Search());
        check(preview.Allowed && !preview.RequiresConfirmation && !ea.PreviewDanger(Search()) &&
            preview.Text.Contains("心志 2") && preview.Text.Contains("护符 0"),
            "护符夹：已知预览纳入先生成护符再承受反噬，不能误报确定致死");
        Need(ea.Execute(Search()));
        check(a.Field.Will == 2 && !a.Field.Finished && a.Item(ClipId).Charges == 1,
            "护符夹：公开保护预览与实际低心志结算一致");

        (a, ea) = Equipped(); a.Rule("hotel-mirror").Confirmed = true;
        a.Rules.Add(new RuleState { Id = "clip-unwardable", NodeId = "hotel", Confirmed = true, Order = 10,
            Trigger = RuleTrigger.SearchInDark, Wardable = false, WillDamage = 2, Answer = "不可抵挡的后续反噬" });
        a.Field!.Will = 2;
        check(ea.Preview(Search()).RequiresConfirmation, "护符夹：一枚护符不能掩盖其后仍确定致死的公开后果");
        Need(ea.Execute(Search()));
        check(a.Field.Finished && a.Field.Will == 0 && a.Item(ClipId).Charges == 1 && a.Item(ClipId).Location == ItemLocation.Room,
            "护符夹：后续归零立即失败，已消耗能力不补回或复活队伍");
    }

    private static void CheckPersistence(Action<bool, string> check)
    {
        string directory = Path.Combine(Path.GetTempPath(), "JoyWardClipTests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (state, engine) = Equipped();
            Need(engine.Execute(Search()));
            var save = new SaveStore(directory); Need(save.Save(state));
            var loaded = new SaveStore(directory).Load();
            check(loaded.Success && loaded.State is not null && Json(loaded.State) == Json(state),
                "护符夹：独立存档实例完整恢复装备、墨水、观察、库存及组合记录");
            if (loaded.State is not { } restored) throw new InvalidOperationException(loaded.Message);
            var resumed = new FieldEngine(restored);
            Step(state, engine, Search()); Step(restored, resumed, Search());
            check(state.Item(ClipId).Charges == 0 && restored.Item(ClipId).Charges == 0 &&
                state.Field!.Ink == 0 && restored.Field!.Ink == 0 && state.Field.Will == restored.Field.Will &&
                state.Field.HandSlots.SequenceEqual(restored.Field.HandSlots) && state.RandomState == restored.RandomState,
                "护符夹：读取后同操作序列消耗第二次，结果及随机进度一致");
            Need(new SaveStore(directory).Save(restored));
            var exhausted = new SaveStore(directory).Load();
            check(exhausted.Success && exhausted.State!.Item(ClipId).Charges == 0 && exhausted.State.Field!.Ink == 0,
                "护符夹：读取已提交耗尽检查点不退次数或墨水");
            int charges = restored.Item(ClipId).Charges;
            restored.Item(ClipId).Charges = 3;
            check(!new SaveStore(directory).Save(restored).Success,
                "护符夹：存档拒绝超过装备最大容量的非法余量");
            restored.Item(ClipId).Charges = charges;
            check(new SaveStore(directory).Load().State!.Item(ClipId).Charges == 0,
                "护符夹：非法保存不能覆盖上一份完整耗尽状态");
            var oldContent = WorldFactory.Create();
            oldContent.Items.RemoveAll(i => i.Id == ClipId);
            var oldSave = new SaveStore(Path.Combine(directory, "before-ward-clip"));
            Need(oldSave.Save(oldContent));
            var oldLoad = oldSave.Load();
            check(oldLoad.Success && oldLoad.State is not null && oldLoad.State.SchemaVersion == 2 &&
                oldLoad.State.Items.All(i => i.Kind != "WardClip") && Json(oldLoad.State) == Json(oldContent),
                "护符夹：没有新装备的既有 v2 库存原样读取，不补发新实物或改写账目");
            restored.Item(ClipId).MaxCharges = 3;
            check(!new SaveStore(directory).Save(restored).Success,
                "护符夹：不能在存档中擅自提高冻结的两次容量");

        }
        finally
        {
            string resolved = Path.GetFullPath(directory);
            string tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (resolved.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) &&
                Path.GetFileName(resolved).StartsWith("JoyWardClipTests-", StringComparison.Ordinal) && Directory.Exists(resolved))
                Directory.Delete(resolved, recursive: true);
        }
    }

    private static void CheckFailureAndRecovery(Action<bool, string> check)
    {
        var (state, engine) = Equipped(); Need(engine.Execute(Search()));
        state.Field!.Will = 3; state.Field.Disturbance = 7;
        Need(engine.EndRound());
        check(state.Field.Finished && !state.Field.Escaped && state.Item(ClipId).Location == ItemLocation.Room &&
            state.Item(ClipId).RoomId == "hotel-room1" && state.Item(ClipId).Charges == 1 && state.Item(ClipId).Discovered,
            "护符夹：失败后唯一装备遗落且已经消耗的一次不退");
        var city = new CityEngine(state); Need(city.CompleteManual()); Need(city.SettleDay());
        Need(city.AddTask(JobKind.Manual, "hotel"));
        check(state.Plan.Single().ToolIds.Count == 0 && state.Plan.Single().EmployeeIds.All(id => id is not ("e1" or "e2")),
            "护符夹：下一经营日用另一队空配装重返，恢复中的原队员不参与");
        Need(city.LockDay()); engine = new FieldEngine(state); Need(engine.Start(state.LockedPlan.Single()));
        Step(state, engine, Move("hotel-room1"));
        Step(state, engine, new() { Kind = FieldActionKind.Pickup, TargetId = ClipId });
        Step(state, engine, new() { Kind = FieldActionKind.Pickup, TargetId = "seal-a" });
        check(state.Item(ClipId).Charges == 1 && state.Items.Count(i => i.Kind == "WardClip") == 1 &&
            state.Node("hotel").Rooms.Single(r => r.Id == "hotel-room1").ArchiveSearchRemaining == 0,
            "护符夹：重访取回原实物，装备与已用档案柜都不刷新");
        Step(state, engine, Move("hotel-entry")); Need(engine.Withdraw()); Need(city.CompleteManual()); Need(city.SettleDay());
        Need(city.AddTask(JobKind.Manual, "hotel")); Need(city.SetTaskTools(state.Plan.Single().Id, new[] { "seal-a", ClipId }));
        Need(city.LockDay()); engine = new FieldEngine(state); Need(engine.Start(state.LockedPlan.Single()));
        check(state.Item(ClipId).Charges == 1 && state.Item(ClipId).OwnerId == state.Field!.AssignmentId &&
            state.Field.BroughtItemIds.Contains(ClipId),
            "护符夹：归库后再配发仍保留剩余一次，没有按经营日或出勤重置");
    }

    private static void CheckCityContracts(Action<bool, string> check)
    {
        var state = WarehouseClip(); var city = new CityEngine(state);
        Need(city.AddTask(JobKind.Manual, "hotel"));
        Need(city.SetTaskTools(state.Plan.Single().Id, new[] { "seal-a", ClipId }));
        check(state.Plan.Single().ToolIds.Contains(ClipId), "护符夹：准备阶段可以显式配给手操队");
        string before = Json(state);
        check(!city.SetTaskTools(state.Plan.Single().Id, new[] { ClipId, ClipId }).Success && Json(state) == before,
            "护符夹：同一实物不能在配装里重复分配，失败修改恢复原计划");
        Need(city.LockDay());
        check(state.Item(ClipId).ReservedBy == state.LockedPlan.Single().Id &&
            !city.SetTaskTools(state.LockedPlan.Single().Id, Array.Empty<string>()).Success,
            "护符夹：锁定使用唯一实物预留，不能执行中移交装备");

        foreach (var kind in new[] { JobKind.Survey, JobKind.Recovery, JobKind.ContrastSurvey })
        {
            state = WarehouseClip(); city = new CityEngine(state);
            string node = kind == JobKind.ContrastSurvey ? "hotel" : "hospital";
            if (kind == JobKind.Recovery)
                foreach (var rule in state.Rules.Where(r => r.NodeId == node))
                { rule.Confirmed = true; rule.Archived = true; rule.EvidenceFound = true; }
            check(!city.AddTask(kind, node, tools: new[] { "seal-a", ClipId }).Success && state.Plan.Count == 0 && state.Item(ClipId).Charges == 2,
                "护符夹：" + kind + "委派不接受该手操组合，也不消耗次数或创建收益");
        }

        state = WarehouseClip(); city = new CityEngine(state);
        int money = state.Money;
        check(!city.ScheduleSupply(ClipId).Success && !city.ScheduleHeadquartersSeal(ClipId).Success &&
            !city.ScheduleResearch(ClipId).Success && !city.SellItem(ClipId).Success &&
            state.Plan.Count == 0 && state.Money == money && state.Item(ClipId).Charges == 2,
            "护符夹：不能补充、补封装、研究或出售，不产生未声明收入与成长");

        state = WarehouseClip(); state.Item(ClipId).AvailableDay = 2; city = new CityEngine(state);
        Need(city.AddTask(JobKind.Manual, "hotel"));
        check(!city.SetTaskTools(state.Plan.Single().Id, new[] { ClipId }).Success,
            "护符夹：遵守真实可用日期，不提前借用次日装备");
        state.Item(ClipId).AvailableDay = 1; state.Item(ClipId).Charges = 0;
        check(!city.SetTaskTools(state.Plan.Single().Id, new[] { ClipId }).Success,
            "护符夹：耗尽的装备不能作为可用手操工具配发");
    }

    private static void CheckRealRecoveryRoute(Action<bool, string> check)
    {
        // No injected cards, resources, positions, discoveries or equipment in this witness.
        var (state, engine) = Start(); var city = new CityEngine(state);
        Step(state, engine, Move("hotel-room1")); Step(state, engine, Search());
        bool reacted = state.Rule("hotel-mirror").ContrastTriggered == true;
        Need(engine.Confirm("hotel-mirror", reacted ? 0 : 1));
        check(reacted && state.Field!.Wards == 0 && !state.Item("hotel-clock").Discovered,
            "护符夹实际路线：从首次暗搜真实观察确认 A，仍须取得第二容器");
        Step(state, engine, Move("hotel-room2")); Step(state, engine, Search()); Step(state, engine, Search());
        check(state.Item(ClipId).Location == ItemLocation.Backpack && state.Item(ClipId).Charges == 2 && state.Field!.Wards == 0,
            "护符夹实际路线：支付绕路和两次真实搜查取得装备，取得当次无免费护符");
        Step(state, engine, Move("hotel-room1"));
        int will = state.Field!.Will; int ink = state.Field.Ink;
        Step(state, engine, Search());
        check(state.Item(ClipId).Charges == 1 && state.Field.Ink == ink - 1 && state.Field.Will == will &&
            state.Field.Wards == 0 && !state.Field.Light && state.Item("hotel-clock").Location == ItemLocation.Backpack,
            "护符夹实际路线：墨水和永久次数替代照明前置，正常搜查无伤取得合同物");
        check(!state.Field.Exhausted.Contains(CardKind.Mimic) && !state.Field.LightSupportUsed,
            "护符夹实际路线：保留唯一模仿及网点支援，不虚构已使用的卡牌收益");
        Step(state, engine, new() { Kind = FieldActionKind.Seal, TargetId = "hotel-clock" });
        Step(state, engine, Move("hotel-entry")); Need(engine.Withdraw()); Need(city.CompleteManual());
        check(state.Item(ClipId).Location == ItemLocation.Warehouse && state.Item(ClipId).Charges == 1 &&
            state.Item("hotel-clock").Location == ItemLocation.Warehouse && state.Item("hotel-clock").Sealed,
            "护符夹实际路线：成功撤离才归库，合同仍需真实封装和日终交付");
        Need(city.SettleDay());
        check(state.Item("hotel-clock").Location == ItemLocation.Delivered && state.Node("hotel").RecoveryPaid &&
            state.Item("seal-a").Charges == 1 && state.Item(ClipId).Charges == 1 && state.Day == 2 && state.Money == 18,
            "护符夹实际路线：完整交付只领取原合同与确认收入，背包装备保留实际余量");
        Need(city.AddTask(JobKind.Manual, "hotel")); Need(city.SetTaskTools(state.Plan.Single().Id, new[] { "seal-a", ClipId }));
        check(state.Plan.Single().ToolIds.Contains(ClipId) && state.Item(ClipId).Charges == 1,
            "护符夹实际路线：次日能再次配发同一件已消耗装备");
    }

    private static GameState WarehouseClip()
    {
        var state = WorldFactory.Create(413, false);
        var clip = state.Item(ClipId); clip.Location = ItemLocation.Warehouse; clip.Discovered = true;
        return state;
    }

    private static (GameState State, FieldEngine Engine) Start(bool variantB = false)
    {
        var state = WorldFactory.Create(413, variantB); var city = new CityEngine(state);
        Need(city.AddTask(JobKind.Manual, "hotel")); Need(city.LockDay());
        var engine = new FieldEngine(state); Need(engine.Start(state.LockedPlan.Single()));
        return (state, engine);
    }

    private static (GameState State, FieldEngine Engine) Equipped(bool variantB = false)
    {
        var state = WorldFactory.Create(413, variantB);
        state.Item(ClipId).Location = ItemLocation.Warehouse; state.Item(ClipId).Discovered = true;
        var city = new CityEngine(state);
        Need(city.AddTask(JobKind.Manual, "hotel", tools: new[] { "seal-a", ClipId })); Need(city.LockDay());
        var engine = new FieldEngine(state); Need(engine.Start(state.LockedPlan.Single()));
        Need(engine.Execute(Move("hotel-room1")));
        state.Field!.Wards = 0; state.Field.Ink = 2;
        return (state, engine);
    }

    private static void Step(GameState state, FieldEngine engine, FieldAction action)
    {
        int cost = action.Kind is FieldActionKind.Search or FieldActionKind.Seal ? 2 : action.Kind is FieldActionKind.Drop or FieldActionKind.SupportLight ? 0 : 1;
        if (state.Field!.Actions < cost) Need(engine.EndRound());
        Need(engine.Execute(action));
    }

    private static FieldAction Search(bool accept = true) => new() { Kind = FieldActionKind.Search, AcceptItem = accept };
    private static FieldAction Move(string room) => new() { Kind = FieldActionKind.Move, TargetId = room };
    private static FieldAction Play(CardKind card) => new() { Kind = FieldActionKind.PlayCard, Card = card, HandSlotIndex = 0 };
    private static void SetHand(GameState state, params CardKind[] cards)
    {
        state.Field!.Hand = cards.ToList();
        state.Field.HandSlots = cards.Select(c => (CardKind?)c).Concat(Enumerable.Repeat<CardKind?>(null, 5)).Take(5).ToList();
    }
    private static string Json(GameState state) => JsonSerializer.Serialize(state);
    private static void Need(ActionResult result)
    { if (!result.Success) throw new InvalidOperationException("护符夹测试前置失败：" + result.Message); }
}
