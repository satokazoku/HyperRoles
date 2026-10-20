using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using HarmonyLib;
using HyperRoles.Roles;

namespace HyperRoles.Modules;

public static class CheckGameEnd
{
    public static Team WinnerTeam = Team.none;
    public static CustomRoleManager.RoleId WinnerRole = CustomRoleManager.RoleId.Unknown;

    static readonly BepInEx.Logging.ManualLogSource log =
        BepInEx.Logging.Logger.CreateLogSource("CheckGameEnd");

    /// <summary>終了処理を一度だけ実行するためのフラグ</summary>
    static bool ended;
    static bool warnedMissingRole;

    /// <summary>イントロが終わったか。イントロ中は終了判定をしない</summary>
    public static bool IntroFinished;

    /// <summary>全滅(生存者0人)で終了したか。終了画面が読む</summary>
    public static bool AllDied;

    /// <summary>勝者のプレイヤーID(終了画面に勝者のキャラクターを出すために使う)</summary>
    public static readonly HashSet<byte> WinnerIds = new();

    /// <summary>新しいゲームの開始時(イントロの開始時)に呼ぶ</summary>
    public static void Reset()
    {
        ended = false;
        warnedMissingRole = false;
        IntroFinished = false;
        AllDied = false;
        WinnerIds.Clear();
        WinnerTeam = Team.none;
        WinnerRole = CustomRoleManager.RoleId.Unknown;
    }

    public static void CheckEnd()
    {
        if (ended) return;
        if (!IntroFinished) return; // イントロ中は判定しない
        if (!IntroCoBeginPatch.RoleAssigned) return;
        if (Main.NoEnd) return;

        int imp = 0;
        int crew = 0;
        int jackal = 0;

        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (p == null || p.Data == null) continue;
            if (p.Data.IsDead) continue;

            // 役職が取れない生存者がいる間は判定しない。
            // (スキップして数えないと、全員0人=全滅と誤判定してしまう)
            if (!CustomRoleManager.TryGetRole(p, out var role))
            {
                if (!warnedMissingRole)
                {
                    log.LogWarning($"[GameEnd] role missing for PlayerId={p.PlayerId}, skip check");
                    warnedMissingRole = true;
                }
                return;
            }

            switch (role.CountType)
            {
                case Count.Crewmate:
                    ++crew;
                    break;
                case Count.Impostor:
                    ++imp;
                    break;
                case Count.Jackal:
                    ++jackal;
                    break;
                case Count.none:
                    break;

                //↓カウントが何らかの理由で取れないときのみ実行
                default:
                    //試合終わると困るんで。
                    if (p.PlayerId == 0) ++imp;
                    else ++crew;
                    break;
            }
        }

        // else if でつなぐ。上から順に、最初に当てはまったものだけを実行する。
        if (imp == 0 && jackal == 0 && crew == 0)
        {
            // 全滅
            End(Team.none, CustomRoleManager.RoleId.Unknown, GameOverReason.ImpostorsByKill,
                winnersAsImpostor: false, isWinnerSide: _ => false, imp, crew, jackal);
        }
        else if (jackal == 0 && imp >= crew)
        {
            // インポスター勝利
            End(Team.Impostor, CustomRoleManager.RoleId.Unknown, GameOverReason.ImpostorsByKill,
                winnersAsImpostor: true, isWinnerSide: r => r.CountType == Count.Impostor, imp, crew, jackal);
        }
        else if (imp == 0 && jackal > 0 && jackal >= crew)
        {
            // ジャッカル勝利
            End(Team.Neutral, CustomRoleManager.RoleId.Jackal, GameOverReason.ImpostorsByKill,
                winnersAsImpostor: true, isWinnerSide: r => r.CountType == Count.Jackal, imp, crew, jackal);
        }
        else if (imp == 0 && jackal == 0 && crew > 0)
        {
            // クルーメイト勝利
            End(Team.Crewmate, CustomRoleManager.RoleId.Unknown, GameOverReason.CrewmatesByVote,
                winnersAsImpostor: false, isWinnerSide: r => r.Team == Team.Crewmate, imp, crew, jackal);
        }
    }

    /// <summary>
    /// 終了処理。バニラの終了画面は「勝者=インポスター側/クルー側」で作られるため、
    /// 勝者側の役職タイプをそろえてから RpcEndGame を呼ぶ。
    /// winnersAsImpostor が true なら、勝者をImpostor・敗者をCrewmateにする(falseはその逆)。
    /// </summary>
    static void End(Team winner, CustomRoleManager.RoleId winnerRole, GameOverReason reason,
        bool winnersAsImpostor, Func<RoleBase, bool> isWinnerSide, int imp, int crew, int jackal)
    {
        ended = true;
        AllDied = winner == Team.none;
        log.LogInfo($"[GameEnd] winner={winner}, imp={imp}, crew={crew}, jackal={jackal}");

        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (p == null || p.Data == null) continue;

            bool dead = p.Data.IsDead;

            if (winner == Team.none)
            {
                p.RpcSetRole(RoleTypes.CrewmateGhost, true);
                continue;
            }

            bool onWinnerSide = CustomRoleManager.TryGetRole(p, out var role) && isWinnerSide(role);
            bool asImpostor = onWinnerSide == winnersAsImpostor;

            RoleTypes type = asImpostor
                ? (dead ? RoleTypes.ImpostorGhost : RoleTypes.Impostor)
                : (dead ? RoleTypes.CrewmateGhost : RoleTypes.Crewmate);
            p.RpcSetRole(type, true);

            if (onWinnerSide) WinnerIds.Add(p.PlayerId);
        }

        WinnerTeam = winner;
        WinnerRole = winnerRole;
        GameManager.Instance.RpcEndGame(reason, false);
    }
}

/// <summary>
/// イントロの開始時に、終了判定の状態を戻す(前のゲームの状態を持ち越さない)。
/// </summary>
[HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.CoBegin))]
static class CheckGameEndIntroStartPatch
{
    public static void Prefix() => CheckGameEnd.Reset();
}

/// <summary>
/// イントロが終わったら、終了判定を始める。
/// </summary>
[HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.OnDestroy))]
static class CheckGameEndIntroEndPatch
{
    public static void Postfix() => CheckGameEnd.IntroFinished = true;
}

/// <summary>
/// 終了画面の勝者(キャラクター表示と、勝利/敗北の判定に使われる)を、自前の結果で作り直す。
/// バニラの勝者リストはインポスター/クルーの区別で作られるので、独自の勝利陣営は入らない。
/// </summary>
[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameEnd))]
static class EndGameWinnersPatch
{
    static readonly BepInEx.Logging.ManualLogSource log =
        BepInEx.Logging.Logger.CreateLogSource("EndGameWinners");

    public static void Postfix()
    {
        var winners = new Il2CppSystem.Collections.Generic.List<CachedPlayerData>();
        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (p == null || p.Data == null) continue;
            if (!CheckGameEnd.WinnerIds.Contains(p.PlayerId)) continue;

            winners.Add(new CachedPlayerData(p.Data));
        }
        EndGameResult.CachedWinners = winners;
        log.LogInfo($"[EndGame] winners={winners.Count}, ids={string.Join(",", CheckGameEnd.WinnerIds)}");
    }
}