using Godot;
using System;

namespace Joy;

/// <summary>
/// A fixed room illustration with public, factual overlays. This view deliberately
/// receives no rule instance, hidden variant, candidate answer or undiscovered item.
/// </summary>
public partial class FieldScene : Control
{
    private Texture2D? _texture;
    private string _site = "";
    private string _room = "";
    private bool _light;
    private bool _reactionObserved;
    private bool _shadowVisible;
    private bool _entrance;
    private int _exitDistance;
    private string _publicFeedback = "";
    private string _viewedRoom = "";

    public FieldScene()
    {
        ClipContents = true;
        CustomMinimumSize = new Vector2(260, 180);
        MouseFilter = MouseFilterEnum.Ignore;
        Resized += QueueRedraw;
    }

    public void Configure(Texture2D? texture, string site, string room, bool light,
        bool reactionObserved, bool shadowVisible, bool entrance, int exitDistance, string publicFeedback = "", string viewedRoom = "")
    {
        _texture = texture; _site = site; _room = room; _light = light;
        _reactionObserved = reactionObserved; _shadowVisible = shadowVisible;
        _entrance = entrance; _exitDistance = exitDistance;
        _publicFeedback = publicFeedback;
        _viewedRoom = viewedRoom.Length > 0 ? viewedRoom : room;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var frame = new Rect2(Vector2.Zero, Size);
        var imageFrame = frame;
        DrawRect(frame, new Color("101722"));
        if (_texture is not null)
        {
            // Fit-to-cover preserves the room's fixed visual anchors at every UI scale.
            var imageSize = _texture.GetSize();
            float scale = Math.Max(Size.X / imageSize.X, Size.Y / imageSize.Y);
            var targetSize = imageSize * scale;
            imageFrame = new Rect2((Size - targetSize) / 2, targetSize);
            DrawTextureRect(_texture, imageFrame, false);
        }
        else DrawArchitecturalFallback();
        DrawRect(frame, new Color(0.03f, 0.05f, 0.08f, 0.13f));
        // Local abnormal treatment is unlocked only by a recorded observable reaction.
        // It stays static, including during A/B comparison and reduced-motion mode.
        if (_reactionObserved)
        {
            // Manifest mirror anchor: (0.82, 0.45) in the source image. Transform
            // both the anchor and its local overlay using the exact image fit.
            var reflection = new Rect2(imageFrame.Position + imageFrame.Size * new Vector2(.76f, .24f), imageFrame.Size * new Vector2(.12f, .42f));
            DrawRect(reflection, new Color(.60f, .57f, .69f, .17f));
            DrawRect(reflection.Grow(3), new Color("9a91af"), false, 2);
            DrawLine(reflection.Position, reflection.End, new Color(.82f, .81f, .9f, .65f), 2);
            DrawLine(reflection.Position + new Vector2(0, reflection.Size.Y * .62f), reflection.Position + new Vector2(reflection.Size.X, reflection.Size.Y * .42f), new Color("9a91af"), 2);
        }
        var font = GetThemeDefaultFont();
        float scaleUi = Main.UiScale;
        int small = (int)Math.Round(13 * scaleUi), title = (int)Math.Round(23 * scaleUi);
        float topHeight = 64 * scaleUi;
        DrawRect(new Rect2(0, 0, Math.Min(Size.X, 465 * scaleUi), topHeight), new Color(.055f, .079f, .119f, .94f));
        DrawString(font, new Vector2(18, 23 * scaleUi), "02 / " + _site + " · 固定现场", HorizontalAlignment.Left, Size.X - 36, small, new Color("8cbab7"));
        DrawString(font, new Vector2(18, 55 * scaleUi), "● " + _room + " / 查看 " + _viewedRoom, HorizontalAlignment.Left, Size.X - 36, title, new Color("e9e6de"));
        bool showFact = _publicFeedback.Length > 0 || _reactionObserved || _shadowVisible;
        float footerHeight = (showFact ? 84 : 59) * scaleUi;
        float footerY = Math.Max(topHeight, Size.Y - footerHeight);
        DrawRect(new Rect2(0, footerY, Math.Min(Size.X, 650 * scaleUi), Size.Y - footerY), new Color(.055f, .079f, .119f, .95f));
        string actualLight = _light ? "真实照明 / 已开启，本轮有效" : "真实照明 / 未开启";
        DrawString(font, new Vector2(18, footerY + 25 * scaleUi), actualLight, HorizontalAlignment.Left, Size.X - 36, (int)Math.Round(16 * scaleUi), _light ? new Color("d3ad6b") : new Color("e9e6de"));
        string exit = _entrance ? "出口 / 当前即入口，撤离不消耗行动" : $"出口 / 距入口 {_exitDistance} 格 · 可通行门见下方导航";
        DrawString(font, new Vector2(18, footerY + 48 * scaleUi), exit, HorizontalAlignment.Left, Size.X - 36, small, new Color("8cbab7"));
        if (showFact)
            DrawString(font, new Vector2(18, footerY + 73 * scaleUi), _publicFeedback.Length > 0 ? _publicFeedback : _shadowVisible ? "! 已公开敌影在此 / 轮末后果见预览" : "! 本房已有反噬记录 / 原因以确认结果为准", HorizontalAlignment.Left, Size.X - 36, small, new Color("c6bbd8"));
        DrawRect(frame.Grow(-1), new Color("56616f"), false, 2);
    }

    private void DrawArchitecturalFallback()
    {
        // The fallback is decorative architecture only; actual doors remain in navigation.
        var wall = new Rect2(Size.X * .08f, Size.Y * .1f, Size.X * .84f, Size.Y * .7f);
        DrawRect(wall, new Color("292a2c"));
        for (int i = 0; i < 8; i++)
        {
            float x = wall.Position.X + i * wall.Size.X / 7;
            DrawLine(new Vector2(x, wall.Position.Y), new Vector2(x, wall.End.Y), new Color("41403c"), 2);
        }
        DrawRect(new Rect2(Size.X * .66f, Size.Y * .23f, Size.X * .15f, Size.Y * .45f), new Color("665c4f"));
        DrawRect(new Rect2(Size.X * .675f, Size.Y * .25f, Size.X * .12f, Size.Y * .40f), new Color("3d494e"));
        DrawRect(new Rect2(Size.X * .17f, Size.Y * .61f, Size.X * .32f, Size.Y * .08f), new Color("6a4c36"));
        DrawRect(new Rect2(Size.X * .19f, Size.Y * .69f, Size.X * .27f, Size.Y * .18f), new Color("32291f"));
    }
}
