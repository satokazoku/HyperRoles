using AmongUs.GameOptions;
using UnityEngine;

namespace HyperRoles.Roles.Neutral;

public class Sidekick : RoleBase
{
    public override string RoleName => GetString("Sidekick");
    public override string Intro => GetString("SidekickIntro");
    public override Color RoleColor => new Color32(0, 180, 235, 255);
    public override Team Team => Team.Neutral;
    public override Count CountType => Count.Jackal;
    public override bool CanVent => true;
    public override bool ShowUseButton => false;
    public PlayerControl Parent;
    public void SetParent(PlayerControl parent)
    {
        Parent = parent;
    }
    public override void OnFixedUpdate(PlayerControl player)
    {
        if (Parent.Data.IsDead && !player.Data.IsDead)
        {
            player.RpcSetRole(RoleTypes.Crewmate, true);
            CustomRoleManager.SetRole(player.PlayerId, CustomRoleManager.RoleId.Jackal);
        }
    }
}