using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Validation;
using W = DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using P = DocumentFormat.OpenXml.Presentation;

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

    [Test]
    public void ValidateConvertedSpreadsheet()
    {
        var stream = CreateSpreadsheet();
        var result = DeterministicPackage.Convert(stream);
        result.Position = 0;

        using var document = SpreadsheetDocument.Open(result, false);
        var validator = new OpenXmlValidator();
        var errors = validator.Validate(document).ToList();

        Assert.That(errors, Is.Empty,
            string.Join(Environment.NewLine, errors.Select(_ => $"{_.Description} ({_.Path})")));
    }

    [Test]
    public void ValidateConvertedDocxWithSvg()
    {
        var stream = CreateDocxWithSvg();
        var result = DeterministicPackage.Convert(stream);
        result.Position = 0;

        using var document = WordprocessingDocument.Open(result, false);
        var validator = new OpenXmlValidator();
        var errors = validator.Validate(document).ToList();

        Assert.That(errors, Is.Empty,
            string.Join(Environment.NewLine, errors.Select(_ => $"{_.Description} ({_.Path})")));
    }

    [Test]
    public void ValidateConvertedDocxWithFooterHyperlink()
    {
        var stream = CreateDocxWithFooterHyperlink();
        var result = DeterministicPackage.Convert(stream);
        result.Position = 0;

        using var document = WordprocessingDocument.Open(result, false);
        var validator = new OpenXmlValidator();
        var errors = validator.Validate(document).ToList();

        Assert.That(errors, Is.Empty,
            string.Join(Environment.NewLine, errors.Select(_ => $"{_.Description} ({_.Path})")));
    }

    [Test]
    public void ValidateConvertedDocxWithHeaderHyperlink()
    {
        var stream = CreateDocxWithHeaderHyperlink();
        var result = DeterministicPackage.Convert(stream);
        result.Position = 0;

        using var document = WordprocessingDocument.Open(result, false);
        var validator = new OpenXmlValidator();
        var errors = validator.Validate(document).ToList();

        Assert.That(errors, Is.Empty,
            string.Join(Environment.NewLine, errors.Select(_ => $"{_.Description} ({_.Path})")));
    }

    static Cell CreateCell(string reference, string value) =>
        new()
        {
            CellReference = reference,
            DataType = CellValues.InlineString,
            InlineString = new(new Text(value))
        };

    [Test]
    public Task ConvertedPptx()
    {
        var pptxStream = CreatePresentation();
        var result = DeterministicPackage.Convert(pptxStream);

        return Verify(result, extension: "pptx")
            .UniqueForRuntime();
    }

    [Test]
    public Task ConvertedPptxZip()
    {
        var pptxStream = CreatePresentation();
        var result = DeterministicPackage.Convert(pptxStream);

        return VerifyZip(result);
    }

    [Test]
    public void PptxBinaryEquality()
    {
        using var stream1 = DeterministicPackage.Convert(CreatePresentation());
        using var stream2 = DeterministicPackage.Convert(CreatePresentation());

        var bytes1 = stream1.ToArray();
        var bytes2 = stream2.ToArray();

        Assert.That(bytes1, Is.EqualTo(bytes2));
    }

    [Test]
    public void ValidateConvertedPptx()
    {
        var stream = CreatePresentation();
        var result = DeterministicPackage.Convert(stream);
        result.Position = 0;

        using var document = PresentationDocument.Open(result, false);
        var validator = new OpenXmlValidator();
        var errors = validator.Validate(document).ToList();

        Assert.That(errors, Is.Empty,
            string.Join(Environment.NewLine, errors.Select(_ => $"{_.Description} ({_.Path})")));
    }

    internal static MemoryStream CreatePresentation()
    {
        var stream = new MemoryStream();
        using (var document = PresentationDocument.Create(stream, PresentationDocumentType.Presentation))
        {
            var presentationPart = document.AddPresentationPart();
            presentationPart.Presentation = new P.Presentation();

            var slideMasterPart = presentationPart.AddNewPart<SlideMasterPart>("smRid1");
            var themePart = slideMasterPart.AddNewPart<ThemePart>("themeRid1");
            themePart.Theme = new A.Theme(
                new A.ThemeElements(
                    new A.ColorScheme(
                        new A.Dark1Color(new A.SystemColor { Val = A.SystemColorValues.WindowText, LastColor = "000000" }),
                        new A.Light1Color(new A.SystemColor { Val = A.SystemColorValues.Window, LastColor = "FFFFFF" }),
                        new A.Dark2Color(new A.RgbColorModelHex { Val = "1F497D" }),
                        new A.Light2Color(new A.RgbColorModelHex { Val = "EEECE1" }),
                        new A.Accent1Color(new A.RgbColorModelHex { Val = "4F81BD" }),
                        new A.Accent2Color(new A.RgbColorModelHex { Val = "C0504D" }),
                        new A.Accent3Color(new A.RgbColorModelHex { Val = "9BBB59" }),
                        new A.Accent4Color(new A.RgbColorModelHex { Val = "8064A2" }),
                        new A.Accent5Color(new A.RgbColorModelHex { Val = "4BACC6" }),
                        new A.Accent6Color(new A.RgbColorModelHex { Val = "F79646" }),
                        new A.Hyperlink(new A.RgbColorModelHex { Val = "0000FF" }),
                        new A.FollowedHyperlinkColor(new A.RgbColorModelHex { Val = "800080" }))
                    { Name = "Office" },
                    new A.FontScheme(
                        new A.MajorFont(new A.LatinFont { Typeface = "Calibri" }, new A.EastAsianFont { Typeface = "" }, new A.ComplexScriptFont { Typeface = "" }),
                        new A.MinorFont(new A.LatinFont { Typeface = "Calibri" }, new A.EastAsianFont { Typeface = "" }, new A.ComplexScriptFont { Typeface = "" }))
                    { Name = "Office" },
                    new A.FormatScheme(
                        new A.FillStyleList(
                            new A.SolidFill(new A.SchemeColor { Val = A.SchemeColorValues.PhColor }),
                            new A.GradientFill(
                                new A.GradientStopList(
                                    new A.GradientStop(new A.SchemeColor { Val = A.SchemeColorValues.PhColor }) { Position = 0 },
                                    new A.GradientStop(new A.SchemeColor { Val = A.SchemeColorValues.PhColor }) { Position = 100000 }),
                                new A.LinearGradientFill { Angle = 5400000, Scaled = true }),
                            new A.SolidFill(new A.SchemeColor { Val = A.SchemeColorValues.PhColor })),
                        new A.LineStyleList(
                            new A.Outline(new A.SolidFill(new A.SchemeColor { Val = A.SchemeColorValues.PhColor })) { Width = 9525 },
                            new A.Outline(new A.SolidFill(new A.SchemeColor { Val = A.SchemeColorValues.PhColor })) { Width = 25400 },
                            new A.Outline(new A.SolidFill(new A.SchemeColor { Val = A.SchemeColorValues.PhColor })) { Width = 38100 }),
                        new A.EffectStyleList(
                            new A.EffectStyle(new A.EffectList()),
                            new A.EffectStyle(new A.EffectList()),
                            new A.EffectStyle(new A.EffectList())),
                        new A.BackgroundFillStyleList(
                            new A.SolidFill(new A.SchemeColor { Val = A.SchemeColorValues.PhColor }),
                            new A.SolidFill(new A.SchemeColor { Val = A.SchemeColorValues.PhColor }),
                            new A.SolidFill(new A.SchemeColor { Val = A.SchemeColorValues.PhColor })))
                    { Name = "Office" }),
                new A.ObjectDefaults(),
                new A.ExtraColorSchemeList())
            { Name = "Office Theme" };

            slideMasterPart.SlideMaster = new P.SlideMaster(
                new P.CommonSlideData(
                    new P.Background(
                        new P.BackgroundStyleReference(new A.SchemeColor { Val = A.SchemeColorValues.PhColor }) { Index = 1001 }),
                    new P.ShapeTree(
                        new P.NonVisualGroupShapeProperties(
                            new P.NonVisualDrawingProperties { Id = 1, Name = "" },
                            new P.NonVisualGroupShapeDrawingProperties(),
                            new P.ApplicationNonVisualDrawingProperties()),
                        new P.GroupShapeProperties(new A.TransformGroup()))),
                new P.ColorMap
                {
                    Background1 = A.ColorSchemeIndexValues.Light1,
                    Text1 = A.ColorSchemeIndexValues.Dark1,
                    Background2 = A.ColorSchemeIndexValues.Light2,
                    Text2 = A.ColorSchemeIndexValues.Dark2,
                    Accent1 = A.ColorSchemeIndexValues.Accent1,
                    Accent2 = A.ColorSchemeIndexValues.Accent2,
                    Accent3 = A.ColorSchemeIndexValues.Accent3,
                    Accent4 = A.ColorSchemeIndexValues.Accent4,
                    Accent5 = A.ColorSchemeIndexValues.Accent5,
                    Accent6 = A.ColorSchemeIndexValues.Accent6,
                    Hyperlink = A.ColorSchemeIndexValues.Hyperlink,
                    FollowedHyperlink = A.ColorSchemeIndexValues.FollowedHyperlink
                },
                new P.SlideLayoutIdList(new P.SlideLayoutId { Id = 2147483649U, RelationshipId = "slRid1" }));

            var slideLayoutPart = slideMasterPart.AddNewPart<SlideLayoutPart>("slRid1");
            slideLayoutPart.SlideLayout = new P.SlideLayout(
                new P.CommonSlideData(
                    new P.ShapeTree(
                        new P.NonVisualGroupShapeProperties(
                            new P.NonVisualDrawingProperties { Id = 1, Name = "" },
                            new P.NonVisualGroupShapeDrawingProperties(),
                            new P.ApplicationNonVisualDrawingProperties()),
                        new P.GroupShapeProperties(new A.TransformGroup()))),
                new P.ColorMapOverride(new A.MasterColorMapping()))
            { Type = P.SlideLayoutValues.Title };

            var slidePart = presentationPart.AddNewPart<SlidePart>("sldRid1");
            slidePart.AddPart(slideLayoutPart);
            slidePart.Slide = new P.Slide(
                new P.CommonSlideData(
                    new P.ShapeTree(
                        new P.NonVisualGroupShapeProperties(
                            new P.NonVisualDrawingProperties { Id = 1, Name = "" },
                            new P.NonVisualGroupShapeDrawingProperties(),
                            new P.ApplicationNonVisualDrawingProperties()),
                        new P.GroupShapeProperties(new A.TransformGroup()),
                        new P.Shape(
                            new P.NonVisualShapeProperties(
                                new P.NonVisualDrawingProperties { Id = 2, Name = "Title" },
                                new P.NonVisualShapeDrawingProperties(new A.ShapeLocks { NoGrouping = true }),
                                new P.ApplicationNonVisualDrawingProperties(new P.PlaceholderShape { Type = P.PlaceholderValues.CenteredTitle })),
                            new P.ShapeProperties(),
                            new P.TextBody(
                                new A.BodyProperties(),
                                new A.ListStyle(),
                                new A.Paragraph(
                                    new A.Run(
                                        new A.RunProperties { Language = "en-US" },
                                        new A.Text("Deterministic!"))))))));

            presentationPart.Presentation = new P.Presentation(
                new P.SlideMasterIdList(new P.SlideMasterId { Id = 2147483648U, RelationshipId = "smRid1" }),
                new P.SlideIdList(new P.SlideId { Id = 256U, RelationshipId = "sldRid1" }),
                new P.SlideSize { Cx = 9144000, Cy = 6858000 },
                new P.NotesSize { Cx = 6858000, Cy = 9144000 });
        }

        stream.Position = 0;
        return stream;
    }
}
