namespace OldenEra.SkillSynergies;

internal sealed class SkillInfo
{
    internal string Id, Name, Icon;
    internal bool IsPerk;
    internal string[] LevelNames = Array.Empty<string>(), LevelIcons = Array.Empty<string>();
    internal string NameAt(int level) => level > 0 && level <= LevelNames.Length && !string.IsNullOrEmpty(LevelNames[level - 1]) ? LevelNames[level - 1] : Name;
    internal string IconAt(int level) => level > 0 && level <= LevelIcons.Length ? LevelIcons[level - 1] : Icon;
}

internal sealed class Synergy
{
    internal string PerkId, Effect;
    internal string[] RequiredSkills;
    internal List<BonusChange> Changes = new();
}

internal sealed class HeroBuild
{
    internal readonly HashSet<string> Skills = new(StringComparer.Ordinal);
    internal readonly HashSet<string> Perks = new(StringComparer.Ordinal);
    internal readonly HashSet<string> TemporaryPerks = new(StringComparer.Ordinal);
    internal readonly Dictionary<string, int> SkillLevels = new(StringComparer.Ordinal);
    internal readonly Dictionary<string, string> PerkParents = new(StringComparer.Ordinal);
    internal int Level(string id) => SkillLevels.TryGetValue(id, out int level) ? level : 0;
    internal void AddSkill(string id, int level)
    {
        if (level <= 0 || string.IsNullOrEmpty(id)) return;
        Skills.Add(id); SkillLevels[id] = level;
    }
    internal void AddPerk(string id, int status, string parent = null)
    {
        // HeroSubSkill.EStatus: 0 is offered/inactive, 1 temporarily active, 2 chosen permanently.
        if (string.IsNullOrEmpty(id) || status is not (1 or 2)) return;
        Perks.Add(id);
        if (status == 1) TemporaryPerks.Add(id);
        if (!string.IsNullOrEmpty(parent)) PerkParents[id] = parent;
    }
}

internal sealed class SynergyHint
{
    internal Synergy Link;
    internal bool Temporary;
}

internal static class SynergyRules
{
    internal static List<SynergyHint> ForOffer(IEnumerable<Synergy> links, HeroBuild hero, string id, bool isPerk)
    {
        var result = new List<SynergyHint>();
        // Preview only combinations activated by choosing this offer now.
        // Already acquired perks / upgrades of a learned skill do not activate presence conditions again.
        if (isPerk ? hero.Perks.Contains(id) : hero.Skills.Contains(id)) return result;
        foreach (var link in links)
        {
            if (isPerk ? link.PerkId != id : !hero.Perks.Contains(link.PerkId) || !link.RequiredSkills.Contains(id)) continue;
            if (link.RequiredSkills.Any(skill => !hero.Skills.Contains(skill) && (isPerk || skill != id))) continue;
            result.Add(new SynergyHint { Link = link, Temporary = hero.TemporaryPerks.Contains(link.PerkId) });
        }
        return result;
    }
}
