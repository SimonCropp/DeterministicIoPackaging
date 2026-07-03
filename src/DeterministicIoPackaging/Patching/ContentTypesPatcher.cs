// Canonicalizes [Content_Types].xml so it is byte-identical run-to-run and
// producer-independent.
//
// The OPC content-type map lets a part's content type be declared two equivalent
// ways: a <Default Extension="..."> that applies to every part with that
// extension, or a per-part <Override PartName="...">. For an extension whose
// parts carry several different content types (e.g. "xml", shared by workbook /
// worksheet / styles / sharedStrings / core-properties), the producer must pick
// ONE content type to be the extension's Default and emit Overrides for the rest.
//
// System.IO.Packaging (used by DocumentFormat.OpenXml's Clone, and by producers
// such as Aspose.Cells) makes that pick via internal collection ordering, so
// which content type wins the "xml" Default — and therefore which parts become
// Overrides — is not stable across producers, SDK versions, or runtimes. Merely
// sorting the entries (the previous behavior here) preserves whatever split the
// input happened to use, so the non-determinism survives.
//
// This patcher removes the ambiguity by recomputing the split from scratch:
//   1. Determine every part's effective content type from the input map
//      (its Override if present, otherwise the Default for its extension).
//   2. For each extension, choose a canonical Default deterministically:
//      the content type shared by the MOST parts of that extension, with the
//      lexicographically-smallest (Ordinal) content type breaking ties.
//   3. Emit an <Override> for every part whose effective content type differs
//      from its extension's chosen Default.
//   4. Sort Defaults by Extension and Overrides by PartName (both Ordinal).
//
// The rewrite is OPC-preserving: every part still resolves to exactly the content
// type it had in the input. It needs the full set of part names — not just what
// the input map lists — because changing an extension's Default turns previously
// implicit parts into ones that must now be spelled out as Overrides. Those names
// come from the package's zip entries, supplied by DeterministicPackage.Convert.
class ContentTypesPatcher(IReadOnlyCollection<string> partNames) :
    IExactMatchPatcher
{
    static XNamespace ns = "http://schemas.openxmlformats.org/package/2006/content-types";
    static XName defaultName = ns + "Default";
    static XName overrideName = ns + "Override";

    public string ExactMatch => "[Content_Types].xml";

    public bool IsMatch(Entry entry) =>
        entry.FullName is "[Content_Types].xml";

    public void PatchXml(XDocument xml, string entryName)
    {
        var root = xml.Root!;

        var defaults = ReadDefaults(root);
        var overrides = ReadOverrides(root);

        // Group each part by its extension, tracking its effective content type,
        // so every extension can be assigned a single canonical Default below.
        var byExtension = new Dictionary<string, List<PartContentType>>(StringComparer.Ordinal);
        foreach (var partName in partNames)
        {
            var extension = Extension(partName);
            if (extension.Length == 0)
            {
                continue;
            }

            var contentType = EffectiveContentType(partName, extension, defaults, overrides);
            if (contentType == null)
            {
                // No Override and no Default covers this extension. The part has no
                // declared content type in the input; leave the map untouched for it.
                continue;
            }

            if (!byExtension.TryGetValue(extension, out var parts))
            {
                parts = [];
                byExtension.Add(extension, parts);
            }

            parts.Add(new(partName, contentType));
        }

        var newDefaults = new List<XElement>();
        var newOverrides = new List<XElement>();
        foreach (var (extension, parts) in byExtension)
        {
            var canonicalDefault = ChooseDefault(parts);
            newDefaults.Add(
                new(defaultName,
                    new XAttribute("Extension", extension),
                    new XAttribute("ContentType", canonicalDefault)));

            foreach (var part in parts)
            {
                if (!string.Equals(part.ContentType, canonicalDefault, StringComparison.Ordinal))
                {
                    newOverrides.Add(
                        new(overrideName,
                            new XAttribute("PartName", part.PartName),
                            new XAttribute("ContentType", part.ContentType)));
                }
            }
        }

        var ordered = newDefaults
            .OrderBy(_ => (string) _.Attribute("Extension")!, StringComparer.Ordinal)
            .Concat(
                newOverrides
                    .OrderBy(_ => (string) _.Attribute("PartName")!, StringComparer.Ordinal))
            .ToList();

        root.ReplaceAll(ordered);
    }

    // The content type shared by the most parts wins the extension's Default;
    // the lexicographically-smallest content type (Ordinal) breaks ties. Both
    // criteria are total and input-order-independent, so the choice is stable
    // regardless of how the producer ordered its parts.
    static string ChooseDefault(List<PartContentType> parts) =>
        parts
            .GroupBy(_ => _.ContentType, StringComparer.Ordinal)
            .OrderByDescending(_ => _.Count())
            .ThenBy(_ => _.Key, StringComparer.Ordinal)
            .First()
            .Key;

    static string? EffectiveContentType(
        string partName,
        string extension,
        Dictionary<string, string> defaults,
        Dictionary<string, string> overrides)
    {
        if (overrides.TryGetValue(partName, out var overridden))
        {
            return overridden;
        }

        if (defaults.TryGetValue(extension, out var byDefault))
        {
            return byDefault;
        }

        return null;
    }

    static Dictionary<string, string> ReadDefaults(XElement root)
    {
        // Extensions are matched case-insensitively per the OPC spec; normalize to
        // lower-case so a part's extension always finds its Default.
        var defaults = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var element in root.Elements(defaultName))
        {
            var extension = (string?) element.Attribute("Extension");
            var contentType = (string?) element.Attribute("ContentType");
            if (extension != null && contentType != null)
            {
                defaults[extension.ToLowerInvariant()] = contentType;
            }
        }

        return defaults;
    }

    static Dictionary<string, string> ReadOverrides(XElement root)
    {
        var overrides = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var element in root.Elements(overrideName))
        {
            var partName = (string?) element.Attribute("PartName");
            var contentType = (string?) element.Attribute("ContentType");
            if (partName != null && contentType != null)
            {
                overrides[partName] = contentType;
            }
        }

        return overrides;
    }

    // Part names in the content-type map are absolute ("/xl/workbook.xml"); the
    // zip entry names are relative ("xl/workbook.xml"). Normalize entry names to
    // the leading-slash form so they line up with the map's PartName values.
    internal static string ToPartName(string entryFullName) =>
        entryFullName.StartsWith('/') ? entryFullName : "/" + entryFullName;

    static string Extension(string partName)
    {
        var lastDot = partName.LastIndexOf('.');
        if (lastDot < 0 || lastDot == partName.Length - 1)
        {
            return "";
        }

        // A dot must be in the final segment to be an extension.
        var lastSlash = partName.LastIndexOf('/');
        if (lastDot < lastSlash)
        {
            return "";
        }

        return partName.Substring(lastDot + 1).ToLowerInvariant();
    }

    readonly record struct PartContentType(string PartName, string ContentType);
}
