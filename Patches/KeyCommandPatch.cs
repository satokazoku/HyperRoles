using AmongUs.GameOptions;
using HarmonyLib;
using Hazel;
using InnerNet;
using UnityEngine;

namespace HyperRoles
{
    class KeyCommandPatch
    {
        // 順番に関係なく、すべてのキーが揃った瞬間だけ検知する
        public static bool GetKeysDown(params KeyCode[] keys)
        {
            if (keys == null || keys.Length == 0) return false;

            bool anyKeyDownInThisFrame = false;

            foreach (var key in keys)
            {
                // 1つでも押されていないキーがあれば、その時点で不成立
                if (!Input.GetKey(key))
                {
                    return false;
                }

                // 現在のフレームで「新しく押されたキー」があるかチェック
                if (Input.GetKeyDown(key))
                {
                    anyKeyDownInThisFrame = true;
                }
            }

            // 「すべてのキーが押されている」かつ「どれか1つは今押された瞬間である」場合のみtrue
            return anyKeyDownInThisFrame;
        }
    }


    [HarmonyPatch(typeof(ControllerManager), nameof(ControllerManager.Update))]
    class ControllerManagerUpdatePatch
    {
        public static bool Haison = false;
        public static void Postfix(ControllerManager __instance)
        {
            // デバッグモード有効化
            if (KeyCommandPatch.GetKeysDown(KeyCode.LeftControl, KeyCode.Z, KeyCode.LeftShift))
            {
                Main.IsDebugMode = !Main.IsDebugMode;

                var text = $"デバッグモード:{Main.IsDebugMode}";
                if (DestroyableSingleton<HudManager>._instance)
                {
                    DestroyableSingleton<HudManager>.Instance.Notifier.AddDisconnectMessage(text);
                }
            }
            // デバッグモード状態表示
            if (KeyCommandPatch.GetKeysDown(KeyCode.LeftControl, KeyCode.X))
            {
                var text = $"デバッグモード:{Main.IsDebugMode}";
                if (DestroyableSingleton<HudManager>._instance)
                {
                    DestroyableSingleton<HudManager>.Instance.Notifier.AddDisconnectMessage(text);
                }
            }

            // ホスト専用処理
            if (!AmongUsClient.Instance.AmHost) return;

            // 廃村
            if (KeyCommandPatch.GetKeysDown(KeyCode.Return, KeyCode.L, KeyCode.LeftShift))
            {
                Haison = true;
                foreach (var p in PlayerControl.AllPlayerControls)
                {
                    p.RpcSetRole(RoleTypes.Crewmate, true);
                }
                GameManager.Instance.RpcEndGame(GameOverReason.CrewmateDisconnect, false);
            }

            // デバッグモード専用処理
            if (!Main.IsDebugMode) return;
            // 自殺
            if (KeyCommandPatch.GetKeysDown(KeyCode.LeftControl, KeyCode.K))
            {
                PlayerControl.LocalPlayer.RpcMurderPlayer(PlayerControl.LocalPlayer, true);
            }
            if (KeyCommandPatch.GetKeysDown(KeyCode.LeftControl, KeyCode.E, KeyCode.R))
            {
                PlayerControl.LocalPlayer.Data.IsDead = false;
            }
            if (KeyCommandPatch.GetKeysDown(KeyCode.LeftControl, KeyCode.G))
            {
                PlayerControl.LocalPlayer.Exiled();
                MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)RpcCalls.Exiled, SendOption.None, -1);
                AmongUsClient.Instance.FinishRpcImmediately(writer);
            }

            // ゲームを終了しない有効化
            if (KeyCommandPatch.GetKeysDown(KeyCode.LeftControl, KeyCode.C, KeyCode.LeftShift))
            {
                Main.NoEnd = !Main.NoEnd;

                var text = $"ゲームを終了しない:{Main.NoEnd}";
                if (DestroyableSingleton<HudManager>._instance)
                {
                    DestroyableSingleton<HudManager>.Instance.Notifier.AddDisconnectMessage(text);
                }
            }
            // ゲームを終了しない状態表示
            if (KeyCommandPatch.GetKeysDown(KeyCode.LeftControl, KeyCode.V))
            {
                var text = $"ゲームを終了しない:{Main.NoEnd}";
                if (DestroyableSingleton<HudManager>._instance)
                {
                    DestroyableSingleton<HudManager>.Instance.Notifier.AddDisconnectMessage(text);
                }
            }
            /*if (KeyCommandPatch.GetKeysDown(KeyCode.LeftControl, KeyCode.O))
            {
                CustomRoleManager.SetRole(PlayerControl.LocalPlayer.PlayerId, CustomRoleManager.RoleId.SerialKiller);
            }*/
        }
    }
}
