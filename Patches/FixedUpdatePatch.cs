using System.Collections.Generic;
using HarmonyLib;
using HyperRoles.Modules;
using HyperRoles.Roles;
using UnityEngine;

namespace HyperRoles.Patches;

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
class FixedUpdatePatch
{
    public static Dictionary<byte, string> name = new();
    static HashSet<byte> SetedNameplayers = new();
    public static void Reset()
    {
        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (!SetedNameplayers.Contains(p.PlayerId))
            {
                name[p.PlayerId] = p.cosmetics.nameText.text;
            }
            p.cosmetics.nameText.text = $"{name[p.PlayerId]}</color>";
        }
    }
    public static void Postfix(PlayerControl __instance)
    {
        if (!GameStates.IsLobby && !GameStates.IsNotJoined && !GameStates.IsEnded && (GameStates.IsOnlineGame || GameStates.IsLocalGame || GameStates.IsFreePlay))
        {
            if (CustomRoleManager.TryGetRole(__instance, out var role))
            {
                if (__instance.PlayerId != PlayerControl.LocalPlayer.PlayerId)
                {
                    if (CustomRoleManager.TryGetRole(PlayerControl.LocalPlayer, out var prole))
                    {
                        var jac = new Color32(0, 180, 235, 255);
                        string code =
                            role.CountType == Count.Jackal && prole.CountType == Count.Jackal ?
                            ColorUtility.ToHtmlStringRGB(jac) :
                            role.CountType == Count.Impostor && prole.CountType == Count.Impostor ?
                            ColorUtility.ToHtmlStringRGB(Palette.ImpostorRed)
                            : ColorUtility.ToHtmlStringRGB(Palette.White);


                        string Rgb = "#" + code;

                        __instance.cosmetics.nameText.text = $"<color={Rgb}>{name[__instance.PlayerId]}</color>";
                        if (role.CountType != Count.Jackal || prole.CountType != Count.Jackal)
                        {
                            return;
                        }
                    }
                    else
                    {
                        return;
                    }
                }
                if (!SetedNameplayers.Contains(__instance.PlayerId))
                {
                    name[__instance.PlayerId] = __instance.cosmetics.nameText.text;
                    SetedNameplayers.Add(__instance.PlayerId);
                }
                string hexRgb = "#" + ColorUtility.ToHtmlStringRGB(role.RoleColor);
                string progress = role.ProgressText(__instance);

                __instance.cosmetics.nameText.text = $"<color={hexRgb}><size=75%>{role.RoleName}{progress}</size>\n{name[__instance.PlayerId]}</color>";
                
                role.OnFixedUpdate(__instance);
            }
            if (!AmongUsClient.Instance.AmHost) return;
            CheckGameEnd.CheckEnd();
        }
        else
        {
            if (!SetedNameplayers.Contains(__instance.PlayerId))
            {
                name[__instance.PlayerId] = __instance.cosmetics.nameText.text;
            }
            __instance.cosmetics.nameText.text = $"{name[__instance.PlayerId]}</color>";
        }
    }
}


/// <summary>
/// PlayerControl.FixedUpdate の直後にも、キルボタンの対象とクールダウンを更新する。
/// バニラの FixedUpdate が、ベントできない/キルできない役職のボタン状態を戻してしまい、
/// HudManager.Update(AllKillHudPatch)とのタイミング次第で、押したときに対象が空になるのを防ぐ。
/// </summary>
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
static class AllKillFixedUpdatePatch
{
    public static void Postfix(PlayerControl __instance)
    {
        if (__instance == null || !__instance.AmOwner) return;
        if (!AllKill.CanShowButton()) return;

        var button = HudManager.Instance.KillButton;
        if (button == null) return;

        button.SetTarget(AllKill.FindTarget(__instance));
        button.SetCoolDown(AllKill.LocalTimer, AllKill.Cooldown());
    }
}

/// <summary>
/// キルボタンが押された瞬間に、対象とクールダウンをもう一度セットし直す。
/// あわせて、押したときの状態をログに出す(原因の切り分け用)。
/// </summary>
[HarmonyPatch(typeof(KillButton), nameof(KillButton.DoClick))]
static class KillButtonDoClickPatch
{
    public static void Prefix(ActionButton __instance)
    {
        var me = PlayerControl.LocalPlayer;
        if (me == null || !AllKill.CanShowButton()) return;

        var kill = HudManager.Instance?.KillButton;
        if (kill == null || __instance.Pointer != kill.Pointer) return;

        var target = AllKill.FindTarget(me);
        AllKill.Log.LogInfo(
            $"[AllKill] click: target={(target != null ? target.PlayerId.ToString() : "none")}, " +
            $"timer={AllKill.LocalTimer:F2}, canMove={me.CanMove}");

        kill.SetTarget(target);
        kill.SetCoolDown(AllKill.LocalTimer, AllKill.Cooldown());
    }
}