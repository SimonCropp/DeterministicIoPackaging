interface IPatcher
{
    public void PatchXml(XDocument xml, string entryName);
    public bool IsMatch(Entry entry);
}
