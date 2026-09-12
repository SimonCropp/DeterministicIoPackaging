# <img src="/src/icon.png" height="30px"> DeterministicIoPackaging

[![Build status](https://img.shields.io/appveyor/build/SimonCropp/deterministiciopackaging)](https://ci.appveyor.com/project/SimonCropp/deterministiciopackaging)
[![NuGet Status](https://img.shields.io/nuget/v/DeterministicIoPackaging.svg)](https://www.nuget.org/packages/DeterministicIoPackaging/)

Modify [System.IO.Packaging](https://learn.microsoft.com/en-us/dotnet/api/system.io.packaging) files to ensure they are deterministic. Helpful for testing, build reproducibility, security verification, and ensuring package integrity across different build environments.

Example file formats that leverage System.IO.Packaging

 * [.nupkg](https://learn.microsoft.com/en-us/nuget/)
 * Microsoft Office files

**See [Milestones](../../milestones?state=closed) for release notes.**


## NuGet

 * https://nuget.org/packages/DeterministicIoPackaging


## How it works

 * For an input package stream
 * Duplicate each entry with Deflate compression and consistent order
 * Omit `package/services/metadata/core-properties/*.psmdcp` entries
 * Omit `.signature.p7s` entries (NuGet package signatures are invalidated by the conversion since package contents are modified)
 * For all relationship entries (`.rels` files)
   * Modify the `Id` of each `Relationship` to be deterministic
   * Convert absolute `Target` paths to relative (e.g. `Target="/xl/workbook.xml"` becomes `Target="xl/workbook.xml"`)
   * Order `Relationship`s by `Type`
 * For the relationships entry `_rels/.rels`
   * Remove the `Relationship` for the `.psmdcp` entry
 * For the relationships entry `docProps/core.xml`
   * Remove the `creator`, `created`, `lastModifiedBy`, and `modified` elements


### Spreadsheet namespace validation

The conversion throws if any spreadsheetml XML entry (e.g. `xl/workbook.xml`, `xl/worksheets/sheet1.xml`) uses a prefixed default namespace such as `<x:worksheet xmlns:x="...">` instead of the unprefixed form `<worksheet xmlns="...">`. This is because tools like Microsoft Spreadsheet Compare cannot open files with prefixed spreadsheetml elements. The OpenXml SDK can produce this form — ensure source xlsx files use default namespace declarations.


### Binary output across .NET frameworks

Binary output may differ between .NET Framework (net48) and .NET (net10.0+) due to differences in Deflate compression implementations. The XML content within entries is identical — only the compressed bytes differ.

This applies to all package formats (xlsx, docx, nupkg, etc.). When snapshot-testing binary package output across multiple target frameworks using [Verify](https://github.com/VerifyTests/Verify), use `UniqueForRuntime` to generate framework-specific verified files:

```cs
await Verify(stream, extension: "xlsx")
    .UniqueForRuntime();
```

See [Verify Naming docs](https://github.com/VerifyTests/Verify/blob/main/docs/naming.md) for more details.


## Usage


### Convert

<!-- snippet: Convert -->
<a id='snippet-Convert'></a>
```cs
using var sourceStream = File.OpenRead(packagePath);
var target = DeterministicPackage.Convert(sourceStream);
```
<sup><a href='/src/Tests/Tests.cs#L268-L273' title='Snippet source file'>snippet source</a> | <a href='#snippet-Convert' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### ConvertAsync

<!-- snippet: ConvertAsync -->
<a id='snippet-ConvertAsync'></a>
```cs
using var sourceStream = File.OpenRead(packagePath);
var target = await DeterministicPackage.ConvertAsync(sourceStream);
```
<sup><a href='/src/Tests/Tests.cs#L315-L320' title='Snippet source file'>snippet source</a> | <a href='#snippet-ConvertAsync' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Reporting what changed

Overloads report what the conversion changed: entries it removed, entries whose content it patched, and whether the package had to be reordered.

<!-- snippet: ConvertChanges -->
<a id='snippet-ConvertChanges'></a>
```cs
using var sourceStream = File.OpenRead(packagePath);
var target = DeterministicPackage.Convert(sourceStream, out var changes);
foreach (var change in changes)
{
    Console.WriteLine($"{change.Kind} {change.Entry}");
}
```
<sup><a href='/src/Tests/Tests.cs#L280-L289' title='Snippet source file'>snippet source</a> | <a href='#snippet-ConvertChanges' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The async form returns the report beside the stream, since an async method cannot have an `out` parameter:

<!-- snippet: ConvertChangesAsync -->
<a id='snippet-ConvertChangesAsync'></a>
```cs
using var sourceStream = File.OpenRead(packagePath);
using var result = await DeterministicPackage.ConvertWithChangesAsync(sourceStream);
foreach (var change in result.Changes)
{
    Console.WriteLine($"{change.Kind} {change.Entry}");
}
```
<sup><a href='/src/Tests/Tests.cs#L296-L305' title='Snippet source file'>snippet source</a> | <a href='#snippet-ConvertChangesAsync' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Only differences are reported. The conversion restamps every timestamp, recompresses every entry and reserializes every XML part regardless, but those happen to every input alike and produce the same bytes on a second conversion — so reporting them would say nothing about why a given package was not already deterministic. An already deterministic package reports nothing at all.


## Icon

[Pi](https://thenounproject.com/icon/pi-2131020/) designed by [Zaidan](https://thenounproject.com/creator/mzaidanfiros/) from [The Noun Project](https://thenounproject.com).


