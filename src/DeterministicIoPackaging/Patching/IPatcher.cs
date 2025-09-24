interface IPatcher
{
    public void PatchXml(XDocument xml);
    public bool IsMatch(Entry entry);
}