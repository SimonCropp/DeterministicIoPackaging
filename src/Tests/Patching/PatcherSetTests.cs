public class PatcherSetTests
{
    [Test]
    public async Task FindResolvesExactMatchPatcherFromDictionary()
    {
        var exact = new FakeExactPatcher("word/document.xml");
        var predicate = new FakePredicatePatcher(_ => false);
        var set = new PatcherSet([exact, predicate]);

        var entry = ZipEntryFor("word/document.xml");

        await Assert.That(set.Find(entry)).IsSameReferenceAs(exact);
    }

    [Test]
    public async Task FindFallsBackToPredicatePatchersWhenNoExactMatch()
    {
        var exact = new FakeExactPatcher("word/document.xml");
        var predicate = new FakePredicatePatcher(entry => entry.FullName.EndsWith(".rels"));
        var set = new PatcherSet([exact, predicate]);

        var entry = ZipEntryFor("word/_rels/footer1.xml.rels");

        await Assert.That(set.Find(entry)).IsSameReferenceAs(predicate);
    }

    [Test]
    public async Task FindReturnsNullWhenNoPatcherMatches()
    {
        var set = new PatcherSet([new FakeExactPatcher("word/document.xml")]);
        var entry = ZipEntryFor("word/styles.xml");

        await Assert.That(set.Find(entry)).IsNull();
    }

    [Test]
    public async Task ExactMatchesAreStoredOrdinal()
    {
        var set = new PatcherSet([new FakeExactPatcher("Word/Document.xml")]);

        // Ordinal — different casing must not match.
        await Assert.That(set.Find(ZipEntryFor("word/document.xml"))).IsNull();
        await Assert.That(set.Find(ZipEntryFor("Word/Document.xml"))).IsNotNull();
    }

    static Entry ZipEntryFor(string fullName)
    {
        // ZipArchiveEntry has no public constructor; create one via a throwaway archive.
        var stream = new MemoryStream();
        var archive = new Archive(stream, ZipArchiveMode.Create, leaveOpen: true);
        return archive.CreateEntry(fullName);
    }

    class FakeExactPatcher(string match) : IExactMatchPatcher
    {
        public string ExactMatch { get; } = match;
        public bool IsMatch(Entry entry) => entry.FullName == ExactMatch;
        public void PatchXml(XDocument xml, string entryName) { }
    }

    class FakePredicatePatcher(Func<Entry, bool> predicate) : IPatcher
    {
        public bool IsMatch(Entry entry) => predicate(entry);
        public void PatchXml(XDocument xml, string entryName) { }
    }
}
