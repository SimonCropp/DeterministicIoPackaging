# Claude Code Reference

This document contains important information about the codebase for future reference.

## Office Open XML (OOXML) Structure

### Word Document IDs

Word documents (.docx) contain multiple types of non-deterministic IDs that need normalization:

1. **Drawing/Picture IDs** (in word/document.xml):
   - `wp:docPr id` - WordprocessingDrawing document properties
   - `pic:cNvPr id` - Picture non-visual properties
   - Both IDs typically have the same value and appear together in drawing elements
   - Key namespaces:
     - `wp:` = `http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing`
     - `pic:` = `http://schemas.openxmlformats.org/drawingml/2006/picture`
     - `a:` = `http://schemas.openxmlformats.org/drawingml/2006/main`

2. **Numbering IDs** (in word/numbering.xml):
   - `w:nsid` - Numbering session ID (should be removed entirely)
   - `w:abstractNumId`, `w:numId` - should be normalized

### Debugging OOXML Files

To examine the internal XML structure of Office files:

```bash
# Extract specific XML file from a .docx/.xlsx
unzip -p file.docx word/document.xml > doc.xml

# Format for readability by adding line breaks between elements
cat doc.xml | tr '><' '>\n<' | grep -A5 -B5 'search-term'

# View structure of entire archive
unzip -l file.docx
```

### Patcher Pattern

The codebase uses a patcher pattern for normalizing OOXML content:

- Each patcher implements `IPatcher` interface
- `IsMatch(Entry entry)` - determines which files the patcher applies to (e.g., "word/document.xml")
- `PatchXml(XDocument xml)` - modifies the XML in-place
- Register patchers via `CreatePatchers()` factory in `DeterministicPackage.cs` (fresh instance per conversion)
- **Order matters** - relationship patchers must run before their content patchers (e.g., `WorkbookRelationshipPatcher` before `WorkbookPatcher`)

#### Paired Patchers

Some patchers work in pairs: a relationship patcher renumbers IDs in `.rels` files and stores the mapping, then a content patcher remaps `r:id` references in the corresponding XML:

- `WorkbookRelationshipPatcher` → `WorkbookPatcher` (xl/_rels/workbook.xml.rels → xl/workbook.xml)
- `DocumentRelationshipPatcher` → `DocumentPatcher` (word/_rels/document.xml.rels → word/document.xml)

The content patcher receives the relationship patcher via constructor injection.

#### Relationship ID Renumbering

`RelationshipRenumber` (in IPatcher.cs) provides shared helpers:
- `RenumberAndSort(XDocument)` — sorts relationships by Type+Target, renumbers to `DeterministicId{n}`, returns old→new mapping
- `RemapIds(XDocument, mapping)` — replaces `r:id` attribute values in content XML using the mapping

#### Content Types Sorting

`ContentTypesPatcher` sorts `[Content_Types].xml` elements by local name, then Extension, then PartName to ensure deterministic order across frameworks.

### ZIP Output

- ZIP entries use Deflate compression via `ZipArchive`. Binary output may differ between net48 and net10.0+ due to Deflate implementation differences, but XML content is identical
- Entries are sorted by `FullName` using `StringComparer.Ordinal`
- Binary snapshot tests use `UniqueForRuntime` to allow framework-specific verified files
- `Convert(Stream source)` / `ConvertAsync(Stream source, Cancel)` are the only entry points and always return a fresh `MemoryStream`. Normalization is not a streaming operation (entries are reordered, every part is rewritten, the central directory is patched afterward, and `ZipArchive` read-mode needs a seekable source), so there is deliberately no `Convert(source, target)` overload — the result is always fully materialized in a buffer. Nested-zip recursion calls `Convert` then `CopyTo`s into the outer entry stream.
- `ZipPlatformNormalizer` makes output **OS-independent**: `ZipArchive` stamps the host OS into each central-directory record (the "version made by" high byte is 0 on Windows, 3 on Unix; Unix can also leak file-mode bits into the external-attributes field). The normalizer rewrites the host byte to 0 (MS-DOS/FAT) and clears external attributes on every record, so identical bytes are produced on Windows/macOS/Linux. The only remaining cross-environment difference is the Deflate stream (cross-runtime, not cross-OS). It patches the central directory **in place** via `MemoryStream.GetBuffer()` — no re-zip, no extra copy.
- `PngNormalizer` writes raw zlib stored blocks (CMF+FLG + DEFLATE stored blocks + Adler-32) instead of using `ZLibStream`, which produces different output on net48 vs net10.0

Example patcher structure:
```csharp
class DocumentPatcher(DocumentRelationshipPatcher relsPatcher) : IPatcher
{
    static XNamespace wp = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
    static XNamespace pic = "http://schemas.openxmlformats.org/drawingml/2006/picture";

    public bool IsMatch(Entry entry) =>
        entry.FullName is "word/document.xml";

    public void PatchXml(XDocument xml)
    {
        // Normalize drawing IDs...
        // Then remap relationship IDs
        if (relsPatcher.IdMapping.Count > 0)
        {
            RelationshipRenumber.RemapIds(xml, relsPatcher.IdMapping);
        }
    }
}
```

### Testing with Verify

Snapshot tests use the Verify library:

- `.verified.*` files are the baseline/expected output
- `.received.*` files are generated when tests fail (showing actual output)
- Framework-specific snapshots: `.DotNet9_0.verified.*`, `.Net4_8.verified.*`
- Generic snapshots: `.DotNet.verified.*`, `.Net.verified.*`

**The received and verified names are not the same.** The tests multi-target and use `UniqueForRuntime()`, so a received file carries the runtime *and version* while its verified file carries only the runtime. Accepting is a mapping, not a rename of `.received.` to `.verified.`:

```
Tests.SomeTest.DotNet9_0.received.xml   ->   Tests.SomeTest.DotNet.verified.xml
Tests.SomeTest.Net4_8.received.xml      ->   Tests.SomeTest.Net.verified.xml
```

Renaming in place writes a second snapshot corpus that no test reads, leaving the real baseline stale and the suite still failing. So do not bulk rename with `find`/`sed`.

To accept, either take the destination from the `Verified:` line of the failure message, or use the tool, which does that mapping:

```bash
dotnet verify accept
```

Run it from the repository root. It pairs each received file with the verified file beside it, which covers updating an existing baseline. A brand new snapshot has no baseline to pair with, so add both the `.DotNet.` and `.Net.` files by hand.

## Project Structure

- `src/DeterministicIoPackaging/` - Main library
  - `Patching/` - XML patchers for different file types
  - `DeterministicPackage.cs` - Entry point with patcher registration
- `src/Tests/` - Tests using Verify for snapshot testing
- `tools/` - Utility projects (e.g., CreateDocx for generating test files)
