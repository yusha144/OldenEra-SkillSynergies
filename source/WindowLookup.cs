namespace OldenEra.SkillSynergies;

internal static class WindowLookup
{
    // A cached or static reference may be absent, destroyed, or belong to a closed window.
    // The game's registry is authoritative in both standard and arena level-up flows.
    internal static T Find<T>(T cached, IEnumerable<T> registered, Func<T, bool> visible) where T : class
    {
        if (cached != null && visible(cached)) return cached;
        foreach (T window in registered)
            if (window != null && visible(window)) return window;
        return null;
    }
}
