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
    private readonly Dictionary<IntPtr, LabelMeasurements> _textMeasurements = new();
    private int _markCount;
    private readonly Il2CppStructArray<Vector3> _corners = new(4);
    private sealed class RowView
    {
        internal RectTransform Root;
        internal Image Icon, Rule;
        internal TextMeshProUGUI Name, Conditions, Effect;
        internal float NameHeight, ConditionsHeight, EffectHeight;
        internal HintRowLayout Layout;
        internal readonly List<SourceView> Sources = new();
        internal float Height => Layout.Height;
    }
    private sealed class SourceView
    {
        internal RectTransform Root;
        internal TextMeshProUGUI Text;
        internal readonly List<Image> Icons = new();
        internal float TextHeight, Height, Inset;
    }
    private sealed class LabelMeasurements
    {
        internal string Text;
        internal float FontSize;
        internal readonly Dictionary<float, TextMeasurement> Widths = new();
        internal TextMeasurement Current;
    }
    private sealed class TextMeasurement
    {
        internal Vector2 RectSize;
        internal Bounds Ink;
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
        float height = 16 * scale;
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
            SetText(row.Conditions, data.Conditions, 17 * scale, units);
            // Presentation escapes all game text before adding numeric color markup.
            SetText(row.Effect, data.EffectRichText ?? data.Effect, 19 * scale, units, data.EffectRichText != null);
            row.NameHeight = string.IsNullOrEmpty(data.Name) ? 0 : TextHeight(row.Name, width - 42 * scale, units);
            row.ConditionsHeight = string.IsNullOrEmpty(data.Conditions) ? 0 : TextHeight(row.Conditions, width, units);
            row.EffectHeight = TextHeight(row.Effect, width, units);
            row.Icon.sprite = Sprite(data.Icon); row.Icon.enabled = row.Icon.sprite;
            var sourceHeights = new List<float>();
            for (int j = 0; j < data.Sources.Count; j++)
            {
                if (j == row.Sources.Count) row.Sources.Add(CreateSource(row.Root));
                var source = row.Sources[j]; var contentSource = data.Sources[j];
                SetText(source.Text, contentSource.RichText ?? contentSource.Text, 16 * scale, units, contentSource.RichText != null);
                for (int k = 0; k < contentSource.Icons.Length; k++)
                {
                    if (k == source.Icons.Count)
                    {
                        var icon = CreateRect("SkillIcon", source.Root).gameObject.AddComponent<Image>();
                        icon.preserveAspect = true; icon.raycastTarget = false; source.Icons.Add(icon);
                    }
                    source.Icons[k].sprite = Sprite(contentSource.Icons[k]);
                    source.Icons[k].enabled = source.Icons[k].sprite;
                }
                for (int k = 0; k < source.Icons.Count; k++) source.Icons[k].gameObject.SetActive(k < contentSource.Icons.Length);
                // A joint requirement keeps all native identities. If there are many,
                // stack their icons vertically instead of squeezing the source text.
                source.Inset = 36 * scale;
                source.TextHeight = TextHeight(source.Text, width - source.Inset, units);
                source.Height = Math.Max(source.TextHeight, contentSource.Icons.Length * 28 * scale);
                sourceHeights.Add(source.Height);
            }
            for (int j = data.Sources.Count; j < row.Sources.Count; j++) row.Sources[j].Root.gameObject.SetActive(false);
            row.Layout = HintRowLayout.Measure(scale, row.NameHeight, row.EffectHeight, row.ConditionsHeight,
                sourceHeights, i < content.Rows.Count - 1);
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
        float available = Math.Max(32 * scale, box.Bottom - 8 * scale - top);
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
            row.Name.gameObject.SetActive(row.NameHeight > 0);
            row.Icon.gameObject.SetActive(row.NameHeight > 0 && row.Icon.enabled);
            Position(row.Icon.rectTransform, new Box(x, y, 32 * scale, 32 * scale));
            if (row.NameHeight > 0) PositionText(row.Name, x + 42 * scale, y + Math.Max(0, (32 * scale - row.NameHeight) / 2));
            PositionText(row.Effect, x, y + row.Layout.EffectOffset);
            row.Conditions.gameObject.SetActive(row.ConditionsHeight > 0);
            if (row.ConditionsHeight > 0) PositionText(row.Conditions, x, y + row.Layout.ConditionsOffset);
            for (int j = 0; j < content.Rows[i].Sources.Count; j++)
            {
                var source = row.Sources[j]; source.Root.gameObject.SetActive(true);
                float sourceY = y + row.Layout.SourceOffsets[j];
                Position(source.Root, new Box(x, sourceY, width, source.Height));
                PositionText(source.Text, x + source.Inset, sourceY + (source.Height - source.TextHeight) / 2);
                for (int k = 0; k < content.Rows[i].Sources[j].Icons.Length; k++)
                    Position(source.Icons[k].rectTransform, new Box(x + 4 * scale, sourceY + k * 28 * scale, 28 * scale, 28 * scale));
            }
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
        var conditions = CreateText("Conditions", root); conditions.color = new Color(0.82f, 0.83f, 0.81f);
        var effect = CreateText("Effect", root);
        var rule = CreateRect("Rule", root).gameObject.AddComponent<Image>(); rule.raycastTarget = false; rule.color = new Color(0.58f, 0.47f, 0.26f, 0.3f);
        return new RowView { Root = root, Icon = icon, Name = name, Conditions = conditions, Effect = effect, Rule = rule };
    }

    private SourceView CreateSource(RectTransform parent)
    {
        var root = CreateRect("Source", parent);
        var text = CreateText("Text", root); text.color = new Color(0.75f, 0.77f, 0.77f);
        return new SourceView { Root = root, Text = text };
    }

    private static RectTransform Inset(string name, RectTransform parent, float inset, Color color)
    {
        var rect = CreateRect(name, parent);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset); rect.offsetMax = new Vector2(-inset, -inset);
        var image = rect.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false;
        return rect;
    }

    private static void SetText(TextMeshProUGUI label, string value, float pixels, float units, bool numericMarkup = false)
    {
        if (label.fontSize != pixels * units) label.fontSize = pixels * units;
        // The native font's default line metrics are very tall; use an explicit compact line height.
        string lineHeight = (pixels * units * 1.25f).ToString("0.##", CultureInfo.InvariantCulture);
        string text = value ?? "";
        string formatted = "<line-height=" + lineHeight + ">" + (numericMarkup ? text : text.Replace("<", "‹").Replace(">", "›"));
        if (label.text != formatted) label.text = formatted;
    }
    private float TextHeight(TextMeshProUGUI label, float width, float units)
    {
        if (!_textMeasurements.TryGetValue(label.Pointer, out var cache))
            _textMeasurements[label.Pointer] = cache = new LabelMeasurements();
        if (cache.Text != label.text || cache.FontSize != label.fontSize)
        { cache.Widths.Clear(); cache.Text = label.text; cache.FontSize = label.fontSize; }
        float localWidth = width * units;
        if (!cache.Widths.TryGetValue(localWidth, out var measurement))
        {
            // Native font ascenders reserve considerably more room than the visible
            // Cyrillic glyphs. Measure wrapped glyphs, preserving the native font size.
            var size = new Vector2(localWidth, Math.Max(label.fontSize * 2,
                label.GetPreferredValues(label.text, localWidth, float.PositiveInfinity).y));
            label.rectTransform.sizeDelta = size;
            label.ForceMeshUpdate(true, true);
            var ink = label.textBounds;
            bool validInk = float.IsFinite(ink.size.x + ink.size.y + ink.min.x + ink.max.y) && ink.size.y > 0;
            if (!validInk)
                ink = new Bounds(Vector3.zero, new Vector3(size.x, size.y, 0));
            measurement = new TextMeasurement { RectSize = size, Ink = ink };
            // Window animations can produce many transient widths. Bound the cache.
            if (cache.Widths.Count >= 24) cache.Widths.Clear();
            // A font still loading may not have a mesh yet. Retry next refresh.
            if (validInk) cache.Widths[localWidth] = measurement;
        }
        label.rectTransform.sizeDelta = measurement.RectSize;
        cache.Current = measurement;
        return measurement.Ink.size.y / units;
    }

    private void PositionText(TextMeshProUGUI label, float x, float y)
    {
        var measurement = _textMeasurements[label.Pointer].Current;
        var rect = label.rectTransform;
        var parent = rect.parent.Cast<RectTransform>();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, new Vector2(x, Screen.height - y), CameraFor(parent), out var point);
        // Keep the measuring rect unchanged; align its visible glyphs rather than
        // its large, invisible font margins. This also keeps the last line in view.
        rect.anchoredPosition = point - parent.rect.center - new Vector2(measurement.Ink.min.x, measurement.Ink.max.y);
    }

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
        text.overflowMode = TextOverflowModes.Overflow;
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
