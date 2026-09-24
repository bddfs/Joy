using Godot;
using Joy.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Joy;

public partial class Main : Control
{
    private static readonly Color Ink = new("e9e6de");
    private static readonly Color Muted = new("a6b0bf");
    private static readonly Color Mint = new("8cbab7");
    private static readonly Color Amber = new("d3ad6b");
    private static readonly Color Panel = new("171e2b");
    private static readonly Color Warning = new("c66d70");
    public static float UiScale { get; private set; } = 1;
    public static bool ReducedMotion { get; private set; } = true;
    public static bool CosmicIntensity { get; private set; }
    private bool _grayscale;
    private bool _mutedAudio;
    private string _interfaceSettingsPath = "user://interface.cfg";
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
        if (args.Contains("--ui-test")) GetWindow().Title = "JOY · 美术与界面 v0.1 验证";
        if (args.Contains("--ui-test")) _interfaceSettingsPath = "user://ui-art-validation-interface.cfg";
        _save = new SaveStore(ProjectSettings.GlobalizePath(_screenshotMode ? "user://preview-m2" : args.Contains("--ui-test") ? "user://ui-art-validation-m2" : "user://profile-m2"));
        GetWindow().MinSize = new Vector2I(1280, 720);
        GetTree().AutoAcceptQuit = false;
        LoadInterfaceSettings(args);
        BuildTheme();
        if (_screenshotMode) PrepareArtCapture(args);
        Render();
        if (_screenshotMode && CaptureCase(args) == "transport") ConfirmDialog("撤离现场", TransportText(), () => { });
        if (_screenshotMode && CaptureCase(args) == "settings") ShowInterfaceSettings();
        if (_screenshotMode) _ = Capture(CaptureName(args));
    }

    private async Task Capture(string name)
    {
        for (int i = 0; i < 12; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var directory = ProjectSettings.GlobalizePath("user://captures");
        DirAccess.MakeDirRecursiveAbsolute(directory);
        var path = System.IO.Path.Combine(directory, name);
        var error = GetViewport().GetTexture().GetImage().SavePng(path);
        SaveUiWitness(path);
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
        Theme.DefaultFont = GD.Load<FontFile>("res://Assets/Fonts/NotoSansCJKsc-Regular.otf");
        Theme.DefaultFontSize = (int)(19 * UiScale);
        Theme.SetColor("font_color", "Label", Ink);
        Theme.SetColor("font_color", "Button", Ink);
        Theme.SetColor("font_disabled_color", "Button", new Color("85909e"));
        Theme.SetStylebox("normal", "Button", Box(new Color("202a39"), new Color("46505f"), 1, 3));
        Theme.SetStylebox("hover", "Button", Box(new Color("303e4a"), Mint, 1, 3));
        Theme.SetStylebox("pressed", "Button", Box(new Color("354952"), Amber, 2, 3));
        Theme.SetStylebox("disabled", "Button", Box(new Color("151d29"), new Color("303b4a"), 1, 3));
        Theme.SetStylebox("focus", "Button", Box(new Color(0, 0, 0, 0), Amber, 2, 3));
        Theme.SetStylebox("normal", "LineEdit", Box(new Color("0e1520"), new Color("46505f"), 1, 3));
        Theme.SetStylebox("focus", "LineEdit", Box(new Color(0, 0, 0, 0), Mint, 2, 3));
        Theme.SetColor("font_color", "LineEdit", Ink);
        foreach (var (state, color) in new[] { ("scroll", new Color("101622")), ("grabber", new Color("627681")), ("grabber_highlight", Mint), ("grabber_pressed", Amber) })
        {
            var rail = Box(color, radius: 2);
            rail.ContentMarginLeft = rail.ContentMarginRight = 5;
            rail.ContentMarginTop = rail.ContentMarginBottom = 0;
            Theme.SetStylebox(state, "VScrollBar", rail);
        }
        Theme.SetStylebox("panel", "PopupPanel", Box(Panel, Mint, 1));
        Theme.SetStylebox("panel", "Window", Box(Panel, Mint, 1));
    }

    public void Render()
    {
        foreach (var child in GetChildren())
        {
            // Embedded windows can still be dispatching the click that requested this redraw.
            // Keep their viewport in the tree until the deferred deletion completes.
            if (child is Window window) window.Hide(); else RemoveChild(child);
            child.QueueFree();
        }
        AddArtBackdrop();
        var margin = new MarginContainer();
        AddChild(margin); margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach (var side in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + side, 24);
        _root = new VBoxContainer(); _root.AddThemeConstantOverride("separation", 12); margin.AddChild(_root);
        if (_welcome) { RenderWelcome(); AddAccessibilityOverlay(); return; }
        if (_captureItemGallery) { RenderItemGallery(); AddAccessibilityOverlay(); return; }
        RenderHeader();
        if (_game!.Phase is DayPhase.Victory or DayPhase.Failed) RenderOutcome();
        else if (_game.Field is { Finished: false }) RenderField();
        else if (_page == "archive") RenderArchive();
        else if (_page == "ledger") RenderLedger();
        else if (_page == "tools") RenderTools();
        else RenderCity();
        var footer = new HBoxContainer(); _root.AddChild(footer);
        var message = Text(_saveFailure.Length > 0 ? _saveFailure : _notice.Length > 0 ? _notice : "先观察，再判断。把未知留在档案里。", 14, Amber);
        message.MaxLinesVisible = 2; message.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        message.TooltipText = message.Text;
        message.SizeFlagsHorizontal = SizeFlags.ExpandFill; footer.AddChild(message);
        var version = Text("M2 / ART–01", 13, Muted);
        version.AutowrapMode = TextServer.AutowrapMode.Off;
        footer.AddChild(version);
        AddAccessibilityOverlay();
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
        var card = Pane("雾港分局  /  新的城市委托", 510 + (UiScale - 1) * 140); row.AddChild(card.Panel);
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
        card.Body.AddChild(Button("显示与辅助设置", ShowInterfaceSettings));
        card.Body.AddChild(Text("M2：镜中住客、共享封装工具与灯具处置。基础场景用于核对五日资源；传播压力可独立开启。旧版存档保留在原目录。", 14, Muted));
    }

    private void RenderHeader()
    {
        var g = _game!;
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 24); _root.AddChild(row);
        var brand = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        brand.AddChild(Text("异常回收公司", 29)); brand.AddChild(Text("A R B  /  雾港分局", 13, Mint)); row.AddChild(brand);
        row.AddChild(Stat("经营日", $"{g.Day:00} / {g.Deadline:00}"));
        row.AddChild(Stat("公司资金", $"{g.Money:00}"));
        row.AddChild(Stat("现场状态", g.Field is { Finished: false } ? "出勤中" : PhaseLabel(g.Phase)));
        row.AddChild(Button("显示", ShowInterfaceSettings)); row.AddChild(Button("手册", ShowHelp)); row.AddChild(Button("标题", () => ConfirmDialog("返回标题", ExitMessage(), () => { _welcome = true; _notice = ""; Render(); })));
        var nav = new HBoxContainer(); nav.AddThemeConstantOverride("separation", 10); _root.AddChild(nav);
        foreach (var (id, label) in new[] { ("dispatch", "01  城市调度"), ("archive", "02  守则档案"), ("tools", "03  工具配装"), ("ledger", "04  收支记录") })
        {
            var b = Button(label, () => { _page = id; Render(); }, _page == id); b.Disabled = g.Field is { Finished: false }; nav.AddChild(b);
        }
        var saved = Text(_lastSavedAt is not null ? _lastSavedCheckpoint + $" · 已保存 {_lastSavedAt.Value.ToLocalTime():HH:mm:ss}" : "尚未保存", 14, Muted);
        saved.SizeFlagsHorizontal = SizeFlags.ExpandFill; saved.HorizontalAlignment = HorizontalAlignment.Right; saved.MaxLinesVisible = 2; nav.AddChild(saved);
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
        row.AddThemeConstantOverride("separation", 16);
        var deck = Pane("行动档案 / 牌组配置", 610); row.AddChild(deck.Panel);
        deck.Body.AddChild(Text($"当前 {_game!.TeamDeck.Count} 张  /  验证范围 10–16 张", 22, Mint));
        deck.Body.AddChild(Text("普通配置作为队伍模板复用。模仿上限 1 张，每次出勤耗尽。调整仅在准备阶段生效。", 15, Muted));
        foreach (var card in CardCatalog.All)
        {
            var r = new HBoxContainer(); r.AddThemeConstantOverride("separation", 10); deck.Body.AddChild(r);
            var art = ArtAssets.Image(card.Id == CardKind.Light ? "card-light" : card.Id == CardKind.Mimic ? "card-mimic" : "card-investigate", 68);
            art.CustomMinimumSize = new Vector2(78, 68); art.SizeFlagsHorizontal = SizeFlags.ShrinkBegin; r.AddChild(art);
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
            foreach (var tool in _game.Items.Where(i => i.Location == ItemLocation.Warehouse && i.Kind is "SealKit" or "SourceCase" or "RevealLens" or WardClipCatalog.Kind))
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
            inventory.Body.AddChild(ItemDossier(item));
            if (reservedTask is not null) inventory.Body.AddChild(Text("用于 " + _game.Node(reservedTask.NodeId).Name + " · " + JobLabel(reservedTask.Kind), 14, Amber));
            inventory.Body.AddChild(Text(item.Description, 15));
            if (item.Kind == WardClipCatalog.Kind)
                inventory.Body.AddChild(Text($"整局余量 {item.Charges} / {item.MaxCharges} · 占 {item.Size} 格 · 可用日 {item.AvailableDay}\n仅供手操配装；归库、换队和读档均不补次数。", 16, Amber));
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
            if (item.Kind is "Lamp" or "Anomaly" && !item.Required && item.Sealed && item.Location == ItemLocation.Warehouse)
                inventory.Body.AddChild(Button("登记实物研究 · 1 人 / 1 事务", () => ConfirmDialog("消耗唯一研究实物", "日终研究会消耗这件已封装可选实物，留下研究档案；当前方案不附加现金或能力。已安装、出售或预留的同一实物不能重复处置。", () => CityAction(city.ScheduleResearch(item.Id)))));
            inventory.Body.AddChild(new HSeparator());
        }
        inventory.Footer.AddChild(Button("查看已交付 / 出售 / 消耗的实物", ShowItemHistory));
        inventory.Footer.AddChild(Text("同一主体代表同一实物；预留不消耗次数，执行后按实际记录扣减。", 15, Amber));
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
        var g = _game;
        pane.Body.AddChild(new HSeparator());
        pane.Body.AddChild(Text("最后一日 / 实际结算顺序", 24, Mint));
        string timeline;
        if (g.SettlementTimeline.Count > 0)
        {
            foreach (var stage in g.SettlementTimeline)
                pane.Body.AddChild(Text($"{stage.Sequence:00}  {stage.Title}  ·  余额 {stage.MoneyBefore} → {stage.MoneyAfter}", 18));
            timeline = string.Join("\n\n", g.SettlementTimeline.Select(stage =>
                $"{stage.Sequence:00}  {stage.Title}\n第 {stage.Day} 日 · 余额 {stage.MoneyBefore} → {stage.MoneyAfter}\n" +
                (stage.Events.Count == 0 ? "本阶段没有新增事件。" : string.Join("\n", stage.Events))));
        }
        else
        {
            timeline = g.LastDaySummary.Count == 0 ? "此存档未记录结算时间线。" :
                string.Join("\n\n", g.LastDaySummary.Select((line, index) => $"{index + 1:00}  {line}"));
            pane.Body.AddChild(Text("此存档没有分阶段记录；可打开保留的原始日结摘要。", 18, Muted));
        }
        string ledger = g.Ledger.Count == 0 ? "尚无资金流水。" : string.Join("\n\n", g.Ledger.Select(e =>
            $"第 {e.Day} 日 · {e.Category}  {e.Amount:+0;-0;0} → 余额 {e.Balance}\n{e.Description}"));
        string logs = g.Log.Count == 0 ? "尚无行动记录。" : string.Join("\n\n", g.Log);
        pane.Footer.AddChild(Text("以下记录按原顺序展示；回查不会改变终局、现金或实物。", 15, Muted));
        var review = new HBoxContainer(); review.AddThemeConstantOverride("separation", 12); pane.Footer.AddChild(review);
        review.AddChild(Button("完整结算时间线", () => ShowLongText("终局 / 完整结算时间线", timeline)));
        review.AddChild(Button("全部资金流水", () => ShowLongText("终局 / 资金流水", ledger)));
        review.AddChild(Button("完整行动记录", () => ShowLongText("终局 / 行动记录", logs)));
        review.AddChild(Button("返回标题", () => { _welcome = true; Render(); }, true));
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
        string helpText = d.DialogText.Replace("M2 存档使用独立目录；三城远征、正式素材和发行包仍未完成。", "M2 存档使用独立目录；当前为单城美术候选版，完整三城远征尚未实现。显示设置支持文字缩放、灰度和静态模式。");
        d.Free();
        ShowLongText("M2 操作手册 / 查看不消耗行动", helpText);
    }

    private void ConfirmDialog(string title, string description, Action confirm)
    {
        var window = new Window { Title = title, Size = new Vector2I(960, 700), Transient = true, Exclusive = true };
        AddChild(window);
        var pane = Pane(title, 0); window.AddChild(pane.Panel); pane.Panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        pane.Body.AddChild(Text(description));
        var actions = new HBoxContainer(); pane.Footer.AddChild(actions);
        var cancel = Button("取消 · 返回原操作", () => window.QueueFree()); cancel.SizeFlagsHorizontal = SizeFlags.ExpandFill; actions.AddChild(cancel);
        var accept = Button("确认", () => { window.QueueFree(); confirm(); }, true); accept.SizeFlagsHorizontal = SizeFlags.ExpandFill; actions.AddChild(accept);
        window.CloseRequested += () => window.QueueFree(); window.PopupCentered();
    }

    private static Label Text(string text, int size = 19, Color? color = null)
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", (int)Math.Round(size * UiScale)); if (color is { } c) label.AddThemeColorOverride("font_color", c); return label;
    }

    private static Button Button(string text, Action action, bool primary = false)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(0, 44 * UiScale), MouseDefaultCursorShape = CursorShape.PointingHand };
        if (text.Contains("锁定计划") || text.Contains("执行选定") || text.Contains("结束本轮") || text.Contains("轮末结算")) b.SetMeta("critical_action", true);
        b.Pressed += action;
        if (primary) { b.AddThemeStyleboxOverride("normal", Box(new Color("334748"), Mint, 1, 3)); b.AddThemeColorOverride("font_color", Ink); }
        return b;
    }

    private static VBoxContainer Stat(string title, string value)
    {
        var v = new VBoxContainer { CustomMinimumSize = new Vector2(150 * UiScale, 0) };
        var heading = Text(title, 13, Muted); heading.AutowrapMode = TextServer.AutowrapMode.Off; v.AddChild(heading);
        var number = Text(value, 27, Amber); number.AutowrapMode = TextServer.AutowrapMode.Off; v.AddChild(number); return v;
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
