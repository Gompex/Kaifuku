using System;
using System.Linq;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace Kaifuku;

public static unsafe class Casting
{
    public const uint Eukrasia = 24290, Diagnosis = 24284, EukrasianDiagnosis = 24291;
    public const uint Swiftcast = 7561;
    private const uint SwiftcastStatus = 167;

    /// <summary>"Swiftcast + raise" bindings are stored as the raise's ID with this bit set.</summary>
    public const uint SwiftFlag = 0x4000_0000;

    public static uint SwiftRaise(uint raiseId) => SwiftFlag | raiseId;
    public static bool IsSwiftRaise(uint id) => (id & SwiftFlag) != 0 && !MacroRunner.IsMacro(id);
    public static uint RaiseOf(uint id) => id & ~SwiftFlag;

    // The raise waiting to go out right after Swiftcast, like the second line of a macro.
    private static uint pendingRaise;
    private static uint pendingTarget;
    private static long pendingUntil;
    private static long pendingSince;
    private static bool sawSwift;

    /// <summary>
    /// Uses the bound action on the clicked member, the same as pressing it on a hotbar with that member
    /// targeted. The game itself handles range, cooldown and queueing.
    /// </summary>
    public static void Cast(uint actionId, uint entityId, bool macroAlsoTarget = false)
    {
        if (actionId == 0 || entityId == 0) return;

        if (MacroRunner.IsMacro(actionId))
        {
            MacroRunner.Run(actionId, entityId, macroAlsoTarget);
            return;
        }
        var am = ActionManager.Instance();
        if (am == null) return;

        if (IsSwiftRaise(actionId))
        {
            CastSwiftRaise(am, RaiseOf(actionId), entityId);
            return;
        }

        if (actionId == EukrasianDiagnosis)
        {
            // Sage's shield needs Eukrasia first, exactly like on the hotbar. Still one action per click:
            // if Eukrasia is already active (Diagnosis has turned into Eukrasian Diagnosis), this click
            // casts the shield on this member; otherwise this click casts Eukrasia.
            if (am->GetAdjustedActionId(Diagnosis) == EukrasianDiagnosis)
                am->UseAction(ActionType.Action, Diagnosis, entityId);
            else
                am->UseAction(ActionType.Action, Eukrasia);
            return;
        }

        am->UseAction(ActionType.Action, actionId, entityId);
    }

    /// <summary>
    /// The classic raise macro (/ac Swiftcast, then /ac Raise &lt;mo&gt;), done in one click:
    /// Swiftcast first if it's ready, then the raise on the clicked member. If Swiftcast is on cooldown
    /// (or already active), the raise goes out on its own. Swiftcast is never spent when the raise
    /// couldn't land anyway (member alive, out of range, not enough MP...).
    /// </summary>
    private static void CastSwiftRaise(ActionManager* am, uint raiseId, uint entityId)
    {
        // Can the raise land on this member at all? Ignore the GCD/cast state, only check target, range, MP.
        if (am->GetActionStatus(ActionType.Action, raiseId, entityId, false, false) != 0) return;

        var swiftReady = am->GetActionStatus(ActionType.Action, Swiftcast) == 0;
        if (swiftReady && am->UseAction(ActionType.Action, Swiftcast))
        {
            pendingRaise = raiseId;
            pendingTarget = entityId;
            pendingSince = Environment.TickCount64;
            pendingUntil = pendingSince + 1500;
            sawSwift = false;
            return;
        }

        am->UseAction(ActionType.Action, raiseId, entityId);
    }

    /// <summary>Every game frame: send the pending raise as soon as the game accepts it after Swiftcast.</summary>
    public static void Update(IFramework framework)
    {
        if (pendingRaise == 0) return;

        var am = ActionManager.Instance();
        if (am == null || Environment.TickCount64 > pendingUntil)
        {
            pendingRaise = 0;
            return;
        }

        // Wait for Swiftcast to land (or ~0.8 s), and give up if it got used on something else.
        var player = Plugin.ObjectTable.LocalPlayer;
        var hasSwift = player != null && player.StatusList.Any(s => s != null && s.StatusId == SwiftcastStatus);
        if (hasSwift) sawSwift = true;
        else if (sawSwift) { pendingRaise = 0; return; }
        if (!hasSwift && Environment.TickCount64 - pendingSince < 800) return;

        // Someone else raised them meanwhile, or they're out of range now: don't send it.
        if (am->GetActionStatus(ActionType.Action, pendingRaise, pendingTarget, false, false) != 0)
        {
            pendingRaise = 0;
            return;
        }

        if (am->UseAction(ActionType.Action, pendingRaise, pendingTarget))
            pendingRaise = 0;
    }
}
