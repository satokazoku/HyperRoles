using System.Linq;
using System.Text;
using AmongUs.GameOptions;
using HarmonyLib;
using HyperRoles.Modules;
using HyperRoles.Patches;
using HyperRoles.Roles;
using TMPro;
using UnityEngine;

namespace HyperRoles;

/// <summary>
/// 終了画面の「勝利」「敗北」の下に、クルーメイトが勝った場合は「クルーメイト勝利」と表示する。
/// </summary>
[HarmonyPatch(typeof(EndGameManager), nameof(EndGameManager.SetEverythingUp))]
static class EndGameWinnerTextPatch
{
    public static NetworkedPlayerInfo GetPlayerInfoById(int PlayerId) =>
        GameData.Instance.AllPlayers.ToArray().Where(info => info.PlayerId == PlayerId).FirstOrDefault();

    public static string GetPlayerColor(this PlayerControl player)
    {
        if (player == null) return "";

        var na = player.Data.PlayerName;

        if (FixedUpdatePatch.name.TryGetValue(player.PlayerId, out var og))
        {
            na = og;
        }
        na = System.Text.RegularExpressions.Regex.Replace(na, "<[^>]*>", "");

        var data = GetPlayerInfoById(player.PlayerId);
        var color = ColorUtility.ToHtmlStringRGB(Palette.PlayerColors[data.DefaultOutfit.ColorId]);

        return $"<#{color}>{na}</color>";
    }
    static readonly BepInEx.Logging.ManualLogSource log =
        BepInEx.Logging.Logger.CreateLogSource("EndGameWinnerTextPatch");
    public static void Postfix(EndGameManager __instance)
    {
        try
        {
            var rlobj = Object.Instantiate(__instance.WinText.gameObject);
            var rlbase = __instance.WinText.transform.position;
            rlobj.transform.position = new Vector3(rlbase.x - 3.2f, rlbase.y + 1.4f, rlbase.z);
            rlobj.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);

            var textr = rlobj.GetComponent<TMPro.TextMeshPro>();
            textr.alignment = TextAlignmentOptions.TopLeft;
            textr.enableAutoSizing = false;
            textr.fontSize = 2f;
            textr.enableWordWrapping = false;
            textr.text = EndGameSummary.Text;

            bool alive = !CheckGameEnd.AllDied;

            if (ControllerManagerUpdatePatch.Haison)
            {
                var c = ColorUtility.ToHtmlStringRGB(Palette.DisabledGrey);
                __instance.WinText.text = $"<color=#{c}>廃村</color>";
                __instance.BackgroundBar.material.color = Palette.DisabledGrey;
                ControllerManagerUpdatePatch.Haison = false;
                return;
            }
            if (!alive)
            {
                var c = ColorUtility.ToHtmlStringRGB(Palette.DisabledGrey);
                __instance.WinText.text = $"<color=#{c}>全滅</color>";
                __instance.BackgroundBar.material.color = Palette.DisabledGrey;

                var b = Object.Instantiate(__instance.WinText.gameObject);
                var s = __instance.WinText.transform.position;
                b.transform.position = new Vector3(s.x, s.y - 0.5f, s.z);
                b.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);

                var t = b.GetComponent<TMPro.TextMeshPro>();
                t.fontSizeMin = 3f;

                t.text = $"<color=#{c}>誰もいなくなった</color>";
                ControllerManagerUpdatePatch.Haison = false;
                return;
            }
            // WinText(勝利/敗北)を複製して、その少し下に置く
            var obj = Object.Instantiate(__instance.WinText.gameObject);
            var basePos = __instance.WinText.transform.position;
            obj.transform.position = new Vector3(basePos.x, basePos.y - 0.5f, basePos.z);
            obj.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);

            var text = obj.GetComponent<TMPro.TextMeshPro>();
            text.fontSizeMin = 3f;

            var colorCode = ColorUtility.ToHtmlStringRGB(Palette.CrewmateBlue);
            text.text = $"<color=#{colorCode}>クルーメイト勝利</color>";
            __instance.BackgroundBar.material.color = Palette.CrewmateBlue;

            if (CheckGameEnd.WinnerTeam == Team.Impostor)
            {
                colorCode = ColorUtility.ToHtmlStringRGB(Palette.ImpostorRed);
                text.text = $"<color=#{colorCode}>インポスター勝利</color>";
                __instance.BackgroundBar.material.color = Palette.ImpostorRed;
            }

            if (CheckGameEnd.WinnerTeam == Team.Neutral)
            {
                if (CheckGameEnd.WinnerRole == CustomRoleManager.RoleId.Jackal)
                {
                    colorCode = ColorUtility.ToHtmlStringRGB(new Color32(0, 180, 235, 255));
                    text.text = $"<color=#{colorCode}>ジャッカル勝利</color>";
                    __instance.BackgroundBar.material.color = new Color32(0, 180, 235, 255);
                }
            }
        }
        catch (System.Exception e)
        {
            log.LogError(e);
        }
        ControllerManagerUpdatePatch.Haison = false;
    }
}

/// <summary>
/// 終了画面に出す「全員の役職一覧」。
/// 終了画面(SetEverythingUp)の時点では、ゲーム中の PlayerControl がすでに無いことがあるので、
/// まだ残っているゲーム終了の瞬間(AmongUsClient.OnGameEnd)に、文字を作って保存しておく。
/// </summary>
public static class EndGameSummary
{
    /// <summary>終了画面が読む、一覧の文字</summary>
    public static string Text = "";
}

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameEnd))]
static class EndGameSummaryPatch
{
    static readonly BepInEx.Logging.ManualLogSource log =
        BepInEx.Logging.Logger.CreateLogSource("EndGameSummary");

    public static void Postfix()
    {
        var sb = new StringBuilder();
        var result = Translator.GetString("Result");
        sb.Append($"<#ffffff>{result}</color>\n");
        int count = 0;

        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (p == null || p.Data == null) continue;
            count++;

            var name = p.GetPlayerColor();
            var roleName = Translator.GetString("Unknown");
            var roleColor = "ffffff";

            if (CustomRoleManager.TryGetRole(p, out var role))
            {
                roleName = role.RoleName;
                roleColor = ColorUtility.ToHtmlStringRGB(role.RoleColor);
            }
            else
            {
                roleName = p.Data.Role.IsImpostor ? Translator.GetString("Impostor") : Translator.GetString("Crewmate");
                if (p.Data.Role.IsImpostor)
                {
                    roleColor = ColorUtility.ToHtmlStringRGB(Palette.ImpostorRed);
                }
                else
                {
                    roleColor = ColorUtility.ToHtmlStringRGB(Palette.CrewmateBlue);
                }
            }

            var status = p.Data.IsDead ? Translator.GetString("Dead") : Translator.GetString("Alive");

            sb.Append($"{name}       <#ffffff>{status}      </color><#{roleColor}>{roleName}</color>\n");
        }

        EndGameSummary.Text = sb.ToString();
        log.LogInfo($"[Summary] players={count}");
    }
}