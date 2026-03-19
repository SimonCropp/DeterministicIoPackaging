static class PatchHelper
{
    public static XDocument Patch<T>(string xml)
        where T : IPatcher, new() =>
        Patch(new T(), xml);

    public static XDocument Patch(IPatcher patcher, string xml)
    {
        var document = XDocument.Parse(xml);
        patcher.PatchXml(document, "test");

        return document;
    }
}