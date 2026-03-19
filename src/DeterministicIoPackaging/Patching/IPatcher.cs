interface IPatcher
{
    public bool PatchXml(XDocument xml, string entryName);
    public bool IsMatch(Entry entry);
}
