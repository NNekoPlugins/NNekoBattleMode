using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc.Internal;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using System;
using System.Linq;
using Emote = Lumina.Excel.Sheets.Emote;

namespace NNekoBattleMode;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] public static ICondition Condition { get; private set; } = null!;
    [PluginService] public static IUnlockState UnlockState { get; private set; } = null!;
    [PluginService] internal static IObjectTable Objects { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string CommandName = "/nnbm";

    public unsafe bool Moving => AgentMap.Instance() is not null && AgentMap.Instance()->IsPlayerMoving;

    public Configuration Configuration { get; init; }

    public Plugin()
    {
        
        ECommonsMain.Init(PluginInterface, this, Module.DalamudReflector);

        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Use in place of /bm to queue your draw/sheathe actions more intelligently."
        });

        // Add a simple message to the log with level set to information
        // Use /xllog to open the log window in-game
        // Example Output: 00:57:54.959 | INF | [NNekoBattleMode] ===A cool log message from Sample Plugin===
        Log.Information($"==={PluginInterface.Manifest.Name} Has Loaded.===");
    }

    public void Dispose()
    {
        CommandManager.RemoveHandler(CommandName);
    }

    private static void Assert(bool succeeds, string message)
    {
        if (!succeeds)
            throw new Exception(message);
    }

    public static Emote? FindEmoteByCommand(IDataManager dataManager, string command)
    {
        command = command.ToLowerInvariant();

        foreach (var emote in dataManager.GetExcelSheet<Emote>()!)
        {
            var textCommand = emote.TextCommand.Value;
            if (textCommand.Command.IsEmpty)
            {
                continue;
            }

            // TextCommand.Command is the slash command without the leading '/'
            if ($"/{textCommand.Command.ToString().ToLowerInvariant}" == command)
            {
                return emote;
            }
        }

        return null;
    }
    
    private static IExposedPlugin? findPlugin(string name)
    {
        IExposedPlugin[] plugins = [.. PluginInterface.InstalledPlugins];
        Log.Information($"Checking {plugins.Length} installed plugins for {name}");
        return plugins.FirstOrDefault(p => p.InternalName == name);
    }

    private async void OnCommand(string command, string args)
        {
            var player = ClientState.LocalPlayer;
            if (player == null)
                return;

            await LogicRunner.RunOnce(player);
        }
    }

}
