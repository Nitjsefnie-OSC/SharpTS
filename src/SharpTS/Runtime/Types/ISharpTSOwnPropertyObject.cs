namespace SharpTS.Runtime.Types;

/// <summary>Descriptor-aware own storage for noncallable runtime objects.</summary>
internal interface ISharpTSOwnPropertyObject
{
    SharpTSObject OwnProperties { get; }
}
