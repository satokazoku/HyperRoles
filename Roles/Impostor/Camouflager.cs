using HyperRoles.Modules;
using UnityEngine;

namespace HyperRoles.Roles.Impostor;

public class Camouflager : RoleBase
{
    public override string RoleName => GetString("Camouflager");
    public override string Intro => GetString("CamouflagerIntro");
    public override Color RoleColor => Palette.ImpostorRed;
    public override Team Team => Team.Impostor;
    public override Count CountType => Count.Impostor;
    public override bool CanVent => true;
    public override bool CanKill => true;
    float timer = 10f;
    bool NowUse;
    public override void OnFixedUpdate(PlayerControl player)
    {
        if (NowUse)
        {
            timer -= Time.fixedDeltaTime;
            if (timer <= 0f)
            {
                RpcSender.SendCamouflageRpc(true);
            }
        }
    }
}

public static class CamouflagerButton
{
    public static void Register()
    {
        CustomActionButton.Register(new CustomActionButton(
            label: Translator.GetString("CamouflagerText"),
            icon: CustomActionButton.LoadSprite("HyperRoles.Resources.Buttons.CamouflagerButton.png"),
            cooldown: 20f,
            isVisible: () => CustomRoleManager.TryGetRole(PlayerControl.LocalPlayer, out var role) && role is Camouflager,
            onClick: () =>
            {
                if (!RpcSender.NowCamouFlage)
                {
                    RpcSender.SendCamouflageRpc();
                }
            },
            canUse: () => PlayerControl.LocalPlayer.CanMove,
            key: () => KeyCode.F));
    }
}