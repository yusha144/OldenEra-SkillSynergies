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
    var preview = HintPresentation.Build(candidate, catalog, current);
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
var detail = HintPresentation.Build(arina, catalog, arenaHero);
Check(detail.Rows.Count == 1 && detail.Rows[0].Name == catalog.Info("arena_skill_magic_night").NameAt(1), "Detail identifies the owned skill and its current level strengthening this offered perk");
Check(detail.Rows[0].Icon == catalog.Info("arena_skill_magic_night").IconAt(1) && string.IsNullOrEmpty(detail.Rows[0].SourceIcon),
    "An offered perk retains its supporting skill icon without duplicating it as another source icon");
Check(!detail.Rows.Any(r => r.Name.Contains("Арканная")) && detail.Rows[0].Effect.Contains("с +6 до +9"), "Only current schools contribute and their exact numeric effect is shown");
Check(arenaHero.Perks.Count == 0 && arenaHero.Skills.Count == 2, "Presentation does not change the hero build");
arenaHero.AddSkill("arena_skill_magic_space", 1);
arina.Hints = SynergyRules.ForOffer(catalog.Links, arenaHero, arina.Id, true);
Check(HintPresentation.Build(arina, catalog, arenaHero).Rows.Count == 2, "Each currently learned related school is displayed once");
var arinaTwoSchools = HintPresentation.Build(arina, catalog, arenaHero);
Check(arinaTwoSchools.Rows.All(r => r.Effect.Contains("ещё +3; бонус перка +12")), "Two schools each contribute +3, with the combined perk bonus +12 shown instead of two misleading +9 totals");
var existingMage = new HeroBuild(); existingMage.AddSkill("arena_skill_magic_day", 3); existingMage.AddSkill("arena_skill_magic_night", 2);
existingMage.AddPerk(arina.Id, 2, "arena_skill_magic_day");
var newSchool = new ChoiceHint { Id = "arena_skill_magic_space", Level = 2, Hints = SynergyRules.ForOffer(catalog.Links, existingMage, "arena_skill_magic_space", false) };
Check(HintPresentation.Build(newSchool, catalog, existingMage).Rows.Single().Effect.Contains("с +9 до +12"), "Existing active synergies are included in before/after for a newly offered school");
var nightHero = new HeroBuild(); nightHero.AddSkill("arena_skill_magic_night", 2);
var cork = new ChoiceHint { Id = "arena_sub_skill_resistance_1", IsPerk = true, Hints = SynergyRules.ForOffer(catalog.Links, nightHero, "arena_sub_skill_resistance_1", true) };
var corkPanel = HintPresentation.Build(cork, catalog, nightHero);
Check(corkPanel.Rows.Count == 1 && corkPanel.Rows[0].Effect == "Стоимость заклинаний школы «Ночь» противника: с +20% до +40%.", "Screenshot Cork shows exact enemy mana-cost increase for the learned night school");
nightHero.AddSkill("arena_skill_magic_day", 1);
cork.Hints = SynergyRules.ForOffer(catalog.Links, nightHero, cork.Id, true);
corkPanel = HintPresentation.Build(cork, catalog, nightHero);
Check(corkPanel.Rows.Count == 2 && corkPanel.Rows.All(r => r.Effect.Contains("с +20% до +40%")), "Independent school surcharges never stack into +60% on one school");
var corkOwner = new HeroBuild(); corkOwner.AddSkill("arena_skill_resistance", 2); corkOwner.AddPerk(cork.Id, 2, "arena_skill_resistance");
var nightOffer = new ChoiceHint { Id = "arena_skill_magic_night", Level = 2, Hints = SynergyRules.ForOffer(catalog.Links, corkOwner, "arena_skill_magic_night", false) };
Check(HintPresentation.Build(nightOffer, catalog, corkOwner).Rows.Single().Effect.Contains("с +20% до +40%"), "Cork also shows numeric effects in the reverse direction");
var taxChoice = new ChoiceHint { Id = "skill_logistic", IsPerk = false, Hints = SynergyRules.ForOffer(catalog.Links, taxHero, "skill_logistic", false) };
var taxPanel = HintPresentation.Build(taxChoice, catalog, taxHero);
Check(taxPanel.Rows.Single().Name == "Усилит ваш перк: Сборщик податей", "Reverse direction explicitly identifies the owned perk improved by this choice");
Check(taxPanel.Rows.Single().Effect == "Золото: с +250 до +500 в день.", "Owned perk details show the extra benefit absent from the offered main skill description");
Check(HintPresentation.ForChoices([], catalog, taxHero).Count == 0, "No offered choices means no annotations");

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
    var shown = HintPresentation.Build(formation, catalog, leader);
    var row = shown.Rows.Single();
    Check(row.Name == "Усилит ваш перк: Решимость" && row.Note == "Источник перка: Продвинутое лидерство", prefix + "Actual acquired parent skill level is distinguished from the activating choice");
    Check(!string.IsNullOrEmpty(row.SourceIcon) && row.SourceIcon == catalog.Info(prefix + "skill_leadership").IconAt(2)
        && row.Icon == catalog.Info(prefix + "sub_skill_leadership_1").Icon,
        prefix + "Owned perk retains its own icon and gains a separate current-level parent icon, not the offered upgrade or activating skill");
    Check(row.Effect.Contains("с +1% до +2%") && row.Effect.Contains("за единицу боевого духа") && row.Effect.Contains("союзников"), prefix + "Resoluteness retains exact before/after, target and per-morale restriction");
    Check(shown.ChoiceId == formation.Id && shown.Rows.All(r => r.Name != catalog.Info(formation.Id).NameAt(2)), prefix + "Activating offer is distinguished from the owned perk's parent");
    Check(leadership.Hints.Count == 0 && leader.Level(prefix + "skill_leadership") == 2 && !leader.Skills.Contains(formation.Id), prefix + "Preview never acquires either offered skill or substitutes offered level for current level");
    leader.AddSkill(formation.Id, 2);
    Check(SynergyRules.ForOffer(catalog.Links, leader, formation.Id, false).Count == 0, prefix + "Existing Battle Order cannot promise another doubling");
}
var unrelated = new HeroBuild(); unrelated.AddSkill("arena_skill_leadership", 2);
Check(catalog.ParentOf("sub_skill_leadership_1", unrelated) == null, "Parent lookup cannot cross game modes");
// Lord Rufus screenshots: Arcane Magic owns Endless Life, but learning Summoning
// is what activates the healing increase. Upgrading Arcane is not that condition.
foreach (string prefix in new[] { "", "arena_", "campaign_" })
{
    string arcaneId = prefix + "skill_magic_space", summonerId = prefix + "skill_summoner";
    string lifeId = prefix + "sub_skill_magic_space_2";
    var healer = new HeroBuild(); healer.AddSkill(arcaneId, 2); healer.AddPerk(lifeId, 2, arcaneId);
    var healing = catalog.Links.Single(l => l.PerkId == lifeId);
    Check(healing.RequiredSkills.SequenceEqual([summonerId]), prefix + "Endless Life requires Summoning, not another Arcane level");
    Check(SynergyRules.ForOffer(catalog.Links, healer, arcaneId, false).Count == 0,
        prefix + "Offering Expert Arcane does not newly double the owned healing perk");
    var summoningHints = SynergyRules.ForOffer(catalog.Links, healer, summonerId, false);
    Check(summoningHints.Count == 1 && summoningHints[0].Link.PerkId == lifeId && healing.Effect.Contains("с +30% до +60%"),
        prefix + "Offering Summoning doubles the owned healing perk from 30 to 60 percent");
    var summon = new ChoiceHint { Id = summonerId, Level = 2, Bounds = new Box(94, 659, 886, 229), Hints = summoningHints };
    var arcane = new ChoiceHint { Id = arcaneId, Level = 3, Bounds = new Box(94, 925, 886, 229),
        Hints = SynergyRules.ForOffer(catalog.Links, healer, arcaneId, false) };
    var healingPanel = HintPresentation.ForChoices([summon, arcane], catalog, healer).Single();
    Check(healingPanel.ChoiceId == summonerId && healingPanel.Anchor.Y == summon.Bounds.Y,
        prefix + "Screenshot annotation belongs to the activating Summoning card, not its owned Arcane source");
    Check(healingPanel.Rows.Single().Name == "Усилит ваш перк: Всякая жизнь бесконечна"
        && healingPanel.Rows.Single().Note == "Источник перка: Продвинутая арканная магия",
        prefix + "Screenshot wording distinguishes the upgraded perk from its current source skill");
    var healingBox = PanelLayout.PlaceCards(1521, 1195, 1195f / 1080, [summon.Bounds, arcane.Bounds], [healingPanel.Anchor],
        (_, _) => 240, [summon.Bounds, arcane.Bounds]).Single();
    Check(healingBox.Valid && healingBox.Y == summon.Bounds.Y && healingBox.Bottom < arcane.Bounds.Y,
        prefix + "Screenshot healing explanation stays at the activating card and does not spill into Arcane");
    healer.AddSkill(arcaneId, 3);
    Check(SynergyRules.ForOffer(catalog.Links, healer, summonerId, false).Count == 1,
        prefix + "Taking Expert Arcane leaves the Summoning combination available at the next level");
    healer.AddSkill(summonerId, 2);
    Check(SynergyRules.ForOffer(catalog.Links, healer, summonerId, false).Count == 0,
        prefix + "Once Summoning is learned its already active healing bonus is not promised again");
}
// Local-map screenshot: the same healing increase must not be explained twice.
var screenshotHero = new HeroBuild(); screenshotHero.AddSkill("skill_summoner", 1);
var screenshotChoice = new ChoiceHint { Id = "sub_skill_magic_space_2", IsPerk = true, Bounds = new Box(123, 815, 852, 160),
    Hints = SynergyRules.ForOffer(catalog.Links, screenshotHero, "sub_skill_magic_space_2", true) };
var screenshotRow = HintPresentation.Build(screenshotChoice, catalog, screenshotHero).Rows.Single();
Check(screenshotRow.Name == "Основы призыва аватара" && !string.IsNullOrEmpty(screenshotRow.Icon)
    && screenshotRow.Effect == "Лечение и воскрешение: с +30% до +60% в бою.\nЗаклинания героя и способности союзников.",
    "Local-map healing hint keeps the source icon and both healing sources but states identical numbers only once");
var screenshotRows = new[] { new Box(123,637,852,160), screenshotChoice.Bounds, new Box(123,993,852,160) };
var screenshotBox = PanelLayout.PlaceCards(1644, 1196, 1196f / 1080, screenshotRows, [screenshotChoice.Bounds],
    (_, _) => 230, screenshotRows).Single();
Check(screenshotBox.Valid && screenshotBox.Y == screenshotChoice.Bounds.Y && screenshotBox.H == 230
    && screenshotRows.All(r => !r.Overlaps(screenshotBox)),
    "The screenshot has enough free adjacent space even for a conservatively tall healing hint, without scrolling");

foreach (bool differentScope in new[] { false, true })
{
    // Real structured healing changes, with one amount/scope made different. Such
    // differences must remain visible, not disappear in the compact paired sentence.
    var original = catalog.Links.Single(l => l.PerkId == screenshotChoice.Id);
    var separated = original.Changes.Select(c => new BonusChange { Base = c.Base, Added = c.Added, Comparable = c.Comparable,
        Part = new BonusPart { Key = c.Part.Key, Attribute = c.Part.Attribute, Subject = c.Part.Subject,
            Suffix = c.Part.Suffix, Percent = c.Part.Percent, Value = c.Part.Value } }).ToList();
    var spellChange = separated.Single(c => c.Part.Subject == "героя");
    if (differentScope) spellChange.Part.Suffix += " на своей территории"; else spellChange.Added = .1m;
    var link = new Synergy { PerkId = "healing-scope", RequiredSkills = ["skill_summoner"], Changes = separated };
    var hint = new SynergyHint { Link = link };
    string text = BonusText.Preview([hint], [hint], [link], screenshotHero, true, compact: true);
    Check(text.Contains("способностями союзников") && text.Contains("заклинаниями героя")
        && (differentScope ? text.Contains("на своей территории") : text.Contains("с +30% до +40%")),
        "Different healing amounts or territories remain separate and explicit");
}

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

// No hover/selection state: each actionable offer produces its own adjacent annotation.
var tacticsHero = new HeroBuild(); tacticsHero.AddSkill("arena_skill_assault", 2); tacticsHero.AddSkill("arena_skill_protection", 2);
var oneForAll = new ChoiceHint { Id = "arena_sub_skill_tactics_1", IsPerk = true, Bounds = new Box(40, 250, 830, 160),
    Hints = SynergyRules.ForOffer(catalog.Links, tacticsHero, "arena_sub_skill_tactics_1", true) };
var allForOne = new ChoiceHint { Id = "arena_sub_skill_tactics_4", IsPerk = true, Bounds = new Box(40, 430, 830, 160),
    Hints = SynergyRules.ForOffer(catalog.Links, tacticsHero, "arena_sub_skill_tactics_4", true) };
var noCombination = new ChoiceHint { Id = "unrelated", IsPerk = true, Bounds = new Box(40, 610, 830, 160), Hints = new() };
var annotations = HintPresentation.ForChoices([oneForAll, noCombination, allForOne], catalog, tacticsHero);
Check(annotations.Count == 2 && annotations.Any(p => p.ChoiceId == oneForAll.Id) && annotations.Any(p => p.ChoiceId == allForOne.Id), "All relevant offers are immediately visible without a hovered choice");
Check(HintPresentation.Build(noCombination, catalog, tacticsHero) == null && HintPresentation.ForChoices([noCombination], catalog, tacticsHero).Count == 0,
    "No combination means no panel, not an empty-state message");
Check(annotations[0].Rows.Single().Name == "Продвинутое нападение" && annotations[0].Rows.Single().Effect.Contains("с +2 до +4"),
    "One for All shows its supporting skill and numbers without repeating its own title");
Check(annotations[0].Anchor.Y == oneForAll.Bounds.Y && annotations[1].Anchor.Y == allForOne.Bounds.Y,
    "Each annotation retains its specific card's position");
var nextHeroPanels = HintPresentation.ForChoices([new ChoiceHint { Id = oneForAll.Id, IsPerk = true, Bounds = oneForAll.Bounds,
    Hints = SynergyRules.ForOffer(catalog.Links, new HeroBuild(), oneForAll.Id, true) }], catalog, new HeroBuild());
Check(nextHeroPanels.Count == 0, "A new hero cannot inherit an old annotation");
Check(HintPresentation.ForChoices([allForOne], catalog, tacticsHero).All(p => p.ChoiceId == allForOne.Id), "Changing offers removes obsolete annotations");
foreach (var size in new[] { (1447f,800f), (1920f,1080f), (2560f,1440f), (1280f,720f) })
{
    float scale = Math.Clamp(size.Item2 / 1080, .75f, 2f);
    var cards = new[] {
        new Box(size.Item1 * .04f, size.Item2 * .30f, size.Item1 * .56f, size.Item2 * .19f),
        new Box(size.Item1 * .04f, size.Item2 * .52f, size.Item1 * .56f, size.Item2 * .19f),
        new Box(size.Item1 * .04f, size.Item2 * .74f, size.Item1 * .56f, size.Item2 * .19f) };
    var boxes = PanelLayout.PlaceCards(size.Item1, size.Item2, scale, cards, cards, (_, _) => 130 * scale);
    Check(boxes.All(b => b.Valid), "All three matching cards get simultaneous annotations at every supported scale");
    for (int i = 0; i < boxes.Length; i++)
    {
        Check(boxes[i].Y == cards[i].Y && boxes[i].X >= cards[i].Right && boxes[i].X - cards[i].Right <= 13 * scale,
            "Annotation aligns with its own card, including second and third rows");
        Check(boxes[i].Right <= size.Item1 && boxes[i].Bottom <= size.Item2 && cards.All(c => !boxes[i].Overlaps(c)),
            "Annotations stay on screen and avoid native cards");
        Check(boxes.Where((_, j) => j != i).All(b => !b.Overlaps(boxes[i])), "Simultaneous annotations never overlap each other");
        var mark = PanelLayout.CardMark(cards[i], scale);
        Check(mark.Valid && mark.X >= cards[i].X && mark.Y >= cards[i].Y && mark.Right <= cards[i].Right && mark.Bottom <= cards[i].Bottom,
            "Automatic marker stays inside the corresponding native card");
    }
    var longFirst = PanelLayout.PlaceCards(size.Item1, size.Item2, scale, cards, cards, (i, _) => (i == 0 ? 500 : 130) * scale);
    Check(longFirst.All(b => b.Valid) && longFirst[0].Bottom < longFirst[1].Y && longFirst[2].Y == cards[2].Y,
        "Long first annotation cannot push later cards out of their rows");
    var reversed = cards.Reverse().ToArray();
    var reverseLayout = PanelLayout.PlaceCards(size.Item1, size.Item2, scale, cards, reversed, (_, _) => 130 * scale);
    Check(reverseLayout.Select(b => b.Y).SequenceEqual(boxes.Reverse().Select(b => b.Y)), "Native enumeration order cannot swap annotation placement");
}
var centeredCard = new Box(420, 250, 600, 160);
var rightOccupied = new[] { centeredCard, new Box(1030, 0, 250, 720) };
var leftPanel = PanelLayout.PlaceCards(1280, 720, .75f, rightOccupied, [centeredCard], (_, _) => 110);
Check(leftPanel[0].Valid && leftPanel[0].Right < centeredCard.X && leftPanel[0].Y == centeredCard.Y,
    "When the right side is blocked, an annotation stays at card height on the left");
Check(!PanelLayout.PlaceCards(1280, 720, .75f, [new Box(0,0,1280,720)], [centeredCard], (_, _) => 110)[0].Valid,
    "A fully occupied screen cannot force a panel over native controls");
Check(PanelLayout.PlaceCards(1280,720,.75f,[],[],(_,_)=>100).Length == 0, "Empty annotations produce no screen rectangles");
var horizontalCards = new[] { new Box(440,300,460,220), new Box(940,300,460,220) };
var horizontalPanels = PanelLayout.PlaceCards(1920,1080,1,horizontalCards,horizontalCards,(_,_)=>130);
Check(horizontalPanels.All(b => b.Valid && b.Y == 300) && !horizontalPanels[0].Overlaps(horizontalPanels[1]),
    "Side-by-side native choices can use opposite sides while staying at card height");
Check(!PanelLayout.PlaceCards(1280,720,1,[],[new Box(100,690,600,30)],(_,_)=>150)[0].Valid,
    "A clipped bottom card cannot create an unreadably short annotation");
var insetCard = new Box(100,300,700,180);
var extendedButton = new Box(100,100,730,600);
var beyondFrame = PanelLayout.PlaceCards(1280,800,1,[insetCard,extendedButton],[insetCard],(_,_)=>130)[0];
Check(beyondFrame.Valid && beyondFrame.Y == insetCard.Y && beyondFrame.X == extendedButton.Right + 12,
    "Native hitbox extending beyond a card shifts the annotation slightly right instead of hiding it");
var unmatchedBelow = new Box(100,500,700,180);
var expandedAnnotation = PanelLayout.PlaceCards(1280,800,1,[insetCard,unmatchedBelow],[insetCard],(_,_)=>350,[insetCard,unmatchedBelow])[0];
Check(expandedAnnotation.Valid && expandedAnnotation.Y == insetCard.Y && expandedAnnotation.H == 350
    && !expandedAnnotation.Overlaps(unmatchedBelow),
    "A single annotation uses empty adjacent space without moving its top away from the activating choice");
var matchedBelow = PanelLayout.PlaceCards(1280,800,1,[insetCard,unmatchedBelow],[insetCard,unmatchedBelow],(_,_)=>350);
Check(matchedBelow.All(b => b.Valid) && matchedBelow[0].Bottom <= matchedBelow[1].Y - 12,
    "A later matching choice still reserves its own space when its neighbour grows");
var controlBelow = new Box(810, 550, 460, 90);
var stoppedByControl = PanelLayout.PlaceCards(1280,800,1,[insetCard,controlBelow],[insetCard],(_,_)=>350)[0];
Check(stoppedByControl.Valid && stoppedByControl.Y == insetCard.Y && stoppedByControl.Bottom <= controlBelow.Y - 4,
    "Expansion stops before a native control, retaining a usable fallback viewport");
Check(!PanelLayout.PlaceCards(1280,800,1,[insetCard,new Box(800,280,480,80)],[insetCard],(_,_)=>100)[0].Valid,
    "Blocked card-height space cannot push the explanation lower beside a different native choice");

// Large text is contained in each card's own scrolling viewport. It is never
// squeezed to fit, even when all three short perk cards have lengthy bonuses.
foreach (var size in new[] { (1634f,1205f), (1920f,1080f), (2560f,1440f), (1280f,720f) })
{
    float scale = Math.Clamp(size.Item2 / 1080, .75f, 2f);
    var cards = new[] { .54f, .69f, .84f }.Select(y => new Box(size.Item1 * .085f, size.Item2 * y, size.Item1 * .51f, size.Item2 * .13f)).ToArray();
    var heights = new[] { 360 * scale, 640 * scale, 290 * scale };
    var boxes = PanelLayout.PlaceCards(size.Item1, size.Item2, scale, cards, cards, (i, _) => heights[i]);
    Check(boxes.All(b => b.Valid) && boxes.Select(b => b.Y).SequenceEqual(cards.Select(c => c.Y)),
        "Three long annotations stay visible at their own short perk cards, including screenshot aspect ratio");
    Check(boxes.Select((box, i) => boxes.Where((_, j) => j != i).All(other => !box.Overlaps(other))
        && cards.All(card => !box.Overlaps(card)) && box.Bottom <= size.Item2 - 8 * scale).All(ok => ok),
        "Large-text panels cannot overlap each other, native choices or the screen edge");
    var scrolls = boxes.Select((box, i) => { var state = new HintScroll(); state.SetRange(heights[i] - 20 * scale, box.H - 20 * scale); return state; }).ToArray();
    Check(scrolls.All(s => s.Overflow && s.ViewHeight >= 64 * scale), "Long explanations get usable individual scroll areas instead of smaller fonts");
    scrolls[1].Wheel(-100, scale);
    Check(scrolls[0].Offset == 0 && scrolls[2].Offset == 0 && Math.Abs(scrolls[1].ContentHeight - scrolls[1].Offset - scrolls[1].ViewHeight) < .25f,
        "Scrolling the middle annotation reaches its final line without moving its neighbors");
}
var scroll = new HintScroll();
var scrollTrack = new Box(600, 200, 3, 150);
scroll.SetRange(140, 150); scroll.Wheel(-100, 1);
Check(!scroll.Overflow && scroll.Offset == 0 && !scroll.Thumb(scrollTrack, 1).Valid, "Short text has no scrollbar or hidden scroll position");
scroll.SetRange(280, 150);
var topThumb = scroll.Thumb(scrollTrack, 1);
Check(topThumb.Valid && topThumb.Y == scrollTrack.Y && topThumb.Bottom < scrollTrack.Bottom, "Scrollbar indicates overflow even for a single long explanation");
scroll.Wheel(-1, 1); float preservedOffset = scroll.Offset;
scroll.SetRange(280, 150);
Check(scroll.Offset == preservedOffset && scroll.Offset > 0, "Routine refresh retains the current scroll position");
scroll.Wheel(-100, 1);
Check(scroll.Offset == 130 && scroll.Thumb(scrollTrack, 1).Bottom == scrollTrack.Bottom, "Scrolling to the end reveals all text and stops at the lower bound");
scroll.Wheel(100, 1);
Check(scroll.Offset == 0, "Scrolling up stops at the title, never above the content");
scroll.Wheel(-100, 1); scroll.SetRange(280, 220);
Check(scroll.Offset == 60, "Resizing clamps the previous offset to the new viewport");
scroll.SetRange(140, 150);
Check(scroll.Offset == 0 && !scroll.Overflow, "A now-fitting annotation clears its old scroll offset");
scroll.SetRange(140.25f, 140.2499f);
Check(!scroll.Overflow, "Fractional measurement differences do not create a spurious scrollbar");
Check(new HintScroll().Offset == 0, "A new choice starts at the beginning of its explanation");

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
