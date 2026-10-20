using System.Collections.Generic;
using Epic.OnlineServices.Presence;
using HarmonyLib;
using Hazel;
using HyperRoles.Roles.Neutral;

namespace HyperRoles;

/// <summary>
/// 自前RPCの呼び出しID。バニラのRpcCallsと被らない値を使う。
/// </summary>
public enum CustomRpc : byte
{
    SyncRoles = 100,
    TargetAbility,
    Camouflage,
    EndCamouFlage,
}

public static class RpcSender
{
    static readonly BepInEx.Logging.ManualLogSource log =
        BepInEx.Logging.Logger.CreateLogSource("Rpc");
    public static Dictionary<byte, NetworkedPlayerInfo> Infos = new();
    public static bool NowCamouFlage = false;
    /// <summary>
    /// ホストが、全員の役職を全クライアントに送る。
    /// 引数の targetClientId を省略(-1)すると全員に送る。
    /// </summary>
    public static void SendSyncRoles(int targetClientId = -1)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        var roles = CustomRoleManager.GetAllRoleIds(); // Dictionary<byte, RoleId>

        var writer = AmongUsClient.Instance.StartRpcImmediately(
            PlayerControl.LocalPlayer.NetId,
            (byte)CustomRpc.SyncRoles,
            SendOption.Reliable,
            targetClientId);

        writer.Write((byte)roles.Count);
        foreach (var kv in roles)
        {
            writer.Write(kv.Key);          // playerId
            writer.Write((byte)kv.Value);  // roleId
        }

        AmongUsClient.Instance.FinishRpcImmediately(writer);
        log.LogInfo($"[Rpc] SyncRoles sent. count={roles.Count}");
    }

    /// <summary>
    /// 受信側。HandleRpcPatch から呼ばれる。
    /// </summary>
    public static void ReceiveSyncRoles(MessageReader reader)
    {
        int count = reader.ReadByte();
        CustomRoleManager.ClearRoles();
        for (int i = 0; i < count; i++)
        {
            byte playerId = reader.ReadByte();
            var roleId = (CustomRoleManager.RoleId)reader.ReadByte();
            CustomRoleManager.SetRole(playerId, roleId);
        }
        log.LogInfo($"[Rpc] SyncRoles received. count={count}");
    }
    public static void SendCamouflageRpc(bool forceEnd = false)
    {
        bool shouldEnd = forceEnd || NowCamouFlage;

        CustomRpc rpcType = shouldEnd ? CustomRpc.EndCamouFlage : CustomRpc.Camouflage;

        HandleRpcPatch.Camouflage(shouldEnd);

        var writer = AmongUsClient.Instance.StartRpcImmediately(
            PlayerControl.LocalPlayer.NetId,
            (byte)rpcType,
            SendOption.Reliable,
            -1);

        AmongUsClient.Instance.FinishRpcImmediately(writer);

        NowCamouFlage = !shouldEnd;

        log.LogInfo($"[Rpc] Camouflage RPC sent. End={shouldEnd}, NowCamouFlage={NowCamouFlage}");
    }
}

/// <summary>
/// 全RPCの受信口。PlayerControl.HandleRpc が呼ばれるたびに、自前のIDだけ拾う。
/// </summary>
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.HandleRpc))]
static class HandleRpcPatch
{
    // HandleRpcPatch を、送信者(__instance)も受け取る形にして、case を足す
    public static void Postfix(
        PlayerControl __instance,
        [HarmonyArgument(0)] byte callId,
        [HarmonyArgument(1)] MessageReader reader)
    {
        switch ((CustomRpc)callId)
        {
            case CustomRpc.SyncRoles:
                RpcSender.ReceiveSyncRoles(reader);
                break;
            case CustomRpc.TargetAbility:
                JackalSidekickButton.ReceiveRequest(__instance, reader);
                break;
            case CustomRpc.Camouflage:
                Camouflage(false);
                break;
            case CustomRpc.EndCamouFlage:
                Camouflage(true);
                break;
        }
    }
    public static void Camouflage(bool end)
    {
        if (end)
        {
            foreach (var pid in RpcSender.Infos.Keys)
            {
                var epc = PlayerControl.AllPlayerControls[pid];
                var Outfit = epc.Data.DefaultOutfit;

                epc.SetHat(Outfit.HatId, epc.Data.DefaultOutfit.ColorId);
                epc.SetVisor(Outfit.VisorId, epc.Data.DefaultOutfit.ColorId);
                epc.SetSkin(Outfit.SkinId, epc.Data.DefaultOutfit.ColorId);
                epc.SetPet(Outfit.PetId, epc.Data.DefaultOutfit.ColorId);
                epc.SetColor(Outfit.ColorId);
            }
        }
        else
        {
            RpcSender.Infos.Clear();

            foreach (var pc in PlayerControl.AllPlayerControls)
            {
                RpcSender.Infos.Add(pc.PlayerId, pc.Data);

                pc.SetHat("", pc.Data.DefaultOutfit.ColorId);
                pc.SetVisor("", pc.Data.DefaultOutfit.ColorId);
                pc.SetSkin("", pc.Data.DefaultOutfit.ColorId);
                pc.SetPet("", pc.Data.DefaultOutfit.ColorId);
                pc.SetColor(6);
            }
        }
    }
}
