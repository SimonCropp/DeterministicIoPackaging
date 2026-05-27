[TestFixture]
public class WorkbookPatcherAbsPathTests
{
    // The original WorkbookPatcher would walk Descendants(AlternateContent) and,
    // for each match, walk *its* Descendants(absPath) again — quadratic.
    // The replacement finds absPath directly and walks up to its
    // AlternateContent ancestor. This test pins the AlternateContent removal
    // behaviour so any future regression breaks the snapshot.
    [Test]
    public Task RemovesAlternateContentContainingAbsPath()
    {
        var xml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"
                      xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"
                      xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
                      xmlns:x15="http://schemas.microsoft.com/office/spreadsheetml/2010/11/main"
                      xmlns:x15ac="http://schemas.microsoft.com/office/spreadsheetml/2010/11/ac"
                      mc:Ignorable="x15">
              <workbookPr defaultThemeVersion="166925"/>
              <mc:AlternateContent>
                <mc:Choice Requires="x15">
                  <x15ac:absPath url="C:\Users\test\Documents\" xmlns:x15ac="http://schemas.microsoft.com/office/spreadsheetml/2010/11/ac"/>
                </mc:Choice>
              </mc:AlternateContent>
              <sheets>
                <sheet name="Sheet1" sheetId="1" r:id="rId1"/>
              </sheets>
            </workbook>
            """;

        var document = PatchHelper.Patch(new WorkbookPatcher(new()), xml, "xl/workbook.xml");
        return Verify(document);
    }

    // Sibling AlternateContent blocks without absPath must be preserved —
    // the new code walks up from absPath, so it only removes the parent that
    // actually contains absPath.
    [Test]
    public Task PreservesUnrelatedAlternateContent()
    {
        var xml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"
                      xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"
                      xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
                      xmlns:x15="http://schemas.microsoft.com/office/spreadsheetml/2010/11/main"
                      xmlns:x15ac="http://schemas.microsoft.com/office/spreadsheetml/2010/11/ac"
                      mc:Ignorable="x15">
              <mc:AlternateContent>
                <mc:Choice Requires="x15">
                  <other xmlns="http://example.com/other"/>
                </mc:Choice>
              </mc:AlternateContent>
              <mc:AlternateContent>
                <mc:Choice Requires="x15">
                  <x15ac:absPath url="C:\Path\" xmlns:x15ac="http://schemas.microsoft.com/office/spreadsheetml/2010/11/ac"/>
                </mc:Choice>
              </mc:AlternateContent>
              <sheets>
                <sheet name="Sheet1" sheetId="1" r:id="rId1"/>
              </sheets>
            </workbook>
            """;

        var document = PatchHelper.Patch(new WorkbookPatcher(new()), xml, "xl/workbook.xml");
        return Verify(document);
    }
}
