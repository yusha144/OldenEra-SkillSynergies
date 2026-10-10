namespace OldenEra.SkillSynergies;

// Each annotation keeps its own offset. Overflow is clipped by the native UI,
// never handled by shrinking text or moving the annotation into another row.
internal sealed class HintScroll
{
    internal float Offset { get; private set; }
    internal float Limit { get; private set; }
    internal float ContentHeight { get; private set; }
    internal float ViewHeight { get; private set; }
    internal bool Overflow => Limit > 0;

    internal void SetRange(float contentHeight, float viewHeight)
    {
        ContentHeight = Math.Max(0, contentHeight);
        ViewHeight = Math.Max(0, viewHeight);
        Limit = ContentHeight > ViewHeight + .25f ? ContentHeight - ViewHeight : 0;
        Offset = Math.Clamp(Offset, 0, Limit);
    }

    internal void Wheel(float delta, float scale)
        => Offset = Math.Clamp(Offset - delta * 52 * scale, 0, Limit);

    internal Box Thumb(Box track, float scale)
    {
        if (!Overflow || !track.Valid) return default;
        float height = Math.Min(track.H, Math.Max(22 * scale, track.H * ViewHeight / ContentHeight));
        return new Box(track.X, track.Y + (track.H - height) * Offset / Limit, track.W, height);
    }
}
