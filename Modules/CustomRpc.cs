using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Hazel;
using HyperRoles.Roles.Neutral;

namespace HyperRoles
{
    /// <summary>
    /// 自前RPCの呼び出しID。バニラのRpcCallsと被らない値を使う。
    /// </summary>
    public enum CustomRpc : byte
    {
        SyncRoles = 100,
        TargetAbility,
        Camouflage,
        EndCamouFlage,
        MurderPlayer,
        RoleRpc,
        Invisible,
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

            // ローカル側でも即時反映
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

        public static void SendInvisibleRpc(bool end = false)
        {
            var me = PlayerControl.LocalPlayer;
            if (end && Main.InvisiblePlayerids.Contains(me.PlayerId))
            {
                Main.InvisiblePlayerids.Remove(me.PlayerId);
            }
            else if (!Main.InvisiblePlayerids.Contains(me.PlayerId))
            {
                Main.InvisiblePlayerids.Add(me.PlayerId);
            }

            var writer = AmongUsClient.Instance.StartRpcImmediately(
                me.NetId,
                (byte)CustomRpc.Invisible,
                SendOption.Reliable,
                -1);

            writer.Write(me.PlayerId);
            writer.Write(end);

            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        public static void ExMurderPlayer(this PlayerControl killer, PlayerControl target)
        {
            killer.MurderPlayer(target, MurderResultFlags.Succeeded);
            var writer = AmongUsClient.Instance.StartRpcImmediately(
                PlayerControl.LocalPlayer.NetId,
                (byte)CustomRpc.MurderPlayer,
                SendOption.Reliable,
                -1);

            writer.Write(killer.PlayerId);
            writer.Write(target.PlayerId);

            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
    }

    /// <summary>
    /// 全RPCの受信口。PlayerControl.HandleRpc が呼ばれるたびに、自前のIDだけ拾う。
    /// </summary>
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.HandleRpc))]
    static class HandleRpcPatch
    {
        public static void Postfix(
            PlayerControl __instance,
            [HarmonyArgument(0)] byte callId,
            [HarmonyArgument(1)] MessageReader reader)
        {
            var list = PlayerControl.AllPlayerControls;

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
                case CustomRpc.MurderPlayer:
                    var killerid = reader.ReadByte();
                    var targetid = reader.ReadByte();

                    PlayerControl killer;
                    PlayerControl target;

                    if (list[killerid] != null) killer = list[killerid];
                    else killer = null;

                    if (list[targetid] != null) target = list[targetid];
                    else return;

                    killer.MurderPlayer(target, MurderResultFlags.Succeeded);
                    break;
                case CustomRpc.Invisible:
                    var me = reader.ReadByte();
                    var end = reader.ReadBoolean();

                    if (end && Main.InvisiblePlayerids.Contains(me))
                    {
                        Main.InvisiblePlayerids.Remove(me);
                    }
                    else if (!Main.InvisiblePlayerids.Contains(me))
                    {
                        Main.InvisiblePlayerids.Add(me);
                    }

                    break;
                case CustomRpc.RoleRpc:
                    var role = (CustomRoleManager.RoleId)reader.ReadByte();
                    switch (role)
                    {
                        case CustomRoleManager.RoleId.Jackal:
                            var targ = reader.ReadByte();
                            var user = reader.ReadByte();

                            PlayerControl userpc;
                            PlayerControl targetpc;

                            if (list[targ] != null) userpc = list[targ];
                            else return;

                            if (list[user] != null) targetpc = list[user];
                            else return;

                            if (CustomRoleManager.TryGetRole(targetpc, out var sk) && sk is Sidekick)
                            {
                                var skclass = (Sidekick)sk;
                                skclass.SetParent(userpc);
                            }
                            break;
                    }
                    break;
            }
        }

        // 衣装データを安全に保存するためのクラス/構造体
        public class SavedOutfit
        {
            public string HatId;
            public string VisorId;
            public string SkinId;
            public string PetId;
            public int ColorId;
        }

        public static Dictionary<byte, SavedOutfit> SavedOutfits = new();

        public static void Camouflage(bool end)
        {
            if (end)
            {
                // 保存しておいた元の見た目を完全に復元する
                foreach (var kvp in SavedOutfits)
                {
                    var pc = PlayerControl.AllPlayerControls.ToArray().FirstOrDefault(x => x.PlayerId == kvp.Key);
                    if (pc == null) continue;

                    var outfit = kvp.Value;
                    pc.SetHat(outfit.HatId, outfit.ColorId);
                    pc.SetVisor(outfit.VisorId, outfit.ColorId);
                    pc.SetSkin(outfit.SkinId, outfit.ColorId);
                    pc.SetPet(outfit.PetId, outfit.ColorId);
                    pc.SetColor(outfit.ColorId);
                }
                SavedOutfits.Clear();
                RpcSender.Infos.Clear();
            }
            else
            {
                SavedOutfits.Clear();
                RpcSender.Infos.Clear();

                foreach (var pc in PlayerControl.AllPlayerControls)
                {
                    RpcSender.Infos.Add(pc.PlayerId, pc.Data);

                    // 現在の見た目を保存しておく
                    SavedOutfits[pc.PlayerId] = new SavedOutfit
                    {
                        HatId = pc.Data.DefaultOutfit.HatId,
                        VisorId = pc.Data.DefaultOutfit.VisorId,
                        SkinId = pc.Data.DefaultOutfit.SkinId,
                        PetId = pc.Data.DefaultOutfit.PetId,
                        ColorId = pc.Data.DefaultOutfit.ColorId
                    };

                    pc.SetHat("", 6);
                    pc.SetVisor("", 6);
                    pc.SetSkin("", 6);
                    pc.SetPet("", 6);
                    pc.SetColor(6);
                }
            }
        }
    }
}