using System.Collections.Generic;
using AmongUs.GameOptions;
using Hazel;
using HyperRoles.Modules;
using UnityEngine;

namespace HyperRoles.Roles.Neutral;

public class Jackal : RoleBase
{
    public override string RoleName => GetString("Jackal");
    public override string Intro => GetString("JackalIntro");
    public override Color RoleColor => new Color32(0, 180, 235, 255);
    public override Team Team => Team.Neutral;
    public override Count CountType => Count.Jackal;
    public override bool CanKill => true;
    public override float KillCoolDown => 1f;
    public override bool CanVent => true;
    public bool CanSidekick = true;
}
public static class JackalSidekickButton
{
    /// <summary>能力のクールダウン(秒)</summary>
    const float AbilityCooldown = 20f;

    static readonly BepInEx.Logging.ManualLogSource log =
        BepInEx.Logging.Logger.CreateLogSource("TargetAbility");

    /// <summary>ホスト側で持つ、各プレイヤーの最後の使用時刻</summary>
    static readonly Dictionary<byte, float> lastUse = new();

    /// <summary>プラグインの Load() から一度だけ呼ぶ</summary>
    public static void Register()
    {
        CustomActionButton.Register(new CustomActionButton(
            label: Translator.GetString("Sidekick"),
            icon: CustomActionButton.LoadSprite("HyperRoles.Resources.Buttons.SideKick.png"),
            cooldown: AbilityCooldown,

            isVisible: () => CustomRoleManager.TryGetRole(PlayerControl.LocalPlayer, out var role)
                            && role is Jackal jackal && jackal.CanSidekick
                            ,

            canUse: () => PlayerControl.LocalPlayer.CanMove && CurrentTarget() != null,

            onClick: () =>
            {
                var target = CurrentTarget();
                if (target == null) return;
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
                    jackal.CanSidekick = false;
                    target.RpcSetRole(RoleTypes.Crewmate, true);
                    CustomRoleManager.SetRole(target.PlayerId, CustomRoleManager.RoleId.Sidekick);
                    if (CustomRoleManager.TryGetRole(target, out var sk))
                    {
                        if (CustomRoleManager.GetId(sk) == CustomRoleManager.RoleId.Sidekick)
                        {
                            var skclass = (Sidekick)sk;
                            skclass.SetParent(user);
                        }
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