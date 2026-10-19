using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP.Configuration;
using UnityEngine;

namespace OldenEra.SkillSynergies;

internal static class HotkeySettings
{
    internal static ConfigEntry<KeyboardShortcut> Load(string name, string configDirectory = null)
    {
        string directory = configDirectory ?? Paths.ConfigPath;
        string path = Path.Combine(directory, "OldenEra.hotkeys.cfg");
        // Use the previous config when the current shared file is absent.
        string legacy = Path.Combine(directory, "yusha144.hotkeys.cfg");
        if (!File.Exists(path) && File.Exists(legacy)) path = legacy;
        bool exists = File.Exists(path);
        // Both independent plugins read the same file once at startup. Do not
        // overwrite the other plugin's settings or the user's comments on load.
        var config = new ConfigFile(path, false) { SaveOnConfigSet = false };
        var map = config.Bind("Hotkeys", "MapHints", new KeyboardShortcut(KeyCode.F8),
            "Cycle map markers. Examples: F8, F8 + LeftControl, None. Restart the game after editing.");
        var skills = config.Bind("Hotkeys", "SkillSynergies", new KeyboardShortcut(KeyCode.F9),
            "Toggle skill hints. Examples: F9, F9 + LeftControl, None.");
        if (!exists) config.Save();
        return name == "MapHints" ? map : skills;
    }
}

