[TestFixture]
public class SheetPatcherTests
{
    [Test]
    public Task Patch()
    {
        var xml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <worksheet
                xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"
                xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"
                xmlns:xdr="http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing"
                xmlns:x14="http://schemas.microsoft.com/office/spreadsheetml/2009/9/main"
                xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
                xmlns:x14ac="http://schemas.microsoft.com/office/spreadsheetml/2009/9/ac"
                xmlns:xm="http://schemas.microsoft.com/office/excel/2006/main"
                xmlns:xr="http://schemas.microsoft.com/office/spreadsheetml/2014/revision"
                xmlns:xr2="http://schemas.microsoft.com/office/spreadsheetml/2015/revision2"
                xmlns:xr3="http://schemas.microsoft.com/office/spreadsheetml/2016/revision3"
                mc:Ignorable="x14ac xr xr2 xr3"
                xr:uid="{F81C51CE-C559-4BE6-9436-ED68E7BEB9A9}">
              <dimension ref="A5"/>
              <sheetViews>
                <sheetView tabSelected="1" workbookViewId="0" topLeftCell="A1"/>
              </sheetViews>
              <sheetFormatPr defaultRowHeight="12.75"/>
              <sheetData>
                <row r="5" spans="1:1" ht="23.25" customHeight="1">
                  <c r="A5" s="3" t="s">
                    <v>30</v>
                  </c>
                </row>
              </sheetData>
              <pageMargins left="0.75" right="0.75" top="1" bottom="1" header="0.5" footer="0.5"/>
            </worksheet>
            """;
        var document = PatchHelper.Patch(new SheetPatcher(new()), xml);
        return Verify(document);
    }

    [Test]
    public Task PatchWithHyperlinks()
    {
        var relsPatcher = new SheetRelationshipPatcher();
        var relsXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships
              xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship
                Id="rId2"
                Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink"
                Target="https://example.com"
                TargetMode="External" />
              <Relationship
                Id="rId1"
                Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink"
                Target="https://google.com"
                TargetMode="External" />
            </Relationships>
            """;
        PatchHelper.Patch(relsPatcher, relsXml, "xl/worksheets/_rels/sheet1.xml.rels");

        var xml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <worksheet
                xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"
                xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <sheetData>
                <row r="1">
                  <c r="A1" t="inlineStr"><is><t>Example</t></is></c>
                  <c r="B1" t="inlineStr"><is><t>Google</t></is></c>
                </row>
              </sheetData>
              <hyperlinks>
                <hyperlink ref="A1" r:id="rId2" />
                <hyperlink ref="B1" r:id="rId1" />
              </hyperlinks>
            </worksheet>
            """;
        var document = PatchHelper.Patch(new SheetPatcher(relsPatcher), xml, "xl/worksheets/sheet1.xml");
        return Verify(document);
    }

    [Test]
    public Task PatchWithDuplicateHyperlinkTargets()
    {
        // When multiple hyperlinks share the same target URL, the DeterministicId
        // assignment must be normalized by cell reference to ensure determinism
        // regardless of the original (non-deterministic) rId ordering.
        var relsPatcher = new SheetRelationshipPatcher();
        var relsXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships
              xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship
                Id="rId1"
                Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink"
                Target="https://github.com"
                TargetMode="External" />
              <Relationship
                Id="rId3"
                Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink"
                Target="https://google.com"
                TargetMode="External" />
              <Relationship
                Id="rId2"
                Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink"
                Target="https://google.com"
                TargetMode="External" />
            </Relationships>
            """;
        PatchHelper.Patch(relsPatcher, relsXml, "xl/worksheets/_rels/sheet1.xml.rels");

        // rId3 maps to E2, rId2 maps to B2.
        // After rels renumbering: google.com gets DeterministicId2 and DeterministicId3
        // (sorted by target, rId2 and rId3 are interchangeable).
        // The normalization should ensure B2 always gets the lower DeterministicId.
        var xml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <worksheet
                xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"
                xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <sheetData>
                <row r="2">
                  <c r="B2" t="inlineStr"><is><t>Google</t></is></c>
                  <c r="C2" t="inlineStr"><is><t>GitHub</t></is></c>
                  <c r="E2" t="inlineStr"><is><t>Google Link</t></is></c>
                </row>
              </sheetData>
              <hyperlinks>
                <hyperlink ref="B2" r:id="rId2" display="Google" />
                <hyperlink ref="C2" r:id="rId1" display="GitHub" />
                <hyperlink ref="E2" r:id="rId3" display="Google Link" />
              </hyperlinks>
            </worksheet>
            """;
        var document = PatchHelper.Patch(new SheetPatcher(relsPatcher), xml, "xl/worksheets/sheet1.xml");
        return Verify(document);
    }

    [Test]
    public Task PatchWithDuplicateHyperlinkTargets_ReversedIds()
    {
        // Same scenario as above but with rId2 and rId3 swapped in the rels file.
        // This simulates a different Aspose run where the IDs are assigned differently.
        // The output must be identical to PatchWithDuplicateHyperlinkTargets.
        var relsPatcher = new SheetRelationshipPatcher();
        var relsXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships
              xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship
                Id="rId1"
                Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink"
                Target="https://github.com"
                TargetMode="External" />
              <Relationship
                Id="rId2"
                Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink"
                Target="https://google.com"
                TargetMode="External" />
              <Relationship
                Id="rId3"
                Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink"
                Target="https://google.com"
                TargetMode="External" />
            </Relationships>
            """;
        PatchHelper.Patch(relsPatcher, relsXml, "xl/worksheets/_rels/sheet1.xml.rels");

        // rId2 now maps to B2, rId3 maps to E2 (opposite of the other test).
        // After normalization, the output should be identical.
        var xml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <worksheet
                xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"
                xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <sheetData>
                <row r="2">
                  <c r="B2" t="inlineStr"><is><t>Google</t></is></c>
                  <c r="C2" t="inlineStr"><is><t>GitHub</t></is></c>
                  <c r="E2" t="inlineStr"><is><t>Google Link</t></is></c>
                </row>
              </sheetData>
              <hyperlinks>
                <hyperlink ref="B2" r:id="rId3" display="Google" />
                <hyperlink ref="C2" r:id="rId1" display="GitHub" />
                <hyperlink ref="E2" r:id="rId2" display="Google Link" />
              </hyperlinks>
            </worksheet>
            """;
        var document = PatchHelper.Patch(new SheetPatcher(relsPatcher), xml, "xl/worksheets/sheet1.xml");
        return Verify(document);
    }
}