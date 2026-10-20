using System.Collections;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using HyperRoles;
using HyperRoles.Modules;
using HyperRoles.Roles;
using UnityEngine;

/*[HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.CoBegin))]
static class IntroCoBeginPatch
{
    public static void Prefix(IntroCutscene __instance)
    {
        CustomRoleManager.RoleAssign();
    }
    public static void Postfix(IntroCutscene __instance)
    {
        __instance.StartCoroutine(OverwriteRoleText(__instance).WrapToIl2Cpp());
    }

    static IEnumerator OverwriteRoleText(IntroCutscene cutscene)
    {
        float elapsed = 0f;
        // チーム画面(数秒)のあとにShowRoleが走るので、イントロ中は監視して上書きする
        while (cutscene != null && elapsed < 10f)
        {
            if (cutscene.RoleText != null && cutscene.RoleText.gameObject.activeInHierarchy)
            {
                var color = CustomRoleManager.GetRoleColor(PlayerControl.LocalPlayer);
                var name = CustomRoleManager.GetRoleName(PlayerControl.LocalPlayer);
                var intro = CustomRoleManager.GetIntro(PlayerControl.LocalPlayer);

                cutscene.YouAreText.color = color;
                cutscene.RoleText.text = name;
                cutscene.RoleText.color = color;
                cutscene.RoleBlurbText.text = intro;
                cutscene.RoleBlurbText.color = color;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }
    }
}*/

[HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.CoBegin))]
static class IntroCoBeginPatch
{
    public static bool RoleAssigned = false;
    static readonly BepInEx.Logging.ManualLogSource log =
        BepInEx.Logging.Logger.CreateLogSource("IntroCoBeginPatch");

    // 動作確認用の仮実装(全員シェリフを自分のクライアントだけで割り当てる)
    public static void Prefix(IntroCutscene __instance)
    {
        log.LogInfo("[Intro] CoBegin Prefix: RoleAssign");
        CustomRoleManager.RoleAssign();
        RoleAssigned = true;
    }

    public static void Postfix(IntroCutscene __instance)
    {
        log.LogInfo("[Intro] CoBegin Postfix called");
        __instance.StartCoroutine(OverwriteRoleText(__instance).WrapToIl2Cpp());
    }

    static IEnumerator OverwriteRoleText(IntroCutscene cutscene)
    {
        float elapsed = 0f;
        bool logged = false;
        while (cutscene != null && elapsed < 10f)
        {
            if (IntroTeam.IsLocalNeutral())
                OverwriteTeamText(cutscene);

            if (cutscene.RoleText != null && cutscene.RoleText.gameObject.activeInHierarchy)
                Apply(cutscene, ref logged);

            elapsed += Time.deltaTime;
            yield return null;
        }
    }
    static void OverwriteTeamText(IntroCutscene cutscene)
    {
        var info = Translator.GetString("NeutralInfo");
        if (cutscene.ImpostorText != null && cutscene.ImpostorText.text != info)
        {
            cutscene.ImpostorText.gameObject.SetActive(true);
            cutscene.ImpostorText.text = info;
        }
    }
    static void Apply(IntroCutscene cutscene, ref bool logged)
    {
        try
        {
            if (!CustomRoleManager.TryGetRole(PlayerControl.LocalPlayer, out var role))
            {
                if (!logged)
                {
                    log.LogInfo($"[Intro] role not assigned yet. roles={CustomRoleManager.Count}, myId={PlayerControl.LocalPlayer.PlayerId}");
                    logged = true;
                }
                return;
            }

            var color = role.RoleColor;
            cutscene.YouAreText.color = color;
            cutscene.RoleText.text = role.RoleName;
            cutscene.RoleText.color = color;
            cutscene.RoleBlurbText.text = role.Intro;
            cutscene.RoleBlurbText.color = color;
        }
        catch (System.Exception e)
        {
            log.LogError(e);
        }
    }

}
static class IntroTeam
{
    /// <summary>チーム画面の大きいタイトルに出す文字</summary>
    public static string NeutralTeamName => Translator.GetString("Neutral");

    /// <summary>自分の役職が、ニュートラル陣営か</summary>
    public static bool IsLocalNeutral()
    {
        return CustomRoleManager.TryGetRole(PlayerControl.LocalPlayer, out var role)
            && role.Team == Team.Neutral;
    }

    /// <summary>自分だけが入ったチーム(チーム画面に、自分1人だけ出すため)</summary>
    public static Il2CppSystem.Collections.Generic.List<PlayerControl> SoloTeam()
    {
        var team = new Il2CppSystem.Collections.Generic.List<PlayerControl>();
        team.Add(PlayerControl.LocalPlayer);
        return team;
    }
}

/// <summary>
/// クルーメイト側のチーム画面。ニュートラルのときは、自分1人・灰色の背景・「第三陣営」の表示にする。
/// </summary>
[HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.BeginCrewmate))]
static class BeginCrewmateNeutralPatch
{
    public static void Prefix(ref Il2CppSystem.Collections.Generic.List<PlayerControl> teamToDisplay)
    {
        if (!IntroTeam.IsLocalNeutral()) return;

        teamToDisplay = IntroTeam.SoloTeam();
    }

    public static void Postfix(IntroCutscene __instance)
    {
        if (!IntroTeam.IsLocalNeutral()) return;

        __instance.TeamTitle.text = IntroTeam.NeutralTeamName;
        __instance.TeamTitle.color = Palette.DisabledGrey;
        __instance.BackgroundBar.material.color = Palette.DisabledGrey;
    }
}

/// <summary>
/// インポスター側のチーム画面。バニラの役職がインポスターのニュートラル役職(ジャッカルなど)が、
/// 赤い背景と仲間の表示にならないよう、クルーメイト側の画面に切り替える。
/// </summary>
[HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.BeginImpostor))]
static class BeginImpostorNeutralPatch
{
    public static bool Prefix(IntroCutscene __instance, ref Il2CppSystem.Collections.Generic.List<PlayerControl> yourTeam)
    {
        if (!IntroTeam.IsLocalNeutral()) return true;

        yourTeam = IntroTeam.SoloTeam();
        __instance.BeginCrewmate(yourTeam); // 上のBeginCrewmateのパッチが、灰色・第三陣営にする
        __instance.overlayHandle.color = Palette.DisabledGrey;

        __instance.ImpostorText.color = Palette.DisabledGrey;
        __instance.ImpostorText.gameObject.SetActive(true);
        __instance.ImpostorText.text = Translator.GetString("NeutralInfo");
        return false; // インポスター側の画面は実行しない
    }
}
