namespace OldenEra.SkillSynergies;

internal sealed class ChoiceHint
{
    internal string Id;
    internal bool IsPerk;
    internal int Level;
    internal Box Bounds;
    internal List<SynergyHint> Hints;
}

internal sealed class HintRow
{
    internal string Icon, Name, Note, Effect = "";
}

internal sealed class HintPanel
{
    internal string ChoiceId;
    internal Box Anchor;
    internal readonly List<HintRow> Rows = new();
}

internal static class HintPresentation
{
    internal static List<string> Related(ChoiceHint choice)
    {
        var ids = new List<string>();
        foreach (var hint in choice.Hints)
            foreach (string id in choice.IsPerk ? hint.Link.RequiredSkills : new[] { hint.Link.PerkId })
                if (!ids.Contains(id)) ids.Add(id);
        return ids;
    }

    internal static List<HintPanel> ForChoices(IReadOnlyList<ChoiceHint> choices, SynergyCatalog catalog, HeroBuild hero)
    {
        var result = new List<HintPanel>();
        foreach (var choice in choices)
        {
            var panel = Build(choice, catalog, hero);
            if (panel != null) result.Add(panel);
        }
        return result;
    }

    internal static HintPanel Build(ChoiceHint choice, SynergyCatalog catalog, HeroBuild hero)
    {
        if (choice == null || choice.Hints.Count == 0) return null;
        var panel = new HintPanel { ChoiceId = choice.Id, Anchor = choice.Bounds };
        // The adjacent native card already names the offered ability. Only its acquired
        // partners, their source and numeric effects belong in this attached annotation.
        foreach (string id in Related(choice))
        {
            var info = catalog.Info(id);
            bool temporary = !choice.IsPerk && choice.Hints.Any(h => h.Link.PerkId == id && h.Temporary);
            string parent = choice.IsPerk ? null : catalog.ParentOf(id, hero);
            string source = parent == null ? "" : "Из навыка: " + catalog.Info(parent).NameAt(hero.Level(parent));
            if (temporary) source += (source.Length == 0 ? "" : "\n") + "Временный перк";
            var related = choice.Hints.Where(h => choice.IsPerk ? h.Link.RequiredSkills.Contains(id) : h.Link.PerkId == id).ToList();
            panel.Rows.Add(new HintRow { Icon = info.IconAt(hero.Level(id)), Name = info.NameAt(hero.Level(id)), Note = source,
                Effect = BonusText.Preview(related, choice.Hints, catalog.Links, hero, choice.IsPerk) });
        }
        return panel.Rows.Count > 0 ? panel : null;
    }
}

internal readonly struct RowPage
{
    internal readonly int Start, Count;
    internal RowPage(int start, int count) { Start = start; Count = count; }
}

internal static class RowPagination
{
    internal static List<RowPage> Split(IReadOnlyList<float> heights, float available)
    {
        var pages = new List<RowPage>();
        int start = 0; float used = 0;
        for (int i = 0; i < heights.Count; i++)
        {
            if (i > start && used + heights[i] > available + 0.25f)
            { pages.Add(new RowPage(start, i - start)); start = i; used = 0; }
            used += heights[i];
        }
        pages.Add(new RowPage(start, heights.Count - start));
        return pages;
    }
}
