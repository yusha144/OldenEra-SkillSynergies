using OldenEra.SkillSynergies;
using System.Text.Json;
using System.IO.Compression;

int checks = 0;
void Check(bool ok, string name) { if (!ok) throw new Exception(name); checks++; }
// Window lookup regression: the legacy singleton is null in this game build.
// Registry entries may include unopened windows or destroyed/stale cached objects.
bool Visible(string window) => window == "standard-open" || window == "arena-open";
Check(WindowLookup.Find<string>(null, ["closed", "standard-open"], Visible) == "standard-open", "Standard selection works without the legacy singleton");
Check(WindowLookup.Find<string>(null, ["closed", "arena-open"], Visible) == "arena-open", "Arena selection works without the legacy singleton");
Check(WindowLookup.Find<string>("closed", ["arena-open"], Visible) == "arena-open", "A stale cached window cannot hide a newly opened selection");
Check(WindowLookup.Find<string>("standard-open", ["closed"], Visible) == "standard-open", "Active cached selection stays attached");
Check(WindowLookup.Find<string>(null, [null, "closed"], Visible) == null, "Closed windows and null entries cannot display hints");
Check(WindowLookup.Find<string>("closed", [], Visible) == null, "Closing a selection stops display even if a reference remains");
var economy = new Synergy { PerkId = "tax", RequiredSkills = ["logistics"], Effect = "+250" };
var magic = new Synergy { PerkId = "magic", RequiredSkills = ["night", "day"], Effect = "+3" };
Synergy[] links = [economy, magic];
var hero = new HeroBuild(); hero.AddSkill("economy", 2); hero.AddPerk("tax", 0);
Check(hero.Perks.Count == 0, "Offered, unselected perks must not generate recommendations");
Check(SynergyRules.ForOffer(links, hero, "logistics", false).Count == 0, "No recommendation from inactive perks");
hero.AddPerk("tax", 2);
Check(SynergyRules.ForOffer(links, hero, "logistics", false).Count == 1, "Existing tax perk recommends offered logistics");
var offered = SynergyRules.ForOffer(links, hero, "logistics", false);
Check(offered.Count == 1 && offered[0].Link.PerkId == "tax", "Learning logistics activates the owned perk");
Check(hero.Skills.SetEquals(["economy"]), "Preview never adds the proposed skill to the actual build");
Check(SynergyRules.ForOffer(links, new HeroBuild(), "tax", true).Count == 0, "Missing logistics must not suggest a future synergy for the offered perk");
hero.AddSkill("logistics", 1);
Check(SynergyRules.ForOffer(links, hero, "logistics", false).Count == 0, "Upgrading logistics cannot promise activation again");
Check(SynergyRules.ForOffer(links, hero, "tax", true).Count == 0, "Already acquired perk is not a new improvement");
var hero2 = new HeroBuild(); hero2.AddSkill("logistics", 1);
Check(SynergyRules.ForOffer(links, hero2, "tax", true).Count == 1, "Offered perk uses the second hero's learned requirement");
Check(!hero2.Perks.Contains("tax"), "Preview never acquires the offered perk");
hero2.AddPerk("magic", 1);
Check(hero2.TemporaryPerks.Contains("magic"), "Temporarily active perks retain their temporary label");
Check(SynergyRules.ForOffer(links, hero2, "night", false).Count == 0, "Incomplete AND condition must not highlight the offered skill");
hero2.AddSkill("day", 1);
Check(SynergyRules.ForOffer(links, hero2, "night", false).Single().Temporary, "Last missing condition completes a temporarily owned perk's combination");
hero2.AddSkill("unknown", 0); Check(!hero2.Skills.Contains("unknown"), "Unlearned level-zero slots are ignored");

using (var json = JsonDocument.Parse("[{\"type\":\"heroStatMap\",\"actionArea\":\"notAllied\",\"parameters\":[\"offence\",\"2\"]}]"))
    Check(BonusText.Describe(json.RootElement, "").Contains("вне своей территории"), "Territorial condition cannot disappear from the effect summary");
using (var json = JsonDocument.Parse("[{\"type\":\"unitStat\",\"receivers\":[\"avatar\"],\"parameters\":[\"hp\",\"2\"]}]"))
    Check(BonusText.Describe(json.RootElement, "Бонус удваивается").Contains("при его обычных условиях"), "Targeted units are not presented as a universal stat gain");

foreach (var size in new[] { (1920f,1080f), (2560f,1440f), (1280f,720f) })
{
    float scale = Math.Clamp(size.Item2 / 1080, 0.75f, 2f);
    var obstacles = new[] { new Box(size.Item1 * 0.25f, size.Item2 * 0.2f, size.Item1 * 0.5f, size.Item2 * 0.5f) };
    var panel = PanelLayout.Place(size.Item1, size.Item2, scale, obstacles);
    Check(panel.Valid, "Panel fits outside the central level-up choices");
    Check(!panel.Overlaps(obstacles[0]) && panel.X >= 0 && panel.Y >= 0 && panel.Right <= size.Item1 && panel.Bottom <= size.Item2, "Panel avoids choices and remains on screen");
    var card = new Box(100, 300, 500 * scale, 130 * scale);
    var mark = PanelLayout.CardMark(card, scale);
    Check(mark.Valid && mark.X >= card.X && mark.Y >= card.Y && mark.Right <= card.Right && mark.Bottom <= card.Bottom, "Choice marker stays inside its own card at every supported scale");
    Check(!mark.Overlaps(new Box(card.X, card.Y - card.H, card.W, card.H)), "Marker cannot appear inside the preceding stat bonus card");
}
Check(!PanelLayout.Place(1280, 720, 1, [new Box(0,0,1280,720)]).Valid, "No free space must hide details rather than cover choices");
var crowded = new[] { new Box(0, 64, 1280, 656) };
Check(!PanelLayout.Place(1280, 720, 1, crowded).Valid, "Crowded screen cannot fit full details");
var status = PanelLayout.Status(1280, 720, 1, crowded);
Check(status.Valid && !status.Overlaps(crowded[0]), "Compact status remains visible without covering the crowded selector");
Check(!PanelLayout.Status(1280, 720, 1, [new Box(0,0,1280,720)]).Valid, "Status never covers a completely occupied screen");

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
Check(taxHint.Link.Effect == "+250 золота в день", "Real tax collector preview gives exact additional income");
Check(catalog.Info("sub_skill_economy_1").Name == "Сборщик податей", "Native Russian perk name resolves");
Check(SynergyRules.ForOffer(catalog.Links, taxHero, "arena_skill_logistic", false).Count == 0, "Arena and standard skills are not conflated");
var luckOffer = SynergyRules.ForOffer(catalog.Links, taxHero, "sub_skill_luck_2", true).Single();
Check(luckOffer.Link.Effect.Contains("+25%"), "Economy completes the resource-finding perk");
var unknownHero = new HeroBuild();
Check(SynergyRules.ForOffer(catalog.Links, unknownHero, "sub_skill_luck_2", true).Count == 0, "New hero does not see future economy as a current improvement");

// Screenshot regression: day magic by itself cannot highlight Arina's mage synergy.
var arenaHero = new HeroBuild(); arenaHero.AddSkill("arena_skill_magic_day", 3);
var arina = new ChoiceHint { Id = "arena_sub_skill_magic_day_1", IsPerk = true, Bounds = new Box(100, 300, 500, 130) };
arina.Hints = SynergyRules.ForOffer(catalog.Links, arenaHero, arina.Id, true);
Check(arina.Hints.Count == 0, "Arina has no current synergy without other learned magic schools");
arenaHero.AddSkill("arena_skill_magic_night", 1);
arina.Hints = SynergyRules.ForOffer(catalog.Links, arenaHero, arina.Id, true);
Check(arina.Hints.Count == 1 && arina.Hints[0].Link.RequiredSkills.SequenceEqual(["arena_skill_magic_night"]), "Arina shows only the magic school actually learned");
var detail = HintPresentation.Build([arina], arina, catalog);
Check(detail.Rows.Count == 1 && detail.Rows[0].Name == "Магия ночи", "Detail identifies the owned skill strengthening this offered perk");
Check(!detail.Rows.Any(r => r.Name.Contains("Арканная") || r.Note.Contains("+3")) && !detail.Subtitle.Contains("нужно"), "No future schools or repeated numeric explanations in the panel");
Check(arenaHero.Perks.Count == 0 && arenaHero.Skills.Count == 2, "Presentation does not change the hero build");
arenaHero.AddSkill("arena_skill_magic_space", 1);
arina.Hints = SynergyRules.ForOffer(catalog.Links, arenaHero, arina.Id, true);
Check(HintPresentation.Build([arina], arina, catalog).Rows.Count == 2, "Each currently learned related school is displayed once");
var taxChoice = new ChoiceHint { Id = "skill_logistic", IsPerk = false, Hints = SynergyRules.ForOffer(catalog.Links, taxHero, "skill_logistic", false) };
var taxPanel = HintPresentation.Build([taxChoice], taxChoice, catalog);
Check(taxPanel.Subtitle == "Усилит ваши перки" && taxPanel.Rows.Single().Name == "Сборщик податей", "Reverse direction identifies the owned perk improved by an offered skill");
Check(HintPresentation.Build([], null, catalog).Rows.Count == 0, "Overview cannot recommend skills that are not offered now");

var selection = new HintSelection();
Check(selection.Observe("hero1-stage1", "a", ["a", "b"]) && selection.Id == "a", "Hover selects the inspected perk");
Check(!selection.Observe("hero1-stage1", null, ["a", "b"]) && selection.Id == "a", "Crossing the gap to the panel preserves both selection and page");
Check(!selection.Observe("hero1-stage1", null, ["a", "b"]) && selection.Id == "a", "Reading and scrolling the panel keeps its original context");
Check(selection.Observe("hero1-stage1", "b", ["a", "b"]) && selection.Id == "b", "Hovering another choice replaces the details and resets its page");
Check(selection.Observe("hero2-stage1", null, ["a", "b"]) && selection.Id == "", "Another hero cannot inherit a stale inspected perk");
selection.Observe("hero2-stage1", "a", ["a"]);
Check(selection.Observe("hero2-stage2", null, ["b"]) && selection.Id == "", "A completed choice cannot remain on the next level-up stage");
var rowPages = RowPagination.Split([70, 70, 70], 150);
Check(rowPages.Count == 2 && rowPages[0].Start == 0 && rowPages[0].Count == 2 && rowPages[1].Start == 2 && rowPages[1].Count == 1, "Names and explanations stay together when paging");
Check(RowPagination.Split([], 150).Single().Count == 0, "Empty result has a valid empty page");

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
