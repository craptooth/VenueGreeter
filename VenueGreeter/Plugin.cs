using System;
using System.Collections.Generic;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons;

namespace VenueGreeter;

public sealed class Plugin : IDalamudPlugin
{
    public string Name => "Fang's Venue Greeter";

    [PluginService] public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] public static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] public static IClientState ClientState { get; private set; } = null!;
    [PluginService] public static IFramework Framework { get; private set; } = null!;
    [PluginService] public static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] public static IPluginLog Log { get; private set; } = null!;

    public Configuration Config { get; private set; } = null!;
    public GreeterService Greeter { get; private set; } = null!;
    public PlaylistService Playlist { get; private set; } = null!;
    public PluginUI Ui { get; private set; } = null!;

    public readonly List<string> ActivityLog = new();

    private const string CommandName = "/fvg";

    public Plugin()
    {
        ECommonsMain.Init(PluginInterface, this);

        Config = LoadConfig();

        Greeter = new GreeterService(this);
        Playlist = new PlaylistService(this);
        Ui = new PluginUI(this);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Opens the Venue Greeter settings window."
        });

        PluginInterface.UiBuilder.Draw += Ui.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += Ui.Toggle;
        Framework.Update += OnFrameworkUpdate;

        // Nothing is started here: venue mode and the playlist always come up
        // off (see LoadConfig), so the constructor never touches game objects
        // and neither feature can begin sending before you turn it on.
    }

    /// <summary>
    /// Loads the saved config, falling back to defaults if the file is
    /// unreadable or malformed rather than letting the plugin fail to load.
    /// Venue mode and the playlist are always forced off on load: the login
    /// screen drops you into a public area, so neither feature should ever
    /// resume by itself after a restart or crash.
    /// </summary>
    private static Configuration LoadConfig()
    {
        Configuration cfg;
        try
        {
            cfg = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not read Venue Greeter config; falling back to defaults.");
            cfg = new Configuration();
        }

        cfg.Sanitize();
        cfg.VenueModeEnabled = false;
        cfg.PlaylistEnabled = false;
        cfg.Save(); // persist the "off" state so the file matches what's running
        return cfg;
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        // Debounced config writes (see Configuration.Save).
        Config.FlushIfDue(TimeSpan.FromSeconds(1));
    }

    private void OnCommand(string command, string args) => Ui.Toggle();

    /// <summary>
    /// All callers of this run on the framework thread already — Framework.Update
    /// and TerritoryChanged both fire there, and there's no background/network
    /// thread in this plugin (unlike a TCP-bridge style plugin), so no marshaling
    /// is needed before touching ActivityLog or game-facing APIs here.
    /// </summary>
    public void LogActivity(string line)
    {
        ActivityLog.Add(line);
        if (ActivityLog.Count > 300)
            ActivityLog.RemoveAt(0);
        Log.Debug(line);
    }

    public void Dispose()
    {
        Framework.Update -= OnFrameworkUpdate;
        CommandManager.RemoveHandler(CommandName);
        PluginInterface.UiBuilder.Draw -= Ui.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= Ui.Toggle;
        Greeter.Dispose();
        Playlist.Dispose();

        // Write out any edits still waiting on the debounce timer.
        Config.SaveNow();

        ECommonsMain.Dispose();
    }
}
