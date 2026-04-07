using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using System;
using System.Threading.Tasks;

namespace NNekoBattleMode.Services;

public static class LogicRunner
{
    [PluginService] private static IClientState ClientState { get; set; } = null!;
    [PluginService] private static ICondition Condition { get; set; } = null!;
    [PluginService] private static ICommandManager Commands { get; set; } = null!;
    [PluginService] private static IPluginInterface Interface { get; set; } = null!;

    // -----------------------------
    // Entry point (triggered by /nnbm)
    // -----------------------------
    public static async Task RunOnce(PlayerCharacter player)
    {
        await CheckLogic(player);
    }

    // -----------------------------
    // check() from WoLua
    // -----------------------------
    private static async Task CheckLogic(PlayerCharacter player)
    {
        bool mounted = Condition[ConditionFlag.Mounted];
        bool inCombat = Condition[ConditionFlag.InCombat];

        if (mounted)
        {
            Send("/mount clear");
            return;
        }

        if (inCombat && HasPlugin("Reset-dummy-enmity-command"))
        {
            Send("/resetenmityall");
            await Task.Delay(500);
        }
        else if (inCombat && HasPlugin("PandorasBox"))
        {
            Send("/pre");
            await Task.Delay(500);
        }

        CoreLogic(player);
    }

    // -----------------------------
    // core() from WoLua
    // -----------------------------
    private static void CoreLogic(PlayerCharacter player)
    {
        bool inCombat = Condition[ConditionFlag.InCombat];
        bool moving = player.IsMoving();
        bool weaponDrawn = Condition[ConditionFlag.WeaponDrawn];

        bool isBlu = player.ClassJob.Id == 36;
        bool isNin = player.ClassJob.Abbreviation == "NIN";

        bool hasDraw = HasEmote("draw");
        bool hasSheathe = HasEmote("sheathe");

        if (inCombat)
        {
            Send("/battlemode");
        }
        else if (weaponDrawn)
        {
            if (moving || !hasSheathe)
                Send("/battlemode off");
            else
                Send("/sheathe motion");

            Send("/cleartarget");
        }
        else if (isBlu || isNin || moving || !hasDraw)
        {
            Send("/battlemode on");
        }
        else
        {
            Send("/draw motion");
        }
    }

    // -----------------------------
    // Helpers
    // -----------------------------
    private static void Send(string text)
    {
        Commands.ProcessCommand(text);
    }

    private static bool HasPlugin(string internalName)
    {
        return Interface.InstalledPlugins.TryGetValue(internalName, out var p) && p.IsLoaded;
    }

    private unsafe static bool HasEmote(string name)
    {
        var actionManager = ActionManager.Instance();
        if (actionManager == null)
            return false;

        for (uint id = 0; id < 1000; id++)
        {
            if (actionManager->GetActionName(7, id).ToString().Equals(name, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}

// -----------------------------
// Extension
// -----------------------------
public static class PlayerExtensions
{
    public static bool IsMoving(this PlayerCharacter pc)
    {
        return pc?.Velocity != System.Numerics.Vector3.Zero;
    }
}
