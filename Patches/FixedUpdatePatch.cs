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

    /// <summary>
    /// ゲーム開始時やリセット時に辞書を初期化する
    /// </summary>
    public static void Reset()
    {
        name.Clear();
        SetedNameplayers.Clear();

        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (p == null || p.Data == null || p.cosmetics?.nameText == null) continue;

            string originalName = string.IsNullOrEmpty(p.Data.PlayerName) ? p.cosmetics.nameText.text : p.Data.PlayerName;
            name[p.PlayerId] = originalName;
            SetedNameplayers.Add(p.PlayerId);

            p.cosmetics.nameText.text = $"{originalName}</color>";
        }
    }

    public static void Postfix(PlayerControl __instance)
    {
        if (__instance == null || __instance.Data == null || __instance.cosmetics?.nameText == null) return;

        if (!SetedNameplayers.Contains(__instance.PlayerId) || string.IsNullOrEmpty(name.GetValueOrDefault(__instance.PlayerId)) || name[__instance.PlayerId] == "???")
        {
            string cleanName = !string.IsNullOrEmpty(__instance.Data.PlayerName) ? __instance.Data.PlayerName : __instance.cosmetics.nameText.text;

            if (cleanName != "???")
            {
                name[__instance.PlayerId] = cleanName;
                SetedNameplayers.Add(__instance.PlayerId);
            }
            else
            {
                return;
            }
        }

        string rawName = name.GetValueOrDefault(__instance.PlayerId, __instance.Data.PlayerName);

        if (!GameStates.IsLobby && !GameStates.IsNotJoined && !GameStates.IsEnded &&
           (GameStates.IsOnlineGame || GameStates.IsLocalGame || GameStates.IsFreePlay))
        {
            var localPlayer = PlayerControl.LocalPlayer;
            if (localPlayer == null)
            {
                if (RpcSender.NowCamouFlage)
                {
                    __instance.cosmetics.nameText.text = "";
                }
                return;
            }
            if (localPlayer.PlayerId != __instance.PlayerId && RpcSender.NowCamouFlage)
            {
                __instance.cosmetics.nameText.text = "";
                return;
            }

            Count targetCount = GetPlayerCountType(__instance);
            Count localCount = GetPlayerCountType(localPlayer);

            if (__instance.PlayerId != localPlayer.PlayerId)
            {
                var jac = new Color32(0, 180, 235, 255);

                bool isBothJackal = targetCount == Count.Jackal && localCount == Count.Jackal;
                bool isBothImpostor = targetCount == Count.Impostor && localCount == Count.Impostor;

                string code = isBothJackal ? ColorUtility.ToHtmlStringRGB(jac) :
                             isBothImpostor ? ColorUtility.ToHtmlStringRGB(Palette.ImpostorRed) :
                             ColorUtility.ToHtmlStringRGB(Palette.White);
                __instance.cosmetics.nameText.text = $"<color=#{code}>{rawName}</color>";
                return;
            }

            if (CustomRoleManager.TryGetRole(__instance, out var role))
            {
                // カスタム役職の場合
                string hexRgb = "#" + ColorUtility.ToHtmlStringRGB(role.RoleColor);
                string progress = role.ProgressText(__instance);

                __instance.cosmetics.nameText.text = $"<color={hexRgb}><size=75%>{role.RoleName}{progress}</size>\n{rawName}</color>";
                role.OnFixedUpdate(__instance);
            }
            else
            {
                // バニラ役職（純インポスター / 純クルーなど）の場合
                Color selfColor = __instance.Data != null && __instance.Data.Role != null && __instance.Data.Role.IsImpostor
                    ? Palette.ImpostorRed
                    : Palette.White;

                var name = __instance.Data.Role.IsImpostor ? Translator.GetString("Impostor") : Translator.GetString("Crewmate");
                string hexRgb = "#" + ColorUtility.ToHtmlStringRGB(selfColor);

                if (!__instance.Data.Role.IsImpostor)
                {
                    string rgb = "#" + ColorUtility.ToHtmlStringRGB(Palette.CrewmateBlue);
                    name = $"<{rgb}>{name}";
                }
                else
                {
                    name = $"<{hexRgb}>{name}";
                }

                __instance.cosmetics.nameText.text = $"<color={hexRgb}><size=75%></color>{name}</size>\n</color><color={hexRgb}>{rawName}</color>";
            }

            if (!AmongUsClient.Instance.AmHost) return;
            CheckGameEnd.CheckEnd();
        }
        else
        {
            __instance.cosmetics.nameText.text = $"<#ffffff>{rawName}</color>";
        }
    }

    /// <summary>
    /// カスタム役職があればその CountType を返し、
    /// なければバニラの Data.Role.IsImpostor から CountType を判断する
    /// </summary>
    private static Count GetPlayerCountType(PlayerControl player)
    {
        if (player == null || player.Data == null) return Count.Crewmate;

        if (CustomRoleManager.TryGetRole(player, out var role))
        {
            return role.CountType;
        }

        if (player.Data.Role != null && player.Data.Role.IsImpostor)
        {
            return Count.Impostor;
        }

        return Count.Crewmate;
    }
}