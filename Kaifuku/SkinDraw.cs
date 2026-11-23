using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ManagedFontAtlas;

namespace Kaifuku;

/// <summary>Drawing helpers for the v2 skins.</summary>
internal static class SkinDraw
{
    public static uint U32(Vector4 c) => ImGui.GetColorU32(c);
    public static uint U32(Vector4 c, float alpha) => ImGui.GetColorU32(c with { W = c.W * alpha });

    /// <summary>Text in a skin font. Returns the width drawn.</summary>
    public static float Text(ImDrawListPtr dl, IFontHandle font, float scale, Vector2 pos, uint color, string text, uint shadow = 0)
    {
        using (font.Push())
        {
            var f = ImGui.GetFont();
            var size = ImGui.GetFontSize() * scale;
            if (shadow != 0) dl.AddText(f, size, pos + new Vector2(0, 1), shadow, text);
            dl.AddText(f, size, pos, color, text);
            return ImGui.CalcTextSize(text).X * scale;
        }
    }

    public static Vector2 Measure(IFontHandle font, float scale, string text)
    {
        using (font.Push()) return ImGui.CalcTextSize(text) * scale;
    }

    public static float Height(IFontHandle font, float scale)
    {
        using (font.Push()) return ImGui.GetFontSize() * scale;
    }

    /// <summary>Cuts text with "..." so it fits the width.</summary>
    public static string Fit(IFontHandle font, float scale, string text, float maxWidth)
    {
        if (maxWidth <= 0) return string.Empty;
        using (font.Push())
        {
            if (ImGui.CalcTextSize(text).X * scale <= maxWidth) return text;
            for (var n = text.Length - 1; n > 0; n--)
            {
                var t = text[..n].TrimEnd() + "...";
                if (ImGui.CalcTextSize(t).X * scale <= maxWidth) return t;
            }
            return string.Empty;
        }
    }

    public static void Panel(ImDrawListPtr dl, Vector2 min, Vector2 max, SkinSpec s, float opacity, bool opaque)
    {
        var fill = opaque ? s.Panel with { W = 1f } : s.Panel with { W = s.Panel.W * opacity };
        dl.AddRectFilled(min, max, U32(fill), s.PanelRadius);
        if (s.PanelLine.W > 0) dl.AddRect(min, max, U32(s.PanelLine), s.PanelRadius, ImDrawFlags.None, 1f);
        if (s.PanelAccent.W > 0)
            dl.AddLine(new Vector2(min.X + s.PanelRadius, min.Y + 0.5f), new Vector2(max.X - s.PanelRadius, min.Y + 0.5f), U32(s.PanelAccent), 1f);
    }

    /// <summary>Diagonal lines clipped to a rectangle: hatching (incoming heal) or stripes (removable debuff).</summary>
    public static void Hatch(ImDrawListPtr dl, Vector2 min, Vector2 max, uint color, float spacing, float thickness, bool forward)
    {
        if (max.X - min.X < 1 || max.Y - min.Y < 1) return;
        dl.PushClipRect(min, max, true);
        var h = max.Y - min.Y;
        for (var x = min.X - h; x < max.X + h; x += spacing)
        {
            if (forward) dl.AddLine(new Vector2(x, max.Y), new Vector2(x + h, min.Y), color, thickness);
            else dl.AddLine(new Vector2(x, min.Y), new Vector2(x + h, max.Y), color, thickness);
        }
        dl.PopClipRect();
    }

    public static void Dashed(ImDrawListPtr dl, Vector2 min, Vector2 max, uint color, float dash = 4, float gap = 3)
    {
        void Line(Vector2 a, Vector2 b)
        {
            var len = Vector2.Distance(a, b);
            var dir = (b - a) / len;
            for (var t = 0f; t < len; t += dash + gap)
                dl.AddLine(a + dir * t, a + dir * MathF.Min(t + dash, len), color, 1f);
        }
        Line(min, new Vector2(max.X, min.Y));
        Line(new Vector2(max.X, min.Y), max);
        Line(max, new Vector2(min.X, max.Y));
        Line(new Vector2(min.X, max.Y), min);
    }

    /// <summary>Heraldry's role crest: a ringed disc with a shield, a cross or a blade.</summary>
    public static void Crest(ImDrawListPtr dl, Vector2 center, float radius, byte role, uint fill, uint ring, uint glyph)
    {
        dl.AddCircleFilled(center, radius, fill);
        dl.AddCircle(center, radius, ring, 0, 1.2f);
        var k = radius * 0.55f;
        Vector2 P(float x, float y) => center + new Vector2(x * k, y * k);
        switch (role)
        {
            case 1: // shield
                Vector2[] s = [P(0, -1), P(0.85f, -0.65f), P(0.85f, 0.1f), P(0, 1), P(-0.85f, 0.1f), P(-0.85f, -0.65f)];
                for (var i = 0; i < s.Length; i++) dl.AddLine(s[i], s[(i + 1) % s.Length], glyph, 1.6f);
                break;
            case 4: // cross
                dl.AddLine(P(0, -1), P(0, 1), glyph, 2f);
                dl.AddLine(P(-1, 0), P(1, 0), glyph, 2f);
                break;
            default: // blade
                dl.AddLine(P(-0.8f, 0.8f), P(0.8f, -0.8f), glyph, 1.8f);
                dl.AddLine(P(-0.6f, 0.1f), P(-0.1f, 0.6f), glyph, 1.6f);
                break;
        }
    }

    public static void Triangle(ImDrawListPtr dl, Vector2 center, float size, uint color)
    {
        var top = center + new Vector2(0, -size * 0.55f);
        var left = center + new Vector2(-size * 0.55f, size * 0.45f);
        var right = center + new Vector2(size * 0.55f, size * 0.45f);
        dl.AddTriangle(top, right, left, color, 1.6f);
        dl.AddLine(center + new Vector2(0, -size * 0.15f), center + new Vector2(0, size * 0.15f), color, 1.6f);
    }
}
