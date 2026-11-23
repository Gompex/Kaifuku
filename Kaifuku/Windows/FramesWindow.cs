using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface.Windowing;
using Dalamud.Game.ClientState.Party;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using GameObjectStruct = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;
using FFXIVClientStructs.FFXIV.Client.UI;
using LuminaStatus = Lumina.Excel.Sheets.Status;

namespace Kaifuku.Windows;

public sealed partial class FramesWindow : Window
{
    private const float MaxRange = 30f;
    private const uint JobIconBase = 62100; // game icon ID of each job's icon = 62100 + job ID

    private readonly Plugin plugin;

    // Priority: 0 = must heal to full (Doom, Walking Dead), 1 = debuff, 2 = tank cooldown, 3 = buff.
    private readonly record struct StatusIcon(uint Icon, float Remaining, bool Debuff, int Priority = 3);

    private enum MemberState { Normal, Dead, Disconnected, Away }

    private sealed class FrameData
    {
        public string Name = string.Empty;
        public uint EntityId, Hp, MaxHp, Mp, MaxMp, JobId;
        public byte ShieldPct, Role;
        public bool InRange = true, Dispellable, IsSelf, Buster, HasHot, HasAggro, RaiseIncoming, MustHeal, HealIncoming;
        public List<StatusIcon> Statuses = new();
        public int Order, EnemiesOnMe, Enmity = -1;
        public MemberState State;
        public float CastProgress = -1f;
        public string CastName = string.Empty;
        public uint PartnerIcon;
        public PetState Pet;
        public float BusterLeft, CriticalSeconds;
        public string CriticalText = string.Empty, CleanseText = string.Empty, RaisedBy = string.Empty;
    }

    // Raises: Raise, Resurrection (SCH/SMN), Ascend, Egeiro, Verraise.
    private static readonly HashSet<uint> RaiseActions = [125, 173, 3603, 24287, 7523];
    private static HashSet<uint>? raisePendingStatuses;   // "Raise" waiting to be accepted
    private static HashSet<uint>? partnerStatuses;        // Kardion (your Kardia), Dance Partner
    private static HashSet<uint> RaisePending => raisePendingStatuses ??= StatusCatalog.IdsByEnglishName("Raise");
    private static HashSet<uint> PartnerStatuses => partnerStatuses ??= StatusCatalog.IdsByEnglishName("Kardion", "Dance Partner");

    // Pet jobs: Arcanist, Summoner, Scholar. Scholar's Dissipation sends the fairy away on purpose.
    private enum PetState { None, Present, Missing, Dissipated }
    private const uint ACN = 26, SMN = 27, SCH = 28;
    private static bool IsPetJob(uint job) => job is ACN or SMN or SCH;
    private static HashSet<uint>? dissipationStatuses;
    private static HashSet<uint> Dissipation => dissipationStatuses ??= StatusCatalog.IdsByEnglishName("Dissipation");

    // Owners of a summoned pet (fairy, Seraph, carbuncle, demi-summons) this frame.
    private readonly HashSet<uint> petOwners = new();

    // Action icons used as the pet marker: Summon Eos (fairy), Summon Carbuncle.
    private const uint SummonEos = 17215, SummonCarbuncle = 25798;
    private static uint PetIcon(uint job) => Icons.ActionIcon(job == SCH ? SummonEos : SummonCarbuncle);
    private static string PetName(uint job) => job == SCH ? "fairy" : "pet";

    /// <summary>
    /// A small round pet icon at a badge's corner: in color when the pet is out, crossed out in red when it's
    /// missing, dimmed during Scholar's Dissipation (sent away on purpose).
    /// </summary>
    private void DrawPetMarker(ImDrawListPtr dl, FrameData f, Vector2 center, float radius, float alpha)
    {
        if (f.Pet == PetState.None) return;
        var icon = PetIcon(f.JobId);
        var min = center - new Vector2(radius);
        var max = center + new Vector2(radius);
        var tint = f.Pet == PetState.Present ? 1f : 0.35f;

        dl.AddCircleFilled(center, radius + 1, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.85f * alpha)));
        if (icon != 0)
            dl.AddImageRounded(Icons.Texture(icon), min, max, Vector2.Zero, Vector2.One,
                ImGui.GetColorU32(new Vector4(tint, tint, tint, alpha)), radius);
        else
            dl.AddCircleFilled(center, radius, ImGui.GetColorU32(new Vector4(0.6f, 0.8f, 1f, tint * alpha)));

        if (f.Pet == PetState.Missing)
        {
            var red = ImGui.GetColorU32(new Vector4(1f, 0.3f, 0.3f, alpha));
            dl.AddCircle(center, radius + 1, red, 0, 1.5f);
            dl.AddLine(center + new Vector2(-radius * 0.7f, radius * 0.7f), center + new Vector2(radius * 0.7f, -radius * 0.7f), red, 1.5f);
        }
    }

    private static bool IsRaiseBinding(uint id) => Casting.IsSwiftRaise(id) || RaiseActions.Contains(id);

    private static readonly Dictionary<uint, string> actionNames = new();
    private static string ActionName(uint id)
    {
        if (actionNames.TryGetValue(id, out var name)) return name;
        name = Icons.Name(id);
        actionNames[id] = name;
        return name;
    }

    public FramesWindow(Plugin plugin) : base("Kaifuku##frames",
        ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.AlwaysAutoResize |
        ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav)
    {
        this.plugin = plugin;
        RespectCloseHotkey = false; // Esc shouldn't close the frames mid-fight
    }

    private Configuration Cfg => plugin.Config;

    private UiStyle? lastGameStyle;

    // Party members an enemy is currently casting something at (tank buster warning).
    private readonly Dictionary<uint, float> busterTargets = new(); // target -> seconds left

    /// <summary>
    /// A tank buster shows up as an enemy cast aimed at one specific player. Raidwides and ground AoEs are
    /// aimed at the caster itself or at nobody, so they don't count; very short casts (trash spells) are
    /// skipped by the minimum cast time.
    /// </summary>
    private void UpdateBusterTargets()
    {
        busterTargets.Clear();
        if (!Cfg.TankBusterWarning) return;

        foreach (var obj in Plugin.ObjectTable)
        {
            // Sub-kind 5 = combatant (an enemy you can fight). Compared by number because the enum name differs between Dalamud versions.
            if (obj is not IBattleNpc npc || (byte)npc.BattleNpcKind != 5) continue;
            if (CastOf(npc) is not { } cast || cast.Total < Cfg.BusterMinCastTime) continue;

            var target = cast.TargetId;
            if (target == 0 || target == 0xE000_0000 || target == npc.EntityId) continue;
            var left = cast.Total - cast.Current;
            busterTargets[target] = busterTargets.TryGetValue(target, out var other) ? MathF.Min(other, left) : left;
        }
    }

    // My pets (fairy, Seraph, Liturgy bell, carbuncle...): their buffs count as mine, e.g. Whispering Dawn.
    private readonly HashSet<uint> myPets = new();
    // How many fighting enemies are targeting each member, and who the tracked enemy is attacking.
    private readonly Dictionary<uint, int> enemiesOn = new();
    private uint aggroHolder;
    // Enmity (0-100) of each member on your current target, from the game's own enmity list.
    private readonly Dictionary<uint, int> enmity = new();
    // Members someone is casting a raise on right now.
    private readonly Dictionary<uint, string> raiseTargets = new(); // target -> who is raising
    // Members another player is casting a single-target spell on (a heal or shield on the way).
    private readonly HashSet<uint> healTargets = new();
    // The enemy raidwide finishing soonest: spell name, seconds left, progress 0..1.
    private (string Name, float Left, float Progress)? raidwide;
    private static readonly Dictionary<uint, bool> raidwideActions = new();

    /// <summary>A raidwide is aimed at the caster itself (or nowhere) and reaches the whole arena.</summary>
    /// <summary>A cast read safely: null when the object isn't casting or has no cast information.</summary>
    private readonly record struct CastState(uint ActionId, uint TargetId, float Current, float Total);

    private static CastState? CastOf(IBattleChara? chara)
    {
        if (chara == null) return null;
        try
        {
            // Some objects (characters spawning or despawning, some NPCs) have no cast information,
            // and Dalamud's IsCasting throws on them instead of returning false.
            if (!chara.IsValid() || !chara.IsCasting) return null;
            return new CastState(chara.CastActionId, (uint)chara.CastTargetObjectId, chara.CurrentCastTime, chara.TotalCastTime);
        }
        catch (NullReferenceException)
        {
            return null;
        }
    }

    private static bool IsRaidwide(uint casterId, CastState cast)
    {
        var target = cast.TargetId;
        if (target != 0 && target != 0xE000_0000 && target != casterId) return false;
        var id = cast.ActionId;
        if (raidwideActions.TryGetValue(id, out var known)) return known;
        var row = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>().GetRowOrDefault(id);
        // Arena-wide circles only: a line or cone's range is its length, so 40 y lines aren't raidwides.
        var wide = row != null && row.Value.CastType == 2 && row.Value.EffectRange >= 30;
        raidwideActions[id] = wide;
        return wide;
    }

    // The game's party list order (<1> = 0, <2> = 1...).
    private readonly Dictionary<uint, int> gameOrder = new();
    // The Left-click skill, used for the real range / line of sight check.
    private uint rangeAction;

    private static bool IsEnemy(IGameObject? obj) => obj is IBattleNpc npc && (byte)npc.BattleNpcKind == 5;

    private bool IsMine(uint sourceId, uint myId) => sourceId == myId || myPets.Contains(sourceId);

    private unsafe void UpdateWorld(uint myId)
    {
        myPets.Clear();
        petOwners.Clear();
        enemiesOn.Clear();
        enmity.Clear();
        raiseTargets.Clear();
        healTargets.Clear();
        raidwide = null;
        gameOrder.Clear();
        aggroHolder = 0;

        var hud = AgentHUD.Instance();
        if (hud != null)
        {
            for (var i = 0; i < hud->PartyMemberCount; i++)
                gameOrder[hud->PartyMembers[i].EntityId] = i;
        }

        IBattleNpc? biggest = null;
        foreach (var obj in Plugin.ObjectTable)
        {
            if (obj.OwnerId == myId && obj.EntityId != myId) myPets.Add(obj.EntityId);
            // BattleNpc sub-kind 2 = pet. Compared by number, like the enemy check.
            if (obj is IBattleNpc pet && (byte)pet.BattleNpcKind == 2 && pet.OwnerId != 0 && !pet.IsDead) petOwners.Add(pet.OwnerId);
            if (obj is IPlayerCharacter caster && CastOf(caster) is { } playerCast)
            {
                var castTarget = playerCast.TargetId;
                if (RaiseActions.Contains(playerCast.ActionId)) raiseTargets[castTarget] = caster.Name.TextValue;
                else if (caster.EntityId != myId && castTarget != caster.EntityId) healTargets.Add(castTarget);
            }

            if (Cfg.RaidwideWarning && obj is IBattleNpc caster2 && (byte)caster2.BattleNpcKind == 5
                && CastOf(caster2) is { } enemyCast && enemyCast.Total >= Cfg.RaidwideMinCastTime && IsRaidwide(caster2.EntityId, enemyCast))
            {
                var left = enemyCast.Total - enemyCast.Current;
                if (raidwide == null || left < raidwide.Value.Left)
                    raidwide = (ActionName(enemyCast.ActionId), left, enemyCast.Current / enemyCast.Total);
            }

            if (!Cfg.ShowAggro || obj is not IBattleNpc npc || (byte)npc.BattleNpcKind != 5 || npc.IsDead) continue;
            var target = (uint)npc.TargetObjectId;
            if (target == 0 || target == 0xE000_0000 || target == npc.EntityId) continue;

            enemiesOn[target] = enemiesOn.GetValueOrDefault(target) + 1;
            if (biggest == null || npc.MaxHp > biggest.MaxHp) biggest = npc;
        }

        if (!Cfg.ShowAggro) return;

        // Whose aggro to show: your target if it's an enemy, else your focus target, else the biggest
        // enemy that is attacking someone (in a boss fight, the boss).
        var current = Plugin.TargetManager.Target;
        var tracked = IsEnemy(current) ? current
            : IsEnemy(Plugin.TargetManager.FocusTarget) ? Plugin.TargetManager.FocusTarget
            : biggest;
        if (tracked != null) aggroHolder = (uint)tracked.TargetObjectId;

        if (Cfg.ShowEnmityGauge && IsEnemy(current))
        {
            var ui = UIState.Instance();
            if (ui != null)
            {
                for (var i = 0; i < ui->Hate.HateArrayLength; i++)
                {
                    var entry = ui->Hate.HateInfo[i];
                    enmity[entry.EntityId] = entry.Enmity;
                }
            }
        }
    }

    private bool HasMyHot(IEnumerable<(LuminaStatus Row, uint SourceId, float RemainingTime)> raw, uint myId) =>
        Cfg.ShowHotIndicator &&
        raw.Any(s => Cfg.HotStatuses.Contains(s.Row.RowId) && (Cfg.HotAnyone || IsMine(s.SourceId, myId)));

    private int EnmityOf(uint entityId) => enmity.TryGetValue(entityId, out var e) ? e : -1;

    private unsafe bool InRangeOf(IPlayerCharacter player, IGameObject target)
    {
        if (target.EntityId == player.EntityId) return true;
        if (Cfg.RangeCheck == RangeMode.Distance30 || rangeAction == 0)
            return Vector3.Distance(target.Position, player.Position) <= MaxRange;
        // 0 = in range and in line of sight for that skill.
        return ActionManager.GetActionInRangeOrLoS(rangeAction, (GameObjectStruct*)player.Address, (GameObjectStruct*)target.Address) == 0;
    }

    private void UpdateRangeAction(uint job)
    {
        var id = Bindings.Resolve(job, Modifier.None, MouseButton.Left, Cfg);
        if (MacroRunner.IsMacro(id)) id = 0;
        else if (Casting.IsSwiftRaise(id)) id = Casting.RaiseOf(id);
        else if (id == Casting.EukrasianDiagnosis) id = Casting.Diagnosis;
        rangeAction = id;
    }

    // Tanks only, unless the user wants a warning for anyone an enemy casts at.
    private bool IsBusterTarget(uint entityId, byte role) =>
        busterTargets.ContainsKey(entityId) && (role == 1 || Cfg.BusterAnyMember);

    public override bool DrawConditions()
    {
        var player = Plugin.ObjectTable.LocalPlayer;
        if (player == null) return false;
        return Cfg.Preview || Bindings.JobKey(player.ClassJob.RowId, Cfg) != 0;
    }

    public override void PreDraw()
    {
        if (Cfg.Locked) Flags |= ImGuiWindowFlags.NoMove;
        else Flags &= ~ImGuiWindowFlags.NoMove;

        // Follow the game's UI theme (System Configuration > Theme) when asked to. React only when the
        // game's setting changes (or right after turning this on), so it never overrides a style every frame.
        if (!Cfg.FollowGameTheme || Cfg.Skin != Skin.Classic)
        {
            lastGameStyle = null;
        }
        else if (UiStyles.TryGetGameStyle(out var gameStyle) && lastGameStyle != gameStyle)
        {
            lastGameStyle = gameStyle;
            if (gameStyle != Cfg.Style)
            {
                UiStyles.Apply(Cfg, gameStyle);
                plugin.SaveConfig();
            }
        }

        // First run (or "Snap next to party list"): place the frames beside the game's party list.
        if (!Cfg.AutoPositioned && TryGetPartyListAnchor(out var pos))
        {
            Position = pos;
            PositionCondition = ImGuiCond.Always;
            Cfg.AutoPositioned = true;
            plugin.SaveConfig();
        }
        else
        {
            Position = null; // after that, ImGui remembers wherever the frames are
        }
    }

    public override void Draw()
    {
        var player = Plugin.ObjectTable.LocalPlayer;
        if (player == null) return;

        var spec = UiStyles.Get(Cfg.Style);
        UpdateBusterTargets();
        UpdateWorld(player.EntityId);
        var job = player.ClassJob.RowId;
        UpdateRangeAction(job);
        var frames = Cfg.Preview ? PreviewFrames() : GatherFrames(player);
        var groups = Arrange(frames);
        var alliance = Cfg.Preview ? PreviewAlliance() : GatherAlliance(player);

        var sizeSkin = Skins.Get(Cfg.Skin);
        var height = sizeSkin != null ? Skins.ClampHeight(sizeSkin, Cfg.FrameHeight) : Cfg.FrameHeight;
        var size = new Vector2(Cfg.FrameWidth, height) * U;
        var columns = Math.Max(1, Cfg.Columns);
        var spacing = Cfg.Spacing * U;

        var widest = Math.Min(columns, Math.Max(1, groups.Count == 0 ? 1 : groups.Max(g => g.Count)));
        var gridWidth = widest * (size.X + BarSpace) + (widest - 1) * spacing;

        var skin = Skins.Get(Cfg.Skin);
        if (Cfg.ShowPanel)
        {
            var windowPos = ImGui.GetWindowPos();
            if (skin != null)
                SkinDraw.Panel(ImGui.GetWindowDrawList(), windowPos, windowPos + ImGui.GetWindowSize(), skin, Cfg.PanelOpacity, Cfg.PanelOpaque);
            else
                StyleDraw.Panel(ImGui.GetWindowDrawList(), windowPos, windowPos + ImGui.GetWindowSize(), spec, Cfg.PanelOpacity, Cfg.PanelOpaque);

        }

        // One fixed slot above the frames: the header normally, the raidwide warning while one is coming.
        // It always takes the same height, so the frames never move when a warning appears or goes away.
        var showHeader = Cfg.ShowPanel && Cfg.ShowPanelHeader;
        var wide = Cfg.Preview && Cfg.RaidwideWarning
            ? ("Cataclysm", 5f - Environment.TickCount64 / 1000f % 5f, Environment.TickCount64 / 1000f % 5f / 5f)
            : raidwide;
        if (showHeader || Cfg.RaidwideWarning)
        {
            var slotStart = ImGui.GetCursorScreenPos();
            var slotHeight = 28f * U;

            if (wide is { } rw)
            {
                if (skin != null) DrawSkinRaidwide(skin, rw.Item1, rw.Item2, rw.Item3, gridWidth);
                else DrawRaidwide(rw.Item1, rw.Item2, rw.Item3, gridWidth);
            }
            else if (showHeader)
            {
                // In an alliance raid the header says which alliance you're in: "Alliance A", "B" or "C".
                var letter = Cfg.Preview && Cfg.ShowAlliance ? 'A' : CurrentAllianceLetter();
                var title = letter is { } l ? $"Alliance {l}"
                    : Plugin.PartyList.IsAlliance ? "Alliance"
                    : frames.Count == 1 ? "Solo" : "Party";
                var alive = frames.Count(fr => fr.State == MemberState.Normal);
                if (skin != null) DrawSkinHeader(skin, title, frames.Count == 1 ? null : $"{alive} alive", gridWidth);
                else StyleDraw.Header(plugin, spec, title, frames.Count == 1 ? "1 member" : $"{frames.Count} members", gridWidth);
            }

            ImGui.SetCursorScreenPos(slotStart);
            ImGui.Dummy(new Vector2(gridWidth, slotHeight));
            ImGui.SetCursorScreenPos(slotStart + new Vector2(0, slotHeight + 6f * U));
        }

        var origin = ImGui.GetCursorScreenPos();
        var y = origin.Y;
        var first = true;

        foreach (var group in groups)
        {
            if (group.Count == 0) continue;
            if (!first) y += Cfg.GroupGap * U;
            first = false;

            for (var i = 0; i < group.Count; i++)
            {
                var col = i % columns;
                var row = i / columns;
                var pos = new Vector2(origin.X + BarSpace + col * (size.X + spacing + BarSpace), y + row * (size.Y + spacing));
                ImGui.SetCursorScreenPos(pos);
                DrawFrame(group[i], job, pos, size, spec);
            }

            var rows = (group.Count + columns - 1) / columns;
            y += rows * (size.Y + spacing);
        }

        // The other two alliance parties: compact frames under yours.
        if (alliance.Count > 0)
        {
            var asize = new Vector2(Cfg.AllianceFrameWidth, Cfg.AllianceFrameHeight) * U;
            var acols = Math.Max(1, Cfg.AllianceColumns);
            for (var g = 0; g < alliance.Count; g++)
            {
                y += Cfg.GroupGap * U;
                var groupLabel = "Other parties";
                if (g > 0)
                {
                    // second group: just a gap, the label above covers both
                }
                else if (skin != null)
                {
                    // Same small label style as the skin's header note, with a hairline to the right.
                    var fonts = plugin.GetSkinFonts(skin, FontU);
                    var dlA = ImGui.GetWindowDrawList();
                    var text = skin.UpperHeaders ? groupLabel.ToUpperInvariant() : groupLabel;
                    var lh = SkinDraw.Height(fonts.Sub, 1f);
                    var lw = SkinDraw.Text(dlA, fonts.Sub, 1f, new Vector2(origin.X, y), SkinDraw.U32(skin.Muted), text);
                    var lineColor = skin.PanelLine.W > 0 ? skin.PanelLine : skin.Track;
                    dlA.AddLine(new Vector2(origin.X + lw + 8 * U, y + lh / 2), new Vector2(origin.X + gridWidth, y + lh / 2), SkinDraw.U32(lineColor), 1f);
                    ImGui.SetCursorScreenPos(new Vector2(origin.X, y));
                    ImGui.Dummy(new Vector2(1, lh));
                    y += lh + 4 * U;
                }
                else
                {
                    ImGui.SetCursorScreenPos(new Vector2(origin.X, y));
                    ImGui.TextColored(spec.SubtextColor, groupLabel);
                    y += ImGui.GetTextLineHeight() + 2;
                }

                var list = alliance[g];
                for (var i = 0; i < list.Count; i++)
                {
                    var pos = new Vector2(origin.X + (i % acols) * (asize.X + spacing), y + (i / acols) * (asize.Y + spacing));
                    ImGui.SetCursorScreenPos(pos);
                    DrawFrame(list[i], job, pos, asize, spec, compact: true, group: 1 + g);
                }
                y += (list.Count + acols - 1) / acols * (asize.Y + spacing);
            }
        }

        if (!Cfg.Locked) DrawUnlockedHint();
    }

    /// <summary>A strip above the frames: the incoming raidwide, how far its cast is, and seconds left.</summary>
    private void DrawRaidwide(string name, float left, float progress, float width)
    {
        var start = ImGui.GetCursorScreenPos();
        var height = ImGui.GetTextLineHeight() + 4;
        ImGui.Dummy(new Vector2(width, height));
        var end = start + new Vector2(width, height);

        var dl = ImGui.GetWindowDrawList();
        var color = Cfg.RaidwideColor;
        var pulse = 0.7f + 0.3f * MathF.Sin(Environment.TickCount64 / 1000f * MathF.PI * 4f);
        dl.AddRectFilled(start, end, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.6f)), 3f);
        dl.AddRectFilled(start, new Vector2(start.X + width * Math.Clamp(progress, 0f, 1f), end.Y),
            ImGui.GetColorU32(color with { W = color.W * 0.55f }), 3f);
        dl.AddRect(start, end, ImGui.GetColorU32(color with { W = color.W * pulse }), 3f, ImDrawFlags.None, 1.5f);

        var text = $"RAIDWIDE  {name}  {left:0.0}s";
        var pos = start + new Vector2(6, 2);
        dl.AddText(pos + Vector2.One, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.9f)), text);
        dl.AddText(pos, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f)), text);
        ImGui.Dummy(new Vector2(0, 2));
    }

    // Room on the left of each party frame for the vertical aggro bar.
    // (bar width + gap + room for the red glow)
    private float BarSpace => Cfg.ShowAggro && Cfg.ShowEnmityGauge ? (Cfg.AggroBarWidth + 6f) * U : 0f;

    private static void DrawUnlockedHint()
    {
        var min = ImGui.GetWindowPos();
        var max = min + ImGui.GetWindowSize();
        var dl = ImGui.GetWindowDrawList();
        dl.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(1f, 0.8f, 0.2f, 0.12f)), 4f);
        dl.AddRect(min, max, ImGui.GetColorU32(new Vector4(1f, 0.8f, 0.2f, 1f)), 4f, ImDrawFlags.None, 2f);

        if (ImGui.IsWindowHovered())
            ImGui.SetTooltip("Unlocked: drag anywhere to move. Clicks don't cast.\nLock again with /kaifuku lock");
    }

    // ---------- data ----------

    private List<FrameData> GatherFrames(IPlayerCharacter player)
    {
        var frames = new List<FrameData>();
        var myId = player.EntityId;

        if (Plugin.PartyList.Length == 0)
        {
            // Duty Support and Trusts: the party list is empty, but the game's HUD knows the NPC party.
            var npcParty = GatherHudParty(player);
            if (npcParty.Count > 1) return npcParty;

            // Solo: just yourself.
            var raw = Dedupe(player.StatusList
                .Where(s => s != null && s.StatusId != 0)
                .Select(s => (Row: s.GameData.Value, s.SourceId, s.RemainingTime)));

            var self = new FrameData
            {
                Name = player.Name.TextValue, EntityId = myId,
                Hp = player.CurrentHp, MaxHp = player.MaxHp, Mp = player.CurrentMp, MaxMp = player.MaxMp,
                ShieldPct = player.ShieldPercentage, JobId = player.ClassJob.RowId,
                Role = (byte)(player.ClassJob.ValueNullable?.Role ?? 0), IsSelf = true,
                State = player.IsDead || player.CurrentHp == 0 ? MemberState.Dead : MemberState.Normal,
            };
            FillLive(self, player, raw, myId);
            frames.Add(self);
            return frames;
        }

        var inDuty = Plugin.Condition[ConditionFlag.BoundByDuty];
        var index = 0;
        foreach (var m in Plugin.PartyList)
            frames.Add(FromMember(m, player, inDuty, index++));
        return frames;
    }

    /// <summary>
    /// Your alliance's letter in a 24-player duty (A, B or C), or null when it can't be read.
    /// Taken from the game's own party list, whose title shows it ("Alliance C"), so it always matches what
    /// the game displays. (The cross-world group number used before didn't match the letter.)
    /// </summary>
    private static unsafe char? CurrentAllianceLetter()
    {
        if (!Plugin.PartyList.IsAlliance) return null;
        var manager = RaptureAtkUnitManager.Instance();
        if (manager == null) return null;
        var partyList = (AddonPartyList*)manager->GetAddonByName("_PartyList");
        if (partyList == null || partyList->PartyTypeTextNode == null) return null;

        var text = partyList->PartyTypeTextNode->NodeText.ToString().Trim();
        if (text.Length == 0) return null;
        var last = text[^1];
        return last is 'A' or 'B' or 'C' ? last : null;
    }

    private unsafe List<FrameData> GatherHudParty(IPlayerCharacter player)
    {
        var list = new List<FrameData>();
        var hud = AgentHUD.Instance();
        if (hud == null || hud->PartyMemberCount <= 1) return list;

        for (var i = 0; i < hud->PartyMemberCount; i++)
        {
            var id = hud->PartyMembers[i].EntityId;
            IBattleChara? chara = null;
            foreach (var obj in Plugin.ObjectTable)
            {
                if (obj.EntityId != id || obj is not IBattleChara bc) continue;
                chara = bc;
                break;
            }
            if (chara == null) continue;

            var raw = Dedupe(chara.StatusList
                .Where(s => s != null && s.StatusId != 0)
                .Select(s => (Row: s.GameData.Value, s.SourceId, s.RemainingTime)));
            var f = new FrameData
            {
                Name = chara.Name.TextValue, EntityId = id,
                Hp = chara.CurrentHp, MaxHp = chara.MaxHp, Mp = chara.CurrentMp, MaxMp = chara.MaxMp,
                ShieldPct = chara.ShieldPercentage, JobId = chara.ClassJob.RowId,
                Role = (byte)(chara.ClassJob.ValueNullable?.Role ?? 0), IsSelf = id == player.EntityId,
                State = chara.IsDead || chara.CurrentHp == 0 ? MemberState.Dead : MemberState.Normal,
                Order = i, InRange = InRangeOf(player, chara),
            };
            FillLive(f, chara, raw, player.EntityId);
            list.Add(f);
        }
        return list;
    }

    private List<List<FrameData>> GatherAlliance(IPlayerCharacter player)
    {
        var groups = new List<List<FrameData>>();
        if (!Cfg.ShowAlliance || !Plugin.PartyList.IsAlliance) return groups;

        var inDuty = Plugin.Condition[ConditionFlag.BoundByDuty];
        for (var g = 0; g < 2; g++)
        {
            var list = new List<FrameData>();
            for (var i = 0; i < 8; i++)
            {
                var address = Plugin.PartyList.GetAllianceMemberAddress(g * 8 + i);
                if (address == nint.Zero) continue;
                var m = Plugin.PartyList.CreateAllianceMemberReference(address);
                if (m == null || m.EntityId == 0 || m.Name.TextValue.Length == 0) continue;
                list.Add(FromMember(m, player, inDuty, i));
            }
            if (list.Count > 0) groups.Add(list);
        }
        return groups;
    }

    private FrameData FromMember(IPartyMember m, IPlayerCharacter player, bool inDuty, int index)
    {
        var myId = player.EntityId;
        var chara = m.GameObject as IBattleChara;

        // Read statuses from the member's character when it's here: that's the game's live list.
        // The party list's own copy can keep entries that were already replaced or expired, which
        // showed up as extra shield icons (e.g. several Galvanize after re-casting Adloquium).
        var statusSource = chara?.StatusList ?? m.Statuses;
        var raw = Dedupe(statusSource
            .Where(s => s != null && s.StatusId != 0)
            .Select(s => (Row: s.GameData.Value, s.SourceId, s.RemainingTime)));

        var sameZone = m.Territory.RowId == Plugin.ClientState.TerritoryType;

        // Dead only when their character is actually here and dead. Not here at all means
        // disconnected (no zone at all, or missing from your own duty) or simply away.
        MemberState state;
        if (chara != null)
            state = chara.IsDead || chara.CurrentHp == 0 ? MemberState.Dead : MemberState.Normal;
        else if (m.Territory.RowId == 0 || (sameZone && inDuty))
            state = MemberState.Disconnected;
        else
            state = MemberState.Away;

        var f = new FrameData
        {
            Name = m.Name.TextValue, EntityId = m.EntityId,
            // Live values from the character when it's nearby; the party list's copy can lag behind.
            Hp = chara?.CurrentHp ?? m.CurrentHP, MaxHp = chara?.MaxHp ?? m.MaxHP,
            Mp = chara?.CurrentMp ?? m.CurrentMP, MaxMp = chara?.MaxMp ?? m.MaxMP,
            ShieldPct = (byte)(chara?.ShieldPercentage ?? 0), JobId = m.ClassJob.RowId,
            Role = (byte)(m.ClassJob.ValueNullable?.Role ?? 0), IsSelf = m.EntityId == myId,
            State = state,
            Order = gameOrder.TryGetValue(m.EntityId, out var o) ? o : 100 + index,
            InRange = sameZone && chara != null && InRangeOf(player, chara),
        };
        if (state != MemberState.Disconnected) FillLive(f, chara, raw, myId);
        return f;
    }

    /// <summary>Everything that comes from the member's live state: statuses, aggro, raises, casts, markers.</summary>
    private void FillLive(FrameData f, IBattleChara? chara, List<(LuminaStatus Row, uint SourceId, float RemainingTime)> raw, uint myId)
    {
        f.Dispellable = raw.Any(s => s.Row.CanDispel);
        f.Statuses = BuildIcons(raw, myId);
        f.MustHeal = Cfg.ShowMustHeal && raw.Any(s => Cfg.MustHealStatuses.Contains(s.Row.RowId));
        f.HealIncoming = Cfg.ShowIncomingHeals && f.State != MemberState.Dead && healTargets.Contains(f.EntityId);
        f.Buster = IsBusterTarget(f.EntityId, f.Role);
        f.HasHot = HasMyHot(raw, myId);
        f.HasAggro = aggroHolder == f.EntityId;
        f.EnemiesOnMe = enemiesOn.GetValueOrDefault(f.EntityId);
        f.Enmity = EnmityOf(f.EntityId);
        f.RaiseIncoming = f.State == MemberState.Dead &&
                          (raiseTargets.ContainsKey(f.EntityId) || raw.Any(s => RaisePending.Contains(s.Row.RowId)));
        f.RaisedBy = raiseTargets.TryGetValue(f.EntityId, out var raiser) ? raiser : string.Empty;
        f.BusterLeft = busterTargets.TryGetValue(f.EntityId, out var busterLeft) ? busterLeft : 0f;
        foreach (var s in raw)
        {
            if (f.CriticalText.Length == 0 && Cfg.MustHealStatuses.Contains(s.Row.RowId))
            {
                f.CriticalText = s.Row.Name.ExtractText();
                f.CriticalSeconds = s.RemainingTime;
            }
            if (f.CleanseText.Length == 0 && s.Row.CanDispel) f.CleanseText = s.Row.Name.ExtractText();
        }

        // Pet jobs: is their fairy / carbuncle out? (only when we can actually see them, and they're alive)
        if (Cfg.ShowPetStatus && IsPetJob(f.JobId) && chara != null && f.State == MemberState.Normal)
        {
            f.Pet = petOwners.Contains(f.EntityId) ? PetState.Present
                : raw.Any(s => Dissipation.Contains(s.Row.RowId)) ? PetState.Dissipated
                : PetState.Missing;
        }

        if (Cfg.ShowPartnerMarker)
        {
            foreach (var s in raw)
            {
                if (!PartnerStatuses.Contains(s.Row.RowId) || !IsMine(s.SourceId, myId)) continue;
                f.PartnerIcon = s.Row.Icon;
                break;
            }
        }

        if (Cfg.ShowCastBars && CastOf(chara) is { } memberCast && memberCast.Total > 0)
        {
            f.CastProgress = Math.Clamp(memberCast.Current / memberCast.Total, 0f, 1f);
            f.CastName = ActionName(memberCast.ActionId);
        }
    }

    /// <summary>One entry per status and caster, keeping the freshest (longest remaining) one.</summary>
    private static List<(LuminaStatus Row, uint SourceId, float RemainingTime)> Dedupe(
        IEnumerable<(LuminaStatus Row, uint SourceId, float RemainingTime)> statuses) =>
        statuses
            .GroupBy(s => (s.Row.RowId, s.SourceId))
            .Select(g => g.OrderByDescending(s => s.RemainingTime).First())
            .ToList();

    private List<StatusIcon> BuildIcons(List<(LuminaStatus Row, uint SourceId, float RemainingTime)> raw, uint myId)
    {
        var icons = new List<StatusIcon>();
        foreach (var s in raw)
        {
            if (s.Row.Icon == 0) continue;
            var id = s.Row.RowId;
            if (Cfg.HiddenStatuses.Contains(id)) continue;

            var debuff = s.Row.StatusCategory == 2;
            var mustHeal = Cfg.ShowMustHeal && Cfg.MustHealStatuses.Contains(id);
            var tankCooldown = Cfg.ShowTankCooldowns && Cfg.TankCooldownStatuses.Contains(id);
            var show = mustHeal || tankCooldown || (debuff
                ? Cfg.ShowDebuffs
                : Cfg.ShowMyBuffs && (Cfg.BuffMode == BuffFilter.AllBuffs
                                      || IsMine(s.SourceId, myId)
                                      || (Cfg.BuffMode == BuffFilter.MineAndMyList && Cfg.AlwaysShowStatuses.Contains(id))));
            var priority = mustHeal ? 0 : debuff ? 1 : tankCooldown ? 2 : 3;
            if (show) icons.Add(new StatusIcon(s.Row.Icon, s.RemainingTime, debuff, priority));
        }

        // Heal-to-full first, then debuffs, then tank cooldowns, then other buffs.
        return icons.OrderBy(i => i.Priority).Take(Math.Max(0, Cfg.MaxStatusIcons)).ToList();
    }

    private enum FakeDebuff { None, Removable, NotRemovable }

    private static readonly (string Name, uint Job, byte Role, float Hp, byte Shield, FakeDebuff Debuff, bool InRange, MemberState State)[] PreviewData =
    [
        ("Tank Alpha", 19, 1, 0.92f, 0, FakeDebuff.None, true, MemberState.Normal),
        ("Tank Bravo", 21, 1, 0.64f, 10, FakeDebuff.NotRemovable, true, MemberState.Normal),
        ("Healer Charlie", 24, 4, 1.00f, 0, FakeDebuff.None, true, MemberState.Normal),
        ("Healer Delta", 28, 4, 0.81f, 0, FakeDebuff.Removable, true, MemberState.Normal),
        ("Melee Echo", 22, 2, 0.28f, 0, FakeDebuff.Removable, true, MemberState.Normal),
        ("Melee Foxtrot", 30, 2, 0.00f, 0, FakeDebuff.None, true, MemberState.Dead),
        ("Ranged Golf", 23, 3, 0.55f, 15, FakeDebuff.None, false, MemberState.Normal),
        ("Caster Hotel", 25, 3, 0.00f, 0, FakeDebuff.None, false, MemberState.Disconnected),
    ];

    // Preview party: who has your HoT, how many enemies are on each member, and their enmity.
    private static readonly bool[] PreviewHot = [true, true, true, false, true, false, false, false];
    private static readonly int[] PreviewEnemies = [3, 1, 0, 0, 1, 0, 0, 0];
    private static readonly int[] PreviewEnmity = [100, 86, 41, 38, 63, -1, 52, -1];

    private static (uint Removable, uint NotRemovable)? previewIcons;

    /// <summary>Icons of a real removable debuff and a real non-removable one, taken from the game data once.</summary>
    private static (uint Removable, uint NotRemovable) PreviewStatusIcons()
    {
        if (previewIcons is { } cached) return cached;

        uint removable = 0, notRemovable = 0;
        foreach (var status in Plugin.DataManager.GetExcelSheet<LuminaStatus>())
        {
            if (status.Icon == 0 || status.StatusCategory != 2 || status.Name.ExtractText().Length == 0) continue;
            if (status.CanDispel) { if (removable == 0) removable = status.Icon; }
            else if (notRemovable == 0) notRemovable = status.Icon;
            if (removable != 0 && notRemovable != 0) break;
        }

        previewIcons = (removable, notRemovable);
        return previewIcons.Value;
    }

    private List<FrameData> PreviewFrames()
    {
        var (removableIcon, notRemovableIcon) = PreviewStatusIcons();
        var remaining = 20f - Environment.TickCount64 / 1000f % 20f; // fake timer counting down 20 -> 0, looping
        var cast = Environment.TickCount64 / 1000f % 2.5f / 2.5f;    // fake 2.5 s cast, looping
        var partnerIcon = PartnerStatuses.Count > 0 ? StatusCatalog.Get(PartnerStatuses.First())?.Icon ?? 0u : 0u;
        var mustHealIcon = Cfg.MustHealStatuses.Count > 0 ? StatusCatalog.Get(Cfg.MustHealStatuses.Last())?.Icon ?? 0u : 0u;

        return PreviewData
            .Select((p, i) =>
            {
                var icons = new List<StatusIcon>();
                var icon = p.Debuff switch
                {
                    FakeDebuff.Removable => removableIcon,
                    FakeDebuff.NotRemovable => notRemovableIcon,
                    _ => 0u,
                };
                if (icon != 0 && Cfg.ShowDebuffs && Cfg.MaxStatusIcons > 0)
                    icons.Add(new StatusIcon(icon, remaining, true, 1));
                if (i == 1 && Cfg.ShowMustHeal && mustHealIcon != 0) // Tank Bravo: Walking Dead
                    icons.Insert(0, new StatusIcon(mustHealIcon, 8f - Environment.TickCount64 / 1000f % 8f, false, 0));

                return new FrameData
                {
                    Name = p.Name, Hp = (uint)(p.Hp * 100000), MaxHp = 100000, Mp = 7000, MaxMp = 10000,
                    ShieldPct = p.Shield, JobId = p.Job, Role = p.Role, InRange = p.InRange,
                    Dispellable = p.Debuff == FakeDebuff.Removable, Statuses = icons, Order = i, IsSelf = i == 2,
                    State = p.State,
                    Buster = Cfg.TankBusterWarning && i == 0,
                    HasHot = Cfg.ShowHotIndicator && PreviewHot[i],
                    HasAggro = Cfg.ShowAggro && i == 0,
                    EnemiesOnMe = Cfg.ShowAggro ? PreviewEnemies[i] : 0,
                    Enmity = Cfg.ShowAggro ? PreviewEnmity[i] : -1,
                    RaiseIncoming = p.State == MemberState.Dead,               // Melee Foxtrot: raise on the way
                    MustHeal = Cfg.ShowMustHeal && i == 1,                     // Tank Bravo: heal to full
                    HealIncoming = Cfg.ShowIncomingHeals && i == 6,            // Ranged Golf: a heal is coming
                    CastProgress = Cfg.ShowCastBars && i == 3 ? cast : -1f,    // Healer Delta is casting
                    CastName = "Adloquium",
                    PartnerIcon = Cfg.ShowPartnerMarker && i == 0 ? partnerIcon : 0u, // your Kardia on Tank Alpha
                    Pet = Cfg.ShowPetStatus && p.Job == SCH ? PetState.Missing : PetState.None, // Healer Delta: no fairy
                    BusterLeft = 3.1f, CriticalText = "Doom", CriticalSeconds = 6f,
                    CleanseText = p.Debuff == FakeDebuff.Removable ? "Paralysis" : string.Empty,
                    RaisedBy = p.State == MemberState.Dead ? "Healer Charlie" : string.Empty,
                };
            })
            .ToList();
    }

    private static readonly string[] PreviewAllianceNames =
        ["Ada", "Bram", "Cora", "Dain", "Esme", "Finn", "Gale", "Hugo", "Iris", "Jory", "Kael", "Lyra", "Milo", "Nell", "Otto", "Pia"];

    private List<List<FrameData>> PreviewAlliance()
    {
        var groups = new List<List<FrameData>>();
        if (!Cfg.ShowAlliance) return groups;
        uint[] jobs = [19, 24, 22, 23, 21, 28, 30, 25];
        byte[] roles = [1, 4, 2, 3, 1, 4, 2, 3];
        for (var g = 0; g < 2; g++)
        {
            var list = new List<FrameData>();
            for (var i = 0; i < 8; i++)
            {
                var n = g * 8 + i;
                var hp = n == 5 ? 0f : 0.35f + (n * 37 % 60) / 100f;
                list.Add(new FrameData
                {
                    Name = PreviewAllianceNames[n], Hp = (uint)(hp * 100000), MaxHp = 100000, Mp = 8000, MaxMp = 10000,
                    JobId = jobs[i], Role = roles[i], Order = i, InRange = n % 7 != 6,
                    State = n == 5 ? MemberState.Dead : MemberState.Normal,
                });
            }
            groups.Add(list);
        }
        return groups;
    }

    private List<List<FrameData>> Arrange(List<FrameData> frames)
    {
        IEnumerable<FrameData> ordered = Cfg.Sort switch
        {
            SortMode.Role => frames.OrderBy(f => RoleRank(f.Role)).ThenBy(f => f.Name),
            SortMode.Name => frames.OrderBy(f => f.Name),
            _ => frames.OrderBy(f => f.Order),
        };

        if (Cfg.SelfFirst) ordered = ordered.OrderByDescending(f => f.IsSelf); // stable: keeps the order above

        if (!Cfg.GroupByRole) return [ordered.ToList()];

        return ordered
            .GroupBy(f => RoleGroup(f.Role))
            .OrderBy(g => g.Key)
            .Select(g => g.ToList())
            .ToList();
    }

    // ClassJob.Role: 1 = tank, 2 = melee, 3 = ranged, 4 = healer
    private static int RoleRank(byte role) => role switch { 1 => 0, 4 => 1, 2 => 2, 3 => 3, _ => 4 };
    private static int RoleGroup(byte role) => role switch { 1 => 0, 4 => 1, _ => 2 };

    // ---------- drawing ----------

    private void DrawFrame(FrameData f, uint job, Vector2 start, Vector2 size, StyleSpec spec, bool compact = false, int group = 0)
    {
        var cfg = Cfg;
        var end = start + size;

        bool hovered;
        if (cfg.Locked)
        {
            // Locked: the frame is a clickable button.
            ImGui.InvisibleButton($"##frame{group}_{f.EntityId}_{f.Order}", size,
                ImGuiButtonFlags.MouseButtonLeft | ImGuiButtonFlags.MouseButtonRight | ImGuiButtonFlags.MouseButtonMiddle);
            hovered = ImGui.IsItemHovered();
        }
        else
        {
            // Unlocked: a non-interactive placeholder, so a click anywhere on the frame drags the window
            // instead of casting.
            ImGui.Dummy(size);
            hovered = false;
        }

        var dead = f.State == MemberState.Dead;
        var offline = f.State == MemberState.Disconnected;
        var unavailable = offline || f.State == MemberState.Away;

        // One click = one action on this member. Nothing is ever cast without a click,
        // and nothing at all on someone who isn't here (no pointless raises on disconnected players).
        if (hovered && f.EntityId != 0 && !unavailable)
        {
            var io = ImGui.GetIO();
            var mod = Bindings.ModifierFrom(io.KeyShift, io.KeyCtrl, io.KeyAlt);
            foreach (var button in Enum.GetValues<MouseButton>())
            {
                if (!ImGui.IsMouseClicked((ImGuiMouseButton)(int)button)) continue;
                var bound = Bindings.Resolve(job, mod, button, cfg);
                // Someone is already raising them: don't waste a raise (or a Swiftcast) on it.
                if (f.RaiseIncoming && cfg.BlockDoubleRaise && IsRaiseBinding(bound)) continue;
                Casting.Cast(bound, f.EntityId, cfg.MacroAlsoTarget);
            }
        }

        var skinSpec = Skins.Get(cfg.Skin);
        if (skinSpec != null)
        {
            DrawSkinnedFrame(f, start, size, skinSpec, compact, hovered);
            return;
        }

        var alpha = f.InRange && !unavailable ? 1f : cfg.OutOfRangeAlpha;
        var frac = offline ? 1f : f.MaxHp == 0 ? 0f : Math.Clamp((float)f.Hp / f.MaxHp, 0f, 1f);
        var mpHeight = cfg.ShowMpBar ? Math.Min(cfg.MpBarHeight, size.Y / 3) : 0f;
        var hpBottom = end.Y - mpHeight;
        var hpAreaHeight = hpBottom - start.Y;
        var dl = ImGui.GetWindowDrawList();

        // Background
        dl.AddRectFilled(start, end, Col(cfg.BackgroundColor, alpha), cfg.Rounding);

        // Health
        var dispelBar = !offline && f.Dispellable && cfg.DispelHighlight != DispelStyle.BorderOnly;
        var dispelBorder = !offline && f.Dispellable && cfg.DispelHighlight != DispelStyle.BarColor;

        var raising = dead && f.RaiseIncoming;
        var hpColor = offline ? cfg.DisconnectedColor
            : raising ? cfg.RaiseColor
            : dead ? cfg.DeadColor
            : dispelBar ? cfg.DispelColor
            : cfg.ColorMode == HpColorMode.ByRole ? RoleColor(f.Role)
            : frac < cfg.LowHpThreshold ? cfg.LowHpColor
            : frac < cfg.MidHpThreshold ? cfg.MidHpColor
            : cfg.HighHpColor;
        var hpWidth = raising ? size.X : size.X * frac;
        if (hpWidth > 0)
            StyleDraw.Bar(dl, start, new Vector2(start.X + hpWidth, hpBottom), Col(hpColor, raising ? alpha * 0.6f : alpha), cfg.Rounding, spec, alpha);

        // A heal is being cast on them by someone else: a soft pulsing glow over the missing health.
        if (f.HealIncoming && !dead && !unavailable && hpWidth < size.X)
        {
            var glow = 0.6f + 0.4f * MathF.Sin(Environment.TickCount64 / 1000f * MathF.PI * 3f);
            dl.AddRectFilled(new Vector2(start.X + hpWidth, start.Y), new Vector2(end.X, hpBottom),
                Col(cfg.IncomingHealColor with { W = cfg.IncomingHealColor.W * glow }, alpha), cfg.Rounding);
        }

        // Shield: a strip along the bottom of the health area, after the HP (or from the right edge if HP is full)
        if (cfg.ShowShields && f.ShieldPct > 0 && !dead && !unavailable)
        {
            var shieldWidth = Math.Min(size.X, size.X * f.ShieldPct / 100f);
            var sx = Math.Min(start.X + hpWidth, end.X - shieldWidth);
            dl.AddRectFilled(new Vector2(sx, hpBottom - hpAreaHeight * 0.3f), new Vector2(sx + shieldWidth, hpBottom),
                Col(cfg.ShieldColor, alpha));
        }

        // MP
        if (mpHeight > 0 && f.MaxMp > 0 && !offline)
        {
            var mpFrac = Math.Clamp((float)f.Mp / f.MaxMp, 0f, 1f);
            dl.AddRectFilled(new Vector2(start.X, end.Y - mpHeight), new Vector2(start.X + size.X * mpFrac, end.Y),
                Col(cfg.MpColor, alpha));
        }

        // Aggro bar: vertical, just left of the frame, filling from the bottom with enmity on your target.
        if (!compact && cfg.ShowAggro && cfg.ShowEnmityGauge)
        {
            var barWidth = cfg.AggroBarWidth;
            var barMin = new Vector2(start.X - barWidth - 2, start.Y);
            var barMax = new Vector2(start.X - 2, end.Y);
            var round = MathF.Min(3f, barWidth / 2);

            var level = f.Enmity >= 0 ? Math.Clamp(f.Enmity / 100f, 0f, 1f) : f.HasAggro ? 1f : 0f;
            var maxed = !unavailable && (f.HasAggro || f.Enmity >= 100);

            // Maxed (this member has aggro): a soft red glow around the bar, gently pulsing.
            if (maxed)
            {
                var pulse = 0.75f + 0.25f * MathF.Sin(Environment.TickCount64 / 1000f * MathF.PI * 2f);
                for (var i = 3; i >= 1; i--)
                {
                    var grow = new Vector2(i * 1.5f);
                    dl.AddRectFilled(barMin - grow, barMax + grow, Col(cfg.AggroColor with { W = 0.16f * pulse / i * 2f }, alpha), round + i * 1.5f);
                }
            }

            dl.AddRectFilled(barMin, barMax, Col(new Vector4(0f, 0f, 0f, 0.5f), alpha), round);

            if (level > 0 && !unavailable)
            {
                var barColor = maxed ? cfg.AggroColor
                    : f.Enmity >= 80 ? new Vector4(0.95f, 0.62f, 0.2f, 1f)
                    : new Vector4(0.85f, 0.85f, 0.85f, 0.8f);
                var fillTop = new Vector2(barMin.X, barMax.Y - (barMax.Y - barMin.Y) * level);
                dl.AddRectFilled(fillTop, barMax, Col(barColor, alpha), round);
                if (maxed) // bright core so the full bar reads as "lit"
                    dl.AddRectFilled(fillTop + new Vector2(barWidth * 0.3f, 2), barMax - new Vector2(barWidth * 0.3f, 2),
                        Col(new Vector4(1f, 0.75f, 0.7f, 0.55f), alpha), round);
            }
        }

        // Cast bar: a thin strip along the bottom of the health area.
        if (f.CastProgress >= 0 && !unavailable)
        {
            dl.AddRectFilled(new Vector2(start.X, hpBottom - 3), new Vector2(start.X + size.X * f.CastProgress, hpBottom),
                Col(cfg.CastColor, alpha));
        }

        // Job icon
        var textX = start.X + 6;
        if (cfg.ShowRoleIcon && f.JobId != 0 && hpAreaHeight > 8)
        {
            var iconSize = hpAreaHeight - 4;
            var p = start + new Vector2(3, 2);
            dl.AddImage(Icons.Texture(JobIconBase + f.JobId), p, p + new Vector2(iconSize, iconSize),
                Vector2.Zero, Vector2.One, Col(Vector4.One, alpha));
            textX = p.X + iconSize + 4;
            DrawPetMarker(dl, f, p + new Vector2(iconSize - 2, 2), MathF.Min(6f, iconSize / 3), alpha);
        }

        // Your Kardia / Dance Partner: that status's own icon, tucked at the job icon's corner.
        if (f.PartnerIcon != 0 && !unavailable)
        {
            var ph = Math.Min(14f, hpAreaHeight * 0.55f);
            var pp = new Vector2(Math.Max(start.X + 1, textX - ph * 0.75f - 2), hpBottom - ph - 1);
            dl.AddImage(Icons.Texture(f.PartnerIcon), pp, pp + new Vector2(ph * 0.75f, ph), Vector2.Zero, Vector2.One, Col(Vector4.One, alpha));
        }

        // Status icons, right-aligned
        if (f.Statuses.Count > 0 && !compact)
        {
            var h = Math.Min(cfg.StatusIconSize * U, hpAreaHeight - 2);
            var w = h * 0.75f; // status icons are taller than wide
            var timerSize = ImGui.GetFontSize() * 0.6f;
            var right = end.X - 2;
            foreach (var s in f.Statuses)
            {
                var p0 = new Vector2(right - w, start.Y + 1);
                dl.AddImage(Icons.Texture(s.Icon), p0, p0 + new Vector2(w, h),
                    Vector2.Zero, Vector2.One, Col(Vector4.One, alpha));
                if (cfg.ShowStatusTimers && s.Remaining > 0 && s.Remaining < 100)
                    dl.AddText(ImGui.GetFont(), timerSize, p0 + new Vector2(1, h - timerSize),
                        Col(cfg.TextColor, alpha), $"{s.Remaining:0}");
                right -= w + 1;
            }
        }

        // Text
        var label = BuildLabel(f, frac, false, compact);
        if (label.Length > 0)
        {
            var fontSize = ImGui.GetFontSize() * cfg.FontScale * FontU;
            if (offline && ImGui.CalcTextSize(label).X * cfg.FontScale > end.X - textX - 4)
                label = BuildLabel(f, frac, true, compact); // "D/C" when "DISCONNECTED" doesn't fit
            var textPos = new Vector2(textX, start.Y + (hpAreaHeight - fontSize) / 2);

            var textColor = cfg.TextColor;
            var shadowColor = spec.TextShadow;
            var shadowOffset = spec.TextShadowOffset;
            var outline = false;
            if (cfg.AutoTextContrast)
            {
                // What the text actually sits on: the bar if it covers most of the label, else the empty track.
                var labelWidth = ImGui.CalcTextSize(label).X * cfg.FontScale;
                var onBar = hpWidth > (textX - start.X) + labelWidth * 0.5f;
                var track = Over(cfg.BackgroundColor, new Vector3(0.12f));
                var under = onBar ? Over(hpColor, track) : track;
                if (onBar && spec.Gloss > 0) under = Vector3.Lerp(under, Vector3.One, spec.Gloss * 0.5f);

                var lUnder = Luminance(under);
                var lText = Luminance(new Vector3(textColor.X, textColor.Y, textColor.Z));
                var normal = Contrast(lUnder, lText);
                var dark = Contrast(lUnder, Luminance(DarkText));

                if (normal >= 4.5f)
                {
                    // Readable as it is.
                }
                else if (dark >= 4.5f && dark > normal * 1.25f)
                {
                    // Clearly light background (pale green, yellow...): dark text, no shadow.
                    textColor = new Vector4(DarkText, 1f);
                    shadowColor = default;
                }
                else
                {
                    // Mid-tone background (purple, magenta, mid green...) where neither black nor white
                    // reads well on its own: keep the text color and give it a full outline instead.
                    outline = true;
                    shadowColor = lText > 0.5f ? new Vector4(0f, 0f, 0f, 0.9f) : new Vector4(1f, 1f, 1f, 0.75f);
                }
            }

            var font = ImGui.GetFont();
            if (outline)
            {
                foreach (var o in OutlineOffsets)
                    dl.AddText(font, fontSize, textPos + o, Col(shadowColor, alpha), label);
            }
            else if (cfg.TextShadow && shadowColor.W > 0) // keeps text readable on any bar color
            {
                dl.AddText(font, fontSize, textPos + shadowOffset, Col(shadowColor, alpha), label);
            }
            dl.AddText(font, fontSize, textPos, Col(textColor, alpha), label);
        }

        if (!compact && cfg.ShowCastName && f.CastProgress >= 0 && f.CastName.Length > 0 && !unavailable)
        {
            var castSize = ImGui.GetFontSize() * 0.62f;
            var castWidth = ImGui.CalcTextSize(f.CastName).X * 0.62f;
            var castPos = new Vector2(end.X - castWidth - 3, hpBottom - 4 - castSize);
            var castFont = ImGui.GetFont();
            dl.AddText(castFont, castSize, castPos + Vector2.One, Col(new Vector4(0f, 0f, 0f, 0.9f), alpha), f.CastName);
            dl.AddText(castFont, castSize, castPos, Col(cfg.CastColor, alpha), f.CastName);
        }

        // Borders
        if (spec.FrameBorder.W > 0 && !dispelBorder)
            dl.AddRect(start, end, Col(spec.FrameBorder, alpha), cfg.Rounding, ImDrawFlags.None, 1f);
        if (dispelBorder)
            dl.AddRect(start, end, Col(cfg.DispelColor, 1f), cfg.Rounding, ImDrawFlags.None, 3f);
        if (hovered)
            dl.AddRect(start, end, Col(cfg.HoverColor, 1f), cfg.Rounding, ImDrawFlags.None, 1.5f);

        // Aggro badge (top-left corner) and the number of enemies attacking this member.
        if (cfg.ShowAggro && !unavailable)
        {
            var badgeCenter = start + new Vector2(7, 7);
            var font = ImGui.GetFont();
            var small = ImGui.GetFontSize() * 0.7f;
            if (f.HasAggro && cfg.ShowAggroBadge)
            {
                dl.AddCircleFilled(badgeCenter, 6.5f, Col(cfg.AggroColor, 1f));
                dl.AddCircle(badgeCenter, 6.5f, Col(new Vector4(0f, 0f, 0f, 0.8f), 1f), 0, 1f);
                dl.AddText(font, small, badgeCenter - new Vector2(small * 0.3f, small * 0.5f), Col(Vector4.One, 1f), "A");
            }
            if (!compact && cfg.ShowEnemyCount && f.EnemiesOnMe > 0 && !(f.HasAggro && f.EnemiesOnMe == 1))
            {
                var text = f.EnemiesOnMe.ToString();
                var w = ImGui.CalcTextSize(text).X * 0.7f + 6;
                var p0 = new Vector2(f.HasAggro && cfg.ShowAggroBadge ? badgeCenter.X + 8 : start.X + 1, start.Y + 1);
                dl.AddRectFilled(p0, p0 + new Vector2(w, 12), Col(new Vector4(0f, 0f, 0f, 0.75f), 1f), 3f);
                dl.AddRect(p0, p0 + new Vector2(w, 12), Col(f.HasAggro ? cfg.AggroColor : new Vector4(0.95f, 0.62f, 0.2f, 1f), 1f), 3f);
                dl.AddText(font, small, p0 + new Vector2(3, (12 - small) / 2), Col(Vector4.One, 1f), text);
            }
        }

        // Heal to full (Doom, Walking Dead...): a fast-pulsing border in the must-heal color.
        if (f.MustHeal && !dead && !unavailable)
        {
            var beat = 0.5f + 0.5f * MathF.Sin(Environment.TickCount64 / 1000f * MathF.PI * 6f);
            dl.AddRect(start, end, Col(cfg.MustHealColor with { W = 0.55f + 0.45f * beat }, 1f), cfg.Rounding, ImDrawFlags.None, 3f);
        }

        if (f.Buster)
        {
            // Pulses twice a second; drawn just outside the frame so it never hides the bar.
            var pulse = 0.55f + 0.45f * MathF.Sin(Environment.TickCount64 / 1000f * MathF.PI * 4f);
            var grow = Vector2.One;
            dl.AddRect(start - grow, end + grow, Col(cfg.BusterColor, pulse), cfg.Rounding + 1f, ImDrawFlags.None, 3f);
        }
    }

    private string BuildLabel(FrameData f, float frac, bool shortStatus, bool compact = false)
    {
        var name = compact ? f.Name.Split(' ')[0] : Cfg.Names switch
        {
            NameMode.FirstName => f.Name.Split(' ')[0],
            NameMode.Initials => string.Concat(f.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(p => $"{p[0]}.")),
            NameMode.None => string.Empty,
            _ => f.Name,
        };

        var hp = f.State switch
        {
            MemberState.Dead => f.RaiseIncoming ? "RAISING" : "DEAD",
            MemberState.Disconnected => shortStatus ? "D/C" : "DISCONNECTED",
            MemberState.Away when f.Hp == 0 || f.MaxHp == 0 => "AWAY",
            _ when f.MustHeal => $"HEAL {frac * 100:0}%",
            _ when f.Pet == PetState.Missing && !compact => $"{HpText(f, frac)}  NO {PetName(f.JobId).ToUpperInvariant()}",
            _ => compact ? $"{frac * 100:0}%" : HpText(f, frac),
        };

        if (name.Length == 0) return hp;
        if (hp.Length == 0) return name;
        return $"{name}  {hp}";
    }

    private string HpText(FrameData f, float frac)
    {
        return Cfg.HpText switch
        {
            HpTextMode.Percent => $"{frac * 100:0}%",
            HpTextMode.Current => f.Hp.ToString("N0"),
            HpTextMode.Missing => f.MaxHp > f.Hp ? $"-{f.MaxHp - f.Hp:N0}" : string.Empty,
            _ => string.Empty,
        };
    }

    private Vector4 RoleColor(byte role) => role switch
    {
        1 => Cfg.TankColor,
        4 => Cfg.HealerColor,
        _ => Cfg.DpsColor,
    };

    private static uint Col(Vector4 color, float alpha) => ImGui.GetColorU32(color with { W = color.W * alpha });

    private static readonly Vector3 DarkText = new(0.07f, 0.07f, 0.07f);

    private static readonly Vector2[] OutlineOffsets =
    [
        new(-1, 0), new(1, 0), new(0, -1), new(0, 1),
        new(-1, -1), new(1, -1), new(-1, 1), new(1, 1),
    ];

    // A color with alpha laid over an opaque one.
    private static Vector3 Over(Vector4 top, Vector3 bottom) =>
        new Vector3(top.X, top.Y, top.Z) * top.W + bottom * (1f - top.W);

    // WCAG relative luminance and contrast ratio.
    private static float Luminance(Vector3 c)
    {
        static float Lin(float v) => v <= 0.03928f ? v / 12.92f : MathF.Pow((v + 0.055f) / 1.055f, 2.4f);
        return 0.2126f * Lin(c.X) + 0.7152f * Lin(c.Y) + 0.0722f * Lin(c.Z);
    }

    private static float Contrast(float a, float b) => (MathF.Max(a, b) + 0.05f) / (MathF.Min(a, b) + 0.05f);

    private static unsafe bool TryGetPartyListAnchor(out Vector2 pos)
    {
        pos = default;
        var manager = RaptureAtkUnitManager.Instance();
        if (manager == null) return false;

        var addon = manager->GetAddonByName("_PartyList");
        if (addon == null || !addon->IsVisible || addon->RootNode == null) return false;

        pos = new Vector2(addon->X + addon->RootNode->Width * addon->Scale + 8, addon->Y);
        return true;
    }
}
