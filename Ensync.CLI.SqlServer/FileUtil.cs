namespace Ensync.CLI;

internal static class FileUtil
{
    internal static IEnumerable<string> FindWhere(string path, string searchPattern, Func<string, bool>? predicate = null)
    {
        var files = Directory.GetFiles(path, searchPattern, SearchOption.AllDirectories);
        return (predicate is not null) ? files.Where(predicate) : files;
    }
}
