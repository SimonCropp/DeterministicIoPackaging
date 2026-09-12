namespace DeterministicIoPackaging;

/// <summary>
/// The output of <see cref="DeterministicPackage.ConvertWithChangesAsync"/>: the converted package,
/// and what changed to produce it.
/// </summary>
/// <param name="Stream">
/// The converted package, a fresh <see cref="MemoryStream"/> positioned at 0. Disposing this result
/// disposes it.
/// </param>
/// <param name="Changes">
/// What actually differed. Empty for a package that was already deterministic.
/// </param>
public record ConvertResult(MemoryStream Stream, IReadOnlyList<ConvertChange> Changes) :
    IDisposable
{
    public void Dispose() => Stream.Dispose();
}
