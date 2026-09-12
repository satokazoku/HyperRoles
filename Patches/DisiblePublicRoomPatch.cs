using System.Globalization;
using HarmonyLib;
using HyperRoles;

namespace TownOfHost
{
    [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.MakePublic))]
    class DisiblePublicRoomPatch
    {
        public static bool Prefix(GameStartManager __instance)
        {
            if (Main.AllowPublicRoom)
            {
                return true;
            }
            var text = "Public rooms are not available.";

            if (CultureInfo.CurrentCulture.Name == "ja-JP")
            {
                text = "公開ルームは使用できません";
            }
            if (DestroyableSingleton<HudManager>._instance)
            {
                DestroyableSingleton<HudManager>.Instance.Notifier.AddDisconnectMessage(text);
            }
            return false;
        }
    }
    [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.Start))]
    public class GameStartManagerStartPatch
    {
        public static void Postfix(GameStartManager __instance)
        {
            __instance.MinPlayers = 1;
        }
    }
}
