using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using System.Security.Cryptography;

namespace OldenEra.SkillSynergies;

[BepInPlugin("local.oldenera.skillsynergies", "Olden Era Skill Synergies", "0.1.4")]
public sealed class Plugin : BasePlugin
{
    internal static Plugin Instance;
    internal ConfigEntry<bool> Enabled;
    internal readonly SynergyCatalog Catalog = new();
    public override void Load()
    {
        Instance = this;
        Enabled = Config.Bind("Display", "Enabled", true, "Initial visibility of skill hints. F9 toggles hints for this session, only while the skill selection window is open.");
        try
        {
            if (!Matches("GameAssembly.dll", "D9972B0E7F7DD1C17758808E2F1B80E0A728946416D9A41DEAA99BAD744DE53A")
                || !Matches("HeroesOldenEra_Data/il2cpp_data/Metadata/global-metadata.dat", "99117DC93F4B427967A2FCA49378E1B39B3202401B15234A98328FAA7D489817"))
            { Log.LogWarning("Skill Synergies disabled: unsupported game build (expected Steam 25672315)."); return; }
            Catalog.Load(Path.Combine(Paths.GameRootPath, "HeroesOldenEra_Data", "StreamingAssets", "Core.zip"));
            AddComponent<SynergyOverlay>();
            Log.LogInfo($"Skill Synergies 0.1.4 loaded: {Catalog.Links.Count} numeric combinations, {Catalog.SkippedConditions} unsupported condition groups and {Catalog.SkippedEffects} unsupported effects skipped. Current-build combinations only. Initial visibility={Enabled.Value}. F9 toggles hints inside skill selection, for this session only.");
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
