using HarmonyLib;
using System.Globalization;

namespace TownOfHost
{
    [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.MakePublic))]
    class DisiblePublicRoomPatch
    {
        public static bool Prefix(GameStartManager __instance)
        {
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
}