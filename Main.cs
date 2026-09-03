using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AmongUs.GameOptions;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace HyperRoles
{
    [BepInPlugin(PluginGuid, "HyperRoles", BepInExPluginVersion)]
    [BepInProcess("Among Us.exe")]
    public class Main : BasePlugin
    {
        public static readonly string ModName = "HyperRoles";
        public static readonly string ModColor = "#FF3333";
        public static readonly bool AllowPublicRoom = true;
        public static readonly string ForkId = "HYPER-ROLES";

        public const string PluginGuid = "com.pko.hyperroles";
        public const string BepInExPluginVersion = "1.0.0";
        public const string PluginVersion = "1.0.0";
        public const string PluginShowVersion = "1.0.0";

        public static Main Instance;
        public Harmony Harmony { get; } = new Harmony(PluginGuid);
        public static BepInEx.Logging.ManualLogSource Logger;

        public override void Load()
        {
            Instance = this;

            Logger = BepInEx.Logging.Logger.CreateLogSource("HyperRoles");

            try
            {
                System.Console.OutputEncoding = System.Text.Encoding.UTF8;
            }
            catch
            {
            }

            Harmony.PatchAll(Assembly.GetExecutingAssembly());
            Logger.LogInfo($"{ModName} v.{PluginVersion} loaded!");
        }
    }
}