using HarmonyLib;
using HyperRoles;

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
class MurderPlayerPatch
{
    public static void Postfix(PlayerControl __instance, PlayerControl target)
    {
        // ターゲットが正常にキルされて死亡しているかチェック
        if (target == null || !target.Data.IsDead) return;

        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (CustomRoleManager.TryGetRole(p, out var role))
            {
                role.OnMurderPlayer(__instance, target, p);
            }
        }
    }
}
