using Hex.Session.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OldenEra.SkillSynergies;

public sealed class SynergyOverlay : MonoBehaviour
{
    public SynergyOverlay(IntPtr pointer) : base(pointer) { }
    private NativeUi _ui;
    private IntPtr _owner;
    private float _nextRefresh, _nextError, _nextLayoutLog;
    private int _page = 1;
    private Box _panelBox;
    private string _hoverId = "", _lastContext = "";
    private readonly Il2CppSystem.Collections.Generic.List<BhLevelUpSkillView> _skills = new();
    private readonly Il2CppSystem.Collections.Generic.List<BhLevelUpSubSkillView> _perks = new();
    private readonly Il2CppSystem.Collections.Generic.List<Button> _buttons = new();
    private readonly List<Offer> _offers = new();
    private readonly List<Box> _obstacles = new();
    private sealed class Offer
    {
        internal string Id;
        internal bool IsPerk;
        internal Box Bounds;
        internal List<SynergyHint> Hints;
    }

    public void Update()
    {
        try
        {
            if (Input.GetKeyDown(KeyCode.F9)) Plugin.Instance.Enabled.Value = !Plugin.Instance.Enabled.Value;
            var window = BhLevelUpView.cild;
            if (!Plugin.Instance.Enabled.Value || !window || !window.gameObject.activeInHierarchy || !window.cszv
                || !window.canvas || !window.canvas.isActiveAndEnabled || (window.cg && window.cg.alpha <= 0.01f))
            { _ui?.Show(false); _hoverId = ""; _nextRefresh = 0; return; }
            var mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            if (_panelBox.Contains(mouse.x, mouse.y) && Input.mouseScrollDelta.y != 0)
            {
                _page += Input.mouseScrollDelta.y > 0 ? -1 : 1;
                _nextRefresh = 0; // Do not miss one-frame scroll events between model refreshes.
            }
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 0.12f;
            Refresh(window);
        }
        catch (Exception error)
        {
            _ui?.Show(false);
            if (Time.unscaledTime >= _nextError)
            {
                Plugin.Instance.Log.LogWarning($"Synergy UI paused: {error.GetType().Name}: {error.Message}");
                _nextError = Time.unscaledTime + 15;
            }
        }
    }

    private void Refresh(BhLevelUpView window)
    {
        var data = window.cilf?.chna;
        if (data?.skills?.list == null) { _ui?.Show(false); return; }
        var sample = window.currentLevel;
        if (!sample || !sample.font) { _ui?.Show(false); return; }
        if (_ui == null || !_ui.Root || _owner != window.Pointer)
        {
            _ui?.Destroy();
            _ui = new NativeUi(window.transform.Cast<RectTransform>(), sample);
            _owner = window.Pointer; _hoverId = "";
        }
        _ui.Show(true);
        var hero = new HeroBuild();
        foreach (var skill in data.skills.list)
        {
            if (skill == null || skill.level <= 0) continue;
            hero.AddSkill(skill.sid, skill.level);
            if (skill.subSkills != null)
                foreach (var perk in skill.subSkills)
                    if (perk != null) hero.AddPerk(perk.sid, (int)perk.status);
        }
        var catalog = Plugin.Instance.Catalog;
        _offers.Clear(); _obstacles.Clear();
        _skills.Clear(); _perks.Clear(); _buttons.Clear();
        window.GetComponentsInChildren<BhLevelUpSkillView>(false, _skills);
        window.GetComponentsInChildren<BhLevelUpSubSkillView>(false, _perks);
        window.GetComponentsInChildren<Button>(false, _buttons);
        foreach (var view in _skills)
        {
            if (!view || !view.isActiveAndEnabled || (view.cg && view.cg.alpha <= 0.01f)) continue;
            Box bounds = ReadObstacle(view.transform);
            if (!view.isSelectable || view.cikp?.chrk == null || !bounds.Valid) continue;
            AddOffer(view.cikp.chrk.sid, false, bounds, hero);
        }
        foreach (var view in _perks)
        {
            if (!view || !view.isActiveAndEnabled || (view.cg && view.cg.alpha <= 0.01f)) continue;
            Box bounds = ReadObstacle(view.transform);
            if (view.cikv?.chrv == null || !bounds.Valid || (view.empty && view.empty.activeInHierarchy)) continue;
            AddOffer(view.cikv.chrv.sid, true, bounds, hero);
        }
        foreach (var button in _buttons) if (button && button.isActiveAndEnabled) ReadObstacle(button.transform);
        if (window.heroPortraitView) ReadObstacle(window.heroPortraitView.transform);
        if (window.heroDataView) ReadObstacle(window.heroDataView.transform);
        ReadObstacle(window.currentLevel.transform);
        if (_offers.Count == 0) { _ui.Show(false); _hoverId = ""; return; }
        string context = data.id + ":" + string.Join(",", _offers.Select(o => o.Id));
        if (context != _lastContext)
        {
            _hoverId = ""; _page = 1; _lastContext = context;
            Plugin.Instance.Log.LogInfo($"Level-up hints: hero {data.id}, learned perks={hero.Perks.Count}, offers={_offers.Count}, related offers={_offers.Count(o => o.Hints.Count > 0)}.");
        }
        float scale = Math.Clamp(Screen.height / 1080f, 0.75f, 2f);
        var mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
        Offer hovered = _offers.LastOrDefault(offer => offer.Bounds.Contains(mouse.x, mouse.y));
        // Keep details readable while moving from a card into the details panel.
        if (hovered != null) _hoverId = hovered.Id;
        else if (!_panelBox.Contains(mouse.x, mouse.y)) _hoverId = "";
        Offer selected = _offers.FirstOrDefault(offer => offer.Id == _hoverId);
        _ui.Begin();
        foreach (var offer in _offers.Where(o => o.Hints.Count > 0))
        {
            var ids = new List<string>();
            foreach (var hint in offer.Hints)
                foreach (string relatedId in offer.IsPerk ? hint.Link.RequiredSkills : new[] { hint.Link.PerkId })
                    if (!ids.Contains(relatedId)) ids.Add(relatedId);
            int count = Math.Min(3, ids.Count);
            float height = 24 * scale, width = count * 27 * scale + 40 * scale;
            var strip = new Box(Math.Clamp(offer.Bounds.X + 8 * scale, 0, Screen.width - width), Math.Max(4 * scale, offer.Bounds.Y - 29 * scale), width, height);
            _obstacles.Add(strip);
            for (int i = 0; i < count; i++)
            {
                bool ready = offer.IsPerk ? hero.Skills.Contains(ids[i]) : offer.Hints.Any(h => h.Link.PerkId == ids[i] && h.Ready);
                _ui.Chip(new Box(strip.X + i * 27 * scale, strip.Y, height, height), catalog.Info(ids[i]).Icon,
                    ready ? Color.white : new Color(0.72f, 0.66f, 0.5f));
            }
            _ui.Count(new Box(strip.X + count * 27 * scale, strip.Y, 40 * scale, height), "×" + offer.Hints.Count, scale, new Color(0.96f, 0.82f, 0.5f));
            if (strip.Contains(mouse.x, mouse.y)) { _hoverId = offer.Id; selected = offer; }
        }
        _ui.End();
        _panelBox = PanelLayout.Place(Screen.width, Screen.height, scale, _obstacles);
        if (!_panelBox.Valid && Time.unscaledTime >= _nextLayoutLog)
        {
            Plugin.Instance.Log.LogWarning("No unobstructed space for synergy details; related-skill badges remain visible.");
            _nextLayoutLog = Time.unscaledTime + 30;
        }
        string heading = selected == null ? "ПОЛЕЗНО ИЗУЧИТЬ" : "СОЧЕТАНИЯ";
        string body = selected == null ? Overview(hero) : Details(selected);
        _ui.Panel(_panelBox, scale, heading, body, ref _page);
    }

    private void AddOffer(string id, bool perk, Box bounds, HeroBuild hero)
    {
        if (string.IsNullOrEmpty(id) || _offers.Any(o => o.Id == id)) return;
        _offers.Add(new Offer { Id = id, IsPerk = perk, Bounds = bounds, Hints = SynergyRules.ForOffer(Plugin.Instance.Catalog.Links, hero, id, perk) });
    }
    private Box ReadObstacle(Transform transform)
    {
        var rect = transform?.TryCast<RectTransform>();
        if (!rect) return default;
        var bounds = _ui.Bounds(rect);
        // Ignore full-screen canvas/animation holders, not actual card rectangles.
        if (bounds.Valid && !(bounds.W > Screen.width * 0.95f && bounds.H > Screen.height * 0.85f)) _obstacles.Add(bounds);
        return bounds;
    }
    private static string Safe(string value) => (value ?? "").Replace("<", "‹").Replace(">", "›");
    private static string Name(string id) => Safe(Plugin.Instance.Catalog.Info(id).Name);
    private string Details(Offer offer)
    {
        var lines = new List<string> { "<b>" + Name(offer.Id) + "</b>" };
        if (offer.Hints.Count == 0)
        {
            lines.Add("Прямых сочетаний с текущими перками не найдено.");
            lines.Add("Это не оценка общей полезности умения.");
        }
        foreach (var hint in offer.Hints)
        {
            string status = hint.AlreadyActive ? "Уже действует" : hint.Ready ? (offer.IsPerk ? "Сочетание готово" : "Усилит ваш перк") : "Для усиления нужно";
            string color = hint.Ready ? "A8D596" : "D9BC74";
            string related = offer.IsPerk ? string.Join(", ", (hint.Ready ? hint.Link.RequiredSkills : hint.Missing).Select(Name)) : Name(hint.Link.PerkId);
            lines.Add($"<color=#{color}><b>{status}</b></color>\n{related}\nДополнительно: {Safe(hint.Link.Effect)}"
                + (!offer.IsPerk && !hint.Ready ? "\nЕщё нужно: " + string.Join(", ", hint.Missing.Select(Name)) : "")
                + (hint.Temporary ? "\n<i>Связанный перк действует временно.</i>" : ""));
        }
        return string.Join("\n\n", lines);
    }
    private string Overview(HeroBuild hero)
    {
        var useful = SynergyRules.UsefulSkills(Plugin.Instance.Catalog.Links, hero);
        if (useful.Count == 0) return "<b>Новых прямых сочетаний пока нет.</b>\n\nНаведи на предлагаемый перк: здесь появятся подходящие и недостающие навыки.\n\nИконки над вариантами показывают связанные умения.";
        var lines = new List<string> { "<color=#BDB9A9>Навыки для усиления уже имеющихся перков. Доступность изучения определяет игра.</color>" };
        foreach (var pair in useful.OrderByDescending(p => p.Value.Select(v => v.PerkId).Distinct().Count()).ThenBy(p => Name(p.Key)))
        {
            string linked = string.Join(", ", pair.Value.Select(link => Name(link.PerkId) + (hero.TemporaryPerks.Contains(link.PerkId) ? " (временно)" : "")).Distinct());
            lines.Add("<color=#D9BC74><b>" + Name(pair.Key) + "</b></color>\n" + linked
                + (pair.Value.Count == 1 ? "\n" + Safe(pair.Value[0].Effect) : ""));
        }
        return string.Join("\n\n", lines);
    }
    public void OnDestroy() { _ui?.Destroy(); }
}
