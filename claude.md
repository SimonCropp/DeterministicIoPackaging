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
- Register patchers in `DeterministicPackage.cs` patchers list
- **Order matters** - patchers run in sequence

Example patcher structure:
```csharp
class DocumentPatcher : IPatcher
{
    static XNamespace wp = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
    static XNamespace pic = "http://schemas.openxmlformats.org/drawingml/2006/picture";

    public bool IsMatch(Entry entry) =>
        entry.FullName is "word/document.xml";

    public void PatchXml(XDocument xml)
    {
        var root = xml.Root!;
        var elements = root.Descendants(wp + "docPr").ToList();
        // Normalize IDs...
    }
}
```

### Testing with Verify

Snapshot tests use the Verify library:

- `.verified.*` files are the baseline/expected output
- `.received.*` files are generated when tests fail (showing actual output)
- Update snapshots by moving `.received.*` files to `.verified.*`
- Framework-specific snapshots: `.DotNet9_0.verified.*`, `.Net4_8.verified.*`
- Generic snapshots: `.DotNet.verified.*`, `.Net.verified.*`

To accept new snapshots after intentional changes:
```bash
# Accept all new snapshots
find . -name "*.received.*" -exec sh -c 'mv "$1" "$(echo "$1" | sed 's/\.received\././')"' _ {} \;

# Or manually for specific tests
cp Tests.SomeTest.DotNet.DotNet9_0.received.xml Tests.SomeTest.DotNet.verified.xml
```

## Project Structure

- `src/DeterministicIoPackaging/` - Main library
  - `Patching/` - XML patchers for different file types
  - `DeterministicPackage.cs` - Entry point with patcher registration
- `src/Tests/` - Tests using Verify for snapshot testing
- `tools/` - Utility projects (e.g., CreateDocx for generating test files)
