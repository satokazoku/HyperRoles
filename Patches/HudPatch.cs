using System.Collections.Generic;
using HarmonyLib;
using HyperRoles.Roles;
using UnityEngine;

namespace HyperRoles;

public static class AllKill
{
    /// <summary>指定したプレイヤーの役職の、キルクールダウン</summary>
    public static float Cooldown(PlayerControl player)
    {
        if (player != null && CustomRoleManager.TryGetRole(player, out var role))
        {
            return role.KillCoolDown;
        }
        return Main.NormalOptions.KillCooldown;
    }

    /// <summary>自分の役職の、キルクールダウン(ボタンの表示用)</summary>
    public static float Cooldown() => Cooldown(PlayerControl.LocalPlayer);

    public static float Range => Main.NormalOptions.KillDistance;
    public static float LocalTimer;
    public static readonly Dictionary<byte, float> LastKillTime = new();
    public static float GameStartTime;
    public static readonly BepInEx.Logging.ManualLogSource Log =
        BepInEx.Logging.Logger.CreateLogSource("AllKill");

    /// <summary>キルボタンを出してよい状況か</summary>
    public static bool CanShowButton()
    {
        var me = PlayerControl.LocalPlayer;
        if (me == null || me.Data == null || me.Data.IsDead) return false;
        if (ShipStatus.Instance == null) return false;   // ロビー等
        if (MeetingHud.Instance != null) return false;   // 会議中
        if (HudManager.Instance == null) return false;
        if (CustomRoleManager.TryGetRole(me, out var role))
        {
            return role.CanKill;
        }
        return false;
    }

    /// <summary>
    /// killer が target をキルしてよいか。
    /// インポスター同士、ジャッカル同士(同じ陣営同士)はキルできない。
    /// 役職が取れない場合は、判断できないので許可する(ホスト側でも同じ判定をする)。
    /// </summary>
    public static bool CanKillTarget(PlayerControl killer, PlayerControl target)
    {
        if (!CustomRoleManager.TryGetRole(killer, out var killerRole)) return true;
        if (!CustomRoleManager.TryGetRole(target, out var targetRole)) return true;

        if (killerRole.CountType == Count.Impostor && targetRole.CountType == Count.Impostor) return false;
        if (killerRole.CountType == Count.Jackal && targetRole.CountType == Count.Jackal) return false;

        return target.Visible;
    }

    /// <summary>近くのキル対象を探す</summary>
    public static PlayerControl FindTarget(PlayerControl me)
    {
        PlayerControl found = null;
        float best = Range;
        var myPos = me.GetTruePosition();

        foreach (var p in PlayerControl.AllPlayerControls.ToArray())
        {
            if (p == null || p.PlayerId == me.PlayerId) continue;
            if (p.Data == null || p.Data.IsDead || p.Data.Disconnected) continue;
            if (p.inVent) continue;
            if (!CanKillTarget(me, p)) continue; // 味方(同じ陣営)は対象にしない
            if (!p.Visible) continue;

            float d = Vector2.Distance(myPos, p.GetTruePosition());
            if (d < best)
            {
                best = d;
                found = p;
            }
        }
        return found;
    }
}

/// <summary>
/// 毎フレーム、キルボタンを表示して、対象とクールダウンを更新する。
/// </summary>
[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
static class AllKillHudPatch
{
    public static void Postfix(HudManager __instance)
    {
        var button = __instance.KillButton;
        if (button == null) return;

        if (!AllKill.CanShowButton())
        {
            return; // 非表示のときはバニラの制御に任せる
        }

        var me = PlayerControl.LocalPlayer;

        if (AllKill.LocalTimer > 0f)
            AllKill.LocalTimer = Mathf.Max(0f, AllKill.LocalTimer - Time.deltaTime);

        button.ToggleVisible(true);
        button.SetTarget(AllKill.FindTarget(me));
        button.SetCoolDown(AllKill.LocalTimer, AllKill.Cooldown());
    }
}

/// <summary>
/// ホスト側のキル判定を差し替える。バニラは「インポスターだけ」を通すので、役職を問わず通す。
/// キルが通らなかったときは、理由をログに出す(どの条件で止まったかを調べるため)。
/// </summary>
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CheckMurder))]
static class AllKillCheckMurderPatch
{
    static bool Reject(string reason, PlayerControl killer, PlayerControl target)
    {
        AllKill.Log.LogInfo($"[AllKill] rejected ({reason}): {killer?.PlayerId} -> {target?.PlayerId}");
        return false;
    }

    public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target)
    {
        if (!AmongUsClient.Instance.AmHost) return true;

        var killer = __instance;
        if (killer == null || target == null) return Reject("null", killer, target);
        if (killer.Data == null || target.Data == null) return Reject("no data", killer, target);
        if (killer.Data.IsDead || killer.Data.Disconnected) return Reject("killer dead", killer, target);
        if (target.Data.IsDead || target.Data.Disconnected) return Reject("target dead", killer, target);
        if (killer.PlayerId == target.PlayerId) return Reject("self", killer, target);
        if (MeetingHud.Instance != null) return Reject("meeting", killer, target);

        // 同じ陣営(インポスター同士、ジャッカル同士)はキルできない
        if (!AllKill.CanKillTarget(killer, target)) return Reject("same team / invisible", killer, target);
        if (CustomRoleManager.TryGetRole(killer, out var irole) && !irole.OnCheckMurder(killer, target)) return Reject("killer role OnCheckMurder", killer, target);
        if (CustomRoleManager.TryGetRole(target, out var trole) && !trole.OnCheckMurder(killer, target)) return Reject("target role OnCheckMurder", killer, target);

        // 距離(通信遅延を見て少し余裕を持たせる)
        float dist = Vector2.Distance(killer.GetTruePosition(), target.GetTruePosition());
        if (dist > AllKill.Range + 1f) return Reject($"distance {dist:F2}", killer, target);

        // クールダウン(こちらも少し余裕を持たせる)
        // ・キルした本人の役職のクールダウンで見る(ホスト自身の役職ではなく)
        // ・バニラのインポスターは、バニラのキルボタンが自分でクールダウンを管理しているので、ここでは見ない。
        //   (見ると、バニラのボタンが「押せる」状態でも、こちらの値が長いと、黙って断ってしまう)
        bool vanillaButton = killer.Data.Role != null && killer.Data.Role.IsImpostor;
        if (!vanillaButton)
        {
            float last = AllKill.LastKillTime.TryGetValue(killer.PlayerId, out var t) ? t : AllKill.GameStartTime;
            if (Time.time - last < AllKill.Cooldown(killer) - 0.5f) return Reject("cooldown", killer, target);
        }

        AllKill.Log.LogInfo($"[AllKill] {killer.PlayerId} -> {target.PlayerId}");
        killer.ExMurderPlayer(target);
        return false; // バニラの判定は実行しない
    }
}

/// <summary>
/// キルが起きたら、クールダウンをリセットする。MurderPlayer は全クライアントで実行される。
/// </summary>
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
static class AllKillMurderPlayerPatch
{
    public static void Postfix(PlayerControl __instance)
    {
        if (__instance == null) return;

        if (__instance.AmOwner)
            AllKill.LocalTimer = AllKill.Cooldown(__instance);

        if (AmongUsClient.Instance.AmHost)
            AllKill.LastKillTime[__instance.PlayerId] = Time.time;
    }
}

/// <summary>
/// イントロが終わったら、最初のキルクールダウンを開始する。
/// </summary>
[HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.OnDestroy))]
static class AllKillIntroEndPatch
{
    public static void Postfix()
    {
        AllKill.LocalTimer = AllKill.Cooldown();
        AllKill.GameStartTime = Time.time;
        AllKill.LastKillTime.Clear();
    }
}
[HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
static class ClearRolesOnLobbyPatch
{
    public static void Postfix() => CustomRoleManager.ClearRoles();
}
/// <summary>
/// ベントボタンの表示と、押せる対象(近くのベント)のセット。
/// 表示しただけでは押せない。バニラは「ベントできる役職」の PlayerControl.FixedUpdate の中で
/// ボタンに対象のベントをセットしているので、それと同じことをここでやる。
/// </summary>
[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
static class VentPatch
{
    public static void Postfix(HudManager __instance)
    {
        var button = __instance.ImpostorVentButton;
        if (button == null) return;

        var me = PlayerControl.LocalPlayer;
        if (me == null || me.Data == null) return;
        if (ShipStatus.Instance == null) return;   // ロビー等
        if (MeetingHud.Instance != null) return;   // 会議中
        if (!CustomRoleManager.TryGetRole(me, out var role)) return;

        bool visible = role.CanVent && !me.Data.IsDead;
        button.ToggleVisible(visible);
        button.SetTarget(visible ? FindVent(me) : null);
    }

    /// <summary>今使えるベントのうち、いちばん近いものを返す</summary>
    static Vent FindVent(PlayerControl me)
    {
        Vent best = null;
        float bestDistance = float.MaxValue;

        foreach (var vent in ShipStatus.Instance.AllVents)
        {
            if (vent == null) continue;

            float distance = vent.CanUse(me.Data, out bool canUse, out bool couldUse);
            if (canUse && distance < bestDistance)
            {
                best = vent;
                bestDistance = distance;
            }
        }
        return best;
    }
}

/// <summary>
/// ベントを使えるかの判定を差し替える。
/// バニラは「バニラの役職がベントできるか」を見るので、自前の役職の CanVent で判定し直す。
/// </summary>
[HarmonyPatch(typeof(Vent), nameof(Vent.CanUse))]
static class VentCanUsePatch
{
    public static bool Prefix(
        Vent __instance,
        [HarmonyArgument(0)] NetworkedPlayerInfo pc,
        [HarmonyArgument(1)] ref bool canUse,
        [HarmonyArgument(2)] ref bool couldUse,
        ref float __result)
    {
        var player = pc?.Object;
        if (player == null) return true;
        if (!CustomRoleManager.TryGetRole(player, out var role)) return true; // 役職が無ければバニラの判定

        float distance = float.MaxValue;

        // ベントの中にいるときは、動けなくても出られるようにする
        couldUse = role.CanVent && !pc.IsDead && (player.CanMove || player.inVent);
        canUse = couldUse;

        if (canUse)
        {
            Vector2 playerPos = player.GetTruePosition();
            Vector2 ventPos = __instance.transform.position;
            distance = Vector2.Distance(playerPos, ventPos);

            canUse &= distance <= __instance.UsableDistance
                && !PhysicsHelpers.AnythingBetween(playerPos, ventPos, Constants.ShipOnlyMask, false);
        }

        __result = distance;
        return false; // バニラの判定は実行しない
    }
}