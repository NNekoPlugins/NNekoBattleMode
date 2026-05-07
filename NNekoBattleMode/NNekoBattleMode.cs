using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.DalamudServices.Legacy;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel;
using Emote = Lumina.Excel.Sheets.Emote;

namespace NNekoBattleMode;

public sealed class NNekoBattleMode : IDalamudPlugin
{
    [PluginService] public static IDalamudPluginInterface PluginInterface { get; private set; }
    [PluginService] public static ICommandManager CommandManager { get; private set; }
    [PluginService] public static IDataManager DataManager { get; private set; }
    [PluginService] public static IClientState ClientState { get; set; }
    [PluginService] public static IObjectTable ObjectTable { get; set; }
    [PluginService] public static IChatGui ChatGui { get; set; }
    [PluginService] public static ICondition Condition { get; set; }
    [PluginService] public static IPluginLog Log { get; private set; }

    private const string CommandName = "/nnbm";
    public WeaponState weaponState;
    public bool Disposed { get; set; }
    public bool Loaded => !this.Disposed
        && ClientState.LocalPlayer is not null
        && ClientState.LocalContentId is not 0;

    public unsafe bool Moving => AgentMap.Instance() is not null && AgentMap.Instance()->IsPlayerMoving;

    public Configuration Configuration { get; init; }

    public NNekoBattleMode()
    {

        ECommonsMain.Init(PluginInterface, this, Module.DalamudReflector, Module.ObjectFunctions);

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

    private void Tick()
    {
        HandleCombatExtras();
        HandleDrawSheathe();
    }



    private void HandleDrawSheathe()
    {
        var player = ClientState.LocalPlayer;
        if (player == null)
            return;

        bool inCombat = Condition[ConditionFlag.InCombat];
        bool moving = this.Moving;
        bool weaponDrawn = weaponState.IsUnsheathed;

        if (inCombat)
        {
            CommandManager.ProcessCommand("/battlemode");
            return;
        }

        if (weaponDrawn)
        {
            if (moving || HasEmote("sheathe") == null)
            {
                CommandManager.ProcessCommand("/battlemode off");
            }
            else
            {
                CommandManager.ProcessCommand("/sheathe motion");
            }
            CommandManager.ProcessCommand("/target");
            return;
        }

        // BLU, NIN, moving, or no draw emote
        if (player.ClassJob.Value.Abbreviation == "BLU" || player.ClassJob.Value.Abbreviation == "NIN" || moving || HasEmote("draw") == null)
        {
            CommandManager.ProcessCommand("/battlemode on");
        }
        else
        {
            CommandManager.ProcessCommand("/draw motion");
        }
    }

    private static void HandleCombatExtras()
    {
        var player = ClientState.LocalPlayer;
        if (player == null)
            return;

        bool inCombat = Condition[ConditionFlag.InCombat];
        bool isMounted = Condition[ConditionFlag.Mounted] || Condition[ConditionFlag.RidingPillion];
        bool weaponDrawn = ObjectTable.LocalPlayer!.StatusFlags.HasFlag(StatusFlags.WeaponOut);

        if (isMounted)
        {
            CommandManager.ProcessCommand("/mount clear");
            return;
        }

        if (inCombat && PluginInterface.InstalledPlugins.Any(p => p.InternalName == "Reset-dummy-enmity-command"))
        {
            CommandManager.ProcessCommand("/resetenmityall");
            return;
        }

        if (inCombat && PluginInterface.InstalledPlugins.Any(p => p.InternalName == "PandorasBox"))
        {
            CommandManager.ProcessCommand("/pre");
            return;
        }
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
    }

    #region Emotes

    private static bool emotesLoaded = false;
    private static readonly Dictionary<string, uint> emoteUnlocks = [];

    internal static void InitialiseEmotes()
    {
        if (emotesLoaded)
            return;
        emotesLoaded = true;

        Log.Information($"[Emotes] Initialising API data");

        ExcelSheet<Emote> emotes = DataManager.GameData.GetExcelSheet<Emote>()!;
        try
        {
            int max = emotes.Count;
            Log.Information($"[Emotes] Indexing {max:N0} emotes...");
            for (uint i = 0; i < max; ++i)
            {
                Emote? emote = emotes.GetRowOrDefault(i);
                if (emote.HasValue)
                {
                    string[] commands = [.. (new string?[] {
                        emote.Value.Name.ToString(),
                        emote.Value.TextCommand.ValueNullable?.Command.ToString(),
                        emote.Value.TextCommand.ValueNullable?.ShortCommand.ToString(),
                        emote.Value.TextCommand.ValueNullable?.Alias.ToString(),
                        emote.Value.TextCommand.ValueNullable?.ShortAlias.ToString(),
                    })
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Cast<string>()
                        .Select(s => s.TrimStart('/'))];
                    foreach (string command in commands)
                        emoteUnlocks[command] = emote.Value.UnlockLink;
                }
            }
            Log.Information($"[Emotes] Cached {emoteUnlocks.Count:N0} emote names");
        }
        catch (Exception e)
        {
            Log.Error("Unable to load Emote sheet, cannot check emote unlock state!", e);
        }

    }




    /// <summary>
    /// [LuaPlayerDoc("Determines whether the current character has unlocked a given emote.",
    ///     "The emote name should be one of the emote's commands, with or without the leading `/` character.")]
    /// </summary>
    /// <param name="emote"></param>
    /// <returns>bool has</returns>
    public unsafe bool? HasEmote(string emote)
    {
        if (!this.Loaded)
            return null;

        string internalName = emote.TrimStart('/');
        Log.Information($"Checking whether '{internalName}' is unlocked", "Emotes");
        if (!emoteUnlocks.TryGetValue(internalName, out uint unlockLink))
        {
            Log.Information("Can't find unlock link in cached map", "Emotes");
            return null;
        }
        UIState* uiState = UIState.Instance();
        if (uiState is null || (nint)uiState == nint.Zero)
        {
            Log.Information("UIState is null", "Emotes");
            return null;
        }
        bool has = uiState->IsUnlockLinkUnlockedOrQuestCompleted(unlockLink, 1);
        Log.Information($"UIState reports emote is {(has ? "un" : "")}locked", "Emotes");
        return has;
    }

    #endregion
}
