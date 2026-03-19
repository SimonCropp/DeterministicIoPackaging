class ContentTypesPatcher(List<string> entryNames) : IPatcher
{
    public bool IsMatch(Entry entry) =>
        entry.FullName is "[Content_Types].xml";

    public bool PatchXml(XDocument xml, string entryName)
    {
        var root = xml.Root!;
        var ns = root.Name.Namespace;

        NormalizeDefaults(root, ns);

        var elements = root.Elements()
            .OrderBy(_ => _.Name.LocalName)
            .ThenBy(_ => (string?)_.Attribute("Extension") ?? "")
            .ThenBy(_ => (string?)_.Attribute("PartName") ?? "")
            .ToList();

        root.ReplaceAll(elements);

        return true;
    }

    void NormalizeDefaults(XElement root, XNamespace ns)
    {
        var defaults = root.Elements(ns + "Default").ToList();

        foreach (var defaultElement in defaults)
        {
            var extension = (string?)defaultElement.Attribute("Extension");
            var contentType = (string?)defaultElement.Attribute("ContentType");

            if (extension is null || contentType is null)
            {
                continue;
            }

            if (!IsOfficeSpecificContentType(contentType))
            {
                continue;
            }

            // Collect parts that already have an Override
            var overriddenParts = root.Elements(ns + "Override")
                .Select(_ => (string?)_.Attribute("PartName"))
                .Where(_ => _ is not null)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Find archive entries with this extension that don't have an Override
            var dotExtension = "." + extension;
            foreach (var entryName in entryNames)
            {
                if (entryName is "[Content_Types].xml")
                {
                    continue;
                }

                if (!entryName.EndsWith(dotExtension, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var partName = "/" + entryName;
                if (overriddenParts.Contains(partName))
                {
                    continue;
                }

                root.Add(new XElement(ns + "Override",
                    new XAttribute("PartName", partName),
                    new XAttribute("ContentType", contentType)));
            }

            // Reset the Default to the generic content type
            defaultElement.SetAttributeValue("ContentType", GetGenericContentType(extension));
        }
    }

    static bool IsOfficeSpecificContentType(string contentType) =>
        contentType.Contains("officedocument");

    static string GetGenericContentType(string extension) =>
        extension.ToLowerInvariant() switch
        {
            "xml" => "application/xml",
            _ => "application/octet-stream"
        };
}
