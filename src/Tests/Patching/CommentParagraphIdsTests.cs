public class CommentParagraphIdsTests
{
    static XNamespace w14 = "http://schemas.microsoft.com/office/word/2010/wordml";
    static XNamespace w15 = "http://schemas.microsoft.com/office/word/2012/wordml";
    static XNamespace w16cid = "http://schemas.microsoft.com/office/word/2016/wordml/cid";

    // A comment and a reply to it, the reply resolved. The reply thread and the resolved state live
    // in commentsExtended.xml, keyed by the w14:paraId of each comment's last paragraph.
    static MemoryStream BuildPackage(string firstId, string replyId)
    {
        var comments =
            $"""
             <w:comments xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main" xmlns:w14="{w14}">
                 <w:comment w:id="0" w:author="A"><w:p w14:paraId="{firstId}" w14:textId="77777777"><w:r><w:t>Question</w:t></w:r></w:p></w:comment>
                 <w:comment w:id="1" w:author="B"><w:p w14:paraId="{replyId}" w14:textId="77777777"><w:r><w:t>Answer</w:t></w:r></w:p></w:comment>
             </w:comments>
             """;
        var extended =
            $"""
             <w15:commentsEx xmlns:w15="{w15}">
                 <w15:commentEx w15:paraId="{firstId}" w15:done="0" />
                 <w15:commentEx w15:paraId="{replyId}" w15:paraIdParent="{firstId}" w15:done="1" />
             </w15:commentsEx>
             """;
        var ids =
            $"""
             <w16cid:commentsIds xmlns:w16cid="{w16cid}">
                 <w16cid:commentId w16cid:paraId="{firstId}" w16cid:durableId="11111111" />
                 <w16cid:commentId w16cid:paraId="{replyId}" w16cid:durableId="22222222" />
             </w16cid:commentsIds>
             """;

        var stream = new MemoryStream();
        using (var archive = new Archive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(archive, "word/comments.xml", comments);
            Add(archive, "word/commentsExtended.xml", extended);
            Add(archive, "word/commentsIds.xml", ids);
        }

        stream.Position = 0;
        return stream;
    }

    [Test]
    public async Task RepliesStayThreaded()
    {
        using var source = BuildPackage("1A2B3C4D", "5E6F7A0B");
        using var result = DeterministicPackage.Convert(source);
        using var archive = new Archive(result, ZipArchiveMode.Read, leaveOpen: true);

        var paragraphIds = Load(archive, "word/comments.xml").Descendants().Attributes(w14 + "paraId").Select(_ => _.Value).ToList();
        var extended = Load(archive, "word/commentsExtended.xml").Root!.Elements().ToList();
        var ids = Load(archive, "word/commentsIds.xml").Root!.Elements().ToList();

        using (Assert.Multiple())
        {
            await Assert.That(paragraphIds).IsEquivalentTo(["00000001", "00000002"], CollectionOrdering.Matching);
            await Assert.That(Load(archive, "word/comments.xml").Descendants().Attributes(w14 + "textId").Any()).IsFalse();
            await Assert.That(extended[0].Attribute(w15 + "paraId")!.Value).IsEqualTo("00000001");
            await Assert.That(extended[1].Attribute(w15 + "paraId")!.Value).IsEqualTo("00000002");
            await Assert.That(extended[1].Attribute(w15 + "paraIdParent")!.Value).IsEqualTo("00000001");
            await Assert.That(ids[0].Attribute(w16cid + "paraId")!.Value).IsEqualTo("00000001");
            await Assert.That(ids[1].Attribute(w16cid + "paraId")!.Value).IsEqualTo("00000002");
        }
    }

    // The ids are random per save, so two saves of the same comments must still convert alike.
    [Test]
    public async Task DifferentIdsConvertAlike()
    {
        using var firstSource = BuildPackage("1A2B3C4D", "5E6F7A0B");
        using var secondSource = BuildPackage("0F0E0D0C", "7B6A5948");
        using var first = DeterministicPackage.Convert(firstSource);
        using var second = DeterministicPackage.Convert(secondSource);

        await Assert.That(first.ToArray()).IsEquivalentTo(second.ToArray(), CollectionOrdering.Matching);
    }

    static void Add(Archive archive, string name, string content)
    {
        using var writer = new StreamWriter(archive.CreateEntry(name).Open());
        writer.Write(content);
    }

    static XDocument Load(Archive archive, string name)
    {
        using var stream = archive.GetEntry(name)!.Open();
        return XDocument.Load(stream);
    }
}
