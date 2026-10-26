using Godot;
using Joy.Core.GridPrototype;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Joy.GridPrototype;

public partial class GridMain : Control
{
    internal static readonly Color Ink = new("e9f0f2"), Muted = new("8fabb5"), Teal = new("61d1ba"), Gold = new("ecc17c"), Red = new("e77f87"), Surface = new("121e2c");
    private GridGame? _game;
    private GridEngine _engine = null!;
    private VBoxContainer _root = null!;
    private string _page = "dispatch", _notice = "", _hero = "mapper", _card = "", _site = "hotel";
    private Cell _target = new(0, 3), _cityCell = new(2, 1);
    private bool _welcome = true, _capture, _saveFailed;
    private string _savePath = "", _captureName = "", _capturePath = "";
    private GridBoard? _board;

    public override void _Ready()
    {
        GetWindow().Title = "Joy · 异常回收公司 / GRID 01";
        GetWindow().MinSize = new Vector2I(1280, 720);
        _savePath = ProjectSettings.GlobalizePath("user://grid-prototype-v1/save.json");
        Theme = CreateTheme();
        var args = OS.GetCmdlineUserArgs();
        if (args.Contains("--grid-ui-test"))
        {
            _savePath = ProjectSettings.GlobalizePath("user://grid-prototype-ui-test/save.json");
            GetWindow().Title += " / QA";
        }
        _captureName = args.FirstOrDefault(a => a.StartsWith("--grid-capture="))?.Split('=', 2)[1] ?? "";
        _capturePath = args.FirstOrDefault(a => a.StartsWith("--grid-capture-path="))?.Split('=', 2)[1] ?? "";
        _capture = _captureName.Length > 0;
        if (_capture) PrepareCapture();
        Render();
        if (_capture) _ = Capture();
    }

    private void PrepareCapture()
    {
        _game = GridEngine.NewGame(); _engine = new(_game); _welcome = false;
        if (_captureName is "skills" or "assets") _page = _captureName;
        if (_captureName == "assets")
            foreach (var asset in Catalog.Assets) if (!_game.Assets.Any(a => a.Id == asset.Id)) _game.Assets.Add(new() { Id = asset.Id });
        if (_captureName is "field" or "combat")
        {
            _engine.StartMission("hotel");
            _hero = _game.Field!.Actors[0].HeroId;
            _target = new(1, 3); _card = "move";
            if (_captureName == "combat")
            {
                var field = _game.Field;
                field.Actors[0].X = 4; field.Actors[0].Y = 3;
                field.Actors[1].X = 2; field.Actors[1].Y = 4;
                foreach (var enemy in field.Enemies) enemy.Alert = true;
                field.EvidenceKnown = true;
                _target = field.Enemies.FirstOrDefault(e => e.Alive)?.Cell ?? new(4, 3);
                _card = "attack";
            }
        }
    }

    private async Task Capture()
    {
        for (int i = 0; i < 12; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var path = _capturePath.Length > 0 ? _capturePath : ProjectSettings.GlobalizePath($"user://grid-captures/{_captureName}.png");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var error = GetViewport().GetTexture().GetImage().SavePng(path);
        var witnesses = new System.Collections.Generic.List<object>();
        void Walk(Node node)
        {
            if (node is Control control && control.IsVisibleInTree() && node is Label or Button)
            {
                var rect = control.GetGlobalRect();
                witnesses.Add(new { kind = node.GetType().Name, text = node is Label label ? label.Text : ((Button)node).Text,
                    x = rect.Position.X, y = rect.Position.Y, width = rect.Size.X, height = rect.Size.Y });
            }
            foreach (var child in node.GetChildren()) Walk(child);
        }
        Walk(this);
        File.WriteAllText(Path.ChangeExtension(path, ".json"), System.Text.Json.JsonSerializer.Serialize(new
        {
            scene = _captureName, viewport = new { width = GetViewportRect().Size.X, height = GetViewportRect().Size.Y }, controls = witnesses
        }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        GD.Print($"GRID_CAPTURE | {path} | {error}");
        GetTree().Quit(error == Error.Ok ? 0 : 1);
    }

    private void NewGame()
    {
        _game = GridEngine.NewGame(); _engine = new(_game); _welcome = false; _page = "dispatch"; _card = ""; _hero = "mapper";
        _notice = "先选两名外勤，再从城市地块选择任务。每位角色有 1 点初始技能，可在「角色成长」中选择专精。";
        Save(); Render();
    }
    private void Continue()
    {
        var loaded = GridSave.Load(_savePath, out var message);
        if (loaded is null) { _notice = message; Render(); return; }
        _game = loaded; _engine = new(_game); _welcome = false; _notice = message;
        if (_game.Field is not null) _hero = _game.Field.Actors[0].HeroId;
        Render();
    }
    private void Save()
    {
        if (_capture || _game is null) return;
        var result = GridSave.Save(_savePath, _game); _saveFailed = !result.Success;
        if (!result.Success) _notice = result.Message;
    }
    private void Apply(Func<Result> command)
    {
        var result = command(); _notice = result.Message;
        if (result.Success)
        {
            Save();
            if (_game?.Field is not null && !_game.Field.Actors.Any(a => a.HeroId == _hero)) _hero = _game.Field.Actors[0].HeroId;
            if (_game?.Field is null) _card = "";
        }
        Render();
    }
    private void Render()
    {
        foreach (var child in GetChildren()) { RemoveChild(child); child.QueueFree(); }
        var background = new ColorRect { Color = new Color("080f1a"), MouseFilter = MouseFilterEnum.Ignore };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(background);
        var margin = new MarginContainer(); margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach (var side in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + side, 22);
        AddChild(margin); _root = V(12); margin.AddChild(_root);
        Header();
        if (_welcome || _game is null) Welcome();
        else if (_game.Field is not null && _game.Phase == "field") Field();
        else City();
        var footer = Panel(_notice.Length > 0 && _saveFailed ? new Color("3a222e") : new Color("101c29"));
        var footrow = H(16); footer.AddChild(footrow);
        var feedback = L(_notice.Length > 0 ? _notice : "选牌 → 选格 → 查看预览 → 打出。掌握异常，让它成为公司的能力。", 19, _saveFailed ? Red : Muted, true, true);
        feedback.MaxLinesVisible = 2;
        feedback.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        feedback.TooltipText = feedback.Text;
        footrow.AddChild(feedback);
        footrow.AddChild(L(_capture ? "预览场景" : _saveFailed ? "保存失败" : _game is not null ? "自动保存 / GRID 01" : "玩法原型 / 0.4", 16, _saveFailed ? Red : Muted));
        _root.AddChild(footer);
    }
    private void Header()
    {
        var row = H(18); _root.AddChild(row);
        var title = V(0); title.SizeFlagsHorizontal = SizeFlags.ExpandFill; row.AddChild(title);
        title.AddChild(L("异常回收公司", 34, Ink)); title.AddChild(L("GRID / 01     人类专长 × 异常能力", 17, Teal));
        if (_game is not null && !_welcome)
        {
            row.AddChild(Stat("经营日", $"{_game.Day:00} / {_game.MaxDays:00}"));
            row.AddChild(Stat("公司资金", _game.Money.ToString()));
            row.AddChild(Stat("封装余量", _game.SealCharges.ToString()));
            row.AddChild(Stat("营业网点", $"{_game.City.Count(t => t.Operating)} / 2"));
        }
        row.AddChild(B("玩法说明", ShowHelp, false, 120));
        if (!_welcome) row.AddChild(B("保存 · 返回标题", () => { Save(); _welcome = true; Render(); }, false, 170));
    }
    private void Welcome()
    {
        var layout = H(28); layout.SizeFlagsVertical = SizeFlags.ExpandFill; _root.AddChild(layout);
        var art = Panel(); art.SizeFlagsHorizontal = SizeFlags.ExpandFill; layout.AddChild(art);
        var av = V(12); art.AddChild(av); av.AddChild(ArtAssets.Image("city", 420));
        av.AddChild(L("把怪谈，变成公司的下一种能力。", 34, Gold, true));
        av.AddChild(L("两层网格 · 三名角色 · 独立牌组 · 技能专精 · 三处回收任务", 21, Muted, true));
        var panel = Panel(); panel.CustomMinimumSize = new Vector2(510, 0); layout.AddChild(panel);
        var col = V(22); panel.AddChild(col);
        col.AddChild(L("雾港 / 七日回收计划", 30, Ink));
        col.AddChild(L("01  经营\n选两名外勤，将其他员工和异常留给公司。", 23, Teal, true));
        col.AddChild(L("02  探索与交战\n打出卡牌移动、查证、诱导和收容。每人拥有自己的行动与手牌。", 23, Ink, true));
        col.AddChild(L("03  带回能力\n敌人可以成为收容物。活着撤离，才能把发现变成公司的资产。", 23, Ink, true));
        col.AddChild(L("目标：第 7 日结束前完成三个委托与两处营业。最后一次出勤前，记得建好网点。", 21, Gold, true));
        var spacer = new Control { SizeFlagsVertical = SizeFlags.ExpandFill }; col.AddChild(spacer);
        col.AddChild(B("开始新远征 →", () =>
        {
            if (File.Exists(_savePath)) Confirm("开始新的远征？", "这会替换本原型的自动存档。", NewGame); else NewGame();
        }, true));
        col.AddChild(B("继续远征", Continue, false, 0, !File.Exists(_savePath)));
        col.AddChild(L("本版使用独立存档。移动和交互牌始终可用，所有现场主动操作都由打牌发动。", 18, Muted, true));
    }
    private void ShowHelp()
    {
        var dialog = new AcceptDialog { Title = "外勤手册 / GRID 01", DialogText =
            "公司：选择恰好两名外勤，其他人留守并结算经营效果。先学专精，再按需要替换个人牌组。\n\n现场：点击角色 → 点击一张牌 → 点击目标格 → 查看预览并打出。常驻基础牌不会抽空；职业牌归属于各自角色。每人每轮 2 行动，打出「结束回合」后敌人行动、时间推进，再各抽 3 张牌。\n\n先用「搜查」取得附近线索，再观察敌人的收容条件。击败或收容目标都可完成任务，但只有收容能得到异常资产。封装消耗工具次数。\n\n撤离：两名角色都需要靠近入口，打出「撤离」。现场货物与零钱只有撤离后才入账。回合耗尽或队伍倒下会丢失本次所得。\n\n成长：完成任务获得技能点。技能有两条互斥路线，可继续提升等级；角色和异常留守或出勤时发挥不同作用。\n\n目标：七日内处理三个地点，并建立两个营业网点。成功动作后自动保存，标题可继续。", MinSize = new Vector2I(820, 550) };
        AddChild(dialog); dialog.Confirmed += dialog.QueueFree; dialog.CloseRequested += dialog.QueueFree; dialog.PopupCentered();
    }
    private void Confirm(string title, string content, Action action)
    {
        var dialog = new ConfirmationDialog { Title = title, DialogText = content, MinSize = new Vector2I(570, 200) };
        AddChild(dialog); dialog.Confirmed += () => { dialog.QueueFree(); action(); }; dialog.Canceled += dialog.QueueFree; dialog.PopupCentered();
    }
    private static Theme CreateTheme()
    {
        var theme = new Theme { DefaultFont = GD.Load<FontFile>("res://Assets/Fonts/NotoSansCJKsc-Regular.otf"), DefaultFontSize = 21 };
        theme.SetColor("font_color", "Label", Ink); theme.SetColor("font_color", "Button", Ink); theme.SetColor("font_disabled_color", "Button", new Color("607780"));
        theme.SetStylebox("normal", "Button", Box(new("1c2d3e"), new("344c5d")));
        theme.SetStylebox("hover", "Button", Box(new("294553"), Teal)); theme.SetStylebox("pressed", "Button", Box(new("2e534f"), Gold));
        theme.SetStylebox("disabled", "Button", Box(new("111c28"), new("243443")));
        theme.SetStylebox("focus", "Button", Box(new Color(0, 0, 0, 0), Gold));
        theme.SetStylebox("panel", "PopupMenu", Box(Surface, Muted));
        return theme;
    }
    internal static StyleBoxFlat Box(Color fill, Color border, int width = 1) => new()
    {
        BgColor = fill, BorderColor = border, BorderWidthLeft = width, BorderWidthRight = width, BorderWidthTop = width, BorderWidthBottom = width,
        CornerRadiusTopLeft = 7, CornerRadiusTopRight = 7, CornerRadiusBottomLeft = 7, CornerRadiusBottomRight = 7,
        ContentMarginLeft = 15, ContentMarginRight = 15, ContentMarginTop = 11, ContentMarginBottom = 11
    };
    private static PanelContainer Panel(Color? fill = null) { var p = new PanelContainer(); p.AddThemeStyleboxOverride("panel", Box(fill ?? Surface, new("294052"))); return p; }
    private static VBoxContainer V(int gap = 10) { var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", gap); return v; }
    private static HBoxContainer H(int gap = 10) { var h = new HBoxContainer(); h.AddThemeConstantOverride("separation", gap); return h; }
    private static Label L(string text, int size = 21, Color? color = null, bool wrap = false, bool expand = false)
    {
        var l = new Label { Text = text, AutowrapMode = wrap ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off, SizeFlagsHorizontal = expand ? SizeFlags.ExpandFill : SizeFlags.Fill };
        l.AddThemeFontSizeOverride("font_size", size); l.AddThemeColorOverride("font_color", color ?? Ink); return l;
    }
    private static Button B(string text, Action action, bool accent = false, float width = 0, bool disabled = false)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(width, 46), Disabled = disabled, FocusMode = FocusModeEnum.None };
        if (accent) b.AddThemeStyleboxOverride("normal", Box(new("244b49"), Teal));
        b.Pressed += action; return b;
    }
    private static Control Stat(string label, string value)
    {
        var v = V(0); v.CustomMinimumSize = new Vector2(120, 65); v.AddChild(L(label, 16, Muted)); v.AddChild(L(value, 29, Gold)); return v;
    }
    private static ScrollContainer Scroll(Control content)
    {
        var scroll = new ScrollContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        content.SizeFlagsHorizontal = SizeFlags.ExpandFill; scroll.AddChild(content); return scroll;
    }
}
