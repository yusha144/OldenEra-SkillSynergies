using System.Globalization;
using System.Text.Json;

namespace OldenEra.SkillSynergies;

internal sealed class BonusPart
{
    internal string Key, Attribute, Subject = "", Suffix = "";
    internal decimal Value;
    internal bool Percent, Unsigned, Replacement;
    internal int Rank;
}

internal sealed class BonusChange
{
    internal BonusPart Part;
    internal decimal Base, Added;
    internal bool Comparable = true;
}

internal static class BonusText
{
    private static readonly string[] AdventureSpells = { "neutral_magic_second_sight", "neutral_magic_shadow_form", "neutral_magic_light_gate",
        "neutral_magic_town_portal", "neutral_magic_dimension_door", "neutral_magic_pocket_dimension" };

    // Unknown bonus shapes are rejected as a whole, never turned into a vague actionable hint.
    internal static List<BonusChange> ReadChanges(JsonElement extra, JsonElement baseline,
        IReadOnlyDictionary<string, JsonElement> buffInfos, IReadOnlyDictionary<string, JsonElement> buffs,
        Func<string, string> local)
    {
        var baseParts = new List<BonusPart>();
        foreach (var item in baseline.EnumerateArray())
        {
            if (SynergyCatalog.Text(item, "type") == "heroActionBonusAddition") continue;
            var parts = Read(item, buffInfos, buffs, local);
            if (parts == null) return null;
            baseParts.AddRange(parts);
        }
        var changes = new List<BonusChange>();
        foreach (var item in extra.EnumerateArray())
        {
            var parts = Read(item, buffInfos, buffs, local);
            if (parts == null) return null;
            foreach (var part in parts)
            {
                var originals = baseParts.Where(b => b.Key == part.Key).ToArray();
                if (part.Replacement && originals.Length > 1) return null;
                decimal before = originals.Sum(b => b.Value);
                changes.Add(new BonusChange { Part = part, Base = before, Added = part.Replacement ? part.Value - before : part.Value,
                    Comparable = !part.Replacement || originals.Length == 1 });
            }
        }
        // This exact six-spell set is the game's neutral adventure-magic group. Do not generalize a subset.
        foreach (string suffix in new[] { "flat", "percent" })
        {
            var spellChanges = changes.Where(c => AdventureSpells.Any(s => c.Part.Key.Contains("|" + s + "|")) && c.Part.Key.EndsWith("|" + suffix)).ToArray();
            if (spellChanges.Length == AdventureSpells.Length && spellChanges.All(c => c.Base == spellChanges[0].Base && c.Added == spellChanges[0].Added))
                foreach (var change in spellChanges) change.Part.Attribute = "Стоимость нейтральных походных заклинаний";
        }
        foreach (var group in changes.Where(c => c.Part.Rank > 0).GroupBy(c => c.Part.Attribute + "|" + c.Part.Suffix + "|" + c.Base + "|" + c.Added))
        {
            var ranked = group.ToArray();
            if (ranked.Length == 3 && ranked.Select(c => c.Part.Rank).OrderBy(r => r).SequenceEqual(new[] { 1, 2, 3 }))
                foreach (var change in ranked) change.Part.Subject = "дружественных существ 1-3-го рангов";
        }
        return changes;
    }

    internal static string Preview(IReadOnlyList<SynergyHint> rowHints, IReadOnlyList<SynergyHint> newHints,
        IEnumerable<Synergy> allLinks, HeroBuild hero, bool offeredPerk, bool compact = false)
        => Render(PreviewValues(rowHints, newHints, allLinks, hero, offeredPerk), compact);

    internal static (string Text, string RichText) NumericPreview(IReadOnlyList<SynergyHint> rowHints,
        IReadOnlyList<SynergyHint> newHints, IEnumerable<Synergy> allLinks, HeroBuild hero, bool offeredPerk)
    {
        // The native perk card already supplies the effect and its restrictions. Repeat
        // only distinct numeric changes; equal spell/creature healing shares one line.
        var values = PreviewValues(rowHints, newHints, allLinks, hero, offeredPerk, numbersOnly: true)
            .GroupBy(v => v.RichNumbers).Select(g => g.First()).ToArray();
        return (string.Join("\n", values.Select(v => v.Numbers)), string.Join("\n", values.Select(v => v.RichNumbers)));
    }

    private static List<Rendered> PreviewValues(IReadOnlyList<SynergyHint> rowHints, IReadOnlyList<SynergyHint> newHints,
        IEnumerable<Synergy> allLinks, HeroBuild hero, bool offeredPerk, bool numbersOnly = false)
    {
        var rendered = new List<Rendered>();
        var visited = new HashSet<string>();
        foreach (var hint in rowHints)
            foreach (var change in hint.Link.Changes)
            {
                string perk = hint.Link.PerkId, key = change.Part.Key;
                if (!visited.Add(perk + "|" + key)) continue;
                decimal before = change.Base;
                if (!offeredPerk)
                    foreach (var link in allLinks.Where(l => l.PerkId == perk && l.RequiredSkills.All(hero.Skills.Contains)))
                        before += link.Changes.Where(c => c.Part.Key == key).Sum(c => c.Added);
                decimal total = newHints.Where(h => h.Link.PerkId == perk).Sum(h => h.Link.Changes.Where(c => c.Part.Key == key).Sum(c => c.Added));
                decimal own = rowHints.Where(h => h.Link.PerkId == perk).Sum(h => h.Link.Changes.Where(c => c.Part.Key == key).Sum(c => c.Added));
                string Comparison(bool rich)
                {
                    string Value(decimal value)
                    {
                        string number = Number(value, change.Part), color = NumberColor(change.Part);
                        return rich && color != null ? "<color=#" + color + ">" + number + "</color>" : number;
                    }
                    return !change.Comparable ? (numbersOnly ? "до " : "устанавливается на ") + Value(change.Part.Value)
                        : offeredPerk && own != total ? "ещё " + Value(own) + (numbersOnly ? "; всего " : "; бонус перка ") + Value(before + total)
                        : "с " + Value(before) + " до " + Value(before + total);
                }
                rendered.Add(new Rendered { Part = change.Part, Numbers = Comparison(false), RichNumbers = Comparison(true) });
            }
        return rendered;
    }

    internal static string Describe(IReadOnlyList<BonusChange> changes)
        => Render(changes.Select(c => new Rendered { Part = c.Part, Numbers = c.Comparable
            ? "с " + Number(c.Base, c.Part) + " до " + Number(c.Base + c.Added, c.Part)
            : "устанавливается на " + Number(c.Part.Value, c.Part) }).ToList());

    private sealed class Rendered { internal BonusPart Part; internal string Numbers, RichNumbers; }

    private static string NumberColor(BonusPart part)
    {
        // Color follows the structured stat ID, never a translated description or sign.
        // Key: type | allegiance | receivers | territory | stat [| extra parameters].
        var key = part.Key.Split('|');
        string stat = key.Length > 4 ? key[4] : "";
        if (stat == "override" && key.Length > 5) stat = key[5];
        return stat switch {
            "hp" or "finalHealingBonusPercent" => "8FD69A",
            "gold" or "heroResPercentSet" => "E8C46A",
            "magicCostSchoolSet" or "magicCostSidSet" or "manaBonusPercent" or "coefBattleStatsForMaxMana" => "86C8EC",
            _ => null };
    }

    private static string Render(List<Rendered> values, bool compact = false)
    {
        var lines = new List<string>();
        var merged = new HashSet<Rendered>();
        if (compact)
        {
            // One rule with two sources, not two independent +30% gains. Merge only
            // matching numbers AND scope; keep rank/territory/different amounts separate.
            foreach (var ability in values.Where(v => v.Part.Attribute == "Лечение и воскрешение способностями"
                && v.Part.Subject == "дружественных существ"))
            {
                var spell = values.FirstOrDefault(v => !merged.Contains(v)
                    && v.Part.Attribute == "Лечение и воскрешение заклинаниями" && v.Part.Subject == "героя"
                    && v.Numbers == ability.Numbers && v.Part.Suffix == ability.Part.Suffix);
                if (spell == null) continue;
                lines.Add("Лечение и воскрешение: " + ability.Numbers + ability.Part.Suffix
                    + ".\nЗаклинания героя и способности союзников.");
                merged.Add(ability); merged.Add(spell);
            }
        }
        // Equal changes to several attributes share one sentence; scopes and coefficients must match.
        foreach (var group in values.Where(v => !merged.Contains(v)).GroupBy(v => v.Numbers + "|" + v.Part.Subject + "|" + v.Part.Suffix))
        {
            var first = group.First();
            var names = group.Select(v => v.Part.Attribute).Distinct().ToArray();
            string label = string.Join(", ", names);
            string subject = compact ? first.Part.Subject.Replace("дружественных существ", "союзников") : first.Part.Subject;
            string line = label + (subject.Length == 0 ? "" : " " + subject) + ": " + first.Numbers + first.Part.Suffix + ".";
            if (!lines.Contains(line)) lines.Add(line);
        }
        return string.Join("\n", lines);
    }

    private static string Number(decimal value, BonusPart part)
    {
        if (part.Percent) value *= 100;
        string sign = value < 0 ? "-" : value > 0 && !part.Unsigned ? "+" : "";
        return sign + Math.Abs(value).ToString("0.##", CultureInfo.InvariantCulture) + (part.Percent ? "%" : "");
    }

    private static List<BonusPart> Read(JsonElement bonus, IReadOnlyDictionary<string, JsonElement> infos,
        IReadOnlyDictionary<string, JsonElement> buffs, Func<string, string> local)
    {
        foreach (var property in bonus.EnumerateObject())
            if (property.Name is not ("type" or "parameters" or "receivers" or "actionArea" or "receiverAllegiance")) return null;
        string type = SynergyCatalog.Text(bonus, "type"), area = SynergyCatalog.Text(bonus, "actionArea"), allegiance = SynergyCatalog.Text(bonus, "receiverAllegiance");
        if (area is not ("" or "allied" or "notAllied") || allegiance is not ("" or "ally" or "enemy")) return null;
        var receivers = SynergyCatalog.Strings(bonus, "receivers");
        var p = SynergyCatalog.Strings(bonus, "parameters");
        if (p.Length < 2) return null;
        string target = type == "unitStat" || type == "battleSubskillBonus" ? (allegiance == "enemy" ? "существ противника" : "дружественных существ") : "";
        if (receivers.Length > 0)
        {
            if (receivers.All(r => r is "avatar" or "avatar_nature" or "avatar_unfrozen")) target = "аватара";
            else if (receivers.All(r => int.TryParse(r, out int rank) && rank is >= 1 and <= 7))
                target += " " + string.Join(", ", receivers) + "-го ранга";
            else return null;
        }
        string scope = type + "|" + allegiance + "|" + string.Join(",", receivers) + "|" + area;
        string areaNote = area == "allied" ? " на своей территории" : area == "notAllied" ? " вне своей территории" : "";
        BonusPart Make(string id, string attribute, string raw, bool percent = false, string suffix = "", string subject = null, bool unsigned = false)
        {
            if (!decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal value)) return null;
            return new BonusPart { Key = scope + "|" + id, Attribute = attribute, Value = value, Percent = percent, Subject = subject ?? target,
                Suffix = suffix + areaNote, Unsigned = unsigned,
                Rank = allegiance != "enemy" && receivers.Length == 1 && int.TryParse(receivers[0], out int rank) ? rank : 0 };
        }
        List<BonusPart> One(BonusPart part) => part == null ? null : new List<BonusPart> { part };
        if (type == "battleSubskillBonus")
        {
            if (p.Length != 2 || p[0] != "unit_buff" || infos == null || buffs == null || !infos.TryGetValue(p[1], out var info)
                || !buffs.TryGetValue(SynergyCatalog.Text(info, "sid"), out var buff) || SynergyCatalog.Text(info, "allegiance") != "ally") return null;
            // These passive buffs carry actual numeric rules, not values inferred from translated prose.
            if (!buff.TryGetProperty("infinite", out var infinite) || !infinite.GetBoolean()) return null;
            if (buff.TryGetProperty("activationParams", out var activation) && activation.GetProperty("radius").GetInt32() == 1
                && buff.TryGetProperty("data", out var data) && data.TryGetProperty("stats", out var stats))
            {
                string neighbour = SynergyCatalog.Text(activation, "targetAllegiance");
                if (neighbour is not ("ally" or "enemy")) return null;
                var result = new List<BonusPart>();
                foreach (var stat in stats.EnumerateObject())
                {
                    string label = stat.Name == "offence" ? "Атака" : stat.Name == "defence" ? "Защита" : null;
                    if (label == null) return null;
                    result.Add(Make("near|" + neighbour + "|" + stat.Name, label, stat.Value.GetRawText(), suffix:
                        neighbour == "enemy" ? " за каждый соседний отряд противника" : " за каждый соседний дружественный отряд"));
                }
                return result;
            }
            if (target == "аватара" && buff.TryGetProperty("statOverrides", out var overrides) && overrides.GetArrayLength() == 1)
            {
                var stat = overrides[0];
                if (SynergyCatalog.Text(stat, "stat") != "hp") return null;
                var part = Make("override|hp", "Здоровье", stat.GetProperty("value").GetRawText(), unsigned: true);
                if (part == null) return null;
                part.Replacement = true; return One(part);
            }
            return null;
        }
        if (type == "sideRes" && p.Length == 2) return p[0] switch {
            "gold" => One(Make("gold", "Золото", p[1], suffix: " в день")),
            "dust" => One(Make("dust", "Алхимическая пыль", p[1], suffix: " в день")), _ => null };
        if (type == "astrologyExp" && p.Length == 2 && p[1] == "0.0") return One(Make("astrology", "Очки астрологии", p[0], suffix: " в день"));
        if (type == "cityUnitsIncrement" && p.Length == 2) return One(Make("growth|" + p[0], "Прирост существ " + p[0] + "-го ранга в ваших городах", p[1]));
        if (type is not ("unitStat" or "heroStat" or "heroStatMap" or "heroStatBattle")) return null;
        string key = p[0];
        if (type == "unitStat")
        {
            if (key == "modifierSet" && p.Length == 4 && p[1] == "inDmgMods" && p[2] == "magic_damage")
                return One(Make("magicDamage", "Получаемый магический урон", p[3], true));
            if (p.Length != 2) return null;
            return key switch {
                "hp" => One(Make(key, "Максимальное здоровье", p[1])),
                "energyPerCast" => One(Make(key, "Очки концентрации при атаке", p[1])),
                "energyPerTakeDamage" => One(Make(key, "Очки концентрации при получении урона", p[1])),
                "additionalActionChanceModifier" => One(Make(key, "Шанс дополнительного хода", p[1], true, " за единицу боевого духа")),
                "critChanceModifier" => One(Make(key, "Шанс удачного удара", p[1], true, " за единицу удачи")),
                "heroSpellPowerModifier" => One(Make(key, "Атака", p[1], true, " от силы магии героя")),
                "heroIntelligenceModifier" => One(Make(key, "Защита", p[1], true, " от знаний героя")),
                "heroOffenceModifier" => One(Make(key, "Атака", p[1], true, " от атаки героя")),
                "heroDefenceModifier" => One(Make(key, "Защита", p[1], true, " от защиты героя")),
                "damagePerc" => One(Make(key, "Урон", p[1], true)),
                "speed" => One(Make(key, "Скорость", p[1])), "initiative" => One(Make(key, "Инициатива", p[1])),
                "crit" => One(Make(key, "Урон удачных ударов", p[1], true)),
                "finalHealingBonusPercent" => One(Make(key, "Лечение и воскрешение способностями", p[1], true, " в бою")), _ => null };
        }
        string heroSubject = allegiance == "enemy" ? "героя противника" : "героя";
        if (key == "magicCostSchoolSet" && p.Length == 4 && School(p[1]) is string school)
        {
            var result = new List<BonusPart>();
            // Flat and percentage components are independent; never interpret 0 as the percentage.
            if (!decimal.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out decimal flat)
                || !decimal.TryParse(p[3], NumberStyles.Float, CultureInfo.InvariantCulture, out decimal percent)) return null;
            string subject = "заклинаний школы «" + school + "» " + (allegiance == "enemy" ? "противника" : "героя");
            if (flat != 0) result.Add(Make(key + "|" + p[1] + "|flat", "Стоимость", p[2], suffix: " маны", subject: subject));
            if (percent != 0) result.Add(Make(key + "|" + p[1] + "|percent", "Стоимость", p[3], true, subject: subject));
            return result;
        }
        if (key == "magicCostSidSet" && p.Length == 4 && AdventureSpells.Contains(p[1]) && p[3] == "0")
            return One(Make(key + "|" + p[1] + "|flat", "Стоимость заклинания «" + local(p[1] + "_name") + "»", p[2], suffix: " маны"));
        if (key == "heroResPercentSet" && p.Length == 3 && p[1] == "all")
            return One(Make(key, "Золото и ресурсы, найденные на карте", p[2], true));
        if (key == "spellPowerSchoolSet" && p.Length == 3 && School(p[1]) is string magicSchool)
            return One(Make(key + "|" + p[1], "Сила магии заклинаний школы «" + magicSchool + "»", p[2]));
        if (p.Length != 2) return null;
        return key switch {
            "offence" => One(Make(key, "Атака", p[1], subject: heroSubject)), "defence" => One(Make(key, "Защита", p[1], subject: heroSubject)),
            "spellPower" => One(Make(key, "Сила магии", p[1], subject: heroSubject)), "intelligence" => One(Make(key, "Знания", p[1], subject: heroSubject)),
            "spellPowerPer" => One(Make(key, "Сила магии", p[1], true, subject: heroSubject)),
            "viewRadius" => One(Make(key, "Обзор", p[1], subject: heroSubject)),
            "manaBonusPercent" => One(Make(key, "Максимальная мана", p[1], true, subject: heroSubject)),
            "movementBonus" => One(Make(key, "Очки движения", p[1], suffix: " в начале дня")),
            "movementRestoreBonus" => One(Make(key, "Лимит переноса очков движения", p[1], suffix: " на следующий день")),
            "sideExpPerBonus" => One(Make(key, "Очки законов", p[1], true)),
            "diplomacyCostPerBonus" => One(Make(key, "Стоимость присоединения нейтралов", p[1], true)),
            "diplomacySumValuePerBonus" => One(Make(key, "Оценка мощи армии нейтралами", p[1], true)),
            "magicAttackPerBonus" => One(Make(key, "Урон заклинаниями", p[1], true)),
            "outComingBuffDuration" => One(Make(key, "Длительность накладываемых положительных эффектов", p[1], suffix: " раунда")),
            "outComingDebuffDuration" => One(Make(key, "Длительность накладываемых отрицательных эффектов", p[1], suffix: " раунда")),
            "energyLevelsPerRoundBonus" => One(Make(key, "Заряды концентрации", p[1], suffix: " в начале каждого раунда, кроме первого")),
            "coefBattleStatsForMaxMana" => One(Make(key, "Максимальная мана", p[1], suffix: " за каждую единицу атаки и защиты героя")),
            "crit" => One(Make(key, "Урон удачных ударов", p[1], true, subject: heroSubject)),
            "finalHealingBonusPercent" => One(Make(key, "Лечение и воскрешение заклинаниями", p[1], true, " в бою", heroSubject)), _ => null };
    }

    private static string School(string value) => value switch { "day" => "День", "night" => "Ночь", "space" => "Арканная", "primal" => "Первородная", "neutral" => "Нейтральная", _ => null };
}
