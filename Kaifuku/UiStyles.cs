using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;

namespace Kaifuku;

public enum UiStyle { Dark, Light, Classic, ClearBlue }
public enum HeaderFont { Serif, Retro, Condensed }

/// <summary>
/// The look of everything around the bars: panel, borders, shading, header font, guide.
/// Picking a style also loads its color palette, which can then be tweaked on the Look tab.
/// </summary>
internal sealed class StyleSpec
{
    public required UiStyle Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }

    // Panel behind the frames and the guide
    public Vector4 PanelTop { get; init; }
    public Vector4 PanelBottom { get; init; }
    public Vector4 PanelBorder { get; init; }
    public float PanelBorderWidth { get; init; } = 1f;
    public Vector4 PanelAccent { get; init; }     // line along the top edge
    public float PanelAccentWidth { get; init; } = 2f;
    public Vector4 PanelInner { get; init; }      // second, inner border
    public Vector4 PanelShadow { get; init; }
    public Vector2 PanelShadowOffset { get; init; } = new(0, 2);
    public Vector4 PanelGlow { get; init; }
    public float PanelRounding { get; init; } = 4f;

    // Header
    public HeaderFont Font { get; init; }
    public Vector4 HeaderColor { get; init; }
    public Vector4 HeaderShadow { get; init; }
    public Vector2 HeaderShadowOffset { get; init; } = new(0, 1);
    public bool HeaderUppercase { get; init; }
    public Vector4 SubtextColor { get; init; }

    // Frames
    public Vector4 FrameBorder { get; init; }
    public float Gloss { get; init; }             // white highlight on the top half of bars
    public float Shade { get; init; }             // dark tint on the bottom of bars
    public Vector4 TextShadow { get; init; }
    public Vector2 TextShadowOffset { get; init; } = new(0, 1);

    // Guide
    public Vector4 GuideText { get; init; }
    public Vector4 GuideIconBorder { get; init; }
    public Vector4 GuideRowLit { get; init; }
    public Vector4 GuideLitText { get; init; }
    public Vector4 GuideTableBorder { get; init; }

    /// <summary>Loads this style's palette into the color settings.</summary>
    public required Action<Configuration> ApplyColors { get; init; }
}

internal static class UiStyles
{
    private static Vector4 Hex(string hex, float alpha = 1f)
    {
        var rgb = int.Parse(hex.TrimStart('#'), NumberStyles.HexNumber);
        return new Vector4(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);
    }

    private static Vector4 Rgba(int r, int g, int b, float a) => new(r / 255f, g / 255f, b / 255f, a);

    public static readonly IReadOnlyList<StyleSpec> All =
    [
        new StyleSpec
        {
            Id = UiStyle.Dark, Name = "Dark", Description = "Charcoal panels with a thin gold edge and shaded bars.",
            PanelTop = Hex("26272B"), PanelBottom = Hex("1A1B1E"), PanelBorder = Hex("45464C"),
            PanelAccent = Hex("8C7A4E"), PanelShadow = Rgba(0, 0, 0, 0.45f), PanelRounding = 4,
            Font = HeaderFont.Serif, HeaderColor = Hex("D9C38A"), SubtextColor = Hex("B8B8BE"),
            FrameBorder = Hex("3A3B40"), Gloss = 0.12f, Shade = 0.18f, TextShadow = Rgba(0, 0, 0, 0.9f),
            GuideText = Hex("D8D8DE"), GuideIconBorder = Hex("55565E"), GuideRowLit = Rgba(217, 195, 138, 0.22f),
            GuideLitText = Hex("F0DFAE"), GuideTableBorder = Hex("3A3B40"),
            ApplyColors = c =>
            {
                c.ResetAppearance();
                c.Rounding = 3; c.OutOfRangeAlpha = 0.4f;
                c.BackgroundColor = Hex("111214"); c.TextColor = Hex("F2F2F2");
                c.HighHpColor = Hex("2B7F48"); c.MidHpColor = Hex("8A6B1C"); c.LowHpColor = Hex("AC4135");
                c.DispelColor = Hex("6A3FB0"); c.DeadColor = Hex("44454A"); c.HoverColor = Hex("F0DFAE");
                c.MpColor = Hex("4F7FD9"); c.ShieldColor = Rgba(255, 248, 220, 0.55f);
                c.TankColor = Hex("3F6ED8"); c.HealerColor = Hex("3E9E5C"); c.DpsColor = Hex("C04848");
            },
        },
        new StyleSpec
        {
            Id = UiStyle.Light, Name = "Light", Description = "Parchment panels with brown trim and dark text.",
            PanelTop = Hex("F6F1E6"), PanelBottom = Hex("E4DCC8"), PanelBorder = Hex("B3A27E"),
            PanelAccent = Hex("8A7448"), PanelShadow = Rgba(60, 40, 10, 0.3f), PanelRounding = 6,
            Font = HeaderFont.Serif, HeaderColor = Hex("5A4627"), SubtextColor = Hex("5E513A"),
            FrameBorder = Hex("B8A986"), Gloss = 0.25f, Shade = 0.08f, TextShadow = Rgba(255, 255, 255, 0.6f),
            GuideText = Hex("3B3222"), GuideIconBorder = Hex("B3A27E"), GuideRowLit = Rgba(138, 116, 72, 0.22f),
            GuideLitText = Hex("4A3712"), GuideTableBorder = Hex("C9BB98"),
            ApplyColors = c =>
            {
                c.ResetAppearance();
                c.Rounding = 4; c.OutOfRangeAlpha = 0.45f;
                c.BackgroundColor = Hex("E2D9C4"); c.TextColor = Hex("1F1A10");
                c.HighHpColor = Hex("99D3A7"); c.MidHpColor = Hex("EDD17B"); c.LowHpColor = Hex("EC9A8A");
                c.DispelColor = Hex("CAADF0"); c.DeadColor = Hex("BDB7A8"); c.DisconnectedColor = Hex("A9A8B4"); c.HoverColor = Hex("5A4627");
                c.MpColor = Hex("4A73C9"); c.ShieldColor = Rgba(90, 70, 20, 0.35f);
                c.TankColor = Hex("2F5DB8"); c.HealerColor = Hex("2F7D49"); c.DpsColor = Hex("A33A3A");
            },
        },
        new StyleSpec
        {
            Id = UiStyle.Classic, Name = "Classic", Description = "Deep blue windows with a white double border and flat, bright bars.",
            PanelTop = Hex("3A4FC0"), PanelBottom = Hex("141C7A"), PanelBorder = Hex("E8ECFF"), PanelBorderWidth = 2,
            PanelInner = Hex("6F7FD6"), PanelShadow = Hex("070B3A"), PanelRounding = 6,
            Font = HeaderFont.Retro, HeaderColor = Hex("FFFFFF"), HeaderShadow = Hex("070B3A"), HeaderShadowOffset = new(2, 2),
            SubtextColor = Hex("D7DCFF"),
            FrameBorder = Hex("8F9BEA"), Gloss = 0f, Shade = 0f, TextShadow = Hex("070B3A"), TextShadowOffset = new(1, 1),
            GuideText = Hex("E8ECFF"), GuideIconBorder = Hex("E8ECFF"), GuideRowLit = Rgba(255, 255, 255, 0.18f),
            GuideLitText = Hex("FFF3B0"), GuideTableBorder = Hex("6F7FD6"),
            ApplyColors = c =>
            {
                c.ResetAppearance();
                c.Rounding = 2; c.OutOfRangeAlpha = 0.45f;
                c.BackgroundColor = Rgba(6, 10, 50, 0.55f); c.TextColor = Hex("FFFFFF");
                c.HighHpColor = Hex("23843F"); c.MidHpColor = Hex("8F7414"); c.LowHpColor = Hex("D2402B");
                c.DispelColor = Hex("7A38C0"); c.DeadColor = Hex("5C6190"); c.HoverColor = Hex("FFF3B0");
                c.MpColor = Hex("5FA8FF"); c.ShieldColor = Rgba(255, 255, 255, 0.6f);
                c.TankColor = Hex("2E56C8"); c.HealerColor = Hex("2B8A47"); c.DpsColor = Hex("B23A3A");
            },
        },
        new StyleSpec
        {
            Id = UiStyle.ClearBlue, Name = "Clear Blue", Description = "Frosted, see-through glass with a soft blue glow.",
            PanelTop = Rgba(16, 52, 96, 0.38f), PanelBottom = Rgba(16, 52, 96, 0.38f), PanelBorder = Rgba(140, 210, 255, 0.65f),
            PanelAccent = Rgba(255, 255, 255, 0.25f), PanelAccentWidth = 1, PanelGlow = Rgba(90, 180, 255, 0.35f), PanelRounding = 10,
            Font = HeaderFont.Condensed, HeaderColor = Hex("E2F5FF"), HeaderShadow = Rgba(0, 20, 50, 0.9f), HeaderUppercase = true,
            SubtextColor = Hex("E2F5FF"),
            FrameBorder = Rgba(150, 215, 255, 0.45f), Gloss = 0.2f, Shade = 0.1f, TextShadow = Rgba(0, 20, 50, 0.95f),
            GuideText = Hex("E2F5FF"), GuideIconBorder = Rgba(160, 220, 255, 0.6f), GuideRowLit = Rgba(140, 210, 255, 0.25f),
            GuideLitText = Hex("FFFFFF"), GuideTableBorder = Rgba(150, 215, 255, 0.35f),
            ApplyColors = c =>
            {
                c.ResetAppearance();
                c.Rounding = 6; c.OutOfRangeAlpha = 0.4f;
                c.BackgroundColor = Rgba(8, 24, 48, 0.35f); c.TextColor = Hex("FFFFFF");
                c.HighHpColor = Rgba(40, 150, 110, 0.8f); c.MidHpColor = Rgba(170, 135, 40, 0.8f); c.LowHpColor = Rgba(220, 95, 85, 0.8f);
                c.DispelColor = Rgba(120, 75, 200, 0.85f); c.DeadColor = Rgba(120, 130, 150, 0.5f); c.HoverColor = Rgba(255, 255, 255, 0.8f);
                c.MpColor = Rgba(110, 180, 255, 0.85f); c.ShieldColor = Rgba(230, 248, 255, 0.55f);
                c.TankColor = Rgba(60, 110, 220, 0.85f); c.HealerColor = Rgba(50, 150, 90, 0.85f); c.DpsColor = Rgba(190, 60, 60, 0.85f);
            },
        },
    ];

    public static StyleSpec Get(UiStyle style)
    {
        foreach (var s in All) if (s.Id == style) return s;
        return All[0];
    }

    /// <summary>Switches style and loads its palette.</summary>
    public static void Apply(Configuration c, UiStyle style)
    {
        var spec = Get(style);
        spec.ApplyColors(c);
        c.Style = style;
        c.ThemeName = spec.Name; // the color preset menu shows the style's own palette
    }

    /// <summary>
    /// The game's own UI theme setting (System Configuration > Theme): 0 Dark, 1 Light, 2 Classic FF, 3 Clear Blue.
    /// </summary>
    public static bool TryGetGameStyle(out UiStyle style)
    {
        style = UiStyle.Dark;
        try
        {
            if (!Plugin.GameConfig.TryGet(Dalamud.Game.Config.SystemConfigOption.ColorThemeType, out uint value)) return false;
            if (value > 3) return false;
            style = (UiStyle)value;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
