using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Joy;

public sealed record MapPoint(string Id, string Title, string Subtitle, float X, float Y, bool Active, bool Selected,
    bool Danger = false, string ArtId = "", string Ownership = "", string Team = "", string PublicThreat = "");
public sealed record MapLink(string From, string To, bool Open, bool Threat = false, string Id = "", string Name = "",
    bool Blockade = false, bool Route = false, bool Selected = false);
public sealed record MapThreat(string From, string To, string Description);

public partial class NetworkMap : Control
{
    private static readonly Color Road = new("6C8793"), Company = new("8CBAB7"), Ink = new("E9E6DE"),
        Warning = new("C66D70"), Gold = new("D3AD6B"), Border = new("526172");
    private readonly List<MapPoint> _points = new();
    private readonly List<MapLink> _links = new();
    private readonly List<MapThreat> _threats = new();
    private readonly Dictionary<string, Button> _buttons = new();
    private readonly List<Control> _edgeBadges = new();
    private bool _city;
    public Action<string>? Selected;
    public Action<string>? EdgeSelected;
    public int RegisteredNodeCount => _points.Count;
    public int RegisteredEdgeCount => _links.Count;
    public void Configure(IEnumerable<MapPoint> points, IEnumerable<MapLink> links) => ConfigureInternal(points, links, Array.Empty<MapThreat>(), false);
    public void ConfigureCity(IEnumerable<MapPoint> points, IEnumerable<MapLink> links, IEnumerable<MapThreat> threats) => ConfigureInternal(points, links, threats, true);

    private void ConfigureInternal(IEnumerable<MapPoint> points, IEnumerable<MapLink> links, IEnumerable<MapThreat> threats, bool city)
    {
        _city = city; MouseFilter = MouseFilterEnum.Stop; ClipContents = true;
        CustomMinimumSize = new Vector2(city ? 640 : 440, city ? 350 + 20 * Main.UiScale : 370);
        SizeFlagsHorizontal = SizeFlags.ExpandFill; SizeFlagsVertical = SizeFlags.ExpandFill;
        _points.AddRange(points); _links.AddRange(links); _threats.AddRange(threats);
        if (city)
        {
            var image = new TextureRect { Texture = ArtAssets.Texture("city"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, Modulate = new Color(1, 1, 1, Main.CosmicIntensity ? .9f : .52f), MouseFilter = MouseFilterEnum.Ignore };
            AddChild(image); image.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            var veil = new ColorRect { Color = new Color(.031f, .047f, .082f, Main.CosmicIntensity ? .25f : .51f), MouseFilter = MouseFilterEnum.Ignore };
            AddChild(veil); veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            var lines = new RoadLayer { Map = this, MouseFilter = MouseFilterEnum.Ignore };
            AddChild(lines); lines.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }
        foreach (var p in _points)
        {
            var button = new Button { MouseDefaultCursorShape = CursorShape.PointingHand,
                TooltipText = $"{p.Title}\n{p.Ownership}\n{p.Subtitle}\n{p.Team}\n{p.PublicThreat}".Trim() };
            var normal = Main.Box(new Color("111A28"), p.Selected ? Ink : p.Active ? Company : Border, p.Selected ? 3 : 1, 4);
            normal.ContentMarginTop = normal.ContentMarginBottom = 5; normal.ContentMarginLeft = normal.ContentMarginRight = 6;
            button.AddThemeStyleboxOverride("normal", normal);
            button.AddThemeStyleboxOverride("hover", Main.Box(new Color("243246"), Ink, 2, 4));
            button.AddThemeStyleboxOverride("pressed", Main.Box(new Color("293A47"), Company, 2, 4));
            button.AddThemeStyleboxOverride("focus", Main.Box(new Color(0, 0, 0, 0), Gold, 3, 4));
            button.Pressed += () => Selected?.Invoke(p.Id); AddChild(button); _buttons[p.Id] = button;
            var content = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            button.AddChild(content); content.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            content.OffsetLeft = content.OffsetTop = 7; content.OffsetRight = content.OffsetBottom = -7;
            content.AddThemeConstantOverride("separation", 2);
            if (city)
            {
                var image = ArtAssets.Image(p.ArtId, (Main.UiScale >= 1.4f ? 26 : 36) * Main.UiScale, TextureRect.StretchModeEnum.KeepAspectCentered); image.MouseFilter = MouseFilterEnum.Ignore; content.AddChild(image);
                content.AddChild(MapLabel(p.Title, 16, Ink));
                content.AddChild(MapLabel(p.Ownership, 11, p.Active ? Company : new Color("B7BFCC")));
                content.AddChild(MapLabel(p.Subtitle, 12, p.Subtitle.Contains("有效营业") ? Gold : Ink));
                if (p.Team != "") content.AddChild(MapLabel("[ " + p.Team + " ]", 11, Company));
                if (p.PublicThreat != "") content.AddChild(MapLabel("▲ " + p.PublicThreat, 11, Warning));
            }
            else { content.AddChild(MapLabel(p.Title, 16, Ink)); content.AddChild(MapLabel(p.Subtitle, 13, p.Danger ? Warning : p.Active ? Company : Ink)); }
            if (p.Selected && !city) content.AddChild(MapLabel("当前选择", 10, Ink));
        }
        foreach (var link in _links.Where(l => !l.Open || l.Blockade))
        {
            var badge = new Button { Text = (!link.Open ? "× 中断" : "▥ 封控安排") + (!link.Open && link.Blockade ? " · ▥" : ""),
                MouseDefaultCursorShape = CursorShape.PointingHand, TooltipText = LinkDescription(link) };
            badge.AddThemeFontSizeOverride("font_size", (int)(11 * Main.UiScale));
            badge.AddThemeStyleboxOverride("normal", Main.Box(new Color("101722"), link.Open ? Company : Warning, 1, 2));
            badge.AddThemeColorOverride("font_color", link.Open ? Ink : Warning);
            badge.Pressed += () => EdgeSelected?.Invoke(link.Id); AddChild(badge); _edgeBadges.Add(badge);
        }
        Resized += Layout; CallDeferred(nameof(Layout));
    }

    public void Layout()
    {
        foreach (var p in _points)
        {
            var b = _buttons[p.Id];
            b.Size = new Vector2(_city ? 148 * Main.UiScale : 148,
                _city ? ((Main.UiScale >= 1.4f ? 105 : 115) + (p.Team != "" ? 16 : 0) + (p.PublicThreat != "" ? 16 : 0)) * Main.UiScale : 76);
            b.Position = PositionFor(p) - b.Size / 2;
        }
        int index = 0;
        foreach (var link in _links.Where(l => !l.Open || l.Blockade))
        {
            if (!TryEndpoints(link.From, link.To, out var a, out var b)) continue;
            var badge = _edgeBadges[index++]; badge.Size = badge.GetCombinedMinimumSize(); badge.Position = (a + b) / 2 - badge.Size / 2;
        }
        QueueRedraw(); foreach (var child in GetChildren().OfType<RoadLayer>()) child.QueueRedraw();
    }
    private Vector2 PositionFor(MapPoint p)
    {
        float x = _city ? 78 * Main.UiScale : 86, y = _city ? (Main.UiScale >= 1.4f ? 68 : 78) * Main.UiScale : 65;
        return new Vector2(x + p.X * Math.Max(0, Size.X - x * 2), y + p.Y * Math.Max(0, Size.Y - y * 2));
    }
    public override void _Draw() { if (!_city) DrawNetwork(this); }
    private void DrawNetwork(CanvasItem canvas)
    {
        for (int x = 18; x < Size.X; x += 38)
            for (int y = 18; y < Size.Y; y += 38) canvas.DrawCircle(new Vector2(x, y), .65f, new Color(.40f, .53f, .62f, .20f));
        foreach (var link in _links)
        {
            if (!TryEndpoints(link.From, link.To, out var a, out var b)) continue;
            if (link.Open) canvas.DrawLine(a, b, link.Selected ? Ink : link.Route ? Company : Road, link.Selected || link.Route ? 3.4f : 2.2f, true);
            else
            {
                var mid = (a + b) / 2; var d = (b - a).Normalized(); var normal = d.Orthogonal();
                canvas.DrawDashedLine(a, mid - d * 19, Warning, 2.5f, 9, true); canvas.DrawDashedLine(mid + d * 19, b, Warning, 2.5f, 9, true);
                canvas.DrawLine(mid - normal * 8 - d * 6, mid + normal * 8 + d * 6, Warning, 3, true);
                canvas.DrawLine(mid - normal * 8 + d * 6, mid + normal * 8 - d * 6, Warning, 3, true);
            }
            if (link.Route) canvas.DrawDashedLine(a + new Vector2(0, 7), b + new Vector2(0, 7), Company, 2, 6, true);
            if (link.Blockade)
            {
                var mid = (a + b) / 2; var normal = (b - a).Normalized().Orthogonal();
                for (int i = -1; i <= 1; i++) canvas.DrawLine(mid + new Vector2(i * 7, 0) - normal * 12, mid + new Vector2(i * 7, 0) + normal * 12, Company, 3, true);
            }
            if (link.Threat) DrawArrow(canvas, a, b);
        }
        foreach (var threat in _threats) if (TryEndpoints(threat.From, threat.To, out var a, out var b)) DrawArrow(canvas, a, b);
    }
    private void DrawArrow(CanvasItem canvas, Vector2 a, Vector2 b)
    {
        var d = (b - a).Normalized(); var offset = d.Orthogonal() * 10;
        float EdgeDistance(Vector2 center)
        {
            var button = _buttons.Values.FirstOrDefault(button => (button.Position + button.Size / 2).DistanceSquaredTo(center) < 2);
            if (button is null) return 45;
            return Math.Min(Math.Abs(d.X) < .001f ? float.MaxValue : button.Size.X / 2 / Math.Abs(d.X),
                Math.Abs(d.Y) < .001f ? float.MaxValue : button.Size.Y / 2 / Math.Abs(d.Y));
        }
        // Arrowheads end in the visible road gap, rather than being covered by the destination card.
        var start = a + d * (EdgeDistance(a) + 4) + offset; var finish = b - d * (EdgeDistance(b) + 6) + offset;
        if ((finish - start).Dot(d) < 8) { start = a.Lerp(b, .44f) + offset; finish = a.Lerp(b, .56f) + offset; }
        canvas.DrawDashedLine(start, finish, Gold, 3, 9, true);
        float head = Math.Min(13, Math.Max(7, start.DistanceTo(finish) * .36f));
        canvas.DrawLine(finish, finish - d.Rotated(.55f) * head, Gold, 4, true); canvas.DrawLine(finish, finish - d.Rotated(-.55f) * head, Gold, 4, true);
    }
    public override void _GuiInput(InputEvent input)
    {
        if (input is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click) return;
        foreach (var link in _links)
        {
            if (!TryEndpoints(link.From, link.To, out var a, out var b) || Geometry2D.GetClosestPointToSegment(click.Position, a, b).DistanceTo(click.Position) > 14) continue;
            EdgeSelected?.Invoke(link.Id); AcceptEvent(); return;
        }
    }
    private bool TryEndpoints(string from, string to, out Vector2 a, out Vector2 b)
    {
        var first = _points.Find(p => p.Id == from); var second = _points.Find(p => p.Id == to);
        a = first is null ? Vector2.Zero : PositionFor(first); b = second is null ? Vector2.Zero : PositionFor(second); return first is not null && second is not null;
    }
    private static string LinkDescription(MapLink l) => (l.Name == "" ? "登记连接" : l.Name) + "\n" +
        (l.Open ? "双向通行" : "已知状态：通路中断；记录未提供具体故障原因。可改道或选择相邻节点抢修。") +
        (l.Blockade ? "\n该边已安排封控，实际生效取决于驻站、连通与已掌握规程。" : "");
    private static Label MapLabel(string text, int size, Color color)
    {
        var label = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = MouseFilterEnum.Ignore, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        label.AddThemeFontSizeOverride("font_size", (int)(size * Main.UiScale)); label.AddThemeColorOverride("font_color", color); return label;
    }
    private partial class RoadLayer : Control
    {
        public NetworkMap Map { get; init; } = null!;
        public override void _Draw() => Map.DrawNetwork(this);
    }
}
