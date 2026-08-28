// Expands the input path parameter into the set of files to process, pairing each with its output
// path. Both the file and the directory forms are supported, and an omitted target means in place.
static class FileResolver
{
    public static IReadOnlyList<FileJob> Resolve(string input, string? target, IReadOnlyList<string> patterns, bool recursive)
    {
        var fullInput = Path.GetFullPath(input);

        if (File.Exists(fullInput))
        {
            return [new(fullInput, ResolveFileTarget(fullInput, target))];
        }

        if (Directory.Exists(fullInput))
        {
            return ResolveDirectory(fullInput, target, patterns, recursive);
        }

        throw new CommandException($"Path not found: {input}");
    }

    // A target that names an existing directory, or is written with a trailing separator, keeps the
    // source file name. Anything else is the output file path itself.
    static string ResolveFileTarget(string source, string? target)
    {
        if (target == null)
        {
            return source;
        }

        if (Directory.Exists(target) ||
            EndsWithSeparator(target))
        {
            return Path.Combine(Path.GetFullPath(target), Path.GetFileName(source));
        }

        return Path.GetFullPath(target);
    }

    static IReadOnlyList<FileJob> ResolveDirectory(string directory, string? target, IReadOnlyList<string> patterns, bool recursive)
    {
        var fullTarget = target == null ? null : Path.GetFullPath(target);
        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        // Sorted so the order of the run does not depend on the order of the patterns or on the
        // order the file system happens to enumerate in, and to drop duplicates when patterns overlap.
        var sources = new SortedSet<string>(PathComparison.Comparer);
        foreach (var pattern in patterns)
        {
            foreach (var file in Directory.EnumerateFiles(directory, pattern, searchOption))
            {
                if (!MatchesExtension(pattern, file))
                {
                    continue;
                }

                // A target nested inside the input directory would otherwise feed its own output
                // back in on a recursive run.
                if (fullTarget != null &&
                    IsUnder(fullTarget, file))
                {
                    continue;
                }

                sources.Add(file);
            }
        }

        var jobs = new List<FileJob>(sources.Count);
        foreach (var source in sources)
        {
            if (fullTarget == null)
            {
                jobs.Add(new(source, source));
                continue;
            }

            jobs.Add(new(source, Path.Combine(fullTarget, Path.GetRelativePath(directory, source))));
        }

        return jobs;
    }

    // Windows keeps legacy 8.3 name matching, so a "*.doc" search pattern also matches "report.docx".
    // Re-check the extension for the plain "*.extension" pattern shape.
    static bool MatchesExtension(string pattern, string file)
    {
        if (!pattern.StartsWith("*.") ||
            pattern.IndexOf('*', 2) != -1 ||
            pattern.Contains('?'))
        {
            return true;
        }

        return string.Equals(Path.GetExtension(file), pattern[1..], StringComparison.OrdinalIgnoreCase);
    }

    static bool IsUnder(string directory, string path) =>
        path.StartsWith(directory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, PathComparison.Value);

    static bool EndsWithSeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar) ||
        path.EndsWith(Path.AltDirectorySeparatorChar);
}
