[Command(
    Description = "Rewrites a System.IO.Packaging file so the same source package always produces byte-identical output.")]
public partial class ConvertCommand : ICommand
{
    // Every System.IO.Packaging format the library is known to handle: NuGet packages, the Office
    // Open XML documents, and the VSIX container.
    static string[] defaultPatterns =
    [
        "*.nupkg",
        "*.snupkg",
        "*.vsix",
        "*.docx",
        "*.docm",
        "*.dotx",
        "*.xlsx",
        "*.xlsm",
        "*.xltx",
        "*.pptx",
        "*.pptm",
        "*.potx"
    ];

    [CommandParameter(
        0,
        Name = "path",
        Description = "Package file, or directory containing packages, to convert. Converted in place unless --target is used.")]
    public required string Input { get; set; }

    [CommandOption(
        "target",
        't',
        Description = "Write results here instead of modifying the input in place. An output file path when the input is a file, otherwise a directory mirroring the input tree.")]
    public string? Target { get; set; }

    [CommandOption(
        "pattern",
        'p',
        Description = "Search patterns applied when the input is a directory. Defaults to every known package extension.")]
    public string[] Patterns { get; set; } = defaultPatterns;

    [CommandOption(
        "recursive",
        'r',
        Description = "Recurse into subdirectories when the input is a directory.")]
    public bool Recursive { get; set; }

    [CommandOption(
        "check",
        Description = "Report which packages are not already deterministic without writing anything. Exits with code 1 if any are found.")]
    public bool Check { get; set; }

    [CommandOption(
        "continue-on-error",
        Description = "Keep processing the remaining files after a failure, then exit with code 1.")]
    public bool ContinueOnError { get; set; }

    [CommandOption(
        "quiet",
        'q',
        Description = "Suppress per file and summary output. Errors are still written.")]
    public bool Quiet { get; set; }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        if (Check &&
            Target != null)
        {
            throw new CommandException("--check does not write anything, so it cannot be combined with --target.");
        }

        if (Patterns.Length == 0)
        {
            throw new CommandException("--pattern requires at least one value.");
        }

        var jobs = FileResolver.Resolve(Input, Target, Patterns, Recursive);
        if (jobs.Count == 0)
        {
            throw new CommandException($"No files matching {string.Join(", ", Patterns)} found in: {Input}");
        }

        var cancel = console.RegisterCancellationHandler();
        var changed = 0;
        var failed = 0;

        foreach (var job in jobs)
        {
            try
            {
                if (await Handle(console, job, cancel))
                {
                    changed++;
                }
            }
            catch (Exception exception)
                when (exception is not OperationCanceledException)
            {
                if (!ContinueOnError)
                {
                    throw new CommandException($"{Relative(job.Source)}: {exception.Message}", innerException: exception);
                }

                failed++;
                await console.Error.WriteLineAsync($"failed: {Relative(job.Source)}: {exception.Message}");
            }
        }

        await WriteSummary(console, jobs.Count, changed, failed);
    }

    // Returns whether converting altered the package.
    async Task<bool> Handle(IConsole console, FileJob job, Cancel cancel)
    {
        var source = await File.ReadAllBytesAsync(job.Source, cancel);

        // Read fully into memory first: an in place run overwrites the file the conversion read from.
        using var sourceStream = new MemoryStream(source, writable: false);
        using var targetStream = await DeterministicPackage.ConvertAsync(sourceStream, cancel);

        var converted = targetStream.ToArray();
        var isChanged = !converted.AsSpan().SequenceEqual(source);

        if (Check)
        {
            if (isChanged)
            {
                await Write(console, $"not deterministic: {Relative(job.Source)}");
            }

            return isChanged;
        }

        // An unchanged package is left alone on an in place run rather than rewritten with the same
        // bytes, so its timestamp is not disturbed. A separate target always has to be written.
        if (isChanged ||
            !job.IsInPlace)
        {
            var directory = Path.GetDirectoryName(job.Target);
            if (directory != null)
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllBytesAsync(job.Target, converted, cancel);
        }

        var status = isChanged ? "converted" : "unchanged";
        if (job.IsInPlace)
        {
            await Write(console, $"{status}: {Relative(job.Source)}");
        }
        else
        {
            await Write(console, $"{status}: {Relative(job.Source)} -> {Relative(job.Target)}");
        }

        return isChanged;
    }

    async Task WriteSummary(IConsole console, int total, int changed, int failed)
    {
        if (Check)
        {
            if (changed > 0 ||
                failed > 0)
            {
                throw new CommandException($"{Count(total)} checked, {changed} not deterministic{(failed > 0 ? $", {failed} failed" : null)}.");
            }

            await Write(console, $"{Count(total)} checked, all deterministic.");
            return;
        }

        await Write(console, $"{Count(total)} processed, {changed} converted.");

        if (failed > 0)
        {
            throw new CommandException($"{Count(failed)} failed.");
        }
    }

    Task Write(IConsole console, string message)
    {
        if (Quiet)
        {
            return Task.CompletedTask;
        }

        return console.Output.WriteLineAsync(message);
    }

    static string Count(int value) => value == 1 ? "1 file" : $"{value} files";

    static string Relative(string path) => Path.GetRelativePath(Directory.GetCurrentDirectory(), path);
}
