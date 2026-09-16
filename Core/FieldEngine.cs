using System;
using System.Collections.Generic;
using System.Linq;

namespace Joy.Core;

/// <summary>
/// One authoritative action pipeline. This class has no Godot or file-system dependencies.
/// Inventory and room-instance counters live in GameState and survive return visits.
/// </summary>
public sealed class FieldEngine
{
    private readonly GameState state;
    private FieldState Field => state.Field ?? throw new InvalidOperationException("尚未进入现场。");
    private CityNode Site => state.Node(Field.NodeId);
    private RoomState Room => Site.Rooms.First(r => r.Id == Field.RoomId);
    public FieldEngine(GameState state) => this.state = state;

    public ActionResult Start(Assignment assignment)
    {
        if (state.Field is { Finished: false }) return ActionResult.Fail("已有未结束的现场出勤。");
        if (state.Field is { } previous && previous.AssignmentId == assignment.Id)
            return ActionResult.Fail("该出勤已有现场记录，请先完成归队结算，不能重新抽取开局。");
        var site = state.Nodes.FirstOrDefault(n => n.Id == assignment.NodeId);
        var entrance = site?.Rooms.FirstOrDefault(r => r.IsEntrance && r.Placed);
        if (site is null || entrance is null) return ActionResult.Fail("该地点没有可用入口。");
        if (assignment.Completed) return ActionResult.Fail("这次出勤已经结算。");
        if (assignment.ToolIds.Distinct().Count() != assignment.ToolIds.Count) return ActionResult.Fail("带入工具实例不能重复分配。");
        var tools = assignment.ToolIds.Select(id => state.Items.FirstOrDefault(i => i.Id == id)).ToList();
        if (tools.Any(i => i is null || i.Location != ItemLocation.Warehouse || i.AvailableDay > state.Day ||
            (i.ReservedBy is not null && i.ReservedBy != assignment.Id)))
            return ActionResult.Fail("带入工具必须仍在仓库，且不能被另一队持有。");
        if (tools.Sum(i => i!.Size) > 6) return ActionResult.Fail("带入工具超过背包容量。");
        var deck = (state.TeamDeck.Count > 0 ? state.TeamDeck : CardCatalog.StartingDeck).ToList();
        if (deck.Count < 10 || deck.Count > 16 || deck.Any(c => !Enum.IsDefined(c)) || deck.Count(c => c == CardKind.Mimic) > 1)
            return ActionResult.Fail("验证版牌组需要 10 至 16 张合法基础牌，模仿最多 1 张。");
        state.Field = new FieldState
        {
            AssignmentId = assignment.Id, NodeId = site.Id, RoomId = entrance.Id,
            SortieId = ++state.SortieSequence,
            DrawPile = deck, AssignedLightSupportId = assignment.SupportItemId
        };
        entrance.Entered = true;
        foreach (var tool in tools.Cast<ItemState>())
        {
            tool.Location = ItemLocation.Backpack;
            tool.OwnerId = assignment.Id;
            Field.BroughtItemIds.Add(tool.Id);
            if (tool.Kind == "RevealLens") Field.RevealCharges += Math.Max(0, tool.Charges);
        }
        Shuffle(Field.DrawPile);
        DrawHand();
        Log($"进入{site.Name}，每轮 3 行动、5 手牌，入口免费撤离；本层共 {Field.RoundLimit} 轮。");
        foreach (var rule in ApplicableRules().Where(r => r.Family == "Mirror" || r.Trigger == RuleTrigger.MirrorSearch))
        {
            rule.EvidenceFound = true;
            if (!rule.EvidenceRecords.Contains(rule.Clue)) rule.EvidenceRecords.Add(rule.Clue);
            Log("可靠历史记录（旧出勤，不计入本次搜查次数）：" + rule.Clue);
        }
        return ActionResult.Ok("出勤开始。");
    }

    public IEnumerable<ItemState> Backpack() => state.Items.Where(i =>
        i.Location == ItemLocation.Backpack && i.OwnerId == Field.AssignmentId);

    public IEnumerable<RuleState> ApplicableRules() => state.Rules.Where(r =>
        r.Scope == RuleScope.City || Site.RuleIds.Contains(r.Id) || r.NodeId == Site.Id)
        .OrderBy(r => r.Scope).ThenBy(r => r.Order).ThenBy(r => r.Id, StringComparer.Ordinal);

    public ActionPreview Preview(FieldAction action)
    {
        var validation = Validate(action);
        if (!validation.Success) return new ActionPreview(false, Cost(action), validation.Message);
        var parts = new List<string> { $"实际动作 / 对象 / 费用\n{ActionName(action)} · {Room.Name} · {Cost(action)} 行动", "基础效果" };
        var command = ResolveCommand(action);
        if (action.Kind == FieldActionKind.Move || command == CardKind.Sprint)
            parts.Add($"路径：{string.Join(" → ", PathFor(action).Select(id => Site.Rooms.First(r => r.Id == id).Name))}；逐格结算。");
        else if (action.Kind == FieldActionKind.Search || command is CardKind.Search or CardKind.Knock)
        {
            parts.Add(UsesArchive(action)
                ? $"搜索档案柜：有限寻物记录剩余 {Room.ArchiveSearchRemaining} 次；只定位第二容器，不发现或取得其中实物。"
                : action.AcceptItem ? "搜索尚未发现的真实库存；可容纳时入包，否则留在原房。" : "执行搜查并发现物品，拒绝入包；物品留在原房，不产生获得事件。");
            if (command != CardKind.Knock)
                parts.Add($"本房本出勤正常搜查次数：{NormalSearchCount(Room.Id)} → {NormalSearchCount(Room.Id) + (action.Card == CardKind.Mimic ? 0 : 1)}；模仿不计正常行为次数。");
        }
        else if (action.Kind == FieldActionKind.Pickup)
        {
            var item = state.Item(action.TargetId);
            parts.Add($"拾取 {item.Name}，占 {item.Size} 格；背包 {Backpack().Sum(i => i.Size)}/{Field.Capacity}。");
        }
        else if (action.Kind == FieldActionKind.Drop) parts.Add("放回当前房间；不恢复库存、发现奖励或实例次数。");
        else if (action.Kind == FieldActionKind.Seal || command == CardKind.Seal)
            parts.Add($"封装 {SealTarget(action)!.Name}；消耗 {SealingTool(SealTarget(action)!)!.Name} 1 次；物品身份和标签保留。");
        else if (action.Kind == FieldActionKind.SupportLight)
            parts.Add("消耗已分配灯具今日一次支援：本轮照明、异动 −1；不耗行动，不更新模仿参考。");
        else if (action.Kind == FieldActionKind.PlaceRoom) parts.Add("放置相邻房间，另消耗 1 墨水；首次进入得到 1 墨水。");
        else if (action.Kind == FieldActionKind.Reveal) parts.Add("消耗稀有揭晓 1 次，揭晓选中规则实例；不发普通确认奖励。");
        else if (command is not null) parts.Add(CardCatalog.Get(command.Value).Description);
        if (action.Card == CardKind.Mimic)
            parts.Add($"模仿参考：{CardCatalog.Get(command!.Value).Name}；不产生原行为标签，仍产生实际结果事件；本次出勤耗尽。");
        if (command == CardKind.Rest) parts.Add($"本房休息剩余 {2 - Room.RestUses} 次，普通和模仿共用。");
        if (command == CardKind.Ward) parts.Add($"护符 {Field.Wards}/3。");
        var projection = ProjectAction(action);
        parts.Add("已知会触发");
        parts.AddRange(projection.Triggers.Count > 0 ? projection.Triggers : new[] { "本步没有已确认且满足条件的规则后果。" });
        parts.Add("相关已知不触发");
        parts.AddRange(projection.Exemptions.Count > 0 ? projection.Exemptions : new[] { "没有需要说明的已知豁免。" });
        parts.Add("尚不确定");
        parts.AddRange(projection.Uncertainties.Count > 0 ? projection.Uncertainties : new[] { "当前已确认的本步条件均可核对。" });
        if (projection.Exact) parts.Add($"按本步公开条件预计：心志 {projection.Will}、护符 {projection.Wards}、异动 {projection.Disturbance}。" + (projection.Will <= 0 ? "心志归零即停止后续效果。" : ""));
        return new ActionPreview(true, Cost(action), string.Join("\n", parts), (projection.Exact && projection.Will <= 0) || action.Kind == FieldActionKind.Reveal);
    }

    /// <summary>UI confirmation is based exclusively on public facts, never on an unknown truth.</summary>
    public bool PreviewDanger(FieldAction action)
    {
        if (!Validate(action).Success) return false;
        var projection = ProjectAction(action);
        return projection.Exact && projection.Will <= 0;
    }

    public ActionResult Execute(FieldAction action)
    {
        var validation = Validate(action);
        if (!validation.Success) return validation;
        var command = ResolveCommand(action);
        var beforeLight = Field.Light;
        var mimicking = action.Kind == FieldActionKind.PlayCard && action.Card == CardKind.Mimic;
        var triggered = new HashSet<string>();
        Field.Actions -= Cost(action);
        Log($"行动前：{Room.Name}，心志 {Field.Will}，护符 {Field.Wards}，异动 {Field.Disturbance}，照明{(beforeLight ? "开启" : "关闭")}；支付 {Cost(action)} 行动。");
        if (action.Kind == FieldActionKind.PlayCard)
        {
            var card = CardCatalog.Get(action.Card!.Value);
            ConsumeHandSlot(action);
            (card.Exhausts ? Field.Exhausted : Field.Discard).Add(card.Id);
            if (card.UpdatesReference) Field.MimicReference = card.Command;
            Log(mimicking ? $"模仿 {CardCatalog.Get(command!.Value).Name}：本出勤耗尽，不复制原行为标签。" : $"使用 {card.Name}。");
        }
        if (action.Kind == FieldActionKind.Move || command == CardKind.Sprint)
        {
            foreach (var roomId in PathFor(action))
            {
                Field.RoomId = roomId;
                if (!Room.Entered) { Room.Entered = true; Field.Ink++; Log("首次进入该房间：墨水 +1。"); }
                Log($"移动至 {Room.Name}。");
                ApplyRules(new Events { Moved = true, Carrying = true, BeforeLight = beforeLight }, triggered);
                if (Field.Finished) break;
            }
        }
        else
        {
            var events = new Events { BeforeLight = beforeLight };
            if (action.Kind == FieldActionKind.Search || command is CardKind.Search or CardKind.Knock)
            {
                events.Searched = !mimicking && command != CardKind.Knock;
                events.Knocked = !mimicking && command == CardKind.Knock;
                int beforeCount = NormalSearchCount(Room.Id);
                if (events.Searched) Field.NormalSearchCounts[Room.Id] = beforeCount + 1;
                if (command == CardKind.Knock) Room.KnockUses++; else Room.SearchUses++;
                if (command != CardKind.Knock) Log($"本房本出勤正常搜查次数：{beforeCount} → {NormalSearchCount(Room.Id)}{(mimicking ? "；模仿不计行为次数" : "")}。");
                if (UsesArchive(action))
                {
                    Room.ArchiveSearchRemaining--;
                    Room.ContainerLocated = true;
                    Log("取得一次寻物记录：已定位第二容器。未发现或领取其中合同物，仍需一次真实搜查；本档案柜库存已消耗。");
                }
                else
                {
                    var item = SearchTarget(action)!;
                    item.Discovered = true;
                    Log($"发现 {item.Name}（占 {item.Size} 格）：{item.Description}");
                    if (action.AcceptItem && Fits(item)) { Take(item); events.Carrying = true; }
                    else Log(action.AcceptItem ? "背包容量不足，物品留在当前房间，未触发获得或携带。" : "拒绝入包；物品留在当前房间，未触发获得或携带。");
                }
            }
            else if (action.Kind == FieldActionKind.Pickup)
            {
                Take(state.Item(action.TargetId));
                events.Carrying = true;
            }
            else if (action.Kind == FieldActionKind.Drop)
            {
                var item = state.Item(action.TargetId);
                PutDown(item);
                Log($"将 {item.Name} 放回 {Room.Name}。");
            }
            else if (action.Kind == FieldActionKind.Seal || command == CardKind.Seal)
            {
                var item = SealTarget(action)!;
                var kit = SealingTool(item)!;
                kit.Charges--;
                item.Sealed = true;
                Log($"封装 {item.Name}；{kit.Name} 剩余 {kit.Charges} 次，物品身份不变。");
            }
            else if (action.Kind == FieldActionKind.PlaceRoom)
            {
                Site.Rooms.First(r => r.Id == action.TargetId).Placed = true;
                Field.Ink--;
                Log("房间已放置，墨水 −1；队伍仍在原房间。");
            }
            else if (action.Kind == FieldActionKind.Reveal)
            {
                var rule = state.Rule(action.TargetId);
                Field.RevealCharges--;
                var lens = Backpack().FirstOrDefault(i => i.Kind == "RevealLens" && i.Charges > 0);
                if (lens is not null) lens.Charges--;
                rule.Confirmed = true;
                rule.EvidenceFound = true;
                rule.RevealedBy = $"稀有揭晓 · 出勤 {Field.SortieId}";
                rule.ConfirmationRewardClaimed = true;
                Field.RevealedRuleIds.Add(rule.Id);
                Log($"稀有揭晓：{rule.Answer}；本来源不发普通确认奖励。");
            }
            else if (command == CardKind.Investigate)
            {
                var rule = ApplicableRules().First(r => !r.EvidenceFound);
                rule.EvidenceFound = true;
                Log($"可靠线索：{rule.Clue}。请据此判断候选；调查不会直接确认答案。");
            }
            else if (command == CardKind.Light || action.Kind == FieldActionKind.SupportLight)
            {
                if (action.Kind == FieldActionKind.SupportLight)
                {
                    var lamp = state.Item(Field.AssignedLightSupportId);
                    Field.LightSupportUsed = true;
                    lamp.SupportUsedDay = state.Day;
                    Field.TriggeredEffects.Add($"lamp:{lamp.Id}:day:{state.Day}");
                    Log($"{lamp.Name} 网点支援已使用：本次出勤和灯具今日额度同时消耗；模仿参考保持。");
                }
                Field.Light = true;
                Field.Disturbance = Math.Max(0, Field.Disturbance - 1);
                Log("点灯行为与灯光事件：本轮照明开启；异动 −1（最低 0）。");
            }
            else if (command == CardKind.Rest)
            {
                Room.RestUses++;
                Field.Will = Math.Min(Field.MaxWill, Field.Will + 2);
                events.Rested = !mimicking;
                Log($"休息：心志 +2（上限 {Field.MaxWill}）；本房共用额度 {Room.RestUses}/2。");
            }
            else if (command == CardKind.Ward)
            {
                Field.Wards = Math.Min(3, Field.Wards + 1);
                Log($"护符 +1，现有 {Field.Wards}/3。");
            }
            else if (command == CardKind.Banish)
            {
                Field.Shadows.Remove(Field.Shadows.First(s => s.RoomId == Field.RoomId && s.Banishable));
                Log("已驱离当前房间的普通敌影。");
            }
            ApplyRules(events, triggered);
            if (events.Searched || (mimicking && command == CardKind.Search)) RecordSearchObservation(beforeLight, triggered, events.Searched);
        }
        if (!Field.Finished) Log($"行动完成：心志 {Field.Will}，护符 {Field.Wards}，异动 {Field.Disturbance}，剩余 {Field.Actions} 行动。");
        return ActionResult.Ok(Field.Finished ? Field.EndReason : "行动完成。");
    }

    public ActionResult Confirm(string ruleId, int candidate)
    {
        if (state.Field is null || Field.Finished) return ActionResult.Fail("没有可确认规则的活动现场。");
        var rule = ApplicableRules().FirstOrDefault(r => r.Id == ruleId);
        if (rule is null) return ActionResult.Fail("该规则不属于当前现场。");
        if (rule.Confirmed) return ActionResult.Fail("规则已确认，不能重复领奖。");
        if (!rule.EvidenceFound && !rule.Observed) return ActionResult.Fail("需要先取得可靠线索或实际触发观察。");
        if (rule.LastCityAttemptDay == state.Day) return ActionResult.Fail("公司本日已判断过该条规则，现场本日不能重复提交。");
        if (Field.LastConfirmationRound == Field.Round) return ActionResult.Fail("本现场本轮已提交过一次判断。");
        if (candidate < 0 || candidate >= rule.Candidates.Count) return ActionResult.Fail("候选不存在。");
        if (rule.WrongCandidates.Contains(candidate)) return ActionResult.Fail("该候选已有错误记录，不能重复提交。");
        Field.LastConfirmationRound = Field.Round;
        if (candidate != rule.CorrectCandidate)
        {
            rule.WrongCandidates.Add(candidate);
            Log($"候选不成立：{rule.Candidates[candidate]}；异动 +2，保留错误记录。");
            AddDisturbance(2);
            return ActionResult.Ok(Field.Finished ? Field.EndReason : "候选不成立，本轮判断额度已使用。");
        }
        rule.Confirmed = true;
        if (!rule.ConfirmationRewardClaimed)
        {
            rule.ConfirmationRewardClaimed = true;
            Field.Will = Math.Min(Field.MaxWill, Field.Will + 1);
            Field.Coins++;
            Log("首次现场确认：心志 +1、零钱 +1；零钱撤离后归队，日终归库。");
        }
        Log($"确认守则：{rule.Answer}。公司证据在日终归档。");
        return ActionResult.Ok("守则确认成功。");
    }

    public ActionResult Withdraw()
    {
        if (state.Field is null || Field.Finished) return ActionResult.Fail("没有可撤离的活动现场。");
        if (!Room.IsEntrance) return ActionResult.Fail("请沿已有通路返回入口后免费撤离。");
        if (Field.Will <= 0) { Fail("心志归零"); return ActionResult.Fail(Field.EndReason); }
        Field.Finished = true;
        Field.Escaped = true;
        Field.EndReason = "成功撤离，等待归队交付与日终归档。";
        Log(Field.EndReason);
        return ActionResult.Ok(Field.EndReason);
    }

    public ActionPreview PreviewEndRound()
    {
        if (state.Field is null || Field.Finished) return new ActionPreview(false, 0, "当前没有活动现场。");
        int will = Field.Will, wards = Field.Wards, disturbance = Field.Disturbance;
        bool unconfirmed = ApplicableRules().Any(r => !r.Confirmed);
        // The current rule vocabulary has one round-end event: an actually ending light.
        // With no light, all remaining round-end effects are already public mechanics.
        bool unknown = unconfirmed && Field.Light;
        var lines = new List<string> { $"实际动作 / 对象 / 费用\n结束第 {Field.Round} 轮 · {Room.Name} · 0 行动", "基础效果", "未使用手牌弃置，下一轮按原顺序抽到固定五槽。", "已知会触发", "轮末顺序：敌影 → 灯光结束 → 异动增长与即时袭击 → 封门。" };
        foreach (var shadow in Field.Shadows.Where(s => s.RoomId == Field.RoomId))
        {
            if (shadow.AnnouncedRound >= Field.Round) { lines.Add("敌影本轮刚出现，仅预告；最早下一轮末攻击。"); continue; }
            if (wards > 0) { wards--; lines.Add("敌影：消耗 1 护符抵挡本次攻击。"); }
            else { will -= shadow.Damage; lines.Add($"敌影：心志 −{shadow.Damage}。"); }
            if (will <= 0) return new ActionPreview(true, 0, string.Join("\n", lines.Append("将心志归零，立即失败；不再执行后续效果。")), true);
        }
        lines.Add(Field.Light ? "灯光：本轮照明结束。" : "灯光：当前未开启照明。");
        if (Field.Light)
            foreach (var rule in ApplicableRules().Where(r => r.Confirmed && r.Trigger == RuleTrigger.LightEnds && (r.RoomIds.Count == 0 || r.RoomIds.Contains(Room.Id))))
            {
                if (unknown)
                    lines.Add($"已知条件：{rule.Answer}；{ConsequenceText(rule)}。只有结算到此项时仍有护符才能抵挡；未知项可能先消耗同一护符。");
                else PreviewRule(rule, ref will, ref wards, ref disturbance, lines);
                if (!unknown && will <= 0) return new ActionPreview(true, 0, string.Join("\n", lines.Append("将心志归零，立即失败。")), true);
            }
        lines.Add("异动：+1。");
        if (!unknown)
        {
            var change = ResolveDisturbance(disturbance, 1);
            disturbance = change.Value;
            will = Math.Max(0, will - change.Damage);
            if (change.Damage > 0)
                lines.Add("阈值袭击：心志 −3，异动回落到 4；护符不能抵挡。");
        }
        if (!unknown && will <= 0) lines.Add("将心志归零，立即失败。");
        else if (Field.Round >= Field.RoundLimit) lines.Add(Room.IsEntrance ? "最后撤离机会：现在可以先免费撤离；结束本轮会封门失败。" : "本轮结束封门失败；必须在结束前返回入口撤离。");
        else lines.Add($"封门：本轮结束后还剩 {Field.RoundLimit - Field.Round} 轮。");
        lines.Add("相关已知不触发");
        lines.Add(Field.Light ? "搜查、移动等行动行为不由结束本轮自动产生。" : "当前未点灯，不产生灯光结束事件；搜查、移动等行为也不发生。");
        lines.Add("尚不确定");
        if (unknown) lines.Add("尚有未确认守则；灯光阶段后异动总量和护符余量只能按条件判断。达到 8 时立即袭击、损 3 心志并回落到 4；不承诺最终净损失。");
        else lines.Add($"预计轮末心志 {Math.Max(0, will)}、护符 {wards}、异动 {disturbance}。" + (unconfirmed ? "尚有未确认规则，但本步没有产生其可能响应的灯光结束事件。" : ""));
        return new ActionPreview(true, 0, string.Join("\n", lines), (!unknown && will <= 0) || Field.Round >= Field.RoundLimit);
    }

    public ActionResult EndRound()
    {
        if (state.Field is null || Field.Finished) return ActionResult.Fail("当前没有可结束的现场回合。");
        Log($"第 {Field.Round} 轮结束。");
        foreach (var shadow in Field.Shadows.Where(s => s.RoomId == Field.RoomId && s.AnnouncedRound < Field.Round).ToList())
        {
            if (Field.Wards > 0) { Field.Wards--; Log("敌影攻击：消耗 1 护符抵挡。"); }
            else Damage(shadow.Damage, "敌影攻击");
            if (Field.Finished) return ActionResult.Ok(Field.EndReason);
        }
        bool wasLit = Field.Light;
        Field.Light = false;
        Log("本轮灯光结束。");
        ApplyRules(new Events { LightEnded = wasLit, BeforeLight = wasLit }, new HashSet<string>());
        if (Field.Finished) return ActionResult.Ok(Field.EndReason);
        AddDisturbance(1);
        if (Field.Finished) return ActionResult.Ok(Field.EndReason);
        if (Field.Round >= Field.RoundLimit) { Fail("楼层轮限耗尽，封门失败"); return ActionResult.Ok(Field.EndReason); }
        Field.Discard.AddRange(Field.Hand);
        Field.Hand.Clear();
        Field.Round++;
        Field.Actions = 3;
        DrawHand();
        // A fixed, public arrival schedule gives the player a full response window.
        if (Field.Round % 4 == 0 && Field.Shadows.Count == 0 && !Room.IsEntrance)
        {
            Field.Shadows.Add(new ShadowState { RoomId = Field.RoomId, AnnouncedRound = Field.Round });
            Log($"敌影预告：{Room.Name} 出現可驱离敌影，最早第 {Field.Round + 1} 轮末攻击该房间，伤害 2；可离开、驱离或用护符抵挡。");
        }
        Log($"第 {Field.Round} 轮：补至 3 行动，抽取 {Field.Hand.Count} 手牌；模仿参考保持。");
        return ActionResult.Ok("进入下一轮，可保存检查点。");
    }

    private ActionResult Validate(FieldAction action)
    {
        if (state.Field is null || Field.Finished) return ActionResult.Fail("当前没有活动现场。");
        if (Field.Will <= 0) return ActionResult.Fail("心志归零，不能继续行动。");
        if (!Enum.IsDefined(action.Kind)) return ActionResult.Fail("操作类型无效。");
        if (action.Kind == FieldActionKind.PlayCard)
        {
            if (action.Card is null || !Enum.IsDefined(action.Card.Value) || !Field.Hand.Contains(action.Card.Value)) return ActionResult.Fail("当前手牌中没有这张合法卡牌。");
            if (!CurrentHandSlots().Contains(action.Card)) return ActionResult.Fail("该牌不在当前五个基础手牌槽中。");
            if (action.HandSlotIndex < -1 || action.HandSlotIndex >= 5 ||
                (action.HandSlotIndex >= 0 && CurrentHandSlots()[action.HandSlotIndex] != action.Card))
                return ActionResult.Fail("该固定卡槽中没有选中的牌；空槽不会补入其他手牌。");
            if (action.Card == CardKind.Mimic)
            {
                if (Field.Exhausted.Contains(CardKind.Mimic)) return ActionResult.Fail("模仿在本次出勤已经耗尽。");
                if (Field.MimicReference is null) return ActionResult.Fail("模仿没有参考指令；疾走不会创建参考。");
                if (!Enum.IsDefined(Field.MimicReference.Value)) return ActionResult.Fail("模仿参考不是合法基础指令。");
                if (!CardCatalog.Get(Field.MimicReference.Value).CanMimic) return ActionResult.Fail("最近行动牌不在模仿白名单，不能回找旧参考。");
            }
        }
        if (Field.Actions < Cost(action)) return ActionResult.Fail($"行动不足：需要 {Cost(action)}，当前 {Field.Actions}。");
        var command = ResolveCommand(action);
        if (action.Kind == FieldActionKind.Move || command == CardKind.Sprint)
        {
            var path = PathFor(action);
            int limit = command == CardKind.Sprint ? 2 : 1;
            if (path.Count == 0 || path.Count > limit) return ActionResult.Fail($"需要一条 1 至 {limit} 格的已放置合法通路。");
            var previous = Room;
            foreach (var id in path)
            {
                var next = Site.Rooms.FirstOrDefault(r => r.Id == id && r.Placed);
                if (next is null || !previous.Neighbors.Contains(id)) return ActionResult.Fail("路径不相邻或房间尚未放置。");
                previous = next;
            }
        }
        else if (action.Kind == FieldActionKind.Search || command is CardKind.Search or CardKind.Knock)
        {
            if (command == CardKind.Knock && !Room.AllowsKnock) return ActionResult.Fail("当前房门不允许应答。");
            if (!UsesArchive(action) && SearchTarget(action) is null) return ActionResult.Fail("当前房间没有可搜查的已定位容器；已发现遗落物请使用拾取。");
        }
        else if (action.Kind == FieldActionKind.Pickup)
        {
            var item = state.Items.FirstOrDefault(i => i.Id == action.TargetId && i.NodeId == Field.NodeId && i.RoomId == Room.Id && i.Location == ItemLocation.Room && i.Discovered);
            if (item is null) return ActionResult.Fail("只能拾取当前位置已经发现的遗落物。");
            if (!Fits(item)) return ActionResult.Fail("背包容量不足；可先将已有物品放回当前房间。");
        }
        else if (action.Kind == FieldActionKind.Drop)
        {
            if (!Backpack().Any(i => i.Id == action.TargetId)) return ActionResult.Fail("该实物不在当前队伍背包。");
        }
        else if (action.Kind == FieldActionKind.Seal || command == CardKind.Seal)
        {
            var item = SealTarget(action);
            if (item is null) return ActionResult.Fail("请指定背包中尚未封装的异常物。");
            if (item.RequiresLightForSealing && !Field.Light) return ActionResult.Fail("该物品的公开收容条件需要本轮照明。");
            if (SealingTool(item) is null) return ActionResult.Fail(IsSourceTarget(item)
                ? "源头合同物需要已获配的专用箱，不能消耗便携隔离工具替代。" : "缺少有剩余次数且用途相符的隔离工具。");
        }
        else if (action.Kind == FieldActionKind.PlaceRoom)
        {
            if (!string.Equals(Site.Kind, "Hotel", StringComparison.OrdinalIgnoreCase)) return ActionResult.Fail("该现场采用固定房网，不使用旅馆放房。");
            var target = Site.Rooms.FirstOrDefault(r => r.Id == action.TargetId);
            if (target is null || target.Placed || !Room.Neighbors.Contains(target.Id)) return ActionResult.Fail("只能放置当前位置相邻的未放置房间。");
            if (Field.Ink < 1) return ActionResult.Fail("放置房间需要 1 墨水。");
        }
        else if (action.Kind == FieldActionKind.Reveal)
        {
            if (Field.RevealCharges < 1) return ActionResult.Fail("没有可用的稀有揭晓机会。");
            if (!ApplicableRules().Any(r => r.Id == action.TargetId && !r.Confirmed)) return ActionResult.Fail("请选择当前现场一条尚未确认的规则实例；无合法目标不消耗机会。");
        }
        else if (action.Kind == FieldActionKind.SupportLight)
        {
            var lamp = state.Items.FirstOrDefault(i => i.Id == Field.AssignedLightSupportId);
            if (Field.LightSupportUsed || lamp is null || lamp.SupportUsedDay == state.Day)
                return ActionResult.Fail("本次出勤没有尚未使用的灯具支援。");
            var supported = new CityEngine(state).CanUseLampSupport(new Assignment { Id = Field.AssignmentId, SupportItemId = Field.AssignedLightSupportId });
            if (!supported.Success) return supported;
        }
        else if (command == CardKind.Investigate && !ApplicableRules().Any(r => !r.EvidenceFound)) return ActionResult.Fail("当前没有尚未取得的可靠线索。");
        else if (command == CardKind.Rest && Room.RestUses >= 2) return ActionResult.Fail("该房间的 2 次休息额度已用尽，普通与模仿共用。");
        else if (command == CardKind.Ward && Field.Wards >= 3) return ActionResult.Fail("护符已达上限 3。");
        else if (command == CardKind.Banish && !Field.Shadows.Any(s => s.RoomId == Field.RoomId && s.Banishable)) return ActionResult.Fail("当前房间没有明确可驱离的敌影。");
        return ActionResult.Ok();
    }

    private int Cost(FieldAction action) => action.Kind switch
    {
        FieldActionKind.Search or FieldActionKind.Seal => 2,
        FieldActionKind.Drop or FieldActionKind.SupportLight => 0,
        FieldActionKind.Reveal => 1,
        _ => 1
    };

    private CardKind? ResolveCommand(FieldAction action) => action.Kind != FieldActionKind.PlayCard ? null
        : action.Card == CardKind.Mimic ? state.Field?.MimicReference : action.Card;

    private List<string> PathFor(FieldAction action)
    {
        if (action.Path.Count > 0) return action.Path.ToList();
        if (string.IsNullOrEmpty(action.TargetId)) return new List<string>();
        if (Room.Neighbors.Contains(action.TargetId)) return new List<string> { action.TargetId };
        if (ResolveCommand(action) == CardKind.Sprint)
            foreach (var neighbor in Site.Rooms.Where(r => r.Placed && Room.Neighbors.Contains(r.Id)))
                if (neighbor.Neighbors.Contains(action.TargetId)) return new List<string> { neighbor.Id, action.TargetId };
        return new List<string>();
    }

    private bool UsesArchive(FieldAction action) => Room.HasArchiveCabinet && Room.ArchiveSearchRemaining > 0 &&
        (string.IsNullOrEmpty(action.TargetId) || action.TargetId == Room.Id);

    private ItemState? SearchTarget(FieldAction action) => Room.HasArchiveCabinet && !Room.ContainerLocated ? null : state.Items.FirstOrDefault(i =>
        i.NodeId == Field.NodeId && i.RoomId == Room.Id && i.Location == ItemLocation.Room && !i.Discovered &&
        (string.IsNullOrEmpty(action.TargetId) || i.Id == action.TargetId));

    private ItemState? SealTarget(FieldAction action) => Backpack().FirstOrDefault(i => !i.Sealed && (i.Kind is "Anomaly" or "Lamp" || i.RequiresSealing) &&
        (string.IsNullOrEmpty(action.TargetId) || i.Id == action.TargetId));
    private bool IsSourceTarget(ItemState item) => state.Nodes.Any(n => n.IsSource && n.TargetItemId == item.Id);
    private ItemState? SealingTool(ItemState target) => IsSourceTarget(target) && state.Scenario == "M2"
        ? Backpack().FirstOrDefault(i => i.Kind == "SourceCase" && i.ContractNodeId == target.ContractNodeId && i.Charges > 0)
        : Backpack().FirstOrDefault(i => i.Kind == "SealKit" && i.Charges > 0);
    private int NormalSearchCount(string roomId) => Field.NormalSearchCounts.GetValueOrDefault(roomId);
    private bool Fits(ItemState item) => Backpack().Sum(i => i.Size) + item.Size <= Field.Capacity;

    private void Take(ItemState item)
    {
        item.Location = ItemLocation.Backpack;
        item.OwnerId = Field.AssignmentId;
        if (!Field.AcquiredItemIds.Contains(item.Id)) Field.AcquiredItemIds.Add(item.Id);
        if (item.Kind == "RevealLens") Field.RevealCharges += Math.Max(0, item.Charges);
        Log($"获得 {item.Name}，背包 {Backpack().Sum(i => i.Size)}/{Field.Capacity}；尚未撤离或交付。");
    }

    private void PutDown(ItemState item)
    {
        item.Location = ItemLocation.Room;
        item.NodeId = Field.NodeId;
        item.RoomId = Field.RoomId;
        item.OwnerId = "";
        item.ReservedBy = null;
        item.Discovered = true;
        if (item.Kind == "RevealLens") Field.RevealCharges = Math.Max(0, Field.RevealCharges - Math.Max(0, item.Charges));
    }

    private void ApplyRules(Events events, HashSet<string> triggered)
    {
        foreach (var rule in ApplicableRules())
        {
            if (Field.Finished) return;
            if (rule.RoomIds.Count > 0 && !rule.RoomIds.Contains(Field.RoomId)) continue;
            bool matches = rule.Trigger switch
            {
                RuleTrigger.SearchInDark => events.Searched && !events.BeforeLight,
                RuleTrigger.MirrorSearch => events.Searched && Room.IsMirrorRoom &&
                    (rule.MirrorVariantB ? NormalSearchCount(Room.Id) >= 2 : !events.BeforeLight),
                RuleTrigger.Knock => events.Knocked,
                RuleTrigger.CarryUnsealed => events.Carrying && Backpack().Any(i => i.RequiresSealing && !i.Sealed),
                RuleTrigger.MoveInDark => events.Moved && !events.BeforeLight,
                RuleTrigger.Rest => events.Rested,
                RuleTrigger.LightEnds => events.LightEnded,
                _ => false
            };
            if (!matches || !triggered.Add(rule.Id)) continue;
            rule.Observed = true;
            Log(rule.Confirmed
                ? $"已知守则产生后果：{rule.Answer}；{ConsequenceText(rule)}。"
                : "实际观察：本次操作出现反噬；原因尚未确认。");
            bool negative = rule.WillDamage > 0 || rule.DisturbanceGain > 0;
            if (negative && rule.Wardable && Field.Wards > 0)
            {
                Field.Wards--;
                Log("消耗 1 护符，取消该项可抵挡规则后果。");
                continue;
            }
            if (rule.WillDamage != 0) Damage(rule.WillDamage, rule.Confirmed ? "已知守则后果" : "观察到的反噬后果");
            if (Field.Finished) return;
            if (rule.DisturbanceGain != 0) AddDisturbance(rule.DisturbanceGain);
            // Rule consequences do not feed a second round of rule events.
        }
    }

    private void AddDisturbance(int amount)
    {
        var change = ResolveDisturbance(Field.Disturbance, amount);
        Log($"异动 {(amount >= 0 ? "+" : "")}{amount} → {Math.Max(0, Field.Disturbance + amount)}。");
        Field.Disturbance = change.Value;
        if (change.Damage > 0 && !Field.Finished)
        {
            Log("异动达到阈值，回落到 4。");
            Damage(change.Damage, "异动阈值袭击（不可用护符抵挡）");
        }
    }

    // Each individual increase resolves once; excess disturbance is discarded.
    // Execution and both previews share this rule to keep their outcomes aligned.
    private static (int Value, int Damage) ResolveDisturbance(int current, int amount)
    {
        int value = Math.Max(0, current + amount);
        return value >= 8 ? (4, 3) : (value, 0);
    }

    private void Damage(int amount, string source)
    {
        Field.Will = Math.Clamp(Field.Will - amount, 0, Field.MaxWill);
        Log($"{source}：心志 {(amount >= 0 ? "−" : "+")}{Math.Abs(amount)} → {Field.Will}。");
        if (Field.Will <= 0) Fail("心志归零，现场失败");
    }

    private void Fail(string reason)
    {
        Field.Finished = true;
        Field.Escaped = false;
        Field.EndReason = reason;
        foreach (var item in Backpack().ToList()) PutDown(item);
        Field.Coins = 0;
        Log($"{reason}。已记录证据保留；携带实物连同带入装备遗落在 {Room.Name}，可重新出勤取回；已消耗工具次数不退，员工恢复由日结办理。");
    }

    private void Shuffle(List<CardKind> cards)
    {
        for (int i = cards.Count - 1; i > 0; i--)
        {
            int j = state.Next(i + 1);
            (cards[i], cards[j]) = (cards[j], cards[i]);
        }
    }

    private void DrawHand()
    {
        Field.HandSlots = Enumerable.Repeat<CardKind?>(null, 5).ToList();
        for (int i = 0; i < 5; i++)
        {
            if (Field.DrawPile.Count == 0)
            {
                Field.DrawPile.AddRange(Field.Discard);
                Field.Discard.Clear();
                Shuffle(Field.DrawPile);
            }
            if (Field.DrawPile.Count == 0) break;
            Field.Hand.Add(Field.DrawPile[^1]);
            Field.HandSlots[i] = Field.DrawPile[^1];
            Field.DrawPile.RemoveAt(Field.DrawPile.Count - 1);
        }
    }

    private List<CardKind?> CurrentHandSlots()
    {
        if (Field.HandSlots.Count == 5 && Field.HandSlots.Where(c => c.HasValue).Select(c => c!.Value).SequenceEqual(Field.Hand))
            return Field.HandSlots.ToList();
        // Legacy fixtures and explicitly imported hands have no slot representation yet.
        return Field.Hand.Select(c => (CardKind?)c).Concat(Enumerable.Repeat<CardKind?>(null, 5)).Take(5).ToList();
    }

    private void ConsumeHandSlot(FieldAction action)
    {
        Field.HandSlots = CurrentHandSlots();
        int index = action.HandSlotIndex >= 0 ? action.HandSlotIndex : Field.HandSlots.FindIndex(c => c == action.Card);
        Field.HandSlots[index] = null;
        Field.Hand = Field.HandSlots.Where(c => c.HasValue).Select(c => c!.Value).ToList();
    }

    private void RecordSearchObservation(bool beforeLight, HashSet<string> triggered, bool normal)
    {
        var facts = $"现场记录：{Room.Name}，搜查前{(beforeLight ? "已照明" : "未照明")}，" +
            (normal ? $"本房本次第 {NormalSearchCount(Room.Id)} 次正常搜查；" : $"模仿搜查，正常行为次数维持 {NormalSearchCount(Room.Id)}；");
        bool reacted = triggered.Count > 0;
        facts += reacted ? "观察到反噬，具体护符与心志变化见本次流水。" : "本次未观察到反噬。";
        Log(facts);
        foreach (var rule in ApplicableRules().Where(r => r.Trigger == RuleTrigger.MirrorSearch && Room.IsMirrorRoom &&
                     (r.RoomIds.Count == 0 || r.RoomIds.Contains(Room.Id))))
        {
            rule.Observed = true;
            rule.EvidenceRecords.Add(facts);
            if (normal && !beforeLight && NormalSearchCount(Room.Id) == 1)
            {
                rule.HasContrastObservation = true;
                rule.ContrastTriggered = reacted;
            }
        }
    }

    private ActionProjection ProjectAction(FieldAction action)
    {
        var result = new ActionProjection { Will = Field.Will, Wards = Field.Wards, Disturbance = Field.Disturbance };
        var command = ResolveCommand(action);
        bool mimic = action.Kind == FieldActionKind.PlayCard && action.Card == CardKind.Mimic;
        bool search = action.Kind == FieldActionKind.Search || command == CardKind.Search;
        bool knock = command == CardKind.Knock;
        bool move = action.Kind == FieldActionKind.Move || command == CardKind.Sprint;
        bool rest = command == CardKind.Rest;
        bool acquiring = action.Kind == FieldActionKind.Pickup || ((search || knock) && action.AcceptItem && !UsesArchive(action));
        bool uncertainAcquisition = acquiring && action.Kind != FieldActionKind.Pickup;
        bool carriesUnsealed = Backpack().Any(i => i.RequiresSealing && !i.Sealed);
        if (action.Kind == FieldActionKind.Pickup)
        {
            var target = state.Item(action.TargetId);
            carriesUnsealed |= target.RequiresSealing && !target.Sealed;
        }
        if (command == CardKind.Light || action.Kind == FieldActionKind.SupportLight) result.Disturbance = Math.Max(0, result.Disturbance - 1);
        if (rest) result.Will = Math.Min(Field.MaxWill, result.Will + 2);
        if (command == CardKind.Ward) result.Wards = Math.Min(3, result.Wards + 1);

        var unknown = ApplicableRules().Where(r => !r.Confirmed).ToList();
        result.Exact = unknown.Count == 0;
        if (unknown.Count > 0)
        {
            result.Uncertainties.Add("仍有未确认守则。仅依据已有事实提示不确定性，不提供隐藏真值或最终净损失。");
            if (Field.Wards > 0)
                result.Uncertainties.Add("已知与未知后果可能按固定顺序共用护符；只有结算到该项时尚有护符，才取消该项可抵挡后果。");
            foreach (var rule in unknown.Where(r => r.EvidenceFound)) result.Uncertainties.Add("可靠记录：" + rule.Clue);
        }
        var applied = new HashSet<string>();
        var stepRooms = move ? PathFor(action).Select(id => Site.Rooms.First(r => r.Id == id)).ToList() : new List<RoomState> { Room };
        foreach (var stepRoom in stepRooms)
        {
            foreach (var rule in ApplicableRules().Where(r => r.Confirmed))
            {
                bool inRoom = rule.RoomIds.Count == 0 || rule.RoomIds.Contains(stepRoom.Id);
                bool related = rule.Trigger switch
                {
                    RuleTrigger.SearchInDark or RuleTrigger.MirrorSearch => search,
                    RuleTrigger.Knock => knock,
                    RuleTrigger.Rest => rest,
                    RuleTrigger.MoveInDark => move,
                    RuleTrigger.CarryUnsealed => move || acquiring,
                    _ => false
                };
                if (!related) continue;
                string reason;
                bool? matches;
                if (!inRoom) { matches = false; reason = "本步房间不在该已知规则作用范围"; }
                else switch (rule.Trigger)
                {
                    case RuleTrigger.SearchInDark:
                        matches = !mimic && !Field.Light;
                        reason = mimic ? "模仿不产生正常搜查行为" : "本次搜查前已有照明";
                        break;
                    case RuleTrigger.MirrorSearch:
                        matches = !mimic && stepRoom.IsMirrorRoom && (rule.MirrorVariantB ? NormalSearchCount(stepRoom.Id) + 1 >= 2 : !Field.Light);
                        reason = mimic ? "模仿不产生正常搜查行为，也不累计次数" : !stepRoom.IsMirrorRoom ? "该房不是镜面房" : rule.MirrorVariantB ? "本房本次出勤尚未达到第二次正常搜查" : "本次搜查前已有照明";
                        break;
                    case RuleTrigger.Knock:
                        matches = !mimic; reason = "模仿不产生原敲门行为"; break;
                    case RuleTrigger.Rest:
                        matches = !mimic; reason = "模仿不产生原休息行为，实际恢复仍发生"; break;
                    case RuleTrigger.MoveInDark:
                        matches = !Field.Light; reason = "移动前已有照明"; break;
                    case RuleTrigger.CarryUnsealed:
                        matches = uncertainAcquisition ? null : carriesUnsealed;
                        reason = "当前没有符合该规则条件的未封装携带物";
                        break;
                    default:
                        matches = false; reason = "本步不产生该规则事件"; break;
                }
                if (matches is null)
                {
                    result.Exact = false;
                    result.Uncertainties.Add($"{rule.Answer}：新容器中的实物尚未发现；只有实际取得符合条件的实物时才结算。{ConsequenceText(rule)}。");
                    continue;
                }
                if (!matches.Value)
                {
                    result.Exemptions.Add($"{stepRoom.Name} · {rule.Answer}：{reason}。");
                    continue;
                }
                if (!applied.Add(rule.Id))
                {
                    result.Exemptions.Add($"{stepRoom.Name} · 同一规则实例在本次疾走前一格已触发，本格不重复。");
                    continue;
                }
                if (!result.Exact)
                {
                    result.Triggers.Add($"{stepRoom.Name} · 如仍存活并结算到此项：{rule.Answer}；{ConsequenceText(rule)}。按全城 → 线路 → 地方和内容固定顺序处理。");
                }
                else
                {
                    PreviewRule(rule, ref result.Will, ref result.Wards, ref result.Disturbance, result.Triggers);
                    if (result.Will <= 0) return result;
                }
            }
        }
        return result;
    }

    private string ActionName(FieldAction action) => action.Kind == FieldActionKind.PlayCard && action.Card is { } card
        ? CardCatalog.Get(card).Name : action.Kind switch
        {
            FieldActionKind.Move => "基础移动", FieldActionKind.Search => "徒手搜查", FieldActionKind.Pickup => "拾取",
            FieldActionKind.Drop => "放回", FieldActionKind.Seal => "基础封装", FieldActionKind.PlaceRoom => "放置房间",
            FieldActionKind.Reveal => "稀有揭晓", FieldActionKind.SupportLight => "网点免费点灯", _ => "操作"
        };

    private sealed class ActionProjection
    {
        public bool Exact;
        public int Will;
        public int Wards;
        public int Disturbance;
        public List<string> Triggers { get; } = new();
        public List<string> Exemptions { get; } = new();
        public List<string> Uncertainties { get; } = new();
    }

    private void PreviewRule(RuleState rule, ref int will, ref int wards, ref int disturbance, List<string> lines)
    {
        if ((rule.WillDamage > 0 || rule.DisturbanceGain > 0) && rule.Wardable && wards > 0)
        {
            wards--; lines.Add($"已知规则：{rule.Answer}；消耗 1 护符抵挡。"); return;
        }
        will = Math.Clamp(will - rule.WillDamage, 0, Field.MaxWill);
        lines.Add($"已知规则：{rule.Answer}；{ConsequenceText(rule)}。");
        if (will <= 0) return;
        var change = ResolveDisturbance(disturbance, rule.DisturbanceGain);
        disturbance = change.Value;
        if (change.Damage > 0)
        {
            will = Math.Max(0, will - change.Damage);
            lines.Add("规则引发即时异动袭击：心志 −3，异动回落到 4；护符不可抵挡。");
        }
    }

    private static string ConsequenceText(RuleState rule) =>
        $"心志 {(rule.WillDamage > 0 ? "−" : "+")}{Math.Abs(rule.WillDamage)}、异动 {(rule.DisturbanceGain >= 0 ? "+" : "")}{rule.DisturbanceGain}；{(rule.Wardable ? "负面后果可用护符抵挡" : "护符不可抵挡")}";

    private void Log(string message) => state.Record($"现场 {Field.Round}/{Field.RoundLimit} · {message}");

    private sealed class Events
    {
        public bool BeforeLight { get; init; }
        public bool Searched { get; set; }
        public bool Knocked { get; set; }
        public bool Carrying { get; set; }
        public bool Moved { get; init; }
        public bool Rested { get; set; }
        public bool LightEnded { get; init; }
    }
}
