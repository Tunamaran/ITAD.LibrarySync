namespace ITAD.LibrarySync.Core.Services;

/// <summary>
/// Expands environment variables in path strings and resolves wildcard directory
/// segments (e.g. <c>*</c> in <c>%LOCALAPPDATA%\Saber\RoadCraftGame\storage\steam\user\*\Main\save</c>)
/// against the local file system.
/// </summary>
public static class WildcardPathResolver
{
    public static (string ResolvedPath, bool Exists) Resolve(
        string rawPath,
        Func<string, string, string[]>? getDirectories = null,
        Func<string, bool>? directoryExists = null)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
            return (string.Empty, false);

        Func<string, string, string[]> getDirs = getDirectories ?? ((dir, pattern) =>
        {
            try
            {
                if (!Directory.Exists(dir))
                    return [];
                return Directory.GetDirectories(dir, pattern);
            }
            catch
            {
                return [];
            }
        });

        Func<string, bool> dirExists = directoryExists ?? (dir =>
        {
            try
            {
                return Directory.Exists(dir);
            }
            catch
            {
                return false;
            }
        });

        var expanded = Environment.ExpandEnvironmentVariables(rawPath.Trim()).TrimEnd('\\', '/');
        if (!expanded.Contains('*') && !expanded.Contains('?'))
        {
            return (expanded, dirExists(expanded));
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(expanded);
        }
        catch
        {
            return (expanded, false);
        }

        var root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrEmpty(root))
        {
            return (expanded, false);
        }

        var relative = fullPath[root.Length..];
        var segments = relative.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);

        var currentDirs = new List<string> { root.TrimEnd('\\', '/') + "\\" };

        foreach (var segment in segments)
        {
            var nextDirs = new List<string>();

            foreach (var dir in currentDirs)
            {
                if (segment.Contains('*') || segment.Contains('?'))
                {
                    var matches = getDirs(dir, segment);
                    // Order by most recently modified if possible
                    var ordered = matches.OrderByDescending(d =>
                    {
                        try { return Directory.GetLastWriteTimeUtc(d); }
                        catch { return DateTime.MinValue; }
                    });
                    nextDirs.AddRange(ordered);
                }
                else
                {
                    var combined = Path.Combine(dir, segment);
                    nextDirs.Add(combined);
                }
            }

            if (nextDirs.Count == 0)
            {
                return (expanded, false);
            }

            currentDirs = nextDirs;
        }

        foreach (var candidate in currentDirs)
        {
            if (dirExists(candidate))
            {
                return (candidate, true);
            }
        }

        return (currentDirs.FirstOrDefault() ?? expanded, false);
    }
}
