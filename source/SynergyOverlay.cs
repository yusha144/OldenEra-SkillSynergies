using Hex.Session.UI;
using Il2CppInterop.Runtime.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OldenEra.SkillSynergies;

public sealed class SynergyOverlay : MonoBehaviour
{
    public SynergyOverlay(IntPtr pointer) : base(pointer) { }
    private NativeUi _ui;
    private BhLevelUpView _window;
    private IntPtr _owner;
    private float _nextRefresh, _nextError, _nextLayoutLog, _nextDiagnostic;
    private bool _started, _visible;
    private string _lastContext = "", _lastDiagnostic = "";
    private sealed class ChoiceView
    {
        internal NativeUi Ui;
        internal Box Bounds;
        internal int Page = 1;
    }
    private readonly Dictionary<string, ChoiceView> _panels = new();
    private readonly Il2CppSystem.Collections.Generic.List<BhLevelUpSkillView> _skills = new();
    private readonly Il2CppSystem.Collections.Generic.List<BhLevelUpSubSkillView> _perks = new();
    private readonly Il2CppSystem.Collections.Generic.List<Button> _buttons = new();
    private readonly List<ChoiceHint> _offers = new();
    private readonly HashSet<IntPtr> _choicePointers = new();
    private readonly List<Box> _obstacles = new();

    public void Update()
    {
        try
        {
            if (!_started)
            {
                _started = true; _visible = Plugin.Instance.Enabled.Value;
                Plugin.Instance.Log.LogInfo("Skill hint updater running; watching the registered level-up windows (standard and arena).");
            }
            if (_window && !IsVisible(_window))
            {
                _ui?.Show(false); ClearPanels(); _window = null; _lastContext = "";
                _nextRefresh = 0;
            }
            // Do not silently persist an accidental F9 press made elsewhere in the game.
            if (_window && Input.GetKeyDown(KeyCode.F9))
            {
                _visible = !_visible; _nextRefresh = 0;
                Plugin.Instance.Log.LogInfo($"Skill hints {(_visible ? "shown" : "hidden")} for this session (F9).");
            }
            var mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            if (_visible && Input.mouseScrollDelta.y != 0)
            {
                foreach (var panel in _panels.Values)
                    if (panel.Bounds.Valid && panel.Bounds.Contains(mouse.x, mouse.y))
                    {
                        panel.Page += Input.mouseScrollDelta.y > 0 ? -1 : 1;
                        _nextRefresh = 0;
                        break;
                    }
            }
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 0.12f;
            _window = WindowLookup.Find<BhLevelUpView>(_window, RegisteredWindows(), IsVisible);
            if (!_window) { _ui?.Show(false); return; }
            Refresh(_window);
        }
        catch (Exception error)
        {
            _ui?.Show(false);
            if (Time.unscaledTime >= _nextError)
            {
                Plugin.Instance.Log.LogWarning($"Synergy UI paused: {error}");
                _nextError = Time.unscaledTime + 15;
            }
        }
    }

    [HideFromIl2Cpp]
    private static bool IsVisible(BhLevelUpView window) => window && window.gameObject.activeInHierarchy && window.cszv
        && window.canvas && window.canvas.isActiveAndEnabled && (!window.cg || window.cg.alpha > 0.01f);

    [HideFromIl2Cpp]
    private static bool IsElementVisible(Behaviour view)
    {
        if (!view || !view.isActiveAndEnabled) return false;
        for (var parent = view.transform; parent; parent = parent.parent)
        {
            var group = parent.GetComponent<CanvasGroup>();
            if (!group) continue;
            if (group.alpha <= 0.01f) return false;
            if (group.ignoreParentGroups) break;
        }
        return true;
    }

    [HideFromIl2Cpp]
    private static IEnumerable<BhLevelUpView> RegisteredWindows()
    {
        // BhLevelUpView.cild is never assigned by the current game binary. Both game modes
        // open a registered BhLevelUpView via cpo instead. Reading the registry does not create UI.
        var manager = cpo.ccog;
        if (manager == null) yield break;
        if (manager.ccoh != null)
            foreach (var pair in manager.ccoh)
            {
                var window = pair.Value?.TryCast<BhLevelUpView>();
                if (window) yield return window;
            }
        if (manager.ccoi != null)
            foreach (var screen in manager.ccoi)
            {
                var window = screen?.TryCast<BhLevelUpView>();
                if (window) yield return window;
            }
    }

    [HideFromIl2Cpp]
    private void Diagnostic(string message)
    {
        if (_lastDiagnostic == message && Time.unscaledTime < _nextDiagnostic) return;
        _lastDiagnostic = message; _nextDiagnostic = Time.unscaledTime + 30;
        Plugin.Instance.Log.LogInfo(message);
    }

    [HideFromIl2Cpp]
    private void Refresh(BhLevelUpView window)
    {
        var data = (window.cile?.chpd ?? window.cilf)?.chna;
        if (data?.skills?.list == null)
        { _ui?.Show(false); Diagnostic("Registered level-up window is open; waiting for hero skill data."); return; }
        var sample = window.currentLevel;
        // Use the card's regular native font, rather than the bold level-ribbon style.
        if (window.cile?.chpe?.TryCast<dzq>() != null && window.statsAndSkillsView?.skillViews != null)
            foreach (var view in window.statsAndSkillsView.skillViews)
                if (IsElementVisible(view) && view.title && view.title.font) { sample = view.title; break; }
        if (window.cile?.chpe?.TryCast<dzr>() != null && window.subSkillsView?.subSkillViews != null)
            foreach (var view in window.subSkillsView.subSkillViews)
                if (IsElementVisible(view) && view.title && view.title.font) { sample = view.title; break; }
        if (!sample || !sample.font)
        { _ui?.Show(false); Diagnostic("Registered level-up window is open; native font is not ready."); return; }
        if (_ui == null || !_ui.Root || _owner != window.Pointer)
        {
            ClearPanels();
            _ui?.Destroy();
            _ui = new NativeUi(window.transform.Cast<RectTransform>(), sample);
            _owner = window.Pointer; _lastContext = "";
            Diagnostic($"Attached skill hints to registered level-up window; arena={window.cilk}, legacy singleton present={(bool)BhLevelUpView.cild}.");
        }
        _ui.Show(true);
        var hero = new HeroBuild();
        foreach (var skill in data.skills.list)
        {
            if (skill == null || skill.level <= 0) continue;
            hero.AddSkill(skill.sid, skill.level);
            if (skill.subSkills != null)
                foreach (var perk in skill.subSkills)
                    if (perk != null) hero.AddPerk(perk.sid, (int)perk.status, skill.sid);
        }
        _offers.Clear(); _obstacles.Clear();
        _skills.Clear(); _perks.Clear(); _buttons.Clear();
        window.GetComponentsInChildren<BhLevelUpSkillView>(false, _skills);
        window.GetComponentsInChildren<BhLevelUpSubSkillView>(false, _perks);
        window.GetComponentsInChildren<Button>(false, _buttons);
        // Only the current stage's choice list; nested preview icons and old animation stages
        // must not become additional offers or create duplicate annotations.
        _choicePointers.Clear();
        bool skillStage = window.cile?.chpe?.TryCast<dzq>() != null;
        bool perkStage = window.cile?.chpe?.TryCast<dzr>() != null;
        if (skillStage && window.statsAndSkillsView?.skillViews != null)
            foreach (var view in window.statsAndSkillsView.skillViews) if (view) _choicePointers.Add(view.Pointer);
        if (perkStage && window.subSkillsView?.subSkillViews != null)
            foreach (var view in window.subSkillsView.subSkillViews) if (view) _choicePointers.Add(view.Pointer);
        foreach (var view in _skills)
        {
            if (!IsElementVisible(view)) continue;
            Box bounds = ReadObstacle(view.transform);
            if (!_choicePointers.Contains(view.Pointer) || !view.isSelectable || view.cikp?.chrk == null || !bounds.Valid) continue;
            AddOffer(view.cikp.chrk.sid, false, bounds, hero, view.cikp.chrk.level);
        }
        foreach (var view in _perks)
        {
            if (!IsElementVisible(view)) continue;
            Box bounds = ReadObstacle(view.transform);
            if (!_choicePointers.Contains(view.Pointer) || view.cikv?.chrv == null || !bounds.Valid || (view.empty && view.empty.activeInHierarchy)) continue;
            AddOffer(view.cikv.chrv.sid, true, bounds, hero);
        }
        foreach (var button in _buttons) if (IsElementVisible(button)) ReadObstacle(button.transform);
        if (window.heroPortraitView) ReadObstacle(window.heroPortraitView.transform);
        if (window.heroDataView) ReadObstacle(window.heroDataView.transform);
        ReadObstacle(window.currentLevel.transform);
        if (_offers.Count == 0)
            Diagnostic($"Waiting for offered cards: skill views={_skills.Count}, perk views={_perks.Count}, arena={window.cilk}.");
        string context = data.id + ":" + (perkStage ? "perks:" : "skills:") + string.Join(",", _offers.Select(o => o.Id).OrderBy(id => id))
            + ":" + string.Join(",", hero.SkillLevels.OrderBy(pair => pair.Key).Select(pair => pair.Key + "=" + pair.Value)) + ":" + string.Join(",", hero.Perks.OrderBy(id => id))
            + ":" + string.Join(",", hero.TemporaryPerks.OrderBy(id => id));
        if (context != _lastContext)
        {
            ClearPanels();
            _lastContext = context;
            Plugin.Instance.Log.LogInfo($"Level-up hints: hero {data.id}, arena={window.cilk}, learned perks={hero.Perks.Count}, offers={_offers.Count}, related offers={_offers.Count(o => o.Hints.Count > 0)}, visible={_visible}.");
        }
        float scale = Math.Clamp(Screen.height / 1080f, 0.75f, 2f);
        foreach (var panel in _panels.Values) { panel.Ui.Show(false); panel.Bounds = default; }
        _ui.Begin();
        foreach (var offer in _offers.Where(o => _visible && o.Hints.Count > 0))
            _ui.Mark(offer.Bounds, scale);
        _ui.End();
        if (!_visible)
        {
            _ui.Show(false);
            return;
        }
        var contents = HintPresentation.ForChoices(_offers, Plugin.Instance.Catalog, hero);
        if (contents.Count == 0) { _ui.Show(false); return; }
        foreach (var content in contents)
        {
            if (!_panels.ContainsKey(content.ChoiceId))
                _panels[content.ChoiceId] = new ChoiceView { Ui = new NativeUi(_ui.Root, sample, "ChoiceSynergy." + content.ChoiceId) };
            _panels[content.ChoiceId].Ui.Show(true);
        }
        var boxes = PanelLayout.PlaceCards(Screen.width, Screen.height, scale, _obstacles, contents.Select(c => c.Anchor).ToArray(),
            (index, width) => _panels[contents[index].ChoiceId].Ui.MeasureHeight(width, scale, contents[index]), _offers.Select(o => o.Bounds).ToArray());
        for (int i = 0; i < contents.Count; i++)
        {
            var panel = _panels[contents[i].ChoiceId]; panel.Bounds = boxes[i];
            panel.Ui.Show(boxes[i].Valid);
            if (boxes[i].Valid) panel.Ui.Panel(boxes[i], scale, contents[i], ref panel.Page);
        }
        if (boxes.Any(box => !box.Valid) && Time.unscaledTime >= _nextLayoutLog)
        {
            Plugin.Instance.Log.LogWarning("No unobstructed adjacent space for some synergy cards; their native-card marks remain visible.");
            _nextLayoutLog = Time.unscaledTime + 30;
        }
    }

    [HideFromIl2Cpp]
    private void ClearPanels()
    {
        foreach (var panel in _panels.Values) { panel.Ui.Show(false); panel.Ui.Destroy(); }
        _panels.Clear();
    }

    [HideFromIl2Cpp]
    private void AddOffer(string id, bool perk, Box bounds, HeroBuild hero, int level = 0)
    {
        if (string.IsNullOrEmpty(id) || _offers.Any(o => o.Id == id)) return;
        _offers.Add(new ChoiceHint { Id = id, IsPerk = perk, Level = level, Bounds = bounds, Hints = SynergyRules.ForOffer(Plugin.Instance.Catalog.Links, hero, id, perk) });
    }
    [HideFromIl2Cpp]
    private Box ReadObstacle(Transform transform)
    {
        var rect = transform?.TryCast<RectTransform>();
        if (!rect) return default;
        var bounds = _ui.Bounds(rect);
        // Ignore full-screen canvas/animation holders, not actual card rectangles.
        if (bounds.Valid && !(bounds.W > Screen.width * 0.95f && bounds.H > Screen.height * 0.85f)) _obstacles.Add(bounds);
        return bounds;
    }
    public void OnDestroy() { ClearPanels(); _ui?.Destroy(); }
}
