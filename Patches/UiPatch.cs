using System.IO;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace HyperRoles
{
    [HarmonyPatch]
    public static class CredentialsPatch
    {
        public static SpriteRenderer HyRLogo { get; private set; }
        private static TextMeshPro pingTrackerCredential = null;
        private static AspectPosition pingTrackerCredentialAspectPos = null;

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
            static StringBuilder sb = new StringBuilder();

            static void Postfix(PingTracker __instance)
            {
                if (pingTrackerCredential == null)
                {
                    var uselessPingTracker = Object.Instantiate(__instance, __instance.transform.parent);
                    pingTrackerCredential = uselessPingTracker.GetComponent<TextMeshPro>();
                    Object.Destroy(uselessPingTracker);

                    pingTrackerCredential.alignment = TextAlignmentOptions.TopRight;
                    pingTrackerCredential.rectTransform.pivot = new Vector2(1f, 0.7f);

                    pingTrackerCredentialAspectPos = pingTrackerCredential.GetComponent<AspectPosition>();
                    pingTrackerCredentialAspectPos.Alignment = AspectPosition.EdgeAlignments.RightTop;
                    pingTrackerCredential.gameObject.name = "CredentialText";

                    uselessPingTracker.gameObject.SetActive(true);
                }

                if (pingTrackerCredentialAspectPos)
                {
                    bool isChatButtonVisible = DestroyableSingleton<HudManager>.InstanceExists
                        && DestroyableSingleton<HudManager>.Instance.Chat.chatButton.gameObject.activeInHierarchy;

                    float rightOffset = isChatButtonVisible ? 2.5f : 1.8f;
                    pingTrackerCredentialAspectPos.DistanceFromEdge = new Vector3(rightOffset, 0, 0);
                }

                sb.Clear();
                sb.Append("\r\n").Append($"<color={Main.ModColor}>{Main.ModName}</color> v{Main.PluginShowVersion}");

                pingTrackerCredential.text = sb.ToString();
                __instance.text.alignment = TextAlignmentOptions.TopLeft;
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

                credentials.text = $"<color={Main.ModColor}>{Main.ModName}</color> v{Main.PluginShowVersion}";

                credentials.fontSize = 2f;
                credentials.fontSizeMin = 2f;
                credentials.fontSizeMax = 2f;
                credentials.alignment = TextAlignmentOptions.TopRight;

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
                logoTransform.localScale = Vector3.one;
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
                var offset_y = HudManager.InstanceExists ? 1.6f : 0.9f;
                __instance.ModStamp.transform.position = AspectPosition.ComputeWorldPosition(
                    __instance.localCamera, AspectPosition.EdgeAlignments.RightTop,
                    new Vector3(0.4f, offset_y, __instance.localCamera.nearClipPlane + 0.1f));
            }
        }
    }
}