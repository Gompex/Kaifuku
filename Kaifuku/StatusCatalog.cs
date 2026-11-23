using System.Collections.Generic;
using Dalamud.Game;
using LuminaStatus = Lumina.Excel.Sheets.Status;

namespace Kaifuku;

/// <summary>All status effects from the game data, for the buff list editors on the Display tab.</summary>
internal static class StatusCatalog
{
    public readonly record struct Entry(uint Id, string Name, uint Icon, bool Debuff);

    private static List<Entry>? all;
    private static Dictionary<uint, Entry>? byId;

    public static IReadOnlyList<Entry> All
    {
        get
        {
            if (all != null) return all;
            all = new List<Entry>();
            byId = new Dictionary<uint, Entry>();
            foreach (var s in Plugin.DataManager.GetExcelSheet<LuminaStatus>())
            {
                if (s.Icon == 0) continue;
                var name = s.Name.ExtractText();
                if (name.Length == 0) continue;
                var entry = new Entry(s.RowId, name, s.Icon, (byte)s.StatusCategory == 2);
                all.Add(entry);
                byId[s.RowId] = entry;
            }
            return all;
        }
    }

    public static Entry? Get(uint id)
    {
        _ = All;
        return byId!.TryGetValue(id, out var e) ? e : null;
    }

    /// <summary>Common shield buffs from every healer and tank, matched by English name. Names that don't exist are skipped.</summary>
    public static readonly string[] CommonShields =
    [
        "Galvanize", "Catalyze", "Seraphic Veil",
        "Eukrasian Diagnosis", "Eukrasian Prognosis", "Differential Diagnosis",
        "Haima", "Haimatinon", "Panhaima", "Panhaimatinon", "Holosakos",
        "Divine Benison", "Divine Caress",
        "Intersection", "Neutral Sect",
        "Blackest Night", "Divine Veil", "Stem the Tide", "Shake It Off", "Brutal Shell",
    ];

    /// <summary>Common healing-over-time buffs (incl. ones cast by pets, like Whispering Dawn).</summary>
    public static readonly string[] CommonHots =
    [
        "Regen", "Medica II", "Medica III", "Asylum",
        "Whispering Dawn", "Angel's Whisper", "Sacred Soil",
        "Aspected Benefic", "Aspected Helios", "Helios Conjunction",
        "Physis", "Physis II", "Kerakeia",
        "Aurora", "Rekindle",
    ];

    /// <summary>Statuses that kill unless the member is healed to full HP in time.</summary>
    public static readonly string[] MustHealDefaults = ["Doom", "Walking Dead"];

    /// <summary>Tank mitigation and invulnerabilities, shown on frames whatever the buff filter says.</summary>
    public static readonly string[] TankCooldownDefaults =
    [
        "Rampart", "Sentinel", "Guardian", "Hallowed Ground", "Bulwark", "Holy Sheltron", "Knight's Resolve",
        "Vengeance", "Damnation", "Holmgang", "Thrill of Battle", "Bloodwhetting", "Stem the Flow",
        "Shadow Wall", "Shadowed Vigil", "Living Dead", "Undead Rebirth", "Dark Mind",
        "Nebula", "Great Nebula", "Superbolide", "Camouflage", "Heart of Corundum",
    ];

    /// <summary>Statuses of any category with these English names.</summary>
    public static HashSet<uint> IdsByEnglishName(params string[] names)
    {
        var wanted = new HashSet<string>();
        foreach (var n in names) wanted.Add(Normalize(n));
        var ids = new HashSet<uint>();
        foreach (var s in Plugin.DataManager.GetExcelSheet<LuminaStatus>(ClientLanguage.English))
            if (wanted.Contains(Normalize(s.Name.ExtractText()))) ids.Add(s.RowId);
        return ids;
    }

    public static IEnumerable<uint> BuffIdsByEnglishName(IEnumerable<string> names)
    {
        var wanted = new HashSet<string>();
        foreach (var n in names) wanted.Add(Normalize(n));

        foreach (var s in Plugin.DataManager.GetExcelSheet<LuminaStatus>(ClientLanguage.English))
        {
            if (s.Icon == 0 || (byte)s.StatusCategory != 1) continue;
            if (wanted.Contains(Normalize(s.Name.ExtractText()))) yield return s.RowId;
        }
    }

    public static string Normalize(string s) => s.Replace('\u2019', '\'').Trim().ToLowerInvariant();
}
