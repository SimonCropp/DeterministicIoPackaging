static class WorkbookRelationships
{
    public static bool IsWorkbookRelationships(this Entry entry) =>
        entry.FullName is "xl/_rels/workbook.xml.rels";
}