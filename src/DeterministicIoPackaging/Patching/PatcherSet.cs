// Splits a patcher list into a FullName-keyed dictionary for IExactMatchPatcher
// entries and a small fallback list of predicate-based patchers. The dispatch
// in DeterministicPackage.DuplicateEntry can then resolve most entries with
// a single dictionary lookup, instead of calling IsMatch on every patcher
// for every entry (O(entries × patchers)).
class PatcherSet
{
    public Dictionary<string, IPatcher> ExactMatches { get; }
    public IReadOnlyList<IPatcher> PredicateMatches { get; }

    public PatcherSet(IReadOnlyList<IPatcher> patchers)
    {
        ExactMatches = new(StringComparer.Ordinal);
        var predicates = new List<IPatcher>();
        foreach (var patcher in patchers)
        {
            if (patcher is IExactMatchPatcher exact)
            {
                ExactMatches.Add(exact.ExactMatch, patcher);
            }
            else
            {
                predicates.Add(patcher);
            }
        }

        PredicateMatches = predicates;
    }

    public IPatcher? Find(Entry entry)
    {
        if (ExactMatches.TryGetValue(entry.FullName, out var exact))
        {
            return exact;
        }

        foreach (var patcher in PredicateMatches)
        {
            if (patcher.IsMatch(entry))
            {
                return patcher;
            }
        }

        return null;
    }
}
