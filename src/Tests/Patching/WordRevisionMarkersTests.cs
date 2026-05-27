[TestFixture]
public class WordRevisionMarkersTests
{
    // Direct test of the helper. End-to-end coverage exists via
    // OpenXmlTests.RevisionMarkersAreStripped, but a focused snapshot makes
    // future regressions obvious without rebuilding a real docx.
    [Test]
    public Task StripsAllKnownAttributes()
    {
        var xml =
            """
            <?xml version="1.0" encoding="utf-8" standalone="yes"?>
            <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"
                        xmlns:w14="http://schemas.microsoft.com/office/word/2010/wordml">
              <w:body>
                <w:p w14:paraId="11111111"
                     w14:textId="22222222"
                     w:rsidR="33333333"
                     w:rsidRPr="44444444"
                     w:rsidP="55555555"
                     w:rsidRDefault="66666666"
                     w:rsidDel="77777777">
                  <w:r w:rsidR="88888888">
                    <w:t>hello</w:t>
                  </w:r>
                </w:p>
                <w:tr w:rsidTr="99999999">
                  <w:tc>
                    <w:p>
                      <w:r><w:t>cell</w:t></w:r>
                    </w:p>
                  </w:tc>
                </w:tr>
                <w:sectPr w:rsidSect="AAAAAAAA">
                  <w:pgSz w:w="12240" w:h="15840"/>
                </w:sectPr>
              </w:body>
            </w:document>
            """;

        var document = XDocument.Parse(xml);
        WordRevisionMarkers.Strip(document);
        return Verify(document);
    }

    // Elements with no rsid/paraId attributes must be left exactly as-is
    // (the new linked-list walk used to do per-element list allocation
    // unconditionally — guards against accidentally mutating something).
    [Test]
    public Task LeavesUnrelatedAttributesUntouched()
    {
        var xml =
            """
            <?xml version="1.0" encoding="utf-8" standalone="yes"?>
            <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
              <w:body>
                <w:p>
                  <w:pPr>
                    <w:pStyle w:val="Normal"/>
                  </w:pPr>
                  <w:r>
                    <w:rPr>
                      <w:b/>
                    </w:rPr>
                    <w:t xml:space="preserve">untouched </w:t>
                  </w:r>
                </w:p>
              </w:body>
            </w:document>
            """;

        var document = XDocument.Parse(xml);
        WordRevisionMarkers.Strip(document);
        return Verify(document);
    }

    [Test]
    public void EmptyDocumentIsNoOp()
    {
        var document = new XDocument();
        Assert.DoesNotThrow(() => WordRevisionMarkers.Strip(document));
    }
}
