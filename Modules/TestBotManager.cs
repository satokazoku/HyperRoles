using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace HyperRoles;
// Town Of Host-Kより
// 2026/10/20に追加

/// <summary>
/// 開始ボタンを押したときに、テスト用のBOTを BotCount 人スポーンさせる。
/// ローカルゲームは TOH の GameStartManagerPatch にあるダミー出現コード(AddDummy方式)、
/// オンラインは TOH の TestBotManager.SpawnLegacySnr(AddPlayer方式)を参考にしている。
/// </summary>
public static class TestBotManager
{
    /// <summary>開始時に用意するBOTの人数(ホスト1人+14人=15人)</summary>
    public const int BotCount = 14;

    const byte MaxPlayerId = 15;

    static readonly BepInEx.Logging.ManualLogSource log =
        BepInEx.Logging.Logger.CreateLogSource("TestBot");

    static readonly List<PlayerControl> bots = new();
    static readonly HashSet<byte> botPlayerIds = new();

    public static bool IsTestBot(this PlayerControl player)
        => player != null && botPlayerIds.Contains(player.PlayerId);

    /// <summary>
    /// 開始ボタンから呼ばれる。足りない分だけBOTを増やす(二重押しで増えすぎないようにする)。
    /// </summary>
    public static void SpawnBotsOnStart()
    {
        log.LogInfo($"[TestBot] SpawnBotsOnStart: AmHost={AmongUsClient.Instance.AmHost}, mode={AmongUsClient.Instance.NetworkMode}, existing={botPlayerIds.Count}");
        if (!AmongUsClient.Instance.AmHost) return;

        int need = BotCount - botPlayerIds.Count;
        if (need <= 0)
        {
            log.LogInfo("[TestBot] bots already spawned, skip");
            return;
        }

        int spawned = 0;
        for (int i = 0; i < need; i++)
        {
            var bot = SpawnBot($"ダミー {botPlayerIds.Count + 1}");
            if (bot == null) break; // 空きIDが無い、または失敗
            spawned++;
        }
        log.LogInfo($"[TestBot] spawned {spawned}/{need}, total bots={botPlayerIds.Count}, allPlayers={PlayerControl.AllPlayerControls.Count}");
    }

    static PlayerControl SpawnBot(string name)
    {
        if (AmongUsClient.Instance.PlayerPrefab == null || GameData.Instance == null)
        {
            log.LogWarning("[TestBot] PlayerPrefab or GameData is null");
            return null;
        }
        if (!TryGetAvailablePlayerId(out var id))
        {
            log.LogWarning("[TestBot] no free PlayerId");
            return null;
        }

        bool isLocal = AmongUsClient.Instance.NetworkMode == NetworkModes.LocalGame;
        var bot = isLocal ? SpawnLocalDummy(id, name) : SpawnLegacy(id, name);
        if (bot == null) return null;

        bots.Add(bot);
        botPlayerIds.Add(id);

        var pos = GetSpawnPosition(botPlayerIds.Count - 1);
        bot.transform.position = pos;
        bot.NetTransform.SnapTo(pos);

        log.LogInfo($"[TestBot] spawned PlayerId={id}, Name={name}, local={isLocal}");
        return bot;
    }

    /// <summary>
    /// ローカルゲーム用。TOH の GameStartManagerPatch(DebugModeManager.Spawndummy)と同じ手順。
    /// </summary>
    static PlayerControl SpawnLocalDummy(byte id, string name)
    {
        try
        {
            var dummy = UnityEngine.Object.Instantiate(AmongUsClient.Instance.PlayerPrefab);
            dummy.isDummy = true;
            dummy.PlayerId = id;
            var playerInfo = GameData.Instance.AddDummy(dummy);
            var colorId = GetUnusedColorId(id);

            dummy.NetTransform.enabled = true;
            dummy.RpcSetColor(colorId);
            dummy.RpcSetName(name);
            var behaviour = dummy.GetComponent<DummyBehaviour>();
            if (behaviour != null)
                behaviour.enabled = true; // true: ダミーが動き回る / false: その場で止まる
            dummy.SetHat("", colorId);
            dummy.SetVisor("", colorId);
            dummy.SetSkin("", colorId);
            dummy.SetPet("");

            AmongUsClient.Instance.Spawn(dummy);
            playerInfo.RpcSetTasks(Array.Empty<byte>());
            return dummy;
        }
        catch (Exception e)
        {
            log.LogWarning($"[TestBot] local dummy spawn failed: {e}");
            return null;
        }
    }

    /// <summary>
    /// オンライン用。TOH の TestBotManager.SpawnLegacySnr と同じ手順。
    /// </summary>
    static PlayerControl SpawnLegacy(byte id, string name)
    {
        var bot = UnityEngine.Object.Instantiate(AmongUsClient.Instance.PlayerPrefab);
        bot.PlayerId = id;
        bot.NetTransform.enabled = true;

        NetworkedPlayerInfo playerInfo;
        try
        {
            playerInfo = GameData.Instance.AddPlayer(bot, null);
            AmongUsClient.Instance.Spawn(bot, -2, SpawnFlags.IsClientCharacter);
        }
        catch (Exception e)
        {
            log.LogWarning($"[TestBot] legacy spawn failed: {e}");
            try
            {
                GameData.Instance?.RemovePlayer(id);
                UnityEngine.Object.Destroy(bot.gameObject);
            }
            catch { }
            return null;
        }

        bot.RpcSetName(name);
        bot.RpcSetColor(GetUnusedColorId(id));
        bot.RpcSetHat("");
        bot.RpcSetSkin("");
        bot.RpcSetVisor("");
        bot.RpcSetPet("");
        bot.RpcSetNamePlate("");
        playerInfo?.RpcSetTasks(Array.Empty<byte>());
        bot.RpcSetRole(RoleTypes.Crewmate, true);
        return bot;
    }

    /// <summary>ホストの周りに、4列で並べる</summary>
    static Vector2 GetSpawnPosition(int index)
    {
        var host = PlayerControl.LocalPlayer != null
            ? PlayerControl.LocalPlayer.GetTruePosition()
            : Vector2.zero;
        var x = 1.1f + (index % 4) * 0.55f;
        var y = -0.45f - ((index / 4) % 4) * 0.45f;
        return host + new Vector2(x, y);
    }

    static bool TryGetAvailablePlayerId(out byte id)
    {
        var used = new HashSet<byte>();
        foreach (var p in PlayerControl.AllPlayerControls.ToArray())
            if (p != null) used.Add(p.PlayerId);
        foreach (var info in GameData.Instance.AllPlayers)
            used.Add(info.PlayerId);

        for (byte c = 0; c <= MaxPlayerId; c++)
        {
            if (!used.Contains(c)) { id = c; return true; }
        }
        id = byte.MaxValue;
        return false;
    }

    static byte GetUnusedColorId(byte fallback)
    {
        var usedColors = PlayerControl.AllPlayerControls.ToArray()
            .Where(pc => pc?.Data?.DefaultOutfit != null)
            .Select(pc => pc.Data.DefaultOutfit.ColorId)
            .ToHashSet();

        for (byte c = 0; c < 18; c++)
            if (!usedColors.Contains(c)) return c;

        return (byte)(fallback % 18);
    }

    public static void DespawnAll()
    {
        foreach (var bot in bots.ToArray())
        {
            if (bot == null) continue;
            try
            {
                GameData.Instance?.RemovePlayer(bot.PlayerId);
                AmongUsClient.Instance?.Despawn(bot);
            }
            catch (Exception e)
            {
                log.LogWarning($"[TestBot] despawn failed: {e}");
            }
        }
        ClearLocal();
    }

    public static void ClearLocal()
    {
        bots.Clear();
        botPlayerIds.Clear();
    }
}

/// <summary>開始ボタンが押されたとき(ホストのみ)にBOTを出す</summary>
[HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.BeginGame))]
static class BotBeginGamePatch
{
    static readonly BepInEx.Logging.ManualLogSource log =
        BepInEx.Logging.Logger.CreateLogSource("TestBot");

    public static void Prefix()
    {
        log.LogInfo("[TestBot] BeginGame Prefix called");
        TestBotManager.SpawnBotsOnStart();
    }
}

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameJoined))]
static class BotOnGameJoinedPatch
{
    public static void Postfix() => TestBotManager.ClearLocal();
}

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnDisconnected))]
static class BotOnDisconnectedPatch
{
    public static void Postfix() => TestBotManager.ClearLocal();
}
