using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

namespace Kaifuku.Windows;

/// <summary>
/// A small overlay listing which skill is on which click for the current job.
/// It's its own window, so it is dragged and positioned independently of the frames.
/// </summary>
public sealed class GuideWindow : Window
{
    private readonly Plugin plugin;

    public GuideWindow(Plugin plugin) : base("Kaifuku Guide##guide",
        ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.AlwaysAutoResize |
        ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav)
    {
        this.plugin = plugin;
        IsOpen = true;              // visibility is controlled by the ShowGuide setting
        RespectCloseHotkey = false;
        Position = new Vector2(120, 320);
        PositionCondition = ImGuiCond.FirstUseEver;
    }

    private Configuration Cfg => plugin.Config;

    /// <summary>The plugin-wide scale (General > Plugin scale).</summary>
    private float U => Math.Clamp(Cfg.UiScale, 0.5f, 3f) * ImGuiHelpers.GlobalScale;
    private float FontU => Math.Clamp(Cfg.UiScale, 0.5f, 3f);

    public override bool DrawConditions()
    {
        if (!Cfg.ShowGuide) return false;
        var player = Plugin.ObjectTable.LocalPlayer;
        return player != null && Bindings.JobKey(player.ClassJob.RowId, Cfg) != 0;
    }

    public override void PreDraw()
    {
        if (Cfg.GuideLocked) Flags |= ImGuiWindowFlags.NoMove;
        else Flags &= ~ImGuiWindowFlags.NoMove;
    }

    public override void Draw()
    {
        var player = Plugin.ObjectTable.LocalPlayer;
        if (player == null) return;

        var job = player.ClassJob.RowId;
        var io = ImGui.GetIO();
        var held = Bindings.ModifierFrom(io.KeyShift, io.KeyCtrl, io.KeyAlt);

        var skin = Skins.Get(Cfg.Skin);
        if (skin != null)
        {
            DrawSkinned(job, held, skin);
            return;
        }

        var spec = UiStyles.Get(Cfg.Style);
        var windowPos = ImGui.GetWindowPos();
        StyleDraw.Panel(ImGui.GetWindowDrawList(), windowPos, windowPos + ImGui.GetWindowSize(), spec, Cfg.GuideBackgroundAlpha, Cfg.GuideOpaque);
        StyleDraw.Header(plugin, spec, Bindings.JobName(Bindings.JobKey(job, Cfg)), null, 0);

        ImGui.PushStyleColor(ImGuiCol.Text, spec.GuideText);
        ImGui.PushStyleColor(ImGuiCol.TableBorderStrong, spec.GuideTableBorder);
        ImGui.PushStyleColor(ImGuiCol.TableBorderLight, spec.GuideTableBorder);
        ImGui.PushStyleColor(ImGuiCol.Separator, spec.GuideTableBorder);

        if (Cfg.GuideStyle == GuideLayout.Table) DrawTable(job, held, spec);
        else DrawList(job, held, spec);

        ImGui.PopStyleColor(4);
    }

    private static void IconBorder(StyleSpec spec)
    {
        if (spec.GuideIconBorder.W <= 0) return;
        ImGui.GetWindowDrawList().AddRect(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(),
            ImGui.GetColorU32(spec.GuideIconBorder), 3f, ImDrawFlags.None, 1f);
    }

    /// <summary>The binding guide in a v2 skin: same grid, skin fonts, icon shapes and row highlight.</summary>
    private void DrawSkinned(uint job, Modifier held, SkinSpec s)
    {
        var fonts = plugin.GetSkinFonts(s, FontU);
        var dl = ImGui.GetWindowDrawList();
        var windowPos = ImGui.GetWindowPos();
        SkinDraw.Panel(dl, windowPos, windowPos + ImGui.GetWindowSize(), s, Cfg.GuideBackgroundAlpha, Cfg.GuideOpaque);

        var mods = Enum.GetValues<Modifier>();
        var buttons = Enum.GetValues<MouseButton>();
        if (Cfg.GuideHideEmpty)
        {
            mods = mods.Where(m => buttons.Any(b => Bindings.Resolve(job, m, b, Cfg) != 0)).ToArray();
            buttons = buttons.Where(b => mods.Any(m => Bindings.Resolve(job, m, b, Cfg) != 0)).ToArray();
        }

        var icon = Cfg.GuideIconSize * U;
        // Names share a width budget so 4-5 mouse buttons don't make the guide huge:
        // 104 px each with up to 3 columns, narrower with 4, icons only (names in tooltips) when they'd be too short.
        var nameWidth = 0f;
        if (Cfg.GuideShowNames)
        {
            var budget = 560f * U - 64f * U - buttons.Length * (Cfg.GuideIconSize * U + 14f * U);
            nameWidth = Math.Min(104f * U, budget / Math.Max(1, buttons.Length));
            if (nameWidth < 48f * U) nameWidth = 0f;
        }
        var labelWidth = 64f * U;
        var gap = 8f * U;
        var cellWidth = icon + (nameWidth > 0 ? 6 * U + nameWidth : 0);
        var width = labelWidth + Math.Max(1, buttons.Length) * (cellWidth + gap);
        var origin = ImGui.GetCursorScreenPos();

        // Header: job name, and which modifier is held
        var title = Bindings.JobName(Bindings.JobKey(job, Cfg));
        if (s.UpperHeaders) title = title.ToUpperInvariant();
        var headerHeight = SkinDraw.Height(fonts.Header, 1f);
        SkinDraw.Text(dl, fonts.Header, 1f, origin, SkinDraw.U32(s.PanelAccent.W > 0 ? s.Gold : s.Text), title);
        var subHeight = SkinDraw.Height(fonts.Sub, 1f);
        if (Cfg.GuideHighlightModifier && held != Modifier.None)
        {
            var note = s.UpperHeaders || s.UpperLabels ? $"Holding {held}" : $"holding {held.ToString().ToLowerInvariant()}";
            var nw = SkinDraw.Measure(fonts.Sub, 1f, note).X;
            SkinDraw.Text(dl, fonts.Sub, 1f, new Vector2(origin.X + width - nw, origin.Y + headerHeight - subHeight), SkinDraw.U32(s.Muted), note);
        }
        var y = origin.Y + headerHeight + 6 * U;
        if (s.PanelAccent.W > 0)
        {
            dl.AddLine(new Vector2(origin.X, y - 2 * U), new Vector2(origin.X + width, y - 2 * U), SkinDraw.U32(s.PanelLine), 1f);
            y += 4 * U;
        }

        if (mods.Length == 0 || buttons.Length == 0)
        {
            SkinDraw.Text(dl, fonts.Sub, 1f, new Vector2(origin.X, y), SkinDraw.U32(s.Muted), "No bindings yet: /kaifuku config");
            ImGui.SetCursorScreenPos(new Vector2(origin.X, y + subHeight + 4 * U));
            ImGui.Dummy(new Vector2(width, 1));
            return;
        }

        // Column headers
        for (var b = 0; b < buttons.Length; b++)
        {
            var label = Bindings.Label(buttons[b]);
            if (s.UpperHeaders) label = label.ToUpperInvariant();
            SkinDraw.Text(dl, fonts.Sub, 1f, new Vector2(origin.X + labelWidth + b * (cellWidth + gap), y), SkinDraw.U32(s.Muted), label);
        }
        y += subHeight + 4 * U;

        var rowHeight = icon + 6 * U;
        var labelHeight = SkinDraw.Height(fonts.Chip, 1f);
        var firstRow = true;
        foreach (var mod in mods)
        {
            var lit = Cfg.GuideHighlightModifier && held != Modifier.None && mod == held;
            var rowMin = new Vector2(origin.X - 4 * U, y);
            var rowMax = new Vector2(origin.X + width, y + rowHeight);
            if (lit)
                dl.AddRectFilled(rowMin, rowMax, SkinDraw.U32(s.RowLit), s.ChipRadius > 4 ? rowHeight / 2 : 4f);
            else if (!firstRow && s.PanelLine.W > 0 && s.PanelAccent.W <= 0) // Ledger: hairlines between rows
                dl.AddLine(new Vector2(origin.X, y - 1), new Vector2(origin.X + width, y - 1), SkinDraw.U32(s.PanelLine), 1f);
            firstRow = false;

            var textColor = lit ? s.RowLitText : s.Text;
            var rowLabel = mod == Modifier.None ? "Click" : mod.ToString();
            SkinDraw.Text(dl, fonts.Chip, 1f, new Vector2(origin.X + 4 * U, y + (rowHeight - labelHeight) / 2), SkinDraw.U32(textColor), rowLabel);

            for (var b = 0; b < buttons.Length; b++)
            {
                var id = Bindings.Resolve(job, mod, buttons[b], Cfg);
                if (id == 0) continue;

                var p = new Vector2(origin.X + labelWidth + b * (cellWidth + gap), y + 3 * U);
                var q = p + new Vector2(icon);
                var rounding = s.Badge == RoleBadge.Crest ? icon / 2 : s.ChipRadius <= 2 ? 2f : 4f;
                var iconId = Icons.ActionIcon(id);
                if (iconId != 0)
                    dl.AddImageRounded(Icons.Texture(iconId), p, q, Vector2.Zero, Vector2.One, SkinDraw.U32(Vector4.One), rounding);
                else
                    dl.AddRectFilled(p, q, SkinDraw.U32(s.IconBg), rounding);

                ImGui.SetCursorScreenPos(p);
                ImGui.Dummy(new Vector2(icon));
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(Icons.Tooltip(id));
                Cooldowns.Overlay(id, p, q);

                if (s.Badge == RoleBadge.Crest) dl.AddCircle(p + new Vector2(icon / 2), icon / 2, SkinDraw.U32(s.Gold), 0, 1f);
                else if (s.PanelLine.W > 0) dl.AddRect(p, q, SkinDraw.U32(s.PanelLine), rounding);

                if (nameWidth > 0)
                {
                    var name = MacroRunner.IsMacro(id) ? MacroRunner.ShortName(id) : Icons.Name(id);
                    SkinDraw.Text(dl, fonts.Sub, 1f, new Vector2(q.X + 6 * U, y + (rowHeight - subHeight) / 2), SkinDraw.U32(textColor),
                        SkinDraw.Fit(fonts.Sub, 1f, name, nameWidth));
                }
            }
            y += rowHeight + 2 * U;
        }

        ImGui.SetCursorScreenPos(new Vector2(origin.X, y));
        ImGui.Dummy(new Vector2(width, 1));
    }

    private bool Highlighted(Modifier mod, Modifier held) =>
        Cfg.GuideHighlightModifier && held != Modifier.None && mod == held;

    // Same layout as Settings -> Bindings: rows = modifiers, columns = mouse buttons.
    private void DrawTable(uint job, Modifier held, StyleSpec spec)
    {
        var iconSize = Cfg.GuideIconSize * U;
        var mods = Enum.GetValues<Modifier>();
        var buttons = Enum.GetValues<MouseButton>();

        if (Cfg.GuideHideEmpty)
        {
            mods = mods.Where(m => buttons.Any(b => Bindings.Resolve(job, m, b, Cfg) != 0)).ToArray();
            buttons = buttons.Where(b => mods.Any(m => Bindings.Resolve(job, m, b, Cfg) != 0)).ToArray();
        }

        if (mods.Length == 0 || buttons.Length == 0)
        {
            ImGui.TextDisabled("No bindings yet: /kaifuku config");
            return;
        }

        if (!ImGui.BeginTable("guide", buttons.Length + 1, ImGuiTableFlags.Borders | ImGuiTableFlags.SizingFixedFit))
            return;

        ImGui.TableSetupColumn("##modifier");
        foreach (var b in buttons) ImGui.TableSetupColumn(Bindings.Label(b));

        // Header drawn as plain text (not clickable headers), so the guide can still be dragged from anywhere.
        ImGui.TableNextRow(ImGuiTableRowFlags.Headers);
        ImGui.TableNextColumn();
        foreach (var b in buttons)
        {
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(Bindings.Label(b));
        }

        foreach (var mod in mods)
        {
            ImGui.TableNextRow();
            var lit = Highlighted(mod, held);
            if (lit) ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(spec.GuideRowLit));

            ImGui.TableNextColumn();
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (iconSize - ImGui.GetTextLineHeight()) / 2);
            if (lit) ImGui.TextColored(spec.GuideLitText, Bindings.Label(mod));
            else ImGui.TextUnformatted(Bindings.Label(mod));

            foreach (var b in buttons)
            {
                ImGui.TableNextColumn();
                var actionId = Bindings.Resolve(job, mod, b, Cfg);
                if (actionId == 0)
                {
                    ImGui.Dummy(new Vector2(iconSize));
                    continue;
                }
                Icons.DrawAction(actionId, iconSize);
                Cooldowns.Overlay(actionId, ImGui.GetItemRectMin(), ImGui.GetItemRectMax());
                IconBorder(spec);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(Icons.Tooltip(actionId));
            }
        }

        ImGui.EndTable();
    }

    private void DrawList(uint job, Modifier held, StyleSpec spec)
    {
        var iconSize = Cfg.GuideIconSize * U;
        ImGui.Separator();

        var any = false;
        foreach (var mod in Enum.GetValues<Modifier>())
        {
            var groupStarted = false;
            foreach (var button in Enum.GetValues<MouseButton>())
            {
                var actionId = Bindings.Resolve(job, mod, button, Cfg);
                if (actionId == 0) continue;

                if (any && !groupStarted) ImGui.Spacing(); // small gap between modifier groups
                any = groupStarted = true;

                Icons.DrawAction(actionId, iconSize);
                Cooldowns.Overlay(actionId, ImGui.GetItemRectMin(), ImGui.GetItemRectMax());
                IconBorder(spec);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(Icons.Tooltip(actionId));
                ImGui.SameLine();
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (iconSize - ImGui.GetTextLineHeight()) / 2);

                var label = Cfg.GuideShowNames
                    ? $"{Bindings.ShortLabel(mod, button)}:  {Icons.Name(actionId)}"
                    : Bindings.ShortLabel(mod, button);

                if (Highlighted(mod, held)) ImGui.TextColored(spec.GuideLitText, label);
                else ImGui.TextUnformatted(label);
            }
        }

        if (!any) ImGui.TextDisabled("No bindings yet: /kaifuku config");
    }
}
