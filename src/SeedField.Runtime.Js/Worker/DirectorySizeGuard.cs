namespace SeedField.Runtime.Js.Worker;

/// <summary>
/// Bounded recursive directory size. Used by the parent as a disk watchdog for the
/// worker's unique temp directory; it stops counting once the limit is exceeded.
/// </summary>
public static class DirectorySizeGuard
{
    public static long ComputeBytes(string root, long stopAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        if (stopAt <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(stopAt));
        }

        if (!Directory.Exists(root))
        {
            return 0;
        }

        var pending = new Stack<string>();
        pending.Push(root);
        var total = 0L;

        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            if (IsReparsePoint(directory))
            {
                continue;
            }

            try
            {
                foreach (var file in Directory.EnumerateFiles(directory))
                {
                    if (IsReparsePoint(file))
                    {
                        continue;
                    }

                    try
                    {
                        total += new FileInfo(file).Length;
                    }
                    catch (IOException)
                    {
                        continue;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        continue;
                    }

                    if (total >= stopAt)
                    {
                        return total;
                    }
                }

                foreach (var child in Directory.EnumerateDirectories(directory))
                {
                    if (!IsReparsePoint(child))
                    {
                        pending.Push(child);
                    }
                }
            }
            catch (IOException)
            {
                // Directory disappeared or became inaccessible; best-effort.
            }
            catch (UnauthorizedAccessException)
            {
                // Best-effort.
            }
        }

        return total;
    }

    private static bool IsReparsePoint(string path)
    {
        try
        {
            return File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
