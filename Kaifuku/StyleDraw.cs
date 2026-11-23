using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Kaifuku;

/// <summary>Drawing helpers shared by the frames and the guide.</summary>
internal static class StyleDraw
{
    private static uint U32(Vector4 c) => ImGui.GetColorU32(c);
    private static Vector4 Fade(Vector4 c, float opacity) => c with { W = c.W * opacity };

    /// <summary>The window panel: glow, drop shadow, vertical gradient, border(s) and top accent line.</summary>
    public static void Panel(ImDrawListPtr dl, Vector2 min, Vector2 max, StyleSpec s, float opacity, bool opaque = false)
    {
        var r = s.PanelRounding;

        if (s.PanelGlow.W > 0)
        {
            for (var i = 3; i >= 1; i--)
            {
                var grow = new Vector2(i * 2f);
                dl.AddRect(min - grow, max + grow, U32(Fade(s.PanelGlow, opacity / i)), r + i * 2f, ImDrawFlags.None, 2f);
            }
        }

        if (s.PanelShadow.W > 0)
            dl.AddRectFilled(min + s.PanelShadowOffset, max + s.PanelShadowOffset, U32(Fade(s.PanelShadow, opacity)), r);

        // Rounded vertical gradient: bottom color everywhere, top color on the rounded top strip,
        // and a gradient between the two straight sections.
        // Opaque: the style's panel colors at full strength, ignoring their own transparency (Clear Blue)
        // and the opacity slider.
        var top = U32(opaque ? s.PanelTop with { W = 1f } : Fade(s.PanelTop, opacity));
        var bottom = U32(opaque ? s.PanelBottom with { W = 1f } : Fade(s.PanelBottom, opacity));
        dl.AddRectFilled(min, max, bottom, r);
        if (r > 0) dl.AddRectFilled(min, new Vector2(max.X, min.Y + r), top, r, ImDrawFlags.RoundCornersTop);
        dl.AddRectFilledMultiColor(new Vector2(min.X, min.Y + r), new Vector2(max.X, max.Y - r), top, top, bottom, bottom);

        if (s.PanelBorder.W > 0)
            dl.AddRect(min, max, U32(s.PanelBorder), r, ImDrawFlags.None, s.PanelBorderWidth);

        if (s.PanelInner.W > 0)
        {
            var inset = new Vector2(s.PanelBorderWidth + 1.5f);
            dl.AddRect(min + inset, max - inset, U32(s.PanelInner), r > 2 ? r - 2 : 0, ImDrawFlags.None, 1f);
        }

        if (s.PanelAccent.W > 0)
        {
            var y = min.Y + s.PanelAccentWidth / 2;
            dl.AddLine(new Vector2(min.X + r, y), new Vector2(max.X - r, y), U32(s.PanelAccent), s.PanelAccentWidth);
        }
    }

    /// <summary>A bar with the style's shading: gloss on the top half, shade on the bottom.</summary>
    public static void Bar(ImDrawListPtr dl, Vector2 min, Vector2 max, uint color, float rounding, StyleSpec s, float alpha)
    {
        dl.AddRectFilled(min, max, color, rounding);
        var h = max.Y - min.Y;
        if (s.Gloss > 0)
            dl.AddRectFilled(min, new Vector2(max.X, min.Y + h * 0.5f), U32(new Vector4(1f, 1f, 1f, s.Gloss * alpha)),
                rounding, ImDrawFlags.RoundCornersTop);
        if (s.Shade > 0)
            dl.AddRectFilled(new Vector2(min.X, max.Y - h * 0.4f), max, U32(new Vector4(0f, 0f, 0f, s.Shade * alpha)),
                rounding, ImDrawFlags.RoundCornersBottom);
    }

    /// <summary>A header line in the style's game font, with an optional right-aligned note.</summary>
    public static void Header(Plugin plugin, StyleSpec s, string title, string? note, float width)
    {
        var text = s.HeaderUppercase ? title.ToUpperInvariant() : title;
        using (plugin.GetHeaderFont(s.Font).Push())
        {
            if (s.HeaderShadow.W > 0)
                ImGui.GetWindowDrawList().AddText(ImGui.GetCursorScreenPos() + s.HeaderShadowOffset, U32(s.HeaderShadow), text);
            ImGui.TextColored(s.HeaderColor, text);
        }

        if (note == null) return;
        var noteWidth = ImGui.CalcTextSize(note).X;
        ImGui.SameLine(ImGui.GetCursorStartPos().X + width - noteWidth);
        ImGui.TextColored(s.SubtextColor, note);
    }
}
