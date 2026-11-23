using System;
using System.Collections.Generic;
using System.Text;
using Dalamud.Game.ClientState.Conditions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using LuminaTerritoryType = Lumina.Excel.Sheets.TerritoryType;

namespace Kaifuku;

/// <summary>
/// Named sets of look / layout / display settings, switched automatically per job or per content type.
/// Bindings, position, lock state and the like are never part of a profile.
/// </summary>
internal static class Profiles
{
    private static readonly HashSet<string> NotInProfile =
    [
        "Version", "JobBindings", "MacroAlsoTarget", "Profiles", "JobProfiles", "ContentProfiles", "ActiveProfile",
        "AutoPositioned", "Preview", "ShowFrames", "Locked", "GuideLocked", "ShowGuide",
        "CommonShieldsSeeded", "CommonHotsSeeded", "AwarenessSeeded", "SkinApplied", "ShowOnAllJobs",
    ];

    private static readonly JsonSerializerSettings ApplySettings = new()
    {
        ObjectCreationHandling = ObjectCreationHandling.Replace, // lists and sets are replaced, not merged
    };

    public static string Capture(Configuration c)
    {
        var json = JObject.FromObject(c);
        foreach (var key in NotInProfile) json.Remove(key);
        return json.ToString(Formatting.None);
    }

    public static void Apply(Configuration c, string json) => JsonConvert.PopulateObject(json, c, ApplySettings);

    // ---------- export / import (whole setup, bindings included) ----------

    private const string SharePrefix = "HF1:";
    private static readonly HashSet<string> NotShared =
        ["Version", "AutoPositioned", "Preview", "ShowFrames", "CommonShieldsSeeded", "CommonHotsSeeded", "AwarenessSeeded", "SkinApplied"];

    public static string Export(Configuration c)
    {
        var json = JObject.FromObject(c);
        foreach (var key in NotShared) json.Remove(key);
        return SharePrefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(json.ToString(Formatting.None)));
    }

    public static bool Import(Plugin plugin, string? text)
    {
        try
        {
            text = text?.Trim();
            if (text == null || !text.StartsWith(SharePrefix)) return false;
            var json = JObject.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(text[SharePrefix.Length..])));
            foreach (var key in NotShared) json.Remove(key);
            JsonConvert.PopulateObject(json.ToString(Formatting.None), plugin.Config, ApplySettings);
            plugin.SaveConfig();
            return true;
        }
        catch (Exception e)
        {
            Plugin.Log.Warning($"Kaifuku: import failed: {e.Message}");
            return false;
        }
    }

    public static string Label(ContentKind kind) => kind switch
    {
        ContentKind.Overworld => "Open world",
        ContentKind.Dungeon => "Dungeons",
        ContentKind.Trial => "Trials",
        ContentKind.Raid => "Raids",
        ContentKind.AllianceRaid => "Alliance raids",
        _ => "Other duties",
    };

    public static ContentKind Current()
    {
        if (!Plugin.Condition[ConditionFlag.BoundByDuty]) return ContentKind.Overworld;
        if (Plugin.PartyList.IsAlliance) return ContentKind.AllianceRaid;

        var territory = Plugin.DataManager.GetExcelSheet<LuminaTerritoryType>().GetRowOrDefault(Plugin.ClientState.TerritoryType);
        var contentType = territory?.ContentFinderCondition.ValueNullable?.ContentType.RowId ?? 0;
        return contentType switch
        {
            2 => ContentKind.Dungeon,
            4 => ContentKind.Trial,
            5 => ContentKind.Raid,
            _ => ContentKind.OtherDuty,
        };
    }

    /// <summary>The profile the rules ask for right now: content type wins over job; null = no rule.</summary>
    public static string? Wanted(Configuration c, uint classJobId, ContentKind kind)
    {
        if (c.ContentProfiles.TryGetValue(kind, out var byContent) && c.Profiles.ContainsKey(byContent)) return byContent;
        if (c.JobProfiles.TryGetValue(classJobId, out var byJob) && c.Profiles.ContainsKey(byJob)) return byJob;
        return null;
    }

    public static void Switch(Plugin plugin, string name)
    {
        var c = plugin.Config;
        if (!c.Profiles.TryGetValue(name, out var json)) return;

        // Keep whatever was changed in the profile we're leaving.
        if (c.ActiveProfile.Length > 0 && c.ActiveProfile != name && c.Profiles.ContainsKey(c.ActiveProfile))
            c.Profiles[c.ActiveProfile] = Capture(c);

        Apply(c, json);
        c.ActiveProfile = name;
        plugin.SaveConfig();
    }

    private static uint lastJob;
    private static uint lastTerritory;
    private static bool lastAlliance;

    /// <summary>Every game frame, but only does work when the job, zone or alliance state changes.</summary>
    public static void Update(Plugin plugin)
    {
        var player = Plugin.ObjectTable.LocalPlayer;
        if (player == null) return;

        var job = player.ClassJob.RowId;
        var territory = Plugin.ClientState.TerritoryType;
        var alliance = Plugin.PartyList.IsAlliance;
        if (job == lastJob && territory == lastTerritory && alliance == lastAlliance) return;
        lastJob = job;
        lastTerritory = territory;
        lastAlliance = alliance;

        var wanted = Wanted(plugin.Config, job, Current());
        if (wanted != null && wanted != plugin.Config.ActiveProfile) Switch(plugin, wanted);
    }
}
