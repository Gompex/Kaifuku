using System.Collections.Generic;
using System.Globalization;
using System.Numerics;

namespace Kaifuku;

public enum JobBadgeStyle { SkinDefault, GameIcon }

/// <summary>Kaifuku side of the skins: the tokens live in Skins.Core (SkinCatalog); applying them to the config lives here.</summary>
internal static class Skins
{
    private static Vector4 Hex(string hex, float alpha = 1f) => SkinCatalog.Hex(hex, alpha);

    public static IReadOnlyList<SkinSpec> All => SkinCatalog.All;

    public static SkinSpec? Get(Skin skin) => SkinCatalog.Get(skin);

    public static float ClampHeight(SkinSpec s, float height) => SkinCatalog.ClampHeight(s, height);

    /// <summary>
    /// Switches skin. Colors (and the skin's corner radius) always load; the layout (frame size, spacing,
    /// alliance columns) only when <paramref name="applyLayout"/> is true. Otherwise the current frame
    /// height is just kept inside the range the skin is designed for.
    /// </summary>
    public static void Apply(Configuration c, Skin skin, bool applyLayout)
    {
        c.Skin = skin;
        var s = Get(skin);
        if (s == null)
        {
            UiStyles.Apply(c, c.Style); // back to the v1 look
            if (applyLayout)
            {
                var d = new Configuration();
                c.FrameHeight = d.FrameHeight;
                c.FrameWidth = d.FrameWidth;
                c.Spacing = d.Spacing;
            }
            return;
        }

        ApplyColors(c, s);
        if (applyLayout) ApplyLayout(c, s);
        else c.FrameHeight = ClampHeight(s, c.FrameHeight);
    }


    public static void ApplyLayout(Configuration c, SkinSpec s)
    {
        c.FrameHeight = s.FrameHeight;
        c.FrameWidth = 280f; // the skins are designed for wide frames (name, second line, status label, icons)
        c.Spacing = 6f;
        // Alliance frames: two columns that line up with the party frames above them.
        c.AllianceColumns = 2;
        c.AllianceFrameWidth = (c.FrameWidth + c.AggroBarWidth + 6f - c.Spacing) / 2f;
        c.AllianceFrameHeight = 24f;
        c.ShowPanel = true;
    }

    public static void ApplyColors(Configuration c, SkinSpec s)
    {
        c.ResetAppearance();
        c.ThemeName = "Default";
        c.Rounding = s.FrameRadius;
        c.OutOfRangeAlpha = 0.35f;
        c.BackgroundColor = s.FrameBg; c.TextColor = s.Text;
        c.HighHpColor = s.HpHigh; c.MidHpColor = s.HpMid; c.LowHpColor = s.HpLow;
        c.DispelColor = s.Cleanse; c.MustHealColor = s.Critical; c.BusterColor = s.Buster; c.AggroColor = s.Aggro;
        c.RaiseColor = s.Raise; c.DisconnectedColor = s.Offline; c.DeadColor = s.Offline;
        c.MpColor = s.Mp; c.ShieldColor = s.Shield; c.CastColor = s.Cast; c.IncomingHealColor = s.Incoming;
        c.TankColor = s.Tank; c.HealerColor = s.Healer; c.DpsColor = s.Dps; c.HoverColor = s.Hover;
        c.RaidwideColor = s.Warn;
    }

    /// <summary>Color variants that work on top of any v2 skin (they keep its chrome, radius and fonts).</summary>
    public sealed record Variant(string Name, string Description, System.Action<Configuration, SkinSpec> Apply);

    public static readonly IReadOnlyList<Variant> Variants =
    [
        new("Default", "The skin's own palette.", (c, s) => ApplyColors(c, s)),
        new("Colorblind friendly", "Blue / yellow / orange health and pink removable debuffs (Okabe-Ito).", (c, s) =>
        {
            ApplyColors(c, s);
            c.HighHpColor = Hex("0072B2"); c.MidHpColor = Hex("B58900"); c.LowHpColor = Hex("D55E00");
            c.DispelColor = Hex("CC79A7"); c.TankColor = Hex("0072B2"); c.HealerColor = Hex("009E73"); c.DpsColor = Hex("D55E00");
        }),
        new("High contrast", "Brighter health colors and pure white text for busy fights.", (c, s) =>
        {
            ApplyColors(c, s);
            c.HighHpColor = Hex("13A452"); c.MidHpColor = Hex("B8860B"); c.LowHpColor = Hex("E0221C");
            c.TextColor = Hex("FFFFFF"); c.OutOfRangeAlpha = 0.45f;
        }),
        new("Role colors", "Health bars in the skin's tank / healer / DPS colors instead of by health.", (c, s) =>
        {
            ApplyColors(c, s);
            c.ColorMode = HpColorMode.ByRole;
        }),
    ];
}
