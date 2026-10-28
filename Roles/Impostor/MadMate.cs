using UnityEngine;

namespace HyperRoles.Roles.Impostor;

public class MadMate : RoleBase
{
    public override string RoleName => GetString("MadMate");
    public override string Intro => GetString("MadMateIntro");
    public override Color RoleColor => Palette.ImpostorRed;
    public override Team Team => Team.Impostor;
    public override Count CountType => Count.Crewmate;
    public override bool CanVent => true;
    public override bool HasTask => false;
}