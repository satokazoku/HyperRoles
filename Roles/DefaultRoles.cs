using UnityEngine;

namespace HyperRoles.Roles;

/// <summary>
/// 基本のクルーメイト。特別な役職が割り当てられなかったクルー側のプレイヤーが、これになる。
/// </summary>
public class DefaultCrewmate : RoleBase
{
    public override string RoleName => GetString("Crewmate");
    public override string Intro => GetString("CrewmateIntro");
    public override Color RoleColor => new Color32(140, 255, 255, 255);
    public override Team Team => Team.Crewmate;
    public override Count CountType => Count.Crewmate;
}

/// <summary>
/// 基本のインポスター。特別な役職が割り当てられなかったインポスター側のプレイヤーが、これになる。
/// キルはバニラのキルボタンに任せる(CanKill は false のまま)。
/// ベントは、自前の判定(VentCanUsePatch)が CanVent を見るので、true にしておく。
/// </summary>
public class DefaultImpostor : RoleBase
{
    public override string RoleName => GetString("Impostor");
    public override string Intro => GetString("ImpostorIntro");
    public override Color RoleColor => Palette.ImpostorRed;
    public override Team Team => Team.Impostor;
    public override Count CountType => Count.Impostor;
    public override bool CanVent => true;
    //public override bool CanKill => true;
}
