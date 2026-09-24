using Godot;
using System;
using System.Linq;

namespace Joy;

public partial class Main
{
    private void LoadInterfaceSettings(string[] args)
    {
        var config = new ConfigFile();
        if (!_screenshotMode && config.Load(_interfaceSettingsPath) == Error.Ok)
        {
            UiScale = Mathf.Clamp((float)config.GetValue("display", "scale", 1f), 1, 1.5f);
            ReducedMotion = (bool)config.GetValue("display", "static", true);
            CosmicIntensity = (bool)config.GetValue("display", "cosmic", false);
            _grayscale = (bool)config.GetValue("display", "grayscale", false);
            _mutedAudio = (bool)config.GetValue("display", "mute", false);
        }
        if (_screenshotMode)
        {
            UiScale = 1; ReducedMotion = true; CosmicIntensity = false; _grayscale = false;
            string value = args.FirstOrDefault(a => a.StartsWith("--scale="))?.Split('=')[1] ?? "1";
            if (float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float scale)) UiScale = Mathf.Clamp(scale, 1, 1.5f);
            _grayscale = args.Contains("--grayscale");
            CosmicIntensity = args.Contains("--cosmic");
        }
        AudioServer.SetBusMute(0, _mutedAudio);
    }

    private void AddArtBackdrop()
    {
        var background = new ColorRect { Color = new Color("080c15"), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(background); background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        if (_welcome)
        {
            var picture = ArtAssets.Image("city", 0);
            picture.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
            picture.Modulate = new Color(1, 1, 1, CosmicIntensity ? .85f : .56f);
            AddChild(picture); picture.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            var shade = new ColorRect { Color = new Color(.025f, .04f, .07f, .36f), MouseFilter = MouseFilterEnum.Ignore };
            AddChild(shade); shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }
    }

    private void AddAccessibilityOverlay()
    {
        if (!_grayscale) return;
        var shader = new Shader { Code = "shader_type canvas_item; uniform sampler2D screen_tex : hint_screen_texture, repeat_disable, filter_nearest; void fragment() { vec4 c = texture(screen_tex, SCREEN_UV); float l = dot(c.rgb, vec3(0.2126, 0.7152, 0.0722)); COLOR = vec4(vec3(l), 1.0); }" };
        var overlay = new ColorRect { MouseFilter = MouseFilterEnum.Ignore, Material = new ShaderMaterial { Shader = shader } };
        AddChild(overlay); overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    private void ShowInterfaceSettings()
    {
        var dialog = new Window { Title = "显示与辅助设置", Size = new Vector2I(900, 760), Transient = true, Exclusive = true };
        AddChild(dialog);
        var pane = Pane("显示与辅助设置", 0); dialog.AddChild(pane.Panel); pane.Panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var layout = pane.Body; layout.AddThemeConstantOverride("separation", 16);
        layout.AddChild(Text("界面文字缩放", 20));
        var sizes = new OptionButton();
        sizes.AddItem("100% · 标准"); sizes.AddItem("125% · 较大"); sizes.AddItem("150% · 大字");
        sizes.Select(UiScale >= 1.5 ? 2 : UiScale >= 1.25 ? 1 : 0); layout.AddChild(sizes);
        var motion = new CheckButton { Text = "静态模式 / 减少装饰动态", ButtonPressed = ReducedMotion }; layout.AddChild(motion);
        var mute = new CheckButton { Text = "静音（所有必要反馈保留文字）", ButtonPressed = _mutedAudio }; layout.AddChild(mute);
        var gray = new CheckButton { Text = "灰度显示 / 状态辨识检查", ButtonPressed = _grayscale }; layout.AddChild(gray);
        var cosmic = new CheckButton { Text = "增强星空氛围（不改变规则与证据）", ButtonPressed = CosmicIntensity }; layout.AddChild(cosmic);
        layout.AddChild(Text("查看与调整显示不会推进经营日、现场轮次或消耗行动。\n所有风险、状态和防护结果都保留静态文字。", 17, Muted));
        pane.Footer.AddChild(Button("应用设置", () =>
        {
            UiScale = new[] { 1f, 1.25f, 1.5f }[sizes.Selected];
            ReducedMotion = motion.ButtonPressed; _mutedAudio = mute.ButtonPressed;
            _grayscale = gray.ButtonPressed; CosmicIntensity = cosmic.ButtonPressed;
            var config = new ConfigFile();
            config.SetValue("display", "scale", UiScale); config.SetValue("display", "static", ReducedMotion);
            config.SetValue("display", "mute", _mutedAudio); config.SetValue("display", "grayscale", _grayscale); config.SetValue("display", "cosmic", CosmicIntensity);
            if (!_screenshotMode) config.Save(_interfaceSettingsPath);
            AudioServer.SetBusMute(0, _mutedAudio);
            BuildTheme(); Render();
        }, true));
        pane.Footer.AddChild(Button("取消 · 返回原操作", () => dialog.QueueFree()));
        dialog.CloseRequested += () => dialog.QueueFree(); dialog.PopupCentered();
    }

    private void ShowLongText(string title, string text)
    {
        var window = new Window { Title = title, Size = new Vector2I(1000, 720), Transient = true, Exclusive = true };
        AddChild(window);
        var panel = Pane(title, 0); window.AddChild(panel.Panel); panel.Panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        panel.Body.AddChild(Text(text, 19));
        panel.Footer.AddChild(Button("关闭 · 返回原操作", () => window.QueueFree()));
        window.CloseRequested += () => window.QueueFree(); window.PopupCentered();
    }
}
