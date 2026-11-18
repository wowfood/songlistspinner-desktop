using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace SonglistSpinner.EndToEndTests.Infrastructure;

/// <summary>
/// Tells whether a built Desktop app honours the test-profile variables, from the assembly metadata its build
/// stamps when <c>EnableTestProfile</c> is true. An app without the stamp ignores
/// <c>SONGLISTSPINNER_PROFILE_DIR</c> and would start on the developer's own settings and saved credential, so
/// the tests never start one.
/// </summary>
internal static class TestProfileStamp
{
    private const string Key = "SonglistSpinner.TestProfile";
    private const string Enabled = "enabled";

    // A custom attribute's value starts with this prolog; its fixed string arguments follow in order.
    private const ushort AttributeValueProlog = 1;

    public static bool IsOn(string assemblyPath)
    {
        using var file = File.OpenRead(assemblyPath);
        using var portableExecutable = new PEReader(file);
        if (!portableExecutable.HasMetadata) return false;

        var metadata = portableExecutable.GetMetadataReader();
        foreach (var handle in metadata.GetAssemblyDefinition().GetCustomAttributes())
        {
            var attribute = metadata.GetCustomAttribute(handle);
            if (!IsAssemblyMetadata(metadata, attribute)) continue;

            var value = metadata.GetBlobReader(attribute.Value);
            if (value.ReadUInt16() == AttributeValueProlog
                && value.ReadSerializedString() == Key
                && value.ReadSerializedString() == Enabled)
                return true;
        }

        return false;
    }

    private static bool IsAssemblyMetadata(MetadataReader metadata, CustomAttribute attribute)
    {
        if (attribute.Constructor.Kind != HandleKind.MemberReference) return false;

        var constructor = metadata.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
        if (constructor.Parent.Kind != HandleKind.TypeReference) return false;

        var type = metadata.GetTypeReference((TypeReferenceHandle)constructor.Parent);
        return metadata.GetString(type.Namespace) == "System.Reflection"
               && metadata.GetString(type.Name) == "AssemblyMetadataAttribute";
    }
}
