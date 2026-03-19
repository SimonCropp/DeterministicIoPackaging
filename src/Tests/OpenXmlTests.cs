using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

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
