using System.Globalization;

[TestFixture]
public class NumberingPatcherTests
{
    [Test]
    public Task Patch()
    {
        var xml =
            """
            <?xml version="1.0" encoding="utf-8" standalone="yes"?>
            <w:numbering xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
                <w:abstractNum w:abstractNumId="1">
                    <w:nsid w:val="02D7C04B" />
                    <w:multiLevelType w:val="hybridMultilevel" />
                </w:abstractNum>
                <w:abstractNum w:abstractNumId="0">
                    <w:nsid w:val="4CD4DDD8" />
                    <w:multiLevelType w:val="hybridMultilevel" />
                </w:abstractNum>
                <w:num w:numId="1">
                    <w:abstractNumId w:val="0" />
                </w:num>
                <w:num w:numId="2">
                    <w:abstractNumId w:val="1" />
                </w:num>
            </w:numbering>
            """;

        var document = PatchHelper.Patch<NumberingPatcher>(xml);
        return Verify(document);
    }

    // abstractNum elements are ordered by their serialized content to assign a
    // deterministic abstractNumId. That sort must use Ordinal (byte) order, not
    // the current culture's linguistic rules — otherwise the id assignment, and
    // therefore the output bytes, would depend on the machine's culture.
    //
    // The two abstractNums below are identical except for a single lvlText value
    // of "z" vs "ä". Ordinal always orders "z" (U+007A) before "ä" (U+00E4). The
    // culture-sensitive default comparer does not: en-US treats "ä" as a variant
    // of "a" (so "ä" sorts first), while sv-SE sorts "ä" after "z". A culture
    // sort would thus swap the abstractNumId assignment between those cultures.
    [Test]
    public void Patch_AbstractNumOrderIsCultureIndependent()
    {
        const string xml =
            """
            <?xml version="1.0" encoding="utf-8" standalone="yes"?>
            <w:numbering xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
                <w:abstractNum w:abstractNumId="1">
                    <w:lvl w:ilvl="0">
                        <w:lvlText w:val="z" />
                    </w:lvl>
                </w:abstractNum>
                <w:abstractNum w:abstractNumId="0">
                    <w:lvl w:ilvl="0">
                        <w:lvlText w:val="ä" />
                    </w:lvl>
                </w:abstractNum>
                <w:num w:numId="1">
                    <w:abstractNumId w:val="0" />
                </w:num>
                <w:num w:numId="2">
                    <w:abstractNumId w:val="1" />
                </w:num>
            </w:numbering>
            """;

        var enUs = PatchUnderCulture(xml, CultureInfo.GetCultureInfo("en-US"));
        var svSe = PatchUnderCulture(xml, CultureInfo.GetCultureInfo("sv-SE"));
        var trTr = PatchUnderCulture(xml, CultureInfo.GetCultureInfo("tr-TR"));
        var invariant = PatchUnderCulture(xml, CultureInfo.InvariantCulture);

        Assert.Multiple(() =>
        {
            // The whole patched document is byte-identical regardless of culture.
            Assert.That(svSe, Is.EqualTo(enUs));
            Assert.That(trTr, Is.EqualTo(enUs));
            Assert.That(invariant, Is.EqualTo(enUs));

            // ...and the order is the Ordinal one: "z" before "ä", so the "z"
            // abstractNum is assigned id 0. The pre-fix culture sort would give
            // it id 1 under en-US.
            Assert.That(AbstractNumIdFor(enUs, "z"), Is.EqualTo("0"));
            Assert.That(AbstractNumIdFor(enUs, "ä"), Is.EqualTo("1"));
        });
    }

    static string PatchUnderCulture(string xml, CultureInfo culture)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = culture;
            return PatchHelper.Patch<NumberingPatcher>(xml)
                .ToString(SaveOptions.DisableFormatting);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    static string AbstractNumIdFor(string patchedXml, string lvlText)
    {
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var abstractNum = XDocument.Parse(patchedXml)
            .Descendants(w + "abstractNum")
            .Single(_ => _.Descendants(w + "lvlText")
                .Any(text => (string?) text.Attribute(w + "val") == lvlText));
        return abstractNum.Attribute(w + "abstractNumId")!.Value;
    }
}
