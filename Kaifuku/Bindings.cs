using System.Collections.Generic;
using System.Linq;
using LuminaClassJob = Lumina.Excel.Sheets.ClassJob;

namespace Kaifuku;

public enum Modifier { None, Shift, Ctrl, Alt }
public enum MouseButton { Left = 0, Right = 1, Middle = 2, Mouse4 = 3, Mouse5 = 4 }

public static class Bindings
{
    public const uint WHM = 24, SCH = 28, AST = 33, SGE = 40;
    public const uint PLD = 19, MNK = 20, WAR = 21, DRG = 22, BRD = 23, BLM = 25, SMN = 27, NIN = 30,
        MCH = 31, DRK = 32, SAM = 34, RDM = 35, GNB = 37, DNC = 38, RPR = 39, VPR = 41, PCT = 42;
    private const uint GLA = 1, PGL = 2, MRD = 3, LNC = 4, ARC = 5, CNJ = 6, THM = 7, ACN = 26, ROG = 29;

    // Action IDs
    private const uint Esuna = 7568;
    private const uint Cure = 120, CureII = 135, Regen = 137, Raise = 125, Benediction = 140,
        Tetragrammaton = 3570, DivineBenison = 7432, AfflatusSolace = 16531, Aquaveil = 25861;
    private const uint Physick = 190, Adloquium = 185, Lustrate = 189, Resurrection = 173,
        Excogitation = 7434, Protraction = 25867, DeploymentTactics = 3585, Aetherpact = 7437;
    private const uint Benefic = 3594, BeneficII = 3610, AspectedBenefic = 3595, EssentialDignity = 3614,
        Ascend = 3603, Synastry = 3612, CelestialIntersection = 16556, Exaltation = 25873;
    private const uint Diagnosis = 24284, Egeiro = 24287, Kardia = 24285, Druochole = 24296,
        Taurochole = 24303, Haima = 24305, Krasis = 24317, Icarus = 24295,
        EukrasianDiagnosis = Casting.EukrasianDiagnosis;

    private const Modifier N = Modifier.None, S = Modifier.Shift, C = Modifier.Ctrl, A = Modifier.Alt;
    private const MouseButton L = MouseButton.Left, R = MouseButton.Right, M = MouseButton.Middle;

    public static string Key(Modifier mod, MouseButton button) => $"{mod}+{button}";

    private static Dictionary<string, uint> Map(params (Modifier Mod, MouseButton Button, uint Action)[] entries)
    {
        var map = new Dictionary<string, uint>();
        foreach (var e in entries) map[Key(e.Mod, e.Button)] = e.Action;
        return map;
    }

    // Most-used actions on the easiest clicks: free/oGCD heals on Left and Right,
    // MP-costing GCD heals behind Ctrl, raises on Middle, Esuna and utility on Alt.
    public static readonly Dictionary<uint, Dictionary<string, uint>> Defaults = new()
    {
        [WHM] = Map((N, L, AfflatusSolace), (N, R, Tetragrammaton),
                    (S, L, Regen), (S, R, DivineBenison),
                    (C, L, CureII), (C, R, Aquaveil),
                    (N, M, Casting.SwiftRaise(Raise)), (C, M, Benediction),
                    (A, L, Esuna)),
        [SCH] = Map((N, L, Lustrate), (N, R, Excogitation),
                    (S, L, Protraction), (S, R, Aetherpact),
                    (C, L, Adloquium), (C, R, DeploymentTactics),
                    (N, M, Casting.SwiftRaise(Resurrection)),
                    (A, L, Esuna)),
        // Sage: left column = Addersgall heals, right column = shields, Alt = utility.
        // Eukrasian Diagnosis is a two-click slot: 1st click Eukrasia, 2nd click the shield.
        [SGE] = Map((N, L, Druochole), (N, R, EukrasianDiagnosis),
                    (S, L, Taurochole), (S, R, Haima),
                    (C, L, Krasis),
                    (N, M, Casting.SwiftRaise(Egeiro)),
                    (A, L, Esuna), (A, R, Kardia), (A, M, Icarus)),
    };

    /// <summary>
    /// Non-healer jobs: the skills they can cast on another party member, by English name (looked up in the
    /// game data, see ActionCatalog.FindByEnglishName). Same scheme as the healers: main tools on Left/Right,
    /// MP-costing GCD heals behind Ctrl, raises on Middle, utility on Alt. Jobs with no ally-targeted skills
    /// (DRG, NIN, SAM, RPR, VPR, MCH, PCT) have no defaults.
    /// </summary>
    private static readonly Dictionary<uint, (Modifier Mod, MouseButton Button, string Name)[]> NamedDefaults = new()
    {
        [PLD] = [(N, L, "Intervention"), (N, R, "Cover"), (C, L, "Clemency"), (A, R, "Shirk")],
        [WAR] = [(N, L, "Nascent Flash"), (A, R, "Shirk")],
        [DRK] = [(N, L, "The Blackest Night"), (N, R, "Oblation"), (A, R, "Shirk")],
        [GNB] = [(N, L, "Heart of Corundum"), (N, R, "Aurora"), (A, R, "Shirk")],
        [MNK] = [(A, M, "Thunderclap")],
        [BRD] = [(N, L, "The Warden's Paean")],
        [DNC] = [(N, L, "Closed Position")],
        [BLM] = [(A, M, "Aetherial Manipulation")],
        [SMN] = [(N, L, "Rekindle"), (N, M, "swift:Resurrection")],
        [RDM] = [(C, L, "Vercure"), (N, M, "swift:Verraise")],

        // Astrologian: main oGCD heals on clicks, regen / mitigation on Shift, the three card buttons on Ctrl
        // (Play I = Balance or Spear, Play II = Arrow or Bole, Play III = Spire or Ewer: each turns into
        // whatever card is drawn, like on the hotbar), Esuna and the filler Benefic II on Alt.
        [AST] = [(N, L, "Essential Dignity"), (N, R, "Celestial Intersection"), (N, M, "swift:Ascend"),
                 (S, L, "Aspected Benefic"), (S, R, "Exaltation"), (S, M, "Synastry"),
                 (C, L, "Play I"), (C, R, "Play II"), (C, M, "Play III"),
                 (A, L, "Esuna"), (A, R, "Benefic II")],
    };

    private static readonly Dictionary<uint, Dictionary<string, uint>> ResolvedNamed = new();

    private static Dictionary<string, uint>? DefaultsFor(uint jobKey)
    {
        if (Defaults.TryGetValue(jobKey, out var healer)) return healer;
        if (ResolvedNamed.TryGetValue(jobKey, out var cached)) return cached;
        if (!NamedDefaults.TryGetValue(jobKey, out var named)) return null;

        var map = new Dictionary<string, uint>();
        foreach (var (mod, button, entry) in named)
        {
            // "swift:" = Swiftcast first, then this raise.
            var swift = entry.StartsWith("swift:");
            var name = swift ? entry["swift:".Length..] : entry;
            var id = ActionCatalog.FindByEnglishName(jobKey, name);
            if (id != 0) map[Key(mod, button)] = swift ? Casting.SwiftRaise(id) : id;
            else Plugin.Log.Warning($"Kaifuku: no action named \"{name}\" for job {jobKey}");
        }
        ResolvedNamed[jobKey] = map;
        return map;
    }

    /// <summary>Icons offered under "Suggested" in the skill picker.</summary>
    public static uint[] Suggested(uint classJobId, Configuration cfg)
    {
        var key = JobKey(classJobId, cfg);
        if (Pools.TryGetValue(key, out var pool)) return pool;
        return DefaultsFor(key)?.Values.Distinct().ToArray() ?? [];
    }

    /// <summary>Quick-pick icons shown at the top of the skill picker.</summary>
    public static readonly Dictionary<uint, uint[]> Pools = new()
    {
        [WHM] = [AfflatusSolace, Tetragrammaton, DivineBenison, Regen, Aquaveil, Benediction, CureII, Cure, Esuna, Casting.SwiftRaise(Raise), Raise],
        [SCH] = [Lustrate, Excogitation, Protraction, Aetherpact, DeploymentTactics, Adloquium, Physick, Esuna, Casting.SwiftRaise(Resurrection), Resurrection],
        [SGE] = [Druochole, Taurochole, EukrasianDiagnosis, Haima, Krasis, Kardia, Diagnosis, Icarus, Esuna, Casting.SwiftRaise(Egeiro), Egeiro],
    };

    /// <summary>
    /// Which binding set a job uses, or 0 if the frames are off for it.
    /// Conjurer shares White Mage's set; other jobs only work with "Show frames on all jobs".
    /// </summary>
    public static uint JobKey(uint classJobId, Configuration cfg)
    {
        switch (classJobId)
        {
            case WHM:
            case CNJ: return WHM;
            case SCH: return SCH;
            case AST: return AST;
            case SGE: return SGE;
        }
        if (!cfg.ShowOnAllJobs || classJobId == 0) return 0;

        // Base classes share their job's bindings.
        return classJobId switch
        {
            GLA => PLD, PGL => MNK, MRD => WAR, LNC => DRG, ARC => BRD,
            THM => BLM, ACN => SMN, ROG => NIN,
            _ => classJobId,
        };
    }

    public static string JobName(uint key) => key switch
    {
        WHM => "White Mage",
        SCH => "Scholar",
        AST => "Astrologian",
        SGE => "Sage",
        _ => Plugin.DataManager.GetExcelSheet<LuminaClassJob>().GetRowOrDefault(key)?.Abbreviation.ExtractText() ?? "?",
    };

    public static uint Resolve(uint classJobId, Modifier mod, MouseButton button, Configuration cfg)
    {
        var job = JobKey(classJobId, cfg);
        if (job == 0) return 0;
        var key = Key(mod, button);
        if (cfg.JobBindings.TryGetValue(job, out var custom) && custom.TryGetValue(key, out var id)) return id;
        var defaults = DefaultsFor(job);
        return defaults != null && defaults.TryGetValue(key, out var def) ? def : 0;
    }

    public static void Set(uint classJobId, Modifier mod, MouseButton button, uint actionId, Configuration cfg)
    {
        var job = JobKey(classJobId, cfg);
        if (job == 0) return;
        if (!cfg.JobBindings.TryGetValue(job, out var custom)) cfg.JobBindings[job] = custom = new();
        custom[Key(mod, button)] = actionId;
    }

    public static void ResetJob(uint classJobId, Configuration cfg) => cfg.JobBindings.Remove(JobKey(classJobId, cfg));

    /// <summary>If several modifiers are held, Ctrl wins, then Shift, then Alt.</summary>
    public static Modifier ModifierFrom(bool shift, bool ctrl, bool alt) =>
        ctrl ? Modifier.Ctrl : shift ? Modifier.Shift : alt ? Modifier.Alt : Modifier.None;

    public static string Label(Modifier mod) => mod switch
    {
        Modifier.None => "No modifier",
        _ => mod.ToString(),
    };

    public static string Label(MouseButton button) => button switch
    {
        MouseButton.Mouse4 => "Mouse 4",
        MouseButton.Mouse5 => "Mouse 5",
        _ => button.ToString(),
    };

    /// <summary>"Left", "Shift + Right", "Alt + Mouse 4"...</summary>
    public static string ShortLabel(Modifier mod, MouseButton button) =>
        mod == Modifier.None ? Label(button) : $"{mod} + {Label(button)}";
}
