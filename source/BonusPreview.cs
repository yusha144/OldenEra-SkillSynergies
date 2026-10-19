namespace OldenEra.SkillSynergies;

internal sealed class BonusContribution
{
    internal string[] Skills;
    internal decimal Value;
    internal bool AfterChoice;
}

internal sealed class BonusValue
{
    internal string PerkId;
    internal BonusPart Part;
    internal decimal Base, Before, After;
    internal bool Comparable;
    internal List<BonusContribution> Contributions;
}

internal static class BonusPreview
{
    private sealed class Match { internal Synergy Link; internal BonusChange Change; }

    // Arithmetic always groups by the exact structured effect key, never by its
    // translated name or equal-looking numbers. The hero remains read-only.
    internal static List<BonusValue> Calculate(IReadOnlyList<SynergyHint> hints,
        IEnumerable<Synergy> allLinks, HeroBuild hero, bool offeredPerk)
    {
        var result = new List<BonusValue>();
        foreach (var perk in hints.GroupBy(h => h.Link.PerkId))
        {
            var added = perk.Select(h => h.Link).Distinct().ToArray();
            var active = offeredPerk ? Array.Empty<Synergy>() : allLinks
                .Where(l => l.PerkId == perk.Key && l.RequiredSkills.All(hero.Skills.Contains)).Distinct().ToArray();
            var combined = active.Concat(added).Distinct().ToArray();
            var newChanges = new List<BonusChange>();
            foreach (var link in added) newChanges.AddRange(link.Changes);
            foreach (var changes in newChanges.GroupBy(c => c.Part.Key))
            {
                var first = changes.First();
                var matches = new List<Match>();
                foreach (var link in combined)
                    foreach (var change in link.Changes)
                        if (change.Part.Key == changes.Key) matches.Add(new Match { Link = link, Change = change });
                // Conflicting absolute assignments have no additive total. The current
                // catalog has only single assignments; never guess their ordering.
                if (matches.Count > 1 && matches.Any(m => m.Change.Part.Replacement)) continue;
                result.Add(new BonusValue {
                    PerkId = perk.Key, Part = first.Part, Base = first.Base, Comparable = first.Comparable,
                    Before = first.Base + active.Sum(l => l.Changes.Where(c => c.Part.Key == changes.Key).Sum(c => c.Added)),
                    After = first.Comparable ? first.Base + matches.Sum(m => m.Change.Added) : first.Part.Value,
                    Contributions = matches.GroupBy(m => string.Join("|", m.Link.RequiredSkills.OrderBy(s => s, StringComparer.Ordinal)))
                        .Select(g => new BonusContribution { Skills = g.First().Link.RequiredSkills.Distinct().ToArray(),
                            Value = g.Sum(m => m.Change.Added),
                            AfterChoice = !offeredPerk && g.Any(m => added.Contains(m.Link)) }).ToList()
                });
            }
        }
        return result;
    }
}
