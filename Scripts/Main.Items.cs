using Godot;
using Joy.Core;
using System;
using System.Linq;

namespace Joy;

public partial class Main
{
    private void RenderItemGallery()
    {
        _root.AddChild(Text("公司实物档案  /  主体与状态", 32));
        _root.AddChild(Text("状态对照样稿 · 每列展示同一件物品的合法状态，不表示同时拥有多件。", 18, Muted));
        var columns = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; columns.AddThemeConstantOverride("separation", 16); _root.AddChild(columns);
        var baseline = _game!;
        foreach (string kind in new[] { "hotel-clock", "lamp", "seal-a" })
        {
            var pane = Pane(kind == "hotel-clock" ? "VIS-ITEM-01 / 座钟" : kind == "lamp" ? "VIS-ITEM-02 / 侧房灯" : "VIS-TOOL-01 / 唯一隔离工具", 0);
            pane.Panel.SizeFlagsHorizontal = SizeFlags.ExpandFill; columns.AddChild(pane.Panel);
            int count = kind == "lamp" ? 5 : 4;
            for (int i = 0; i < count; i++)
            {
                _game = System.Text.Json.JsonSerializer.Deserialize<GameState>(System.Text.Json.JsonSerializer.Serialize(baseline))!;
                var item = _game.Item(kind); item.Discovered = true;
                if (kind == "hotel-clock")
                {
                    item.Location = new[] { ItemLocation.Room, ItemLocation.Backpack, ItemLocation.Warehouse, ItemLocation.Delivered }[i]; item.Sealed = i >= 2;
                }
                else if (kind == "lamp")
                {
                    item.Location = i < 2 ? ItemLocation.Warehouse : ItemLocation.Installed; item.Sealed = i >= 1;
                    var node = _game.Node("depot"); node.Built = true; node.BuiltDay = 0; _game.Stations["depot"] = "e1";
                    if (i >= 2) item.InstalledNodeId = "depot";
                    if (i == 3) item.SupportUsedDay = _game.Day;
                    if (i == 4) node.Invaded = true;
                }
                else
                {
                    item.Charges = i == 2 ? 0 : 2;
                    if (i == 1) new CityEngine(_game).AddTask(JobKind.Manual, "hotel");
                    if (i == 3) { item.Charges = 1; item.AvailableDay = _game.Day + 1; }
                }
                pane.Body.AddChild(ItemDossier(item, true));
            }
        }
        _game = baseline;
    }

    private Control ItemDossier(ItemState item, bool compact = false)
    {
        var display = ItemPresentation.Describe(_game!, item);
        var panel = new PanelContainer(); panel.AddThemeStyleboxOverride("panel", Box(new Color("101722"), new Color("48515f"), 1, 3));
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 18); panel.AddChild(row);
        var portrait = new VBoxContainer { CustomMinimumSize = new Vector2(compact ? 112 : 140, 0) }; row.AddChild(portrait);
        var art = ArtAssets.Image(display.ArtId, compact ? 84 : 116); art.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered; portrait.AddChild(art);
        var stamp = Text(display.Seal, 14, item.Sealed ? Mint : Amber); stamp.HorizontalAlignment = HorizontalAlignment.Center; portrait.AddChild(stamp);
        var details = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; details.AddThemeConstantOverride("separation", compact ? 3 : 7); row.AddChild(details);
        details.AddChild(Text(item.Name, compact ? 20 : 22));
        details.AddChild(Text(display.Location, 17, Mint));
        if (display.Capacity.Length > 0) details.AddChild(Text(display.Capacity, 17, Amber));
        if (display.Support.Length > 0) details.AddChild(Text(display.Support, 17, Amber));
        details.AddChild(Text(display.Reservation + "\n" + display.Availability, 15, Muted));
        return panel;
    }

    private void ShowItemHistory()
    {
        string text = string.Join("\n\n", _game!.Items.Where(i => i.Location is ItemLocation.Delivered or ItemLocation.Sold or ItemLocation.Consumed)
            .Select(i => i.Name + "\n" + ItemPresentation.Describe(_game, i).Location));
        ShowLongText("实物去向 / 同一实例不会复制", text.Length == 0 ? "尚无已交付、出售或消耗的实物。" : text);
    }
}
