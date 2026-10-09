using System.Globalization;
using System.Text.Json;

namespace OldenEra.SkillSynergies;

internal static class BonusText
{
    internal static string Describe(JsonElement bonuses, string description)
    {
        var lines = new List<string>(); bool unresolved = false;
        foreach (var bonus in bonuses.EnumerateArray())
        {
            string line = DescribeOne(bonus);
            if (line == null) unresolved = true;
            else if (!lines.Contains(line)) lines.Add(line);
        }
        if (unresolved)
        {
            // Retain all original situational/target restrictions in the native description.
            if (description.Contains("Бонус удваивается", StringComparison.OrdinalIgnoreCase))
                return "Бонус перка ×2 — при его обычных условиях.";
            return "Дополнительное усиление — условия в описании перка.";
        }
        return lines.Count > 0 ? string.Join("; ", lines) : "Дополнительный эффект перка.";
    }

    private static string DescribeOne(JsonElement bonus)
    {
        var p = SynergyCatalog.Strings(bonus, "parameters");
        var receivers = SynergyCatalog.Strings(bonus, "receivers");
        // Targeted unit ranks/avatars and indirect battle effects need the game's full description.
        if (p.Length < 2 || receivers.Length > 0) return null;
        string type = SynergyCatalog.Text(bonus, "type"), key = p[0];
        string Number(string raw, bool percent = false)
        {
            if (!decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return null;
            if (percent) value *= 100;
            return (value >= 0 ? "+" : "−") + Math.Abs(value).ToString("0.##", CultureInfo.InvariantCulture) + (percent ? "%" : "");
        }
        string Signed(string label, string raw, bool percent = false) => Number(raw, percent) is string n ? n + " " + label : null;
        if (type == "sideRes") return key switch {
            "gold" => Signed("золота в день", p[1]), "dust" => Signed("алхимической пыли в день", p[1]), _ => null };
        if (type == "astrologyExp" && p[1] == "0.0") return Signed("очков астрологии в день", p[0]);
        if (type == "cityUnitsIncrement") return Signed("к приросту существ ранга " + key, p[1]);
        if (type is not ("heroStat" or "heroStatMap" or "heroStatBattle")) return null;
        string text = key switch {
            "offence" => Signed("к атаке", p[1]), "defence" => Signed("к защите", p[1]),
            "spellPower" => Signed("к силе магии", p[1]), "intelligence" => Signed("к знаниям", p[1]),
            "viewRadius" => Signed("к обзору", p[1]), "manaBonusPercent" => Signed("максимальной маны", p[1], true),
            "movementBonus" => Signed("очков движения в начале дня", p[1]),
            "movementRestoreBonus" => Signed("к лимиту переноса очков движения", p[1]),
            "sideExpPerBonus" => Signed("очков законов", p[1], true),
            "diplomacyCostPerBonus" => Signed("к стоимости присоединения нейтралов", p[1], true),
            "diplomacySumValuePerBonus" => Signed("к оценке мощи армии нейтралами", p[1], true),
            "magicAttackPerBonus" => Signed("урона заклинаниями", p[1], true),
            "outComingBuffDuration" => Signed("раунда длительности положительных эффектов", p[1]),
            "outComingDebuffDuration" => Signed("раунда длительности отрицательных эффектов", p[1]),
            "heroResPercentSet" when p.Length == 3 && p[1] == "all" => Signed("золота и ресурсов, найденных на карте", p[2], true),
            "spellPowerSchoolSet" when p.Length == 3 => Signed("к силе магии заклинаний школы «" + School(p[1]) + "»", p[2]),
            _ => null
        };
        if (text == null) return null;
        string area = SynergyCatalog.Text(bonus, "actionArea");
        if (area == "allied") text += " (на своей территории)";
        else if (area == "notAllied") text += " (вне своей территории)";
        else if (!string.IsNullOrEmpty(area)) return null;
        string allegiance = SynergyCatalog.Text(bonus, "receiverAllegiance");
        if (allegiance == "enemy") text += " у противника";
        else if (!string.IsNullOrEmpty(allegiance) && allegiance != "ally") return null;
        return text;
    }
    private static string School(string value) => value switch { "day" => "День", "night" => "Ночь", "space" => "Арканная", "primal" => "Первородная", _ => value };
}
