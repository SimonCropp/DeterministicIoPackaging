public class WordIdNumberingTests
{
    [Test]
    public Task Apply()
    {
        var xml =
            """
            <?xml version="1.0" encoding="utf-8" standalone="yes"?>
            <w:hdr xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"
                   xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing"
                   xmlns:wp14="http://schemas.microsoft.com/office/word/2010/wordprocessingDrawing"
                   xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"
                   xmlns:pic="http://schemas.openxmlformats.org/drawingml/2006/picture">
              <w:sdt>
                <w:sdtPr>
                  <w:id w:val="-1834946227"/>
                  <w:date w:fullDate="2024-05-06T00:00:00Z"/>
                </w:sdtPr>
                <w:sdtContent>
                  <w:p>
                    <w:ins w:id="1" w:author="Author" w:date="2026-11-02T10:11:12Z">
                      <w:r>
                        <w:drawing>
                          <wp:anchor wp14:anchorId="3A4B5C6D" wp14:editId="7E8F9A0B">
                            <wp:docPr id="1627681933" name="Picture 1"/>
                            <a:graphic>
                              <a:graphicData>
                                <pic:pic>
                                  <pic:nvPicPr>
                                    <pic:cNvPr id="0" name="Picture 1"/>
                                  </pic:nvPicPr>
                                </pic:pic>
                              </a:graphicData>
                            </a:graphic>
                          </wp:anchor>
                        </w:drawing>
                      </w:r>
                    </w:ins>
                  </w:p>
                </w:sdtContent>
              </w:sdt>
              <w:sdt>
                <w:sdtPr>
                  <w:id w:val="1294018223"/>
                </w:sdtPr>
              </w:sdt>
            </w:hdr>
            """;

        var document = XDocument.Parse(xml);
        new WordIdNumbering().Apply(document.Root!);
        return Verify(document)
            .DontScrubDateTimes();
    }

    // Numbering continues from one part to the next, so ids stay unique
    // across the document rather than restarting at 1 in each part.
    [Test]
    public async Task NumberingContinuesAcrossParts()
    {
        var xml =
            """
            <w:hdr xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"
                   xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing">
              <wp:docPr id="99" name="Picture"/>
              <w:sdt>
                <w:sdtPr>
                  <w:id w:val="-5"/>
                </w:sdtPr>
              </w:sdt>
            </w:hdr>
            """;
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        XNamespace wp = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";

        var numbering = new WordIdNumbering();
        var first = XDocument.Parse(xml);
        var second = XDocument.Parse(xml);
        numbering.Apply(first.Root!);
        numbering.Apply(second.Root!);

        await Assert.That(second.Descendants(wp + "docPr").Single().Attribute("id")!.Value).IsEqualTo("2");
        await Assert.That(second.Descendants(w + "id").Single().Attribute(w + "val")!.Value).IsEqualTo("2");
    }

    // A w:id outside w:sdtPr (eg a bookmark or comment id) is not a content
    // control id and must be left alone.
    [Test]
    public async Task LeavesOtherIdsUntouched()
    {
        var xml =
            """
            <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
              <w:body>
                <w:p>
                  <w:bookmarkStart w:id="7" w:name="b"/>
                  <w:id w:val="42"/>
                </w:p>
              </w:body>
            </w:document>
            """;
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

        var document = XDocument.Parse(xml);
        new WordIdNumbering().Apply(document.Root!);

        await Assert.That(document.Descendants(w + "id").Single().Attribute(w + "val")!.Value).IsEqualTo("42");
        await Assert.That(document.Descendants(w + "bookmarkStart").Single().Attribute(w + "id")!.Value).IsEqualTo("7");
    }
}
