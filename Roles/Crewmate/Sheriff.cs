using System.Collections.Generic;
using UnityEngine;

namespace HyperRoles.Roles.Crewmate;

public class Sheriff : RoleBase
{
    private static readonly Dictionary<int, RoleOptionDef> Opt = new()
    {
        [2] = new(() => GetString("KillCool"), 30f, 0f, 60f, 2.5f),
        [3] = new(() => GetString("SheriffShotLimit"), 5f, 1f, 99f, 1f),
    };
    public override Dictionary<int, RoleOptionDef> Options => Opt;
    public override string RoleName => GetString("Sheriff");
    public override string Intro => GetString("SheriffIntro");
    public override Color RoleColor => new Color32(248, 205, 70, 255);
    public override Team Team => Team.Crewmate;
    public override Count CountType => Count.Crewmate;
    public override bool CanKill => true;
    public override float KillCoolDown => GetOptionValue(CustomRoleManager.RoleId.Sheriff, 2, 30f);
    public override bool CanVent => false;
    private int? _shot;
    public int shot
    {
        get => _shot ??= (int)GetOptionValue(CustomRoleManager.RoleId.Jackal, 4, 1f);
        set => _shot = value;
    }
    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        var pc = PlayerControl.LocalPlayer;
        if (!CustomRoleManager.TryGetRole(killer, out var role)) return true;
        if (role is not Sheriff) return true;
        if (!CustomRoleManager.TryGetRole(target, out var targetrole)) return true;
        if (killer.PlayerId != pc.PlayerId) return true;
        if (killer.PlayerId == pc.PlayerId && target.PlayerId == pc.PlayerId) return true;
        if (shot <= 0) return false;

        --shot;
        if (targetrole.Team != Team.Crewmate)
        {
            return true;
        }
        killer.ExMurderPlayer(killer);
        return false;
    }
    public override string ProgressText(PlayerControl player)
        => shot > 0 ? $"({shot})" : "";
}