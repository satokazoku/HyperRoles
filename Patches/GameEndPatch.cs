using HarmonyLib;

namespace HyperRoles.Patches;

[HarmonyPatch(typeof(LogicGameFlowNormal), nameof(LogicGameFlowNormal.CheckEndCriteria))]
class GameEndChecker
{
    public static bool Prefix()
    {
        return false;
    }
}