namespace OldenEra.SkillSynergies;

internal sealed class SkillInfo
{
    internal string Id, Name, Icon;
    internal bool IsPerk;
}

internal sealed class Synergy
{
    internal string PerkId, Effect;
    internal string[] RequiredSkills;
}

internal sealed class HeroBuild
{
    internal readonly HashSet<string> Skills = new(StringComparer.Ordinal);
    internal readonly HashSet<string> Perks = new(StringComparer.Ordinal);
    internal readonly HashSet<string> TemporaryPerks = new(StringComparer.Ordinal);
    internal void AddSkill(string id, int level) { if (level > 0 && !string.IsNullOrEmpty(id)) Skills.Add(id); }
    internal void AddPerk(string id, int status)
    {
        // HeroSubSkill.EStatus: 0 is offered/inactive, 1 temporarily active, 2 chosen permanently.
        if (string.IsNullOrEmpty(id) || status is not (1 or 2)) return;
        Perks.Add(id);
        if (status == 1) TemporaryPerks.Add(id);
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
