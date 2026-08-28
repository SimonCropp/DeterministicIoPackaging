// A single file to process, and where its result is written. Target equals Source for an in place run.
record FileJob(string Source, string Target)
{
    public bool IsInPlace => string.Equals(Source, Target, PathComparison.Value);
}
