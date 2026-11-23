using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using FFXIVClientStructs.FFXIV.Client.Game;
using LuminaAction = Lumina.Excel.Sheets.Action;

namespace Kaifuku;

internal static unsafe class Icons
{
    public static ImTextureID Texture(uint iconId) =>
        Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(iconId)).GetWrapOrEmpty().Handle;

    public static uint ActionIcon(uint actionId)
    {
        if (actionId == 0) return 0;
        if (MacroRunner.IsMacro(actionId)) return MacroRunner.Icon(actionId);
        if (Casting.IsSwiftRaise(actionId)) actionId = Casting.RaiseOf(actionId);
        actionId = Adjusted(actionId); // e.g. Play II shows the card you've drawn
        var row = Plugin.DataManager.GetExcelSheet<LuminaAction>().GetRowOrDefault(actionId);
        return row == null ? 0u : row.Value.Icon;
    }

    public static void DrawAction(uint actionId, float size)
    {
        var icon = ActionIcon(actionId);
        if (icon == 0)
        {
            ImGui.Dummy(new Vector2(size));
            return;
        }
        ImGui.Image(Texture(icon), new Vector2(size));
    }

    /// <summary>What a button currently turns into, the way the hotbar shows it (Play I -> The Balance, etc.).</summary>
    public static uint Adjusted(uint actionId)
    {
        var am = ActionManager.Instance();
        if (am == null || actionId == 0 || MacroRunner.IsMacro(actionId) || Casting.IsSwiftRaise(actionId)) return actionId;
        var adjusted = am->GetAdjustedActionId(actionId);
        return adjusted == 0 ? actionId : adjusted;
    }

    /// <summary>"Play II (The Arrow)" while a card is drawn; just the name otherwise.</summary>
    private static string NameWithCurrent(uint actionId)
    {
        var adjusted = Adjusted(actionId);
        return adjusted != actionId ? $"{Name(actionId)} ({Name(adjusted)})" : Name(actionId);
    }

    public static string Tooltip(uint actionId) =>
        actionId == Casting.EukrasianDiagnosis ? Name(actionId) + "\n1st click: Eukrasia, 2nd click: shield on that member"
        : MacroRunner.IsMacro(actionId) ? Name(actionId) + "\nRuns with the clicked member as <mo>"
        : Casting.IsSwiftRaise(actionId) ? Name(actionId) + "\nSwiftcast first if it's ready, then the raise"
        : NameWithCurrent(actionId);

    public static string Name(uint actionId)
    {
        if (actionId == 0) return "(nothing)";
        if (MacroRunner.IsMacro(actionId)) return MacroRunner.Name(actionId);
        if (Casting.IsSwiftRaise(actionId)) return "Swiftcast + " + Name(Casting.RaiseOf(actionId));
        var row = Plugin.DataManager.GetExcelSheet<LuminaAction>().GetRowOrDefault(actionId);
        return row?.Name.ExtractText() ?? $"Action #{actionId}";
    }
}
