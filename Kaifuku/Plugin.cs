using System;
using System.Collections.Generic;
using System.IO;
using Dalamud.Game.Command;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Kaifuku.Windows;

namespace Kaifuku;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IPartyList PartyList { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IGameConfig GameConfig { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;

    private const string Command = "/kaifuku";

    public Configuration Config { get; }

    // The game's own fonts, used for headers: serif (Dark/Light), retro (Classic), condensed (Clear Blue).
    private readonly IFontHandle serifFont;
    private readonly IFontHandle retroFont;
    private readonly IFontHandle condensedFont;

    public IFontHandle GetHeaderFont(HeaderFont font) => font switch
    {
        HeaderFont.Retro => retroFont,
        HeaderFont.Condensed => condensedFont,
        _ => serifFont,
    };

    /// <summary>The fonts one v2 skin draws with (files bundled in the plugin's Fonts folder).</summary>
    public sealed record SkinFontSet(IFontHandle Name, IFontHandle Sub, IFontHandle Number, IFontHandle Header, IFontHandle Chip, IFontHandle Tag);

    private readonly Dictionary<Skin, (float Scale, SkinFontSet Set)> skinFonts = new();
    private readonly List<IFontHandle> ownedFonts = new();

    /// <summary>A skin's fonts at the plugin scale; rebuilt (crisp, not stretched) when the scale changes.</summary>
    internal SkinFontSet GetSkinFonts(SkinSpec spec, float scale)
    {
        scale = MathF.Round(scale * 20f) / 20f; // steps of 5% so dragging the slider doesn't rebuild every frame
        if (skinFonts.TryGetValue(spec.Id, out var entry))
        {
            if (MathF.Abs(entry.Scale - scale) < 0.001f) return entry.Set;
            foreach (var h in new[] { entry.Set.Name, entry.Set.Sub, entry.Set.Number, entry.Set.Header, entry.Set.Chip, entry.Set.Tag })
            {
                ownedFonts.Remove(h);
                h.Dispose();
            }
        }

        var set = new SkinFontSet(
            FileFont(spec.NameFont, scale), FileFont(spec.SubFont, scale), FileFont(spec.NumberFont, scale),
            FileFont(spec.HeaderFont, scale), FileFont(spec.ChipFont, scale), FileFont(spec.TagFont, scale));
        skinFonts[spec.Id] = (scale, set);
        return set;
    }

    private IFontHandle FileFont((string File, float Px) font, float scale)
    {
        font.Px *= scale;
        var path = Path.Combine(PluginInterface.AssemblyLocation.Directory!.FullName, "Fonts", font.File);
        var handle = PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(e => e.OnPreBuild(tk =>
        {
            if (File.Exists(path)) tk.AddFontFromFile(path, new SafeFontConfig { SizePx = font.Px });
            else tk.AddDalamudDefaultFont(font.Px);
        }));
        ownedFonts.Add(handle);
        return handle;
    }

    private readonly WindowSystem windows = new("Kaifuku");
    private readonly FramesWindow framesWindow;
    private readonly ConfigWindow configWindow;
    internal bool SettingsOpen => configWindow.IsOpen;
    private readonly GuideWindow guideWindow;

    public Plugin()
    {
        Config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        if (!Config.CommonShieldsSeeded)
        {
            foreach (var id in StatusCatalog.BuffIdsByEnglishName(StatusCatalog.CommonShields))
                Config.AlwaysShowStatuses.Add(id);
            Config.CommonShieldsSeeded = true;
            SaveConfig();
        }

        if (!Config.SkinApplied)
        {
            Skins.Apply(Config, Config.Skin, true); // first run of v2: load the default skin's colors and layout
            Config.SkinApplied = true;
            SaveConfig();
        }

        if (!Config.AwarenessSeeded)
        {
            Config.MustHealStatuses.UnionWith(StatusCatalog.IdsByEnglishName(StatusCatalog.MustHealDefaults));
            Config.TankCooldownStatuses.UnionWith(StatusCatalog.IdsByEnglishName(StatusCatalog.TankCooldownDefaults));
            Config.AwarenessSeeded = true;
            SaveConfig();
        }

        if (!Config.CommonHotsSeeded)
        {
            foreach (var id in StatusCatalog.BuffIdsByEnglishName(StatusCatalog.CommonHots))
                Config.HotStatuses.Add(id);
            Config.CommonHotsSeeded = true;
            SaveConfig();
        }

        var atlas = PluginInterface.UiBuilder.FontAtlas;
        serifFont = atlas.NewGameFontHandle(new GameFontStyle(GameFontFamilyAndSize.Jupiter16));
        retroFont = atlas.NewGameFontHandle(new GameFontStyle(GameFontFamilyAndSize.MiedingerMid14));
        condensedFont = atlas.NewGameFontHandle(new GameFontStyle(GameFontFamilyAndSize.TrumpGothic23));

        framesWindow = new FramesWindow(this) { IsOpen = Config.ShowFrames };
        configWindow = new ConfigWindow(this);
        guideWindow = new GuideWindow(this);
        windows.AddWindow(framesWindow);
        windows.AddWindow(configWindow);
        windows.AddWindow(guideWindow);

        CommandManager.AddHandler(Command, new CommandInfo(OnCommand)
        {
            HelpMessage = "Show/hide the frames. Also: \"/kaifuku config\" settings, \"/kaifuku lock\" lock/unlock, \"/kaifuku guide\" binding guide.",
        });

        CommandManager.AddHandler("/healframes", new CommandInfo(OnCommand) { HelpMessage = "Old name of /kaifuku.", ShowInHelp = false });
        PluginInterface.UiBuilder.Draw += windows.Draw;
        Framework.Update += MacroRunner.Update;
        Framework.Update += Casting.Update;
        Framework.Update += OnFrameworkUpdate;
        PluginInterface.UiBuilder.OpenMainUi += ToggleFrames;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfig;
    }

    public void SaveConfig() => PluginInterface.SavePluginConfig(Config);

    private void OnCommand(string command, string args)
    {
        switch (args.Trim().ToLowerInvariant())
        {
            case "config":
                ToggleConfig();
                break;
            case "lock":
                Config.Locked = !Config.Locked;
                SaveConfig();
                break;
            case "guide":
                Config.ShowGuide = !Config.ShowGuide;
                SaveConfig();
                break;
            default:
                ToggleFrames();
                break;
        }
    }

    private void ToggleFrames()
    {
        framesWindow.Toggle();
        Config.ShowFrames = framesWindow.IsOpen;
        SaveConfig();
    }
    public void SetFramesVisible(bool visible)
    {
        framesWindow.IsOpen = visible;
        Config.ShowFrames = visible;
        SaveConfig();
    }

    private void ToggleConfig() => configWindow.Toggle();

    private void OnFrameworkUpdate(IFramework framework) => Profiles.Update(this);

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= windows.Draw;
        Framework.Update -= MacroRunner.Update;
        Framework.Update -= Casting.Update;
        Framework.Update -= OnFrameworkUpdate;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleFrames;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfig;
        CommandManager.RemoveHandler(Command);
        CommandManager.RemoveHandler("/healframes");
        windows.RemoveAllWindows();
        serifFont.Dispose();
        foreach (var f in ownedFonts) f.Dispose();
        retroFont.Dispose();
        condensedFont.Dispose();
    }
}
