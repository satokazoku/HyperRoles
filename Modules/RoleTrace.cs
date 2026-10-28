using System;
using System.Linq;
using AmongUs.GameOptions;
using HarmonyLib;

namespace HyperRoles;

static class RoleTrace
{
    static readonly BepInEx.Logging.ManualLogSource log =
        BepInEx.Logging.Logger.CreateLogSource("RoleTrace");

    public static void Write(string where, byte playerId, RoleTypes role, string extra = "")
    {
        // 呼び出し元(自分のMODのコードが出てくれば、そこが原因)を、上から数行だけ出す
        var frames = new System.Diagnostics.StackTrace(2, false).ToString()
            .Split('\n')
            .Take(10)
            .Select(l => "    " + l.Trim());

        log.LogInfo($"[RoleTrace] {where}: player={playerId} role={role} {extra}\n{string.Join("\n", frames)}");
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSetRole), new[] { typeof(RoleTypes), typeof(bool) })]
static class RpcSetRoleTracePatch
{
    public static void Prefix(PlayerControl __instance, RoleTypes roleType, bool canOverrideRole)
        => RoleTrace.Write("PlayerControl.RpcSetRole", __instance.PlayerId, roleType, $"canOverride={canOverrideRole}");
}

[HarmonyPatch(typeof(RoleManager), nameof(RoleManager.SetRole), new[] { typeof(PlayerControl), typeof(RoleTypes) })]
static class RoleManagerSetRoleTracePatch
{
    public static void Prefix(PlayerControl targetPlayer, RoleTypes roleType)
        => RoleTrace.Write("RoleManager.SetRole", targetPlayer != null ? targetPlayer.PlayerId : (byte)255, roleType);
}
