# <img src="/src/icon.png" height="30px"> DeterministicIoPackaging

[![Build status](https://ci.appveyor.com/api/projects/status/yw2qps5cxvxh850v?svg=true)](https://ci.appveyor.com/project/SimonCropp/deterministiciopackaging)
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
 * Duplicate each entry with a consistent compression
 * Omit `package/services/metadata/core-properties/*.psmdcp` entries
 * For the relationships entry `_rels/.rels`
   * Modify the Id of each relationship to be deterministic
   * Remove the relationship for the `.psmdcp` entry


## Usage


### Convert

<!-- snippet: ConvertAsync -->
<a id='snippet-ConvertAsync'></a>
```cs
using var sourceStream = File.OpenRead(packagePath);
await DeterministicPackage.ConvertAsync(sourceStream, targetStream);
```
<sup><a href='/src/Tests/Tests.cs#L105-L110' title='Snippet source file'>snippet source</a> | <a href='#snippet-ConvertAsync' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### ConvertAsync

<!-- snippet: ConvertAsync -->
<a id='snippet-ConvertAsync'></a>
```cs
using var sourceStream = File.OpenRead(packagePath);
await DeterministicPackage.ConvertAsync(sourceStream, targetStream);
```
<sup><a href='/src/Tests/Tests.cs#L105-L110' title='Snippet source file'>snippet source</a> | <a href='#snippet-ConvertAsync' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


## Icon

[Pi](https://thenounproject.com/icon/pi-2131020/) designed by [Zaidan](https://thenounproject.com/creator/mzaidanfiros/) from [The Noun Project](https://thenounproject.com).


