namespace OldenEra.SkillSynergies;

internal readonly struct Box
{
    internal readonly float X, Y, W, H;
    internal float Right => X + W;
    internal float Bottom => Y + H;
    internal bool Valid => W > 0 && H > 0 && float.IsFinite(X + Y + W + H);
    internal Box(float x, float y, float w, float h) { X = x; Y = y; W = w; H = h; }
    internal bool Contains(float x, float y) => x >= X && x <= Right && y >= Y && y <= Bottom;
    internal bool Overlaps(Box other) => Valid && other.Valid && X < other.Right && Right > other.X && Y < other.Bottom && Bottom > other.Y;
    internal Box Expand(float value) => new(X - value, Y - value, W + 2 * value, H + 2 * value);
}

internal static class PanelLayout
{
    internal static Box Place(float width, float height, float scale, IReadOnlyList<Box> obstacles, Box anchor = default,
        Func<float, float> measureHeight = null)
    {
        // Prefer a sidebar; use a shorter panel or footer when the native window is wide.
        var sizes = new[] { (340f, 380f), (300f, 360f), (280f, 340f), (620f, 250f), (460f, 250f) };
        return PlaceSizes(width, height, scale, obstacles, sizes, anchor, measureHeight);
    }

    internal static Box Status(float width, float height, float scale, IReadOnlyList<Box> obstacles)
        => PlaceSizes(width, height, scale, obstacles, new[] { (310f, 30f) });

    internal static Box CardMark(Box card, float scale)
    {
        if (!card.Valid || card.H < 24 * scale || card.W < 20 * scale) return default;
        float height = Math.Min(44 * scale, card.H - 16 * scale);
        return new Box(card.X + 4 * scale, card.Y + (card.H - height) / 2, 3 * scale, height);
    }

    internal static Box Group(IEnumerable<Box> boxes)
    {
        var valid = boxes.Where(b => b.Valid).ToArray();
        if (valid.Length == 0) return default;
        float x = valid.Min(b => b.X), y = valid.Min(b => b.Y);
        return new Box(x, y, valid.Max(b => b.Right) - x, valid.Max(b => b.Bottom) - y);
    }

    private static Box PlaceSizes(float width, float height, float scale, IReadOnlyList<Box> obstacles, (float, float)[] sizes,
        Box anchor = default, Func<float, float> measureHeight = null)
    {
        float margin = 12 * scale;
        foreach (var (rawW, rawH) in sizes)
        {
            float w = Math.Min(rawW * scale, width - 2 * margin), h = rawH * scale;
            if (measureHeight != null) h = Math.Min(h, measureHeight(w));
            if (w <= 0 || h <= 0 || h > height - 2 * margin) continue;
            var xs = new List<float> { width - w - margin, margin };
            var ys = new List<float> { (height - h) / 2, height - h - margin, margin };
            float idealX = anchor.Right + margin, idealY = Math.Clamp(anchor.Y, margin, height - h - margin);
            if (anchor.Valid) { xs.Add(idealX); xs.Add(anchor.X - w - margin); ys.Add(idealY); }
            foreach (var ob in obstacles) { xs.Add(ob.Right + margin); xs.Add(ob.X - w - margin); ys.Add(ob.Bottom + margin); ys.Add(ob.Y - h - margin); }
            Box best = default; float bestScore = float.PositiveInfinity;
            foreach (float x in xs)
                foreach (float y in ys)
                {
                    var candidate = new Box(x, y, w, h);
                    if (x < margin || y < margin || candidate.Right > width - margin || candidate.Bottom > height - margin) continue;
                    if (obstacles.Any(o => o.Expand(4 * scale).Overlaps(candidate))) continue;
                    if (!anchor.Valid) return candidate;
                    float score = Math.Abs(x - idealX) + Math.Abs(y - idealY);
                    if (score < bestScore) { best = candidate; bestScore = score; }
                }
            if (best.Valid) return best;
        }
        return default; // Never cover a choice or button when no safe space exists.
    }
}
