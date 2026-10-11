using SharpTS.References;

namespace SharpTS.LanguageServer.Project;

/// <summary>Parses C# project references for language-server metadata resolution.</summary>
public static class CsprojParser
{
    /// <summary>Returns existing assembly paths without evaluating, restoring, or building the project.</summary>
    public static List<string> Parse(string csprojPath) => CsprojReferenceResolver.Parse(csprojPath);
}
