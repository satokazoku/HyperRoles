using System;
using System.Collections.Generic;
using HyperRoles.Modules;
using UnityEngine;

namespace HyperRoles.Roles.Impostor;

public class SerialKiller : RoleBase
{
    private static readonly Dictionary<int, RoleOptionDef> Opt = new()
    {
        [2] = new(() => GetString("KillCool"), 20f, 0f, 60f, 2.5f),
        [3] = new(() => GetString("SerialKillerLimit"), 60f, 0f, 180f, 2.5f),
    };
    public override Dictionary<int, RoleOptionDef> Options => Opt;
    public override string RoleName => GetString("SerialKiller");
    public override string Intro => GetString("SerialKillerIntro");
    public override Color RoleColor => Palette.ImpostorRed;
    public override Team Team => Team.Impostor;
    public override Count CountType => Count.Impostor;
    public override bool CanVent => true;
    public override bool CanKill => true;
    public override bool HasTask => false;
    public override float KillCoolDown => GetOptionValue(CustomRoleManager.RoleId.SerialKiller, 2, 1f);
    float Timer = 0f;
    private float? _limit;
    public float Limit
    {
        get => _limit ??= GetOptionValue(CustomRoleManager.RoleId.SerialKiller, 3, 1f);
        set => _limit = value;
    }
    bool HasKilled = false;
    public override void OnFixedUpdate(PlayerControl player)
    {
        if (player.Data.IsDead || !HasKilled) return;
        Timer += Time.fixedDeltaTime;
        if (Timer >= Limit && !player.Data.IsDead)
        {
            player.ExMurderPlayer(player);
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