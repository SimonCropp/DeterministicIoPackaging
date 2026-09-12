namespace DeterministicIoPackaging;

// Accumulates what the conversion actually altered.
//
// Only a real difference is recorded, never the fact that a pass ran. The conversion rewrites every
// entry whether or not anything about it was non-deterministic, so "we touched it" would be true of
// every part of every package and would say nothing. Detecting the difference costs something —
// cloning an XML part before patching it, buffering a PNG so the output can be compared — which is
// why a recorder only exists when a caller asked for one, and every other path passes null.
class ChangeRecorder
{
    List<ConvertChange> changes = [];

    public IReadOnlyList<ConvertChange> Changes => changes;

    public void Record(ConvertChangeKind kind, string? entry = null) =>
        changes.Add(new(kind, entry));

    // Entries are written sorted by FullName, so a package whose source listing was in any other
    // order changes even when no single entry does. Skipped entries are excluded: they are already
    // reported as removed, and their absence is not a reordering.
    public void RecordOrder(IEnumerable<string> sourceOrder, IEnumerable<string> targetOrder)
    {
        if (!sourceOrder.SequenceEqual(targetOrder, StringComparer.Ordinal))
        {
            Record(ConvertChangeKind.Reordered);
        }
    }
}
