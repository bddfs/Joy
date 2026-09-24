using Godot;
using Joy.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Joy;

public partial class Main
{
    private string _cityDetailTab = "node";
    private string _citySelectedEdge = "";

    private void RenderCity()
    {
        var g = _game!; var city = new CityEngine(g);
        if (!g.Nodes.Any(n => n.Id == _selected)) _selected = g.Nodes.First(n => !n.IsHeadquarters).Id;
        var assignments = CityPresentation.Assignments(g);
        RenderCityDispatch(g, city, assignments);

        var workspace = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        workspace.AddThemeConstantOverride("separation", 14); _root.AddChild(workspace);
        var mapPanel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        mapPanel.AddThemeStyleboxOverride("panel", Box(new Color("101824"), new Color("334451"), 1, 4)); workspace.AddChild(mapPanel);
        var mapBody = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; mapBody.AddThemeConstantOverride("separation", 7); mapPanel.AddChild(mapBody);
        var mapHeader = new HBoxContainer(); mapBody.AddChild(mapHeader);
        var title = Text("雾港 / 登记交通与服务网络", 20, Ink); title.SizeFlagsHorizontal = SizeFlags.ExpandFill; mapHeader.AddChild(title);
        var operating = city.OperatingNodeIds().ToHashSet();
        var objective = Text($"源头 {(g.SourceRecovered ? "已交付" : "待交付")}   ·   营业 {operating.Count} / {g.RequiredStations}", 15, Mint);
        objective.AutowrapMode = TextServer.AutowrapMode.Off; mapHeader.AddChild(objective);
        const string mapLegend = "实线：登记双向道路\n╳ 缺口：通路中断，可点击查看记录\n▥ 短栅：封控安排\n青色虚线：选中事务的出勤路线\n琥珀箭头：公开扩张预告，箭头端为目标";
        if (UiScale < 1.4f) mapBody.AddChild(Text("实线：双向道路   ╳：中断   ▥：封控安排   虚线：出勤路线   →：公开扩张预告", 13, Muted));
        else mapHeader.AddChild(CitySmallButton("图例", () => ShowLongText("登记网络图例", mapLegend)));
        var route = assignments.FirstOrDefault(a => a.NodeId == _selected)?.Route ?? new List<string>();
        bool OnRoute(CityEdge e) => Enumerable.Range(0, Math.Max(0, route.Count - 1)).Any(i =>
            route[i] == e.From && route[i + 1] == e.To || route[i] == e.To && route[i + 1] == e.From);
        var map = new NetworkMap();
        map.ConfigureCity(g.Nodes.Select(n =>
        {
            var p = CityPresentation.Position(n);
            string threat = n.Invaded ? "已知受入侵" : g.Forecast.Any(f => f.TargetId == n.Id) ? "今夜预告目标" : "";
            return new MapPoint(n.Id, n.Name, CityPresentation.Operation(g, n), p.X, p.Y, n.Built || n.IsHeadquarters,
                n.Id == _selected, n.Invaded, CityPresentation.ArtId(n.Id), CityPresentation.Ownership(n), CityPresentation.TeamAt(g, n.Id), threat);
        }), g.Edges.Select(e => new MapLink(e.From, e.To, e.Passable, false, e.Id, e.Name,
            g.Nodes.Any(n => n.BlockadeEdgeId == e.Id), OnRoute(e), e.Id == _citySelectedEdge)),
            g.Forecast.Select(f => new MapThreat(f.SourceId, f.TargetId, f.Description)));
        map.Selected += id => { _selected = id; _citySelectedEdge = ""; _cityDetailTab = "node"; Render(); };
        map.EdgeSelected += id => { _citySelectedEdge = id; _cityDetailTab = "node"; Render(); };
        mapBody.AddChild(map);
        var forecast = new HBoxContainer(); mapBody.AddChild(forecast);
        var forecastText = Text(g.Forecast.Count == 0 ? "今夜暂无公开扩张预告。已登记路线仍可回访。" : $"今夜公开预告 {g.Forecast.Count} 项 · " + g.Forecast[0].Description, 14, g.Forecast.Count == 0 ? Muted : Amber);
        forecastText.SizeFlagsHorizontal = SizeFlags.ExpandFill; forecastText.MaxLinesVisible = 2; forecastText.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis; forecast.AddChild(forecastText);
        if (g.Forecast.Count > 0) forecast.AddChild(CitySmallButton("预告详情", () => ShowLongText("今夜扩张预告", string.Join("\n\n", g.Forecast.Select(f => f.Description)))));

        var detail = Pane("调度档案", 370 + (UiScale - 1) * 95); workspace.AddChild(detail.Panel);
        var tabs = new HBoxContainer(); detail.Body.AddChild(tabs);
        foreach (var (id, caption) in new[] { ("node", "地点"), ("crew", "人员"), ("settlement", "日结") })
        {
            var key = id; var b = CitySmallButton(caption, () => { _cityDetailTab = key; Render(); }, _cityDetailTab == id);
            b.SizeFlagsHorizontal = SizeFlags.ExpandFill; tabs.AddChild(b);
        }
        if (_cityDetailTab == "crew") RenderCityCrew(detail.Body, g);
        else if (_cityDetailTab == "settlement") RenderCitySettlement(detail.Body, g);
        else RenderCityNode(detail.Body, g, city, g.Node(_selected));
        RenderCityFooter(g, city, assignments);
    }

    private void RenderCityDispatch(GameState g, CityEngine city, IReadOnlyList<Assignment> assignments)
    {
        var staff = new HBoxContainer(); staff.AddThemeConstantOverride("separation", 16); _root.AddChild(staff);
        int Count(string role) => g.Employees.Count(e => CityPresentation.EmployeeRole(g, e).StartsWith(role, StringComparison.Ordinal));
        int free = Count("待命");
        var totals = Text($"人力 {g.Employees.Count}   /   占用 {g.Employees.Count - free}   /   可用 {free}", 18, Mint); totals.AutowrapMode = TextServer.AutowrapMode.Off; staff.AddChild(totals);
        var states = Text($"○ 待命 {free}    ▷ 外勤 {Count("外勤")}    ◇ 施工 {Count("施工")}    ■ 驻站 {Count("驻站")}    + 恢复 {Count("恢复")}", 14, Muted);
        states.SizeFlagsHorizontal = SizeFlags.ExpandFill; staff.AddChild(states);
        staff.AddChild(CitySmallButton("人员详情", () => { _cityDetailTab = "crew"; Render(); }));
        var slots = new HBoxContainer(); slots.AddThemeConstantOverride("separation", 12); _root.AddChild(slots);
        for (int index = 0; index < g.TransactionLimit; index++)
        {
            var card = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 1 };
            card.AddThemeStyleboxOverride("panel", Box(new Color("151E2B"), new Color("384855"), 1, 3)); slots.AddChild(card);
            var body = new VBoxContainer(); body.AddThemeConstantOverride("separation", 4); card.AddChild(body);
            var task = assignments.ElementAtOrDefault(index);
            var top = new HBoxContainer(); body.AddChild(top);
            var heading = Text(task is null ? $"事务 {index + 1:00}  /  空闲" : $"事务 {index + 1:00}  /  {JobLabel(task.Kind)} · {g.Node(task.NodeId).Name}", 17, task is null ? Muted : Ink);
            heading.SizeFlagsHorizontal = SizeFlags.ExpandFill; top.AddChild(heading);
            if (task is null) { body.AddChild(Text("选择地点加入计划 · 每日最多一次手操出勤", 14, Muted)); continue; }
            var local = task;
            if (g.Phase == DayPhase.Preparation) top.AddChild(CitySmallButton("撤回", () => CityAction(city.RemoveTask(local.Id))));
            else { var status = Text(local.Completed ? "✓ 已完成" : "已锁定", 14, local.Completed ? Mint : Amber); status.AutowrapMode = TextServer.AutowrapMode.Off; top.AddChild(status); }
            string tools = local.ToolIds.Count == 0 ? "未携工具" : string.Join("、", local.ToolIds.Select(id => g.Item(id).Name + $"[{g.Item(id).Charges}]"));
            string workers = string.Join("、", local.EmployeeIds.Select(id => g.Employees.First(e => e.Id == id).Name));
            var brief = Text($"{workers}   ·   {(g.Phase == DayPhase.Preparation ? "待预付" : "已预付")} {local.Cost}   ·   {tools}", 14, Muted);
            brief.MaxLinesVisible = 1; brief.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis; brief.TooltipText = brief.Text;
            var briefRow = new HBoxContainer(); brief.SizeFlagsHorizontal = SizeFlags.ExpandFill; briefRow.AddChild(brief); body.AddChild(briefRow);
            if (local.Kind == JobKind.Manual && g.Phase == DayPhase.Preparation && g.Items.Any(i => i.Location == ItemLocation.Installed && i.Kind == "Lamp"))
            {
                foreach (var lamp in g.Items.Where(i => i.Location == ItemLocation.Installed && i.Kind == "Lamp"))
                {
                    var id = lamp.Id; var support = CitySmallButton((local.SupportItemId == id ? "✓ " : "○ ") + "灯光", () => CityAction(city.AssignSupport(local.Id, local.SupportItemId == id ? null : id)));
                    support.TooltipText = "为这次手操出勤预留驻点灯光支援：" + g.Node(lamp.InstalledNodeId!).Name;
                    briefRow.AddChild(support);
                }
            }
        }
    }

    private void RenderCityNode(VBoxContainer body, GameState g, CityEngine city, CityNode node)
    {
        body.AddChild(ArtAssets.Image(CityPresentation.ArtId(node.Id), 165, TextureRect.StretchModeEnum.KeepAspectCentered));
        body.AddChild(Text(node.Name, 26));
        body.AddChild(Text(CityPresentation.Ownership(node) + "  /  " + CityPresentation.Operation(g, node), 17, Mint));
        body.AddChild(Text(node.Description, 18, Muted));
        if (_citySelectedEdge.Length > 0 && g.Edges.FirstOrDefault(e => e.Id == _citySelectedEdge) is { } edge)
        {
            body.AddChild(new HSeparator()); body.AddChild(Text("所选连接 / " + edge.Name, 20, Amber));
            body.AddChild(Text($"{g.Node(edge.From).Name} ⇄ {g.Node(edge.To).Name}\n" + (edge.Passable ? "登记道路正常通行。" : "通路中断。具体故障原因未记录；可选择相邻节点尝试抢修，或使用另一条可用路线。"), 17));
            foreach (var owner in g.Nodes.Where(n => n.BlockadeEdgeId == edge.Id)) body.AddChild(Text("▥ " + owner.Name + "已安排封控。生效仍需驻站、总部连通及规程条件。", 16, Mint));
        }
        body.AddChild(Text(node.PublicCooperation ? "公开合作 · 可投入 4 资金启动建设。" : $"合同 / 调查里程碑 +{node.SurveyReward} · 交付 +{node.RecoveryReward}\n接管需合同资格，并投入 4 资金。", 17, Amber));
        string path = string.Join(" → ", city.FindRoute(node.Id).Select(id => g.Node(id).Name));
        body.AddChild(Text("总部可用路线 / " + (path.Length == 0 ? "当前断联" : path), 15, Muted));
        if (node.Built)
        {
            body.AddChild(Text("营业条件 / 设施归属不会因停业消失", 18, Mint));
            foreach (var condition in CityPresentation.OperatingConditions(g, node)) body.AddChild(Text((condition.Met ? "✓  " : "○  ") + condition.Label, 16, condition.Met ? Muted : Amber));
            bool staffed = g.Stations.ContainsKey(node.Id);
            var station = Button(staffed ? "撤回驻站员工" : "安排一名驻站员工", () =>
            {
                var worker = g.Employees.FirstOrDefault(e => e.RecoverUntilDay <= g.Day && !g.Stations.Values.Contains(e.Id) && !g.Plan.Any(a => a.EmployeeIds.Contains(e.Id)));
                CityAction(staffed ? city.AssignStation(node.Id, null) : worker is null ? ActionResult.Fail("没有空闲员工可驻站") : city.AssignStation(node.Id, worker.Id));
            }); station.Disabled = g.Phase != DayPhase.Preparation; body.AddChild(station);
            foreach (var e in g.Edges.Where(e => e.From == node.Id || e.To == node.Id))
            {
                var adjacent = e; var blockade = Button((node.BlockadeEdgeId == e.Id ? "▥ 已安排 " : "○ 安排封控 ") + e.Name,
                    () => CityAction(city.SetBlockade(node.Id, node.BlockadeEdgeId == adjacent.Id ? null : adjacent.Id)));
                blockade.Disabled = g.Phase != DayPhase.Preparation; body.AddChild(blockade);
            }
        }
        body.AddChild(new HSeparator()); body.AddChild(Text("事务选择", 20, Ink));
        if (!node.IsHeadquarters)
        {
            foreach (var kind in new[] { JobKind.Manual, JobKind.Survey, JobKind.ContrastSurvey, JobKind.Recovery, JobKind.Build, JobKind.Maintenance, JobKind.Repair })
            {
                var k = kind; var preview = city.PreviewTask(k, node.Id);
                if (k == JobKind.ContrastSurvey && !node.RuleIds.Any(id => g.Rule(id).Family == "MirrorGuest")) continue;
                var action = Button($"{JobLabel(k)}  /  {preview.Cost} 资金", () => CityAction(city.AddTask(k, node.Id)));
                action.Disabled = !preview.Allowed || g.Phase != DayPhase.Preparation; action.TooltipText = preview.Text; body.AddChild(action);
                if (!preview.Allowed) body.AddChild(Text("条件：" + preview.Text, 14, Muted));
            }
        }
        else
        {
            var supply = Button("补充唯一封装工具 · 3 资金", () => CityAction(city.ScheduleSupply())); supply.Disabled = g.Phase != DayPhase.Preparation; body.AddChild(supply);
            body.AddChild(Text("占用 1 名员工和 1 个事务位；增加 1 次使用，从次日起可用。", 15, Muted));
            foreach (var item in g.Items.Where(i => i.Location == ItemLocation.Warehouse && i.RequiresSealing && !i.Sealed && !i.Required))
            {
                var id = item.Id; var seal = Button("总部补封装 · " + item.Name, () => CityAction(city.ScheduleHeadquartersSeal(id)));
                seal.Disabled = g.Phase != DayPhase.Preparation; body.AddChild(seal);
            }
        }
        foreach (var lamp in g.Items.Where(i => i.Kind == "Lamp" && i.Location == ItemLocation.Warehouse))
        {
            body.AddChild(new HSeparator()); body.AddChild(Text(lamp.Name + (lamp.Sealed ? $" · 已封装 · 收购价 {lamp.Value}" : " · 未封装暂存"), 18, Amber));
            var id = lamp.Id;
            if (lamp.Sealed)
            {
                var sell = Button(lamp.Disposition == "Sell" ? "✓ 已登记日终出售" : $"登记日终出售 +{lamp.Value}", () => CityAction(city.SellItem(id)));
                sell.Disabled = g.Phase != DayPhase.Preparation; body.AddChild(sell);
                var keep = Button("保留实物", () => CityAction(city.KeepItem(id))); keep.Disabled = g.Phase != DayPhase.Preparation; body.AddChild(keep);
            }
            else { var seal = Button("安排总部补封装", () => CityAction(city.ScheduleHeadquartersSeal(id))); seal.Disabled = g.Phase != DayPhase.Preparation; body.AddChild(seal); }
        }
        foreach (var rule in g.Rules.Where(r => node.RuleIds.Contains(r.Id))) AddRule(body, rule, false);
    }

    private void RenderCityCrew(VBoxContainer body, GameState g)
    {
        body.AddChild(Text("员工岗位 / 互斥占用", 24));
        body.AddChild(Text("待命、外勤、施工、驻站、恢复共五类。研究属于施工；准备阶段的占用表示已排期，锁定后保持至日终。", 17, Muted));
        foreach (var employee in g.Employees)
        {
            body.AddChild(new HSeparator()); string role = CityPresentation.EmployeeRole(g, employee);
            body.AddChild(Text(employee.Name + "  /  " + role, 20, role == "待命" ? Mint : Ink));
            body.AddChild(Text(CityPresentation.EmployeeDetail(g, employee), 17, Muted));
        }
    }

    private void RenderCitySettlement(VBoxContainer body, GameState g)
    {
        body.AddChild(Text("上一日 / 结算时间线", 24));
        body.AddChild(Text("按引擎实际记录的先后顺序。每阶段余额是实际结果；预期收入不会提前记账。", 17, Muted));
        if (g.SettlementTimeline.Count > 0)
        {
            foreach (var stage in g.SettlementTimeline)
            {
                body.AddChild(new HSeparator()); body.AddChild(Text($"{stage.Sequence:00}  {stage.Title}", 20, Mint));
                body.AddChild(Text($"第 {stage.Day} 日 · 余额 {stage.MoneyBefore} → {stage.MoneyAfter}", 15, Amber));
                foreach (var line in stage.Events) body.AddChild(Text(line, 17));
                if (stage.Events.Count == 0) body.AddChild(Text("本阶段没有新增事件。", 16, Muted));
            }
        }
        else if (g.LastDaySummary.Count > 0)
        {
            for (int i = 0; i < g.LastDaySummary.Count; i++) body.AddChild(Text($"{i + 1:00}  {g.LastDaySummary[i]}", 17));
        }
        else body.AddChild(Text("尚未完成第一次日结。", 18, Amber));
    }

    private void RenderCityFooter(GameState g, CityEngine city, IReadOnlyList<Assignment> assignments)
    {
        var panel = new PanelContainer(); panel.AddThemeStyleboxOverride("panel", Box(new Color("182431"), new Color("637B84"), 1, 4)); _root.AddChild(panel);
        var bottom = new HBoxContainer(); bottom.AddThemeConstantOverride("separation", 16); panel.AddChild(bottom);
        var figuresColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; figuresColumn.AddThemeConstantOverride("separation", 4); bottom.AddChild(figuresColumn);
        var tools = g.Items.Where(i => (i.Kind is "SealKit" or "SourceCase" or "WardClip") && (i.Location != ItemLocation.Room || i.Discovered)).ToList();
        var toolsText = Text("实物台账 / " + string.Join("   ·   ", tools.Select(i => i.Name + $" {i.Charges}/{i.MaxCharges}" + (CityPresentation.ItemReservation(g, i) == "未预留" ? " 未预留" : " 已预留"))), 14, Mint);
        toolsText.SizeFlagsHorizontal = SizeFlags.ExpandFill; toolsText.MaxLinesVisible = 1; toolsText.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis; figuresColumn.AddChild(toolsText);
        var budget = city.GetBudget();
        var figures = Text(UiScale >= 1.4f
            ? $"可用 {budget.CurrentMoney}  已付 {budget.AlreadyPaid}  待付 {budget.UnpaidUpfront + budget.DueExpenses}  条件 +{budget.ConditionalIncome}  保守日终 {budget.ConservativeBalance}"
            : $"可用 {budget.CurrentMoney}   已付 {budget.AlreadyPaid}   待付 {budget.UnpaidUpfront + budget.DueExpenses}\n固定收入 +{budget.LockedOperatingIncome}   条件收入 +{budget.ConditionalIncome}   保守日终 {budget.ConservativeBalance}", 16, Ink);
        figures.SizeFlagsHorizontal = SizeFlags.ExpandFill; figuresColumn.AddChild(figures);
        bottom.AddChild(CitySmallButton("实物配装", () => { _page = "tools"; Render(); }));
        bottom.AddChild(CitySmallButton("预算明细", () => ShowLongText("今日预算 / 分阶段现金", budget.Text)));
        if (g.Phase == DayPhase.Preparation)
        {
            if (g.PressureEnabled && !g.EmergencySupportUsed) bottom.AddChild(CitySmallButton("紧急支援 +6", () => CityAction(city.EmergencySupport())));
            bottom.AddChild(Button("锁定计划 →", () => ConfirmDialog("锁定今日计划", $"立即预付 {budget.UnpaidUpfront} 资金。\n人员、工具与两个事务位会被锁定；全部队伍统一日结。\n\n{budget.Text}", LockDay), true));
        }
        else bottom.AddChild(Button("完成日终结算 →", () => CityAction(city.SettleDay()), true));
    }

    private static Button CitySmallButton(string text, Action action, bool primary = false)
    {
        var button = Button(text, action, primary); button.CustomMinimumSize = new Vector2(0, 30 * UiScale);
        button.AddThemeFontSizeOverride("font_size", (int)(14 * UiScale));
        foreach (var style in new[] { "normal", "hover", "pressed", "disabled" })
        {
            var box = Box(primary ? new Color("314C52") : new Color("202C3A"), primary ? Mint : new Color("46505F"), 1, 3);
            box.ContentMarginTop = box.ContentMarginBottom = 5; box.ContentMarginLeft = box.ContentMarginRight = 10; button.AddThemeStyleboxOverride(style, box);
        }
        return button;
    }

    private void LockDay()
    {
        var g = _game!; var city = new CityEngine(g); var result = city.LockDay();
        _notice = result.Message;
        if (result.Success)
        {
            var manual = g.LockedPlan.FirstOrDefault(a => a.Kind == JobKind.Manual && !a.Completed);
            if (manual is not null) result = new FieldEngine(g).Start(manual);
            _notice = result.Message;
            Save(g.Field is { Finished: false } f ? $"{g.Node(f.NodeId).Name} · 第 {f.Round} 轮开始" : $"第 {g.Day} 日 · 计划已锁定");
        }
        Render();
    }
}
