using HarmonyLib;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEngine;
// Town Of Hostのコードを一部移植
namespace HyperRoles
{
    [HarmonyPatch]

    public static class CredentialsPatch
    {
        public static SpriteRenderer HyRLogo { get; private set; }

        // ===== 見た目の調整用定数 =====
        private const float FontSize = 2.4f;          // Ping行・バージョンの文字サイズ
        private const string NameSize = "130%";       // MOD名の相対サイズ(スクショ風に大きめ)
        private const float RightOffset = 2.6f;       // 右端からの距離(チャットボタンなし)
        private const float RightOffsetWithChat = 3.2f; // 右端からの距離(チャットボタンあり。v18のHelpボタン回避)
        private const float TopOffset = 0f;           // 上端からの距離(小さいほど上。0.1→0で少し上へ)
        private const float ModStampOffsetY = 1.2f;   // 「Modded」スタンプの高さ(全シーン共通)

        // MOD名を色分けしたい場合はここを編集(例: "<color=#ffa500>Hyper</color><color=#00ff00>Roles</color>")
        private static string ColoredModName => $"<color={Main.ModColor}>{Main.ModName}</color>";

        private static string Header =>
            $"<size={NameSize}>{ColoredModName}</size> v{Main.PluginShowVersion}\n";

        public static Sprite LoadSpriteFromResource(string resourcePath, float pixelsPerUnit)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream(resourcePath))
            {
                if (stream != null)
                {
                    byte[] byteTexture = new byte[stream.Length];
                    stream.Read(byteTexture, 0, byteTexture.Length);

                    Texture2D texture = new Texture2D(2, 2);
                    ImageConversion.LoadImage(texture, byteTexture);

                    return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
                }
            }
            Debug.LogError($"[HyperRoles] 画像が見つかりません: {resourcePath}");
            return null;
        }

        [HarmonyPatch(typeof(PingTracker), nameof(PingTracker.Update))]
        class PingTrackerUpdatePatch
        {
            static void Postfix(PingTracker __instance)
            {
                var text = __instance.text;
                if (text == null) return;

                // 文字設定をシーンに関係なく固定
                text.enableAutoSizing = false;
                text.fontSize = text.fontSizeMin = text.fontSizeMax = FontSize;
                text.enableWordWrapping = false;
                text.richText = true;
                text.alignment = TextAlignmentOptions.TopRight;
                text.rectTransform.pivot = new Vector2(1f, 1f);
                text.fontSize = 2.5f;
                text.fontSizeMin = 2.5f;
                text.fontSizeMax = 2.5f;

                // 位置はチャットボタンの有無で切り替え
                var aspect = __instance.GetComponent<AspectPosition>();
                if (aspect != null)
                {
                    bool isChatButtonVisible = DestroyableSingleton<HudManager>.InstanceExists
                        && DestroyableSingleton<HudManager>.Instance.Chat != null
                        && DestroyableSingleton<HudManager>.Instance.Chat.chatButton.gameObject.activeInHierarchy;

                    float rightOffset = isChatButtonVisible ? RightOffsetWithChat : RightOffset;

                    aspect.Alignment = AspectPosition.EdgeAlignments.RightTop;
                    aspect.DistanceFromEdge = new Vector3(rightOffset, TopOffset, 0f);
                    aspect.AdjustPosition();
                }

                // 1行目: MOD名 + バージョン / 2行目以降: 元のPing表示
                var header = Header;
                if (!text.text.StartsWith(header))
                {
                    text.text = header + text.text;
                }
            }
        }
        [HarmonyPatch(typeof(VersionShower), nameof(VersionShower.Start))]
        class VersionShowerStartPatch
        {
            static void Postfix(VersionShower __instance)
            {
                if (!__instance) return;

                var credentials = Object.Instantiate(__instance.text, __instance.transform);
                credentials.gameObject.name = "HyRCredentialsText";

                // ローカル画面を開くとVersionShower側のスケール等が変わりサイズが変動するため、
                // 親から切り離して影響を受けないようにする(現在のワールドスケール・位置は維持)
                credentials.transform.SetParent(null, true);

                credentials.text = $"<color={Main.ModColor}>{Main.ModName}</color> v{Main.PluginShowVersion}";

                credentials.fontSize = 2f;
                credentials.fontSizeMin = 2f;
                credentials.fontSizeMax = 2f;
                credentials.alignment = TextAlignmentOptions.Right;
                credentials.enableAutoSizing = false;
                credentials.enableWordWrapping = false;

                credentials.transform.position = new Vector3(1.07f, 2.8f, -5f);
            }
        }
        [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Start))]
        class TitleLogoPatch
        {
            public static GameObject amongUsLogo;

            [HarmonyPriority(Priority.VeryHigh)]
            static void Postfix(MainMenuManager __instance)
            {
                amongUsLogo = GameObject.Find("LOGO-AU");

                var rightpanel = __instance.gameModeButtons.transform.parent;
                var logoObject = new GameObject("titleLogo_HyR");
                var logoTransform = logoObject.transform;
                HyRLogo = logoObject.AddComponent<SpriteRenderer>();
                logoTransform.parent = rightpanel;
                logoTransform.localPosition = new Vector3(0f, 0.15f, 1f);
                logoTransform.localScale *= 0.45f;
                HyRLogo.sprite = LoadSpriteFromResource("HyperRoles.Resources.HyperRoles-logo.png", 175f);
            }
        }

        [HarmonyPatch(typeof(ModManager), nameof(ModManager.LateUpdate))]
        class ModManagerLateUpdatePatch
        {
            public static void Prefix(ModManager __instance)
            {
                __instance.ShowModStamp();
            }

            public static void Postfix(ModManager __instance)
            {
                __instance.ModStamp.transform.position = AspectPosition.ComputeWorldPosition(
                    __instance.localCamera, AspectPosition.EdgeAlignments.RightTop,
                    new Vector3(0.4f, ModStampOffsetY, __instance.localCamera.nearClipPlane + 0.1f));
            }
        }
    }
}