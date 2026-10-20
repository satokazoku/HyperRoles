using UnityEngine;

namespace HyperRoles.Roles.Crewmate;

public class Sheriff : RoleBase
{
    public override string RoleName => GetString("Sheriff");
    public override string Intro => GetString("SheriffIntro");
    public override Color RoleColor => new Color32(248, 205, 70, 255);
    public override Team Team => Team.Crewmate;
    public override Count CountType => Count.Crewmate;
    public override bool CanKill => true;
    public override float KillCoolDown => 1f;
    public override bool CanVent => false;
    public override bool ShowUseButton => false;
    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (!CustomRoleManager.TryGetRole(killer, out var role)) return false;
        if (!CustomRoleManager.TryGetRole(target, out var targetrole)) return false;
        if (killer.PlayerId != PlayerControl.LocalPlayer.PlayerId) return true;
        if (killer.PlayerId == PlayerControl.LocalPlayer.PlayerId && target.PlayerId == PlayerControl.LocalPlayer.PlayerId) return true;
        if (PlayerControl.LocalPlayer.Data.IsDead) return false;

        if (targetrole.Team != Team.Crewmate)
        {
            return true;
        }
        PlayerControl.LocalPlayer.MurderPlayer(killer, MurderResultFlags.Succeeded);
        PlayerControl.LocalPlayer.RpcMurderPlayer(killer, true);
        return false;
    }
}