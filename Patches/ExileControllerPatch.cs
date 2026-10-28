using System;
using System.Collections;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using HyperRoles.Modules;
using HyperRoles.Roles.Crewmate;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRoles;
[HarmonyPatch(typeof(ExileController), nameof(ExileController.Begin))]
static class ExileMeetingEndTextPatch
{
    static string Label => Translator.GetString("BakeryText");

    const float OffsetY = -0.4f;

    static readonly BepInEx.Logging.ManualLogSource log =
        BepInEx.Logging.Logger.CreateLogSource("ExileMeetingEndText");

    public static void Postfix(ExileController __instance)
    {
        try
        {
            if (__instance == null || __instance.Text == null) return;
            bool bakeryalive = false;
            foreach (var pc in PlayerControl.AllPlayerControls)
            {
                if (pc.Data.IsDead || !CustomRoleManager.TryGetRole(pc, out var role)) continue;
                if (role is Bakery) bakeryalive = true;
            }
            if (!bakeryalive) return;

            // 位置の基準: 説明文があれば、その下。なければ、追放の文字の下
            var reference = __instance.ImpostorText != null ? __instance.ImpostorText : __instance.Text;

            var label = Object.Instantiate(__instance.Text, __instance.Text.transform.parent);
            label.name = "MeetingEndText";

            // 自動翻訳が、文字を書き戻さないようにする
            var translator = label.GetComponent<TextTranslatorTMP>();
            if (translator != null) Object.Destroy(translator);

            label.text = Label;
            // 元の文字は、1文字ずつ出る演出で、表示する文字数が絞られていることがある。複製にも引き継がれるので、全部出す
            label.maxVisibleCharacters = int.MaxValue;
            label.transform.localPosition = reference.transform.localPosition + new Vector3(0f, OffsetY, 0f);

            // バニラの文字が出てくるまで、隠しておく
            label.gameObject.SetActive(false);
            __instance.StartCoroutine(ShowWhenReady(__instance, label).WrapToIl2Cpp());
        }
        catch (Exception e)
        {
            log.LogError(e);
        }
    }

    /// <summary>バニラの追放の文字が出たら、こちらも出す(最大10秒待つ)</summary>
    static IEnumerator ShowWhenReady(ExileController exile, TMPro.TextMeshPro label)
    {
        float waited = 0f;
        while (exile != null && exile.Text != null && !exile.Text.gameObject.activeInHierarchy && waited < 10f)
        {
            waited += Time.deltaTime;
            yield return null;
        }

        if (label != null) label.gameObject.SetActive(true);
    }
}
