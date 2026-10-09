using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OldenEra.SkillSynergies;

internal sealed class NativeUi
{
    internal readonly RectTransform Root;
    internal readonly Image Frame;
    internal readonly TextMeshProUGUI Header, Body, Footer;
    private readonly RectTransform _panel;
    private readonly TMP_FontAsset _font;
    private readonly Material _fontMaterial;
    private readonly List<Image> _chips = new();
    private readonly Dictionary<string, Sprite> _sprites = new();
    private readonly List<TextMeshProUGUI> _counts = new();
    private int _chipCount, _countCount;
    private readonly Il2CppStructArray<Vector3> _corners = new(4);

    internal NativeUi(RectTransform parent, TextMeshProUGUI sample)
    {
        _font = sample.font; _fontMaterial = sample.fontSharedMaterial;
        Root = CreateRect("SkillSynergies.LocalHints", parent);
        Root.anchorMin = Vector2.zero; Root.anchorMax = Vector2.one; Root.offsetMin = Vector2.zero; Root.offsetMax = Vector2.zero;
        var group = Root.gameObject.AddComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false;
        _panel = CreateRect("Combinations", Root);
        Frame = _panel.gameObject.AddComponent<Image>(); Frame.color = new Color(0.58f, 0.47f, 0.26f, 0.98f); Frame.raycastTarget = false;
        var inside = CreateRect("Background", _panel);
        inside.anchorMin = Vector2.zero; inside.anchorMax = Vector2.one; inside.offsetMin = new Vector2(2, 2); inside.offsetMax = new Vector2(-2, -2);
        var background = inside.gameObject.AddComponent<Image>(); background.color = new Color(0.055f, 0.063f, 0.10f, 0.98f); background.raycastTarget = false;
        Header = CreateText("Heading", _panel); Header.fontStyle = FontStyles.Bold; Header.color = new Color(0.93f, 0.79f, 0.46f);
        Body = CreateText("Details", _panel); Body.overflowMode = TextOverflowModes.Page;
        Footer = CreateText("Navigation", _panel); Footer.color = new Color(0.66f, 0.68f, 0.73f);
    }

    internal void Begin() { _chipCount = 0; _countCount = 0; }
    internal void End()
    {
        for (int i = _chipCount; i < _chips.Count; i++) _chips[i].transform.parent.gameObject.SetActive(false);
        for (int i = _countCount; i < _counts.Count; i++) _counts[i].gameObject.SetActive(false);
    }
    internal void Show(bool show) { if (Root) Root.gameObject.SetActive(show); }
    internal void Destroy() { if (Root) UnityEngine.Object.Destroy(Root.gameObject); }

    internal void Panel(Box box, float scale, string heading, string body, ref int page)
    {
        _panel.gameObject.SetActive(box.Valid);
        if (!box.Valid) return;
        Position(_panel, box);
        Position(Header.rectTransform, new Box(box.X + 14 * scale, box.Y + 10 * scale, box.W - 28 * scale, 25 * scale));
        Position(Body.rectTransform, new Box(box.X + 14 * scale, box.Y + 43 * scale, box.W - 28 * scale, box.H - 75 * scale));
        Position(Footer.rectTransform, new Box(box.X + 14 * scale, box.Bottom - 26 * scale, box.W - 28 * scale, 18 * scale));
        float units = UnitsPerPixel(Root);
        Header.fontSize = 18 * scale * units; Body.fontSize = 15 * scale * units; Footer.fontSize = 11 * scale * units;
        Header.text = heading;
        if (Body.text != body) { Body.text = body; page = 1; }
        Body.ForceMeshUpdate();
        int pages = Math.Max(1, Body.textInfo.pageCount);
        page = Math.Clamp(page, 1, pages); Body.pageToDisplay = page;
        Footer.text = pages > 1 ? $"{page}/{pages} · колесо над панелью · F9 скрыть" : "Наведи на вариант · F9 скрыть";
    }

    internal void Chip(Box box, string icon, Color tint)
    {
        Image image;
        if (_chipCount == _chips.Count)
        {
            var rect = CreateRect("RelatedSkill", Root);
            var back = rect.gameObject.AddComponent<Image>(); back.color = new Color(0.055f, 0.063f, 0.10f, 0.98f); back.raycastTarget = false;
            var inner = CreateRect("Icon", rect); inner.anchorMin = Vector2.zero; inner.anchorMax = Vector2.one;
            inner.offsetMin = new Vector2(2, 2); inner.offsetMax = new Vector2(-2, -2);
            image = inner.gameObject.AddComponent<Image>(); image.preserveAspect = true; image.raycastTarget = false;
            _chips.Add(image);
        }
        image = _chips[_chipCount++];
        image.transform.parent.gameObject.SetActive(true); image.gameObject.SetActive(true);
        Position(image.transform.parent.Cast<RectTransform>(), box);
        image.sprite = Sprite(icon); image.color = tint;
        // A missing native texture never turns into a solid white square.
        image.enabled = image.sprite;
    }

    internal void Count(Box box, string text, float scale, Color color)
    {
        if (_countCount == _counts.Count) _counts.Add(CreateText("RelationCount", Root));
        var label = _counts[_countCount++]; label.gameObject.SetActive(true); Position(label.rectTransform, box);
        label.text = text; label.color = color; label.fontStyle = FontStyles.Bold; label.fontSize = 13 * scale * UnitsPerPixel(Root);
        label.alignment = TextAlignmentOptions.Center;
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
