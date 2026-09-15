using Godot;
using System;
using System.Collections.Generic;

namespace Joy;

public sealed record MapPoint(string Id, string Title, string Subtitle, float X, float Y, bool Active, bool Selected, bool Danger = false);
public sealed record MapLink(string From, string To, bool Open, bool Threat = false);

public partial class NetworkMap : Control
{
    private readonly List<MapPoint> _points = new();
    private readonly List<MapLink> _links = new();
    private readonly Dictionary<string, Button> _buttons = new();
    public Action<string>? Selected;

    public void Configure(IEnumerable<MapPoint> points, IEnumerable<MapLink> links)
    {
        MouseFilter = MouseFilterEnum.Pass;
        CustomMinimumSize = new Vector2(440, 420);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        _points.AddRange(points);
        _links.AddRange(links);
        foreach (var point in _points)
        {
            var b = new Button { Text = point.Title + "\n" + point.Subtitle, ClipText = true, MouseDefaultCursorShape = CursorShape.PointingHand };
            b.AddThemeFontSizeOverride("font_size", 16);
            var fill = point.Active ? new Color("193e38") : new Color("16252d");
            var edge = point.Selected ? new Color("e8b777") : point.Active ? new Color("69bfa3") : new Color("41515b");
            b.AddThemeStyleboxOverride("normal", Main.Box(fill, edge, 1, 8));
            b.AddThemeStyleboxOverride("hover", Main.Box(new Color("263c42"), new Color("e8b777"), 2, 8));
            b.AddThemeStyleboxOverride("pressed", Main.Box(new Color("344e48"), new Color("e8b777"), 2, 8));
            b.AddThemeColorOverride("font_color", point.Danger ? new Color("f2a286") : new Color("e5eadf"));
            b.Pressed += () => Selected?.Invoke(point.Id);
            AddChild(b);
            _buttons[point.Id] = b;
        }
        Resized += Layout;
        CallDeferred(nameof(Layout));
    }

    public void Layout()
    {
        foreach (var p in _points)
        {
            var b = _buttons[p.Id];
            b.Size = new Vector2(146, 70);
            b.Position = PositionFor(p) - b.Size / 2;
        }
        QueueRedraw();
    }

    private Vector2 PositionFor(MapPoint p) => new(86 + p.X * Math.Max(0, Size.X - 172), 65 + p.Y * Math.Max(0, Size.Y - 130));

    public override void _Draw()
    {
        for (int x = 14; x < Size.X; x += 28)
            for (int y = 14; y < Size.Y; y += 28)
                DrawCircle(new Vector2(x, y), 1, new Color("1c2c32"));
        foreach (var link in _links)
        {
            var from = _points.Find(p => p.Id == link.From);
            var to = _points.Find(p => p.Id == link.To);
            if (from is null || to is null) continue;
            var a = PositionFor(from);
            var b = PositionFor(to);
            var color = link.Threat ? new Color("d78d66") : link.Open ? new Color("4b706c") : new Color("644649");
            DrawDashedLine(a, b, color, link.Threat ? 3 : 2, link.Open ? 1000 : 7, true);
            var mid = (a + b) / 2;
            DrawCircle(mid, 4, color);
            if (link.Threat)
            {
                var dir = (b - a).Normalized();
                DrawLine(mid, mid - dir.Rotated(.55f) * 14, color, 3, true);
                DrawLine(mid, mid - dir.Rotated(-.55f) * 14, color, 3, true);
            }
        }
    }
}
