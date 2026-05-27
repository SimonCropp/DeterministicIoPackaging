[TestFixture]
public class PatcherSetTests
{
    [Test]
    public void FindResolvesExactMatchPatcherFromDictionary()
    {
        var exact = new FakeExactPatcher("word/document.xml");
        var predicate = new FakePredicatePatcher(_ => false);
        var set = new PatcherSet([exact, predicate]);

        var entry = ZipEntryFor("word/document.xml");

        Assert.That(set.Find(entry), Is.SameAs(exact));
    }

    [Test]
    public void FindFallsBackToPredicatePatchersWhenNoExactMatch()
    {
        var exact = new FakeExactPatcher("word/document.xml");
        var predicate = new FakePredicatePatcher(entry => entry.FullName.EndsWith(".rels"));
        var set = new PatcherSet([exact, predicate]);

        var entry = ZipEntryFor("word/_rels/footer1.xml.rels");

        Assert.That(set.Find(entry), Is.SameAs(predicate));
    }

    [Test]
    public void FindReturnsNullWhenNoPatcherMatches()
    {
        var set = new PatcherSet([new FakeExactPatcher("word/document.xml")]);
        var entry = ZipEntryFor("word/styles.xml");

        Assert.That(set.Find(entry), Is.Null);
    }

    [Test]
    public void ExactMatchesAreStoredOrdinal()
    {
        var set = new PatcherSet([new FakeExactPatcher("Word/Document.xml")]);

        // Ordinal — different casing must not match.
        Assert.That(set.Find(ZipEntryFor("word/document.xml")), Is.Null);
        Assert.That(set.Find(ZipEntryFor("Word/Document.xml")), Is.Not.Null);
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
