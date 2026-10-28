using System.Collections.Generic;
using UnityEngine;

namespace HyperRoles.Roles.Crewmate;

public class Bakery : RoleBase
{
    public override string RoleName => GetString("Bakery");
    public override string Intro => GetString("BakeryIntro");
    public override Color RoleColor => new Color32(127, 255, 0, 255);
    public override Team Team => Team.Crewmate;
    public override Count CountType => Count.Crewmate;
}