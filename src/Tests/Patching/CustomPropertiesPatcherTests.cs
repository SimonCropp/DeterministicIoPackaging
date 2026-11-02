public class CustomPropertiesPatcherTests
{
    [Test]
    public Task Patch()
    {
        var xml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Properties xmlns="http://schemas.openxmlformats.org/officeDocument/2006/custom-properties"
                        xmlns:vt="http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes">
              <property fmtid="{D5CDD505-2E9C-101B-9397-08002B2CF9AE}" pid="2" name="Saved">
                <vt:filetime>2026-11-02T10:11:12Z</vt:filetime>
              </property>
              <property fmtid="{D5CDD505-2E9C-101B-9397-08002B2CF9AE}" pid="3" name="Name">
                <vt:lpwstr>untouched</vt:lpwstr>
              </property>
              <property fmtid="{D5CDD505-2E9C-101B-9397-08002B2CF9AE}" pid="4" name="Dates">
                <vt:vector size="1" baseType="filetime">
                  <vt:filetime>2026-11-02T10:11:12Z</vt:filetime>
                </vt:vector>
              </property>
            </Properties>
            """;

        var document = PatchHelper.Patch<CustomPropertiesPatcher>(xml);
        return Verify(document)
            .DontScrubDateTimes();
    }
}
