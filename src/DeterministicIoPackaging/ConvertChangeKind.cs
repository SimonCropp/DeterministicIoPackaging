namespace DeterministicIoPackaging;

/// <summary>
/// What <see cref="DeterministicPackage"/> did to a part of a package.
/// </summary>
public enum ConvertChangeKind
{
    /// <summary>
    /// The entry was dropped from the output: a NuGet signature, or a core-properties part whose
    /// name is regenerated on every pack.
    /// </summary>
    Removed,

    /// <summary>
    /// The entry's content was rewritten to a deterministic form — relationship ids renumbered,
    /// volatile core properties stripped, a prefixed spreadsheet namespace unprefixed, or a nested
    /// package recursively converted.
    /// </summary>
    Patched,

    /// <summary>
    /// The package as a whole: entries were written in a different order than the source listed
    /// them. Carries no <see cref="ConvertChange.Entry"/>.
    /// </summary>
    Reordered
}
