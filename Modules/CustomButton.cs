using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRoles;

/// <summary>
/// HUDの右下に出す、カスタムボタン。
/// バニラの AbilityButton を複製して作る。複製元の親には GridArrange があるので、
/// 表示中のボタンは自動で右下に並ぶ(位置を自分で決める必要はない)。
/// </summary>
public class CustomActionButton
{
    /// <summary>登録されたボタンの一覧。Load() などで Register する</summary>
    static readonly List<CustomActionButton> All = new();

    static readonly BepInEx.Logging.ManualLogSource log =
        BepInEx.Logging.Logger.CreateLogSource("CustomButton");

    readonly string label;
    readonly float cooldown;
    readonly Sprite icon;
    readonly Func<bool> isVisible;
    readonly Func<bool> canUse;
    readonly Action onClick;
    readonly Func<KeyCode> key;
    readonly Func<int> uses;

    ActionButton button;
    float timer;

    /// <param name="label">ボタンの下に出る文字</param>
    /// <param name="icon">アイコン画像。null ならバニラのアイコンのまま</param>
    /// <param name="cooldown">クールダウン(秒)。0 ならクールダウンなし</param>
    /// <param name="isVisible">ボタンを出す条件(例: 自分の役職がジャッカルのとき)</param>
    /// <param name="onClick">押したときの処理</param>
    /// <param name="canUse">押せる条件。省略すると常に押せる(表示中のみ)</param>
    /// <param name="key">キー。省略するとキーでは押せない</param>
    /// <param name="uses">残り回数を左上に出す。省略すると、回数の表示は出さない</param>
    public CustomActionButton(
        string label, Sprite icon, float cooldown,
        Func<bool> isVisible, Action onClick,
        Func<bool> canUse = null, Func<KeyCode> key = null,
        Func<int> uses = null)
    {
        this.label = label;
        this.icon = icon;
        this.cooldown = cooldown;
        this.isVisible = isVisible;
        this.onClick = onClick;
        this.canUse = canUse ?? (() => true);
        this.key = key;
        this.uses = uses;
    }

    public static void Register(CustomActionButton b) => All.Add(b);

    /// <summary>クールダウンを最初からやり直す(イントロ後など)</summary>
    public void ResetCooldown() => timer = cooldown;

    /// <summary>外からクールダウンを開始する(使用後に呼ぶなど)</summary>
    public void StartCooldown() => timer = cooldown;

    // ------------------------------------------------------------
    // ボタンの生成と更新
    // ------------------------------------------------------------

    void EnsureCreated(HudManager hud)
    {
        // シーンが変わると HudManager ごと壊れるので、そのたびに作り直す
        if (button != null) return;

        var source = hud.AbilityButton;
        if (source == null) return;

        button = Object.Instantiate(source, source.transform.parent);
        button.name = "CustomButton_" + label;
        button.OverrideText(label);
        if (icon != null) button.graphic.sprite = icon;

        // 複製元(アビリティボタン)の「残り回数」の表示(左上の数字)を、いったん消す
        SetUsesVisible(false);

        // バニラのアビリティ処理(押すと役職の能力を使う)を外して、自前の処理だけにする
        var passive = button.GetComponent<PassiveButton>();
        passive.OnClick = new UnityEngine.UI.Button.ButtonClickedEvent();
        passive.OnClick.AddListener((Action)OnClicked);

        timer = cooldown;
        log.LogInfo($"[CustomButton] created: {label}");
    }

    void OnClicked()
    {
        if (button == null || !button.isActiveAndEnabled) return;
        if (timer > 0f || !canUse()) return;

        onClick();
        timer = cooldown;
    }

    void Update(HudManager hud)
    {
        EnsureCreated(hud);
        if (button == null) return;

        bool visible = BaseCondition() && isVisible();
        button.ToggleVisible(visible);
        if (!visible) return;

        // 複製元のボタンが、自分の更新で画像を元に戻すことがあるので、毎フレーム設定し直す
        if (icon != null && button.graphic.sprite != icon)
            button.graphic.sprite = icon;

        if (timer > 0f) timer = Mathf.Max(0f, timer - Time.deltaTime);
        button.SetCoolDown(timer, cooldown);

        if (canUse()) button.SetEnabled();
        else button.SetDisabled();

        // 残り回数を出す指定があるときだけ、左上に表示する
        if (uses != null)
        {
            SetUsesVisible(true);
            button.usesRemainingText.text = uses().ToString();
        }

        if (key != null && Input.GetKeyDown(key())) OnClicked();
    }

    /// <summary>左上の「残り回数」の表示(数字と、その背景)を、出す/消す</summary>
    void SetUsesVisible(bool visible)
    {
        if (button.usesRemainingText != null)
            button.usesRemainingText.gameObject.SetActive(visible);
        if (button.usesRemainingSprite != null)
            button.usesRemainingSprite.gameObject.SetActive(visible);
    }

    /// <summary>どのボタンでも共通の、出してよい状況</summary>
    static bool BaseCondition()
    {
        var me = PlayerControl.LocalPlayer;
        if (me == null || me.Data == null || me.Data.IsDead) return false;
        if (ShipStatus.Instance == null) return false;   // ロビー等
        if (MeetingHud.Instance != null) return false;   // 会議中
        return true;
    }

    internal static void UpdateAll(HudManager hud)
    {
        foreach (var b in All) b.Update(hud);
    }

    internal static void ResetAll()
    {
        foreach (var b in All) b.ResetCooldown();
    }

    // ------------------------------------------------------------
    // 画像の読み込み(DLLに埋め込んだPNGから)
    // ------------------------------------------------------------

    /// <summary>
    /// DLLに「埋め込みリソース」として入れたPNGを、スプライトにする。
    /// resourcePath の例: "HyperRoles.Resources.Teleport.png"(名前空間.フォルダ.ファイル名)
    /// </summary>
    public static Sprite LoadSprite(string resourcePath, float pixelsPerUnit = 115f)
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream(resourcePath);
            if (stream == null)
            {
                log.LogWarning($"[CustomButton] resource not found: {resourcePath}");
                log.LogWarning($"[CustomButton] available: {string.Join(", ", asm.GetManifestResourceNames())}");
                return null;
            }

            var bytes = new byte[stream.Length];
            stream.Read(bytes, 0, bytes.Length);

            var texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
            ImageConversion.LoadImage(texture, bytes, false);
            texture.hideFlags |= HideFlags.DontUnloadUnusedAsset; // Unityに勝手に破棄されないようにする

            var sprite = Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit);
            sprite.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            return sprite;
        }
        catch (Exception e)
        {
            log.LogWarning($"[CustomButton] LoadSprite failed: {e}");
            return null;
        }
    }
}

[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
static class CustomActionButtonUpdatePatch
{
    public static void Postfix(HudManager __instance) => CustomActionButton.UpdateAll(__instance);
}

[HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.OnDestroy))]
static class CustomActionButtonIntroEndPatch
{
    public static void Postfix() => CustomActionButton.ResetAll();
}

/// <summary>
/// 使い方の例。プラグインの Load() から CustomButtonExample.Register() を呼ぶ。
/// </summary>
public static class CustomButtonExample
{
    public static void Register()
    {
        CustomActionButton.Register(new CustomActionButton(
            label: "テレポート",
            icon: null, // 画像を使うなら CustomActionButton.LoadSprite("HyperRoles.Resources.Teleport.png")
            cooldown: 20f,
            isVisible: () => CustomRoleManager.TryGetRole(PlayerControl.LocalPlayer, out var role) && role.CanVent,
            onClick: () =>
            {
                // 押したときの処理。ここでは自分を (0, 0) に移動させるだけの例
                PlayerControl.LocalPlayer.NetTransform.RpcSnapTo(Vector2.zero);
            },
            canUse: () => PlayerControl.LocalPlayer.CanMove,
            key: () => KeyCode.F));
    }
}