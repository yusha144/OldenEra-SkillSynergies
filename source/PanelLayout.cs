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
    internal static Box[] PlaceCards(float width, float height, float scale, IReadOnlyList<Box> obstacles,
        IReadOnlyList<Box> anchors, Func<int, float, float> measureHeight, IReadOnlyList<Box> choiceRows = null)
    {
        var placed = new Box[anchors.Count];
        var occupied = new List<Box>();
        Box group = Group(choiceRows ?? anchors);
        float gap = 12 * scale, margin = 8 * scale;
        // Layout is independent of the pointer and input order. Each next eligible card
        // reserves its own vertical band, so earlier annotations cannot displace it.
        foreach (int index in Enumerable.Range(0, anchors.Count).OrderBy(i => anchors[i].Y).ThenBy(i => anchors[i].X))
        {
            Box anchor = anchors[index];
            if (!anchor.Valid) continue;
            float top = Math.Max(margin, anchor.Y);
            float bottom = height - margin;
            foreach (var other in choiceRows ?? anchors)
                if (other.Valid && other.Y > anchor.Y + scale) bottom = Math.Min(bottom, other.Y - gap);
            if (bottom - top < 96 * scale) continue;
            Box best = default; float bestScore = float.PositiveInfinity;
            var measurements = new Dictionary<float, float>();
            foreach (float rawWidth in new[] { 340f, 420f, 300f, 260f })
                foreach (bool right in new[] { true, false })
                {
                    // Native button hitboxes and window ornaments can extend past a card.
                    // Stay at its height, moving just beyond that obstruction when necessary.
                    var edges = new HashSet<float> { right ? anchor.Right : anchor.X, right ? group.Right : group.X };
                    foreach (var ob in obstacles)
                        if (ob.Valid && (right ? ob.Right >= anchor.Right : ob.X <= anchor.X)) edges.Add(right ? ob.Right : ob.X);
                    foreach (float edge in edges)
                    {
                        float free = right ? width - margin - edge - gap : edge - gap - margin;
                        float w = Math.Min(rawWidth * scale, free);
                        if (w < 260 * scale) continue;
                        float x = right ? edge + gap : edge - gap - w;
                        if (!measurements.TryGetValue(w, out float desired)) measurements[w] = desired = measureHeight(index, w);
                        if (!float.IsFinite(desired) || desired <= 0) continue;
                        float h = Math.Min(desired, Math.Min(380 * scale, bottom - top));
                        var ys = new List<float> { top };
                        foreach (var ob in obstacles.Concat(occupied))
                            if (ob.Bottom + gap > top) ys.Add(ob.Bottom + gap);
                        foreach (float y in ys)
                        {
                            var candidate = new Box(x, y, w, h);
                            if (candidate.X < margin || candidate.Right > width - margin || y < margin || candidate.Bottom > bottom + .25f) continue;
                            if (obstacles.Any(o => o.Expand(4 * scale).Overlaps(candidate)) || occupied.Any(o => o.Expand(6 * scale).Overlaps(candidate))) continue;
                            float distance = right ? Math.Abs(x - anchor.Right) : Math.Abs(candidate.Right - anchor.X);
                            float score = 4 * Math.Abs(y - top) + distance + 10 * (desired - h)
                                + .1f * Math.Abs(w - 340 * scale) + (right ? 0 : scale);
                            if (score < bestScore) { best = candidate; bestScore = score; }
                        }
                    }
                }
            placed[index] = best;
            if (best.Valid) occupied.Add(best);
        }
        return placed;
    }

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
}
