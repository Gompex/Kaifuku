using System.Collections.Generic;
using System.Linq;
using Dalamud.Game;
using LuminaAction = Lumina.Excel.Sheets.Action;
using LuminaClassJob = Lumina.Excel.Sheets.ClassJob;

namespace Kaifuku;

/// <summary>Every player skill a job can use (job + role actions), for the searchable picker.</summary>
internal static class ActionCatalog
{
    public readonly record struct Entry(uint Id, string Name, byte Level);

    private static readonly Dictionary<uint, List<Entry>> Cache = new();

    public static IReadOnlyList<Entry> ForJob(uint classJobId)
    {
        if (Cache.TryGetValue(classJobId, out var cached)) return cached;

        var abbr = Plugin.DataManager.GetExcelSheet<LuminaClassJob>().GetRowOrDefault(classJobId)?.Abbreviation.ExtractText()
                   ?? string.Empty;
        var list = new List<Entry>();

        if (abbr.Length > 0)
        {
            foreach (var a in Plugin.DataManager.GetExcelSheet<LuminaAction>())
            {
                if (!a.IsPlayerAction || a.IsPvP || a.Icon == 0) continue;

                // ClassJobCategory names look like "CNJ WHM" or "WHM SCH AST SGE".
                var category = a.ClassJobCategory.ValueNullable?.Name.ExtractText();
                if (string.IsNullOrEmpty(category) || !category.Split(' ').Contains(abbr)) continue;

                var name = a.Name.ExtractText();
                if (name.Length == 0) continue;

                list.Add(new Entry(a.RowId, name, a.ClassJobLevel));
            }
        }

        list = list.OrderBy(e => e.Level).ThenBy(e => e.Name).ToList();
        Cache[classJobId] = list;
        return list;
    }

    /// <summary>
    /// The player action with this English name that the job can use, or 0. Default bindings for non-healer
    /// jobs are written by name and looked up here, so they always point at the real action in the game data.
    /// </summary>
    public static uint FindByEnglishName(uint classJobId, string name)
    {
        var abbr = Plugin.DataManager.GetExcelSheet<LuminaClassJob>(ClientLanguage.English)
            .GetRowOrDefault(classJobId)?.Abbreviation.ExtractText() ?? string.Empty;
        if (abbr.Length == 0) return 0;

        var wanted = StatusCatalog.Normalize(name);
        foreach (var a in Plugin.DataManager.GetExcelSheet<LuminaAction>(ClientLanguage.English))
        {
            if (!a.IsPlayerAction || a.IsPvP) continue;
            if (StatusCatalog.Normalize(a.Name.ExtractText()) != wanted) continue;
            var category = a.ClassJobCategory.ValueNullable?.Name.ExtractText();
            if (string.IsNullOrEmpty(category) || !category.Split(' ').Contains(abbr)) continue;
            return a.RowId;
        }
        return 0;
    }
}
