namespace Ensync.Core.Extensions;

public static class PathHelper
{
	public static string Resolve(string basePath, string pathFragment) =>
		pathFragment.StartsWith(".\\") ? Path.Combine(basePath, pathFragment.Substring(2)) : pathFragment;
}
