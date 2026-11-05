using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

internal static class ScreenStateChecks
{
    internal static void Run(string assembly)
    {
        using var stream = File.OpenRead(assembly);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        var methods = new HashSet<string>();
        foreach (var handle in reader.MemberReferences)
        {
            var member = reader.GetMemberReference(handle);
            if (member.Parent.Kind != HandleKind.TypeReference) continue;
            var owner = reader.GetTypeReference((TypeReferenceHandle)member.Parent);
            if (reader.GetString(owner.Namespace) == "Hex.UI" && reader.GetString(owner.Name) == "BhScreen")
                methods.Add(reader.GetString(member.Name));
        }
        // udl reads the open flag. udi is a different virtual property that returns true.
        if (!methods.Contains("udl") || methods.Contains("udi"))
            throw new Exception("Compiled screen visibility must read BhScreen.udl, never the constant BhScreen.udi");
        Console.WriteLine("PASS: compiled window state uses the real open flag");
    }
}
