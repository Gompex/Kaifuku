using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using LuminaClassJob = Lumina.Excel.Sheets.ClassJob;

namespace Kaifuku.Windows;

/// <summary>The v2 skins (Ledger, Heraldry, Signal): same data, new visual language.</summary>
public sealed partial class FramesWindow
{
    private static readonly Dictionary<uint, string> jobAbbr = new();

    private static string JobAbbr(uint jobId)
    {
        if (jobAbbr.TryGetValue(jobId, out var a)) return a;
        a = Plugin.DataManager.GetExcelSheet<LuminaClassJob>().GetRowOrDefault(jobId)?.Abbreviation.ExtractText() ?? "?";
        jobAbbr[jobId] = a;
        return a;
    }

    /// <summary>Size scale: the plugin scale (General > Plugin scale) times Dalamud's own UI scale.</summary>
    private float U => Math.Clamp(Cfg.UiScale, 0.5f, 3f) * ImGuiHelpers.GlobalScale;

    /// <summary>Font scale: only the plugin scale (Dalamud already applies its own scale to fonts).</summary>
    private float FontU => Math.Clamp(Cfg.UiScale, 0.5f, 3f);

    private static float Pulse(float hz) => 0.5f + 0.5f * MathF.Sin(Environment.TickCount64 / 1000f * MathF.PI * 2f * hz);

    private string NameFor(FrameData f) => Cfg.Names switch
    {
        NameMode.FirstName => f.Name.Split(' ')[0],
        NameMode.Initials => string.Concat(f.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(p => $"{p[0]}.")),
        NameMode.None => string.Empty,
        _ => f.Name,
    };

    private void DrawSkinHeader(SkinSpec s, string title, string? note, float width)
    {
        var fonts = plugin.GetSkinFonts(s, FontU);
        var dl = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var text = s.UpperHeaders ? title.ToUpperInvariant() : title;
        var h = SkinDraw.Height(fonts.Header, 1f);

        SkinDraw.Text(dl, fonts.Header, 1f, start, SkinDraw.U32(s.PanelAccent.W > 0 ? s.Gold : s.Text), text);
        if (note != null)
        {
            var nh = SkinDraw.Height(fonts.Sub, 1f);
            var nw = SkinDraw.Measure(fonts.Sub, 1f, note).X;
            SkinDraw.Text(dl, fonts.Sub, 1f, new Vector2(start.X + width - nw, start.Y + h - nh), SkinDraw.U32(s.Muted), note);
        }
        if (s.PanelAccent.W > 0) // Heraldry: a rule under the header
            dl.AddLine(new Vector2(start.X, start.Y + h + 4 * U), new Vector2(start.X + width, start.Y + h + 4 * U), SkinDraw.U32(s.PanelLine), 1f);
        ImGui.Dummy(new Vector2(width, h + 8 * U));
    }

    private void DrawSkinRaidwide(SkinSpec s, string name, float left, float progress, float width)
    {
        var fonts = plugin.GetSkinFonts(s, FontU);
        var dl = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var h = 28f * U;
        ImGui.Dummy(new Vector2(width, h));
        var end = start + new Vector2(width, h);
        var radius = s.ChipRadius > 4 ? h / 2 : (s.TextAboveBar ? 6f : 3f) * U;
        var warn = Cfg.RaidwideColor;
        var label = s.UpperHeaders ? $"RAIDWIDE \u00B7 {name.ToUpperInvariant()}" : $"Raidwide \u00B7 {name}";
        var seconds = $"{left:0.0}s";

        Vector4 fg;
        float textX;
        if (s.TextAboveBar)
        {
            // Signal: a solid warning bar with dark text, and its progress as a line along the bottom.
            dl.AddRectFilled(start, end, SkinDraw.U32(warn), radius);
            dl.AddRectFilled(new Vector2(start.X, end.Y - 3 * U), new Vector2(start.X + width * Math.Clamp(progress, 0f, 1f), end.Y),
                SkinDraw.U32(s.OnWarn, 0.35f));
            fg = s.OnWarn;
            SkinDraw.Triangle(dl, new Vector2(start.X + 14 * U, start.Y + h / 2), 12f * U, SkinDraw.U32(fg));
            textX = start.X + 28 * U;
        }
        else
        {
            dl.AddRectFilled(start, end, SkinDraw.U32(s.Track), radius);
            dl.AddRectFilled(start, new Vector2(start.X + width * Math.Clamp(progress, 0f, 1f), end.Y), SkinDraw.U32(warn, 0.3f), radius);
            dl.AddRect(start, end, SkinDraw.U32(warn, 0.7f + 0.3f * Pulse(2f)), radius, ImDrawFlags.None, 1f);
            fg = s.Text;
            textX = start.X + 10 * U;
        }

        var lh = SkinDraw.Height(fonts.Chip, 1f);
        SkinDraw.Text(dl, fonts.Chip, 1f, new Vector2(textX, start.Y + (h - lh) / 2), SkinDraw.U32(fg), label);
        var sw = SkinDraw.Measure(fonts.Number, 1f, seconds).X;
        var sh = SkinDraw.Height(fonts.Number, 1f);
        SkinDraw.Text(dl, fonts.Number, 1f, new Vector2(end.X - sw - 10 * U, start.Y + (h - sh) / 2), SkinDraw.U32(fg), seconds);
    }

    private void DrawSkinRail(ImDrawListPtr dl, FrameData f, SkinSpec s, Plugin.SkinFontSet fonts, Vector2 start, Vector2 end, float alpha, bool unavailable)
    {
        var cfg = Cfg;
        if (!cfg.ShowAggro || !cfg.ShowEnmityGauge) return;

        var w = cfg.AggroBarWidth * U;
        var min = new Vector2(start.X - w - 2 * U, start.Y);
        var max = new Vector2(start.X - 2 * U, end.Y);
        var round = MathF.Min(s.FrameRadius + 1, cfg.AggroBarWidth / 2) * U;
        var level = f.Enmity >= 0 ? Math.Clamp(f.Enmity / 100f, 0f, 1f) : f.HasAggro ? 1f : 0f;
        var maxed = !unavailable && (f.HasAggro || f.Enmity >= 100);

        if (maxed)
        {
            var glow = 0.75f + 0.25f * Pulse(1f);
            for (var i = 3; i >= 1; i--)
            {
                var grow = new Vector2(i * 1.5f * U);
                dl.AddRectFilled(min - grow, max + grow, SkinDraw.U32(cfg.AggroColor with { W = 0.32f * glow / i }, alpha), round + i * 1.5f * U);
            }
        }

        dl.AddRectFilled(min, max, SkinDraw.U32(s.Track, alpha), round);
        if (s.PanelAccent.W > 0) dl.AddRect(min, max, SkinDraw.U32(s.PanelLine, alpha), round); // Heraldry: framed rail

        if (level > 0 && !unavailable)
        {
            var color = maxed ? cfg.AggroColor : f.Enmity >= 80 ? s.Warn : s.RailFill;
            dl.AddRectFilled(new Vector2(min.X, max.Y - (max.Y - min.Y) * level), max, SkinDraw.U32(color, alpha), round);
        }

        // How many enemies are on this member: a small count at the top of the rail.
        // Orange on a non-tank (something got loose), muted on a tank.
        if (cfg.ShowEnemyCount && !unavailable && f.EnemiesOnMe > 0 && !(f.HasAggro && f.EnemiesOnMe == 1))
        {
            var text = f.EnemiesOnMe.ToString();
            var size = SkinDraw.Measure(fonts.Tag, 1f, text);
            var cx = (min.X + max.X) / 2;
            var pMin = new Vector2(cx - size.X / 2 - 3 * U, min.Y - 1 * U);
            var pMax = new Vector2(cx + size.X / 2 + 3 * U, min.Y + size.Y + 1 * U);
            var ring = f.Role == 1 ? s.Muted : s.Warn;
            dl.AddRectFilled(pMin, pMax, SkinDraw.U32(s.Panel), 3f * U);
            dl.AddRect(pMin, pMax, SkinDraw.U32(ring), 3f * U);
            SkinDraw.Text(dl, fonts.Tag, 1f, new Vector2(cx - size.X / 2, min.Y), SkinDraw.U32(s.Text), text);
        }
    }

    private (string? Text, Vector4 Bg, Vector4 Fg, Vector4 Line) SkinChip(FrameData f, SkinSpec s, bool raising, bool dead, bool offline, bool cleanse)
    {
        string L(string t) => s.UpperLabels ? t.ToUpperInvariant() : t;
        var none = Vector4.Zero;
        if (f.Buster) return ($"{s.BusterLabel} {f.BusterLeft:0.0}s", Cfg.BusterColor, s.OnBuster, none);
        if (f.MustHeal)
            return (s.TextAboveBar && f.CriticalSeconds > 0 ? $"{L("heal full")} {f.CriticalSeconds:0}s" : L("heal full"),
                Cfg.MustHealColor, s.OnCritical, none);
        if (raising) return (L("raising"), Cfg.RaiseColor, s.OnRaise, none);
        if (dead) return (L("dead"), none, s.Muted, s.Muted);
        if (offline) return (L("offline"), none, s.Muted, s.Muted);
        if (f.State == MemberState.Away) return (L("away"), none, s.Muted, s.Muted);
        if (cleanse) return (L("esuna"), Cfg.DispelColor, s.OnCleanse, s.CleanseBorder ? s.CleanseLine : none);
        if (f.Pet == PetState.Missing) return (L("no " + PetName(f.JobId)), s.Warn, s.OnWarn, none);
        return (null, none, none, none);
    }

    private string SkinSubText(FrameData f, SkinSpec s, bool raising)
    {
        if (Cfg.ShowCastName && f.CastProgress >= 0 && f.CastName.Length > 0)
            return s.UpperLabels ? $"{f.CastName} \u00B7 casting" : $"casting {f.CastName}";
        if (f.MustHeal && f.CriticalText.Length > 0) return $"{f.CriticalText} {f.CriticalSeconds:0}s";
        if (f.Dispellable && f.CleanseText.Length > 0) return f.CleanseText;
        if (raising && f.RaisedBy.Length > 0) return s.UpperLabels ? $"Raise from {f.RaisedBy}" : $"raised by {f.RaisedBy}";
        return string.Empty;
    }

    private Vector4 SkinSubColor(FrameData f, bool raising) =>
        Cfg.ShowCastName && f.CastProgress >= 0 ? Cfg.CastColor
        : f.MustHeal ? new Vector4(1f, 0.61f, 0.76f, 1f)
        : f.Dispellable ? new Vector4(0.82f, 0.75f, 0.98f, 1f)
        : raising ? Cfg.RaiseColor
        : Cfg.TextColor;

    private RoleBadge BadgeFor(SkinSpec s) => Cfg.JobBadge == JobBadgeStyle.GameIcon ? RoleBadge.JobIcon : s.Badge;

    private void DrawSkinnedFrame(FrameData f, Vector2 start, Vector2 size, SkinSpec s, bool compact, bool hovered)
    {
        var cfg = Cfg;
        var fonts = plugin.GetSkinFonts(s, FontU);
        var dl = ImGui.GetWindowDrawList();
        var end = start + size;
        var scale = Math.Min(cfg.FontScale, 1.4f) * (compact ? 0.8f : 1f);

        var dead = f.State == MemberState.Dead;
        var offline = f.State == MemberState.Disconnected;
        var unavailable = offline || f.State == MemberState.Away;
        var raising = dead && f.RaiseIncoming;
        var ok = !dead && !unavailable;
        var cleanse = f.Dispellable && !offline;

        // Out of range: the bar, badge and icons fade; the name and numbers stay readable.
        var alpha = offline ? 0.55f : f.InRange && !unavailable ? 1f : cfg.OutOfRangeAlpha;
        var textAlpha = offline ? 0.55f : alpha < 1f ? MathF.Max(0.75f, alpha) : 1f;

        var r = cfg.Rounding * U;
        var frac = f.MaxHp == 0 ? 0f : Math.Clamp((float)f.Hp / f.MaxHp, 0f, 1f);
        var separateBar = s.TextAboveBar && !compact;
        var mpHeight = (compact ? 2f : Math.Clamp(cfg.MpBarHeight, 2f, 5f)) * U;

        if (!compact) DrawSkinRail(dl, f, s, fonts, start, end, alpha, unavailable);

        dl.AddRectFilled(start, end, SkinDraw.U32(cfg.BackgroundColor, alpha), r);

        // ---- the bar: full frame (Ledger, Heraldry) or a separate band under the text (Signal)
        Vector2 barMin, barMax;
        if (separateBar)
        {
            var bh = MathF.Max(6f * U, size.Y * 0.23f);
            barMin = new Vector2(start.X + 8 * U, end.Y - 8 * U - bh);
            barMax = new Vector2(end.X - 8 * U, end.Y - 8 * U);
            dl.AddRectFilled(barMin, barMax, SkinDraw.U32(s.Track, alpha), 2f * U);
        }
        else
        {
            barMin = start;
            barMax = new Vector2(end.X, end.Y - (cfg.ShowMpBar || compact ? mpHeight : 0));
        }

        var barWidth = barMax.X - barMin.X;
        var fill = offline || raising ? 1f : dead ? 0f : frac;

        // The bar always shows health. A removable debuff never replaces that color: it adds stripes
        // over the health (and a border + ESUNA label), so low HP stays visible.
        var barColor = offline ? cfg.DisconnectedColor
            : raising ? (separateBar ? cfg.RaiseColor : s.RaiseBar)
            : cfg.ColorMode == HpColorMode.ByRole ? RoleColor(f.Role)
            : frac < cfg.LowHpThreshold ? cfg.LowHpColor
            : frac < cfg.MidHpThreshold ? cfg.MidHpColor
            : cfg.HighHpColor;
        var barRound = separateBar ? 2f * U : r;
        var fillMax = new Vector2(barMin.X + barWidth * fill, barMax.Y);
        if (fill > 0) dl.AddRectFilled(barMin, fillMax, SkinDraw.U32(barColor, alpha), barRound);

        if (s.StripedCleanse && cleanse && fill > 0 && cfg.DispelHighlight != DispelStyle.BorderOnly)
            SkinDraw.Hatch(dl, barMin, fillMax, SkinDraw.U32(cfg.DispelColor with { W = 0.9f }, alpha), 7f * U, 2.5f * U, true);
        if (f.HealIncoming && ok && fill < 1)
            SkinDraw.Hatch(dl, new Vector2(fillMax.X, barMin.Y), barMax, SkinDraw.U32(cfg.IncomingHealColor, alpha), 7f * U, 2f * U, false);

        if (cfg.ShowShields && f.ShieldPct > 0 && ok)
        {
            var sw = Math.Min(barWidth, barWidth * f.ShieldPct / 100f);
            var sx = Math.Min(fillMax.X, barMax.X - sw);
            if (separateBar)
                dl.AddRectFilled(new Vector2(sx, barMin.Y), new Vector2(sx + sw, barMin.Y + 3 * U), SkinDraw.U32(cfg.ShieldColor, alpha));
            else
                dl.AddRectFilled(new Vector2(sx, barMax.Y - 5 * U), new Vector2(sx + sw, barMax.Y), SkinDraw.U32(cfg.ShieldColor, alpha));
        }

        if (s.Ticks && separateBar)
        {
            for (var i = 1; i < 10; i++)
            {
                var tx = barMin.X + barWidth * i / 10f;
                dl.AddLine(new Vector2(tx, barMin.Y), new Vector2(tx, barMax.Y), SkinDraw.U32(s.Panel, 0.7f * alpha), 1f);
            }
        }

        // MP line, or the cast's progress while casting
        var casting = f.CastProgress >= 0 && !unavailable;
        var lineFrac = casting ? f.CastProgress
            : cfg.ShowMpBar && f.MaxMp > 0 && ok ? Math.Clamp((float)f.Mp / f.MaxMp, 0f, 1f) : 0f;
        if (lineFrac > 0)
        {
            var lineColor = casting ? cfg.CastColor : cfg.MpColor;
            if (separateBar)
                dl.AddRectFilled(new Vector2(start.X + 8 * U, end.Y - 5 * U), new Vector2(start.X + 8 * U + barWidth * lineFrac, end.Y - 3 * U), SkinDraw.U32(lineColor, alpha), 1f);
            else
                dl.AddRectFilled(new Vector2(start.X, end.Y - mpHeight), new Vector2(start.X + size.X * lineFrac, end.Y), SkinDraw.U32(lineColor, alpha));
        }

        // ---- content row
        var rowTop = start.Y + (separateBar ? 2 * U : 0);
        var rowHeight = separateBar ? barMin.Y - rowTop - 2 * U : barMax.Y - start.Y;
        var x = start.X + 6 * U;
        var right = end.X - 8 * U;
        var shadow = cfg.TextShadow && !separateBar ? SkinDraw.U32(new Vector4(0f, 0f, 0f, 0.9f), textAlpha) : 0u;

        if (cfg.ShowRoleIcon && f.JobId != 0)
        {
            var bs = MathF.Min((compact ? 14f : 24f) * U, rowHeight - 6 * U);
            var by = rowTop + (rowHeight - bs) / 2;
            switch (BadgeFor(s))
            {
                case RoleBadge.Crest:
                    SkinDraw.Crest(dl, new Vector2(x + bs / 2, by + bs / 2), bs / 2, f.Role,
                        SkinDraw.U32(RoleColor(f.Role), alpha), SkinDraw.U32(s.Gold, alpha), SkinDraw.U32(s.Text, alpha));
                    x += bs + 8 * U;
                    break;
                case RoleBadge.Tag:
                {
                    var tag = JobAbbr(f.JobId);
                    var tw = SkinDraw.Measure(fonts.Tag, scale, tag).X;
                    var th = SkinDraw.Height(fonts.Tag, scale);
                    var tMin = new Vector2(x, rowTop + (rowHeight - th - 2 * U) / 2);
                    var tMax = tMin + new Vector2(tw + 8 * U, th + 2 * U);
                    dl.AddRect(tMin, tMax, SkinDraw.U32(RoleColor(f.Role), alpha), 3f * U);
                    SkinDraw.Text(dl, fonts.Tag, scale, tMin + new Vector2(4 * U, 1 * U), SkinDraw.U32(RoleColor(f.Role), textAlpha), tag);
                    x = tMax.X + 8 * U;
                    break;
                }
                case RoleBadge.FilledTag:
                {
                    // Ledger: a flat role-colored square with the job's letters.
                    var tag = JobAbbr(f.JobId);
                    var tw = SkinDraw.Measure(fonts.Tag, scale, tag).X;
                    var th = SkinDraw.Height(fonts.Tag, scale);
                    var bw = MathF.Max(bs, tw + 6 * U);
                    var bMin = new Vector2(x, by);
                    dl.AddRectFilled(bMin, bMin + new Vector2(bw, bs), SkinDraw.U32(RoleColor(f.Role), alpha), 2f * U);
                    SkinDraw.Text(dl, fonts.Tag, scale, bMin + new Vector2((bw - tw) / 2, (bs - th) / 2), SkinDraw.U32(Vector4.One, textAlpha), tag);
                    x += bw + 8 * U;
                    break;
                }
                default:
                    dl.AddImageRounded(Icons.Texture(JobIconBase + f.JobId), new Vector2(x, by), new Vector2(x + bs, by + bs),
                        Vector2.Zero, Vector2.One, SkinDraw.U32(Vector4.One, alpha), 2f * U);
                    x += bs + 8 * U;
                    break;
            }

            // Pet status (Scholar, Summoner): at the badge's top-right corner
            DrawPetMarker(dl, f, new Vector2(x - 8 * U - 1 * U, by + 1 * U), 6f * U, alpha);

            // Your Kardia / Dance Partner: tucked at the badge's corner
            if (f.PartnerIcon != 0 && !unavailable)
            {
                var ph = MathF.Min(12f * U, rowHeight * 0.45f);
                var pp = new Vector2(x - 8 * U - ph * 0.75f + 2 * U, rowTop + rowHeight - ph - 1 * U);
                dl.AddImage(Icons.Texture(f.PartnerIcon), pp, pp + new Vector2(ph * 0.75f, ph), Vector2.Zero, Vector2.One, SkinDraw.U32(Vector4.One, alpha));
            }
        }

        // HP number, right-aligned. Compact (alliance) frames have no status label, so their number slot says it.
        string Up(string t) => s.UpperLabels ? t.ToUpperInvariant() : t;
        var hpText = ok ? (compact ? $"{frac * 100:0}%" : HpText(f, frac))
            : compact ? (offline ? "D/C" : raising ? Up("raise") : dead ? Up("dead") : Up("away"))
            : offline ? "-" : string.Empty;
        if (f.MustHeal && ok && !compact && hpText.Length == 0) hpText = $"{frac * 100:0}%";
        if (hpText.Length > 0)
        {
            var w = SkinDraw.Measure(fonts.Number, scale, hpText).X;
            var nh = SkinDraw.Height(fonts.Number, scale);
            SkinDraw.Text(dl, fonts.Number, scale, new Vector2(right - w, rowTop + (rowHeight - nh) / 2), SkinDraw.U32(cfg.TextColor, textAlpha), hpText, shadow);
            right -= w + 8 * U;
        }

        // One status label, by priority
        var (chip, chipBg, chipFg, chipLine) = SkinChip(f, s, raising, dead, offline, cleanse);
        if (chip != null && !compact)
        {
            var cw = SkinDraw.Measure(fonts.Chip, scale, chip).X;
            var ch = SkinDraw.Height(fonts.Chip, scale) + 2 * U;
            var cMin = new Vector2(right - cw - 10 * U, rowTop + (rowHeight - ch) / 2);
            var cMax = new Vector2(right, cMin.Y + ch);
            if (chipBg.W > 0) dl.AddRectFilled(cMin, cMax, SkinDraw.U32(chipBg, textAlpha), s.ChipRadius * U);
            if (chipLine.W > 0) dl.AddRect(cMin, cMax, SkinDraw.U32(chipLine, textAlpha), s.ChipRadius * U);
            SkinDraw.Text(dl, fonts.Chip, scale, cMin + new Vector2(5 * U, 1 * U), SkinDraw.U32(chipFg, textAlpha), chip);
            right = cMin.X - 6 * U;
        }

        // Status icons, each with its timer
        if (!compact && !unavailable && f.Statuses.Count > 0)
        {
            var ih = MathF.Min(cfg.StatusIconSize * U, rowHeight - 4 * U);
            var iw = ih * 0.75f;
            var timerH = SkinDraw.Height(fonts.Tag, 1f);
            foreach (var st in f.Statuses)
            {
                var p0 = new Vector2(right - iw, rowTop + (rowHeight - ih) / 2);
                if (p0.X < x + 96 * U) break; // keep room for the name
                dl.AddImage(Icons.Texture(st.Icon), p0, p0 + new Vector2(iw, ih), Vector2.Zero, Vector2.One, SkinDraw.U32(Vector4.One, alpha));
                if (cfg.ShowStatusTimers && st.Remaining > 0 && st.Remaining < 100)
                {
                    var t = $"{st.Remaining:0}";
                    var tw = SkinDraw.Measure(fonts.Tag, 1f, t).X;
                    SkinDraw.Text(dl, fonts.Tag, 1f, new Vector2(p0.X + (iw - tw) / 2, p0.Y + ih - timerH + 1 * U),
                        SkinDraw.U32(Vector4.One, textAlpha), t, SkinDraw.U32(new Vector4(0f, 0f, 0f, 1f), textAlpha));
                }
                right = p0.X - 2 * U;
            }
            right -= 4 * U;
        }

        // Name, and a second line with what's going on (Ledger, Heraldry; only on frames tall enough)
        var sub = !compact && !separateBar && cfg.FrameHeight >= 36 ? SkinSubText(f, s, raising) : string.Empty;
        var nameH = SkinDraw.Height(fonts.Name, scale);
        var subH = sub.Length > 0 ? SkinDraw.Height(fonts.Sub, scale) : 0f;
        var block = nameH + (subH > 0 ? subH - 2 * U : 0);
        var ny = rowTop + (rowHeight - block) / 2;
        var name = SkinDraw.Fit(fonts.Name, scale, compact ? f.Name.Split(' ')[0] : NameFor(f), right - x);
        if (name.Length > 0) SkinDraw.Text(dl, fonts.Name, scale, new Vector2(x, ny), SkinDraw.U32(cfg.TextColor, textAlpha), name, shadow);
        if (sub.Length > 0)
            SkinDraw.Text(dl, fonts.Sub, scale, new Vector2(x, ny + nameH - 2 * U), SkinDraw.U32(SkinSubColor(f, raising), textAlpha),
                SkinDraw.Fit(fonts.Sub, scale, sub, right - x), shadow);

        // ---- borders: one at a time, most important first
        if (f.MustHeal && ok)
            dl.AddRect(start, end, SkinDraw.U32(cfg.MustHealColor, 0.6f + 0.4f * Pulse(2f)), r, ImDrawFlags.None, 2f * U);
        else if (cleanse && s.CleanseBorder && cfg.DispelHighlight != DispelStyle.BarColor)
            dl.AddRect(start, end, SkinDraw.U32(s.CleanseLine, alpha), r, ImDrawFlags.None, 2f * U);
        else if (offline)
            dl.AddRect(start, end, SkinDraw.U32(s.Muted, 0.5f), r, ImDrawFlags.None, 1f * U); // solid, muted: no square dash corners
        else if (s.Line.W > 0)
            dl.AddRect(start, end, SkinDraw.U32(s.Line, alpha), r, ImDrawFlags.None, 1f);

        if (hovered) dl.AddRect(start, end, SkinDraw.U32(cfg.HoverColor), r, ImDrawFlags.None, 1.5f * U);

        if (f.Buster)
            dl.AddRect(start - new Vector2(4 * U), end + new Vector2(4 * U), SkinDraw.U32(cfg.BusterColor, 0.55f + 0.45f * Pulse(2f)), r + 3 * U, ImDrawFlags.None, 2f * U);
    }
}
