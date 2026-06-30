using System.Buffers.Binary;

static class SampleXml
{
    static XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    static XNamespace w14 = "http://schemas.microsoft.com/office/word/2010/wordml";
    static XNamespace wp = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
    static XNamespace pic = "http://schemas.openxmlformats.org/drawingml/2006/picture";
    static XNamespace a = "http://schemas.openxmlformats.org/drawingml/2006/main";
    static XNamespace r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    public static XDocument BuildWordDocument(int paragraphs, int drawings, int hyperlinks)
    {
        var body = new XElement(w + "body");
        for (var p = 0; p < paragraphs; p++)
        {
            // Paragraph with all the rsid/paraId/textId attributes Word emits.
            var paragraph = new XElement(w + "p",
                new XAttribute(w14 + "paraId", $"{p:X8}"),
                new XAttribute(w14 + "textId", $"{p:X8}"),
                new XAttribute(w + "rsidR", $"{p:X8}"),
                new XAttribute(w + "rsidRDefault", $"{p:X8}"),
                new XAttribute(w + "rsidP", $"{p:X8}"),
                new XAttribute(w + "rsidRPr", $"{p:X8}"));
            for (var run = 0; run < 4; run++)
            {
                paragraph.Add(
                    new XElement(
                        w + "r",
                        new XAttribute(w + "rsidR", $"{p:X8}"),
                        new XElement(w + "t", $"Paragraph {p} run {run} text")));
            }

            body.Add(paragraph);
        }

        for (var d = 0; d < drawings; d++)
        {
            var randomId = (d * 31 + 7).ToString();
            var drawing = new XElement(w + "p",
                new XElement(w + "r",
                    new XElement(w + "drawing",
                        new XElement(wp + "inline",
                            new XElement(wp + "docPr",
                                new XAttribute("id", randomId),
                                new XAttribute("name", $"Picture {d}")),
                            new XElement(a + "graphic",
                                new XElement(a + "graphicData",
                                    new XAttribute("uri", "http://schemas.openxmlformats.org/drawingml/2006/picture"),
                                    new XElement(pic + "pic",
                                        new XElement(pic + "nvPicPr",
                                            new XElement(pic + "cNvPr",
                                                new XAttribute("id", randomId),
                                                new XAttribute("name", $"Picture {d}"))),
                                        new XElement(pic + "blipFill",
                                            new XElement(a + "blip",
                                                new XAttribute(r + "embed", $"rId{d + 10}"))))))))));
            body.Add(drawing);
        }

        for (var h = 0; h < hyperlinks; h++)
        {
            var link = new XElement(
                w + "p",
                new XElement(w + "hyperlink",
                    new XAttribute(r + "id", $"rId{h + 10000}"),
                    new XElement(
                        w + "r",
                        new XElement(w + "t", $"link {h}"))));
            body.Add(link);
        }

        body.Add(
            new XElement(
                w + "sectPr",
                new XAttribute(w + "rsidR", "00112233"),
                new XAttribute(w + "rsidSect", "44556677"),
                new XElement(
                    w + "pgSz",
                    new XAttribute(w + "w", "12240"),
                    new XAttribute(w + "h", "15840"))));

        var root = new XElement(w + "document",
            new XAttribute(XNamespace.Xmlns + "w", w.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "w14", w14.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "wp", wp.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "pic", pic.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "a", a.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "r", r.NamespaceName),
            body);
        return new(root);
    }

    public static Dictionary<string, string> BuildRIdMapping(int hyperlinks)
    {
        var mapping = new Dictionary<string, string>(hyperlinks);
        for (var h = 0; h < hyperlinks; h++)
        {
            mapping[$"rId{h + 10000}"] = $"DeterministicId{h + 1}";
        }

        // Also remap the drawing embeds so the worst case exercises both attribute paths.
        for (var d = 0; d < 200; d++)
        {
            mapping[$"rId{d + 10}"] = $"DeterministicId{hyperlinks + d + 1}";
        }

        return mapping;
    }

    public static XDocument BuildNumbering(int abstractNums, int namespaceDecls)
    {
        var root = new XElement(
            w + "numbering",
            new XAttribute(XNamespace.Xmlns + "w", w.NamespaceName));

        for (var i = 0; i < abstractNums; i++)
        {
            var abstractNum = new XElement(
                w + "abstractNum",
                new XAttribute(w + "abstractNumId", i.ToString()),
                new XElement(w + "nsid", new XAttribute(w + "val", $"{i:X8}")),
                new XElement(w + "multiLevelType", new XAttribute(w + "val", "hybridMultilevel")));

            // Add redundant namespace declarations to lots of descendants.
            for (var n = 0; n < namespaceDecls; n++)
            {
                abstractNum.Add(new XElement(w + "lvl",
                    new XAttribute(XNamespace.Xmlns + $"redundant{n}", w.NamespaceName),
                    new XAttribute(w + "ilvl", n.ToString())));
            }

            root.Add(abstractNum);
        }

        for (var i = 0; i < abstractNums; i++)
        {
            root.Add(new XElement(
                w + "num",
                new XAttribute(w + "numId", (i + 1).ToString()),
                new XElement(w + "abstractNumId", new XAttribute(w + "val", i.ToString()))));
        }

        return new(root);
    }

    public static byte[] BuildDocxZip(int paragraphs, int drawings, int hyperlinks)
    {
        var doc = BuildWordDocument(paragraphs, drawings, hyperlinks);
        var contentTypes = new XDocument(
            new XElement(XName.Get("Types", "http://schemas.openxmlformats.org/package/2006/content-types"),
                new XElement(XName.Get("Default", "http://schemas.openxmlformats.org/package/2006/content-types"),
                    new XAttribute("Extension", "xml"),
                    new XAttribute("ContentType", "application/xml")),
                new XElement(XName.Get("Default", "http://schemas.openxmlformats.org/package/2006/content-types"),
                    new XAttribute("Extension", "rels"),
                    new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                new XElement(XName.Get("Override", "http://schemas.openxmlformats.org/package/2006/content-types"),
                    new XAttribute("PartName", "/word/document.xml"),
                    new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"))));

        var rootRels = new XDocument(
            new XElement(XName.Get("Relationships", "http://schemas.openxmlformats.org/package/2006/relationships"),
                new XElement(XName.Get("Relationship", "http://schemas.openxmlformats.org/package/2006/relationships"),
                    new XAttribute("Id", "rId1"),
                    new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"),
                    new XAttribute("Target", "word/document.xml"))));

        var docRels = new XElement(XName.Get("Relationships", "http://schemas.openxmlformats.org/package/2006/relationships"));
        for (var h = 0; h < hyperlinks; h++)
        {
            docRels.Add(new XElement(XName.Get("Relationship", "http://schemas.openxmlformats.org/package/2006/relationships"),
                new XAttribute("Id", $"rId{h + 10000}"),
                new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink"),
                new XAttribute("Target", $"https://example.com/{h}"),
                new XAttribute("TargetMode", "External")));
        }

        using var stream = new MemoryStream();
        using (var zip = new Archive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(zip, "[Content_Types].xml", contentTypes);
            WriteEntry(zip, "_rels/.rels", rootRels);
            WriteEntry(zip, "word/_rels/document.xml.rels", new(docRels));
            WriteEntry(zip, "word/document.xml", doc);
        }

        return stream.ToArray();
    }

    static void WriteEntry(Archive zip, string name, XDocument xml)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Fastest);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        xml.Save(writer, SaveOptions.DisableFormatting);
    }

    // Builds a minimal PNG (signature + IHDR + IDAT + IEND) whose IDAT carries a
    // zlib-compressed payload of `rawBytes` bytes — that payload is what
    // PngNormalizer decompresses and rewrites as stored zlib blocks. Chunk CRCs
    // are left zero: the normalizer reads chunk lengths and re-derives the IDAT
    // CRC itself, so the input value is irrelevant for benchmarking it.
    public static byte[] BuildPng(int rawBytes)
    {
        var raw = new byte[rawBytes];
        for (var i = 0; i < raw.Length; i++)
        {
            raw[i] = (byte) (i * 31 + 7);
        }

        byte[] idat;
        using (var compressed = new MemoryStream())
        {
            using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
            {
                zlib.Write(raw, 0, raw.Length);
            }

            idat = compressed.ToArray();
        }

        using var png = new MemoryStream();
        ReadOnlySpan<byte> signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        png.Write(signature);
        WriteChunk(png, "IHDR"u8, new byte[13]);
        WriteChunk(png, "IDAT"u8, idat);
        WriteChunk(png, "IEND"u8, []);
        return png.ToArray();
    }

    static void WriteChunk(Stream png, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> header = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(header, data.Length);
        png.Write(header);
        png.Write(type);
        png.Write(data);

        // CRC placeholder (PngNormalizer does not validate input chunk CRCs).
        header.Clear();
        png.Write(header);
    }
}
