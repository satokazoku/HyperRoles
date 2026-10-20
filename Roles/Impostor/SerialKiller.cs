using System;
using UnityEngine;

namespace HyperRoles.Roles.Impostor;

public class SerialKiller : RoleBase
{
    public override string RoleName => GetString("SerialKiller");
    public override string Intro => GetString("SerialKillerIntro");
    public override Color RoleColor => Palette.ImpostorRed;
    public override Team Team => Team.Impostor;
    public override Count CountType => Count.Impostor;
    public override bool CanVent => true;
    public override bool CanKill => true;
    float Timer = 0f;
    float Limit = 20f;
    bool HasKilled = false;
    public override void OnFixedUpdate(PlayerControl player)
    {
        if (player.Data.IsDead || !HasKilled) return;
        Timer += Time.fixedDeltaTime;
        if (Timer >= Limit && !player.Data.IsDead)
        {
            player.RpcMurderPlayer(player, true);
        }
    }
    public override void OnMurderPlayer(PlayerControl killer, PlayerControl target, PlayerControl player)
    {
        if (killer.PlayerId != player.PlayerId) return;
        HasKilled = true;
        Timer = 0f;
    }
    public override string ProgressText(PlayerControl player)
    {
        if (player.Data.IsDead) return "";

        var text = Math.Truncate(Limit - Timer);
        return $"({text})";
    }
}