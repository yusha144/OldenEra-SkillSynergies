using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

internal static class InteropBoundaryChecks
{
    // Inspect the actual compiled plugin without loading Unity or native game code.
    // A local function in a hidden method can still generate an unhidden method on
    // the injected MonoBehaviour, with an unsupported managed closure parameter.
    internal static void Run(string assemblyPath, Action<bool, string> check)
    {
        using var stream = File.OpenRead(assemblyPath);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        var overlay = reader.TypeDefinitions.Select(reader.GetTypeDefinition).Single(t =>
            reader.GetString(t.Namespace) == "OldenEra.SkillSynergies" && reader.GetString(t.Name) == "SynergyOverlay");
        var exposed = new List<string>();
        foreach (var handle in overlay.GetMethods())
        {
            var method = reader.GetMethodDefinition(handle);
            string name = reader.GetString(method.Name);
            if (name is ".ctor" or ".cctor") continue;
            bool hidden = method.GetCustomAttributes().Any(a => IsHidden(reader, a));
            if (!hidden) exposed.Add(name);
        }
        check(exposed.OrderBy(n => n).SequenceEqual(new[] { "OnDestroy", "Update" }),
            "Only Unity lifecycle methods may cross the IL2CPP boundary. Exposed in compiled DLL: " + string.Join(", ", exposed));
    }

    private static bool IsHidden(MetadataReader reader, CustomAttributeHandle handle)
    {
        var attribute = reader.GetCustomAttribute(handle);
        if (attribute.Constructor.Kind != HandleKind.MemberReference) return false;
        var parent = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Parent;
        if (parent.Kind != HandleKind.TypeReference) return false;
        var type = reader.GetTypeReference((TypeReferenceHandle)parent);
        return reader.GetString(type.Namespace) == "Il2CppInterop.Runtime.Attributes"
            && reader.GetString(type.Name) == "HideFromIl2CppAttribute";
    }
}
