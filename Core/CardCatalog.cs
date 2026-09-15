using System;
using System.Collections.Generic;
using System.Linq;

namespace Joy.Core;

/// <summary>Prototype content for the ten cards in requirements v0.3, P-CARD/P-MIM.</summary>
public sealed record CardDefinition(
    CardKind Id, string Name, CardKind Command, int ActionCost, string Description,
    string BehaviorTag, bool CanMimic, bool UpdatesReference = true, bool Exhausts = false);

public static class CardCatalog
{
    public static readonly IReadOnlyList<CardDefinition> All = new List<CardDefinition>
    {
        new(CardKind.Investigate, "调查", CardKind.Investigate, 1, "取得当前位置一条尚未取得的可靠线索。", "调查", true),
        new(CardKind.Search, "搜查", CardKind.Search, 1, "搜索当前容器；本房本出勤正常搜查 +1，可拒绝入包。", "搜查", true),
        new(CardKind.Knock, "敲门", CardKind.Knock, 1, "从当前允许应答的房门取得一份真实库存。", "敲门", true),
        new(CardKind.Light, "点灯", CardKind.Light, 1, "本轮照明，异动 −1。", "点灯", true),
        new(CardKind.Rest, "休息", CardKind.Rest, 1, "心志 +2；每个房间实例最多 2 次，与模仿共享。", "休息", true),
        new(CardKind.Ward, "护身", CardKind.Ward, 1, "护符 +1，上限 3。", "护身", true),
        new(CardKind.Sprint, "疾走", CardKind.Sprint, 1, "沿合法通路最多移动 2 格，逐格结算；保留模仿参考。", "移动", false, false),
        new(CardKind.Mimic, "模仿", CardKind.Mimic, 1, "在当前位置执行参考基础指令；不计正常搜查行为，每出勤耗尽。", "模仿", false, false, true),
        new(CardKind.Seal, "封装", CardKind.Seal, 1, "封装一件已经满足收容条件的背包物品。", "封装", false),
        new(CardKind.Banish, "驱离", CardKind.Banish, 1, "清除当前位置一名明确可驱离的普通敌影。", "驱离", false)
    };

    public static readonly IReadOnlyList<CardKind> StartingDeck = new[]
    {
        CardKind.Investigate, CardKind.Investigate, CardKind.Search, CardKind.Search, CardKind.Knock, CardKind.Light,
        CardKind.Rest, CardKind.Ward, CardKind.Sprint, CardKind.Mimic, CardKind.Seal, CardKind.Banish
    };

    public static CardDefinition Get(CardKind id) => All.FirstOrDefault(x => x.Id == id)
        ?? throw new ArgumentException($"未知卡牌：{id}", nameof(id));

    public static bool TryGet(CardKind id, out CardDefinition? card)
    {
        card = All.FirstOrDefault(x => x.Id == id);
        return card is not null;
    }
}
