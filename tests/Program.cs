using OldenEra.SkillSynergies;
using System.Text.Json;
using System.IO.Compression;

int checks = 0;
void Check(bool ok, string name) { if (!ok) throw new Exception(name); checks++; }
var economy = new Synergy { PerkId = "tax", RequiredSkills = ["logistics"], Effect = "+250" };
var magic = new Synergy { PerkId = "magic", RequiredSkills = ["night", "day"], Effect = "+3" };
Synergy[] links = [economy, magic];
var hero = new HeroBuild(); hero.AddSkill("economy", 2); hero.AddPerk("tax", 0);
Check(hero.Perks.Count == 0, "Offered, unselected perks must not generate recommendations");
Check(SynergyRules.UsefulSkills(links, hero).Count == 0, "No recommendation from inactive perks");
hero.AddPerk("tax", 2);
Check(SynergyRules.UsefulSkills(links, hero).ContainsKey("logistics"), "Existing tax perk recommends logistics");
var offered = SynergyRules.ForOffer(links, hero, "logistics", false);
Check(offered.Count == 1 && offered[0].Ready, "Learning logistics activates the owned perk");
Check(hero.Skills.SetEquals(["economy"]), "Preview never adds the proposed skill to the actual build");
Check(!SynergyRules.ForOffer(links, new HeroBuild(), "tax", true).Single().Ready, "New tax perk lacks logistics");
hero.AddSkill("logistics", 1);
Check(SynergyRules.UsefulSkills(links, hero).Count == 0, "Learned requirements leave the missing list");
Check(SynergyRules.ForOffer(links, hero, "logistics", false).Count == 0, "Upgrading logistics cannot promise activation again");
Check(SynergyRules.ForOffer(links, hero, "tax", true).Single().AlreadyActive, "Already active combo is distinguished");
var hero2 = new HeroBuild(); hero2.AddSkill("logistics", 1);
Check(SynergyRules.ForOffer(links, hero2, "tax", true).Single().Ready, "Offered perk uses the second hero's learned requirement");
Check(SynergyRules.ForOffer(links, hero2, "tax", true).Single().AlreadyActive == false, "A proposed perk is not yet active");
hero2.AddPerk("magic", 1);
Check(hero2.TemporaryPerks.Contains("magic"), "Temporarily active perks retain their temporary label");
Check(!SynergyRules.ForOffer(links, hero2, "night", false).Single().Ready, "One of two conditions is not enough");
Check(SynergyRules.ForOffer(links, hero2, "night", false).Single().Missing.SequenceEqual(["day"]), "Remaining AND condition is visible");
hero2.AddSkill("day", 1);
Check(SynergyRules.ForOffer(links, hero2, "night", false).Single().Ready, "Last missing condition completes the combination");
hero2.AddSkill("unknown", 0); Check(!hero2.Skills.Contains("unknown"), "Unlearned level-zero slots are ignored");

using (var json = JsonDocument.Parse("[{\"type\":\"heroStatMap\",\"actionArea\":\"notAllied\",\"parameters\":[\"offence\",\"2\"]}]"))
    Check(BonusText.Describe(json.RootElement, "").Contains("вне своей территории"), "Territorial condition cannot disappear from the effect summary");
using (var json = JsonDocument.Parse("[{\"type\":\"unitStat\",\"receivers\":[\"avatar\"],\"parameters\":[\"hp\",\"2\"]}]"))
    Check(BonusText.Describe(json.RootElement, "Бонус удваивается").Contains("при его обычных условиях"), "Targeted units are not presented as a universal stat gain");

foreach (var size in new[] { (1920f,1080f), (2560f,1440f), (1280f,720f) })
{
    float scale = size.Item2 / 1080;
    var obstacles = new[] { new Box(size.Item1 * 0.25f, size.Item2 * 0.2f, size.Item1 * 0.5f, size.Item2 * 0.5f) };
    var panel = PanelLayout.Place(size.Item1, size.Item2, scale, obstacles);
    Check(panel.Valid, "Panel fits outside the central level-up choices");
    Check(!panel.Overlaps(obstacles[0]) && panel.X >= 0 && panel.Y >= 0 && panel.Right <= size.Item1 && panel.Bottom <= size.Item2, "Panel avoids choices and remains on screen");
}
Check(!PanelLayout.Place(1280, 720, 1, [new Box(0,0,1280,720)]).Valid, "No free space must hide details rather than cover choices");

// Integration with installed read-only game data; no game methods or saves are accessed.
string core = args.Length > 0 ? args[0] : @"E:\Steam_M2\steamapps\common\Heroes of Might and Magic Olden Era\HeroesOldenEra_Data\StreamingAssets\Core.zip";
var catalog = new SynergyCatalog(); catalog.Load(core);
Check(catalog.Links.Count > 100, "All mode-specific catalogs load");
Check(catalog.SkippedConditions == 0, "Current direct action conditions are supported");
Check(catalog.Links.All(l => l.RequiredSkills.Length > 0 && l.RequiredSkills.All(catalog.Infos.ContainsKey)), "Every relation resolves to known skills");
Check(catalog.Links.All(l => !l.Effect.Contains('{') && !l.Effect.Contains('}')), "No untranslated numeric placeholders enter the UI");
Check(!catalog.Links.Any(l => l.PerkId == "sub_skill_assault_1"), "Obsolete localized synergy absent from actual bonus rules is ignored");
var taxHero = new HeroBuild(); taxHero.AddSkill("skill_economy", 2); taxHero.AddPerk("sub_skill_economy_1", 2);
var taxHint = SynergyRules.ForOffer(catalog.Links, taxHero, "skill_logistic", false).Single();
Check(taxHint.Ready && taxHint.Link.Effect == "+250 золота в день", "Real tax collector preview gives exact additional income");
Check(catalog.Info("sub_skill_economy_1").Name == "Сборщик податей", "Native Russian perk name resolves");
Check(SynergyRules.ForOffer(catalog.Links, taxHero, "arena_skill_logistic", false).Count == 0, "Arena and standard skills are not conflated");
var luckOffer = SynergyRules.ForOffer(catalog.Links, taxHero, "sub_skill_luck_2", true).Single();
Check(luckOffer.Ready && luckOffer.Link.Effect.Contains("+25%"), "Economy completes the resource-finding perk");
var unknownHero = new HeroBuild();
Check(SynergyRules.ForOffer(catalog.Links, unknownHero, "sub_skill_luck_2", true).Single().Missing.SequenceEqual(["skill_economy"]), "New hero sees economy as missing");

// Verify the catalog rejects unknown conditions instead of dropping them.
string temporary = Path.Combine(Path.GetTempPath(), "synergy-test-" + Guid.NewGuid() + ".zip");
try
{
    using (var zip = ZipFile.Open(temporary, ZipArchiveMode.Create))
    {
        void Entry(string name, string json) { using var writer = new StreamWriter(zip.CreateEntry(name).Open()); writer.Write(json); }
        Entry("DB/heroes_skills/skills/skills.json", "{\"array\":[{\"id\":\"s\",\"name\":\"s\",\"parametersPerLevel\":[{\"icon\":\"s\",\"subSkills\":[\"p\"]}]}]}");
        Entry("DB/heroes_skills/sub_skills/sub_skills.json", "{\"array\":[{\"id\":\"p\",\"name\":\"p\",\"desc\":\"p\",\"icon\":\"p\",\"bonuses\":[{\"type\":\"heroActionBonusAddition\",\"parameters\":[\"a\"]}]}]}");
        Entry("DB/hero_action_bonuses/a.json", "{\"array\":[{\"id\":\"a\",\"actions\":[{\"conditions\":[{\"type\":\"HeroLearnedSkill\",\"parameters\":[\"s\"]},{\"type\":\"UnknownCondition\",\"parameters\":[]}],\"bonuses\":[]}]}]}");
    }
    var future = new SynergyCatalog(); future.Load(temporary);
    Check(future.Links.Count == 0 && future.SkippedConditions == 1, "Unknown AND condition makes the whole link unsupported");
}
finally { File.Delete(temporary); }
Console.WriteLine($"PASS: {checks} checks; loaded {catalog.Links.Count} relations across {catalog.Links.Select(l=>l.PerkId).Distinct().Count()} perks; no game/save mutations.");
