using System.Collections.Generic;
using System.Linq;

namespace Joy.Core.GridPrototype;

public sealed record TalentNode(string Id, string HeroId, string Name, string Kind, string Description,
    IReadOnlyList<string> Prerequisites, int Cost, int Column, int Row, string UnlockCardId,
    IReadOnlyList<string> AffectedCards);

/// <summary>Three prerequisite chains per hero. Card access is derived from learned nodes.</summary>
public static class TalentCatalog
{
    private static TalentNode N(string hero, string step, string name, string kind, string description,
        string[] affected, string unlock = "")
    {
        int tier = step[1] - '0';
        return new(hero + "-" + step, hero, name, kind, description,
            tier == 1 ? System.Array.Empty<string>() : new[] { hero + "-" + step[0] + (tier - 1) },
            1, step[0] - 'a', tier, unlock, affected);
    }
    public static readonly IReadOnlyList<TalentNode> Nodes = new[]
    {
        N("mapper", "a1", "轻捷步伐", "stat", "行进距离 1 → 2 格；留守收入额外 +1。", new[]{"move"}),
        N("mapper", "a2", "远行规划", "stat", "疾走距离 3 → 4 格；留守收入再 +1。", new[]{"sprint"}),
        N("mapper", "a3", "探路疾行", "card", "解锁特色牌「探路疾行」：移动最多 3 格，并自动调查落点相邻的一处未读线索。", new[]{"trailblaze"}, "trailblaze"),
        N("mapper", "b1", "压制标记", "mechanic", "标定现在还使目标停顿一次敌方行动；留守收入额外 +2。", new[]{"mark"}),
        N("mapper", "b2", "协同破绽", "mechanic", "打出标定后，两名队员各自下一次攻击伤害 +1；不会叠加储存。", new[]{"mark"}),
        N("mapper", "b3", "联动标定", "card", "解锁特色牌「联动标定」：同时标定目标与相邻敌人，沿用压制和协同效果。", new[]{"chain-mark"}, "chain-mark"),
        N("mapper", "c1", "精确投射", "stat", "精准投射基础伤害 2 → 3；对标定目标仍额外 +1。", new[]{"shot"}),
        N("mapper", "c2", "留取活口", "mechanic", "精准投射命中已标定目标时，伤害最多将其压至 1 生命，便于后续收容。", new[]{"shot"}),
        N("mapper", "c3", "贯穿投射", "card", "解锁特色牌「贯穿投射」：沿横纵直线，对目标及其正后方一格的敌人分别造成 3 基础伤害。", new[]{"piercing-shot"}, "piercing-shot"),
        N("engineer", "a1", "远距布灯", "stat", "布灯距离 3 → 4 格，灯区持续 2 → 3 轮。", new[]{"light"}),
        N("engineer", "a2", "轻装线路", "stat", "轻装机动距离 2 → 3 格。", new[]{"dash"}),
        N("engineer", "a3", "随行灯", "card", "解锁特色牌「随行灯」：移动最多 2 格，并在落点建立照明区。", new[]{"mobile-light"}, "mobile-light"),
        N("engineer", "b1", "值守灯区", "mechanic", "照明区内的队员每次受到的敌方攻击伤害 −1；留守为留库异常恢复 1 次使用额度。", new[]{"light"}),
        N("engineer", "b2", "长明灯芯", "stat", "布灯和特色灯阵持续时间再 +1 轮。", new[]{"light","mobile-light","light-dome"}),
        N("engineer", "b3", "防护穹顶", "card", "解锁特色牌「防护穹顶」：在目标格建立灯区，并为半径 1 内队员各提供 3 防护。", new[]{"light-dome"}, "light-dome"),
        N("engineer", "c1", "电弧增幅", "stat", "电弧探针基础伤害 3 → 4。", new[]{"arc"}),
        N("engineer", "c2", "维护屏障", "mechanic", "应急维护在恢复生命后，额外为目标提供 2 防护；满生命时也能获得防护。", new[]{"repair"}),
        N("engineer", "c3", "连锁电弧", "card", "解锁特色牌「连锁电弧」：对目标及相邻可见敌人分别造成 2 基础伤害。", new[]{"chain-arc"}, "chain-arc"),
        N("warden", "a1", "交涉窗口", "stat", "收容与快速收容的目标生命上限 2 → 4；留守额外恢复全员 1 生命。", new[]{"capture","quick-capture"}),
        N("warden", "a2", "延伸封缄", "stat", "收容和快速收容的作用距离各 +1 格。", new[]{"capture","quick-capture"}),
        N("warden", "a3", "安抚协议", "card", "解锁特色牌「安抚协议」：造成 3 基础伤害但至少保留目标 1 生命，同时标定目标。", new[]{"pact"}, "pact"),
        N("warden", "b1", "持续束缚", "stat", "封缄束缚使目标停顿 1 → 2 次敌方行动；留守补充工具额外 +1。", new[]{"bind"}),
        N("warden", "b2", "熟练封装", "mechanic", "常驻收容牌行动费用 2 → 1，仍消耗真实封装次数。", new[]{"capture"}),
        N("warden", "b3", "封锁阵", "card", "解锁特色牌「封锁阵」：束缚目标格半径 1 内可见敌人，持续 2 次敌方行动。", new[]{"seal-circle"}, "seal-circle"),
        N("warden", "c1", "镇静强化", "stat", "镇静基础伤害 2 → 3，仍会标定存活目标。", new[]{"soothe"}),
        N("warden", "c2", "取证回收", "mechanic", "防护试探打开箱柜后，若货物位置足够，会自动拾取其中物资；仍获得 2 防护。", new[]{"probe"}),
        N("warden", "c3", "全队急救", "card", "解锁特色牌「全队急救」：花费 2 行动，为两名外勤各恢复 4 生命并提供 1 防护。", new[]{"team-aid"}, "team-aid")
    };
    public static IReadOnlyList<TalentNode> ForHero(string heroId) => Nodes.Where(n => n.HeroId == heroId).ToArray();
    public static TalentNode Node(string id) => Nodes.First(n => n.Id == id);
    public static bool IsCardUnlocked(HeroState hero, string cardId)
    {
        var unlock = Nodes.FirstOrDefault(n => n.UnlockCardId == cardId);
        return unlock is null || unlock.HeroId == hero.Id && hero.LearnedTalents.Contains(unlock.Id);
    }
}
