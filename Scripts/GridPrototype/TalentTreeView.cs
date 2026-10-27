using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Joy.GridPrototype;

public sealed record TalentVisual(string Id, string Name, string Kind, int Column, int Row, int Cost,
    bool Learned, bool Available, IReadOnlyList<string> Prerequisites);

/// <summary>A spatial, clickable talent tree. Buttons remain inspectable even while locked.</summary>
public partial class TalentTreeView : Control
{
    public IReadOnlyList<TalentVisual> Nodes { get; set; } = Array.Empty<TalentVisual>();
    public string RootTitle { get; set; } = "职业根基";
    public string SelectedId { get; set; } = "";
    public string FlashId { get; set; } = "";
    public Color HeroColor { get; set; } = GridMain.Teal;
    public string[] BranchNames { get; set; } = { "专长", "协同", "技法" };
    public event Action<string>? NodeSelected;
    private readonly Dictionary<string, Button> _buttons = new();
    private readonly Dictionary<string, Rect2> _rects = new();
    private Rect2 _rootRect;
    private Font _font = null!;
    private float _flashTime;

    public override void _Ready()
    {
        _font = GD.Load<FontFile>("res://Assets/Fonts/NotoSansCJKsc-Regular.otf");
        MouseFilter = MouseFilterEnum.Ignore;
        foreach (var node in Nodes)
        {
            var edge = node.Learned ? GridMain.Gold : node.Available ? GridMain.Teal : new Color("425262");
            var fill = node.Learned ? new Color("273b3b") : node.Available ? new Color("163c41") : new Color("172332");
            var button = new Button { FocusMode = FocusModeEnum.None, MouseDefaultCursorShape = CursorShape.PointingHand };
            button.AddThemeStyleboxOverride("normal", GridMain.Box(fill, node.Id == SelectedId ? new Color("f1e1b9") : edge, node.Id == SelectedId ? 3 : node.Learned ? 2 : 1));
            button.AddThemeStyleboxOverride("hover", GridMain.Box(new Color("304650"), node.Learned ? GridMain.Gold : GridMain.Teal, 2));
            button.AddThemeStyleboxOverride("pressed", GridMain.Box(new Color("36564f"), GridMain.Gold, 2));
            button.TooltipText = node.Name + " · " + KindName(node.Kind) + "\n" + (node.Learned ? "已经点亮" : node.Available ? "可以点亮，消耗 " + node.Cost + " 点天赋" : "点击查看前置条件");
            AddChild(button); _buttons[node.Id] = button;
            var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
            margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            margin.AddThemeConstantOverride("margin_left", 13); margin.AddThemeConstantOverride("margin_right", 13);
            margin.AddThemeConstantOverride("margin_top", 10); margin.AddThemeConstantOverride("margin_bottom", 10);
            button.AddChild(margin);
            var col = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore }; col.AddThemeConstantOverride("separation", 3); margin.AddChild(col);
            col.AddChild(Label(KindSymbol(node.Kind) + "  " + KindName(node.Kind), 15, node.Learned ? GridMain.Gold : node.Available ? GridMain.Teal : GridMain.Muted));
            col.AddChild(Label(node.Name, 23, node.Learned ? new Color("fff0c8") : GridMain.Ink));
            col.AddChild(Label(node.Learned ? "已点亮  ✓" : node.Available ? $"可学习 · {node.Cost} 点" : $"待解锁 · {node.Cost} 点", 16, node.Learned ? GridMain.Gold : node.Available ? GridMain.Teal : GridMain.Muted));
            button.Pressed += () => NodeSelected?.Invoke(node.Id);
        }
        Resized += Arrange;
        Arrange();
        SetProcess(FlashId.Length > 0);
    }

    private static Label Label(string text, int size, Color color)
    {
        var label = new Label { Text = text, MouseFilter = MouseFilterEnum.Ignore, HorizontalAlignment = HorizontalAlignment.Center,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis };
        label.AddThemeFontSizeOverride("font_size", size); label.AddThemeColorOverride("font_color", color); return label;
    }
    public static string KindName(string kind) => kind switch { "stat" => "数值强化", "mechanic" => "机制变化", "card" => "特色新牌", _ => "天赋" };
    public static string KindSymbol(string kind) => kind switch { "stat" => "+", "mechanic" => "◇", "card" => "▣", _ => "○" };

    private void Arrange()
    {
        float width = Mathf.Max(Size.X, 650), height = Mathf.Max(Size.Y, 610);
        float column = (width - 36) / 3;
        float nodeWidth = Mathf.Min(column - 38, 250);
        float nodeHeight = 108;
        _rootRect = new Rect2(new Vector2(width / 2 - 102, 22), new Vector2(204, 80));
        float firstY = 175;
        float step = Math.Max(140, (height - firstY - nodeHeight - 24) / 2);
        _rects.Clear();
        foreach (var n in Nodes)
        {
            float x = 18 + column * (n.Column + .5f) - nodeWidth / 2;
            float y = firstY + (n.Row - 1) * step;
            var rect = new Rect2(new Vector2(x, y), new Vector2(nodeWidth, nodeHeight)); _rects[n.Id] = rect;
            _buttons[n.Id].Position = rect.Position; _buttons[n.Id].Size = rect.Size;
        }
        QueueRedraw();
    }
    public override void _Process(double delta)
    {
        _flashTime += (float)delta; QueueRedraw();
        if (_flashTime > 1.0f) { FlashId = ""; SetProcess(false); QueueRedraw(); }
    }
    public override void _Draw()
    {
        _font ??= GD.Load<FontFile>("res://Assets/Fonts/NotoSansCJKsc-Regular.otf");
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("0c1724"));
        for (int x = 28; x < Size.X; x += 38)
            for (int y = 18; y < Size.Y; y += 38) DrawCircle(new Vector2(x, y), 1, new Color(.45f, .67f, .72f, .11f));
        foreach (var n in Nodes.OrderBy(n => n.Learned ? 2 : n.Available ? 1 : 0))
        {
            if (!_rects.TryGetValue(n.Id, out var rect)) continue;
            foreach (string parent in n.Prerequisites.Count == 0 ? new[] { "" } : n.Prerequisites)
            {
                var fromRect = parent == "" ? _rootRect : _rects.GetValueOrDefault(parent, _rootRect);
                var from = new Vector2(fromRect.GetCenter().X, fromRect.End.Y);
                var to = new Vector2(rect.GetCenter().X, rect.Position.Y);
                var middle = (from.Y + to.Y) / 2;
                Vector2[] points = { from, new(from.X, middle), new(to.X, middle), to };
                var color = n.Learned ? GridMain.Gold : n.Available ? GridMain.Teal : new Color("344455");
                if (n.Learned) DrawPolyline(points, new Color(color.R, color.G, color.B, .13f), 10, true);
                DrawPolyline(points, color, n.Learned ? 3 : 2, true);
                DrawCircle(to + new Vector2(0, -8), 4, color);
            }
        }
        DrawStyleBox(GridMain.Box(new Color("21383c"), HeroColor, 2), _rootRect);
        Text(RootTitle, _rootRect.Position + new Vector2(_rootRect.Size.X / 2, 30), 24, HeroColor);
        Text("职业根基 · 已掌握", _rootRect.Position + new Vector2(_rootRect.Size.X / 2, 60), 16, GridMain.Muted);
        for (int col = 0; col < 3; col++)
        {
            var first = Nodes.FirstOrDefault(n => n.Column == col && n.Row == 1);
            if (first is not null && _rects.TryGetValue(first.Id, out var r))
                Text(BranchNames[Math.Min(col, BranchNames.Length - 1)], new Vector2(r.GetCenter().X, 154), 18, HeroColor);
        }
        if (FlashId.Length > 0 && _rects.TryGetValue(FlashId, out var flash))
        {
            float alpha = 1 - _flashTime; var color = new Color(GridMain.Gold.R, GridMain.Gold.G, GridMain.Gold.B, alpha);
            DrawRect(flash.Grow(4 + 12 * _flashTime), color, false, 2, true);
        }
    }
    private void Text(string text, Vector2 center, int size, Color color)
    {
        float width = _font.GetStringSize(text, HorizontalAlignment.Left, -1, size).X;
        DrawString(_font, center - new Vector2(width / 2, 0), text, HorizontalAlignment.Left, -1, size, color);
    }
}
