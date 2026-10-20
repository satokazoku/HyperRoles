using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace HyperRoles.Roles.Impostor;

public class SilentKiller : RoleBase
{
    public override string RoleName => GetString("SilentKiller");
    public override string Intro => GetString("SilentKillerIntro");
    public override Color RoleColor => Palette.ImpostorRed;
    public override Team Team => Team.Impostor;
    public override Count CountType => Count.Impostor;
    public override From FromMod => From.TownOfHost_Pko;
    public override bool CanVent => true;
    public override bool CanKill => true;
    public override float KillCoolDown => 1f;
    public override bool ShowUseButton => false;
    public static float StopTime = 5f;
    public static Dictionary<byte, float> StoppedPlayers = new();
    public static Dictionary<byte, Vector2> AllPlayerpositions = new();
    public override void Add()
    {
        StoppedPlayers.Clear();
        AllPlayerpositions.Clear();
    }
    public override void OnFixedUpdate(PlayerControl player)
    {
        if (player.Data.IsDead) return;

        foreach (var pc in PlayerControl.AllPlayerControls.ToArray().Where(p => !p.Data.IsDead && p.PlayerId != PlayerControl.LocalPlayer.PlayerId))
        {
            if (!AllPlayerpositions.ContainsKey(pc.PlayerId))
            {
                AllPlayerpositions[pc.PlayerId] = pc.transform.position;
                continue;
            }
            var old = AllPlayerpositions[pc.PlayerId];
            var pos = pc.transform.position;
            var distance = Vector2.Distance(old, pos);
            AllPlayerpositions[pc.PlayerId] = pc.transform.position;

            if (distance < 0.01f)
            {
                if (StoppedPlayers.ContainsKey(pc.PlayerId))
                {
                    StoppedPlayers[pc.PlayerId] = StoppedPlayers[pc.PlayerId] += Time.fixedDeltaTime;
                }
                else
                {
                    StoppedPlayers[pc.PlayerId] = Time.fixedDeltaTime;
                }
            }
            else
            {
                StoppedPlayers.Remove(pc.PlayerId);
            }
        }
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
public static class SilentKillerInvisPatch
{
    static void Postfix(PlayerControl __instance)
    {
        if (__instance == null) return;
        if (__instance.PlayerId == PlayerControl.LocalPlayer.PlayerId)
        {
            __instance.Visible = true;
            return;
        }
        if (!CustomRoleManager.TryGetRole(PlayerControl.LocalPlayer, out var role))
        {
            __instance.Visible = true;
            return;
        }
        if (role is not SilentKiller)
        {
            __instance.Visible = true;
            return;
        }

        if (!SilentKiller.StoppedPlayers.TryGetValue(__instance.PlayerId, out var time))
        {
            __instance.Visible = false;
            return;
        }
        if (time < SilentKiller.StopTime)
        {
            __instance.Visible = false;
            return;
        }
        __instance.Visible = true;
    }
}