using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using W = DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

[TestFixture]
public class OpenXmlTests
{
    [Test]
    public Task FixesPrefixedNamespace()
    {
        var xlsxStream = CreateSpreadsheet();
        var result = DeterministicPackage.Convert(xlsxStream);

        return VerifyZip(result);
    }

    [Test]
    public void SvgBlipEmbedIdsAreRemapped()
    {
        var docxStream = CreateDocxWithSvg();
        var result = DeterministicPackage.Convert(docxStream);

        result.Position = 0;
        using var archive = new Archive(result, ZipArchiveMode.Read);

        // Collect relationship IDs from .rels
        var relsEntry = archive.GetEntry("word/_rels/document.xml.rels")!;
        using var relsStream = relsEntry.Open();
        var relsXml = XDocument.Load(relsStream);
        var relIds = relsXml.Root!.Elements()
            .Select(_ => _.Attribute("Id")!.Value)
            .ToList();

        // Collect r:embed references from document.xml
        var docEntry = archive.GetEntry("word/document.xml")!;
        using var docStream = docEntry.Open();
        var docXml = XDocument.Load(docStream);
        XNamespace r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        var embedRefs = docXml.Descendants().Attributes(r + "embed")
            .Select(_ => _.Value)
            .ToList();

        // Every r:embed reference must exist in the .rels file
        foreach (var embedRef in embedRefs)
        {
            Assert.That(relIds, Does.Contain(embedRef),
                $"r:embed=\"{embedRef}\" in document.xml has no matching relationship ID in .rels");
        }
    }

    [Test]
    public Task SvgBlipDocx()
    {
        var docxStream = CreateDocxWithSvg();
        var result = DeterministicPackage.Convert(docxStream);

        return Verify(result, extension: "docx")
            .UniqueForRuntime();
    }

    internal static MemoryStream CreateDocxWithSvg()
    {
        var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = document.AddMainDocumentPart();

            // Add PNG fallback
            var pngPart = mainPart.AddImagePart("image/png", "rPng1");
            var pngBytes = Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAIAAAD91JpzAAAAEElEQVR4nGP4z8AARAwQCgAf7gP9i18U1AAAAABJRU5ErkJggg==");
            using (var ms = new MemoryStream(pngBytes))
            {
                pngPart.FeedData(ms);
            }

            // Add SVG
            var svgPart = mainPart.AddImagePart("image/svg+xml", "rSvg1");
            var svgBytes = System.Text.Encoding.UTF8.GetBytes(
                """<svg xmlns="http://www.w3.org/2000/svg" width="100" height="100"><circle cx="50" cy="50" r="40" fill="red" /></svg>""");
            using (var ms = new MemoryStream(svgBytes))
            {
                svgPart.FeedData(ms);
            }

            // Build blip with SVG extension
            var blip = new A.Blip { Embed = "rPng1" };
            var svgBlipElement = new OpenXmlUnknownElement("asvg", "svgBlip", "http://schemas.microsoft.com/office/drawing/2016/SVG/main");
            svgBlipElement.SetAttribute(new OpenXmlAttribute("r", "embed", "http://schemas.openxmlformats.org/officeDocument/2006/relationships", "rSvg1"));
            var ext = new OpenXmlUnknownElement("a", "ext", "http://schemas.openxmlformats.org/drawingml/2006/main");
            ext.SetAttribute(new OpenXmlAttribute("", "uri", "", "{96DAC541-7B7A-43D3-8B79-37D633B846F1}"));
            ext.Append(svgBlipElement);
            var extList = new OpenXmlUnknownElement("a", "extLst", "http://schemas.openxmlformats.org/drawingml/2006/main");
            extList.Append(ext);
            blip.Append(extList);

            var drawing = new W.Drawing(
                new DW.Inline(
                    new DW.Extent { Cx = 952500, Cy = 952500 },
                    new DW.DocProperties { Id = 1U, Name = "Image" },
                    new A.Graphic(
                        new A.GraphicData(
                            new PIC.Picture(
                                new PIC.NonVisualPictureProperties(
                                    new PIC.NonVisualDrawingProperties { Id = 0U, Name = "Image" },
                                    new PIC.NonVisualPictureDrawingProperties()),
                                new PIC.BlipFill(blip, new A.Stretch(new A.FillRectangle())),
                                new PIC.ShapeProperties(
                                    new A.Transform2D(
                                        new A.Offset { X = 0, Y = 0 },
                                        new A.Extents { Cx = 952500, Cy = 952500 }),
                                    new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
                        { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" })
                )
                {
                    DistanceFromTop = 0U,
                    DistanceFromBottom = 0U,
                    DistanceFromLeft = 0U,
                    DistanceFromRight = 0U
                });

            var body = new W.Body(
                new W.Paragraph(
                    new W.Run(
                        new W.Text("Before SVG") { Space = SpaceProcessingModeValues.Preserve })),
                new W.Paragraph(new W.Run(drawing)),
                new W.Paragraph(
                    new W.Run(
                        new W.Text("After SVG") { Space = SpaceProcessingModeValues.Preserve })));
            mainPart.Document = new W.Document(body);
        }

        stream.Position = 0;
        return stream;
    }

    [Test]
    public void FooterHyperlinkIdsAreDeterministic()
    {
        var docxStream = CreateDocxWithFooterHyperlink();
        var result = DeterministicPackage.Convert(docxStream);

        result.Position = 0;
        using var archive = new Archive(result, ZipArchiveMode.Read);

        // Check all .rels files have deterministic IDs
        foreach (var entry in archive.Entries.Where(_ => _.FullName.EndsWith(".rels")))
        {
            using var entryStream = entry.Open();
            var xml = XDocument.Load(entryStream);
            var ids = xml.Root!.Elements()
                .Select(_ => _.Attribute("Id")?.Value)
                .Where(_ => _ != null)
                .ToList();

            foreach (var id in ids)
            {
                Assert.That(id, Does.StartWith("DeterministicId"),
                    $"Entry '{entry.FullName}' has non-deterministic relationship Id '{id}'");
            }
        }
    }

    [Test]
    public void FooterHyperlinkBinaryEquality()
    {
        // Create two docx files with the same content but different random relationship IDs
        using var stream1 = DeterministicPackage.Convert(CreateDocxWithFooterHyperlink());
        using var stream2 = DeterministicPackage.Convert(CreateDocxWithFooterHyperlink());

        var bytes1 = stream1.ToArray();
        var bytes2 = stream2.ToArray();

        Assert.That(bytes1, Is.EqualTo(bytes2));
    }

    [Test]
    public Task FooterHyperlinkDocx()
    {
        var docxStream = CreateDocxWithFooterHyperlink();
        var result = DeterministicPackage.Convert(docxStream);

        return Verify(result, extension: "docx")
            .UniqueForRuntime();
    }

    [Test]
    public Task FooterHyperlinkZip()
    {
        var docxStream = CreateDocxWithFooterHyperlink();
        var result = DeterministicPackage.Convert(docxStream);

        return VerifyZip(result);
    }

    [Test]
    public void HeaderRelIdsAreRemappedInContent()
    {
        var docxStream = CreateDocxWithHeaderHyperlink();
        var result = DeterministicPackage.Convert(docxStream);

        result.Position = 0;
        using var archive = new Archive(result, ZipArchiveMode.Read);

        // Get the relationship IDs from header .rels
        var headerRelsEntry = archive.Entries
            .FirstOrDefault(_ => _.FullName.StartsWith("word/_rels/header") && _.FullName.EndsWith(".rels"));
        Assert.That(headerRelsEntry, Is.Not.Null, "Header .rels entry should exist");

        using var relsStream = headerRelsEntry!.Open();
        var relsXml = XDocument.Load(relsStream);
        var relIds = relsXml.Root!.Elements()
            .Select(_ => _.Attribute("Id")!.Value)
            .ToList();

        // Get r:id references from header XML
        var headerEntry = archive.Entries
            .FirstOrDefault(_ => _.FullName.StartsWith("word/header") && _.FullName.EndsWith(".xml") && !_.FullName.Contains("_rels"));
        Assert.That(headerEntry, Is.Not.Null, "Header XML entry should exist");

        using var headerStream = headerEntry!.Open();
        var headerXml = XDocument.Load(headerStream);
        XNamespace r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        var rIdRefs = headerXml.Descendants().Attributes(r + "id")
            .Select(_ => _.Value)
            .ToList();

        // Every r:id in header content must match a .rels ID
        foreach (var rIdRef in rIdRefs)
        {
            Assert.That(relIds, Does.Contain(rIdRef),
                $"r:id=\"{rIdRef}\" in header XML has no matching relationship ID in header .rels");
        }
    }

    static MemoryStream CreateDocxWithFooterHyperlink()
    {
        var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = document.AddMainDocumentPart();

            // Create footer with hyperlink
            var footerPart = mainPart.AddNewPart<FooterPart>();
            var hyperlinkRel = footerPart.AddHyperlinkRelationship(new Uri("https://example.com"), true);
            var footer = new W.Footer(
                new W.Paragraph(
                    new W.Hyperlink(
                        new W.Run(
                            new W.Text("Example Link") { Space = SpaceProcessingModeValues.Preserve }))
                    {
                        Id = hyperlinkRel.Id
                    }));
            footerPart.Footer = footer;

            var body = new W.Body(
                new W.Paragraph(
                    new W.Run(
                        new W.Text("Body content") { Space = SpaceProcessingModeValues.Preserve })),
                new W.SectionProperties(
                    new W.FooterReference
                    {
                        Type = W.HeaderFooterValues.Default,
                        Id = mainPart.GetIdOfPart(footerPart)
                    }));
            mainPart.Document = new W.Document(body);
        }

        stream.Position = 0;
        return stream;
    }

    static MemoryStream CreateDocxWithHeaderHyperlink()
    {
        var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = document.AddMainDocumentPart();

            // Create header with hyperlink
            var headerPart = mainPart.AddNewPart<HeaderPart>();
            var hyperlinkRel = headerPart.AddHyperlinkRelationship(new Uri("https://example.com/header"), true);
            var header = new W.Header(
                new W.Paragraph(
                    new W.Hyperlink(
                        new W.Run(
                            new W.Text("Header Link") { Space = SpaceProcessingModeValues.Preserve }))
                    {
                        Id = hyperlinkRel.Id
                    }));
            headerPart.Header = header;

            var body = new W.Body(
                new W.Paragraph(
                    new W.Run(
                        new W.Text("Body content") { Space = SpaceProcessingModeValues.Preserve })),
                new W.SectionProperties(
                    new W.HeaderReference
                    {
                        Type = W.HeaderFooterValues.Default,
                        Id = mainPart.GetIdOfPart(headerPart)
                    }));
            mainPart.Document = new W.Document(body);
        }

        stream.Position = 0;
        return stream;
    }

    static MemoryStream CreateSpreadsheet()
    {
        var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new(new Sheets());

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            worksheetPart.Worksheet = new(new SheetData());

            var sheets = workbookPart.Workbook.GetFirstChild<Sheets>()!;
            sheets.Append(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = "Sheet1"
            });

            var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
            stylesPart.Stylesheet = new(
                new Fonts(new Font()),
                new Fills(
                    new Fill(new PatternFill { PatternType = PatternValues.None }),
                    new Fill(new PatternFill { PatternType = PatternValues.Gray125 })),
                new Borders(new Border()),
                new CellFormats(new CellFormat()));

            var sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>()!;

            var headerRow = new Row { RowIndex = 1 };
            headerRow.Append(CreateCell("A1", "Name"));
            headerRow.Append(CreateCell("B1", "Value"));
            sheetData.Append(headerRow);

            var dataRow = new Row { RowIndex = 2 };
            dataRow.Append(CreateCell("A2", "Test"));
            dataRow.Append(CreateCell("B2", "123"));
            sheetData.Append(dataRow);
        }

        stream.Position = 0;
        return stream;
    }

    static Cell CreateCell(string reference, string value) =>
        new()
        {
            CellReference = reference,
            DataType = CellValues.InlineString,
            InlineString = new(new Text(value))
        };
}
