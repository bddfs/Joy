using System.Collections.Generic;
using System.Linq;

namespace Joy.Core;

/// <summary>Public, read-only item display data. No rule truth or reward prediction is consulted.</summary>
public sealed record ItemDisplay(string ArtId, string Location, string Seal, string Availability, string Reservation, string Capacity, string Support);

public static class ItemPresentation
{
    public static string ArtId(ItemState item) => item.Id == "hotel-clock" ? "item-clock" : item.Kind == "Lamp" ? "item-lamp" : "item-kit";

    public static ItemDisplay Describe(GameState state, ItemState item)
    {
        var tasks = state.Phase == DayPhase.Preparation ? state.Plan : state.LockedPlan;
        var reserved = tasks.FirstOrDefault(t => t.Id == item.ReservedBy)
            ?? tasks.FirstOrDefault(t => t.ToolIds.Contains(item.Id) || t.SupportItemId == item.Id || t.TargetItemId == item.Id);
        string place = item.Location switch
        {
            ItemLocation.Room => item.Discovered ? "已发现 · 留在现场" : "尚未发现",
            ItemLocation.Backpack => "随队携带 · 尚未归库",
            ItemLocation.Warehouse => "公司仓库 · 唯一实物",
            ItemLocation.Installed => "已安装 · " + (state.Nodes.FirstOrDefault(n => n.Id == item.InstalledNodeId)?.Name ?? "已登记驻点"),
            ItemLocation.Delivered => "合同已交付 · 已离开库存",
            ItemLocation.Sold => "已出售 · 已离开库存",
            _ => "实物已消耗 · 不可再次使用"
        };
        string seal = !item.RequiresSealing ? "无需封装" : item.Sealed ? "已封装" : "未封装";
        string available = item.AvailableDay > state.Day ? $"工程待生效 · 第 {item.AvailableDay} 日可用" : "按当前阶段与规程操作";
        string reservation = reserved is null ? "未被事务预留" : $"整件预留 · {state.Node(reserved.NodeId).Name} / {reserved.EmployeeIds.Count} 人事务";
        string capacity = item.Kind is "SealKit" or "SourceCase" or "SupplyCharge" or "RevealLens" or WardClipCatalog.Kind
            ? $"剩余 {item.Charges} / 容量 {item.MaxCharges} · 预留不等于消耗" : "";
        string support = "";
        if (item.Kind == "Lamp")
        {
            if (!item.Sealed) support = "未封装暂存 · 不能出售或支援";
            else if (item.Location == ItemLocation.Installed)
                support = item.SupportUsedDay == state.Day ? "今日支援已用 · 拆装与读档不刷新"
                    : new CityEngine(state).OperatingNodeIds().Contains(item.InstalledNodeId) ? "今日支援未用 · 需在锁定前预留"
                    : "今日支援未用 · 停业暂停能力";
            else if (item.Location == ItemLocation.Warehouse) support = item.Disposition == "Sell" ? "已登记日终出售 · 尚未到账" : $"公开报价 {item.Value} · 出售到账前不计现金";
        }
        return new(ArtId(item), place, seal, available, reservation, capacity, support);
    }
}
