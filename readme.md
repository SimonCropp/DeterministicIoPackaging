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
<sup><a href='/src/Tests/Tests.cs#L283-L288' title='Snippet source file'>snippet source</a> | <a href='#snippet-ConvertAsync' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


## CLI tool

A [dotnet tool](https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools) that applies the same conversion to files on disk.

 * https://nuget.org/packages/DeterministicIoPackaging.Tool

```
dotnet tool install -g DeterministicIoPackaging.Tool
```


### Usage

```
detpackage <path> [options]
```

`path` is a package file, or a directory containing packages. It is converted in place unless `--target` is used.

 * `-t|--target` Write results here instead of modifying the input in place. An output file path when the input is a file, otherwise a directory mirroring the input tree.
 * `-p|--pattern` Search patterns applied when the input is a directory. Defaults to every known package extension: `*.nupkg`, `*.snupkg`, `*.vsix`, `*.docx`, `*.docm`, `*.dotx`, `*.xlsx`, `*.xlsm`, `*.xltx`, `*.pptx`, `*.pptm`, `*.potx`. Repeat the option for multiple patterns.
 * `-r|--recursive` Recurse into subdirectories when the input is a directory.
 * `--check` Report which packages are not already deterministic without writing anything. Exits with code 1 if any are found.
 * `--continue-on-error` Keep processing the remaining files after a failure, then exit with code 1.
 * `-q|--quiet` Suppress per file and summary output. Errors are still written.

A package that is already deterministic is left untouched, so an in place run does not disturb its timestamp.


### Examples

Convert one package in place:

```
detpackage MyPackage.1.0.0.nupkg
```

Convert a tree into a separate output directory:

```
detpackage ./input -r --target ./output
```

Fail a build when any package is not deterministic:

```
detpackage ./artifacts -r --check
```


## Icon

[Pi](https://thenounproject.com/icon/pi-2131020/) designed by [Zaidan](https://thenounproject.com/creator/mzaidanfiros/) from [The Noun Project](https://thenounproject.com).


