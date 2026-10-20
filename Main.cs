using System.Reflection;
using AmongUs.GameOptions;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using HyperRoles.Modules;
using HyperRoles.Roles.Impostor;
using HyperRoles.Roles.Neutral;

namespace HyperRoles
{
    [BepInPlugin(PluginGuid, "HyperRoles", BepInExPluginVersion)]
    [BepInProcess("Among Us.exe")]
    public class Main : BasePlugin
    {
        public static readonly string ModName = "HyperRoles";
        public static readonly string ModColor = "#FF3333";
        public static readonly bool AllowPublicRoom = false;
        public static readonly string ForkId = "HYPER-ROLES";

        public const string PluginGuid = "com.pko.hyperroles";
        public const string BepInExPluginVersion = "1.0.0.0";
        public const string PluginVersion = "1.0.0.0";
        public const string PluginShowVersion = "1.0.0.0";
        public static bool IsDebugMode = false;
        public static bool NoEnd = false;
        public static Main Instance;
        public Harmony Harmony { get; } = new Harmony(PluginGuid);
        public static BepInEx.Logging.ManualLogSource Logger;
        public static NormalGameOptionsV12 NormalOptions => GameOptionsManager.Instance.currentNormalGameOptions;

        public override void Load()
        {
            Instance = this;
            Translator.LoadLangs();
            Logger = BepInEx.Logging.Logger.CreateLogSource("HyperRoles");

            try
            {
                System.Console.OutputEncoding = System.Text.Encoding.UTF8;
            }
            catch
            {
            }

            JackalSidekickButton.Register();
            CamouflagerButton.Register();

            Harmony.PatchAll(Assembly.GetExecutingAssembly());
            Logger.LogInfo($"{ModName} v.{PluginVersion} loaded!");
        }
    }
}