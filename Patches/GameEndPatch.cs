using HarmonyLib;

namespace HyperRoles.Modules
{
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.ShouldCheckForGameEnd), MethodType.Getter)]
    static class GameManagerShouldCheckForGameEndPatch
    {
        public static void Postfix(ref bool __result)
        {
            if (!CheckGameEnd.IntroFinished)
            {
                __result = false;
            }
        }
    }
    [HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.Awake))]
    static class ShipStatusAwakeResetPatch
    {
        public static void Prefix()
        {
            CheckGameEnd.Reset();
        }
    }
    [HarmonyPatch(typeof(LogicGameFlowNormal), nameof(LogicGameFlowNormal.CheckEndCriteria))]
    class GameEndChecker
    {
        public static bool Prefix()
        {
            return false;
        }
    }
}