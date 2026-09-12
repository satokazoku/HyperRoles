using HarmonyLib;
using HyperRoles;
using UnityEngine;
using System.Threading.Tasks;

namespace HyperRoles
{
    class KeyCommandPatch
    {
        public static bool GetKeysDown(params KeyCode[] keys)
        {
            foreach (var key in keys)
            {
                if (!Input.GetKey(key))
                {
                    return false;
                }
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(ControllerManager), nameof(ControllerManager.Update))]
    class ControllerManagerUpdatePatch
    {
        static bool CanUseDebugcmd = true;
        static bool CanUseDebugShowcmd = true;
        static bool Useddebugcmd = false;
        static bool UsedKillcmd = false;
        public static void Postfix(ControllerManager __instance)
        {
            // デバッグモード有効化
            if (CanUseDebugcmd && KeyCommandPatch.GetKeysDown(KeyCode.LeftControl, KeyCode.Z, KeyCode.LeftShift))
            {
                Main.IsDebugMode = !Main.IsDebugMode;

                CanUseDebugcmd = false;
                Useddebugcmd = true;
                var text = $"デバッグモード:{Main.IsDebugMode}";
                if (DestroyableSingleton<HudManager>._instance)
                {
                    DestroyableSingleton<HudManager>.Instance.Notifier.AddDisconnectMessage(text);
                }

                Task.Run(async () =>
                {
                    await Task.Delay(1000);
                    CanUseDebugcmd = true;
                });
            }
            // デバッグモード状態表示
            if (!Useddebugcmd && CanUseDebugShowcmd && KeyCommandPatch.GetKeysDown(KeyCode.LeftControl, KeyCode.Z))
            {
                CanUseDebugShowcmd = false;

                var text = $"デバッグモード:{Main.IsDebugMode}";
                if (DestroyableSingleton<HudManager>._instance)
                {
                    DestroyableSingleton<HudManager>.Instance.Notifier.AddDisconnectMessage(text);
                }
                Task.Run(async () =>
                {
                    await Task.Delay(1000);
                    CanUseDebugShowcmd = true;
                });
            }

            // ホスト専用処理
            if (!AmongUsClient.Instance.AmHost) return;

            // 廃村
            if (KeyCommandPatch.GetKeysDown(KeyCode.Return, KeyCode.L, KeyCode.LeftShift))
            {
                GameManager.Instance.RpcEndGame(GameOverReason.ImpostorDisconnect, false);
            }

            // デバッグモード専用処理
            if (!Main.IsDebugMode) return;
            // 自殺
            if (!UsedKillcmd && KeyCommandPatch.GetKeysDown(KeyCode.LeftControl, KeyCode.K))
            {
                PlayerControl.LocalPlayer.RpcMurderPlayer(PlayerControl.LocalPlayer, true);
                UsedKillcmd = true;
                Task.Run(async () =>
                {
                    await Task.Delay(1000);
                    UsedKillcmd = false;
                });
            }
        }
    }
}
