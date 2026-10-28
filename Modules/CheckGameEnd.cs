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
        IntroFinished = false;
        AllDied = false;
        WinnerIds.Clear();
        WinnerTeam = Team.none;
        WinnerRole = CustomRoleManager.RoleId.Unknown;
    }
    static bool SabotageTimedOut()
    {
        var ship = ShipStatus.Instance;
        if (ship == null || ship.Systems == null) return false;

        if (ship.Systems.TryGetValue(SystemTypes.LifeSupp, out var lifeSupp))
        {
            var oxygen = lifeSupp.TryCast<LifeSuppSystemType>();
            if (oxygen != null && oxygen.Countdown < 0f) return true;
        }

        if (ship.Systems.TryGetValue(SystemTypes.Reactor, out var reactorSystem))
        {
            var reactor = reactorSystem.TryCast<ReactorSystemType>();
            if (reactor != null && reactor.Countdown < 0f) return true;
        }

        if (ship.Systems.TryGetValue(SystemTypes.Laboratory, out var laboratorySystem))
        {
            var laboratory = laboratorySystem.TryCast<ReactorSystemType>();
            if (laboratory != null && laboratory.Countdown < 0f) return true;
        }

        if (ship.Systems.TryGetValue(SystemTypes.HeliSabotage, out var heliSystem))
        {
            var heli = heliSystem.TryCast<HeliSabotageSystem>();
            if (heli != null && heli.Countdown < 0f) return true;
        }

        return false;
    }

    public static void CheckEnd()
    {
        if (ended || Main.NoEnd) return;
        if (!IntroFinished || !IntroCoBeginPatch.RoleAssigned) return; // イントロ中は判定しない
        if (PlayerControl.AllPlayerControls == null || PlayerControl.AllPlayerControls.Count == 0) return;

        // サボタージュ(酸素・リアクターなど)のカウントダウンが0になったら、インポスターの勝ち。
        // バニラの終了判定を止めているので、ここで自前で確認する。
        if (SabotageTimedOut())
        {
            foreach (var pc in PlayerControl.AllPlayerControls)
            {
                if (pc.Data.IsDead || pc.Data.Role.IsImpostor) continue;
                pc.ExMurderPlayer(pc);
            }
            End(Team.Impostor, CustomRoleManager.RoleId.Unknown, GameOverReason.ImpostorsBySabotage,
                winnersAsImpostor: true, isWinnerSide: r => r.Team == Team.Impostor, 0, 0, 0);
            return;
        }

        int imp = 0;
        int crew = 0;
        int jackal = 0;
        bool _return = false;

        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (p == null || p.Data == null) continue;
            if (p.Data.IsDead) continue;

            bool nullrole = false;

            if (!CustomRoleManager.TryGetRole(p, out var role))
            {
                if (p.Data.Role.IsImpostor) ++imp;
                else ++crew;
                nullrole = true;
            }

            if (!nullrole)
            {
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
                        _return = true;
                        break;
                }
            }
        }

        if (_return) return;

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
                winnersAsImpostor: true, isWinnerSide: r => r.Team == Team.Impostor, imp, crew, jackal);
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

    static void End(Team winner, CustomRoleManager.RoleId winnerRole, GameOverReason reason,
            bool winnersAsImpostor, Func<RoleBase, bool> isWinnerSide, int imp, int crew, int jackal)
    {
        if (ended) return; // 二重実行防止
        ended = true;
        AllDied = winner == Team.none;
        log.LogInfo($"[GameEnd] winner={winner}, imp={imp}, crew={crew}, jackal={jackal}");

        WinnerIds.Clear();

        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (p == null || p.Data == null) continue;

            bool onWinnerSide = CustomRoleManager.TryGetRole(p, out var role) && isWinnerSide(role);
            if (onWinnerSide)
            {
                WinnerIds.Add(p.PlayerId);
            }
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