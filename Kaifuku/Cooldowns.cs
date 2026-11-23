using System.Numerics;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace Kaifuku;

/// <summary>Hotbar-style cooldown shading, timer and charges, drawn over an icon in the binding guide.</summary>
internal static unsafe class Cooldowns
{
    public static void Overlay(uint bindingId, Vector2 min, Vector2 max)
    {
        if (bindingId == 0 || MacroRunner.IsMacro(bindingId)) return;

        // "Swiftcast + raise" shows Swiftcast's cooldown: that's the part that matters.
        var id = Casting.IsSwiftRaise(bindingId) ? Casting.Swiftcast : bindingId;
        if (id == Casting.EukrasianDiagnosis) id = Casting.Diagnosis;

        var am = ActionManager.Instance();
        if (am == null) return;

        var total = am->GetRecastTime(ActionType.Action, id);
        var elapsed = am->GetRecastTimeElapsed(ActionType.Action, id);
        var level = (uint)(Plugin.ObjectTable.LocalPlayer?.Level ?? 100);
        var maxCharges = ActionManager.GetMaxCharges(id, level);

        var dl = ImGui.GetWindowDrawList();
        var font = ImGui.GetFont();
        var height = max.Y - min.Y;

        float remaining;
        float fraction;
        var available = true;
        if (maxCharges > 1)
        {
            var charges = am->GetCurrentCharges(id);
            var perCharge = total / maxCharges;
            if (charges >= maxCharges || perCharge <= 3f || total <= 0f)
            {
                DrawCharges(dl, font, min, max, charges);
                return;
            }
            remaining = perCharge - elapsed % perCharge;
            fraction = remaining / perCharge;
            available = charges > 0;
            Shade(dl, min, max, fraction, available);
            Timer(dl, font, min, max, remaining);
            DrawCharges(dl, font, min, max, charges);
            return;
        }

        // Ignore the GCD itself (every GCD heal would flicker), only real cooldowns.
        if (total <= 3f || elapsed <= 0f || elapsed >= total) return;
        remaining = total - elapsed;
        fraction = remaining / total;
        Shade(dl, min, max, fraction, available: false);
        Timer(dl, font, min, max, remaining);

        static void Shade(ImDrawListPtr dl, Vector2 min, Vector2 max, float fraction, bool available)
        {
            var alpha = available ? 0.35f : 0.65f;
            dl.AddRectFilled(min, new Vector2(max.X, min.Y + (max.Y - min.Y) * fraction),
                ImGui.GetColorU32(new Vector4(0f, 0f, 0f, alpha)));
        }
    }

    private static void Timer(ImDrawListPtr dl, ImFontPtr font, Vector2 min, Vector2 max, float remaining)
    {
        var text = remaining >= 10 ? $"{remaining:0}" : $"{remaining:0.0}";
        var size = (max.Y - min.Y) * 0.45f;
        var width = ImGui.CalcTextSize(text).X * size / ImGui.GetFontSize();
        var pos = new Vector2((min.X + max.X - width) / 2, (min.Y + max.Y - size) / 2);
        dl.AddText(font, size, pos + Vector2.One, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.9f)), text);
        dl.AddText(font, size, pos, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f)), text);
    }

    private static void DrawCharges(ImDrawListPtr dl, ImFontPtr font, Vector2 min, Vector2 max, uint charges)
    {
        var text = charges.ToString();
        var size = (max.Y - min.Y) * 0.38f;
        var width = ImGui.CalcTextSize(text).X * size / ImGui.GetFontSize();
        var pos = new Vector2(max.X - width - 2, max.Y - size - 1);
        dl.AddText(font, size, pos + Vector2.One, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.9f)), text);
        dl.AddText(font, size, pos, ImGui.GetColorU32(new Vector4(1f, 0.95f, 0.7f, 1f)), text);
    }
}
