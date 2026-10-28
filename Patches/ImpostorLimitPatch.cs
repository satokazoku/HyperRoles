using AmongUs.GameOptions;
using HarmonyLib;
using UnityEngine;

namespace HyperRoles;

public static class ImpostorLimit
{
    public const int Min = 1;
    public const int Max = 99;
}

/// <summary>
/// 役職割り当て時の「人数に応じたインポスター数の上限」を無効にする。
/// バニラは RoleManager.SelectRoles の中で GetAdjustedNumImpostors(プレイヤー数) を呼び、
/// 人数ごとの上限で丸めている。設定値をそのまま(0〜99)返すようにする。
/// </summary>
[HarmonyPatch(typeof(IGameOptionsExtensions), nameof(IGameOptionsExtensions.GetAdjustedNumImpostors))]
static class GetAdjustedNumImpostorsPatch
{
    // 引数名はゲームのバージョンで変わりうるので、位置指定(__0)で受ける
    public static bool Prefix(IGameOptions __0, ref int __result)
    {
        __result = Mathf.Clamp(__0.NumImpostors, ImpostorLimit.Min, ImpostorLimit.Max);
        return false; // 元の処理を実行しない
    }
}

/// <summary>
/// ロビーの設定画面で、インポスター数のスライダーの範囲を 0〜99 にする。
/// </summary>
[HarmonyPatch(typeof(NumberOption), nameof(NumberOption.Initialize))]
static class ImpostorCountRangePatch
{
    public static void Postfix(NumberOption __instance)
    {
        if (__instance.floatOptionName == FloatOptionNames.KillCooldown)
        {
            __instance.ValidRange = new FloatRange(2.5f, 180f);
        }
        if (__instance.intOptionName == Int32OptionNames.NumImpostors)
        {
            __instance.ValidRange = new FloatRange(ImpostorLimit.Min, ImpostorLimit.Max);
        }
    }
}
