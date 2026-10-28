using System.Collections.Generic;
using AmongUs.GameOptions;
using Hazel;
using HyperRoles.Modules;
using MS.Internal.Xml.XPath;
using UnityEngine;

namespace HyperRoles.Roles.Neutral;

public class Jackal : RoleBase
{
    private static readonly Dictionary<int, RoleOptionDef> Opt = new()
    {
        [2] = new(() => GetString("KillCool"), 30f, 0f, 60f, 2.5f),
        [3] = new(() => GetString("JackalSidekickCooldown"), 30f, 0f, 60f, 2.5f),
        [4] = new(() => GetString("JackalSideKickLimit"), 1f, 0f, 99f, 1f),
    };
    public override Dictionary<int, RoleOptionDef> Options => Opt;
    public override string RoleName => GetString("Jackal");
    public override string Intro => GetString("JackalIntro");
    public override Color RoleColor => new Color32(0, 180, 235, 255);
    public override Team Team => Team.Neutral;
    public override Count CountType => Count.Jackal;
    public override bool CanKill => true;
    public override float KillCoolDown => GetOptionValue(CustomRoleManager.RoleId.Jackal, 2, 30f);
    public override bool HasTask => false;
    public override bool CanVent => true;
    private int? _sk;
    public int sk
    {
        get => _sk ??= (int)GetOptionValue(CustomRoleManager.RoleId.Jackal, 4, 1f);
        set => _sk = value;
    }
}
public static class JackalSidekickButton
{
    static float AbilityCooldown => RoleBase.GetOptionValue(CustomRoleManager.RoleId.Jackal, 3, 30f);

    static readonly BepInEx.Logging.ManualLogSource log =
        BepInEx.Logging.Logger.CreateLogSource("TargetAbility");

    /// <summary>ホスト側で持つ、各プレイヤーの最後の使用時刻</summary>
    static readonly Dictionary<byte, float> lastUse = new();

    /// <summary>プラグインの Load() から一度だけ呼ぶ</summary>
    public static void Register()
    {
        CustomActionButton.Register(new CustomActionButton(
            label: () => Translator.GetString("Sidekick"),
            icon: CustomActionButton.LoadSprite("HyperRoles.Resources.Buttons.SideKick.png"),
            cooldown: AbilityCooldown,

            isVisible: () => CustomRoleManager.TryGetRole(PlayerControl.LocalPlayer, out var role)
                            && role is Jackal jackal && jackal.sk > 0
                            ,

            canUse: () => PlayerControl.LocalPlayer.CanMove && CurrentTarget() != null,

            onClick: () =>
            {
                var p = PlayerControl.LocalPlayer;
                if (!CustomRoleManager.TryGetRole(p, out var role) || role is not Jackal) return;
                var jac = (Jackal)role;

                var target = CurrentTarget();
                if (target == null) return;
                jac.sk--;
                SendRequest(target);
            },
            key: () => KeyCode.F));
    }

    static PlayerControl CurrentTarget()
    {
        var me = PlayerControl.LocalPlayer;
        return me == null ? null : AllKill.FindTarget(me);
    }

    static void SendRequest(PlayerControl target)
    {
        var me = PlayerControl.LocalPlayer;

        if (AmongUsClient.Instance.AmHost)
        {
            Execute(me, target);
            return;
        }

        var writer = AmongUsClient.Instance.StartRpcImmediately(
            me.NetId,
            (byte)CustomRpc.TargetAbility,
            SendOption.Reliable,
            AmongUsClient.Instance.HostId);
        writer.Write(target.PlayerId);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRequest(PlayerControl sender, MessageReader reader)
    {
        byte targetId = reader.ReadByte();
        if (!AmongUsClient.Instance.AmHost) return;

        Execute(sender, FindPlayer(targetId));
    }

    static void Execute(PlayerControl user, PlayerControl target)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (user == null || target == null) return;
        if (user.Data == null || target.Data == null) return;
        if (user.Data.IsDead || target.Data.IsDead) return;
        if (user.PlayerId == target.PlayerId) return;
        if (MeetingHud.Instance != null) return;

        // 味方(同じ陣営)には使えない
        if (!AllKill.CanKillTarget(user, target)) return;

        // 距離
        float dist = Vector2.Distance(user.GetTruePosition(), target.GetTruePosition());
        if (dist > AllKill.Range + 1f) return;

        // クールダウン
        float last = lastUse.TryGetValue(user.PlayerId, out var t) ? t : AllKill.GameStartTime;
        if (Time.time - last < AbilityCooldown - 0.5f) return;
        lastUse[user.PlayerId] = Time.time;

        log.LogInfo($"[TargetAbility] {user.PlayerId} -> {target.PlayerId}");

        if (!target.Data.IsDead && !user.Data.IsDead)
        {
            if (CustomRoleManager.TryGetRole(user, out var role))
            {
                if (CustomRoleManager.GetId(role) is CustomRoleManager.RoleId.Jackal)
                {
                    var jackal = (Jackal)role;
                    target.RpcSetRole(RoleTypes.Crewmate, true);
                    CustomRoleManager.SetRole(target.PlayerId, CustomRoleManager.RoleId.Sidekick);
                    var writer = AmongUsClient.Instance.StartRpcImmediately(
                        PlayerControl.LocalPlayer.NetId,
                        (byte)CustomRpc.RoleRpc,
                        SendOption.Reliable,
                        -1);

                    writer.Write((byte)CustomRoleManager.RoleId.Jackal);
                    writer.Write(target.PlayerId);
                    writer.Write(user.PlayerId);
                    AmongUsClient.Instance.FinishRpcImmediately(writer);

                    if (CustomRoleManager.TryGetRole(target, out var sk) && sk is Sidekick)
                    {
                        var skclass = (Sidekick)sk;
                        skclass.SetParent(user);
                    }
                }
            }
        }
    }

    static PlayerControl FindPlayer(byte id)
    {
        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (p != null && p.PlayerId == id) return p;
        }
        return null;
    }
    public static void Reset() => lastUse.Clear();
}