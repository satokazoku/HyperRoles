using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HyperRoles.Modules;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRoles;

public class CustomActionButton
{
    static readonly List<CustomActionButton> All = new();

    static readonly BepInEx.Logging.ManualLogSource log =
        BepInEx.Logging.Logger.CreateLogSource("CustomButton");

    readonly Func<string> labelGetter;
    string shownLabel;
    readonly Func<float> cooldownGetter;
    readonly Sprite icon;
    readonly Func<bool> isVisible;
    readonly Func<bool> canUse;
    readonly Action onClick;
    readonly Func<KeyCode> key;
    readonly Func<int> uses;
    readonly Func<float> durationGetter;
    readonly Action onEffectEnd;

    ActionButton button;
    float timer;
    float effectTimer;

    // cooldown と duration を Func<float> で受け取れるように変更
    public CustomActionButton(
        Func<string> label, Sprite icon, Func<float> cooldown,
        Func<bool> isVisible, Action onClick,
        Func<bool> canUse = null, Func<KeyCode> key = null,
        Func<int> uses = null,
        Func<float> duration = null, Action onEffectEnd = null)
    {
        this.labelGetter = label;
        this.icon = icon;
        this.cooldownGetter = cooldown;
        this.isVisible = isVisible;
        this.onClick = onClick;
        this.canUse = canUse ?? (() => true);
        this.key = key;
        this.uses = uses;
        this.durationGetter = duration ?? (() => 0f);
        this.onEffectEnd = onEffectEnd;
    }

    // 互換性のために float もそのまま渡せるオーバーロードを残す
    public CustomActionButton(
        Func<string> label, Sprite icon, float cooldown,
        Func<bool> isVisible, Action onClick,
        Func<bool> canUse = null, Func<KeyCode> key = null,
        Func<int> uses = null,
        float duration = 0f, Action onEffectEnd = null)
        : this(label, icon, () => cooldown, isVisible, onClick, canUse, key, uses, () => duration, onEffectEnd)
    {
    }

    public static void Register(CustomActionButton b) => All.Add(b);

    public void ResetCooldown()
    {
        timer = cooldownGetter();
        effectTimer = 0f;
    }

    public void StartCooldown() => timer = cooldownGetter();

    public bool IsEffectActive => effectTimer > 0f;

    public void CancelEffect()
    {
        if (effectTimer > 0f) EndEffect();
    }

    void EndEffect()
    {
        effectTimer = 0f;
        timer = cooldownGetter();
        onEffectEnd?.Invoke();
    }

    void EnsureCreated(HudManager hud)
    {
        if (button != null) return;

        var source = hud.AbilityButton;
        if (source == null) return;

        button = Object.Instantiate(source, source.transform.parent);
        shownLabel = labelGetter();
        button.name = "CustomButton_" + shownLabel;
        button.OverrideText(shownLabel);
        if (icon != null) button.graphic.sprite = icon;

        SetUsesVisible(false);

        var passive = button.GetComponent<PassiveButton>();
        passive.OnClick = new UnityEngine.UI.Button.ButtonClickedEvent();
        passive.OnClick.AddListener((Action)OnClicked);

        timer = cooldownGetter();
        log.LogInfo($"[CustomButton] created: {shownLabel}");
    }

    void OnClicked()
    {
        if (button == null || !button.isActiveAndEnabled) return;
        if (effectTimer > 0f) return;
        if (timer > 0f || !canUse()) return;

        onClick();
        float currentDuration = durationGetter();
        if (currentDuration > 0f) effectTimer = currentDuration;
        else timer = cooldownGetter();
    }

    void Update(HudManager hud)
    {
        EnsureCreated(hud);
        if (button == null) return;

        bool visible = BaseCondition() && isVisible();

        if (!visible && effectTimer > 0f) EndEffect();

        button.ToggleVisible(visible);
        if (!visible) return;

        var label = labelGetter();
        if (label != shownLabel)
        {
            shownLabel = label;
            button.OverrideText(label);
        }

        if (icon != null && button.graphic.sprite != icon)
            button.graphic.sprite = icon;

        if (effectTimer > 0f)
        {
            effectTimer = Mathf.Max(0f, effectTimer - Time.deltaTime);
            button.SetFillUp(effectTimer, durationGetter());
            if (effectTimer <= 0f) EndEffect();
        }
        else
        {
            if (timer > 0f) timer = Mathf.Max(0f, timer - Time.deltaTime);
            button.SetCoolDown(timer, cooldownGetter());
        }

        if (effectTimer > 0f || canUse()) button.SetEnabled();
        else button.SetDisabled();

        if (uses != null)
        {
            SetUsesVisible(true);
            button.usesRemainingText.text = uses().ToString();
        }

        if (key != null && Input.GetKeyDown(key())) OnClicked();
    }

    void SetUsesVisible(bool visible)
    {
        if (button.usesRemainingText != null)
            button.usesRemainingText.gameObject.SetActive(visible);
        if (button.usesRemainingSprite != null)
            button.usesRemainingSprite.gameObject.SetActive(visible);
    }

    static bool BaseCondition()
    {
        var me = PlayerControl.LocalPlayer;
        if (me == null || me.Data == null || me.Data.IsDead) return false;
        if (ShipStatus.Instance == null) return false;
        if (MeetingHud.Instance != null) return false;
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

    public static Sprite LoadSprite(string resourcePath, float pixelsPerUnit = 115f)
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream(resourcePath);
            if (stream == null)
            {
                log.LogWarning($"[CustomButton] resource not found: {resourcePath}");
                return null;
            }

            var bytes = new byte[stream.Length];
            stream.Read(bytes, 0, bytes.Length);

            var texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
            ImageConversion.LoadImage(texture, bytes, false);
            texture.hideFlags |= HideFlags.DontUnloadUnusedAsset;

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