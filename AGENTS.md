# Skill Synergies — isolated mod

- This is a separate plugin from Map Hints. Keep GUID `local.oldenera.skillsynergies`, DLL `OldenEra.SkillSynergies.dll`, config and installation directory separate.
- Do not modify or reinstall Map Hints while working here.
- Read existing game data and already-generated level-up choices only. Never simulate picks by calling game mutations, generate alternatives, consume randomness, apply bonuses or write saves.
- Use native UI children of the level-up window; all mod graphics have raycastTarget=false and the root CanvasGroup must not block raycasts.
- Match raw skill IDs exactly across standard, arena and campaign data. Use only active HeroSubSkill statuses (1 temporary, 2 permanent); offered/inactive entries do not count as acquired perks.
- Base synergy detection on referenced HeroLearnedSkill conditions, not loose text matching or obsolete translations. Skip complete condition groups when any condition is unsupported.
- Show only combinations activated by currently offered choices and the hero's already acquired abilities. Do not suggest future missing skills. Owned perks and upgrades that do not newly satisfy presence conditions are not new improvements.
- Show verified numeric effects in BOTH directions: an offered skill improving an owned perk and an offered perk strengthened by existing skills. For an owned perk show its parent skill at the hero's CURRENT level. Do not substitute the offered next level. Distinguish the activating choice from the parent skill.
- Resolve amounts from actual bonus parameters and referenced passive buffs, preserving target, territory, trigger, rank and coefficient restrictions. Never infer a doubling from localized prose. Include already active synergies in before/after values, and combine multiple current supports without double-counting. Treat absolute stat overrides separately from additive bonuses. Reject unrecognized effect shapes with a diagnostic rather than displaying a vague description placeholder.
- Use the words `с ... до ...` for numeric comparisons: the native font lacks the arrow glyph. Do not display a hotkey footer. F9 still toggles the UI, but no status panel remains when hidden.
- Anchor details beside the choice group, avoid all native controls, and size the panel to its measured text. Use the native card font, slate-blue background and a single thin brass frame; no inset frame. Show the page hint only when there is more than one page.
- Keep the inspected choice while the mouse crosses to the details panel. Reset on a different choice, hero, or choice stage; paginate complete rows. Keep markers inside their own card and related ability icons in the details panel.
- Test pure logic against the installed Core.zip, build against the verified game's interop assemblies, back up any previous installed Synergies DLL/config before updates.
- User owns in-game testing. Do not start, close, or control their game.
