using Godot;
using System;
using System.Collections.Generic;

namespace Joy;

/// <summary>Candidate v0.1 art. UI labels, state, routes and rule evidence remain editable controls.</summary>
public static class ArtAssets
{
    private const string Root = "res://Assets/Art/";
    private const float AtlasCell = 1254f / 4f;
    private static readonly Dictionary<string, Texture2D> Cache = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, Vector2I> Cells = new(StringComparer.Ordinal)
    {
        ["node-hq"] = new(0, 0),
        ["node-hotel"] = new(1, 0),
        ["node-north"] = new(2, 0),
        ["node-hospital"] = new(3, 0),
        ["node-source"] = new(0, 1),
        ["node-south"] = new(1, 1),
        ["node-coop"] = new(2, 1),
        ["card-investigate"] = new(0, 2),
        ["card-light"] = new(1, 2),
        ["card-mimic"] = new(2, 2),
        ["item-clock"] = new(0, 3),
        ["item-lamp"] = new(1, 3),
        ["item-kit"] = new(2, 3),
    };
    private static Texture2D? _atlas;
    private static Texture2D? _fallback;

    /// <summary>Returns the same base identity for every public state; unknown IDs use a neutral fill.</summary>
    public static Texture2D Texture(string id)
    {
        if (string.IsNullOrEmpty(id)) return Fallback();
        if (Cache.TryGetValue(id, out var cached)) return cached;
        Texture2D? texture = id switch
        {
            "city" => GD.Load<Texture2D>(Root + "city-night-v01.png"),
            "room" => GD.Load<Texture2D>(Root + "room-mirror-neutral-v01.png"),
            _ => null,
        };
        if (texture is null && Cells.TryGetValue(id, out var cell))
        {
            _atlas ??= GD.Load<Texture2D>(Root + "subjects-atlas-v01.png");
            if (_atlas is not null)
                texture = new AtlasTexture
                {
                    Atlas = _atlas,
                    Region = new Rect2(cell.X * AtlasCell, cell.Y * AtlasCell, AtlasCell, AtlasCell),
                    FilterClip = true,
                };
        }
        if (texture is null) return Fallback();
        Cache[id] = texture;
        return texture;
    }

    /// <summary>Background default; callers can use the overload or set StretchMode for complete subjects.</summary>
    public static TextureRect Image(string id, float height) =>
        Image(id, height, TextureRect.StretchModeEnum.KeepAspectCovered);

    public static TextureRect Image(string id, float height, TextureRect.StretchModeEnum stretchMode) => new()
    {
        Texture = Texture(id),
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = stretchMode,
        MouseFilter = Control.MouseFilterEnum.Ignore,
        CustomMinimumSize = new Vector2(0, Math.Max(0, height)),
        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
    };

    private static Texture2D Fallback() => _fallback ??= new GradientTexture2D
    {
        Width = 2,
        Height = 2,
        Gradient = new Gradient
        {
            Colors = new[] { new Color("080c15"), new Color("171e2b") },
        },
    };
}
