static class WorkbookRelationships
{
    public static bool IsWorkbookRelationships(this ZipArchiveEntry entry) =>
        entry.FullName is "xl/_rels/workbook.xml.rels";
}