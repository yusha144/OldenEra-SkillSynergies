# Skill Synergies — isolated mod

- This is a separate plugin from Map Hints. Keep GUID `local.oldenera.skillsynergies`, DLL `OldenEra.SkillSynergies.dll`, config and installation directory separate.
- Do not modify or reinstall Map Hints while working here.
- Read existing game data and already-generated level-up choices only. Never simulate picks by calling game mutations, generate alternatives, consume randomness, apply bonuses or write saves.
- Use native UI children of the level-up window; all mod graphics have raycastTarget=false and the root CanvasGroup must not block raycasts.
- Match raw skill IDs exactly across standard, arena and campaign data. Use only active HeroSubSkill statuses (1 temporary, 2 permanent); offered/inactive entries do not count as acquired perks.
- Base synergy detection on referenced HeroLearnedSkill conditions, not loose text matching or obsolete translations. Skip complete condition groups when any condition is unsupported.
- Show only combinations activated by currently offered choices and the hero's already acquired abilities. Do not suggest future missing skills or repeat bonus descriptions. Owned perks and upgrades that do not newly satisfy presence conditions are not new improvements.
- Keep the inspected choice while the mouse crosses to the details panel. Reset on a different choice, hero, or choice stage; paginate complete rows. Keep markers inside their own card and related ability icons in the details panel.
- Test pure logic against the installed Core.zip, build against the verified game's interop assemblies, back up any previous installed Synergies DLL/config before updates.
- User owns in-game testing. Do not start, close, or control their game.
