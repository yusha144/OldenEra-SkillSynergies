namespace OldenEra.SkillSynergies;

// Native TMP supplies measured heights, including wrapping. This layout is also
// exercised without Unity so sources cannot slip above an effect or its conditions.
internal sealed class HintRowLayout
{
    internal float EffectOffset, ConditionsOffset, Height;
    internal float[] SourceOffsets;

    internal static HintRowLayout Measure(float scale, float nameHeight, float effectHeight,
        float conditionsHeight, IReadOnlyList<float> sourceHeights, bool separator)
    {
        var result = new HintRowLayout { EffectOffset = nameHeight > 0 ? Math.Max(32 * scale, nameHeight) + 6 * scale : 0,
            SourceOffsets = new float[sourceHeights.Count] };
        float bottom = result.EffectOffset + effectHeight;
        result.ConditionsOffset = bottom + 6 * scale;
        if (conditionsHeight > 0) bottom = result.ConditionsOffset + conditionsHeight;
        for (int i = 0; i < sourceHeights.Count; i++)
        {
            result.SourceOffsets[i] = bottom + (i == 0 ? 6 : 4) * scale;
            bottom = result.SourceOffsets[i] + sourceHeights[i];
        }
        result.Height = bottom + (separator ? 12 * scale : 0);
        return result;
    }
}
