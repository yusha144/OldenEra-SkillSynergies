namespace OldenEra.SkillSynergies;

internal sealed class ChoiceHint
{
    internal string Id;
    internal bool IsPerk;
    internal Box Bounds;
    internal List<SynergyHint> Hints;
}

internal sealed class HintRow
{
    internal string Icon, Name, Note;
}

internal sealed class HintPanel
{
    internal string Title, Subtitle;
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

    internal static HintPanel Build(IReadOnlyList<ChoiceHint> choices, ChoiceHint selected, SynergyCatalog catalog)
    {
        var panel = new HintPanel();
        if (selected != null)
        {
            panel.Title = catalog.Info(selected.Id).Name;
            panel.Subtitle = selected.Hints.Count == 0 ? "С текущими умениями усилений нет"
                : selected.IsPerk ? "Усилен изученными навыками" : "Усилит ваши перки";
            foreach (string id in Related(selected))
            {
                var info = catalog.Info(id);
                bool temporary = !selected.IsPerk && selected.Hints.Any(h => h.Link.PerkId == id && h.Temporary);
                panel.Rows.Add(new HintRow { Icon = info.Icon, Name = info.Name, Note = temporary ? "Временный перк" : "" });
            }
            return panel;
        }
        panel.Title = "Доступные сочетания";
        panel.Subtitle = "Среди предлагаемых вариантов";
        foreach (var choice in choices.Where(c => c.Hints.Count > 0))
        {
            var info = catalog.Info(choice.Id);
            var names = new List<string>();
            foreach (string id in Related(choice))
            {
                bool temporary = !choice.IsPerk && choice.Hints.Any(h => h.Link.PerkId == id && h.Temporary);
                names.Add(catalog.Info(id).Name + (temporary ? " (временно)" : ""));
            }
            panel.Rows.Add(new HintRow { Icon = info.Icon, Name = info.Name,
                Note = (choice.IsPerk ? "Усилен: " : "Усилит: ") + string.Join(", ", names) });
        }
        if (panel.Rows.Count == 0) { panel.Title = "Сейчас усилений нет"; panel.Subtitle = "Среди предлагаемых вариантов"; }
        return panel;
    }
}

internal sealed class HintSelection
{
    internal string Id = "";
    private string _context = "";
    internal void Clear() { Id = ""; _context = ""; }
    internal bool Observe(string context, string hoveredId, IEnumerable<string> visibleIds)
    {
        string previous = Id;
        bool changed = context != _context;
        if (changed) { Id = ""; _context = context; }
        if (!string.IsNullOrEmpty(hoveredId)) Id = hoveredId;
        if (!visibleIds.Contains(Id)) Id = "";
        // Crossing the gap, entering the panel, and scrolling do not clear the last hovered choice.
        return changed || previous != Id;
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
            if (i > start && used + heights[i] > available)
            { pages.Add(new RowPage(start, i - start)); start = i; used = 0; }
            used += heights[i];
        }
        pages.Add(new RowPage(start, heights.Count - start));
        return pages;
    }
}
