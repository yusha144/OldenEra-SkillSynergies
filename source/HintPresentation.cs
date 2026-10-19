namespace OldenEra.SkillSynergies;

internal sealed class ChoiceHint
{
    internal string Id;
    internal bool IsPerk;
    internal int Level;
    internal Box Bounds;
    internal List<SynergyHint> Hints;
}

internal sealed class HintSource
{
    internal string Text, RichText;
    internal string[] Icons = Array.Empty<string>();
}

internal sealed class HintRow
{
    internal string Icon, Name = "", Effect, EffectRichText, Conditions = "";
    internal readonly List<HintSource> Sources = new();
}

internal sealed class HintPanel
{
    internal string ChoiceId;
    internal Box Anchor;
    internal readonly List<HintRow> Rows = new();
}

internal static class HintPresentation
{
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
        foreach (var perk in BonusPreview.Calculate(choice.Hints, catalog.Links, hero, choice.IsPerk).GroupBy(v => v.PerkId))
        {
            var effects = perk.Select(v => { var caption = Caption(v.Part);
                return new Presented { Value = v, Label = caption.Label, Conditions = caption.Conditions }; }).ToList();
            MergeHealing(effects);
            // Equivalent avatar variants, the verified six-spell set and rank groups
            // may share a line. Compare their complete scope AND contributions first.
            effects = effects.GroupBy(e => e.Label + "\n" + e.Conditions + "\n" + Signature(e.Value))
                .Select(g => g.First()).ToList();
            var rows = new List<HintRow>();
            foreach (var effect in effects)
            {
                var value = effect.Value;
                bool breakdown = value.Comparable && !value.Part.Replacement && value.Contributions.Count > 1;
                string Amount(bool rich) => !value.Comparable ? "до " + Number(value.After, value.Part, rich)
                    : choice.IsPerk && breakdown ? Number(value.After, value.Part, rich)
                    : "с " + Number(value.Before, value.Part, rich) + " до " + Number(value.After, value.Part, rich);
                var row = new HintRow { Effect = effect.Label + ": " + Amount(false),
                    EffectRichText = Escape(effect.Label) + ": " + Amount(true), Conditions = effect.Conditions };
                if (breakdown)
                    row.Sources.Add(new HintSource { Text = "Бонус перка: " + Number(value.Base, value.Part, false),
                        RichText = "Бонус перка: " + Number(value.Base, value.Part, true) });
                if (choice.IsPerk || breakdown)
                    foreach (var contribution in value.Contributions)
                    {
                        // An AND requirement earns one shared contribution, not a
                        // separate copy of the bonus for every required skill.
                        var skills = contribution.Skills.Select(id => (Info: catalog.Info(id),
                            Level: hero.Skills.Contains(id) ? hero.Level(id) : Math.Max(1, choice.Level))).ToArray();
                        string name = string.Join(" + ", skills.Select(s => s.Info.NameAt(s.Level)));
                        string tail = contribution.AfterChoice ? " (после выбора)" : "";
                        row.Sources.Add(new HintSource { Icons = skills.Select(s => s.Info.IconAt(s.Level)).ToArray(),
                            Text = name + (breakdown ? ": " + Number(contribution.Value, value.Part, false) : "") + tail,
                            RichText = Escape(name) + (breakdown ? ": " + Number(contribution.Value, value.Part, true) : "") + tail });
                    }
                rows.Add(row);
            }
            if (rows.Count == 0) continue;
            if (!choice.IsPerk)
            {
                var info = catalog.Info(perk.Key);
                rows[0].Name = "Усилит ваш перк: " + info.Name;
                rows[0].Icon = info.Icon;
                string parent = catalog.ParentOf(perk.Key, hero);
                string note = parent == null ? "" : "Источник перка: " + catalog.Info(parent).NameAt(hero.Level(parent));
                if (choice.Hints.Any(h => h.Link.PerkId == perk.Key && h.Temporary))
                    note += (note.Length == 0 ? "" : "\n") + "Временный перк";
                if (note.Length > 0) rows[^1].Sources.Add(new HintSource { Text = note,
                    Icons = parent == null ? Array.Empty<string>() : new[] { catalog.Info(parent).IconAt(hero.Level(parent)) } });
            }
            panel.Rows.AddRange(rows);
        }
        return panel.Rows.Count > 0 ? panel : null;
    }

    private sealed class Presented { internal BonusValue Value; internal string Label, Conditions; }

    private static string Signature(BonusValue value) => string.Join("|", value.Base, value.Before, value.After,
        value.Comparable, value.Part.Percent, value.Part.Unsigned, value.Part.Replacement, BonusText.NumberColor(value.Part))
        + "|" + string.Join(";", value.Contributions.Select(c => string.Join(",", c.Skills.OrderBy(s => s, StringComparer.Ordinal))
            + ":" + c.Value + ":" + c.AfterChoice).OrderBy(s => s, StringComparer.Ordinal));

    private static void MergeHealing(List<Presented> effects)
    {
        foreach (var ability in effects.Where(e => e.Value.Part.Attribute == "Лечение и воскрешение способностями"
            && e.Value.Part.Subject == "дружественных существ").ToArray())
        {
            var spell = effects.FirstOrDefault(e => e.Value.Part.Attribute == "Лечение и воскрешение заклинаниями"
                && e.Value.Part.Subject == "героя" && e.Value.Part.Suffix == ability.Value.Part.Suffix
                && Signature(e.Value) == Signature(ability.Value));
            if (spell == null) continue;
            ability.Label = "Лечение и воскрешение";
            ability.Conditions = Capitalize(ability.Value.Part.Suffix.Trim()) + "\nЗаклинания героя и способности союзников";
            effects.Remove(spell);
        }
    }

    private static (string Label, string Conditions) Caption(BonusPart part)
    {
        var key = part.Key.Split('|');
        string stat = key.Length > 4 ? key[4] : "";
        string label = part.Attribute.Replace("Максимальное", "Макс.").Replace("Максимальная", "Макс.")
            .Replace("Шанс дополнительного хода", "Шанс доп. хода")
            .Replace("Длительность накладываемых положительных эффектов", "Длительность усилений")
            .Replace("Длительность накладываемых отрицательных эффектов", "Длительность ослаблений");
        string suffix = part.Suffix.Trim(), condition = "", subject = part.Subject
            .Replace("дружественных существ", "союзников").Replace("существ противника", "врагов");
        if (part.Rank > 0)
        {
            condition = part.Subject.Replace("дружественных существ", "Союзники");
            subject = "союзников";
        }
        if (stat is "energyPerCast" or "energyPerTakeDamage")
        {
            label = "Очки концентрации";
            condition = Join(condition, stat == "energyPerCast" ? "При атаке" : "При получении урона");
        }
        if (stat == "heroResPercentSet") { label = "Золото и ресурсы"; condition = "Найденные на карте"; }
        if (stat == "growth" && key.Length > 5)
        { label = "Прирост существ"; condition = "Существа " + key[5] + "-го ранга в ваших городах"; }
        if ((stat is "magicCostSchoolSet" or "spellPowerSchoolSet") && key.Length > 5)
        {
            string school = key[5] switch { "day" => "дня", "night" => "ночи", "space" => "арканной магии",
                "primal" => "первородной магии", "neutral" => "нейтральной магии", _ => key[5] };
            label = (stat == "magicCostSchoolSet" ? "Стоимость заклинаний " : "Сила магии заклинаний ") + school;
            subject = "";
            if (stat == "magicCostSchoolSet") condition = Join(condition, key[1] == "enemy" ? "Заклинания противника" : "Заклинания героя");
        }
        // Units stay beside the effect; timing, territory and coefficients get
        // their own line. Never drop a restriction to make the caption shorter.
        if (suffix.StartsWith("маны")) { label += " (мана)"; suffix = suffix[4..].Trim(); }
        else if (suffix.StartsWith("раунда")) { label += " (раунды)"; suffix = suffix[6..].Trim(); }
        suffix = suffix.Replace("за единицу ", "за 1 ед. ").Replace("за каждую единицу ", "за 1 ед. ");
        if (subject.Length > 0) label += " " + subject;
        label = label.Replace("«", "").Replace("»", "");
        return (label, Join(condition, Capitalize(suffix)));
    }

    private static string Join(string first, string second) => first.Length == 0 ? second : second.Length == 0 ? first : first + "\n" + second;
    private static string Capitalize(string text) => text.Length == 0 ? "" : char.ToUpperInvariant(text[0]) + text[1..];
    internal static string Escape(string text) => text.Replace("<", "‹").Replace(">", "›");
    private static string Number(decimal value, BonusPart part, bool rich)
    {
        string number = BonusText.Number(value, part), color = BonusText.NumberColor(part);
        return rich && color != null ? "<color=#" + color + ">" + number + "</color>" : number;
    }
}
