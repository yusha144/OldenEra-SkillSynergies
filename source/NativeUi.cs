using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Globalization;

namespace OldenEra.SkillSynergies;

internal sealed class NativeUi
{
    internal readonly RectTransform Root;
    internal readonly Image Frame;
    private readonly Image _scrollTrack, _scrollThumb;
    private readonly RectTransform _panel;
    private readonly RectTransform _rowViewport;
    private readonly TMP_FontAsset _font;
    private readonly Material _fontMaterial;
    private readonly List<Image> _marks = new();
    private readonly List<RowView> _rows = new();
    private readonly Dictionary<string, Sprite> _sprites = new();
    private int _markCount;
    private readonly Il2CppStructArray<Vector3> _corners = new(4);
    private sealed class RowView
    {
        internal RectTransform Root;
        internal Image Icon, Rule;
        internal TextMeshProUGUI Name, Note, Effect;
        internal float Height, NameHeight, NoteHeight, EffectHeight, EffectOffset;
    }

    internal NativeUi(RectTransform parent, TextMeshProUGUI sample, string name = "SkillSynergies.LocalHints")
    {
        _font = sample.font; _fontMaterial = sample.fontSharedMaterial;
        Root = CreateRect(name, parent);
        Root.anchorMin = Vector2.zero; Root.anchorMax = Vector2.one; Root.offsetMin = Vector2.zero; Root.offsetMax = Vector2.zero;
        var group = Root.gameObject.AddComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false;
        _panel = CreateRect("Combinations", Root);
        Frame = _panel.gameObject.AddComponent<Image>(); Frame.color = new Color(0.045f, 0.07f, 0.11f, 1); Frame.raycastTarget = false;
        var outline = Inset("BrassOutline", _panel, 1, new Color(0.54f, 0.48f, 0.33f, 1));
        Inset("BlueSlate", outline, 1, new Color(0.14f, 0.18f, 0.25f, 1));
        _rowViewport = CreateRect("RelationsViewport", _panel);
        _rowViewport.gameObject.AddComponent<RectMask2D>();
        _scrollTrack = CreateRect("ScrollTrack", _panel).gameObject.AddComponent<Image>();
        _scrollTrack.color = new Color(0.54f, 0.48f, 0.33f, 0.25f); _scrollTrack.raycastTarget = false;
        _scrollThumb = CreateRect("ScrollThumb", _panel).gameObject.AddComponent<Image>();
        _scrollThumb.color = new Color(0.82f, 0.67f, 0.37f, 0.9f); _scrollThumb.raycastTarget = false;
        _panel.gameObject.SetActive(false);
    }

    internal void Begin() { _markCount = 0; }
    internal void End()
    {
        for (int i = _markCount; i < _marks.Count; i++) _marks[i].gameObject.SetActive(false);
    }
    internal void Show(bool show) { if (Root) Root.gameObject.SetActive(show); }
    internal void Destroy() { if (Root) UnityEngine.Object.Destroy(Root.gameObject); }

    internal float MeasureHeight(float width, float scale, HintPanel content)
    {
        // Initialize native TMP components before measuring new rows; final visibility is
        // assigned by the layout pass before Unity renders the frame.
        _panel.gameObject.SetActive(true);
        Prepare(content, width - 32 * scale, scale);
        float height = 20 * scale;
        for (int i = 0; i < content.Rows.Count; i++) height += _rows[i].Height;
        return height;
    }

    private void Prepare(HintPanel content, float width, float scale)
    {
        float units = UnitsPerPixel(Root);
        for (int i = 0; i < content.Rows.Count; i++)
        {
            if (i == _rows.Count) _rows.Add(CreateRow());
            var row = _rows[i]; var data = content.Rows[i];
            SetText(row.Name, data.Name, 21 * scale, units);
            SetText(row.Note, data.Note, 16 * scale, units);
            SetText(row.Effect, data.Effect, 19 * scale, units);
            row.NameHeight = TextHeight(row.Name, width - 50 * scale, units);
            row.NoteHeight = string.IsNullOrEmpty(data.Note) ? 0 : TextHeight(row.Note, width - 50 * scale, units);
            row.EffectOffset = Math.Max(40 * scale, row.NameHeight + (row.NoteHeight > 0 ? row.NoteHeight + 4 * scale : 0)) + 8 * scale;
            row.EffectHeight = string.IsNullOrEmpty(data.Effect) ? 0 : TextHeight(row.Effect, width, units);
            row.Height = row.EffectOffset + row.EffectHeight + 12 * scale;
            row.Icon.sprite = Sprite(data.Icon); row.Icon.enabled = row.Icon.sprite;
        }
    }

    internal void Panel(Box box, float scale, HintPanel content, HintScroll scroll)
    {
        _panel.gameObject.SetActive(box.Valid);
        if (!box.Valid) return;
        Position(_panel, box);
        // Reserve a narrow gutter so showing a scrollbar never reflows the text.
        float x = box.X + 12 * scale, width = box.W - 32 * scale;
        Prepare(content, width, scale);
        float top = box.Y + 8 * scale;
        float available = Math.Max(32 * scale, box.Bottom - 12 * scale - top);
        Position(_rowViewport, new Box(x, top, width, available));
        float totalHeight = 0;
        for (int i = 0; i < content.Rows.Count; i++) totalHeight += _rows[i].Height;
        scroll.SetRange(totalHeight, available);
        for (int i = content.Rows.Count; i < _rows.Count; i++) _rows[i].Root.gameObject.SetActive(false);
        float y = top - scroll.Offset;
        for (int i = 0; i < content.Rows.Count; i++)
        {
            var row = _rows[i]; row.Root.gameObject.SetActive(true);
            Position(row.Root, new Box(x, y, width, row.Height));
            Position(row.Icon.rectTransform, new Box(x, y, 40 * scale, 40 * scale));
            Position(row.Name.rectTransform, new Box(x + 50 * scale, y, width - 50 * scale, row.NameHeight));
            row.Note.gameObject.SetActive(row.NoteHeight > 0);
            Position(row.Note.rectTransform, new Box(x + 50 * scale, y + row.NameHeight + 4 * scale, width - 50 * scale, row.NoteHeight));
            row.Effect.gameObject.SetActive(row.EffectHeight > 0);
            Position(row.Effect.rectTransform, new Box(x, y + row.EffectOffset, width, row.EffectHeight));
            Position(row.Rule.rectTransform, new Box(x, y + row.Height - 7 * scale, width, scale));
            row.Rule.gameObject.SetActive(i < content.Rows.Count - 1);
            y += row.Height;
        }
        _scrollTrack.gameObject.SetActive(scroll.Overflow);
        _scrollThumb.gameObject.SetActive(scroll.Overflow);
        if (scroll.Overflow)
        {
            var track = new Box(box.Right - 9 * scale, top, 3 * scale, available);
            Position(_scrollTrack.rectTransform, track);
            Position(_scrollThumb.rectTransform, scroll.Thumb(track, scale));
        }
    }

    internal void Mark(Box card, float scale)
    {
        if (_markCount == _marks.Count)
        {
            var rect = CreateRect("AvailableCombination", Root);
            var image = rect.gameObject.AddComponent<Image>(); image.raycastTarget = false;
            _marks.Add(image);
        }
        var mark = _marks[_markCount++];
        var box = PanelLayout.CardMark(card, scale);
        mark.gameObject.SetActive(box.Valid); if (!box.Valid) return;
        Position(mark.rectTransform, box);
        mark.color = new Color(0.82f, 0.67f, 0.37f, 0.95f);
    }

    private RowView CreateRow()
    {
        var root = CreateRect("OwnedAbility", _rowViewport);
        var icon = CreateRect("Icon", root).gameObject.AddComponent<Image>(); icon.preserveAspect = true; icon.raycastTarget = false;
        var name = CreateText("Name", root); name.color = new Color(0.91f, 0.80f, 0.56f);
        var note = CreateText("Relation", root); note.color = new Color(0.75f, 0.77f, 0.77f);
        var effect = CreateText("Effect", root);
        var rule = CreateRect("Rule", root).gameObject.AddComponent<Image>(); rule.raycastTarget = false; rule.color = new Color(0.58f, 0.47f, 0.26f, 0.3f);
        return new RowView { Root = root, Icon = icon, Name = name, Note = note, Effect = effect, Rule = rule };
    }

    private static RectTransform Inset(string name, RectTransform parent, float inset, Color color)
    {
        var rect = CreateRect(name, parent);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset); rect.offsetMax = new Vector2(-inset, -inset);
        var image = rect.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false;
        return rect;
    }

    private static void SetText(TextMeshProUGUI label, string value, float pixels, float units)
    {
        label.fontSize = pixels * units;
        // The native font's default line metrics are very tall; use an explicit compact line height.
        string lineHeight = (pixels * units * 1.25f).ToString("0.##", CultureInfo.InvariantCulture);
        label.text = "<line-height=" + lineHeight + ">" + (value ?? "").Replace("<", "‹").Replace(">", "›");
    }
    private static float TextHeight(TextMeshProUGUI label, float width, float units)
        => Math.Max(label.fontSize * 1.3f, label.GetPreferredValues(label.text, width * units, float.PositiveInfinity).y) / units;

    private Sprite Sprite(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (_sprites.TryGetValue(id, out var cached) && cached) return cached;
        var atlas = cnh.cbwr?.cbws;
        if (atlas != null && atlas.TryGetValue(id, out var sprite) && sprite) { _sprites[id] = sprite; return sprite; }
        return null;
    }
    private TextMeshProUGUI CreateText(string name, RectTransform parent)
    {
        var rect = CreateRect(name, parent); var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = _font; text.fontSharedMaterial = _fontMaterial; text.color = new Color(0.9f, 0.89f, 0.83f);
        text.richText = true; text.enableWordWrapping = true; text.alignment = TextAlignmentOptions.TopLeft;
        text.enableAutoSizing = false; text.lineSpacing = 0; text.paragraphSpacing = 0;
        text.raycastTarget = false; text.margin = Vector4.zero; return text;
    }
    private static RectTransform CreateRect(string name, RectTransform parent)
    {
        var obj = new GameObject(name, new Il2CppReferenceArray<Il2CppSystem.Type>(new[] { Il2CppType.Of<RectTransform>() }));
        obj.layer = parent.gameObject.layer;
        var rect = obj.transform.Cast<RectTransform>(); rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f); rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one; return rect;
    }
    private static Camera CameraFor(RectTransform rect)
    {
        var canvas = rect.GetComponentInParent<Canvas>();
        if (!canvas || canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay) return null;
        return canvas.worldCamera ? canvas.worldCamera : canvas.rootCanvas.worldCamera;
    }
    private static float UnitsPerPixel(RectTransform parent)
    {
        var camera = CameraFor(parent);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, Vector2.zero, camera, out var a);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, new Vector2(0, 100), camera, out var b);
        return Math.Abs(b.y - a.y) / 100;
    }
    private static void Position(RectTransform rect, Box box)
    {
        var parent = rect.parent.Cast<RectTransform>(); var camera = CameraFor(parent);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, new Vector2(box.X, Screen.height - box.Y), camera, out var a);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, new Vector2(box.Right, Screen.height - box.Bottom), camera, out var b);
        rect.anchoredPosition = (a + b) / 2 - parent.rect.center;
        rect.sizeDelta = new Vector2(Math.Abs(b.x - a.x), Math.Abs(b.y - a.y));
    }
    internal Box Bounds(RectTransform rect)
    {
        if (!rect || !rect.gameObject.activeInHierarchy) return default;
        rect.GetWorldCorners(_corners); var camera = CameraFor(rect);
        float x = float.PositiveInfinity, y = float.PositiveInfinity, right = float.NegativeInfinity, bottom = float.NegativeInfinity;
        foreach (var corner in _corners)
        {
            var p = RectTransformUtility.WorldToScreenPoint(camera, corner);
            x = Math.Min(x, p.x); y = Math.Min(y, Screen.height - p.y);
            right = Math.Max(right, p.x); bottom = Math.Max(bottom, Screen.height - p.y);
        }
        return new Box(x, y, right - x, bottom - y);
    }
}
