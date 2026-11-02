using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using BepInEx.Unity.IL2CPP.Configuration;
using System.Security.Cryptography;

namespace OldenEra.SkillSynergies;

[BepInPlugin("local.oldenera.skillsynergies", "Olden Era Skill Synergies", "0.1.16")]
public sealed class Plugin : BasePlugin
{
    internal static Plugin Instance;
    internal ConfigEntry<bool> Enabled;
    internal ConfigEntry<KeyboardShortcut> ToggleKey;
    internal readonly SynergyCatalog Catalog = new();
    public override void Load()
    {
        Instance = this;
        Enabled = Config.Bind("Display", "Enabled", true, "Initial visibility of skill hints. Configure the toggle in OldenEra.hotkeys.cfg; it applies only inside skill selection.");
        ToggleKey = HotkeySettings.Load("SkillSynergies");
        try
        {
            if (!Matches("GameAssembly.dll", "20AD026C2E10ABFCB17320E8174B611551857B5D97D59BEBC29142B28CA45CB3")
                || !Matches("HeroesOldenEra_Data/il2cpp_data/Metadata/global-metadata.dat", "BB50ABC2B3CBFCFA61617674A723761A5542831C263A34C6A70486840863F846"))
            { Log.LogWarning("Skill Synergies disabled: unsupported game build (expected Steam 26321935)."); return; }
            Catalog.Load(Path.Combine(Paths.GameRootPath, "HeroesOldenEra_Data", "StreamingAssets", "Core.zip"));
            AddComponent<SynergyOverlay>();
            Log.LogInfo($"Skill Synergies 0.1.16 loaded: {Catalog.Links.Count} numeric combinations, {Catalog.SkippedConditions} unsupported condition groups and {Catalog.SkippedEffects} unsupported effects skipped. Effect summaries with separate conditions, source contributions and native perk/skill icons. Initial visibility={Enabled.Value}. Toggle={ToggleKey.Value}; configure OldenEra.hotkeys.cfg.");
            if (Catalog.UnsupportedEffectPerks.Count > 0) Log.LogWarning("Unsupported perk effects: " + string.Join(", ", Catalog.UnsupportedEffectPerks));
        }
        catch (Exception error) { Log.LogError($"Skill Synergies could not start: {error}"); }
    }
    private static bool Matches(string path, string expected)
    {
        using var stream = File.OpenRead(Path.Combine(Paths.GameRootPath, path));
        using var hash = SHA256.Create();
        return Convert.ToHexString(hash.ComputeHash(stream)) == expected;
    }
}
