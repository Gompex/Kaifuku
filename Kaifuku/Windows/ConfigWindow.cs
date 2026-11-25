using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace Kaifuku.Windows;

public sealed class ConfigWindow : Window
{
    private const float CellSize = 36f;

    private readonly Plugin plugin;
    private string search = string.Empty;

    public ConfigWindow(Plugin plugin) : base("Kaifuku Settings###kaifuku-config")
    {
        this.plugin = plugin;
        Size = new Vector2(560, 540);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    private Configuration Cfg => plugin.Config;

    public override void OnClose() => plugin.SaveConfig();

    public override void Draw()
    {
        if (!ImGui.BeginTabBar("tabs")) return;

        if (ImGui.BeginTabItem("Bindings")) { DrawBindingsTab(); ImGui.EndTabItem(); }
        if (ImGui.BeginTabItem("Look")) { DrawLookTab(); ImGui.EndTabItem(); }
        if (ImGui.BeginTabItem("Layout")) { DrawLayoutTab(); ImGui.EndTabItem(); }
        if (ImGui.BeginTabItem("Display")) { DrawDisplayTab(); ImGui.EndTabItem(); }
        if (ImGui.BeginTabItem("Profiles")) { DrawProfilesTab(); ImGui.EndTabItem(); }
        if (ImGui.BeginTabItem("General")) { DrawGeneralTab(); ImGui.EndTabItem(); }

        ImGui.EndTabBar();
    }

    // ---------- Bindings ----------

    private void DrawBindingsTab()
    {
        Check("Show binding guide on screen", Cfg.ShowGuide, v => Cfg.ShowGuide = v);
        ImGui.Separator();

        var job = Plugin.ObjectTable.LocalPlayer?.ClassJob.RowId ?? 0;
        var key = Bindings.JobKey(job, Cfg);
        if (key == 0)
        {
            ImGui.TextWrapped("Switch to White Mage, Scholar, Astrologian or Sage, " +
                              "or turn on \"Show frames on all jobs\" in the General tab.");
            return;
        }

        ImGui.TextUnformatted($"{Bindings.JobName(key)}: click a square to pick a skill.");
        ImGui.TextDisabled("Rows are modifier keys, columns are mouse buttons.");
        ImGui.Spacing();

        var buttons = Enum.GetValues<MouseButton>();
        if (ImGui.BeginTable("grid", buttons.Length + 1, ImGuiTableFlags.Borders | ImGuiTableFlags.SizingFixedFit))
        {
            ImGui.TableSetupColumn("##modifier");
            foreach (var b in buttons) ImGui.TableSetupColumn(Bindings.Label(b));
            ImGui.TableHeadersRow();

            foreach (var mod in Enum.GetValues<Modifier>())
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(Bindings.Label(mod));
                foreach (var b in buttons)
                {
                    ImGui.TableNextColumn();
                    DrawCell(job, mod, b);
                }
            }

            ImGui.EndTable();
        }

        ImGui.Spacing();
        if (ImGui.Button($"Reset {Bindings.JobName(key)} bindings to defaults"))
        {
            Bindings.ResetJob(job, Cfg);
            plugin.SaveConfig();
        }

        ImGui.TextDisabled("Only skills that can target party members do anything on a frame.");

        ImGui.Separator();
        ImGui.TextUnformatted("Macros");
        ImGui.TextDisabled("A macro runs like pressing it on your hotbar, with the clicked member as <mo>.");
        Check("Also make the clicked member my target (for macros that use <t>)", Cfg.MacroAlsoTarget, v => Cfg.MacroAlsoTarget = v);
    }

    private void DrawCell(uint job, Modifier mod, MouseButton button)
    {
        ImGui.PushID($"{mod}{button}");

        var actionId = Bindings.Resolve(job, mod, button, Cfg);
        bool clicked;
        if (actionId == 0)
        {
            clicked = ImGui.Button("+", new Vector2(CellSize));
        }
        else
        {
            Icons.DrawAction(actionId, CellSize);
            clicked = ImGui.IsItemClicked();
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(actionId == 0 ? "Empty: click to assign a skill" : Icons.Tooltip(actionId));

        if (clicked)
        {
            search = string.Empty;
            ImGui.OpenPopup("picker");
        }

        DrawPicker(job, mod, button);
        ImGui.PopID();
    }

    private void DrawPicker(uint job, Modifier mod, MouseButton button)
    {
        if (!ImGui.BeginPopup("picker")) return;

        ImGui.TextUnformatted($"{Bindings.Label(mod)} + {Bindings.Label(button)}");
        ImGui.Separator();

        var pool = Bindings.Suggested(job, Cfg);
        if (pool.Length > 0)
        {
            ImGui.TextDisabled("Suggested");
            var i = 0;
            foreach (var id in pool)
            {
                if (i++ % 8 != 0) ImGui.SameLine();
                Icons.DrawAction(id, CellSize);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(Icons.Tooltip(id));
                if (ImGui.IsItemClicked())
                {
                    Assign(job, mod, button, id);
                    ImGui.CloseCurrentPopup();
                }
            }
            ImGui.Separator();
        }

        ImGui.TextDisabled("All skills for this job");
        ImGui.SetNextItemWidth(320);
        ImGui.InputTextWithHint("##search", "Search by name...", ref search, 64);

        ImGui.BeginChild("list", new Vector2(320, 220));
        foreach (var entry in ActionCatalog.ForJob(job))
        {
            if (search.Length > 0 && !entry.Name.Contains(search, StringComparison.OrdinalIgnoreCase)) continue;
            Icons.DrawAction(entry.Id, 24);
            ImGui.SameLine();
            if (ImGui.Selectable($"{entry.Name}  (Lv {entry.Level})##{entry.Id}"))
            {
                Assign(job, mod, button, entry.Id);
                ImGui.CloseCurrentPopup();
            }
        }
        ImGui.EndChild();

        ImGui.Separator();
        ImGui.TextDisabled("Your macros (run with the clicked member as <mo>)");
        if (ImGui.BeginTabBar("macroSets"))
        {
            for (uint set = 0; set < 2; set++)
            {
                if (!ImGui.BeginTabItem(set == 0 ? "Individual" : "Shared")) continue;

                ImGui.BeginChild($"macros{set}", new Vector2(320, 160));
                var any = false;
                for (uint index = 0; index < 100; index++)
                {
                    var macroId = MacroRunner.Make(set, index);
                    if (!MacroRunner.Exists(macroId)) continue;
                    any = true;
                    Icons.DrawAction(macroId, 24);
                    ImGui.SameLine();
                    if (ImGui.Selectable($"{index}: {MacroRunner.ShortName(macroId)}##macro{set}_{index}"))
                    {
                        Assign(job, mod, button, macroId);
                        ImGui.CloseCurrentPopup();
                    }
                }
                if (!any) ImGui.TextDisabled("No macros in this set yet.");
                ImGui.EndChild();

                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }

        ImGui.Separator();
        if (ImGui.Selectable("Clear this slot"))
            Assign(job, mod, button, 0);

        ImGui.EndPopup();
    }

    private void Assign(uint job, Modifier mod, MouseButton button, uint actionId)
    {
        Bindings.Set(job, mod, button, actionId, Cfg);
        plugin.SaveConfig();
    }

    // ---------- Look ----------

    private void DrawLookTab()
    {
        var c = Cfg;

        DrawScaleSlider();
        ImGui.Separator();
        DrawSkinPicker();
        if (c.Skin == Skin.Classic)
        {
            DrawStylePicker();
            Check("Match my in-game UI theme", c.FollowGameTheme, v => c.FollowGameTheme = v);
            ImGui.TextDisabled("Follows System Configuration > Theme. Picking a style above turns this off.");
        }
        else
        {
            ImGui.TextDisabled(Skins.Get(c.Skin)?.Description ?? string.Empty);
        }
        Check("Show panel behind the frames", c.ShowPanel, v => c.ShowPanel = v);
        if (c.ShowPanel)
        {
            Check("Show \"Party\" header", c.ShowPanelHeader, v => c.ShowPanelHeader = v);
            Check("Fully opaque panel", c.PanelOpaque, v => c.PanelOpaque = v);
            if (!c.PanelOpaque)
                SliderF("Panel opacity", c.PanelOpacity, 0f, 1f, v => c.PanelOpacity = v, "%.2f");
        }
        ImGui.TextDisabled("Tip: turn on General > Preview to compare styles on a full party.");
        ImGui.Separator();

        DrawThemePicker();
        ImGui.TextDisabled(c.Skin == Skin.Classic
            ? "Optional: swap in another color palette. Your UI style stays as it is."
            : "Optional: a color variant of this skin. Its shapes and fonts stay as they are.");
        if (c.Skin != Skin.Classic)
            EnumCombo("Job badge", c.JobBadge, v => c.JobBadge = v);
        ImGui.Separator();

        SliderF("Frame width", c.FrameWidth, 80, 400, v => c.FrameWidth = v);
        var skinRange = Skins.Get(c.Skin);
        if (skinRange != null)
        {
            SliderF("Frame height", c.FrameHeight, skinRange.MinHeight, skinRange.MaxHeight, v => c.FrameHeight = v);
            SliderF("Text size", c.FontScale, 0.8f, 1.4f, v => c.FontScale = v, "%.2fx");
            ImGui.TextDisabled("Below 36 px the second line under the name is hidden. For everything bigger, use Plugin scale.");
        }
        else
        {
            SliderF("Frame height", c.FrameHeight, 16, 80, v => c.FrameHeight = v);
            SliderF("Text size", c.FontScale, 0.6f, 2f, v => c.FontScale = v, "%.2fx");
        }
        SliderF("Corner rounding", c.Rounding, 0, 12, v => c.Rounding = v);
        Check("Text shadow", c.TextShadow, v => c.TextShadow = v);
        if (c.Skin == Skin.Classic)
            Check("Automatic text contrast (dark text on light bars)", c.AutoTextContrast, v => c.AutoTextContrast = v);
        SliderF("Out-of-range opacity", c.OutOfRangeAlpha, 0.05f, 1f, v => c.OutOfRangeAlpha = v, "%.2f");

        ImGui.Separator();
        EnumCombo("Health bar color", c.ColorMode, v => c.ColorMode = v);
        if (c.ColorMode == HpColorMode.ByHealth)
        {
            ColorEdit("High health", c.HighHpColor, v => c.HighHpColor = v);
            ColorEdit("Medium health", c.MidHpColor, v => c.MidHpColor = v);
            ColorEdit("Low health", c.LowHpColor, v => c.LowHpColor = v);
            SliderF("Medium below", c.MidHpThreshold * 100, 1, 100, v => c.MidHpThreshold = v / 100, "%.0f%%");
            SliderF("Low below", c.LowHpThreshold * 100, 1, 100, v => c.LowHpThreshold = v / 100, "%.0f%%");
        }
        else
        {
            ColorEdit("Tank", c.TankColor, v => c.TankColor = v);
            ColorEdit("Healer", c.HealerColor, v => c.HealerColor = v);
            ColorEdit("DPS", c.DpsColor, v => c.DpsColor = v);
        }

        ImGui.Separator();
        ColorEdit("Dead", c.DeadColor, v => c.DeadColor = v);
        ColorEdit("Disconnected", c.DisconnectedColor, v => c.DisconnectedColor = v);
        ColorEdit("Background", c.BackgroundColor, v => c.BackgroundColor = v);
        ColorEdit("Text", c.TextColor, v => c.TextColor = v);
        ColorEdit("Shield", c.ShieldColor, v => c.ShieldColor = v);
        ColorEdit("MP bar", c.MpColor, v => c.MpColor = v);
        ColorEdit("Removable debuff", c.DispelColor, v => c.DispelColor = v);
        EnumCombo("Removable debuff shows as", c.DispelHighlight, v => c.DispelHighlight = v);
        ColorEdit("Hover border", c.HoverColor, v => c.HoverColor = v);

        ImGui.Spacing();
        if (ImGui.Button("Reset look to defaults"))
        {
            if (c.Skin == Skin.Classic) c.ResetLook();
            else Skins.Apply(c, c.Skin, true); // back to the skin's own colors and layout
            plugin.SaveConfig();
        }
    }

    private Skin? pendingSkin;
    private bool openSkinPrompt;

    private static string SkinLabel(Skin skin) => skin == Skin.Classic ? "Classic (v1)" : Skins.Get(skin)?.Name ?? skin.ToString();

    private void DrawSkinPicker()
    {
        if (ImGui.BeginCombo("Skin", SkinLabel(Cfg.Skin)))
        {
            foreach (var skin in Enum.GetValues<Skin>())
            {
                if (ImGui.Selectable(SkinLabel(skin), skin == Cfg.Skin))
                {
                    pendingSkin = skin;
                    openSkinPrompt = true; // asked after the combo closes
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(skin == Skin.Classic
                        ? "The original look, with its Dark / Light / Classic / Clear Blue styles."
                        : Skins.Get(skin)?.Description ?? string.Empty);
            }
            ImGui.EndCombo();
        }

        if (openSkinPrompt)
        {
            ImGui.OpenPopup("Apply skin layout?");
            openSkinPrompt = false;
        }

        if (!ImGui.BeginPopup("Apply skin layout?")) return;
        if (pendingSkin is { } chosen)
        {
            ImGui.TextUnformatted($"Switch to {SkinLabel(chosen)}.");
            ImGui.TextDisabled("Also use its frame size, spacing and alliance layout?");
            if (ImGui.Button("Colors and layout"))
            {
                Skins.Apply(Cfg, chosen, true);
                plugin.SaveConfig();
                pendingSkin = null;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Colors only, keep my layout"))
            {
                Skins.Apply(Cfg, chosen, false);
                plugin.SaveConfig();
                pendingSkin = null;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel"))
            {
                pendingSkin = null;
                ImGui.CloseCurrentPopup();
            }
        }
        ImGui.EndPopup();
    }

    private void DrawStylePicker()
    {
        var current = UiStyles.Get(Cfg.Style);
        if (!ImGui.BeginCombo("UI style", current.Name)) return;

        foreach (var style in UiStyles.All)
        {
            if (ImGui.Selectable(style.Name, style.Id == Cfg.Style))
            {
                UiStyles.Apply(Cfg, style.Id);
                Cfg.FollowGameTheme = false; // a style picked by hand wins over the game's theme
                plugin.SaveConfig();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(style.Description);
        }

        ImGui.EndCombo();
    }

    private void DrawThemePicker()
    {
        var skinSpec = Skins.Get(Cfg.Skin);
        if (skinSpec != null)
        {
            if (!ImGui.BeginCombo("Color preset", Cfg.ThemeName)) return;
            foreach (var variant in Skins.Variants)
            {
                if (ImGui.Selectable(variant.Name, variant.Name == Cfg.ThemeName))
                {
                    variant.Apply(Cfg, skinSpec);
                    Cfg.ThemeName = variant.Name;
                    plugin.SaveConfig();
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(variant.Description);
            }
            ImGui.EndCombo();
            return;
        }

        if (!ImGui.BeginCombo("Color preset", Cfg.ThemeName)) return;

        foreach (var theme in Themes.All)
        {
            if (ImGui.Selectable(theme.Name, theme.Name == Cfg.ThemeName))
            {
                theme.Apply(Cfg);
                Cfg.ThemeName = theme.Name;
                plugin.SaveConfig();
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(theme.Description);

            // Color swatches on the right of each row
            var max = ImGui.GetItemRectMax();
            var min = ImGui.GetItemRectMin();
            var size = (max.Y - min.Y) * 0.7f;
            var x = max.X - (size + 3) * theme.Swatch.Length - 2;
            var y = min.Y + (max.Y - min.Y - size) / 2;
            var dl = ImGui.GetWindowDrawList();
            foreach (var color in theme.Swatch)
            {
                dl.AddRectFilled(new Vector2(x, y), new Vector2(x + size, y + size),
                    ImGui.GetColorU32(color with { W = 1f }), 2f);
                x += size + 3;
            }
        }

        ImGui.EndCombo();
    }

    // ---------- Layout ----------

    private void DrawLayoutTab()
    {
        var c = Cfg;

        SliderI("Frames per row", c.Columns, 1, 8, v => c.Columns = v);
        SliderF("Spacing", c.Spacing, 0, 20, v => c.Spacing = v);
        EnumCombo("Sort by", c.Sort, v => c.Sort = v);
        if (c.Sort == SortMode.PartyOrder) ImGui.TextDisabled("Party order = the same order as the game's party list (<1> to <8>).");
        Check("Always put me first", c.SelfFirst, v => c.SelfFirst = v);
        Check("Group by role (tanks, healers, DPS)", c.GroupByRole, v => c.GroupByRole = v);
        if (c.GroupByRole) SliderF("Gap between groups", c.GroupGap, 0, 40, v => c.GroupGap = v);

        ImGui.Spacing();
        ImGui.TextDisabled("There's no sort by HP on purpose: frames would move under your cursor and cause misclicks.");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextUnformatted("Alliance raids");
        Check("Show the other two alliance parties", c.ShowAlliance, v => c.ShowAlliance = v);
        if (c.ShowAlliance)
        {
            SliderF("Alliance frame width", c.AllianceFrameWidth, 60, 200, v => c.AllianceFrameWidth = v);
            SliderF("Alliance frame height", c.AllianceFrameHeight, 14, 40, v => c.AllianceFrameHeight = v);
            SliderI("Alliance frames per row", c.AllianceColumns, 1, 8, v => c.AllianceColumns = v);
            ImGui.TextDisabled("Compact frames under your party. Same bindings: click to heal, raise or Esuna across alliances.");
        }

        ImGui.Spacing();
        if (ImGui.Button("Reset layout to defaults")) { c.ResetLayout(); plugin.SaveConfig(); }
    }

    // ---------- Display ----------

    private void DrawDisplayTab()
    {
        var c = Cfg;

        EnumCombo("Name", c.Names, v => c.Names = v);
        EnumCombo("Health text", c.HpText, v => c.HpText = v);
        Check("Show MP bar", c.ShowMpBar, v => c.ShowMpBar = v);
        if (c.ShowMpBar) SliderF("MP bar height", c.MpBarHeight, 2, 12, v => c.MpBarHeight = v);
        Check("Show shields", c.ShowShields, v => c.ShowShields = v);
        Check("Show job icon", c.ShowRoleIcon, v => c.ShowRoleIcon = v);

        ImGui.Separator();
        Check("Show buffs", c.ShowMyBuffs, v => c.ShowMyBuffs = v);
        if (c.ShowMyBuffs)
        {
            EnumCombo("Which buffs", c.BuffMode, v => c.BuffMode = v);
            ImGui.TextDisabled(c.BuffMode switch
            {
                BuffFilter.MineOnly => "Only buffs you cast (your HoTs and shields, incl. your fairy's or pet's).",
                BuffFilter.MineAndMyList => "Buffs you cast, plus the ones in your list below, whoever cast them.",
                _ => "Every buff on the player (can get crowded; use the hidden list to trim it).",
            });
        }
        Check("Show debuffs", c.ShowDebuffs, v => c.ShowDebuffs = v);
        Check("Show status timers", c.ShowStatusTimers, v => c.ShowStatusTimers = v);
        SliderF("Status icon size", c.StatusIconSize, 10, 40, v => c.StatusIconSize = v);
        SliderI("Max status icons", c.MaxStatusIcons, 0, 8, v => c.MaxStatusIcons = v);

        ImGui.Separator();
        ImGui.TextUnformatted("Aggro");
        Check("Show aggro", c.ShowAggro, v => c.ShowAggro = v);
        if (c.ShowAggro)
        {
            ColorEdit("Aggro color", c.AggroColor, v => c.AggroColor = v);
            Check("Aggro bar left of each frame", c.ShowEnmityGauge, v => c.ShowEnmityGauge = v);
            if (c.ShowEnmityGauge) SliderF("Aggro bar width", c.AggroBarWidth, 3, 14, v => c.AggroBarWidth = v);
            ImGui.TextDisabled("Fills with enmity on your current target. Glowing red = has aggro, orange = 80%+ (close to pulling).");
            if (c.Skin == Skin.Classic)
                Check("\"A\" badge on whoever has aggro", c.ShowAggroBadge, v => c.ShowAggroBadge = v);
            Check("Number of enemies attacking each member", c.ShowEnemyCount, v => c.ShowEnemyCount = v);
            ImGui.TextDisabled("Aggro follows your target if it's an enemy, else your focus target, else the biggest enemy in the fight.");
        }

        ImGui.Separator();
        ImGui.TextUnformatted("Range");
        EnumCombo("Fade frames that are", c.RangeCheck, v => c.RangeCheck = v);
        ImGui.TextDisabled(c.RangeCheck == RangeMode.BoundSkill
            ? "Bound Skill: out of range or line of sight for your Left-click skill."
            : "Distance 30: more than 30 yalms away (ignores line of sight).");

        ImGui.Separator();
        ImGui.TextUnformatted("Raises");
        ColorEdit("Raise coming color", c.RaiseColor, v => c.RaiseColor = v);
        Check("Block my raise when one is already coming", c.BlockDoubleRaise, v => c.BlockDoubleRaise = v);
        ImGui.TextDisabled("A dead member shows RAISING when someone is casting a raise on them or a raise is waiting.");

        ImGui.Separator();
        ImGui.TextUnformatted("Cast bars");
        Check("Show party members' cast bars", c.ShowCastBars, v => c.ShowCastBars = v);
        if (c.ShowCastBars)
        {
            ColorEdit("Cast bar color", c.CastColor, v => c.CastColor = v);
            Check("Show the spell name", c.ShowCastName, v => c.ShowCastName = v);
        }

        ImGui.Separator();
        Check("Mark my Kardia / Dance Partner", c.ShowPartnerMarker, v => c.ShowPartnerMarker = v);
        Check("Pet status for Scholar and Summoner (fairy / carbuncle out or missing)", c.ShowPetStatus, v => c.ShowPetStatus = v);

        ImGui.Separator();
        ImGui.TextUnformatted("Raidwide warning");
        Check("Show a warning strip above the frames when a raidwide is coming", c.RaidwideWarning, v => c.RaidwideWarning = v);
        if (c.RaidwideWarning)
        {
            ColorEdit("Raidwide color", c.RaidwideColor, v => c.RaidwideColor = v);
            SliderF("Ignore enemy casts shorter than", c.RaidwideMinCastTime, 0.5f, 8f, v => c.RaidwideMinCastTime = v, "%.1f s");
            ImGui.TextDisabled("An enemy cast that covers the whole arena: your cue to shield or mitigate.");
        }

        ImGui.Separator();
        ImGui.TextUnformatted("Heal to full");
        Check("Highlight members who must be healed to full (Doom, Walking Dead)", c.ShowMustHeal, v => c.ShowMustHeal = v);
        if (c.ShowMustHeal)
        {
            ColorEdit("Heal-to-full color", c.MustHealColor, v => c.MustHealColor = v);
            if (ImGui.Button("Add defaults##musthealdefaults"))
            {
                c.MustHealStatuses.UnionWith(StatusCatalog.IdsByEnglishName(StatusCatalog.MustHealDefaults));
                plugin.SaveConfig();
            }
            StatusListEditor("musthealList", c.MustHealStatuses, buffsOnly: false, ref mustHealSearch);
        }

        ImGui.Separator();
        ImGui.TextUnformatted("Tank cooldowns");
        Check("Always show tank mitigation and invulnerabilities", c.ShowTankCooldowns, v => c.ShowTankCooldowns = v);
        if (c.ShowTankCooldowns)
        {
            if (ImGui.Button("Add defaults##tankdefaults"))
            {
                c.TankCooldownStatuses.UnionWith(StatusCatalog.IdsByEnglishName(StatusCatalog.TankCooldownDefaults));
                plugin.SaveConfig();
            }
            StatusListEditor("tankList", c.TankCooldownStatuses, buffsOnly: false, ref tankSearch);
        }

        ImGui.Separator();
        ImGui.TextUnformatted("Incoming heals");
        Check("Glow over the missing health when someone else is casting on them", c.ShowIncomingHeals, v => c.ShowIncomingHeals = v);
        if (c.ShowIncomingHeals)
        {
            ColorEdit("Incoming heal color", c.IncomingHealColor, v => c.IncomingHealColor = v);
            ImGui.TextDisabled("The game doesn't reveal how much a heal will do until it lands, so this shows THAT one is coming.");
        }

        ImGui.Separator();
        ImGui.TextUnformatted("Tank buster warning");
        Check("Flash the tank's frame when an enemy casts at them", c.TankBusterWarning, v => c.TankBusterWarning = v);
        if (c.TankBusterWarning)
        {
            ColorEdit("Warning color", c.BusterColor, v => c.BusterColor = v);
            SliderF("Ignore casts shorter than", c.BusterMinCastTime, 0.5f, 8f, v => c.BusterMinCastTime = v, "%.1f s");
            Check("Also warn for non-tanks (shared or targeted busters)", c.BusterAnyMember, v => c.BusterAnyMember = v);
            ImGui.TextDisabled("Detects enemy casts aimed at one player. Busters with only a head marker and no cast bar can't be seen.");
        }

        if (c.ShowMyBuffs && c.BuffMode == BuffFilter.MineAndMyList)
        {
            ImGui.Separator();
            ImGui.TextUnformatted("Buffs to always show (from anyone)");
            if (ImGui.Button("Add common shields"))
            {
                foreach (var id in StatusCatalog.BuffIdsByEnglishName(StatusCatalog.CommonShields)) c.AlwaysShowStatuses.Add(id);
                plugin.SaveConfig();
            }
            StatusListEditor("always", c.AlwaysShowStatuses, buffsOnly: true, ref alwaysSearch);
        }

        ImGui.Separator();
        ImGui.TextUnformatted("Statuses to never show (buffs or debuffs)");
        StatusListEditor("hidden", c.HiddenStatuses, buffsOnly: false, ref hiddenSearch);

        ImGui.Spacing();
        if (ImGui.Button("Reset display to defaults")) { c.ResetDisplay(); plugin.SaveConfig(); }
    }

    private string alwaysSearch = string.Empty;
    private string hiddenSearch = string.Empty;
    private string hotSearch = string.Empty;
    private string mustHealSearch = string.Empty;
    private string tankSearch = string.Empty;
    private string transferMessage = string.Empty;

    /// <summary>The current list (icon, name, remove) plus a search box to add more.</summary>
    private void StatusListEditor(string id, HashSet<uint> list, bool buffsOnly, ref string search)
    {
        ImGui.PushID(id);

        if (list.Count == 0) ImGui.TextDisabled("(empty)");
        uint? remove = null;
        foreach (var statusId in list.OrderBy(x => StatusCatalog.Get(x)?.Name ?? string.Empty))
        {
            var entry = StatusCatalog.Get(statusId);
            if (entry is { } e)
            {
                ImGui.Image(Icons.Texture(e.Icon), new Vector2(15, 20));
                ImGui.SameLine();
            }
            ImGui.TextUnformatted(entry?.Name ?? $"Status #{statusId}");
            ImGui.SameLine();
            if (ImGui.SmallButton($"Remove##{statusId}")) remove = statusId;
        }
        if (remove is { } r)
        {
            list.Remove(r);
            plugin.SaveConfig();
        }

        ImGui.SetNextItemWidth(260);
        ImGui.InputTextWithHint("##search", "Search to add...", ref search, 64);
        if (search.Length >= 2)
        {
            ImGui.BeginChild("results", new Vector2(320, 140));
            var shown = 0;
            var seen = new HashSet<string>();
            foreach (var e in StatusCatalog.All)
            {
                if (buffsOnly && e.Debuff) continue;
                if (!e.Name.Contains(search, StringComparison.OrdinalIgnoreCase)) continue;
                if (list.Contains(e.Id) || !seen.Add(e.Name + e.Icon)) continue;

                ImGui.Image(Icons.Texture(e.Icon), new Vector2(15, 20));
                ImGui.SameLine();
                if (ImGui.Selectable($"{e.Name}##{e.Id}"))
                {
                    list.Add(e.Id);
                    plugin.SaveConfig();
                }
                if (++shown >= 40) break;
            }
            if (shown == 0) ImGui.TextDisabled("No matches.");
            ImGui.EndChild();
        }

        ImGui.PopID();
    }

    // ---------- General ----------

    private string newProfileName = string.Empty;

    private void DrawProfilesTab()
    {
        var c = Cfg;
        ImGui.TextWrapped("A profile saves the Look, Layout and Display settings (not bindings). " +
                          "Switch by hand, or let Kaifuku switch per job or per type of content.");
        ImGui.TextUnformatted($"Active profile: {(c.ActiveProfile.Length > 0 ? c.ActiveProfile : "(none)")}");
        ImGui.Separator();

        ImGui.SetNextItemWidth(200);
        ImGui.InputTextWithHint("##newprofile", "Profile name", ref newProfileName, 40);
        ImGui.SameLine();
        if (ImGui.Button("Save current settings") && newProfileName.Trim().Length > 0)
        {
            var name = newProfileName.Trim();
            c.Profiles[name] = Kaifuku.Profiles.Capture(c);
            c.ActiveProfile = name;
            newProfileName = string.Empty;
            plugin.SaveConfig();
        }

        string? load = null, delete = null, update = null;
        foreach (var name in c.Profiles.Keys.OrderBy(n => n))
        {
            ImGui.PushID(name);
            ImGui.TextUnformatted(name == c.ActiveProfile ? $"> {name}" : $"  {name}");
            ImGui.SameLine(220);
            if (ImGui.SmallButton("Load")) load = name;
            ImGui.SameLine();
            if (ImGui.SmallButton("Update")) update = name;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Overwrite this profile with the current settings");
            ImGui.SameLine();
            if (ImGui.SmallButton("Delete")) delete = name;
            ImGui.PopID();
        }
        if (c.Profiles.Count == 0) ImGui.TextDisabled("No profiles yet.");

        if (load != null) Kaifuku.Profiles.Switch(plugin, load);
        if (update != null) { c.Profiles[update] = Kaifuku.Profiles.Capture(c); plugin.SaveConfig(); }
        if (delete != null)
        {
            c.Profiles.Remove(delete);
            if (c.ActiveProfile == delete) c.ActiveProfile = string.Empty;
            foreach (var k in c.JobProfiles.Where(kv => kv.Value == delete).Select(kv => kv.Key).ToList()) c.JobProfiles.Remove(k);
            foreach (var k in c.ContentProfiles.Where(kv => kv.Value == delete).Select(kv => kv.Key).ToList()) c.ContentProfiles.Remove(k);
            plugin.SaveConfig();
        }

        if (c.Profiles.Count == 0) return;

        ImGui.Separator();
        ImGui.TextUnformatted("Switch automatically");
        var job = Plugin.ObjectTable.LocalPlayer?.ClassJob.RowId ?? 0;
        if (job != 0)
        {
            c.JobProfiles.TryGetValue(job, out var current);
            ProfileCombo($"When I play {Bindings.JobName(job)}", current, v =>
            {
                if (v == null) c.JobProfiles.Remove(job); else c.JobProfiles[job] = v;
            });
        }
        foreach (var kind in Enum.GetValues<ContentKind>())
        {
            c.ContentProfiles.TryGetValue(kind, out var current);
            ProfileCombo($"In {Kaifuku.Profiles.Label(kind)}", current, v =>
            {
                if (v == null) c.ContentProfiles.Remove(kind); else c.ContentProfiles[kind] = v;
            });
        }
        ImGui.TextDisabled("Content rules win over job rules. Changes you make are kept in the active profile when it switches.");
    }

    private void ProfileCombo(string label, string? current, Action<string?> set)
    {
        if (!ImGui.BeginCombo(label, current ?? "(no change)")) return;
        if (ImGui.Selectable("(no change)", current == null)) { set(null); plugin.SaveConfig(); }
        foreach (var name in Cfg.Profiles.Keys.OrderBy(n => n))
        {
            if (ImGui.Selectable(name, name == current)) { set(name); plugin.SaveConfig(); }
        }
        ImGui.EndCombo();
    }

    /// <summary>One slider that scales everything Kaifuku draws: frames, text, icons, guide, alliance.</summary>
    private void DrawScaleSlider()
    {
        var c = Cfg;
        var percent = c.UiScale * 100f;
        ImGui.SetNextItemWidth(220);
        if (ImGui.SliderFloat("Plugin scale", ref percent, 75f, 250f, "%.0f%%")) c.UiScale = MathF.Round(percent / 5f) * 5f / 100f;
        if (ImGui.IsItemDeactivatedAfterEdit()) plugin.SaveConfig();
        foreach (var preset in new[] { 100, 125, 150, 200 })
        {
            ImGui.SameLine();
            if (ImGui.SmallButton($"{preset}%"))
            {
                c.UiScale = preset / 100f;
                plugin.SaveConfig();
            }
        }
        ImGui.TextDisabled("Scales the frames, text, icons, binding guide and alliance frames together.");
    }

    private void DrawGeneralTab()
    {
        var c = Cfg;

        DrawScaleSlider();
        ImGui.Separator();

        Check("Show frames", c.ShowFrames, v =>
        {
            c.ShowFrames = v;
            plugin.SetFramesVisible(v);
        });
        Check("Lock frames in place", c.Locked, v => c.Locked = v);
        ImGui.TextDisabled("Unlocked: drag the frames from anywhere (clicks don't cast). Lock again to heal.");

        if (ImGui.Button("Snap next to party list"))
        {
            c.AutoPositioned = false;
            plugin.SaveConfig();
        }

        ImGui.Separator();
        Check("Preview with a fake 8-person party", c.Preview, v => c.Preview = v);
        ImGui.TextDisabled("Handy for tuning the look and layout while solo. Clicks do nothing in preview.");
        ImGui.TextDisabled("Healer Delta and Melee Echo have a removable debuff; Tank Bravo has one Esuna can't remove.");
        ImGui.TextDisabled("Melee Foxtrot is dead, Caster Hotel is disconnected, Ranged Golf is out of range.");
        ImGui.TextDisabled("Tank Alpha shows the tank buster warning and holds aggro (3 enemies).");
        ImGui.TextDisabled("Tank Bravo must be healed to full, Ranged Golf has a heal coming, and a fake raidwide runs above.");

        ImGui.Separator();
        ImGui.Separator();
        ImGui.TextUnformatted("Share settings");
        ImGui.TextDisabled("Everything, bindings and profiles included, as one text you can paste anywhere.");
        if (ImGui.Button("Copy all settings to clipboard"))
        {
            ImGui.SetClipboardText(Kaifuku.Profiles.Export(c));
            transferMessage = "Copied. Paste it to a friend, or keep it as a backup.";
        }
        ImGui.SameLine();
        if (ImGui.Button("Import from clipboard"))
        {
            transferMessage = Kaifuku.Profiles.Import(plugin, ImGui.GetClipboardText())
                ? "Imported."
                : "The clipboard doesn't contain Kaifuku settings.";
        }
        if (transferMessage.Length > 0) ImGui.TextDisabled(transferMessage);

        ImGui.Separator();
        Check("Show frames on non-healer jobs", c.ShowOnAllJobs, v => c.ShowOnAllJobs = v);
        ImGui.TextDisabled("Tanks, BRD, DNC, MNK, BLM, SMN and RDM come with defaults for their ally skills.");

        DrawGuideSection();
    }

    private void DrawGuideSection()
    {
        var c = Cfg;
        ImGui.Separator();
        ImGui.TextUnformatted("Binding guide");
        Check("Show binding guide", c.ShowGuide, v => c.ShowGuide = v);
        ImGui.TextDisabled("Also: /kaifuku guide. Drag it anywhere; it moves separately from the frames.");
        if (!c.ShowGuide) return;
        Check("Only show in combat (always shown while these settings are open)", c.GuideOnlyInCombat, v => c.GuideOnlyInCombat = v);
        Check("Lock guide in place", c.GuideLocked, v => c.GuideLocked = v);
        EnumCombo("Guide layout", c.GuideStyle, v => c.GuideStyle = v);
        if (c.GuideStyle == GuideLayout.Table)
            Check("Hide empty rows and columns", c.GuideHideEmpty, v => c.GuideHideEmpty = v);
        else
            Check("Show skill names", c.GuideShowNames, v => c.GuideShowNames = v);
        if (c.GuideStyle == GuideLayout.Table && c.Skin != Skin.Classic)
            Check("Show skill names next to icons", c.GuideShowNames, v => c.GuideShowNames = v);
        Check("Highlight the modifier I'm holding", c.GuideHighlightModifier, v => c.GuideHighlightModifier = v);
        SliderF("Guide icon size", c.GuideIconSize, 12, 48, v => c.GuideIconSize = v);
        Check("Fully opaque guide panel", c.GuideOpaque, v => c.GuideOpaque = v);
        if (!c.GuideOpaque)
            SliderF("Guide panel opacity", c.GuideBackgroundAlpha, 0f, 1f, v => c.GuideBackgroundAlpha = v, "%.2f");
    }

    // ---------- widget helpers ----------

    private void SliderF(string label, float value, float min, float max, Action<float> set, string format = "%.0f")
    {
        var v = value;
        if (ImGui.SliderFloat(label, ref v, min, max, format)) set(v);
        if (ImGui.IsItemDeactivatedAfterEdit()) plugin.SaveConfig();
    }

    private void SliderI(string label, int value, int min, int max, Action<int> set)
    {
        var v = value;
        if (ImGui.SliderInt(label, ref v, min, max)) set(v);
        if (ImGui.IsItemDeactivatedAfterEdit()) plugin.SaveConfig();
    }

    private void Check(string label, bool value, Action<bool> set)
    {
        var v = value;
        if (ImGui.Checkbox(label, ref v))
        {
            set(v);
            plugin.SaveConfig();
        }
    }

    private void ColorEdit(string label, Vector4 value, Action<Vector4> set)
    {
        var v = value;
        if (ImGui.ColorEdit4(label, ref v, ImGuiColorEditFlags.AlphaBar)) set(v);
        if (ImGui.IsItemDeactivatedAfterEdit()) plugin.SaveConfig();
    }

    private void EnumCombo<T>(string label, T value, Action<T> set) where T : struct, Enum
    {
        if (!ImGui.BeginCombo(label, Pretty(value.ToString()))) return;
        foreach (var option in Enum.GetValues<T>())
        {
            if (ImGui.Selectable(Pretty(option.ToString()), EqualityComparer<T>.Default.Equals(option, value)))
            {
                set(option);
                plugin.SaveConfig();
            }
        }
        ImGui.EndCombo();
    }

    // "PartyOrder" -> "Party Order"
    private static string Pretty(string name) => Regex.Replace(name, "(?<=[a-z])(?=[A-Z])", " ");
}
