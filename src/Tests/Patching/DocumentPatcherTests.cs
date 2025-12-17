[TestFixture]
public class DocumentPatcherTests
{
    [Test]
    public Task Patch()
    {
        var xml =
            """
            <?xml version="1.0" encoding="utf-8" standalone="yes"?>
            <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"
                        xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing"
                        xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"
                        xmlns:pic="http://schemas.openxmlformats.org/drawingml/2006/picture">
                <w:body>
                    <w:p>
                        <w:r>
                            <w:drawing>
                                <wp:inline>
                                    <wp:docPr id="1627681933" name="Picture 1" />
                                    <a:graphic>
                                        <a:graphicData>
                                            <pic:pic>
                                                <pic:nvPicPr>
                                                    <pic:cNvPr id="1627681933" name="Picture 1" />
                                                </pic:nvPicPr>
                                            </pic:pic>
                                        </a:graphicData>
                                    </a:graphic>
                                </wp:inline>
                            </w:drawing>
                        </w:r>
                    </w:p>
                    <w:p>
                        <w:r>
                            <w:drawing>
                                <wp:inline>
                                    <wp:docPr id="805879261" name="Picture 2" />
                                    <a:graphic>
                                        <a:graphicData>
                                            <pic:pic>
                                                <pic:nvPicPr>
                                                    <pic:cNvPr id="805879261" name="Picture 2" />
                                                </pic:nvPicPr>
                                            </pic:pic>
                                        </a:graphicData>
                                    </a:graphic>
                                </wp:inline>
                            </w:drawing>
                        </w:r>
                    </w:p>
                    <w:p>
                        <w:r>
                            <w:drawing>
                                <wp:inline>
                                    <wp:docPr id="999999999" name="Picture 3" />
                                    <a:graphic>
                                        <a:graphicData>
                                            <pic:pic>
                                                <pic:nvPicPr>
                                                    <pic:cNvPr id="999999999" name="Picture 3" />
                                                </pic:nvPicPr>
                                            </pic:pic>
                                        </a:graphicData>
                                    </a:graphic>
                                </wp:inline>
                            </w:drawing>
                        </w:r>
                    </w:p>
                </w:body>
            </w:document>
            """;

        var document = PatchHelper.Patch<DocumentPatcher>(xml);
        return Verify(document);
    }
}
