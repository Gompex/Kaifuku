using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;

namespace Kaifuku;

/// <summary>Ready-made looks. Applying one changes colors and shape only; frame size, text size and layout are kept.</summary>
internal static class Themes
{
    public sealed record Theme(string Name, string Description, Action<Configuration> Apply)
    {
        private Vector4[]? swatch;

        /// <summary>A few representative colors for the picker.</summary>
        public Vector4[] Swatch
        {
            get
            {
                if (swatch != null) return swatch;
                var c = new Configuration();
                Apply(c);
                swatch = c.ColorMode == HpColorMode.ByRole
                    ? [c.TankColor, c.HealerColor, c.DpsColor, c.DispelColor]
                    : [c.HighHpColor, c.MidHpColor, c.LowHpColor, c.DispelColor];
                return swatch;
            }
        }
    }

    private static Vector4 Hex(string hex, float alpha = 1f)
    {
        var rgb = int.Parse(hex.TrimStart('#'), NumberStyles.HexNumber);
        return new Vector4(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);
    }

    public static readonly IReadOnlyList<Theme> All =
    [
        new("Default", "The original look: dark background, green / yellow / red health.",
            c => c.ResetAppearance()),

        new("Classic", "Square, bold raid-frame look with bright traffic-light health colors.", c =>
        {
            c.ResetAppearance();
            c.Rounding = 0; c.OutOfRangeAlpha = 0.4f;
            c.BackgroundColor = Hex("000000", 0.9f);
            c.HighHpColor = Hex("20C020"); c.MidHpColor = Hex("E0C000"); c.LowHpColor = Hex("E02020");
            c.DispelColor = Hex("7B2FD6"); c.MpColor = Hex("3070FF"); c.ShieldColor = Hex("FFFFFF", 0.6f);
        }),

        new("Midnight", "Muted, low-glare colors on a near-black background.", c =>
        {
            c.ResetAppearance();
            c.Rounding = 6; c.OutOfRangeAlpha = 0.3f;
            c.BackgroundColor = Hex("12141A", 0.9f); c.TextColor = Hex("D8DEE9");
            c.HighHpColor = Hex("4F9D69"); c.MidHpColor = Hex("C9A54C"); c.LowHpColor = Hex("B5484B");
            c.DeadColor = Hex("3B4252"); c.DispelColor = Hex("7650B8"); c.HoverColor = Hex("88C0D0", 0.9f);
            c.MpColor = Hex("5E81AC"); c.ShieldColor = Hex("E5E9F0", 0.45f);
        }),

        new("Eorzea Gold", "Navy and gold, in the spirit of the game's own interface.", c =>
        {
            c.ResetAppearance();
            c.Rounding = 3;
            c.BackgroundColor = Hex("0E1626", 0.9f); c.TextColor = Hex("F3E3B5");
            c.HighHpColor = Hex("3E9E7A"); c.MidHpColor = Hex("D8A93B"); c.LowHpColor = Hex("B83A3A");
            c.DeadColor = Hex("4A4A55"); c.DispelColor = Hex("8456C4"); c.HoverColor = Hex("E8C46A");
            c.MpColor = Hex("4A7BD0"); c.ShieldColor = Hex("FFF2C2", 0.5f);
        }),

        new("Glass", "See-through frames that keep the fight visible behind them.", c =>
        {
            c.ResetAppearance();
            c.Rounding = 8; c.OutOfRangeAlpha = 0.25f;
            c.BackgroundColor = Hex("000000", 0.25f);
            c.HighHpColor = Hex("3FBF5F", 0.55f); c.MidHpColor = Hex("E6C34A", 0.55f); c.LowHpColor = Hex("E04848", 0.65f);
            c.DeadColor = Hex("707070", 0.4f); c.DispelColor = Hex("8A45D6", 0.9f); c.HoverColor = Hex("FFFFFF", 0.6f);
            c.MpColor = Hex("4A80E0", 0.6f); c.ShieldColor = Hex("FFFFFF", 0.35f);
        }),

        new("Role Colors", "Health bars colored by role (tank, healer, DPS) instead of by health.", c =>
        {
            c.ResetAppearance();
            c.ColorMode = HpColorMode.ByRole;
            c.TankColor = Hex("3D6FD6"); c.HealerColor = Hex("3FA85A"); c.DpsColor = Hex("C94141");
        }),

        new("High Contrast", "Maximum contrast for readability in busy fights.", c =>
        {
            c.ResetAppearance();
            c.Rounding = 0; c.OutOfRangeAlpha = 0.5f;
            c.BackgroundColor = Hex("000000", 1f);
            c.HighHpColor = Hex("00FF55"); c.MidHpColor = Hex("FFFF00"); c.LowHpColor = Hex("FF2020");
            c.DeadColor = Hex("808080"); c.DispelColor = Hex("FF00FF"); c.HoverColor = Hex("00FFFF");
            c.MpColor = Hex("00A0FF"); c.ShieldColor = Hex("FFFFFF", 0.8f);
        }),

        new("Colorblind Friendly", "Blue / yellow / orange health instead of green / red (Okabe-Ito palette).", c =>
        {
            c.ResetAppearance();
            c.BackgroundColor = Hex("101010", 0.85f);
            c.HighHpColor = Hex("0072B2"); c.MidHpColor = Hex("F0E442"); c.LowHpColor = Hex("D55E00");
            c.DeadColor = Hex("555555"); c.DispelColor = Hex("CC79A7");
            c.MpColor = Hex("56B4E9"); c.ShieldColor = Hex("FFFFFF", 0.55f);
            c.TankColor = Hex("0072B2"); c.HealerColor = Hex("009E73"); c.DpsColor = Hex("D55E00");
        }),
    ];
}
