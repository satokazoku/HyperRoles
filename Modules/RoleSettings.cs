using System.Collections.Generic;
using System.IO;
using BepInEx;
using RoleId = HyperRoles.CustomRoleManager.RoleId;

namespace HyperRoles;

/// <summary>1つの役職の設定</summary>
public class RoleSetting
{
    public int SpawnRate = 100;
    public Dictionary<int, float> Values = new();
}

/// <summary>
/// 役職ごとの設定の保存先。IL2CPPの型競合を避けるため、自前のテキスト形式でセーブ・ロードを行う。
/// </summary>
public static class RoleSettings
{
    private static readonly string FilePath = Path.Combine(Paths.ConfigPath, "HyperRoles_RoleSettings.txt");
    private static readonly Dictionary<string, RoleSetting> roleSettingsMap = new();
    private static bool isLoaded = false;

    /// <summary>ファイルから設定を読み込む</summary>
    public static void Load()
    {
        if (isLoaded) return;
        isLoaded = true;

        if (!File.Exists(FilePath)) return;

        try
        {
            var lines = File.ReadAllLines(FilePath);
            string currentRole = null;
            RoleSetting currentSetting = null;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;

                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    currentRole = trimmed.Substring(1, trimmed.Length - 2);
                    currentSetting = new RoleSetting();
                    roleSettingsMap[currentRole] = currentSetting;
                }
                else if (currentSetting != null && trimmed.Contains("="))
                {
                    var parts = trimmed.Split('=');
                    if (parts.Length == 2)
                    {
                        var key = parts[0].Trim();
                        var valStr = parts[1].Trim();

                        if (key == "SpawnRate")
                        {
                            if (int.TryParse(valStr, out var sr))
                                currentSetting.SpawnRate = sr;
                        }
                        else if (int.TryParse(key, out var optId))
                        {
                            if (float.TryParse(valStr, out var optVal))
                                currentSetting.Values[optId] = optVal;
                        }
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            BepInEx.Logging.Logger.CreateLogSource("RoleSettings").LogError($"Failed to load settings: {e}");
        }
    }

    /// <summary>設定をファイルに保存する</summary>
    public static void Save()
    {
        try
        {
            var lines = new List<string>();
            foreach (var kvp in roleSettingsMap)
            {
                lines.Add($"[{kvp.Key}]");
                lines.Add($"SpawnRate={kvp.Value.SpawnRate}");
                foreach (var valKvp in kvp.Value.Values)
                {
                    lines.Add($"{valKvp.Key}={valKvp.Value}");
                }
                lines.Add(""); // 役職ごとの空行
            }
            File.WriteAllLines(FilePath, lines);
        }
        catch (System.Exception e)
        {
            BepInEx.Logging.Logger.CreateLogSource("RoleSettings").LogError($"Failed to save settings: {e}");
        }
    }

    public static RoleSetting Get(RoleId id)
    {
        Load();
        string key = id.ToString();

        if (!roleSettingsMap.TryGetValue(key, out var setting))
        {
            setting = new RoleSetting();
            roleSettingsMap[key] = setting;
        }
        return setting;
    }

    public static int SpawnRate(RoleId id) => Get(id).SpawnRate;

    public static float GetOption(RoleId id, int optionId, float defaultValue)
        => Get(id).Values.TryGetValue(optionId, out var value) ? value : defaultValue;
}