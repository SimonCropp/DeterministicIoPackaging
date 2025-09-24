static class PatchHelper
{
    public static XDocument Patch<T>(string xml)
        where T : IPatcher, new()
    {
        var document = XDocument.Parse(xml);
        new T().PatchXml(document);

        return document;
    }
}