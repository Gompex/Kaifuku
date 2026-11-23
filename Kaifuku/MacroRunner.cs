using System;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;

namespace Kaifuku;

/// <summary>
/// Runs the player's own game macros from a frame click, exactly like pressing the macro on a hotbar,
/// with the clicked member as the mouseover target (&lt;mo&gt;) for as long as the macro runs.
/// A macro binding is stored as an "action ID" with the top bit set: flag | set &lt;&lt; 8 | index.
/// </summary>
internal static unsafe class MacroRunner
{
    private const uint MacroFlag = 0x8000_0000;
    private const long MaxHoldMs = 15_000;

    private static uint holdEntity;
    private static long startedAt;
    private static bool sawRunning;

    public static bool IsMacro(uint id) => (id & MacroFlag) != 0;
    public static uint Make(uint set, uint index) => MacroFlag | (set << 8) | index;
    private static (uint Set, uint Index) Split(uint id) => ((id >> 8) & 0xFF, id & 0xFF);

    private static RaptureMacroModule.Macro* Get(uint id)
    {
        var (set, index) = Split(id);
        if (set > 1 || index > 99) return null;
        var module = RaptureMacroModule.Instance();
        return module == null ? null : module->GetMacro(set, index);
    }

    private static bool IsEmpty(RaptureMacroModule.Macro* m) =>
        m == null || (m->Name.ToString().Length == 0 && m->Lines[0].ToString().Length == 0);

    public static bool Exists(uint id) => !IsEmpty(Get(id));

    public static uint Icon(uint id)
    {
        var m = Get(id);
        return IsEmpty(m) ? 0 : m->IconId;
    }

    public static string ShortName(uint id)
    {
        var m = Get(id);
        if (IsEmpty(m)) return "(empty)";
        var name = m->Name.ToString();
        return name.Length > 0 ? name : "(no name)";
    }

    public static string Name(uint id)
    {
        var (set, index) = Split(id);
        return $"{(set == 0 ? "Individual" : "Shared")} macro {index}: {ShortName(id)}";
    }

    private static IGameObject? Find(uint entityId)
    {
        foreach (var obj in Plugin.ObjectTable)
            if (obj.EntityId == entityId) return obj;
        return null;
    }

    private static void SetMouseOver(IGameObject? target)
    {
        var pronouns = PronounModule.Instance();
        if (pronouns != null) pronouns->UiMouseOverTarget = target == null ? null : (GameObject*)target.Address;
    }

    public static void Run(uint id, uint entityId, bool alsoTarget)
    {
        var shell = RaptureShellModule.Instance();
        var macro = Get(id);
        if (shell == null || IsEmpty(macro)) return;

        var target = Find(entityId);
        if (target == null) return;

        if (alsoTarget) Plugin.TargetManager.Target = target; // for macros written with <t>

        holdEntity = entityId;
        startedAt = Environment.TickCount64;
        sawRunning = false;
        SetMouseOver(target);
        shell->ExecuteMacro(macro);
    }

    /// <summary>Every game frame: keep the clicked member as &lt;mo&gt; while the macro is still running.</summary>
    public static void Update(IFramework framework)
    {
        if (holdEntity == 0) return;

        var shell = RaptureShellModule.Instance();
        var running = shell != null && shell->MacroCurrentLine >= 0;
        if (running) sawRunning = true;

        var elapsed = Environment.TickCount64 - startedAt;
        var finished = !running && (sawRunning || elapsed > 500);
        var target = Find(holdEntity);
        if (finished || elapsed > MaxHoldMs || target == null)
        {
            Release(target);
            return;
        }

        SetMouseOver(target);
    }

    private static void Release(IGameObject? target)
    {
        var pronouns = PronounModule.Instance();
        if (pronouns != null && target != null && pronouns->UiMouseOverTarget == (GameObject*)target.Address)
            pronouns->UiMouseOverTarget = null;
        holdEntity = 0;
    }
}
