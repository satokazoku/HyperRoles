using System;
using System.Collections.Generic;
using HyperRoles.Roles;
using HyperRoles.Roles.Crewmate;
using HyperRoles.Roles.Impostor;
using HyperRoles.Roles.Neutral;

namespace HyperRoles;

public static class CustomRoleManager
{
    static readonly Dictionary<byte, RoleBase> AllPlayerRoles = new();

    public enum RoleId : byte
    {
        Unknown = 0,
        Sheriff,
        SerialKiller,
        Jackal,
        Sidekick,
        Camouflager,
        SilentKiller,
    }
    static readonly Dictionary<RoleId, Func<RoleBase>> Factories = new()
    {
        [RoleId.Sheriff] = () => new Sheriff(),
        [RoleId.SerialKiller] = () => new SerialKiller(),
        [RoleId.Jackal] = () => new Jackal(),
        [RoleId.Sidekick] = () => new Sidekick(),
        [RoleId.Camouflager] = () => new Camouflager(),
        [RoleId.SilentKiller] = () => new SilentKiller(),
    };
    public static RoleId GetId(RoleBase role) => role switch
    {
        Sheriff => RoleId.Sheriff,
        SerialKiller => RoleId.SerialKiller,
        Jackal => RoleId.Jackal,
        Sidekick => RoleId.Sidekick,
        Camouflager => RoleId.Camouflager,
        SilentKiller => RoleId.SilentKiller,
        _ => RoleId.Unknown,
    };

    public static int Count => AllPlayerRoles.Count;

    public static bool TryGetRole(PlayerControl player, out RoleBase role)
    {
        role = null;
        return player != null
            && AllPlayerRoles.TryGetValue(player.PlayerId, out role)
            && role != null;
    }

    public static void ClearRoles() => AllPlayerRoles.Clear();

    public static void SetRole(byte playerId, RoleId id)
    {
        if (!Factories.TryGetValue(id, out var create))
        {
            create = Factories[RoleId.Unknown];
        }
        if (PlayerControl.AllPlayerControls.Count > playerId)
        {
            var pc = PlayerControl.AllPlayerControls[playerId];
            if (TryGetRole(pc, out var dr))
            {
                dr.OnDestroy();
            }

            AllPlayerRoles[playerId] = create();

            if (TryGetRole(pc, out var role))
            {
                role.Add();
            }
        }
        else
        {
            AllPlayerRoles[playerId] = create();
        }
    }

    /// <summary>
    /// ホストが役職を割り当てる。
    /// </summary>
    public static void RoleAssign()
    {
        AllPlayerRoles.Clear();
        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (p.Data.Role.IsImpostor)
            {
                SetRole(p.PlayerId, RoleId.SilentKiller);
            }
            else
            {
                if (p.PlayerId < 5)
                {
                    SetRole(p.PlayerId, RoleId.Jackal);
                }
                else
                {
                    SetRole(p.PlayerId, RoleId.Sheriff);
                }
            }
        }
        RpcSender.SendSyncRoles();
    }

    /// <summary>
    /// RPCで送るために、全員の役職IDを取り出す。
    /// </summary>
    public static Dictionary<byte, RoleId> GetAllRoleIds()
    {
        var result = new Dictionary<byte, RoleId>();
        foreach (var kv in AllPlayerRoles)
        {
            if (kv.Value == null) continue;
            result[kv.Key] = GetId(kv.Value);
        }
        return result;
    }
}