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

    static readonly BepInEx.Logging.ManualLogSource log =
        BepInEx.Logging.Logger.CreateLogSource("RoleAssign");

    /// <summary>
    /// インポだけわける
    /// </summary>
    static Dictionary<RoleId, int> ImpAssingn = new()
    {
        [RoleId.Camouflager] = 1,
        [RoleId.SerialKiller] = 1,
    };
    /// <summary>
    /// 設定がないので仮アサイン用
    /// Key：役職　Value：出現数
    /// </summary>
    static Dictionary<RoleId, int> AssignRoles = new()
    {
        [RoleId.MadMate] = 1,
        [RoleId.Sheriff] = 1,
        [RoleId.Jackal] = 1,
    };

    public enum RoleId : byte
    {
        Unknown = 0,
        //imp
        Impostor,
        SerialKiller,
        Camouflager,
        SilentKiller,
        Ninja,
        MadMate,

        //crew
        Crewmate,
        Sheriff,
        Bakery,

        //neutral
        Jackal,
        Sidekick,
    }
    static readonly Dictionary<RoleId, Func<RoleBase>> Factories = new()
    {
        [RoleId.Impostor] = () => new DefaultImpostor(),
        [RoleId.SerialKiller] = () => new SerialKiller(),
        [RoleId.Camouflager] = () => new Camouflager(),
        [RoleId.SilentKiller] = () => new SilentKiller(),
        [RoleId.Ninja] = () => new Ninja(),
        [RoleId.MadMate] = () => new MadMate(),

        [RoleId.Crewmate] = () => new DefaultCrewmate(),
        [RoleId.Sheriff] = () => new Sheriff(),
        [RoleId.Bakery] = () => new Bakery(),

        [RoleId.Jackal] = () => new Jackal(),
        [RoleId.Sidekick] = () => new Sidekick(),
    };
    public static RoleId GetId(RoleBase role) => role switch
    {
        DefaultImpostor => RoleId.Impostor,
        SerialKiller => RoleId.SerialKiller,
        Camouflager => RoleId.Camouflager,
        SilentKiller => RoleId.SilentKiller,
        Ninja => RoleId.Ninja,
        MadMate => RoleId.MadMate,

        DefaultCrewmate => RoleId.Crewmate,
        Sheriff => RoleId.Sheriff,
        Bakery => RoleId.Bakery,

        Jackal => RoleId.Jackal,
        Sidekick => RoleId.Sidekick,
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

    /// <summary>
    /// 役職の見本を作る(設定の定義や、陣営を調べるために使う。プレイヤーには割り当てない)。
    /// 登録されていない役職IDなら null。
    /// </summary>
    public static RoleBase CreateRole(RoleId id)
        => Factories.TryGetValue(id, out var create) ? create() : null;

    /// <summary>
    /// 設定画面に並べる、割り当ての対象になる役職の一覧。
    /// 基本の役職(Crewmate / Impostor)と、能力で作るサイドキックは、含めない。
    /// </summary>
    public static IEnumerable<RoleId> AssignableRoles()
    {
        foreach (var id in Factories.Keys)
        {
            if (id is RoleId.Unknown or RoleId.Crewmate or RoleId.Impostor or RoleId.Sidekick) continue;
            yield return id;
        }
    }

    // この役職が入る枠(インポスター側 / クルー側)。
    // このクラスには、Count という名前のプロパティがあるので、列挙型は、名前空間をつけて書く。
    static Dictionary<RoleId, int> PoolFor(RoleId id)
        => CreateRole(id)?.CountType == HyperRoles.Roles.Count.Impostor ? ImpAssingn : AssignRoles;

    /// <summary>その役職の最大数(1試合に、最大で何人に割り当てるか)</summary>
    public static int GetMaxCount(RoleId id)
        => PoolFor(id).TryGetValue(id, out var count) ? count : 0;

    /// <summary>その役職の最大数を決める。0 以下なら、その役職は割り当てない。</summary>
    public static void SetMaxCount(RoleId id, int count)
    {
        var pool = PoolFor(id);
        if (count <= 0) pool.Remove(id);
        else pool[id] = count;
    }

    public static void SetRole(byte playerId, RoleId id)
    {
        // 未登録のID(Unknown など)は、基本のクルーメイトにする。
        // (以前は Factories[RoleId.Unknown] を引いていたが、Unknown は登録されていないので、例外になっていた)
        if (!Factories.TryGetValue(id, out var create))
        {
            create = Factories[RoleId.Crewmate];
        }

        // プレイヤーIDから探す。AllPlayerControls の並び順は、IDの順とは限らない
        // (以前は AllPlayerControls[playerId] と、IDを位置として使っていたので、別のプレイヤーを引くことがあった)
        PlayerControl pc = null;
        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (p != null && p.PlayerId == playerId)
            {
                pc = p;
                break;
            }
        }

        if (pc != null && TryGetRole(pc, out var dr))
        {
            dr.OnDestroy();
        }

        AllPlayerRoles[playerId] = create();

        if (pc != null && TryGetRole(pc, out var role))
        {
            role.Add();
        }
    }

    /// <summary>
    /// 最大数の枠を、役職ごとの出現率(設定画面で決める)で抽選して、実際に使う枠の数にする。
    /// 例: 最大数2、出現率50% → 0〜2人(それぞれ50%の確率で出る)
    /// </summary>
    static Dictionary<RoleId, int> ApplySpawnRate(Dictionary<RoleId, int> pool)
    {
        var result = new Dictionary<RoleId, int>();
        foreach (var pair in pool)
        {
            int rate = RoleSettings.SpawnRate(pair.Key);
            int count = 0;
            for (int i = 0; i < pair.Value; i++)
            {
                if (Random.Shared.Next(100) < rate) count++;
            }
            if (count > 0) result[pair.Key] = count;
        }
        return result;
    }

    /// <summary>
    /// ホストが役職を割り当てる。
    /// 設定した枠(AssignRoles / ImpAssingn)が足りなくなっても、全員に必ず役職を割り当てる
    /// (足りない分は、基本のクルーメイト / 基本のインポスター)。
    /// </summary>
    public static void RoleAssign()
    {
        AllPlayerRoles.Clear();

        // 毎試合使い切ってしまわないよう、設定のコピーを作成してそれを使用する
        // 設定画面の「出現率」を、ここで反映する(最大数の枠ごとに、出現率で抽選して、外れた枠は使わない)
        var assignRolesCopy = ApplySpawnRate(AssignRoles);
        var impAssignCopy = ApplySpawnRate(ImpAssingn);

        var apc = new List<PlayerControl>();

        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (p != null && p.Data != null)
            {
                apc.Add(p);
            }
        }

        // プレイヤーリストをシャッフル
        int n = apc.Count;
        while (n > 1)
        {
            n--;
            int k = Random.Shared.Next(n + 1);
            (apc[k], apc[n]) = (apc[n], apc[k]);
        }

        int numImpostors = Main.NormalOptions?.NumImpostors ?? 1;
        log.LogInfo($"[RoleAssign] players={apc.Count}, numImpostors(setting)={numImpostors}");

        // インポスター数が人数以上だと、全員がインポスター側になり、開始直後にゲームが終わってしまう。
        // 少なくとも1人は、インポスター側以外にする。
        // (全員をインポスター側にしたい場合は、この2行を消す)
        if (numImpostors >= apc.Count) numImpostors = Math.Max(0, apc.Count - 1);

        int assignedImps = 0;

        foreach (var p in apc)
        {
            RoleId? defaultImpostorRole = null;
            foreach (var key in impAssignCopy.Keys) { defaultImpostorRole = key; break; }

            RoleId? defaultCrewRole = null;
            foreach (var key in assignRolesCopy.Keys) { defaultCrewRole = key; break; }

            // 前試合のバニラ役職データに依存せず、設定されたインポスター数に基づいて上から順に割り当てる
            bool shouldBeImpostor = assignedImps < numImpostors;

            if (shouldBeImpostor)
            {
                if (defaultImpostorRole.HasValue)
                {
                    var roleId = defaultImpostorRole.Value;
                    SetRole(p.PlayerId, roleId);

                    impAssignCopy[roleId] -= 1;
                    if (impAssignCopy[roleId] <= 0)
                    {
                        impAssignCopy.Remove(roleId);
                    }
                }
                else
                {
                    // インポスターの枠が足りない: 基本のインポスター
                    SetRole(p.PlayerId, RoleId.Impostor);
                }
                assignedImps++;
            }
            else
            {
                if (defaultCrewRole.HasValue)
                {
                    var roleId = defaultCrewRole.Value;
                    SetRole(p.PlayerId, roleId);

                    assignRolesCopy[roleId] -= 1;
                    if (assignRolesCopy[roleId] <= 0)
                    {
                        assignRolesCopy.Remove(roleId);
                    }
                }
                else
                {
                    // クルーの枠が足りない: 基本のクルーメイト
                    SetRole(p.PlayerId, RoleId.Crewmate);
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