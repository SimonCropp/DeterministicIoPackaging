[TestFixture]
public class CoverageTests
{
    // Gap 1: SheetPatcher collision-normalization.
    // When two hyperlinks point to the same URL, the DeterministicId assignment
    // depends on the non-deterministic order the OpenXml SDK stored them. The
    // normalizer sorts by cell ref so the earliest cell gets the lowest ID.
    [Test]
    public void SheetPatcher_InterchangeableIds_AssignedInCellRefOrder()
    {
        const string sheetXml = """
            <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"
                       xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <sheetData>
                <row r="1"><c r="A1"><v>1</v></c></row>
                <row r="2"><c r="A2"><v>2</v></c></row>
              </sheetData>
              <hyperlinks>
                <hyperlink ref="B2" r:id="rIdSecond" />
                <hyperlink ref="B1" r:id="rIdFirst" />
              </hyperlinks>
            </worksheet>
            """;

        const string sheetRels = """
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rIdFirst" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink" Target="https://example.com/" TargetMode="External" />
              <Relationship Id="rIdSecond" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink" Target="https://example.com/" TargetMode="External" />
            </Relationships>
            """;

        var zip = BuildZip(new()
        {
            ["xl/worksheets/sheet1.xml"] = sheetXml,
            ["xl/worksheets/_rels/sheet1.xml.rels"] = sheetRels
        });

        var result = DeterministicPackage.Convert(zip);
        var sheet = ReadEntryXml(result, "xl/worksheets/sheet1.xml");

        XNamespace r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        var hyperlinks = sheet.Descendants()
            .Where(_ => _.Name.LocalName == "hyperlink")
            .OrderBy(_ => _.Attribute("ref")!.Value, StringComparer.Ordinal)
            .ToList();

        // B1 (earliest cell ref) must get DeterministicId1, B2 must get DeterministicId2.
        Assert.Multiple(() =>
        {
            Assert.That(hyperlinks[0].Attribute("ref")!.Value, Is.EqualTo("B1"));
            Assert.That(hyperlinks[0].Attribute(r + "id")!.Value, Is.EqualTo("DeterministicId1"));
            Assert.That(hyperlinks[1].Attribute("ref")!.Value, Is.EqualTo("B2"));
            Assert.That(hyperlinks[1].Attribute(r + "id")!.Value, Is.EqualTo("DeterministicId2"));
        });
    }

    // Gap 1b: stability check — vary the (rIdA, rIdB) naming and which cell ref
    // each one was originally attached to. After normalization, the cell-ref →
    // DeterministicId mapping must always be the same: the earliest cell gets
    // the lowest id. This is the property the normalizer is supposed to guarantee.
    [Test]
    public void SheetPatcher_InterchangeableIds_MappingIsInputOrderIndependent()
    {
        static Dictionary<string, string> BuildAndConvert(string firstRef, string firstId, string secondRef, string secondId)
        {
            var sheetXml = $"""
                <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"
                           xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <sheetData>
                    <row r="1"><c r="A1"><v>1</v></c></row>
                    <row r="2"><c r="A2"><v>2</v></c></row>
                  </sheetData>
                  <hyperlinks>
                    <hyperlink ref="{firstRef}" r:id="{firstId}" />
                    <hyperlink ref="{secondRef}" r:id="{secondId}" />
                  </hyperlinks>
                </worksheet>
                """;

            var sheetRels = $"""
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="{firstId}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink" Target="https://example.com/" TargetMode="External" />
                  <Relationship Id="{secondId}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink" Target="https://example.com/" TargetMode="External" />
                </Relationships>
                """;

            var zip = BuildZip(new()
            {
                ["xl/worksheets/sheet1.xml"] = sheetXml,
                ["xl/worksheets/_rels/sheet1.xml.rels"] = sheetRels
            });

            var result = DeterministicPackage.Convert(zip);
            var sheet = ReadEntryXml(result, "xl/worksheets/sheet1.xml");
            XNamespace r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            return sheet.Descendants()
                .Where(_ => _.Name.LocalName == "hyperlink")
                .ToDictionary(
                    _ => _.Attribute("ref")!.Value,
                    _ => _.Attribute(r + "id")!.Value);
        }

        var expected = new Dictionary<string, string>
        {
            ["B1"] = "DeterministicId1",
            ["B2"] = "DeterministicId2"
        };

        // Same semantic content, different element / rels / id orderings.
        Assert.Multiple(() =>
        {
            Assert.That(BuildAndConvert("B1", "rIdA", "B2", "rIdB"), Is.EquivalentTo(expected));
            Assert.That(BuildAndConvert("B2", "rIdA", "B1", "rIdB"), Is.EquivalentTo(expected));
            Assert.That(BuildAndConvert("B1", "zzzId", "B2", "aaaId"), Is.EquivalentTo(expected));
            Assert.That(BuildAndConvert("B2", "zzzId", "B1", "aaaId"), Is.EquivalentTo(expected));
        });
    }

    // Gap 2: Pptx patcher coverage for notesSlide, commentAuthors, handoutMaster.
    // The existing ConvertedPptx test uses only slide/master/layout/theme — none
    // of these additional content types exercise the patcher.
    [Test]
    public void PptxPatcher_CoversNotesSlideCommentAuthorsHandoutMaster()
    {
        const string notesSlideXml = """
            <p:notes xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main"
                     xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <p:cSld><p:spTree /></p:cSld>
              <p:ref r:id="rand-notes-ref-1" />
            </p:notes>
            """;

        const string notesSlideRels = """
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rand-notes-ref-1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/notesMaster" Target="../notesMasters/notesMaster1.xml" />
            </Relationships>
            """;

        const string commentAuthorsXml = """
            <p:cmAuthorLst xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main"
                           xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <p:cmAuthor id="0" name="Author" initials="A">
                <p:extLst>
                  <p:ext uri="{X}"><p:link r:id="rand-cm-1" /></p:ext>
                </p:extLst>
              </p:cmAuthor>
            </p:cmAuthorLst>
            """;

        const string commentAuthorsRels = """
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rand-cm-1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/image" Target="media/avatar.png" />
            </Relationships>
            """;

        const string handoutMasterXml = """
            <p:handoutMaster xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main"
                             xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <p:cSld><p:spTree /></p:cSld>
              <p:clrMap />
              <p:ref r:id="rand-ho-1" />
            </p:handoutMaster>
            """;

        const string handoutMasterRels = """
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rand-ho-1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/theme" Target="../theme/theme2.xml" />
            </Relationships>
            """;

        var zip = BuildZip(new()
        {
            ["ppt/notesSlides/notesSlide1.xml"] = notesSlideXml,
            ["ppt/notesSlides/_rels/notesSlide1.xml.rels"] = notesSlideRels,
            ["ppt/commentAuthors.xml"] = commentAuthorsXml,
            ["ppt/_rels/commentAuthors.xml.rels"] = commentAuthorsRels,
            ["ppt/handoutMasters/handoutMaster1.xml"] = handoutMasterXml,
            ["ppt/handoutMasters/_rels/handoutMaster1.xml.rels"] = handoutMasterRels
        });

        var result = DeterministicPackage.Convert(zip);

        AssertRelsIdsAreDeterministic(result, "ppt/notesSlides/_rels/notesSlide1.xml.rels");
        AssertRelsIdsAreDeterministic(result, "ppt/_rels/commentAuthors.xml.rels");
        AssertRelsIdsAreDeterministic(result, "ppt/handoutMasters/_rels/handoutMaster1.xml.rels");

        AssertContentRefsAreDeterministic(result, "ppt/notesSlides/notesSlide1.xml");
        AssertContentRefsAreDeterministic(result, "ppt/commentAuthors.xml");
        AssertContentRefsAreDeterministic(result, "ppt/handoutMasters/handoutMaster1.xml");
    }

    // Gap 3: psmdcp skip in IsSkippedEntry.
    // Entries under package/services/metadata/core-properties/ with .psmdcp suffix
    // must be removed from the output.
    [Test]
    public void PsmdcpEntry_IsRemoved()
    {
        var zip = BuildZip(new()
        {
            ["package/services/metadata/core-properties/abc123.psmdcp"] =
                """<coreProperties xmlns="http://schemas.openxmlformats.org/package/2006/metadata/core-properties" />""",
            ["some/other/entry.xml"] = "<root />"
        });

        var result = DeterministicPackage.Convert(zip);
        result.Position = 0;
        using var archive = new Archive(result, ZipArchiveMode.Read);

        Assert.Multiple(() =>
        {
            Assert.That(archive.Entries.Any(_ => _.FullName.EndsWith(".psmdcp")), Is.False);
            Assert.That(archive.GetEntry("some/other/entry.xml"), Is.Not.Null);
        });
    }

    // Gap 4: RelationshipRenumber.NormalizeTargets fallback — absolute target
    // that does NOT start with the base path must be left as-is (stripping the
    // leading / would break the reference).
    [Test]
    public void NormalizeTargets_ExternalAbsoluteTarget_IsPreserved()
    {
        // Base path derived from entry "word/_rels/document.xml.rels" is "/word/".
        // Target "/foreign/thing.xml" does not start with "/word/" → keep as-is.
        const string rels = """
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="r1" Type="http://example.com/type1" Target="/word/document.xml" />
              <Relationship Id="r2" Type="http://example.com/type2" Target="/foreign/thing.xml" />
            </Relationships>
            """;

        var zip = BuildZip(new()
        {
            ["word/_rels/document.xml.rels"] = rels
        });

        var result = DeterministicPackage.Convert(zip);
        var patched = ReadEntryXml(result, "word/_rels/document.xml.rels");

        var targets = patched.Root!.Elements()
            .ToDictionary(
                _ => _.Attribute("Type")!.Value,
                _ => _.Attribute("Target")!.Value);

        Assert.Multiple(() =>
        {
            // Matches base path → stripped to relative.
            Assert.That(targets["http://example.com/type1"], Is.EqualTo("document.xml"));
            // Does NOT match base path → preserved verbatim.
            Assert.That(targets["http://example.com/type2"], Is.EqualTo("/foreign/thing.xml"));
        });
    }

    // Gap 5: IsSpreadsheetXml fallback — any xl/*.xml entry that doesn't hit a
    // dedicated patcher still runs through FixPrefixedDefaultNamespace.
    [Test]
    public void IsSpreadsheetXml_UnpatchedXlEntry_HasPrefixedNamespaceFixed()
    {
        // xl/sharedStrings.xml has no dedicated patcher, so it falls through
        // to the IsSpreadsheetXml branch. Author it with a prefixed default
        // namespace and assert the prefix is stripped.
        const string sharedStrings = """
            <x:sst xmlns:x="http://schemas.openxmlformats.org/spreadsheetml/2006/main" count="1" uniqueCount="1">
              <x:si><x:t>Hello</x:t></x:si>
            </x:sst>
            """;

        var zip = BuildZip(new()
        {
            ["xl/sharedStrings.xml"] = sharedStrings
        });

        var result = DeterministicPackage.Convert(zip);
        var patched = ReadEntryXml(result, "xl/sharedStrings.xml");

        // Root element must use the default (unprefixed) namespace declaration.
        Assert.Multiple(() =>
        {
            Assert.That(patched.Root!.Name.LocalName, Is.EqualTo("sst"));
            Assert.That(patched.Root.Name.NamespaceName,
                Is.EqualTo("http://schemas.openxmlformats.org/spreadsheetml/2006/main"));
            Assert.That(patched.Root.GetPrefixOfNamespace(patched.Root.Name.Namespace),
                Is.Null.Or.Empty);
        });
    }

    static void AssertRelsIdsAreDeterministic(Stream zip, string entryPath)
    {
        var xml = ReadEntryXml(zip, entryPath);
        var ids = xml.Root!.Elements().Select(_ => _.Attribute("Id")!.Value).ToList();
        foreach (var id in ids)
        {
            Assert.That(id, Does.StartWith("DeterministicId"),
                $"{entryPath} has non-deterministic Id '{id}'");
        }
    }

    static void AssertContentRefsAreDeterministic(Stream zip, string entryPath)
    {
        var xml = ReadEntryXml(zip, entryPath);
        XNamespace r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        var refs = xml.Descendants().Attributes(r + "id").Select(_ => _.Value).ToList();
        Assert.That(refs, Is.Not.Empty, $"{entryPath} should contain at least one r:id reference");
        foreach (var refId in refs)
        {
            Assert.That(refId, Does.StartWith("DeterministicId"),
                $"{entryPath} has non-deterministic r:id '{refId}'");
        }
    }

    static MemoryStream BuildZip(Dictionary<string, string> entries)
    {
        var stream = new MemoryStream();
        using (var archive = new Archive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
                using var entryStream = entry.Open();
                using var writer = new StreamWriter(entryStream, Encoding.UTF8);
                writer.Write(content);
            }
        }

        stream.Position = 0;
        return stream;
    }

    static XDocument ReadEntryXml(Stream zip, string entryPath)
    {
        zip.Position = 0;
        using var archive = new Archive(zip, ZipArchiveMode.Read, leaveOpen: true);
        var entry = archive.GetEntry(entryPath)
            ?? throw new InvalidOperationException($"Entry '{entryPath}' not found in archive.");
        using var entryStream = entry.Open();
        return XDocument.Load(entryStream);
    }
}
