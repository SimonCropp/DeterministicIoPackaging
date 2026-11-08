using System.Globalization;

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

    // Sorting the abstractNums must leave the rest of the part alone: the picture bullets they
    // point at, w:numIdMacAtCleanup, and the root's namespace declarations and mc:Ignorable.
    [Test]
    public async Task KeepsEverythingButTheAbstractNumOrder()
    {
        const string xml =
            """
            <w:numbering
                xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
                xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"
                xmlns:w15="http://schemas.microsoft.com/office/word/2012/wordml"
                mc:Ignorable="w15">
                <w:numPicBullet w:numPicBulletId="0"><w:pict /></w:numPicBullet>
                <w:abstractNum w:abstractNumId="1" w15:restartNumberingAfterBreak="0">
                    <w:multiLevelType w:val="singleLevel" />
                </w:abstractNum>
                <w:abstractNum w:abstractNumId="0">
                    <w:lvl w:ilvl="0"><w:lvlPicBulletId w:val="0" /></w:lvl>
                </w:abstractNum>
                <w:num w:numId="1"><w:abstractNumId w:val="0" /></w:num>
                <w:numIdMacAtCleanup w:val="0" />
            </w:numbering>
            """;

        var root = PatchHelper.Patch<NumberingPatcher>(xml).Root!;

        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        XNamespace mc = "http://schemas.openxmlformats.org/markup-compatibility/2006";
        var names = root.Elements().Select(_ => _.Name.LocalName).ToList();
        using (Assert.Multiple())
        {
            await Assert.That(names).IsEquivalentTo(["numPicBullet", "abstractNum", "abstractNum", "num", "numIdMacAtCleanup"], CollectionOrdering.Matching);
            await Assert.That(root.GetPrefixOfNamespace(w)).IsEqualTo("w");
            await Assert.That((string?) root.Attribute(mc + "Ignorable")).IsEqualTo("w15");
            // The num still points at the abstractNum that uses the picture bullet.
            var picture = root.Elements(w + "abstractNum").Single(_ => _.Descendants(w + "lvlPicBulletId").Any());
            await Assert.That((string?) root.Element(w + "num")!.Element(w + "abstractNumId")!.Attribute(w + "val"))
                .IsEqualTo((string?) picture.Attribute(w + "abstractNumId"));
        }
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
    public async Task Patch_AbstractNumOrderIsCultureIndependent()
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

        using (Assert.Multiple())

        {
            // The whole patched document is byte-identical regardless of culture.
            await Assert.That(svSe).IsEqualTo(enUs);
            await Assert.That(trTr).IsEqualTo(enUs);
            await Assert.That(invariant).IsEqualTo(enUs);

            // ...and the order is the Ordinal one: "z" before "ä", so the "z"
            // abstractNum is assigned id 0. The pre-fix culture sort would give
            // it id 1 under en-US.
            await Assert.That(AbstractNumIdFor(enUs, "z")).IsEqualTo("0");
            await Assert.That(AbstractNumIdFor(enUs, "ä")).IsEqualTo("1");
        }
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
