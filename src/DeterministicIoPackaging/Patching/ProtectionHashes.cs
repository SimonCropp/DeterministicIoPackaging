// Replaces the salt and hash of password protection with fixed values.
//
// OOXML password protection stores a hash of the password together with the random salt it was
// hashed with. The salt is random by design, so producing the same protected document twice gives
// different bytes, and some writers (eg Syncfusion) generate a fresh salt on every save:
//   - xlsx <sheetProtection>, <protectedRange>, <fileSharing>: saltValue / hashValue
//   - xlsx <workbookProtection>: workbookSaltValue / workbookHashValue,
//     revisionsSaltValue / revisionsHashValue
//   - docx <w:documentProtection>, <w:writeProtection>: w:salt / w:hash (legacy names)
//     and w:saltValue / w:hashValue
//
// Each value is replaced with zero bytes of the same decoded length, so the part stays valid
// OOXML. The original password no longer unlocks the converted document.
//
// All these elements are direct children of the part's root (worksheet, workbook, w:settings),
// so only the root's children are inspected rather than walking a potentially large sheet.
static class ProtectionHashes
{
    static HashSet<string> elements =
    [
        "sheetProtection",
        "protectedRange",
        "workbookProtection",
        "fileSharing",
        "documentProtection",
        "writeProtection"
    ];

    static HashSet<string> attributes =
    [
        "salt",
        "hash",
        "saltValue",
        "hashValue",
        "workbookSaltValue",
        "workbookHashValue",
        "revisionsSaltValue",
        "revisionsHashValue"
    ];

    public static void Scrub(XDocument xml)
    {
        var root = xml.Root;
        if (root == null)
        {
            return;
        }

        foreach (var element in root.Elements())
        {
            if (element.Name.LocalName == "protectedRanges")
            {
                foreach (var range in element.Elements())
                {
                    ScrubElement(range);
                }

                continue;
            }

            ScrubElement(element);
        }
    }

    static void ScrubElement(XElement element)
    {
        if (!elements.Contains(element.Name.LocalName))
        {
            return;
        }

        foreach (var attribute in element.Attributes())
        {
            if (attributes.Contains(attribute.Name.LocalName))
            {
                attribute.Value = Zeroed(attribute.Value);
            }
        }
    }

    static string Zeroed(string value)
    {
        int length;
        try
        {
            length = Convert.FromBase64String(value).Length;
        }
        catch (FormatException)
        {
            return value;
        }

        return Convert.ToBase64String(new byte[length]);
    }
}
