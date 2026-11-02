// Replaces every date-time value (vt:filetime) in docProps/custom.xml with
// StableDate.
//
// Producers that build a document in code commonly stamp a custom property
// with the time of the save (eg OfficeIMO), which differs on every run. The
// value is replaced rather than the property removed, so the property, its
// name and its type survive.
class CustomPropertiesPatcher :
    IExactMatchPatcher
{
    public string ExactMatch => "docProps/custom.xml";

    static XNamespace vt = "http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes";
    static XName filetime = vt + "filetime";
    static string stableValue = DeterministicPackage.StableDate.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);

    public void PatchXml(XDocument xml, string entryName)
    {
        foreach (var element in xml.Root!.Descendants(filetime))
        {
            element.Value = stableValue;
        }
    }

    public bool IsMatch(Entry entry) =>
        entry.FullName == "docProps/custom.xml";
}
