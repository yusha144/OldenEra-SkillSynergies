using OldenEra.SkillSynergies;
using System.Text.Json;
using System.IO.Compression;

int checks = 0;
void Check(bool ok, string name) { if (!ok) throw new Exception(name); checks++; }
string Direct(JsonElement extra, JsonElement baseline) => BonusText.Describe(BonusText.ReadChanges(extra, baseline, null, null, key => key));
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
    Check(Direct(json.RootElement, json.RootElement).Contains("вне своей территории"), "Territorial condition cannot disappear from the effect summary");
using (var json = JsonDocument.Parse("[{\"type\":\"unitStat\",\"receivers\":[\"avatar\"],\"parameters\":[\"hp\",\"2\"]}]"))
    Check(Direct(json.RootElement, json.RootElement).Contains("аватара"), "Targeted units are not presented as a universal stat gain");

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
Check(catalog.Links.Count == 154 && catalog.SkippedEffects == 0, $"All 154 mode-specific relations have exact numeric effects; loaded {catalog.Links.Count}, skipped {catalog.SkippedEffects}: {string.Join(",", catalog.UnsupportedEffectPerks)}");
Check(catalog.SkippedConditions == 0, "Current direct action conditions are supported");
Check(catalog.Links.All(l => l.RequiredSkills.Length > 0 && l.RequiredSkills.All(catalog.Infos.ContainsKey)), "Every relation resolves to known skills");
Check(catalog.Links.All(l => !l.Effect.Contains('{') && !l.Effect.Contains('}')), "No untranslated numeric placeholders enter the UI");
Check(catalog.Links.All(l => l.Changes.Count > 0 && l.Changes.All(c => c.Part != null)), "Every displayed synergy has structured numeric changes");
Check(catalog.Links.All(l => !l.Effect.Contains('→') && !l.Effect.Contains('□') && !l.Effect.Contains("описании") && l.Effect.Any(char.IsDigit)), "No missing-arrow glyph or vague description fallback in any current relation");
Check(!catalog.Links.Any(l => l.PerkId == "sub_skill_assault_1"), "Obsolete localized synergy absent from actual bonus rules is ignored");
string Effect(string id) => catalog.Links.First(l => l.PerkId == id).Effect;
Check(Effect("arena_sub_skill_magic_night_2") == "Максимальное здоровье существ противника: с -1 до -2.", "Chronic Weakness shows the exact enemy health reduction");
Check(Effect("sub_skill_trainer_3").Contains("с +2 до +3"), "Veterans uses actual +1 increase instead of trusting the translated doubling claim");
Check(Effect("campaign_sub_skill_trainer_3").Contains("с +2 до +4"), "Campaign Veterans uses its own +2 increase");
Check(Effect("sub_skill_assault_3").Contains("при атаке") && Effect("sub_skill_assault_3").Contains("с +1 до +2"), "Concentration generation retains attack trigger and numeric increase");
Check(Effect("sub_skill_protection_3").Contains("при получении урона"), "Taking-damage trigger remains distinct from attacking");
Check(Effect("sub_skill_formation_4").Contains("кроме первого"), "Round-based concentration excludes first round");
Check(Effect("sub_skill_tactics_1").Contains("с +2 до +4") && Effect("sub_skill_tactics_1").Contains("соседний отряд противника"), "Indirect offensive tactics buff resolves numeric amount and enemy adjacency");
Check(Effect("sub_skill_tactics_4").Contains("соседний дружественный отряд"), "Defensive tactics retains allied adjacency");
Check(Effect("sub_skill_summoner_2") == "Здоровье аватара: с 4 до 5.", "Health override replaces previous override rather than adding 5");
Check(Effect("arena_sub_skill_summoner_2") == "Здоровье аватара: устанавливается на 5.", "Arena additive base and absolute override are not incorrectly summed");
Check(Effect("sub_skill_summoner_1") == "Урон аватара: с +40% до +80%.", "Three avatar variants collapse visually without tripling the numeric bonus");
Check(Effect("sub_skill_logistic_4") == "Стоимость нейтральных походных заклинаний: с -4 до -8 маны.", "Equivalent changes to all six adventure spells remain compact and exact");
Check(Effect("sub_skill_trainer_5").Contains("1-3-го рангов") && Effect("sub_skill_trainer_5").Split('\n').Length == 2, "Elite Guard retains rank restriction while merging equal per-rank entries");
Check(Effect("sub_skill_battlemage_5").Contains("с +15% до +30% от знаний героя"), "Hero-stat scaling is shown as a coefficient, not invented flat defense");
foreach (var link in catalog.Links)
{
    var current = new HeroBuild();
    foreach (string skill in link.RequiredSkills) current.AddSkill(skill, 1);
    var candidate = new ChoiceHint { Id = link.PerkId, IsPerk = true, Hints = SynergyRules.ForOffer(catalog.Links, current, link.PerkId, true) };
    var preview = HintPresentation.Build([candidate], candidate, catalog, current);
    Check(preview.Rows.Count > 0 && preview.Rows.All(r => r.Effect.Any(char.IsDigit) && !r.Effect.Contains("описании")), "Numeric perk-offer coverage: " + link.PerkId + "/" + string.Join(",", link.RequiredSkills));
}
var taxHero = new HeroBuild(); taxHero.AddSkill("skill_economy", 2); taxHero.AddPerk("sub_skill_economy_1", 2);
var taxHint = SynergyRules.ForOffer(catalog.Links, taxHero, "skill_logistic", false).Single();
Check(taxHint.Link.Effect == "Золото: с +250 до +500 в день.", "Real tax collector preview gives exact additional income");
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
var detail = HintPresentation.Build([arina], arina, catalog, arenaHero);
Check(detail.Rows.Count == 1 && detail.Rows[0].Name == catalog.Info("arena_skill_magic_night").NameAt(1), "Detail identifies the owned skill and its current level strengthening this offered perk");
Check(!detail.Rows.Any(r => r.Name.Contains("Арканная")) && detail.Rows[0].Effect.Contains("с +6 до +9") && !detail.Subtitle.Contains("нужно"), "Only current schools contribute and their exact numeric effect is shown");
Check(arenaHero.Perks.Count == 0 && arenaHero.Skills.Count == 2, "Presentation does not change the hero build");
arenaHero.AddSkill("arena_skill_magic_space", 1);
arina.Hints = SynergyRules.ForOffer(catalog.Links, arenaHero, arina.Id, true);
Check(HintPresentation.Build([arina], arina, catalog, arenaHero).Rows.Count == 2, "Each currently learned related school is displayed once");
var arinaTwoSchools = HintPresentation.Build([arina], arina, catalog, arenaHero);
Check(arinaTwoSchools.Rows.All(r => r.Effect.Contains("ещё +3; бонус перка +12")), "Two schools each contribute +3, with the combined perk bonus +12 shown instead of two misleading +9 totals");
var existingMage = new HeroBuild(); existingMage.AddSkill("arena_skill_magic_day", 3); existingMage.AddSkill("arena_skill_magic_night", 2);
existingMage.AddPerk(arina.Id, 2, "arena_skill_magic_day");
var newSchool = new ChoiceHint { Id = "arena_skill_magic_space", Level = 2, Hints = SynergyRules.ForOffer(catalog.Links, existingMage, "arena_skill_magic_space", false) };
Check(HintPresentation.Build([newSchool], newSchool, catalog, existingMage).Rows.Single().Effect.Contains("с +9 до +12"), "Existing active synergies are included in before/after for a newly offered school");
var nightHero = new HeroBuild(); nightHero.AddSkill("arena_skill_magic_night", 2);
var cork = new ChoiceHint { Id = "arena_sub_skill_resistance_1", IsPerk = true, Hints = SynergyRules.ForOffer(catalog.Links, nightHero, "arena_sub_skill_resistance_1", true) };
var corkPanel = HintPresentation.Build([cork], cork, catalog, nightHero);
Check(corkPanel.Rows.Count == 1 && corkPanel.Rows[0].Effect == "Стоимость заклинаний школы «Ночь» противника: с +20% до +40%.", "Screenshot Cork shows exact enemy mana-cost increase for the learned night school");
nightHero.AddSkill("arena_skill_magic_day", 1);
cork.Hints = SynergyRules.ForOffer(catalog.Links, nightHero, cork.Id, true);
corkPanel = HintPresentation.Build([cork], cork, catalog, nightHero);
Check(corkPanel.Rows.Count == 2 && corkPanel.Rows.All(r => r.Effect.Contains("с +20% до +40%")), "Independent school surcharges never stack into +60% on one school");
var corkOwner = new HeroBuild(); corkOwner.AddSkill("arena_skill_resistance", 2); corkOwner.AddPerk(cork.Id, 2, "arena_skill_resistance");
var nightOffer = new ChoiceHint { Id = "arena_skill_magic_night", Level = 2, Hints = SynergyRules.ForOffer(catalog.Links, corkOwner, "arena_skill_magic_night", false) };
Check(HintPresentation.Build([nightOffer], nightOffer, catalog, corkOwner).Rows.Single().Effect.Contains("с +20% до +40%"), "Cork also shows numeric effects in the reverse direction");
var taxChoice = new ChoiceHint { Id = "skill_logistic", IsPerk = false, Hints = SynergyRules.ForOffer(catalog.Links, taxHero, "skill_logistic", false) };
var taxPanel = HintPresentation.Build([taxChoice], taxChoice, catalog, taxHero);
Check(taxPanel.Subtitle == "Усилит ваши перки" && taxPanel.Rows.Single().Name == "Сборщик податей", "Reverse direction identifies the owned perk improved by an offered skill");
Check(taxPanel.Rows.Single().Effect == "Золото: с +250 до +500 в день.", "Owned perk details show the extra benefit absent from the offered main skill description");
Check(HintPresentation.Build([], null, catalog, taxHero).Rows.Count == 0, "Overview cannot recommend skills that are not offered now");

// Screenshot regression: Resoluteness belongs to advanced Leadership, while the offered
// Battle Order activates its conditional bonus. The offered Expert Leadership is not owned yet.
foreach (string prefix in new[] { "", "arena_", "campaign_" })
{
    var leader = new HeroBuild(); leader.AddSkill(prefix + "skill_leadership", 2);
    leader.AddPerk(prefix + "sub_skill_leadership_1", 2, prefix + "skill_leadership");
    var formation = new ChoiceHint { Id = prefix + "skill_formation", Level = 2,
        Hints = SynergyRules.ForOffer(catalog.Links, leader, prefix + "skill_formation", false) };
    var leadership = new ChoiceHint { Id = prefix + "skill_leadership", Level = 3,
        Hints = SynergyRules.ForOffer(catalog.Links, leader, prefix + "skill_leadership", false) };
    var shown = HintPresentation.Build([formation, leadership], formation, catalog, leader);
    var row = shown.Rows.Single();
    Check(row.Name == "Решимость" && row.Note == "Из навыка: Продвинутое лидерство", prefix + "Actual acquired parent skill level is shown");
    Check(row.Effect.Contains("с +1% до +2%") && row.Effect.Contains("за единицу боевого духа") && row.Effect.Contains("дружественных"), prefix + "Resoluteness retains exact before/after, target and per-morale restriction");
    Check(shown.Title == catalog.Info(formation.Id).NameAt(2) && !shown.Title.Contains("лидерство"), prefix + "Activating offer is distinguished from the owned perk's parent");
    Check(leadership.Hints.Count == 0 && leader.Level(prefix + "skill_leadership") == 2 && !leader.Skills.Contains(formation.Id), prefix + "Preview never acquires either offered skill or substitutes offered level for current level");
    leader.AddSkill(formation.Id, 2);
    Check(SynergyRules.ForOffer(catalog.Links, leader, formation.Id, false).Count == 0, prefix + "Existing Battle Order cannot promise another doubling");
}
var unrelated = new HeroBuild(); unrelated.AddSkill("arena_skill_leadership", 2);
Check(catalog.ParentOf("sub_skill_leadership_1", unrelated) == null, "Parent lookup cannot cross game modes");
var ambiguousCatalog = new SynergyCatalog(); ambiguousCatalog.PerkParents["p"] = ["a", "b"];
var ambiguousHero = new HeroBuild(); ambiguousHero.AddSkill("a", 2); ambiguousHero.AddSkill("b", 1);
Check(ambiguousCatalog.ParentOf("p", ambiguousHero) == null, "Ambiguous parent is not invented");
ambiguousHero.AddPerk("p", 1, "b");
Check(ambiguousCatalog.ParentOf("p", ambiguousHero) == "b", "Actual hero perk container disambiguates the source skill");
using (var baseBonus = JsonDocument.Parse("[{\"type\":\"unitStat\",\"parameters\":[\"additionalActionChanceModifier\",\"0.01\"]}]"))
using (var extraBonus = JsonDocument.Parse("[{\"type\":\"unitStat\",\"receiverAllegiance\":\"enemy\",\"parameters\":[\"additionalActionChanceModifier\",\"0.01\"]}]"))
    Check(!Direct(extraBonus.RootElement, baseBonus.RootElement).Contains("дружественных"), "Different target cannot inherit Resoluteness's allied summary");
using (var extraBonus = JsonDocument.Parse("[{\"type\":\"unitStat\",\"chance\":0.5,\"parameters\":[\"hp\",\"2\"]}]"))
using (var baseline = JsonDocument.Parse("[]"))
    Check(BonusText.ReadChanges(extraBonus.RootElement, baseline.RootElement, null, null, key => key) == null, "An unrecognized scope or chance field cannot be silently dropped");

foreach (var size in new[] { (1790f,1227f), (1920f,1080f), (2560f,1440f), (1280f,720f) })
{
    float scale = Math.Clamp(size.Item2 / 1080, 0.75f, 2f);
    var card = new Box(size.Item1 * .065f, size.Item2 * .35f, size.Item1 * .497f, size.Item2 * .18f);
    var lower = new Box(card.X, card.Bottom + 20 * scale, card.W, card.H);
    var anchor = PanelLayout.Group([card, lower]);
    var compact = PanelLayout.Place(size.Item1, size.Item2, scale, [card, lower], anchor, _ => 220 * scale);
    Check(compact.Valid && Math.Abs(compact.X - anchor.Right - 12 * scale) < .01f && compact.Y == anchor.Y,
        "Sidebar hugs the choice group instead of the screen edge");
    Check(compact.H == 220 * scale && compact.Right <= size.Item1 && compact.Bottom <= size.Item2,
        "Short content produces a compact panel at every screen scale");
    var moved = PanelLayout.Place(size.Item1, size.Item2, scale, [card, lower, compact], anchor, _ => 220 * scale);
    Check(!moved.Valid || (!moved.Overlaps(card) && !moved.Overlaps(lower) && !moved.Overlaps(compact)),
        "Anchoring still avoids another native button or panel");
}

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
Check(RowPagination.Split([70.125f, 70.125f], 140.2499f).Count == 1, "Fractional screen scaling cannot create a spurious extra page in a measured panel");

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
