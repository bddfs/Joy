using Godot;
using Joy.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Joy;

public partial class Main : Control
{
    private static readonly Color Ink = new("e7ece2");
    private static readonly Color Muted = new("97acae");
    private static readonly Color Mint = new("83c9ae");
    private static readonly Color Amber = new("e5b577");
    private static readonly Color Panel = new("101e25");
    private GameState? _game;
    private SaveStore _save = null!;
    private string _selected = "hotel";
    private string _viewedRoom = "";
    private string _page = "dispatch";
    private string _notice = "";
    private string _saveFailure = "";
    private string _lastSavedCheckpoint = "";
    private DateTimeOffset? _lastSavedAt;
    private bool _welcome = true;
    private bool _screenshotMode;
    private FieldAction? _pendingAction;
    private bool _pendingEndRound;
    private string _fieldTab = "evidence";
    private VBoxContainer _root = null!;

    public override void _Ready()
    {
        var args = OS.GetCmdlineUserArgs();
        _screenshotMode = args.Any(a => a.StartsWith("--capture"));
        if (args.Contains("--ui-test")) GetWindow().Title = "JOY · M2 界面验证";
        _save = new SaveStore(ProjectSettings.GlobalizePath(_screenshotMode ? "user://preview-m2" : args.Contains("--ui-test") ? "user://ui-validation-m2" : "user://profile-m2"));
        GetWindow().MinSize = new Vector2I(1280, 800);
        GetTree().AutoAcceptQuit = false;
        BuildTheme();
        if (_screenshotMode)
        {
            _game = WorldFactory.Create(413);
            _welcome = false;
            if (args.Any(a => a.StartsWith("--capture-field")))
            {
                var city = new CityEngine(_game);
                city.AddTask(JobKind.Manual, "hotel");
                city.LockDay();
                new FieldEngine(_game).Start(_game.LockedPlan.First(a => a.Kind == JobKind.Manual));
                if (args.Contains("--capture-field-used"))
                {
                    var engine = new FieldEngine(_game);
                    engine.Execute(new FieldAction { Kind = FieldActionKind.Move, TargetId = "hotel-room1" });
                    int slot = _game.Field!.HandSlots.FindIndex(c => c == CardKind.Search);
                    engine.Execute(new FieldAction { Kind = FieldActionKind.PlayCard, Card = CardKind.Search, HandSlotIndex = slot });
                    _viewedRoom = "hotel-room1";
                    _pendingAction = CardAction(CardKind.Search, _game.Field.HandSlots.FindIndex(c => c == CardKind.Search));
                }
            }
        }
        Render();
        if (_screenshotMode) _ = Capture(args.Contains("--capture-field-used") ? "field-used-preview.png" : args.Contains("--capture-field") ? "field-preview.png" : "city-preview.png");
    }

    private async Task Capture(string name)
    {
        for (int i = 0; i < 12; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var path = ProjectSettings.GlobalizePath("res://../.checks/" + name);
        var error = GetViewport().GetTexture().GetImage().SavePng(path);
        GD.Print($"JOY_UI_CAPTURE | {path} | {error}");
        GetTree().Quit(error == Error.Ok ? 0 : 1);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest) AskQuit();
    }

    public static StyleBoxFlat Box(Color fill, Color? border = null, int width = 0, int radius = 8)
    {
        return new StyleBoxFlat
        {
            BgColor = fill, BorderColor = border ?? new Color("2b4147"),
            BorderWidthBottom = width, BorderWidthTop = width, BorderWidthLeft = width, BorderWidthRight = width,
            CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
            CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius,
            ContentMarginLeft = 16, ContentMarginRight = 16, ContentMarginTop = 12, ContentMarginBottom = 12
        };
    }

    private void BuildTheme()
    {
        Theme = new Theme();
        Theme.DefaultFont = new SystemFont { FontNames = new[] { "Microsoft YaHei UI", "Microsoft YaHei", "PingFang SC", "Noto Sans CJK SC" } };
        Theme.DefaultFontSize = 17;
        Theme.SetColor("font_color", "Label", Ink);
        Theme.SetColor("font_color", "Button", Ink);
        Theme.SetColor("font_disabled_color", "Button", new Color("60777c"));
        Theme.SetStylebox("normal", "Button", Box(new Color("21363a"), new Color("3c5a5c"), 1, 5));
        Theme.SetStylebox("hover", "Button", Box(new Color("304947"), Mint, 1, 5));
        Theme.SetStylebox("pressed", "Button", Box(new Color("3c5b50"), Amber, 1, 5));
        Theme.SetStylebox("disabled", "Button", Box(new Color("15242b"), new Color("293a40"), 1, 5));
        Theme.SetStylebox("focus", "Button", Box(new Color(0, 0, 0, 0), Amber, 2, 5));
        Theme.SetStylebox("panel", "PopupPanel", Box(Panel, Mint, 1));
        Theme.SetStylebox("panel", "Window", Box(Panel, Mint, 1));
    }

    public void Render()
    {
        foreach (var child in GetChildren()) { RemoveChild(child); child.QueueFree(); }
        var background = new ColorRect { Color = new Color("0b151b"), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(background); background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var margin = new MarginContainer();
        AddChild(margin); margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach (var side in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + side, 24);
        _root = new VBoxContainer(); _root.AddThemeConstantOverride("separation", 16); margin.AddChild(_root);
        if (_welcome) { RenderWelcome(); return; }
        RenderHeader();
        if (_game!.Phase is DayPhase.Victory or DayPhase.Failed) RenderOutcome();
        else if (_game.Field is { Finished: false }) RenderField();
        else if (_page == "archive") RenderArchive();
        else if (_page == "ledger") RenderLedger();
        else if (_page == "tools") RenderTools();
        else RenderCity();
        var footer = new HBoxContainer(); _root.AddChild(footer);
        var message = Text(_saveFailure.Length > 0 ? _saveFailure : _notice.Length > 0 ? _notice : "每一步都留下记录。先理解守则，再决定带走什么。", 15, Amber);
        message.SizeFlagsHorizontal = SizeFlags.ExpandFill; footer.AddChild(message);
        var version = Text("JOY / v0.3 · M2 验证", 13, Muted);
        version.AutowrapMode = TextServer.AutowrapMode.Off;
        footer.AddChild(version);
    }

    private void RenderWelcome()
    {
        var row = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; _root.AddChild(row);
        var title = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
        title.AddThemeConstantOverride("separation", 22); row.AddChild(title);
        title.AddChild(Text("J O Y   /   FIELD OPERATIONS DIVISION", 16, Mint));
        title.AddChild(Text("异常回收公司", 60));
        title.AddChild(Text("把未知留在档案里。\n把员工带回家。", 29, Amber));
        title.AddChild(Text("经营一张逐渐亮起的城市网络，\n派出队伍调查、回收，再亲自走进规则的现场。", 20, Muted));
        var card = Pane("新的一份城市委托", 470); row.AddChild(card.Panel);
        card.Body.AddChild(Text("雾港 / 5 个经营日", 30));
        card.Body.AddChild(Text("目标：回收异常源头，并让 2 个服务点有效营业。\n\n你有 16 资金、6 名员工和每天 2 个事务位。每个经营日最多亲自指挥一支队伍。", 19, Muted));
        var seed = new LineEdit { Text = "413", PlaceholderText = "城市种子", CustomMinimumSize = new Vector2(0, 44) };
        card.Body.AddChild(Text("验证种子（固定城市骨架，隐藏实例与抽牌可变）", 14, Mint)); card.Body.AddChild(seed);
        var price = new OptionButton(); price.AddItem("灯具公开收购价：6 资金", 6); price.AddItem("灯具公开收购价：2 资金", 2); card.Body.AddChild(price);
        var pressure = new CheckButton { Text = "开启独立传播压力场景" }; card.Body.AddChild(pressure);
        card.Body.AddChild(Button("开始新局  →", () =>
        {
            void Start() { _game = WorldFactory.Create(int.TryParse(seed.Text, out int value) ? value : 413, lampPrice: price.GetSelectedId(), pressure: pressure.ButtonPressed); _lastSavedCheckpoint = ""; _lastSavedAt = null; _welcome = false; _page = "dispatch"; _selected = "hotel"; _pendingAction = null; _pendingEndRound = false; Save("第 1 日 · 准备阶段"); Render(); }
            if (_save.Exists) ConfirmDialog("开始新局", "将替换当前继续进度；上一份完整存档保留为备份。", Start); else Start();
        }, true));
        if (_save.Exists)
        {
            var loaded = _save.Load();
            if (loaded.Success && loaded.State is { } s)
            {
                card.Body.AddChild(Text($"继续进度：{s.CityName} · 第 {s.Day} 日\n{s.LastCheckpoint}\n{s.SavedAt?.ToLocalTime():MM-dd HH:mm}", 15, Mint));
                card.Body.AddChild(Button("继续游戏", () => { _game = s; _lastSavedCheckpoint = s.LastCheckpoint; _lastSavedAt = s.SavedAt; _saveFailure = ""; _welcome = false; _notice = loaded.Message; Render(); }));
            }
            else card.Body.AddChild(Text(loaded.Message, 15, Amber));
        }
        card.Body.AddChild(Button("操作手册", ShowHelp));
        card.Body.AddChild(Text("M2：镜中住客、共享封装工具与灯具处置。基础场景用于核对五日资源；传播压力可独立开启。旧版存档保留在原目录。", 14, Muted));
    }

    private void RenderHeader()
    {
        var g = _game!;
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 24); _root.AddChild(row);
        var brand = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        brand.AddChild(Text("异常回收公司", 31)); brand.AddChild(Text("ANOMALY RECOVERY BUREAU  /  雾港营业部", 13, Mint)); row.AddChild(brand);
        row.AddChild(Stat("经营日", $"{g.Day:00} / {g.Deadline:00}"));
        row.AddChild(Stat("公司资金", $"{g.Money:00}"));
        row.AddChild(Stat("现场状态", g.Field is { Finished: false } ? "出勤中" : PhaseLabel(g.Phase)));
        row.AddChild(Button("操作手册", ShowHelp)); row.AddChild(Button("返回标题", () => ConfirmDialog("返回标题", ExitMessage(), () => { _welcome = true; _notice = ""; Render(); })));
        var nav = new HBoxContainer(); nav.AddThemeConstantOverride("separation", 10); _root.AddChild(nav);
        foreach (var (id, label) in new[] { ("dispatch", "01  城市调度"), ("archive", "02  守则档案"), ("tools", "03  工具配装"), ("ledger", "04  收支记录") })
        {
            var b = Button(label, () => { _page = id; Render(); }, _page == id); b.Disabled = g.Field is { Finished: false }; nav.AddChild(b);
        }
        var saved = Text(_lastSavedAt is not null ? _lastSavedCheckpoint + $" · 已保存 {_lastSavedAt.Value.ToLocalTime():HH:mm:ss}" : "尚未保存", 14, Muted);
        saved.SizeFlagsHorizontal = SizeFlags.ExpandFill; saved.HorizontalAlignment = HorizontalAlignment.Right; nav.AddChild(saved);
    }

    private void RenderCity()
    {
        var g = _game!; var city = new CityEngine(g);
        if (!g.Nodes.Any(n => n.Id == _selected)) _selected = g.Nodes.First(n => !n.IsHeadquarters).Id;
        var node = g.Node(_selected);
        var row = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; row.AddThemeConstantOverride("separation", 16); _root.AddChild(row);
        var left = Pane("今日安排", 280); row.AddChild(left.Panel);
        left.Body.AddChild(Text($"事务 {g.Plan.Count} / {g.TransactionLimit}   ·   驻站 {g.Stations.Count}", 22, Mint));
        left.Body.AddChild(Text("出勤每队 2 人；工程、驻站各 1 人。\n锁定后不能重排，全部队伍共用一次日结。", 14, Muted));
        foreach (var assignment in g.Plan)
        {
            var local = assignment;
            left.Body.AddChild(Text($"{JobLabel(local.Kind)} · {g.Node(local.NodeId).Name}\n预付 {local.Cost}  /  {string.Join("、", local.EmployeeIds.Select(id => g.Employees.First(e => e.Id == id).Name))}", 16));
            left.Body.AddChild(Text(local.ToolIds.Count == 0 ? "未携带工具" : "预留实物：" + string.Join("、", local.ToolIds.Select(id => g.Item(id).Name + $"（余 {g.Item(id).Charges}）")), 13, Muted));
            if (g.Phase == DayPhase.Preparation) left.Body.AddChild(Button("撤回此安排", () => CityAction(city.RemoveTask(local.Id))));
            if (local.Kind == JobKind.Manual && g.Phase == DayPhase.Preparation)
                foreach (var lamp in g.Items.Where(i => i.Location == ItemLocation.Installed))
                    left.Body.AddChild(Button((local.SupportItemId == lamp.Id ? "✓ " : "○ ") + "预留驻点灯光支援", () => CityAction(city.AssignSupport(local.Id, local.SupportItemId == lamp.Id ? null : lamp.Id))));
        }
        if (g.Plan.Count == 0) left.Body.AddChild(Text("尚未安排事务。\n选择地图地点，在右侧加入计划。", 17, Amber));
        left.Body.AddChild(new HSeparator());
        foreach (var employee in g.Employees)
        {
            var assigned = g.Plan.FirstOrDefault(a => a.EmployeeIds.Contains(employee.Id));
            var station = g.Stations.FirstOrDefault(s => s.Value == employee.Id);
            string role = employee.RecoverUntilDay > g.Day ? "恢复中" : assigned is not null ? JobLabel(assigned.Kind) : station.Key is not null ? "驻站 · " + g.Node(station.Key).Name : "待命";
            left.Body.AddChild(Text($"●  {employee.Name}    {role}", 15, role == "待命" ? Muted : Ink));
        }
        var budget = city.GetBudget();
        int upfront = budget.UnpaidUpfront;
        left.Footer.AddChild(new HSeparator());
        left.Footer.AddChild(Text($"当前可用 {budget.CurrentMoney}   已预付 {budget.AlreadyPaid}\n待预付 −{budget.UnpaidUpfront}   待日支出 −{budget.DueExpenses}\n固定营业 +{budget.LockedOperatingIncome}\n条件收入 +{budget.ConditionalIncome}\n保守日终 {budget.ConservativeBalance}  /  达成后 {budget.ConditionalBalance}", 15, Amber));
        if (g.Phase == DayPhase.Preparation)
        {
            left.Footer.AddChild(Button("锁定计划 · 开始经营日", () => ConfirmDialog("锁定今日计划", $"预付 {upfront} 资金，固定人员与工具。\n执行后所有队伍统一结算，不能回到晨间重排。", LockDay), true));
            if (g.PressureEnabled && !g.EmergencySupportUsed) left.Footer.AddChild(Button("申请一次紧急支援  +6", () => CityAction(city.EmergencySupport())));
        }
        else left.Footer.AddChild(Button("完成日终结算 →", () => CityAction(city.SettleDay()), true));

        var center = Pane("雾港交通与服务网络", 0); center.Panel.SizeFlagsHorizontal = SizeFlags.ExpandFill; row.AddChild(center.Panel);
        center.Body.AddChild(Text("登记路线始终保留 · 可双向回访 · 橙色箭头为今夜扩张", 14, Muted));
        var operating = city.OperatingNodeIds().ToHashSet();
        var map = new NetworkMap();
        map.Configure(g.Nodes.Select(n => new MapPoint(n.Id, n.Name, n.IsHeadquarters ? "总部" : n.IsSource ? (g.SourceRecovered ? "源头已回收" : "异常源头") : n.Invaded ? "! 受入侵" : operating.Contains(n.Id) ? "● 有效营业" : n.Built ? "设施已建" : KindLabel(n.Kind), n.X, n.Y, operating.Contains(n.Id) || n.IsHeadquarters, n.Id == _selected, n.Invaded)),
            g.Edges.SelectMany(e => g.Forecast.Any(f => f.EdgeId == e.Id)
                ? g.Forecast.Where(f => f.EdgeId == e.Id).Select(f => new MapLink(f.SourceId, f.TargetId, e.Passable, true))
                : new[] { new MapLink(e.From, e.To, e.Passable) }));
        map.Selected += id => { _selected = id; Render(); }; center.Body.AddChild(map);
        center.Body.AddChild(Text($"交接目标   {(g.SourceRecovered ? "✓" : "○")} 源头交付    {(operating.Count >= g.RequiredStations ? "✓" : "○")} 有效营业 {operating.Count} / {g.RequiredStations}", 20, Mint));
        foreach (var f in g.Forecast) center.Body.AddChild(Text("今夜预告  →  " + f.Description, 15, Amber));
        if (g.Forecast.Count == 0) center.Body.AddChild(Text("今夜暂无已预告的扩张。现场与运输规则仍然有效。", 15, Muted));
        if (g.LastDaySummary.Count > 0)
        {
            center.Body.AddChild(Text("上一日结算", 17, Mint));
            foreach (var line in g.LastDaySummary) center.Body.AddChild(Text(line, 14, Muted));
        }
        foreach (var lamp in g.Items.Where(i => i.Kind == "Lamp" && i.Location == ItemLocation.Warehouse))
        {
            center.Footer.AddChild(Text(lamp.Name + (lamp.Sealed ? $" · 已封装 · 公开收购 {lamp.Value}" : " · 未封装暂存 · 尚不能出售或支援"), 16, Amber));
            var disposal = new HBoxContainer(); center.Footer.AddChild(disposal);
            if (lamp.Sealed)
            {
                disposal.AddChild(Button(lamp.Disposition == "Sell" ? "已登记日终出售" : $"登记日终出售 +{lamp.Value}", () => CityAction(city.SellItem(lamp.Id))));
                disposal.AddChild(Button("保留实物", () => CityAction(city.KeepItem(lamp.Id))));
            }
            else disposal.AddChild(Button("安排总部补封装", () => CityAction(city.ScheduleHeadquartersSeal(lamp.Id))));
        }

        var right = Pane("地点档案 / " + KindLabel(node.Kind), 340); row.AddChild(right.Panel);
        right.Body.AddChild(Text(node.Name, 27)); right.Body.AddChild(Text(node.Description, 15, Muted));
        right.Body.AddChild(Text(node.PublicCooperation ? "公开合作：支付启动投入后施工。" : $"调查里程碑 +{node.SurveyReward}  /  实物交付 +{node.RecoveryReward}\n接管需要完成合同资格，再投入 4 资金。", 15, Amber));
        string path = string.Join(" → ", city.FindRoute(node.Id).Select(id => g.Node(id).Name));
        right.Body.AddChild(Text("登记路线\n" + (path.Length == 0 ? "无可用路线" : path), 14, Muted));
        if (node.Built)
        {
            right.Body.AddChild(Text($"设施保留 · 建成于第 {node.BuiltDay} 日\n{(node.Invaded ? "需要维护" : "维护状态正常")}", 15, Mint));
            bool staffed = g.Stations.ContainsKey(node.Id);
            right.Body.AddChild(Button(staffed ? "撤回驻站员工" : "安排一名驻站员工", () =>
            {
                var worker = g.Employees.FirstOrDefault(e => e.RecoverUntilDay <= g.Day && !g.Stations.Values.Contains(e.Id) && !g.Plan.Any(a => a.EmployeeIds.Contains(e.Id)));
                CityAction(staffed ? city.AssignStation(node.Id, null) : worker is null ? ActionResult.Fail("没有空闲员工可驻站") : city.AssignStation(node.Id, worker.Id));
            }));
            foreach (var edge in g.Edges.Where(e => e.From == node.Id || e.To == node.Id))
            {
                var e = edge;
                right.Body.AddChild(Button((node.BlockadeEdgeId == e.Id ? "✓ " : "○ ") + "封控 " + e.Name, () => CityAction(city.SetBlockade(node.Id, node.BlockadeEdgeId == e.Id ? null : e.Id))));
            }
        }
        if (!node.IsHeadquarters)
        {
            foreach (var kind in new[] { JobKind.Manual, JobKind.Survey, JobKind.ContrastSurvey, JobKind.Recovery, JobKind.Build, JobKind.Maintenance, JobKind.Repair })
            {
                var k = kind; var preview = city.PreviewTask(k, node.Id);
                if (k == JobKind.ContrastSurvey && !node.RuleIds.Any(id => g.Rule(id).Trigger == RuleTrigger.MirrorSearch)) continue;
                var button = Button($"{JobLabel(k)}  /  {preview.Cost} 资金", () => CityAction(city.AddTask(k, node.Id)));
                button.Disabled = !preview.Allowed || g.Phase != DayPhase.Preparation; button.TooltipText = preview.Text; right.Body.AddChild(button);
            }
        }
        else
        {
            right.Body.AddChild(Button("补充唯一封装工具 · 3", () => CityAction(city.ScheduleSupply())));
            foreach (var item in g.Items.Where(i => i.Location == ItemLocation.Warehouse && i.RequiresSealing && !i.Sealed && !i.Required))
                right.Body.AddChild(Button("总部补封装 · " + item.Name, () => CityAction(city.ScheduleHeadquartersSeal(item.Id))));
        }
        foreach (var rule in g.Rules.Where(r => node.RuleIds.Contains(r.Id))) AddRule(right.Body, rule, false);
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

    private void RenderField()
    {
        var g = _game!; var f = g.Field!; var site = g.Node(f.NodeId); var engine = new FieldEngine(g);
        if (!site.Rooms.Any(r => r.Id == _viewedRoom && r.Placed)) _viewedRoom = f.RoomId;
        var room = site.Rooms.First(r => r.Id == f.RoomId);
        var row = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 14); _root.AddChild(row);
        var left = Pane("现场状态", 260); row.AddChild(left.Panel);
        left.Body.AddChild(Text(site.Name, 26));
        left.Body.AddChild(Text($"第 {f.Round} / {f.RoundLimit} 轮\n行动 {f.Actions} / 3", 24, Mint));
        left.Body.AddChild(Text($"心志  {f.Will} / {f.MaxWill}\n护符  {f.Wards} / 3    异动  {f.Disturbance} / 8\n灯光  {(f.Light ? "亮" : "灭")}    墨水  {f.Ink}\n现场零钱  {f.Coins}", 18));
        left.Body.AddChild(Text("异动达 8：立即损失 3 心志并回落 4；护符不能抵挡。", 13, Amber));
        left.Body.AddChild(new HSeparator());
        left.Body.AddChild(Text("当前模仿参考", 15, Mint));
        left.Body.AddChild(Text(f.MimicReference is { } reference ? CardCatalog.Get(reference).Name + " · 作用于 " + room.Name : "尚无参考", 17));
        left.Body.AddChild(Text("疾走和点灯支援保留参考。\n徒手搜查不会建立参考。", 13, Muted));
        left.Body.AddChild(Text("委托目标", 15, Mint));
        left.Body.AddChild(Text($"{(site.RuleIds.All(id => g.Rule(id).Confirmed) ? "✓" : "○")} 确认地点规程\n{(g.Items.Any(i => i.Id == site.TargetItemId && i.Location == ItemLocation.Backpack) ? "✓" : "○")} 携带合同实物\n○ 返回入口 · 归队交付", 15));
        left.Footer.AddChild(Button("结束本轮 · 查看预览", () => { _pendingAction = null; _pendingEndRound = true; Render(); }, true));
        var withdraw = Button("从入口免费撤离", () => ConfirmDialog("撤离现场", TransportText(), () => FinishFieldAction(engine.Withdraw(), true)));
        withdraw.Disabled = !room.IsEntrance; left.Footer.AddChild(withdraw);
        left.Footer.AddChild(Text("轮末、完整归队和确认奖励后建立检查点。", 12, Muted));

        var center = Pane("现场平面 / " + room.Name, 0); center.Panel.SizeFlagsHorizontal = SizeFlags.ExpandFill; row.AddChild(center.Panel);
        center.Body.AddChild(Text("绿色：队伍位置    金框：查看目标    点击地图仅查看", 13, Muted));
        var map = new NetworkMap(); var rooms = site.Rooms.Where(r => r.Placed).ToList();
        float minX = rooms.Min(r => r.X), maxX = rooms.Max(r => r.X), minY = rooms.Min(r => r.Y), maxY = rooms.Max(r => r.Y);
        map.Configure(rooms.Select(r => new MapPoint(r.Id, r.Name, r.Id == f.RoomId ? "● 队伍在此" : r.IsEntrance ? "免费撤离入口" : r.Entered ? "已进入" : "未进入", (r.X - minX) / Math.Max(1, maxX - minX), (r.Y - minY) / Math.Max(1, maxY - minY), r.Id == f.RoomId, r.Id == _viewedRoom)),
            rooms.SelectMany(r => r.Neighbors.Where(n => rooms.Any(p => p.Id == n)).Select(n => new MapLink(r.Id, n, true))));
        map.CustomMinimumSize = new Vector2(350, 240);
        map.Selected += id =>
        {
            _viewedRoom = id;
            if (_pendingAction?.Card == CardKind.Sprint) _pendingAction = CardAction(CardKind.Sprint, _pendingAction.HandSlotIndex);
            else if (_pendingAction?.Kind == FieldActionKind.Move) _pendingAction.TargetId = id;
            Render();
        };
        center.Body.AddChild(map);
        var target = site.Rooms.First(r => r.Id == _viewedRoom);
        center.Footer.AddChild(Text("查看：" + target.Name + "  /  队伍：" + room.Name, 16, Amber));
        var actions = new HBoxContainer(); actions.AddThemeConstantOverride("separation", 8); center.Footer.AddChild(actions);
        actions.AddChild(ActionButton("移动至此 · 1", new FieldAction { Kind = FieldActionKind.Move, TargetId = target.Id }));
        actions.AddChild(ActionButton("徒手搜查 · 2", new FieldAction { Kind = FieldActionKind.Search }));
        foreach (var unplaced in site.Rooms.Where(r => !r.Placed && r.Neighbors.Contains(room.Id)))
            actions.AddChild(ActionButton("放置侧房 · 1", new FieldAction { Kind = FieldActionKind.PlaceRoom, TargetId = unplaced.Id }));
        center.Footer.AddChild(Text("手牌  /  选牌后在右侧执行；非移动牌作用于当前位置", 14, Mint));
        var cards = new HBoxContainer(); cards.AddThemeConstantOverride("separation", 8); center.Footer.AddChild(cards);
        var slots = f.HandSlots.Count == 5 ? f.HandSlots : f.Hand.Take(5).Select(c => (CardKind?)c).Concat(Enumerable.Repeat<CardKind?>(null, 5)).Take(5).ToList();
        for (int index = 0; index < 5; index++)
        {
            int slot = index; var kind = slots[index];
            var card = new Button { CustomMinimumSize = new Vector2(0, 132), SizeFlagsHorizontal = SizeFlags.ExpandFill, ClipText = true };
            bool selected = _pendingAction is { Kind: FieldActionKind.PlayCard } && _pendingAction.HandSlotIndex == slot;
            card.AddThemeStyleboxOverride("normal", Box(kind is null ? new Color("101b22") : new Color("1b3038"), selected ? Amber : new Color("405659"), selected ? 2 : 1, 7));
            var label = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            card.AddChild(label); label.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            label.OffsetLeft = 9; label.OffsetRight = -9; label.OffsetTop = 10; label.OffsetBottom = -10;
            if (kind is { } value)
            {
                var definition = CardCatalog.Get(value); var fa = CardAction(value, slot); var preview = engine.Preview(fa);
                label.AddChild(Text(definition.Name + $"  {definition.ActionCost}", 19, selected ? Amber : Ink));
                label.AddChild(Text(CardSummary(value), 13, Muted));
                if (definition.Exhausts) label.AddChild(Text("每次出勤耗尽", 12, Amber));
                card.TooltipText = preview.Allowed ? definition.Description : preview.Text;
                card.Disabled = !preview.Allowed;
                card.Pressed += () => PreviewAction(fa);
            }
            else { label.AddChild(Text("已使用", 16, Muted)); label.AddChild(Text("轮末补牌", 13, Muted)); card.Disabled = true; }
            cards.AddChild(card);
        }
        // Cards beyond the ordinary hand never resize or displace its five physical slots.
        if (f.Hand.Count > slots.Count(c => c.HasValue))
        {
            var remaining = f.Hand.ToList(); foreach (var c in slots.Where(c => c.HasValue)) remaining.Remove(c!.Value);
            var borrowed = new HBoxContainer(); center.Footer.AddChild(borrowed);
            foreach (var c in remaining) borrowed.AddChild(ActionButton("额外 · " + CardCatalog.Get(c).Name, CardAction(c)));
        }
        center.Footer.AddChild(Text($"牌库 {f.DrawPile.Count}  ·  弃牌 {f.Discard.Count}  ·  耗尽 {f.Exhausted.Count}", 12, Muted));

        var rightPanel = new PanelContainer { CustomMinimumSize = new Vector2(370, 0) };
        rightPanel.AddThemeStyleboxOverride("panel", Box(Panel, new Color("2b3e44"), 1)); row.AddChild(rightPanel);
        var right = new VBoxContainer(); right.AddThemeConstantOverride("separation", 10); rightPanel.AddChild(right);
        right.AddChild(Text("行动预览", 16, Mint));
        var previewScroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 220), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        right.AddChild(previewScroll);
        var previewText = Text(_pendingEndRound ? engine.PreviewEndRound().Text : _pendingAction is { } pending ? engine.Preview(pending).Text : "选择一张手牌或基础动作。\n\n这里显示实际目标、成本和本步已知后果。\n未知规程需要通过现场事实验证。", 14);
        previewText.SizeFlagsHorizontal = SizeFlags.ExpandFill; previewScroll.AddChild(previewText);
        var previewOptions = new VBoxContainer { CustomMinimumSize = new Vector2(0, 46) };
        right.AddChild(previewOptions);
        bool search = _pendingAction?.Kind == FieldActionKind.Search || _pendingAction?.Card is CardKind.Search or CardKind.Knock || (_pendingAction?.Card == CardKind.Mimic && f.MimicReference is CardKind.Search or CardKind.Knock);
        if (search && _pendingAction is { } searchAction)
        {
            var accept = new CheckButton { Text = "取得物品时收入背包", ButtonPressed = searchAction.AcceptItem };
            accept.Toggled += value => { searchAction.AcceptItem = value; Render(); }; previewOptions.AddChild(accept);
        }
        else if (_pendingAction is { Card: CardKind.Seal } sealingAction)
        {
            var targets = engine.Backpack().Where(i => i.RequiresSealing && !i.Sealed).ToList();
            var chooseTarget = new OptionButton { CustomMinimumSize = new Vector2(0, 43) };
            foreach (var item in targets) chooseTarget.AddItem("封装对象：" + item.Name);
            int selected = targets.FindIndex(i => i.Id == sealingAction.TargetId);
            if (selected >= 0) chooseTarget.Select(selected);
            chooseTarget.ItemSelected += index => { sealingAction.TargetId = targets[(int)index].Id; Render(); };
            previewOptions.AddChild(chooseTarget);
        }
        else if (_pendingAction is { Kind: FieldActionKind.Reveal } revealingAction)
        {
            var targets = engine.ApplicableRules().Where(r => !r.Confirmed).ToList();
            var chooseTarget = new OptionButton { CustomMinimumSize = new Vector2(0, 43) };
            foreach (var rule in targets) chooseTarget.AddItem("揭晓对象：" + rule.Name);
            int selected = targets.FindIndex(r => r.Id == revealingAction.TargetId);
            if (selected >= 0) chooseTarget.Select(selected);
            chooseTarget.ItemSelected += index => { revealingAction.TargetId = targets[(int)index].Id; Render(); };
            previewOptions.AddChild(chooseTarget);
        }
        var execute = Button(_pendingEndRound ? "执行轮末结算 →" : "执行选定行动 →", ExecuteSelected, true);
        execute.Disabled = _pendingEndRound ? !engine.PreviewEndRound().Allowed : _pendingAction is null || !engine.Preview(_pendingAction).Allowed;
        right.AddChild(execute);
        var tabs = new HBoxContainer(); right.AddChild(tabs);
        foreach (var (id, name) in new[] { ("evidence", "证据"), ("bag", "背包"), ("log", "日志") })
        {
            var tab = Button(name, () => { _fieldTab = id; Render(); }, _fieldTab == id); tab.SizeFlagsHorizontal = SizeFlags.ExpandFill; tabs.AddChild(tab);
        }
        var detailScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        right.AddChild(detailScroll);
        var details = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; details.AddThemeConstantOverride("separation", 10); detailScroll.AddChild(details);
        if (_fieldTab == "evidence")
        {
            foreach (var rule in g.Rules.Where(r => site.RuleIds.Contains(r.Id) || r.Scope == RuleScope.City)) AddRule(details, rule, true);
            if (f.RevealCharges > 0) details.AddChild(ActionButton("稀有揭晓", new FieldAction { Kind = FieldActionKind.Reveal, TargetId = site.RuleIds.FirstOrDefault(id => !g.Rule(id).Confirmed) ?? "" }));
        }
        else if (_fieldTab == "bag")
        {
            var bag = engine.Backpack().ToList();
            details.AddChild(Text($"背包  {bag.Sum(i => i.Size)} / {f.Capacity} 格", 19, Mint));
            if (!string.IsNullOrEmpty(f.AssignedLightSupportId)) details.AddChild(ActionButton(f.LightSupportUsed ? "点灯支援已使用" : "使用驻点点灯支援 · 0", new FieldAction { Kind = FieldActionKind.SupportLight }));
            foreach (var item in bag)
            {
                details.AddChild(Text(item.Name + $" · {item.Size} 格\n" + (item.RequiresSealing ? item.Sealed ? "已封装" : "未封装" : item.Kind == "SealKit" ? $"余量 {item.Charges} / {item.MaxCharges}" : "随行工具"), 15));
                details.AddChild(Text(item.Description, 13, Muted));
                if (!item.Sealed && item.RequiresSealing) details.AddChild(ActionButton("基础封装 · 2", new FieldAction { Kind = FieldActionKind.Seal, TargetId = item.Id }));
                details.AddChild(ActionButton("放回当前房间", new FieldAction { Kind = FieldActionKind.Drop, TargetId = item.Id }));
            }
            foreach (var item in g.Items.Where(i => i.Location == ItemLocation.Room && i.RoomId == room.Id && i.NodeId == site.Id && i.Discovered))
                details.AddChild(ActionButton("拾取 " + item.Name, new FieldAction { Kind = FieldActionKind.Pickup, TargetId = item.Id }));
        }
        else
        {
            details.AddChild(Text("只记录发生的事件；守则真因由你判断。", 13, Mint));
            foreach (var log in g.Log.AsEnumerable().Reverse()) details.AddChild(Text(log, 13, Muted));
        }
    }

    private static string CardSummary(CardKind card) => card switch
    {
        CardKind.Investigate => "取得一份可靠线索", CardKind.Search => "搜索当前真实库存",
        CardKind.Knock => "取得门内一份库存", CardKind.Light => "本轮照明 · 异动 −1",
        CardKind.Rest => "心志 +2 · 房间限两次", CardKind.Ward => "护符 +1 · 上限 3",
        CardKind.Sprint => "最多两格 · 保留参考", CardKind.Mimic => "执行参考的基础效果",
        CardKind.Seal => "封装一件背包实物", CardKind.Banish => "驱离当前普通敌影", _ => ""
    };

    private FieldAction CardAction(CardKind kind, int slot = -1)
    {
        var g = _game!; var f = g.Field!;
        var action = new FieldAction { Kind = FieldActionKind.PlayCard, Card = kind, HandSlotIndex = slot };
        if (kind == CardKind.Sprint)
        {
            action.TargetId = _viewedRoom;
            var path = RoomPath(f.RoomId, _viewedRoom);
            action.Path = path.Skip(1).ToList();
        }
        if (kind == CardKind.Seal)
        {
            var targets = g.Items.Where(i => i.Location == ItemLocation.Backpack && i.OwnerId == f.AssignmentId && !i.Sealed && i.RequiresSealing).ToList();
            var engine = new FieldEngine(g);
            var target = targets.FirstOrDefault(i => engine.Preview(new FieldAction { Kind = FieldActionKind.PlayCard, Card = CardKind.Seal, HandSlotIndex = slot, TargetId = i.Id }).Allowed) ?? targets.FirstOrDefault();
            action.TargetId = target?.Id ?? "";
        }
        return action;
    }

    private List<string> RoomPath(string start, string end)
    {
        var rooms = _game!.Node(_game.Field!.NodeId).Rooms; var queue = new Queue<List<string>>(); queue.Enqueue(new() { start }); var visited = new HashSet<string> { start };
        while (queue.Count > 0)
        {
            var path = queue.Dequeue(); if (path[^1] == end) return path;
            foreach (var next in rooms.First(r => r.Id == path[^1]).Neighbors.Where(id => rooms.Any(r => r.Id == id && r.Placed)))
                if (visited.Add(next)) queue.Enqueue(path.Concat(new[] { next }).ToList());
        }
        return new();
    }

    private Button ActionButton(string label, FieldAction action)
    {
        var preview = new FieldEngine(_game!).Preview(action);
        var b = Button(label, () => PreviewAction(action)); b.Disabled = !preview.Allowed;
        b.TooltipText = preview.Allowed ? "选择后在右侧查看后果并执行。" : preview.Text; return b;
    }

    private void PreviewAction(FieldAction action)
    {
        _pendingAction = action; _pendingEndRound = false; Render();
    }

    private void ExecuteSelected()
    {
        var engine = new FieldEngine(_game!);
        if (_pendingEndRound)
        {
            var preview = engine.PreviewEndRound();
            if (!preview.Allowed) { _notice = preview.Text; Render(); return; }
            void ExecuteRound() { _pendingEndRound = false; FinishFieldAction(engine.EndRound(), true); }
            if (preview.RequiresConfirmation) ConfirmDialog("已知立即失败风险", preview.Text, ExecuteRound); else ExecuteRound();
            return;
        }
        if (_pendingAction is not { } action) return;
        var p = engine.Preview(action);
        if (!p.Allowed) { _notice = p.Text; Render(); return; }
        void ExecuteAction() { _pendingAction = null; FinishFieldAction(engine.Execute(action)); }
        if (p.RequiresConfirmation || action.Kind == FieldActionKind.Reveal)
            ConfirmDialog(action.Kind == FieldActionKind.Reveal ? "消耗唯一揭晓资源" : "已知立即失败风险", p.Text, ExecuteAction);
        else ExecuteAction();
    }

    private void FinishFieldAction(ActionResult result, bool checkpoint = false)
    {
        var g = _game!;
        _notice = result.Message;
        if (g.Field is { Finished: true })
        {
            var done = new CityEngine(g).CompleteManual(); _notice += " · " + done.Message;
            Save($"第 {g.Day} 日 · 出勤已归队，待统一日结"); _page = "dispatch";
        }
        else if (result.Success && checkpoint) Save($"{g.Node(g.Field!.NodeId).Name} · 第 {g.Field.Round} 轮完整检查点");
        Render();
    }

    private void AddRule(VBoxContainer body, RuleState rule, bool field)
    {
        body.AddChild(Text(rule.Confirmed ? "✓ " + rule.Answer : "? " + rule.Name, 16, rule.Confirmed ? Mint : Amber));
        if (!string.IsNullOrEmpty(rule.Notice)) body.AddChild(Text("原始告示（未经核实）：" + rule.Notice, 13, Muted));
        if (rule.EvidenceFound && (field || rule.Archived)) body.AddChild(Text("可靠线索：" + rule.Clue, 14));
        else if (rule.Observed && (field || rule.Archived)) body.AddChild(Text("已有实际触发观察，详情见行动记录；可据此验证。", 13, Muted));
        else body.AddChild(Text(field ? "尚无可靠线索；可用调查牌取得。" : "尚无已归档的可靠线索。", 13, Muted));
        if (field || rule.Archived) foreach (var evidence in rule.EvidenceRecords.Where(e => e != rule.Clue)) body.AddChild(Text("记录：" + evidence, 13, Muted));
        if (rule.Confirmed) return;
        bool eligible = field ? rule.EvidenceFound || rule.Observed : rule.Archived;
        if (!eligible) return;
        for (int index = 0; index < rule.Candidates.Count; index++)
        {
            int candidate = index; bool wrong = rule.WrongCandidates.Contains(index);
            string candidateText = (wrong ? "已排除 · " : "验证 · ") + rule.Candidates[index];
            var button = Button("", () =>
            {
                var result = field ? new FieldEngine(_game!).Confirm(rule.Id, candidate) : new CityEngine(_game!).ConfirmRule(rule.Id, candidate);
                if (field) FinishFieldAction(result, result.Success); else CityAction(result);
            });
            button.Disabled = wrong || rule.LastCityAttemptDay == _game!.Day || (field ? _game.Field!.LastConfirmationRound == _game.Field.Round : _game.Phase != DayPhase.Preparation);
            button.CustomMinimumSize = new Vector2(0, 76); button.TooltipText = rule.Candidates[index];
            var candidateLabel = Text(candidateText, 14, button.Disabled ? Muted : Ink);
            candidateLabel.VerticalAlignment = VerticalAlignment.Center; button.AddChild(candidateLabel);
            candidateLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            candidateLabel.OffsetLeft = 12; candidateLabel.OffsetRight = -12; candidateLabel.OffsetTop = 6; candidateLabel.OffsetBottom = -6;
            button.AddThemeFontSizeOverride("font_size", 14); body.AddChild(button);
        }
    }

    private string TransportText()
    {
        var g = _game!; var f = g.Field!;
        var city = new CityEngine(g);
        var items = g.Items.Where(i => i.Location == ItemLocation.Backpack && i.OwnerId == f.AssignmentId).Select(i => i.Id);
        return "从入口撤离不消耗行动。实物与零钱归队后登记，合同在统一日结时付款。\n\n" + city.PreviewTransport(items, city.FindRoute(f.NodeId).AsEnumerable().Reverse()).Text;
    }

    private string EndRoundText()
    {
        return new FieldEngine(_game!).PreviewEndRound().Text;
    }

    private void RenderArchive()
    {
        var pane = Pane("守则档案 / 现场证据在日终归档", 0); pane.Panel.SizeFlagsVertical = SizeFlags.ExpandFill; _root.AddChild(pane.Panel);
        foreach (var node in _game!.Nodes.Where(n => n.RuleIds.Count > 0))
        {
            pane.Body.AddChild(Text(node.Name, 25, Mint));
            foreach (var id in node.RuleIds) AddRule(pane.Body, _game.Rule(id), false);
            pane.Body.AddChild(new HSeparator());
        }
        foreach (var rule in _game.Rules.Where(r => r.Scope != RuleScope.Local && !_game.Nodes.Any(n => n.RuleIds.Contains(r.Id)))) AddRule(pane.Body, rule, false);
    }

    private void RenderLedger()
    {
        var row = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; _root.AddChild(row);
        var ledger = Pane("资金流水", 580); row.AddChild(ledger.Panel);
        ledger.Body.AddChild(Text("预付、合同、营业与基本支出分别入账。", 17, Muted));
        foreach (var e in _game!.Ledger.AsEnumerable().Reverse()) ledger.Body.AddChild(Text($"第 {e.Day} 日  {e.Category}   {e.Amount:+0;-0;0}   →  余额 {e.Balance}\n{e.Description}", 16, e.Amount >= 0 ? Mint : Ink));
        var logs = Pane("行动与结算记录", 0); logs.Panel.SizeFlagsHorizontal = SizeFlags.ExpandFill; row.AddChild(logs.Panel);
        foreach (var line in _game.Log.AsEnumerable().Reverse()) logs.Body.AddChild(Text(line, 16));
    }

    private void RenderTools()
    {
        var row = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; _root.AddChild(row);
        var deck = Pane("队伍基础牌组", 600); row.AddChild(deck.Panel);
        deck.Body.AddChild(Text($"当前 {_game!.TeamDeck.Count} 张  /  验证范围 10–16 张", 22, Mint));
        deck.Body.AddChild(Text("普通配置作为队伍模板复用。模仿上限 1 张，每次出勤耗尽。调整仅在准备阶段生效。", 15, Muted));
        foreach (var card in CardCatalog.All)
        {
            var r = new HBoxContainer(); deck.Body.AddChild(r);
            var text = Text($"{card.Name} ×{_game.TeamDeck.Count(c => c == card.Id)}  ·  {card.Description}", 15); text.SizeFlagsHorizontal = SizeFlags.ExpandFill; r.AddChild(text);
            var minus = Button("−", () => EditDeck(card.Id, false)); minus.Disabled = _game.Phase != DayPhase.Preparation || _game.TeamDeck.Count <= 10 || !_game.TeamDeck.Contains(card.Id); r.AddChild(minus);
            var plus = Button("+", () => EditDeck(card.Id, true)); plus.Disabled = _game.Phase != DayPhase.Preparation || _game.TeamDeck.Count >= 16 || (card.Id == CardKind.Mimic && _game.TeamDeck.Contains(CardKind.Mimic)); r.AddChild(plus);
        }
        var inventory = Pane("公司实物库", 0); inventory.Panel.SizeFlagsHorizontal = SizeFlags.ExpandFill; row.AddChild(inventory.Panel);
        var city = new CityEngine(_game);
        foreach (var task in _game.Plan.Where(t => t.Kind == JobKind.Manual))
        {
            inventory.Body.AddChild(Text("随行配置 / " + _game.Node(task.NodeId).Name, 22, Mint));
            inventory.Body.AddChild(Text("按实物选择；空配装可以回访并拾回遗落工具。", 14, Muted));
            foreach (var tool in _game.Items.Where(i => i.Location == ItemLocation.Warehouse && i.Kind is "SealKit" or "SourceCase" or "RevealLens"))
            {
                bool carried = task.ToolIds.Contains(tool.Id);
                var toggle = Button((carried ? "✓ 带入 " : "○ 加入 ") + tool.Name + $" · 余 {tool.Charges}", () =>
                {
                    var nextTools = task.ToolIds.ToList();
                    if (carried) nextTools.Remove(tool.Id); else nextTools.Add(tool.Id);
                    CityAction(city.SetTaskTools(task.Id, nextTools));
                });
                toggle.Disabled = _game.Phase != DayPhase.Preparation; inventory.Body.AddChild(toggle);
            }
            inventory.Body.AddChild(new HSeparator());
        }
        foreach (var item in _game.Items.Where(i => i.Location is ItemLocation.Warehouse or ItemLocation.Installed))
        {
            var reservedTask = _game.Phase == DayPhase.Preparation
                ? _game.Plan.FirstOrDefault(t => t.ToolIds.Contains(item.Id) || t.SupportItemId == item.Id || t.TargetItemId == item.Id)
                : _game.LockedPlan.FirstOrDefault(t => t.Id == item.ReservedBy);
            inventory.Body.AddChild(Text(item.Name + (reservedTask is null ? "" : " · 已预留"), 20, Mint));
            if (reservedTask is not null) inventory.Body.AddChild(Text("用于 " + _game.Node(reservedTask.NodeId).Name + " · " + JobLabel(reservedTask.Kind), 14, Amber));
            inventory.Body.AddChild(Text(item.Description, 15));
            if (item.Kind == "SealKit")
            {
                inventory.Body.AddChild(Text($"余量 {item.Charges} / {item.MaxCharges} · 可用日 {item.AvailableDay}", 16, Amber));
                inventory.Body.AddChild(Button("安排补充 · 3 资金 / 1 人 / 1 事务", () => CityAction(city.ScheduleSupply(item.Id))));
            }
            if (item.Kind == "Lamp")
            {
                inventory.Body.AddChild(Text($"公开报价 {item.Value} · {(item.Sealed ? "已封装" : "未封装暂存")}\n" + (item.Location == ItemLocation.Installed ? "已安装于 " + _game.Node(item.InstalledNodeId).Name + (item.SupportUsedDay == _game.Day ? " · 今日支援已用" : " · 今日支援未用") : item.Disposition == "Sell" ? "已登记日终出售" : "保留实物"), 16, Amber));
                if (item.Location == ItemLocation.Installed)
                    inventory.Body.AddChild(Button("卸下灯具", () => CityAction(city.UninstallLamp(item.Id))));
                else if (item.Sealed)
                {
                    inventory.Body.AddChild(Button($"登记日终出售 +{item.Value}", () => CityAction(city.SellItem(item.Id))));
                    inventory.Body.AddChild(Button("保留实物 · 取消出售", () => CityAction(city.KeepItem(item.Id))));
                    foreach (var nodeId in city.OperatingNodeIds())
                        inventory.Body.AddChild(Button("安装于 " + _game.Node(nodeId).Name, () => CityAction(city.InstallLamp(item.Id, nodeId))));
                }
                else inventory.Body.AddChild(Button("总部补封装 · 1 人 / 1 事务 / 1 余量", () => CityAction(city.ScheduleHeadquartersSeal(item.Id))));
            }
            if (item.Kind == "Rare" && item.Location == ItemLocation.Warehouse)
                inventory.Body.AddChild(Button("登记实物研究 · 1 人 / 1 事务", () => ConfirmDialog("消耗唯一研究实物", "研究会消耗这件实物，得到的能力在次日生效。", () => CityAction(city.ScheduleResearch(item.Id)))));
            inventory.Body.AddChild(new HSeparator());
        }
        inventory.Body.AddChild(Text("标准回收按规程消耗真实工具实例；不同队伍不能同时占用同一件工具。", 15, Amber));
    }

    private void EditDeck(CardKind kind, bool add)
    {
        if (_game!.Phase != DayPhase.Preparation) return;
        if (add && _game.TeamDeck.Count < 16 && (kind != CardKind.Mimic || !_game.TeamDeck.Contains(kind))) _game.TeamDeck.Add(kind);
        else if (!add && _game.TeamDeck.Count > 10) _game.TeamDeck.Remove(kind);
        Save("准备阶段 · 牌组已调整"); Render();
    }

    private void RenderOutcome()
    {
        var pane = Pane(_game!.Phase == DayPhase.Victory ? "雾港交接完成" : "远征暂停", 0); pane.Panel.SizeFlagsVertical = SizeFlags.ExpandFill; _root.AddChild(pane.Panel);
        pane.Body.AddChild(Text(_game.Phase == DayPhase.Victory ? "这座城市，终于可以交班。" : "每一次失败，都应该留下可靠的记录。", 40, _game.Phase == DayPhase.Victory ? Mint : Amber));
        pane.Body.AddChild(Text(_game.Outcome, 24));
        pane.Body.AddChild(Text($"第 {_game.Day} 日 · 资金 {_game.Money}\n已确认守则 {_game.Rules.Count(r => r.Confirmed)} 条\n已交付实物 {_game.Items.Count(i => i.Location == ItemLocation.Delivered)} 件\n紧急支援 {(_game.EmergencySupportUsed ? "已使用" : "未使用")}", 22));
        pane.Body.AddChild(Text("当前构建到此完成单城验证。三城交接、连续生成验证与跨局成长尚未接入，本版不会将单城成功记作完整远征胜利。", 18, Muted));
        foreach (var line in _game.Log.TakeLast(8)) pane.Body.AddChild(Text(line, 15, Muted));
        pane.Body.AddChild(Button("返回标题", () => { _welcome = true; Render(); }, true));
    }

    private void CityAction(ActionResult result)
    {
        _notice = result.Message;
        if (result.Success) Save(_game!.Phase is DayPhase.Victory or DayPhase.Failed ? "单城结算完成" : $"第 {_game.Day} 日 · {PhaseLabel(_game.Phase)}");
        Render();
    }

    private void Save(string label)
    {
        if (_game is null || _screenshotMode) return;
        _game.LastCheckpoint = label;
        var result = _save.Save(_game);
        if (!result.Success) { _game.LastCheckpoint = _lastSavedCheckpoint; _saveFailure = result.Message; }
        else { _lastSavedCheckpoint = label; _lastSavedAt = _game.SavedAt; _saveFailure = ""; }
    }

    private string ExitMessage() => (_saveFailure.Length > 0 ? "最近一次保存失败，未保存的进度退出后会丢失。\n" : "") +
        (_lastSavedAt is null ? "本次尚无成功保存的检查点。" : "上次成功保存：" + _lastSavedCheckpoint) +
        (_game?.Field is { Finished: false } ? "\n现场回合中尚未保存的动作会回到上一检查点。结束本轮可以先建立完整检查点。" : "\n继续游戏将读取上述完整检查点。");
    private void AskQuit() => ConfirmDialog("退出游戏", ExitMessage(), () => GetTree().Quit());
    private void ShowFieldLog()
    {
        var window = new Window { Title = "完整行动日志 / 查看不消耗行动", Size = new Vector2I(1000, 700), Transient = true, Exclusive = true };
        AddChild(window);
        var panel = Pane("前置状态 → 基础效果 → 规则与护符 → 实际结果", 0);
        window.AddChild(panel.Panel); panel.Panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach (var line in _game!.Log.AsEnumerable().Reverse()) panel.Body.AddChild(Text(line, 17));
        window.CloseRequested += () => window.QueueFree();
        window.PopupCentered();
    }
    private void ShowHelp()
    {
        var d = new AcceptDialog
        {
            Title = "M2 操作手册",
            DialogText = "1. 城市：每天最多 2 个事务，出勤各 2 人，工程各 1 人，最多一队手操。驻站另占一人。\n2. 先选地点加入计划；工具按唯一实物预留。锁定后统一执行、日结，不能中途重排。\n3. 旅馆的旧录像不能直接区分两种假说。看灯光、当前出勤次数和实际反噬记录。\n4. 徒手搜查需 2 行动，搜查牌需 1。第一柜只定位，第二次搜查才能发现合同实物。\n5. 点击地图仅查看；选牌或基础动作后，在右侧预览并执行。已使用卡槽留空到轮末。\n6. 模仿不复制原行为标签，但消耗同一真实库存。疾走与驻点灯光支援保留参考。\n7. 现场每轮 3 行动；结束本轮也先在右侧预览。到入口可免费撤离，需确认归队运输。\n8. 唯一隔离工具最多 2 次。总部补充需 3 资金、1 人、1 事务，且供应耗材只有一份。\n9. 灯具封装后可登记日终出售，或次日安装在有效驻站点。锁定前给手操队预留支援。\n10. 灯具未封装也可合法带回暂存；不能出售或支援，总部补封装仍消耗事务和真实余量。\n11. 医院封装需要照明；源头确认后用专用箱，不能借用或替代便携隔离工具。\n12. 次日归档后才能用新情报安排委派。设施次日起生效，驻站和连通共同决定营业。\n13. 轮末、判断奖励后、归队及日结自动保存完整检查点；退出前会提示最近成功位置。\n\n目标：五日内交付源头，并使两个服务点有效营业。基础账例没有传播，压力场景独立开启。\nM2 存档使用独立目录；三城远征、正式素材和发行包仍未完成。",
            MinSize = new Vector2I(960, 700), OkButtonText = "明白了"
        };
        d.GetLabel().AutowrapMode = TextServer.AutowrapMode.WordSmart;
        AddChild(d); d.Confirmed += () => d.QueueFree(); d.PopupCentered();
    }

    private void ConfirmDialog(string title, string description, Action confirm)
    {
        var d = new ConfirmationDialog { Title = title, DialogText = description, MinSize = new Vector2I(580, 250), OkButtonText = "确认", CancelButtonText = "取消" };
        d.GetLabel().AutowrapMode = TextServer.AutowrapMode.WordSmart;
        d.GetLabel().CustomMinimumSize = new Vector2(540, 0);
        AddChild(d); d.Confirmed += () => { d.QueueFree(); confirm(); }; d.Canceled += () => d.QueueFree(); d.PopupCentered();
    }

    private static Label Text(string text, int size = 17, Color? color = null)
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size); if (color is { } c) label.AddThemeColorOverride("font_color", c); return label;
    }

    private static Button Button(string text, Action action, bool primary = false)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(0, 43), MouseDefaultCursorShape = CursorShape.PointingHand };
        b.Pressed += action;
        if (primary) { b.AddThemeStyleboxOverride("normal", Box(new Color("315b4b"), new Color("79b797"), 1, 5)); b.AddThemeColorOverride("font_color", new Color("eff4db")); }
        return b;
    }

    private static VBoxContainer Stat(string title, string value)
    {
        var v = new VBoxContainer { CustomMinimumSize = new Vector2(125, 0) }; v.AddChild(Text(title, 13, Muted)); v.AddChild(Text(value, 27, Amber)); return v;
    }

    private static (PanelContainer Panel, VBoxContainer Body, VBoxContainer Footer) Pane(string title, float width)
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(width, 0), SizeFlagsVertical = SizeFlags.ExpandFill };
        panel.AddThemeStyleboxOverride("panel", Box(Panel, new Color("2b3e44"), 1, 7));
        var shell = new VBoxContainer(); shell.AddThemeConstantOverride("separation", 12); panel.AddChild(shell);
        shell.AddChild(Text(title, 15, Mint)); shell.AddChild(new HSeparator());
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; shell.AddChild(scroll);
        var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill }; body.AddThemeConstantOverride("separation", 12); scroll.AddChild(body);
        var footer = new VBoxContainer(); footer.AddThemeConstantOverride("separation", 10); shell.AddChild(footer);
        return (panel, body, footer);
    }

    private static string PhaseLabel(DayPhase phase) => phase switch { DayPhase.Preparation => "准备阶段", DayPhase.Locked => "已锁定", DayPhase.Executing => "执行阶段", DayPhase.Settlement => "待日结", DayPhase.Victory => "单城完成", DayPhase.Failed => "结算失败", _ => phase.ToString() };
    private static string KindLabel(string kind) => kind switch { "Hotel" => "旅馆", "Hospital" => "医院", "Hub" => "交通枢纽", "Cooperation" => "合作网点", "Source" => "源头设施", "HQ" => "公司总部", _ => kind };
    private static string JobLabel(JobKind kind) => kind switch { JobKind.Survey => "历史调查", JobKind.ContrastSurvey => "对照调查", JobKind.Supply => "补充封装工具", JobKind.HeadquartersSeal => "总部补封装", JobKind.Research => "实物研究", JobKind.Recovery => "标准回收", JobKind.Manual => "手操出勤", JobKind.Build => "接管施工", JobKind.Maintenance => "常规维护", JobKind.Repair => "线路抢修", JobKind.Recruit => "员工扩编", _ => "未登记事务" };
}
