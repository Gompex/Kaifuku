using System.Collections.Generic;
using System.Globalization;
using System.Numerics;

namespace Kaifuku;

/// <summary>Classic = the v1 look (with its UI styles); the others are the v2 skins.</summary>
public enum Skin { Ledger, Heraldry, Signal, Classic }
public enum RoleBadge { JobIcon, Crest, Tag, FilledTag }
public enum JobBadgeStyle { SkinDefault, GameIcon }

/// <summary>One skin's design tokens. Colors that have a setting are loaded into the config when the skin is picked.</summary>
internal sealed class SkinSpec
{
    public required Skin Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }

    // Surfaces and chrome
    public Vector4 Panel, PanelLine, PanelAccent, Line, Text, Muted, Track, RailFill, Warn, OnWarn, Gold;
    public float PanelRadius = 4f, ChipRadius = 2f;

    // State colors not covered by a setting
    public Vector4 OnBuster, OnCritical, OnRaise, OnCleanse, RaiseBar, CleanseLine, IconBg, RowLit, RowLitText;

    // Structure
    public bool TextAboveBar;   // Signal: name and numbers above a separate bar
    public bool Ticks;          // Signal: 10% marks on every bar
    public bool StripedCleanse = true; // removable debuff = stripes over the HP color (all skins)
    public bool CleanseBorder = true;  // ...plus a border (Ledger, Heraldry)
    public float MinHeight = 32f, MaxHeight = 56f; // the range the layout is designed for
    public bool UpperLabels = true;  // status labels (TB, HEAL FULL, ESUNA...)
    public bool UpperHeaders = true; // panel titles, raidwide strip, guide column headers
    public bool InvertedRowLit; // Signal: the held-modifier row is inverted
    public RoleBadge Badge = RoleBadge.JobIcon;
    public float FrameHeight = 40f, FrameRadius = 2f;
    public string BusterLabel = "TB";

    // Fonts: file name + size in px
    public (string File, float Px) NameFont, SubFont, NumberFont, HeaderFont, ChipFont, TagFont;

    // Colors that live in the config (so the Look tab can still tweak them)
    public Vector4 FrameBg, HpHigh, HpMid, HpLow, Cleanse, Critical, Buster, Aggro, Raise, Offline, Mp, Shield, Cast,
        Incoming, Tank, Healer, Dps, Hover;
}

internal static class Skins
{
    private static Vector4 Hex(string hex, float alpha = 1f)
    {
        var rgb = int.Parse(hex.TrimStart('#'), NumberStyles.HexNumber);
        return new Vector4(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);
    }

    private static Vector4 Rgba(int r, int g, int b, float a) => new(r / 255f, g / 255f, b / 255f, a);

    public static readonly IReadOnlyList<SkinSpec> All =
    [
        new SkinSpec
        {
            Id = Skin.Ledger, Name = "Ledger", Description = "Flat instrument panel: charcoal, 1 px lines, condensed names, monospaced numbers.",
            Panel = Hex("18171C"), PanelLine = Hex("34323C"), Line = Hex("34323C"), Text = Hex("EEEAF4"), Muted = Hex("A6A1B2"),
            Track = Hex("0E0D11"), RailFill = Hex("7C7787"), Warn = Hex("E09A2B"), OnWarn = Hex("1A1200"), Gold = Hex("E09A2B"),
            OnBuster = Hex("1A0B08"), OnCritical = Hex("FFFFFF"), OnRaise = Hex("1C1404"), OnCleanse = Hex("FFFFFF"),
            RaiseBar = Rgba(217, 178, 74, 0.35f), CleanseLine = Hex("B79CF2"), IconBg = Hex("2E2D35"),
            RowLit = Rgba(224, 154, 43, 0.16f), RowLitText = Hex("E09A2B"),
            PanelRadius = 4, ChipRadius = 2, FrameRadius = 2, FrameHeight = 40, Badge = RoleBadge.FilledTag, BusterLabel = "TB",
            NameFont = ("BarlowCondensed-SemiBold.ttf", 16), SubFont = ("BarlowCondensed-SemiBold.ttf", 12),
            NumberFont = ("JetBrainsMono-Bold.ttf", 13), HeaderFont = ("BarlowCondensed-SemiBold.ttf", 14),
            ChipFont = ("BarlowCondensed-SemiBold.ttf", 12), TagFont = ("JetBrainsMono-Bold.ttf", 9),
            FrameBg = Hex("222128"), HpHigh = Hex("3B7F56"), HpMid = Hex("8E6614"), HpLow = Hex("B4432F"), Cleanse = Hex("6F4DB8"),
            Critical = Hex("C2185B"), Buster = Hex("FF6A4D"), Aggro = Hex("E5483A"), Raise = Hex("D9B24A"), Offline = Hex("2B2A31"),
            Mp = Hex("5A86D6"), Shield = Rgba(255, 244, 214, 0.6f), Cast = Hex("8FC7FF"), Incoming = Rgba(155, 227, 176, 0.45f),
            Tank = Hex("3D63B8"), Healer = Hex("2F7A4C"), Dps = Hex("9C3A3A"), Hover = Hex("EEEAF4", 0.9f),
        },
        new SkinSpec
        {
            Id = Skin.Heraldry, Name = "Heraldry", Description = "Ink navy, parchment text, gold hairlines and role crests.",
            Panel = Hex("0F1522"), PanelLine = Hex("2E3A55"), PanelAccent = Hex("C9A45A"), Line = Hex("2E3A55"), Text = Hex("F1E8D6"),
            Muted = Hex("B3A88F"), Track = Hex("090D16"), RailFill = Hex("6F7790"), Warn = Hex("D79B35"), OnWarn = Hex("1A1200"),
            Gold = Hex("C9A45A"),
            OnBuster = Hex("1A0B06"), OnCritical = Hex("FFFFFF"), OnRaise = Hex("1C1404"), OnCleanse = Hex("FFFFFF"),
            RaiseBar = Rgba(216, 180, 90, 0.32f), CleanseLine = Hex("C4A8F5"), IconBg = Hex("1F2A42"),
            RowLit = Rgba(201, 164, 90, 0.18f), RowLitText = Hex("C9A45A"),
            PanelRadius = 6, ChipRadius = 9, FrameRadius = 5, FrameHeight = 40, Badge = RoleBadge.Crest, UpperLabels = false, UpperHeaders = false,
            BusterLabel = "buster",
            NameFont = ("AlegreyaSansSC-Bold.ttf", 16), SubFont = ("AlegreyaSans-Bold.ttf", 12),
            NumberFont = ("AlegreyaSans-Bold.ttf", 15), HeaderFont = ("Cinzel.ttf", 16),
            ChipFont = ("AlegreyaSansSC-Bold.ttf", 13), TagFont = ("AlegreyaSansSC-Bold.ttf", 10),
            FrameBg = Hex("172033"), HpHigh = Hex("2E7A5E"), HpMid = Hex("8A6A1E"), HpLow = Hex("A8323F"), Cleanse = Hex("6A4BAE"),
            Critical = Hex("B0124F"), Buster = Hex("F06A3E"), Aggro = Hex("D9473B"), Raise = Hex("D8B45A"), Offline = Hex("222B3D"),
            Mp = Hex("4E78C8"), Shield = Rgba(241, 232, 214, 0.55f), Cast = Hex("9CC8FF"), Incoming = Rgba(150, 220, 170, 0.42f),
            Tank = Hex("2F4F9E"), Healer = Hex("2C6E4A"), Dps = Hex("8E2F35"), Hover = Hex("C9A45A"),
        },
        new SkinSpec
        {
            Id = Skin.Signal, Name = "Signal", Description = "Accessibility first: text above the bar, colorblind-safe colors, ticks and patterns.",
            Panel = Hex("0B0D0E"), Line = new Vector4(0, 0, 0, 0), Text = Hex("F4F6F7"), Muted = Hex("A9B1B6"),
            Track = Hex("262B2F"), RailFill = Hex("7D868C"), Warn = Hex("E69F00"), OnWarn = Hex("1A1200"), Gold = Hex("E69F00"),
            OnBuster = Hex("1A0606"), OnCritical = Hex("141400"), OnRaise = Hex("04121C"), OnCleanse = Hex("1A0612"),
            RaiseBar = Hex("56B4E9"), CleanseLine = Hex("CC79A7"), IconBg = Hex("22272B"),
            RowLit = Hex("F4F6F7"), RowLitText = Hex("0B0D0E"), InvertedRowLit = true,
            PanelRadius = 8, ChipRadius = 3, FrameRadius = 6, FrameHeight = 44, Badge = RoleBadge.Tag,
            TextAboveBar = true, Ticks = true, CleanseBorder = false, MinHeight = 40, MaxHeight = 60,
            BusterLabel = "BUSTER", UpperHeaders = false,
            NameFont = ("AtkinsonHyperlegible-Bold.ttf", 14), SubFont = ("AtkinsonHyperlegible-Regular.ttf", 12),
            NumberFont = ("AtkinsonHyperlegible-Bold.ttf", 16), HeaderFont = ("AtkinsonHyperlegible-Bold.ttf", 15),
            ChipFont = ("AtkinsonHyperlegible-Bold.ttf", 12), TagFont = ("AtkinsonHyperlegible-Bold.ttf", 11),
            FrameBg = Hex("15181A"), HpHigh = Hex("009E73"), HpMid = Hex("E69F00"), HpLow = Hex("D55E00"), Cleanse = Hex("CC79A7"),
            Critical = Hex("F0E442"), Buster = Hex("FF5C5C"), Aggro = Hex("FF5C5C"), Raise = Hex("56B4E9"), Offline = Hex("2A2F33"),
            Mp = Hex("3A79C4"), Shield = Rgba(244, 246, 247, 0.7f), Cast = Hex("F4F6F7"), Incoming = Rgba(0, 158, 115, 0.55f),
            Tank = Hex("6FA8FF"), Healer = Hex("3FD39E"), Dps = Hex("FF8A7A"), Hover = Hex("F4F6F7"),
        },
    ];

    public static SkinSpec? Get(Skin skin)
    {
        foreach (var s in All) if (s.Id == skin) return s;
        return null; // Classic
    }

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

    public static float ClampHeight(SkinSpec s, float height) => System.Math.Clamp(height, s.MinHeight, s.MaxHeight);

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
