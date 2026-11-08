// https://github.com/SimonCropp/Determinize/issues/22
public class WhitespaceTests
{
    const string slideName = "ppt/slides/slide1.xml";

    // Only slides with a .rels file are patched (and so reserialized), as every real slide has.
    static (string, string) slideRels =
    (
        "ppt/slides/_rels/slide1.xml.rels",
        """
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideLayout" Target="../slideLayouts/slideLayout1.xml"/></Relationships>
        """
    );

    // DrawingML does not mark text runs with xml:space="preserve", so a run holding only a space is
    // a whitespace-only text node that a default XDocument.Load throws away.
    const string slide =
        """
        <p:sld xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main"><p:cSld><p:spTree><p:sp><p:txBody><a:p><a:r><a:t>basename</a:t></a:r><a:r><a:t> </a:t></a:r><a:r><a:t>"$0"</a:t></a:r></a:p></p:txBody></p:sp></p:spTree></p:cSld></p:sld>
        """;

    [Test]
    public async Task SpaceOnlyRunIsKept()
    {
        using var source = BuildZip((slideName, slide), slideRels);

        using var result = DeterministicPackage.Convert(source);

        await Assert.That(ReadEntry(result, slideName)).Contains("<a:t> </a:t>");
    }

    [Test]
    public async Task SpaceOnlyRunIsKeptAsync()
    {
        using var source = BuildZip((slideName, slide), slideRels);

        using var result = await DeterministicPackage.ConvertAsync(source);

        await Assert.That(ReadEntry(result, slideName)).Contains("<a:t> </a:t>");
    }

    [Test]
    public async Task SpaceOnlyRunIsKeptInSpreadsheetPart()
    {
        const string sharedStrings =
            """
            <sst xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><si><t> </t></si></sst>
            """;
        using var source = BuildZip(("xl/sharedStrings.xml", sharedStrings));

        using var result = DeterministicPackage.Convert(source);

        await Assert.That(ReadEntry(result, "xl/sharedStrings.xml")).Contains("<t> </t>");
    }

    [Test]
    public async Task SpaceOnlyRunReportsNoChange()
    {
        using var source = BuildZip((slideName, slide), slideRels);
        using var first = DeterministicPackage.Convert(source);

        using var second = DeterministicPackage.Convert(first, out var changes);

        await Assert.That(changes).IsEmpty();
    }

    // Indentation between elements is still formatting, not content, so a prettified part and a
    // compact one must still give the same output.
    [Test]
    public async Task IndentationIsStillDropped()
    {
        const string prettified =
            """
            <p:sld xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main">
              <p:cSld>
                <p:spTree>
                  <p:sp>
                    <p:txBody>
                      <a:p>
                        <a:r>
                          <a:t>basename</a:t>
                        </a:r>
                        <a:r>
                          <a:t> </a:t>
                        </a:r>
                        <a:r>
                          <a:t>"$0"</a:t>
                        </a:r>
                      </a:p>
                    </p:txBody>
                  </p:sp>
                </p:spTree>
              </p:cSld>
            </p:sld>
            """;
        using var compactSource = BuildZip((slideName, slide), slideRels);
        using var prettifiedSource = BuildZip((slideName, prettified), slideRels);

        using var fromCompact = DeterministicPackage.Convert(compactSource);
        using var fromPrettified = DeterministicPackage.Convert(prettifiedSource);

        await Assert.That(ReadEntry(fromPrettified, slideName)).IsEqualTo(ReadEntry(fromCompact, slideName));
    }

    static MemoryStream BuildZip(params (string name, string content)[] entries)
    {
        var stream = new MemoryStream();
        using (var archive = new Archive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                var entry = archive.CreateEntry(name);
                using var writer = new StreamWriter(entry.Open());
                writer.Write(content);
            }
        }

        stream.Position = 0;
        return stream;
    }

    static string ReadEntry(MemoryStream zip, string name)
    {
        zip.Position = 0;
        using var archive = new Archive(zip, ZipArchiveMode.Read, leaveOpen: true);
        using var reader = new StreamReader(archive.GetEntry(name)!.Open());
        return reader.ReadToEnd();
    }
}
