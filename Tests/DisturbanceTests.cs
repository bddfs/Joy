using System;
using System.Linq;
using System.Text.Json;
using Joy.Core;

namespace Joy.Tests;

public static class DisturbanceTests
{
    public static void Run(Action<bool, string> check)
    {
        CheckThresholdAndPreview(check);
        CheckIndependentConsequences(check);
        CheckWardBoundary(check);
        CheckWrongCandidate(check);
        CheckRoundEnd(check);
        CheckImmediateFailure(check);
        CheckUnknownIsolation(check);
    }

    private static void CheckThresholdAndPreview(Action<bool, string> check)
    {
        foreach (var sample in new (int Before, int Gain, int After, int Will)[] { (6, 1, 7, 12), (7, 1, 4, 9), (7, 2, 4, 9), (7, 9, 4, 9) })
        {
            var (state, engine) = Start(sample.Before);
            state.Rules.Add(Rule("threshold", RuleTrigger.SearchInDark, sample.Gain));
            string beforePreview = JsonSerializer.Serialize(state);
            var preview = engine.Preview(Search());
            check(preview.Allowed && !preview.RequiresConfirmation &&
                HasTotals(preview, sample.Will, 3, sample.After) && JsonSerializer.Serialize(state) == beforePreview,
                $"异动：{sample.Before}+{sample.Gain} 的已知预览正确且不改变状态");
            Need(engine.Execute(Search()));
            check(state.Field!.Disturbance == sample.After && state.Field.Will == sample.Will && state.Field.Wards == 3 && !state.Field.Finished,
                $"异动：{sample.Before}+{sample.Gain} 单次增量至多袭击一次，达到阈值回落到 4");
        }
    }

    private static void CheckIndependentConsequences(Action<bool, string> check)
    {
        var (state, engine) = Start(7);
        var first = Rule("first", RuleTrigger.SearchInDark, 2);
        var second = Rule("second", RuleTrigger.SearchInDark, 4); second.Order = 1;
        state.Rules.Add(first); state.Rules.Add(second);
        var preview = engine.Preview(Search());
        Need(engine.Execute(Search()));
        check(HasTotals(preview, 6, 3, 4) && state.Field!.Will == 6 && state.Field.Disturbance == 4 && first.Observed && second.Observed,
            "FR-RULE-07：同一动作的两个独立增量逐项检查，回落到 4 后再次加 4 仍会袭击");
    }

    private static void CheckWardBoundary(Action<bool, string> check)
    {
        foreach (bool wardable in new[] { true, false })
        {
            var (state, engine) = Start(7);
            var rule = Rule("ward-boundary", RuleTrigger.SearchInDark, 2); rule.Wardable = wardable;
            state.Rules.Add(rule);
            int will = wardable ? 12 : 9, wards = wardable ? 2 : 3, disturbance = wardable ? 7 : 4;
            var preview = engine.Preview(Search());
            Need(engine.Execute(Search()));
            check(HasTotals(preview, will, wards, disturbance) && state.Field!.Will == will && state.Field.Wards == wards && state.Field.Disturbance == disturbance,
                wardable ? "AC-14：护符取消原规则的异动增加，取消后不产生阈值袭击" : "AC-14：不可抵挡的原规则达到阈值，袭击不消耗护符");
        }
    }

    private static void CheckWrongCandidate(Action<bool, string> check)
    {
        foreach (int will in new[] { 12, 3 })
        {
            var (state, engine) = Start(7, will);
            var rule = Rule("candidate", RuleTrigger.SearchInDark, 0);
            rule.Confirmed = false; rule.EvidenceFound = true;
            state.Rules.Add(rule);
            Need(engine.Confirm(rule.Id, 0));
            check(state.Field!.Disturbance == 4 && state.Field.Will == will - 3 && state.Field.Wards == 3 &&
                state.Field.Finished == (will == 3) && state.Field.LastConfirmationRound == state.Field.Round &&
                rule.WrongCandidates.SequenceEqual(new[] { 0 }) && !rule.Confirmed && state.Field.Coins == 0,
                $"AC-35：心志 {will} 时错误候选使 7+2 回落到 4，保留错误记录和本轮配额");
            check(!engine.Confirm(rule.Id, 1).Success && !rule.Confirmed && state.Field.Coins == 0,
                "AC-13/35：错误候选后的失败或本轮配额阻止后续确认奖励");
        }
    }

    private static void CheckRoundEnd(Action<bool, string> check)
    {
        foreach (var sample in new (int Before, int LightGain, int After)[] { (7, 0, 4), (7, 2, 5), (7, 9, 5), (6, 1, 4) })
        {
            var (state, engine) = Start(sample.Before);
            if (sample.LightGain > 0)
            {
                state.Field!.Light = true;
                state.Rules.Add(Rule("light-end", RuleTrigger.LightEnds, sample.LightGain));
            }
            var preview = engine.PreviewEndRound();
            Need(engine.EndRound());
            check(preview.Allowed && !preview.RequiresConfirmation && HasTotals(preview, 9, 3, sample.After) &&
                state.Field!.Will == 9 && state.Field.Wards == 3 && state.Field.Disturbance == sample.After && !state.Field.Light && state.Field.Round == 2,
                $"AC-27：轮末先结算灯灭异动 +{sample.LightGain} 再增长 1，预览与实际一致");
        }
    }

    private static void CheckImmediateFailure(Action<bool, string> check)
    {
        foreach (int will in new[] { 1, 2, 3 })
        {
            var (state, engine) = Start(7, will);
            var healing = Rule("后续恢复不应执行", RuleTrigger.SearchInDark, -4);
            healing.WillDamage = -6; healing.Order = 1;
            state.Rules.Add(Rule("lethal-threshold", RuleTrigger.SearchInDark, 2));
            state.Rules.Add(healing);
            var preview = engine.Preview(Search());
            check(preview.RequiresConfirmation && engine.PreviewDanger(Search()) && HasTotals(preview, 0, 3, 4) && !preview.Text.Contains(healing.Answer),
                $"AC-13/26：心志 {will} 的动作阈值预览截断到 0，并停止后续恢复预测");
            Need(engine.Execute(Search()));
            check(state.Field!.Finished && state.Field.Will == 0 && state.Field.Disturbance == 4 && state.Field.Wards == 3 && !healing.Observed,
                $"AC-13：心志 {will} 被阈值袭击归零后不执行后续正面规则");

            (state, engine) = Start(7, will);
            state.Field!.Light = true;
            healing = Rule("后续灯灭恢复不应执行", RuleTrigger.LightEnds, -4);
            healing.WillDamage = -6; healing.Order = 1;
            state.Rules.Add(Rule("lethal-light-end", RuleTrigger.LightEnds, 2));
            state.Rules.Add(healing);
            var hand = state.Field.Hand.ToArray(); var slots = state.Field.HandSlots.ToArray();
            var draw = state.Field.DrawPile.ToArray(); var discard = state.Field.Discard.ToArray();
            var random = state.RandomState;
            preview = engine.PreviewEndRound();
            Need(engine.EndRound());
            check(preview.RequiresConfirmation && !preview.Text.Contains(healing.Answer) && state.Field.Finished && state.Field.Will == 0 &&
                state.Field.Disturbance == 4 && !healing.Observed && !state.Field.Light && state.Field.Round == 1 &&
                state.Field.Hand.SequenceEqual(hand) && state.Field.HandSlots.SequenceEqual(slots) && state.Field.DrawPile.SequenceEqual(draw) &&
                state.Field.Discard.SequenceEqual(discard) && state.RandomState == random,
                $"AC-13/27：心志 {will} 在灯灭阈值归零，停止恢复、普通异动增长和换轮抽牌");
        }

        var (lastState, lastEngine) = Start(7, 2);
        lastState.Field!.Round = lastState.Field.RoundLimit;
        var lastHand = lastState.Field.Hand.ToArray();
        var lastPreview = lastEngine.PreviewEndRound();
        Need(lastEngine.EndRound());
        check(lastPreview.RequiresConfirmation && HasTotals(lastPreview, 0, 3, 4) && lastState.Field.Will == 0 &&
            lastState.Field.Disturbance == 4 && lastState.Field.Finished && lastState.Field.EndReason.Contains("心志归零") &&
            !lastState.Field.EndReason.Contains("封门") && lastState.Field.Hand.SequenceEqual(lastHand),
            "AC-27：普通轮末阈值先致死，封门不得覆盖失败原因或继续换轮");
    }

    private static void CheckUnknownIsolation(Action<bool, string> check)
    {
        foreach (bool roundEnd in new[] { false, true })
        {
            var (first, firstEngine) = Start(roundEnd ? 6 : 7, 3);
            var (second, secondEngine) = Start(roundEnd ? 6 : 7, 3);
            foreach (var sample in new[] { (State: first, Gain: 0), (State: second, Gain: 9) })
            {
                sample.State.Field!.Light = roundEnd;
                var trigger = roundEnd ? RuleTrigger.LightEnds : RuleTrigger.SearchInDark;
                var hidden = Rule("hidden", trigger, sample.Gain);
                hidden.Confirmed = false; hidden.EvidenceFound = true; hidden.Scope = RuleScope.City;
                sample.State.Rules.Add(hidden);
                var known = Rule("known", trigger, 4); known.Wardable = true;
                sample.State.Rules.Add(known);
            }
            var firstPreview = roundEnd ? firstEngine.PreviewEndRound() : firstEngine.Preview(Search());
            var secondPreview = roundEnd ? secondEngine.PreviewEndRound() : secondEngine.Preview(Search());
            check(firstPreview == secondPreview && !firstPreview.RequiresConfirmation &&
                !firstPreview.Text.Contains("预计轮末心志") && !firstPreview.Text.Contains("按本步公开条件预计") &&
                (roundEnd || firstEngine.PreviewDanger(Search()) == secondEngine.PreviewDanger(Search())),
                $"AC-26/27：{(roundEnd ? "轮末" : "动作")}隐藏异动增量不同，不改变预览或危险确认");
            Need(roundEnd ? firstEngine.EndRound() : firstEngine.Execute(Search()));
            Need(roundEnd ? secondEngine.EndRound() : secondEngine.Execute(Search()));
            check(!first.Field!.Finished && second.Field!.Finished && second.Field.Will == 0 && second.Field.Disturbance == 4,
                "AC-26：预览隔离后仍按各自隐藏规则结算真实存活与失败结果");
        }
    }

    private static bool HasTotals(ActionPreview preview, int will, int wards, int disturbance) =>
        preview.Text.Contains($"心志 {will}、护符 {wards}、异动 {disturbance}");

    private static FieldAction Search() => new() { Kind = FieldActionKind.Search, AcceptItem = false };

    private static RuleState Rule(string id, RuleTrigger trigger, int gain) => new()
    {
        Id = id, NodeId = "site", Scope = RuleScope.Local, Trigger = trigger, WillDamage = 0, DisturbanceGain = gain,
        Wardable = false, Confirmed = true, Answer = id, Clue = "同一份公开可靠记录",
        CorrectCandidate = 1, Candidates = new() { "错误候选", "正确候选" }
    };

    private static (GameState, FieldEngine) Start(int disturbance, int will = 12)
    {
        var state = new GameState
        {
            RandomState = 314159,
            Nodes = new() { new CityNode { Id = "site", Name = "阈值测试现场", Rooms = new() { new RoomState { Id = "entry", Name = "入口", IsEntrance = true } } } },
            Items = new() { new ItemState { Id = "item", Name = "测试库存", NodeId = "site", RoomId = "entry" } }
        };
        var engine = new FieldEngine(state);
        Need(engine.Start(new Assignment { Id = "manual", Kind = JobKind.Manual, NodeId = "site" }));
        state.Field!.Disturbance = disturbance; state.Field.Will = will; state.Field.Wards = 3;
        return (state, engine);
    }

    private static void Need(ActionResult result)
    {
        if (!result.Success) throw new InvalidOperationException(result.Message);
    }
}
