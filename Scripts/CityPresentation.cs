using Joy.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Joy;

/// <summary>Read-only registered facts. Hidden rule truth and consequences are never inspected.</summary>
public static class CityPresentation
{
    public static IReadOnlyList<Assignment> Assignments(GameState s) => s.Phase == DayPhase.Preparation ? s.Plan : s.LockedPlan;
    public static string Ownership(CityNode n) => n.IsHeadquarters ? "■ 公司总部" : n.Built ? "■ 公司已接管" : "□ 未接管";
    public static string Operation(GameState s, CityNode n)
    {
        if (n.IsHeadquarters) return "总部 · 人员与资产归档";
        if (!n.Built)
        {
            if (Assignments(s).Any(t => t.NodeId == n.Id && t.Kind == JobKind.Build && !t.Completed))
                return s.Phase == DayPhase.Preparation ? "建设已排期" : "施工中 · 次日生效";
            if (n.IsSource) return s.SourceRecovered ? "源头已交付" : "源头待处置";
            if (n.Kind == "Hub") return "登记交通枢纽";
            bool eligible = n.PublicCooperation || n.RecoveryPaid && n.RuleIds.All(id => s.Rule(id).Confirmed && s.Rule(id).Archived);
            return eligible ? "可接管 · 启动投入 4" : "待满足接管条件";
        }
        if (n.BuiltDay >= s.Day) return "明日生效 · 设施已建成";
        var missing = OperatingConditions(s, n).Where(x => !x.Met).Select(x => x.ShortLabel).ToList();
        return missing.Count == 0 ? "● 有效营业" : "暂停 · " + string.Join(" / ", missing);
    }
    public static IReadOnlyList<CityCondition> OperatingConditions(GameState s, CityNode n) => new[]
    {
        new CityCondition("已建立公司设施", "未建成", n.Built),
        new CityCondition("建成已进入下一经营日", "明日生效", n.Built && n.BuiltDay < s.Day),
        new CityCondition("存在通往总部的可用路线", "断联", new CityEngine(s).FindRoute(n.Id).Count > 0),
        new CityCondition("本日安排驻站员工", "缺驻站", s.Stations.ContainsKey(n.Id)),
        new CityCondition("维护条件正常", "待维护", n.Maintained),
        new CityCondition("当前未受已知入侵", "受入侵", !n.Invaded)
    };
    public static string TeamAt(GameState s, string id) => string.Join(" · ", Assignments(s).Select((t, i) => (Task: t, Number: i + 1))
        .Where(x => x.Task.NodeId == id).Select(x => $"{(s.Phase == DayPhase.Preparation ? "拟派" : x.Task.Completed ? "已归队" : "事务")} {x.Number:00}"));
    public static string EmployeeRole(GameState s, Employee e)
    {
        if (e.RecoverUntilDay > s.Day) return "恢复";
        var t = Assignments(s).FirstOrDefault(a => a.EmployeeIds.Contains(e.Id));
        if (t is not null) return t.Kind is JobKind.Manual or JobKind.Survey or JobKind.ContrastSurvey or JobKind.Recovery ? "外勤" : t.Kind == JobKind.Research ? "施工·研究" : "施工";
        return s.Stations.ContainsValue(e.Id) ? "驻站" : "待命";
    }
    public static string EmployeeDetail(GameState s, Employee e)
    {
        if (e.RecoverUntilDay > s.Day) return $"第 {e.RecoverUntilDay} 日恢复可用";
        var t = Assignments(s).FirstOrDefault(a => a.EmployeeIds.Contains(e.Id));
        if (t is not null) return s.Node(t.NodeId).Name + (s.Phase == DayPhase.Preparation ? " · 已排期" : " · 当日岗位已锁定");
        var station = s.Stations.FirstOrDefault(x => x.Value == e.Id);
        return station.Key is not null ? s.Node(station.Key).Name : "可安排出勤、工程或驻站";
    }
    public static string ItemReservation(GameState s, ItemState item)
    {
        var assignments = Assignments(s);
        for (int i = 0; i < assignments.Count; i++)
        {
            var t = assignments[i];
            if (t.ToolIds.Contains(item.Id) || t.TargetItemId == item.Id || t.SupportItemId == item.Id || item.ReservedBy == t.Id)
                return $"事务 {i + 1:00} · {s.Node(t.NodeId).Name}";
        }
        return item.ReservedBy is not null ? "已有任务预留" : "未预留";
    }
    public static string ItemPlace(GameState s, ItemState item) => item.Location switch
    {
        ItemLocation.Warehouse => item.AvailableDay > s.Day ? $"总部 · 第 {item.AvailableDay} 日可用" : "总部仓库",
        ItemLocation.Backpack => "队伍携带中", ItemLocation.Room => s.Nodes.Any(n => n.Id == item.NodeId) ? s.Node(item.NodeId).Name + " · 现场库存" : "现场库存",
        ItemLocation.Installed => s.Nodes.Any(n => n.Id == item.InstalledNodeId) ? s.Node(item.InstalledNodeId).Name + " · 已安装" : "已安装",
        ItemLocation.Delivered => "已交付", ItemLocation.Consumed => "已消耗", ItemLocation.Sold => "已出售", _ => "实物记录"
    };
    public static string ArtId(string id) => id switch
    {
        "hq" => "node-hq", "hotel" => "node-hotel", "junction" => "node-north", "hospital" => "node-hospital",
        "source" => "node-source", "relay" => "node-south", "depot" => "node-coop", _ => "city"
    };
    // Presentation placement only: topology always comes directly from the registered edges.
    public static (float X, float Y) Position(CityNode n) => n.Id switch
    {
        "hq" => (0f, 0f), "hotel" => (.25f, 0f), "junction" => (.50f, 0f), "hospital" => (.75f, 0f),
        "source" => (1f, 0f), "relay" => (.28f, 1f), "depot" => (.62f, 1f), _ => (n.X, n.Y)
    };
}
public sealed record CityCondition(string Label, string ShortLabel, bool Met);
