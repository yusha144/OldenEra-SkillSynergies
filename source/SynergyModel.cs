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
    internal string[] Missing;
    internal bool Ready, AlreadyActive, Temporary;
}

internal static class SynergyRules
{
    internal static List<SynergyHint> ForOffer(IEnumerable<Synergy> links, HeroBuild hero, string id, bool isPerk)
    {
        var result = new List<SynergyHint>();
        foreach (var link in links)
        {
            if (isPerk ? link.PerkId != id : !hero.Perks.Contains(link.PerkId) || !link.RequiredSkills.Contains(id)) continue;
            // A presence condition has already been met at level 1; upgrading to 2/3 does not activate it again.
            if (!isPerk && hero.Skills.Contains(id)) continue;
            var missing = link.RequiredSkills.Where(skill => !hero.Skills.Contains(skill) && (isPerk || skill != id)).ToArray();
            result.Add(new SynergyHint { Link = link, Missing = missing, Ready = missing.Length == 0,
                AlreadyActive = isPerk && hero.Perks.Contains(id) && missing.Length == 0,
                Temporary = hero.TemporaryPerks.Contains(link.PerkId) });
        }
        return result.OrderByDescending(h => h.Ready).ToList();
    }

    internal static Dictionary<string, List<Synergy>> UsefulSkills(IEnumerable<Synergy> links, HeroBuild hero)
    {
        var result = new Dictionary<string, List<Synergy>>(StringComparer.Ordinal);
        foreach (var link in links.Where(link => hero.Perks.Contains(link.PerkId)))
            foreach (string missing in link.RequiredSkills.Where(id => !hero.Skills.Contains(id)).Distinct())
            {
                if (!result.TryGetValue(missing, out var list)) result[missing] = list = new();
                if (!list.Contains(link)) list.Add(link);
            }
        return result;
    }
}
