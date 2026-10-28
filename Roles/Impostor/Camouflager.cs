using System.Collections.Generic;
using HyperRoles.Modules;
using UnityEngine;
using RoleId = HyperRoles.CustomRoleManager.RoleId;

namespace HyperRoles.Roles.Impostor;

public class Camouflager : RoleBase
{
    private static readonly Dictionary<int, RoleOptionDef> Opt = new()
    {
        [2] = new(() => GetString("Cooldown"), 30f, 0f, 60f, 2.5f),
        [3] = new(() => GetString("Duration"), 10f, 2.5f, 60f, 2.5f),
    };
    public override Dictionary<int, RoleOptionDef> Options => Opt;
    public override string RoleName => GetString("Camouflager");
    public override string Intro => GetString("CamouflagerIntro");
    public override Color RoleColor => Palette.ImpostorRed;
    public override Team Team => Team.Impostor;
    public override Count CountType => Count.Impostor;
    public override bool CanVent => true;
    public override bool CanKill => true;
    public override bool HasTask => false;
}

public static class CamouflagerButton
{
    static readonly BepInEx.Logging.ManualLogSource log =
        BepInEx.Logging.Logger.CreateLogSource("CamouflagerButton");

    private static float GetCooldown()
    {
        var setting = RoleSettings.Get(RoleId.Camouflager);
        var template = CustomRoleManager.CreateRole(RoleId.Camouflager);

        if (setting == null)
        {
            log.LogWarning("[CamouflagerButton] RoleSettings.Get(RoleId.Camouflager) is null!");
            return 30f;
        }

        // 保存されている設定値をすべてログに出力して確認
        log.LogInfo($"[CamouflagerButton] Setting Values Count: {setting.Values.Count}");
        foreach (var kvp in setting.Values)
        {
            log.LogInfo($"[CamouflagerButton] Stored Option ID {kvp.Key} = {kvp.Value}");
        }

        if (setting.Values.TryGetValue(2, out var val))
        {
            log.LogInfo($"[CamouflagerButton] Found custom cooldown: {val}");
            return val;
        }

        if (template != null && template.Options.TryGetValue(2, out var def))
        {
            log.LogInfo($"[CamouflagerButton] Using default cooldown: {def.Default}");
            return def.Default;
        }

        return 30f;
    }

    private static float GetDuration()
    {
        var setting = RoleSettings.Get(RoleId.Camouflager);
        var template = CustomRoleManager.CreateRole(RoleId.Camouflager);

        if (setting != null && setting.Values.TryGetValue(3, out var val))
        {
            log.LogInfo($"[CamouflagerButton] Found custom duration: {val}");
            return val;
        }

        if (template != null && template.Options.TryGetValue(3, out var def))
        {
            log.LogInfo($"[CamouflagerButton] Using default duration: {def.Default}");
            return def.Default;
        }

        return 10f;
    }

    public static void Register()
    {
        CustomActionButton.Register(new CustomActionButton(
            label: () => Translator.GetString("CamouflagerText"),
            icon: CustomActionButton.LoadSprite("HyperRoles.Resources.Buttons.CamouflagerButton.png"),
            cooldown: () => GetCooldown(),
            isVisible: () => CustomRoleManager.TryGetRole(PlayerControl.LocalPlayer, out var role) && role is Camouflager,
            onClick: () =>
            {
                if (!RpcSender.NowCamouFlage)
                {
                    RpcSender.SendCamouflageRpc();
                }
            },
            canUse: () => PlayerControl.LocalPlayer.CanMove && !RpcSender.NowCamouFlage,
            key: () => KeyCode.F,
            duration: () => GetDuration(),
            onEffectEnd: () =>
            {
                RpcSender.SendCamouflageRpc(true);
            }));
    }
}