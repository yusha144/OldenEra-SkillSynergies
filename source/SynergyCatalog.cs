using System.IO.Compression;
using System.Text.Json;

namespace OldenEra.SkillSynergies;

internal sealed class SynergyCatalog
{
    internal readonly Dictionary<string, SkillInfo> Infos = new(StringComparer.Ordinal);
    internal readonly List<Synergy> Links = new();
    internal readonly Dictionary<string, List<string>> PerkParents = new(StringComparer.Ordinal);
    internal int SkippedConditions;

    internal void Load(string corePath)
    {
        Infos.Clear(); Links.Clear(); PerkParents.Clear(); SkippedConditions = 0;
        using var zip = ZipFile.OpenRead(corePath);
        var language = new Dictionary<string, string>();
        foreach (var entry in zip.Entries.Where(e => e.FullName.StartsWith("Lang/russian/texts/") && e.FullName.EndsWith(".json")))
        {
            using var doc = Read(entry);
            if (!doc.RootElement.TryGetProperty("tokens", out var tokens)) continue;
            foreach (var token in tokens.EnumerateArray()) language[Text(token, "sid")] = Text(token, "text");
        }
        string Local(string key) => language.TryGetValue(key, out var text) ? text : key;
        var usedPerks = new HashSet<string>();
        foreach (var row in Rows(zip, "DB/heroes_skills/skills/"))
        {
            var levels = row.GetProperty("parametersPerLevel").EnumerateArray().ToArray();
            if (levels.Length == 0) continue;
            string id = Text(row, "id");
            Infos[id] = new SkillInfo { Id = id, Name = Local(Text(row, "name")), Icon = Text(levels[0], "icon"),
                LevelNames = levels.Select(level => Local(Text(level, "name"))).ToArray(),
                LevelIcons = levels.Select(level => Text(level, "icon")).ToArray() };
            foreach (var level in levels)
                foreach (var sid in Strings(level, "subSkills"))
                {
                    usedPerks.Add(sid);
                    if (!PerkParents.TryGetValue(sid, out var parents)) PerkParents[sid] = parents = new List<string>();
                    if (!parents.Contains(id)) parents.Add(id);
                }
        }
        var actions = new Dictionary<string, JsonElement>();
        foreach (var actionRow in Rows(zip, "DB/hero_action_bonuses/")) actions[Text(actionRow, "id")] = actionRow;
        foreach (var row in Rows(zip, "DB/heroes_skills/sub_skills/"))
        {
            string id = Text(row, "id");
            if (!usedPerks.Contains(id)) continue; // Do not use obsolete descriptions or test-only perks.
            Infos[id] = new SkillInfo { Id = id, Name = Local(Text(row, "name")), Icon = Text(row, "icon"), IsPerk = true };
            string description = Local(Text(row, "desc"));
            foreach (var bonus in row.GetProperty("bonuses").EnumerateArray())
            {
                if (Text(bonus, "type") != "heroActionBonusAddition") continue;
                foreach (string actionId in Strings(bonus, "parameters"))
                {
                    if (!actions.TryGetValue(actionId, out var actionSet)) continue;
                    foreach (var action in actionSet.GetProperty("actions").EnumerateArray())
                    {
                        var conditions = action.GetProperty("conditions").EnumerateArray().ToArray();
                        // Only verified learned-skill presence conditions. Never silently drop an unknown condition.
                        if (conditions.Length == 0 || conditions.Any(c => Text(c, "type") != "HeroLearnedSkill" || Strings(c, "parameters").Length != 1))
                        { SkippedConditions++; continue; }
                        var required = conditions.Select(c => Strings(c, "parameters")[0]).Distinct().ToArray();
                        if (required.Any(sid => !Infos.TryGetValue(sid, out var info) || info.IsPerk)) { SkippedConditions++; continue; }
                        var link = new Synergy { PerkId = id, RequiredSkills = required,
                            Effect = BonusText.Describe(action.GetProperty("bonuses"), description, row.GetProperty("bonuses")) };
                        if (!Links.Any(old => old.PerkId == id && old.RequiredSkills.SequenceEqual(required) && old.Effect == link.Effect)) Links.Add(link);
                    }
                }
            }
        }
    }

    internal SkillInfo Info(string id) => Infos.TryGetValue(id, out var info) ? info : new SkillInfo { Id = id, Name = id, Icon = "" };
    internal string ParentOf(string perkId, HeroBuild hero)
    {
        if (!PerkParents.TryGetValue(perkId, out var parents)) return null;
        if (hero.PerkParents.TryGetValue(perkId, out string parent) && parents.Contains(parent) && hero.Skills.Contains(parent)) return parent;
        var learned = parents.Where(hero.Skills.Contains).ToArray();
        return learned.Length == 1 ? learned[0] : null;
    }
    internal static string Text(JsonElement row, string key) => row.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
    internal static string[] Strings(JsonElement row, string key) => row.TryGetProperty(key, out var values) && values.ValueKind == JsonValueKind.Array
        ? values.EnumerateArray().Select(v => v.GetString() ?? "").ToArray() : Array.Empty<string>();

    private static JsonDocument Read(ZipArchiveEntry entry)
    {
        using var stream = entry.Open(); using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
        return JsonDocument.Parse(reader.ReadToEnd());
    }
    private static IEnumerable<JsonElement> Rows(ZipArchive zip, string prefix)
    {
        foreach (var entry in zip.Entries.Where(e => e.FullName.StartsWith(prefix) && e.FullName.EndsWith(".json") && !e.FullName.EndsWith("_test.json")))
        {
            using var doc = Read(entry);
            foreach (var row in doc.RootElement.GetProperty("array").EnumerateArray()) yield return row.Clone();
        }
    }
}
