using System;
using System.Collections.Generic;
using System.Linq;

namespace Joy.Core.GridPrototype;

public static class Catalog
{
    public static readonly IReadOnlyList<HeroDef> Heroes = new[]
    {
        new HeroDef("mapper", "林舟", "测绘员", "80cbd0", "留守：每次归队额外收入 2；远行专精每级再 +1。", "远行", "行进可多走 1 格；二级疾走再 +1 格。留守收入每级 +1。", "协同", "标定额外使敌人停顿；二级标定使队伍下一次伤害 +1。留守额外收入 +2。"),
        new HeroDef("engineer", "许灯", "灯光工程师", "e8bb73", "留守：每次归队修复一段封闭道路；值守专精恢复留库异常 1 次。", "巡灯", "布灯范围 +1，持续时间 +1 轮；二级疾走 +1 格。留守照常修路。", "值守", "灯区内角色受到的攻击减少 1；二级持续照明再 +1 轮。留守恢复留库异常 1 次。"),
        new HeroDef("warden", "陈墨", "收容官", "b29cd9", "留守：全员归队恢复 3 生命，收容工具补充 1（上限 4）。", "交涉", "可收容生命不高于 4 的目标；二级收容范围 +1。留守恢复生命额外 +1。", "封锁", "束缚持续时间 +1 轮；二级收容消耗 1 行动。留守补充工具额外 +1。")
    };
    public static readonly IReadOnlyList<AssetDef> Assets = new[]
    {
        new AssetDef("bell", "回声铃", "声音会牵引听觉型敌影。保留其规律，就能把威胁引向别处。", "留库：每次归队触发接应，收入 +2，不耗次数。", "asset-bell", "item-clock"),
        new AssetDef("wardrobe", "折叠衣柜", "只能连接已经留下的入口锚点，无法通往未知空间。", "留库：本次外勤战利品容量 +1。", "asset-wardrobe", "node-hotel"),
        new AssetDef("pain", "食痛者", "吞食有限的伤痛，饱食后需要维护。", "留库：每次归队全员恢复 2 生命。", "asset-pain", "item-kit")
    };
    public static readonly IReadOnlyList<string> BasicIds = new[] { "move", "search", "pickup", "attack", "guard", "capture", "ring", "drop", "retreat", "wait", "end" };
    public static readonly IReadOnlyList<CardDef> Cards = new[]
    {
        new CardDef("move", "行进", "basic", 1, 1, "cell", "沿通路移动 1 格；不能穿过墙、敌人或队友。", "card-mimic"),
        new CardDef("search", "搜查", "basic", 1, 1, "interact", "调查相邻线索；打开箱柜；开启房门。", "card-investigate"),
        new CardDef("pickup", "拾取", "basic", 1, 1, "interact", "拾取相邻已发现的战利品或掉落物，占用货物位置。", "item-kit"),
        new CardDef("attack", "防身", "basic", 1, 1, "enemy", "对相邻目标造成 2 伤害；击败异常不会得到其收容能力。", "card-mimic"),
        new CardDef("guard", "防护", "basic", 1, 0, "self", "获得 2 防护，抵挡下一次敌方伤害；下轮开始清除。", "item-kit"),
        new CardDef("capture", "收容", "basic", 2, 1, "enemy", "已取得证据，目标生命 ≤2，且被标定、束缚或处于灯区：消耗 1 工具收容。", "item-kit"),
        new CardDef("ring", "敲响铃铛", "basic", 1, 1, "interact", "敲响相邻的现场铃铛，听觉型敌人向铃移动最多 2 格。", "item-clock"),
        new CardDef("drop", "放下货物", "basic", 0, 0, "self", "把最后一件货物放在脚下；拾回需重新支付行动。", "item-kit"),
        new CardDef("retreat", "整队撤离", "basic", 0, 0, "exit", "两名角色都在入口 1 格内时撤离，交付实际带回的成果并推进经营日。", "node-hq"),
        new CardDef("wait", "待命", "basic", 0, 0, "self", "放弃此角色本轮剩余行动。随后可打出结束回合。", "card-light"),
        new CardDef("end", "结束回合", "basic", 0, 0, "end", "执行敌人已显示的意图，然后为每人恢复 2 行动并抽 3 张牌。", "card-light"),
        new CardDef("survey", "勘察", "mapper", 1, 4, "interact", "远距离调查一处线索或打开箱柜，隔墙无效。", "card-investigate"),
        new CardDef("mark", "标定", "mapper", 1, 4, "enemy", "标定可见目标至下轮，使其受到的伤害 +1，并满足收容的控制条件。", "card-investigate"),
        new CardDef("sprint", "疾走", "mapper", 1, 3, "cell", "沿合法通路移动最多 3 格。", "card-mimic"),
        new CardDef("lure", "投声", "mapper", 1, 5, "cell", "在可见格制造声响，听觉型敌人向声源移动最多 2 格。", "item-clock"),
        new CardDef("shot", "精准投射", "mapper", 1, 4, "enemy", "对可见目标造成 2 伤害；标定目标额外 +1。", "card-investigate"),
        new CardDef("swap", "协同换位", "mapper", 1, 5, "ally", "与可见队友交换位置；不刷新行动。", "card-mimic"),
        new CardDef("light", "布灯", "engineer", 1, 3, "cell", "在可见格布置半径 1 的灯区，持续 2 轮；揭露并限制畏光敌人。", "card-light"),
        new CardDef("flash", "闪光压制", "engineer", 1, 3, "enemy", "造成 1 伤害并让目标停顿 1 次敌方行动。", "card-light"),
        new CardDef("barrier", "隔离屏障", "engineer", 1, 3, "ally", "使可见队友获得 4 防护。", "item-kit"),
        new CardDef("repair", "应急维护", "engineer", 1, 2, "ally", "为可见队友恢复 3 生命。", "item-kit"),
        new CardDef("dash", "轻装机动", "engineer", 1, 2, "cell", "沿合法通路移动最多 2 格。", "card-mimic"),
        new CardDef("arc", "电弧探针", "engineer", 1, 3, "enemy", "对可见目标造成 3 伤害。", "card-light"),
        new CardDef("bind", "封缄束缚", "warden", 1, 3, "enemy", "让可见目标停顿 1 次敌方行动，并满足收容的控制条件。", "item-kit"),
        new CardDef("quick-capture", "快速收容", "warden", 1, 2, "enemy", "满足收容条件时只花 1 行动，消耗 1 工具；仍需真实运输空间。", "item-kit"),
        new CardDef("probe", "防护试探", "warden", 1, 3, "interact", "取得可见线索或打开箱柜，并获得 2 防护。", "card-investigate"),
        new CardDef("soothe", "镇静", "warden", 1, 3, "enemy", "使可见目标损失 2 生命并被标定；保留其可收容身份。", "card-mimic"),
        new CardDef("heal", "急救", "warden", 1, 2, "ally", "为可见队友恢复 4 生命。", "item-kit"),
        new CardDef("escort", "护送", "warden", 1, 2, "cell", "沿通路移动最多 2 格，并获得 1 防护。", "card-mimic"),
        new CardDef("asset-bell", "异常 · 响铃", "asset", 0, 6, "cell", "消耗回声铃 1 次，牵引听觉型敌人；不消耗角色行动。", "item-clock"),
        new CardDef("asset-wardrobe", "异常 · 折叠门", "asset", 1, 0, "self", "消耗衣柜 1 次，将自己转移到入口附近的空格。", "node-hotel"),
        new CardDef("asset-pain", "异常 · 代受", "asset", 0, 0, "self", "消耗食痛者 1 次，队伍获得 4 点共享伤害吸收。", "item-kit")
    };
    public static CardDef Card(string id) => Cards.First(c => c.Id == id);
    public static HeroDef Hero(string id) => Heroes.First(h => h.Id == id);
    public static AssetDef Asset(string id) => Assets.First(a => a.Id == id);
    public static IReadOnlyList<CardDef> DeckOptions(string heroId) => Cards.Where(c => c.Owner == heroId).ToArray();
}
