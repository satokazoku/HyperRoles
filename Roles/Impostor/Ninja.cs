using System.Collections.Generic;
using HyperRoles.Modules;
using UnityEngine;
using static HyperRoles.CustomRoleManager;

namespace HyperRoles.Roles.Impostor;

public class Ninja : RoleBase
{
    private static readonly Dictionary<int, RoleOptionDef> Opt = new()
    {
        [2] = new(() => GetString("KillCool"), 30f, 0f, 60f, 2.5f),
        [3] = new(() => GetString("Cooldown"), 30f, 0f, 60f, 2.5f),
        [4] = new(() => GetString("Duration"), 10f, 2.5f, 60f, 2.5f),
        [5] = new(() => GetString("NinjaInvisKillcool"), 0f, 0f, 60f, 0.5f),
        [6] = new(() => GetString("NinjaVisKillcool"), 0f, 0f, 60f, 0.5f),
        [7] = new(() => GetString("MaxKillcool"), 45f, 0f, 180f, 2.5f),
    };
    public override Dictionary<int, RoleOptionDef> Options => Opt;
    public override string RoleName => GetString("Ninja");
    public override string Intro => GetString("NinjaIntro");
    public override Color RoleColor => Palette.ImpostorRed;
    public override Team Team => Team.Impostor;
    public override Count CountType => Count.Impostor;
    public override bool CanVent => true;
    public override bool CanKill => true;
    public override bool HasTask => false;
    public override float KillCoolDown => Killcool < MaxKillcool ? Killcool : MaxKillcool;
    private float? _max;
    public float MaxKillcool
    {
        get => _max ??= GetOptionValue(RoleId.Ninja, 7, Main.NormalOptions.KillCooldown);
        set => _max = value;
    }
    private float? _Killcool;
    public float Killcool
    {
        get => _Killcool ??= GetOptionValue(RoleId.Ninja, 2, Main.NormalOptions.KillCooldown);
        set => _Killcool = value;
    }
    bool IsInvisible() => Main.InvisiblePlayerids.Contains(PlayerControl.LocalPlayer.PlayerId);
    
    public override void OnMurderPlayer(PlayerControl killer, PlayerControl target, PlayerControl player)
    {
        var me = PlayerControl.LocalPlayer;
        if (killer.PlayerId != player.PlayerId || target.PlayerId == player.PlayerId || player.PlayerId != me.PlayerId || !IsInvisible()) return;

        var p = GetOptionValue(RoleId.Ninja, 5, 0f);
        Killcool += p;
    }
}
public static class NinjaButton
{
    public static void Register()
    {
        CustomActionButton.Register(new CustomActionButton(
            label: () => Translator.GetString("NinjaText"),
            icon: CustomActionButton.LoadSprite("HyperRoles.Resources.Buttons.NinjaButton.png"),
            cooldown: () => RoleBase.GetOptionValue(RoleId.Ninja, 3, 30f),
            isVisible: () => TryGetRole(PlayerControl.LocalPlayer, out var role) && role is Ninja,
            onClick: () =>
            {
                RpcSender.SendInvisibleRpc();
            },
            canUse: () => PlayerControl.LocalPlayer.CanMove && !RpcSender.NowCamouFlage,
            key: () => KeyCode.F,
            duration: () => RoleBase.GetOptionValue(RoleId.Ninja, 4, 10f),
            onEffectEnd: () =>
            {
                RpcSender.SendInvisibleRpc(true);
                if (TryGetRole(PlayerControl.LocalPlayer, out var ninja) && ninja is Ninja)
                {
                    var ninjaclass = (Ninja)ninja;
                    if (ninjaclass is null) return;

                    var p = RoleBase.GetOptionValue(RoleId.Ninja, 6, 0f);
                    ninjaclass.Killcool += p;
                }
            }));
    }
}