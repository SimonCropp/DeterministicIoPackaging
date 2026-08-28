// File system case sensitivity: Windows and macOS treat paths case insensitively, Linux does not.
static class PathComparison
{
    public static StringComparison Value { get; } =
        OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    public static StringComparer Comparer { get; } =
        OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
}
