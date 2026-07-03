[TestFixture]
public class ContentTypesPatcherTests
{
    const string variantDefaultIsWorkbook =
        """
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" />
          <Default Extension="xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml" />
          <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml" />
          <Override PartName="/xl/worksheets/sheet2.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml" />
          <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml" />
        </Types>
        """;

    // Same package, but the producer chose a different content type for the "xml"
    // Default and moved the workbook to an Override. Semantically identical.
    const string variantDefaultIsWorksheet =
        """
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" />
          <Default Extension="xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml" />
          <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml" />
          <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml" />
        </Types>
        """;

    static string[] partNames =
    [
        "/_rels/.rels",
        "/xl/workbook.xml",
        "/xl/worksheets/sheet1.xml",
        "/xl/worksheets/sheet2.xml",
        "/xl/styles.xml"
    ];

    static string Patch(string xml) =>
        PatchHelper
            .Patch(new ContentTypesPatcher(partNames), xml, "[Content_Types].xml")
            .ToString();

    // The two equivalent producer splits must canonicalize to identical output.
    [Test]
    public void SplitChoiceIsCanonicalized() =>
        Assert.That(Patch(variantDefaultIsWorksheet), Is.EqualTo(Patch(variantDefaultIsWorkbook)));

    // The most-common content type for an extension becomes its Default; here two
    // worksheets vs one each of workbook/styles, so worksheet+xml wins the "xml"
    // Default and the two worksheet parts carry no Override.
    [Test]
    public Task MostCommonBecomesDefault() =>
        Verify(Patch(variantDefaultIsWorkbook));

    // Every part still resolves to the content type it had in the input: the
    // rewrite must be OPC-preserving, only relocating declarations between
    // Default and Override.
    [Test]
    public void ContentTypesArePreserved()
    {
        var patched = XDocument.Parse(Patch(variantDefaultIsWorkbook));

        var expected = new Dictionary<string, string>
        {
            ["/_rels/.rels"] = "application/vnd.openxmlformats-package.relationships+xml",
            ["/xl/workbook.xml"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml",
            ["/xl/worksheets/sheet1.xml"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml",
            ["/xl/worksheets/sheet2.xml"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml",
            ["/xl/styles.xml"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"
        };

        foreach (var (partName, contentType) in expected)
        {
            Assert.That(Resolve(patched, partName), Is.EqualTo(contentType),
                $"'{partName}' resolved to the wrong content type");
        }
    }

    // Resolves a part's content type the way an OPC reader would: an Override for
    // that exact part wins, otherwise the Default for its extension.
    static string? Resolve(XDocument contentTypes, string partName)
    {
        XNamespace ns = "http://schemas.openxmlformats.org/package/2006/content-types";
        var root = contentTypes.Root!;

        var over = root.Elements(ns + "Override")
            .FirstOrDefault(_ => (string?) _.Attribute("PartName") == partName);
        if (over != null)
        {
            return (string?) over.Attribute("ContentType");
        }

        var extension = partName[(partName.LastIndexOf('.') + 1)..];
        var def = root.Elements(ns + "Default")
            .FirstOrDefault(_ =>
                string.Equals((string?) _.Attribute("Extension"), extension, StringComparison.OrdinalIgnoreCase));
        return (string?) def?.Attribute("ContentType");
    }
}
