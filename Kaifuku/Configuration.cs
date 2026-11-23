using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Configuration;

namespace Kaifuku;

public enum SortMode { PartyOrder, Role, Name }
public enum HpTextMode { Percent, Current, Missing, None }
public enum NameMode { Full, FirstName, Initials, None }
public enum HpColorMode { ByHealth, ByRole }
public enum DispelStyle { BarAndBorder, BarColor, BorderOnly }
public enum GuideLayout { Table, List }
public enum BuffFilter { MineOnly, MineAndMyList, AllBuffs }
public enum RangeMode { BoundSkill, Distance30 }
public enum ContentKind { Overworld, Dungeon, Trial, Raid, AllianceRaid, OtherDuty }

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 2;

    // General
    public bool Locked { get; set; } = true;
    public bool AutoPositioned { get; set; } = false;
    public bool ShowOnAllJobs { get; set; } = false;
    public bool Preview { get; set; } = false;
    public float UiScale { get; set; } = 1f; // whole plugin: frames, text, guide
    public bool ShowFrames { get; set; } = true; // remembered across restarts

    // Binding guide
    public bool ShowGuide { get; set; } = false;
    public bool GuideLocked { get; set; } = false;
    public GuideLayout GuideStyle { get; set; } = GuideLayout.Table;
    public bool GuideHideEmpty { get; set; } = true;
    public bool GuideShowNames { get; set; } = true;
    public bool GuideHighlightModifier { get; set; } = true;
    public float GuideIconSize { get; set; } = 24f;
    public float GuideBackgroundAlpha { get; set; } = 1f; // guide panel opacity
    public bool GuideOpaque { get; set; } = false;

    // Look
    public Skin Skin { get; set; } = Skin.Ledger;   // v2 skin; Classic = the v1 look with UiStyle
    public bool SkinApplied { get; set; } = false;  // the default skin's colors were loaded once
    public JobBadgeStyle JobBadge { get; set; } = JobBadgeStyle.SkinDefault; // v2: skin's own badge or the game's job icon
    public UiStyle Style { get; set; } = UiStyle.Dark;
    public bool FollowGameTheme { get; set; } = true;
    public bool ShowPanel { get; set; } = true;
    public bool ShowPanelHeader { get; set; } = true;
    public float PanelOpacity { get; set; } = 1f;
    public bool PanelOpaque { get; set; } = false;
    public string ThemeName { get; set; } = "Default";
    public bool TextShadow { get; set; } = true;
    public bool AutoTextContrast { get; set; } = true;
    public float FrameWidth { get; set; } = 170f;
    public float FrameHeight { get; set; } = 34f;
    public float FontScale { get; set; } = 1f;
    public float Rounding { get; set; } = 4f;
    public float OutOfRangeAlpha { get; set; } = 0.35f;
    public HpColorMode ColorMode { get; set; } = HpColorMode.ByHealth;
    public float MidHpThreshold { get; set; } = 0.70f;
    public float LowHpThreshold { get; set; } = 0.35f;
    public Vector4 HighHpColor { get; set; } = new(0.25f, 0.75f, 0.30f, 1f);
    public Vector4 MidHpColor { get; set; } = new(0.90f, 0.75f, 0.20f, 1f);
    public Vector4 LowHpColor { get; set; } = new(0.85f, 0.20f, 0.20f, 1f);
    public Vector4 TankColor { get; set; } = new(0.25f, 0.45f, 0.90f, 1f);
    public Vector4 HealerColor { get; set; } = new(0.25f, 0.75f, 0.30f, 1f);
    public Vector4 DpsColor { get; set; } = new(0.80f, 0.25f, 0.25f, 1f);
    public Vector4 DeadColor { get; set; } = new(0.35f, 0.35f, 0.35f, 1f);
    public Vector4 DisconnectedColor { get; set; } = new(0.28f, 0.30f, 0.38f, 0.9f);
    public Vector4 BackgroundColor { get; set; } = new(0.08f, 0.08f, 0.08f, 0.85f);
    public Vector4 TextColor { get; set; } = new(1f, 1f, 1f, 1f);
    public Vector4 ShieldColor { get; set; } = new(1f, 1f, 0.85f, 0.55f);
    public Vector4 MpColor { get; set; } = new(0.35f, 0.55f, 0.95f, 1f);
    public Vector4 DispelColor { get; set; } = new(0.48f, 0.25f, 0.81f, 1f);
    public DispelStyle DispelHighlight { get; set; } = DispelStyle.BarAndBorder;
    public Vector4 HoverColor { get; set; } = new(1f, 1f, 1f, 0.9f);
    public Vector4 BusterColor { get; set; } = new(0.95f, 0.15f, 0.15f, 1f);
    public Vector4 HotColor { get; set; } = new(0.45f, 0.95f, 0.55f, 1f);
    public Vector4 AggroColor { get; set; } = new(0.92f, 0.22f, 0.18f, 1f);
    public Vector4 RaiseColor { get; set; } = new(0.95f, 0.80f, 0.35f, 1f);
    public Vector4 CastColor { get; set; } = new(0.75f, 0.90f, 1f, 0.95f);
    public Vector4 RaidwideColor { get; set; } = new(1f, 0.45f, 0.2f, 1f);
    public Vector4 MustHealColor { get; set; } = new(1f, 0.18f, 0.53f, 1f);
    public Vector4 IncomingHealColor { get; set; } = new(0.55f, 1f, 0.65f, 0.35f);

    // Layout
    public int Columns { get; set; } = 1;
    public float Spacing { get; set; } = 2f;
    public SortMode Sort { get; set; } = SortMode.PartyOrder;
    public bool SelfFirst { get; set; } = false;
    public bool GroupByRole { get; set; } = false;
    public float GroupGap { get; set; } = 8f;

    // Alliance raid frames (the other two parties)
    public bool ShowAlliance { get; set; } = true;
    public float AllianceFrameWidth { get; set; } = 100f;
    public float AllianceFrameHeight { get; set; } = 22f;
    public int AllianceColumns { get; set; } = 4;

    // Display
    public NameMode Names { get; set; } = NameMode.Full;
    public HpTextMode HpText { get; set; } = HpTextMode.Percent;
    public bool ShowMpBar { get; set; } = true;
    public float MpBarHeight { get; set; } = 4f;
    public bool ShowShields { get; set; } = true;
    public bool ShowRoleIcon { get; set; } = true;
    public bool ShowMyBuffs { get; set; } = true; // "Show buffs"
    public BuffFilter BuffMode { get; set; } = BuffFilter.MineAndMyList;
    public HashSet<uint> AlwaysShowStatuses { get; set; } = new(); // buffs shown from anyone
    public HashSet<uint> HiddenStatuses { get; set; } = new();     // never shown
    public bool CommonShieldsSeeded { get; set; } = false;
    public bool ShowDebuffs { get; set; } = true;
    public bool ShowStatusTimers { get; set; } = true;
    public float StatusIconSize { get; set; } = 18f;
    public int MaxStatusIcons { get; set; } = 4;

    // Tank buster warning
    public bool TankBusterWarning { get; set; } = true;
    public float BusterMinCastTime { get; set; } = 3f;
    public bool BusterAnyMember { get; set; } = false;

    // HoT marker
    public bool ShowHotIndicator { get; set; } = false; // corner marker removed; HoTs show as buff icons
    public bool HotAnyone { get; set; } = false;
    public HashSet<uint> HotStatuses { get; set; } = new();
    public bool CommonHotsSeeded { get; set; } = false;

    // Aggro
    public bool ShowAggro { get; set; } = true;
    public bool ShowEnmityGauge { get; set; } = true;
    public bool ShowEnemyCount { get; set; } = true;
    public bool ShowAggroBadge { get; set; } = false;
    public float AggroBarWidth { get; set; } = 7f;

    public RangeMode RangeCheck { get; set; } = RangeMode.BoundSkill;
    public bool BlockDoubleRaise { get; set; } = true;
    public bool ShowCastBars { get; set; } = true;
    public bool ShowCastName { get; set; } = true;
    public bool ShowPartnerMarker { get; set; } = true;
    public bool ShowPetStatus { get; set; } = true;

    public bool RaidwideWarning { get; set; } = true;
    public float RaidwideMinCastTime { get; set; } = 2.5f;
    public bool ShowMustHeal { get; set; } = true;
    public HashSet<uint> MustHealStatuses { get; set; } = new();
    public bool ShowTankCooldowns { get; set; } = true;
    public HashSet<uint> TankCooldownStatuses { get; set; } = new();
    public bool ShowIncomingHeals { get; set; } = true;
    public bool AwarenessSeeded { get; set; } = false;

    // Profiles: name -> saved look/layout/display settings, and when to switch to them.
    public Dictionary<string, string> Profiles { get; set; } = new();
    public Dictionary<uint, string> JobProfiles { get; set; } = new();
    public Dictionary<ContentKind, string> ContentProfiles { get; set; } = new();
    public string ActiveProfile { get; set; } = string.Empty;

    // Bindings: job -> ("Shift+Left" -> action ID). Only what the user changed; the rest comes from Bindings.Defaults.
    public Dictionary<uint, Dictionary<string, uint>> JobBindings { get; set; } = new();

    // Macros run with the clicked member as <mo>; optionally also make them your target (<t>).
    public bool MacroAlsoTarget { get; set; } = false;

    public void ResetLook()
    {
        var d = new Configuration();
        FrameWidth = d.FrameWidth; FrameHeight = d.FrameHeight; FontScale = d.FontScale;
        ResetAppearance();
        ThemeName = d.ThemeName;
    }

    /// <summary>Colors and shape only (what a theme changes); frame size and text size are kept.</summary>
    public void ResetAppearance()
    {
        var d = new Configuration();
        Rounding = d.Rounding; TextShadow = d.TextShadow; AutoTextContrast = d.AutoTextContrast;
        OutOfRangeAlpha = d.OutOfRangeAlpha; ColorMode = d.ColorMode;
        MidHpThreshold = d.MidHpThreshold; LowHpThreshold = d.LowHpThreshold;
        HighHpColor = d.HighHpColor; MidHpColor = d.MidHpColor; LowHpColor = d.LowHpColor;
        TankColor = d.TankColor; HealerColor = d.HealerColor; DpsColor = d.DpsColor; DeadColor = d.DeadColor;
        DisconnectedColor = d.DisconnectedColor; BusterColor = d.BusterColor; HotColor = d.HotColor; AggroColor = d.AggroColor;
        RaiseColor = d.RaiseColor; CastColor = d.CastColor;
        RaidwideColor = d.RaidwideColor; MustHealColor = d.MustHealColor; IncomingHealColor = d.IncomingHealColor;
        BackgroundColor = d.BackgroundColor; TextColor = d.TextColor; ShieldColor = d.ShieldColor;
        MpColor = d.MpColor; DispelColor = d.DispelColor; HoverColor = d.HoverColor;
        DispelHighlight = d.DispelHighlight;
    }

    public void ResetLayout()
    {
        var d = new Configuration();
        Columns = d.Columns; Spacing = d.Spacing; Sort = d.Sort; SelfFirst = d.SelfFirst;
        GroupByRole = d.GroupByRole; GroupGap = d.GroupGap;
        ShowAlliance = d.ShowAlliance; AllianceFrameWidth = d.AllianceFrameWidth;
        AllianceFrameHeight = d.AllianceFrameHeight; AllianceColumns = d.AllianceColumns;
    }

    public void ResetDisplay()
    {
        var d = new Configuration();
        Names = d.Names; HpText = d.HpText; ShowMpBar = d.ShowMpBar; MpBarHeight = d.MpBarHeight;
        ShowShields = d.ShowShields; ShowRoleIcon = d.ShowRoleIcon; ShowMyBuffs = d.ShowMyBuffs; BuffMode = d.BuffMode;
        ShowDebuffs = d.ShowDebuffs; ShowStatusTimers = d.ShowStatusTimers;
        StatusIconSize = d.StatusIconSize; MaxStatusIcons = d.MaxStatusIcons;
        TankBusterWarning = d.TankBusterWarning; BusterMinCastTime = d.BusterMinCastTime; BusterAnyMember = d.BusterAnyMember;
        ShowHotIndicator = d.ShowHotIndicator; HotAnyone = d.HotAnyone;
        ShowAggro = d.ShowAggro; ShowEnmityGauge = d.ShowEnmityGauge; ShowEnemyCount = d.ShowEnemyCount;
        ShowAggroBadge = d.ShowAggroBadge; AggroBarWidth = d.AggroBarWidth; RangeCheck = d.RangeCheck; BlockDoubleRaise = d.BlockDoubleRaise;
        ShowCastBars = d.ShowCastBars; ShowCastName = d.ShowCastName; ShowPartnerMarker = d.ShowPartnerMarker; ShowPetStatus = d.ShowPetStatus;
        RaidwideWarning = d.RaidwideWarning; RaidwideMinCastTime = d.RaidwideMinCastTime;
        ShowMustHeal = d.ShowMustHeal; ShowTankCooldowns = d.ShowTankCooldowns; ShowIncomingHeals = d.ShowIncomingHeals;
    }
}
