using System.Text;
using System.Threading;

namespace FolderHoverPreview;

internal static class PreviewConfig
{
    private const string ConfigMutexName = @"Local\ShapkaFolderHoverPreviewConfigCSharp";
    private const string ConfigTitle = "# FolderHoverPreview — список включённых папок";
    private static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;
    private static readonly string[] ConfigHeader =
    {
        ConfigTitle,
        "#",
        "# Каждая строка без # в начале — полный путь к одной папке.",
        "# Папки, перечисленные ниже, включены; все остальные — выключены.",
        "#",
        "# Как изменить список:",
        "#   • Чтобы включить папку — добавьте её полный путь отдельной строкой.",
        "#   • Чтобы выключить — удалите строку или поставьте # в её начале.",
        "#   • Пустые строки и строки, начинающиеся с #, игнорируются.",
        "#",
        "# Поддерживаются папки на Рабочем столе пользователя и на общем",
        "# Рабочем столе Windows. Для быстрого переключения используйте пункт",
        "# «Превью при наведении» в контекстном меню папки в Проводнике.",
        "#",
        "# ── ВКЛЮЧЁННЫЕ ПАПКИ ──",
        "# Укажите ниже по одному полному пути к папке на строку:"
    };

    public static string ConfigPath
    {
        get
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FolderHoverPreview");
            return Path.Combine(directory, "enabled-folders.txt");
        }
    }

    public static void EnsureCreated()
    {
        WithConfigLock(() =>
        {
            if (!File.Exists(ConfigPath))
            {
                WritePaths(Array.Empty<string>());
                return;
            }

            EnsureDocumented();
        });
    }

    public static Dictionary<string, string> LoadEnabledDesktopFolders()
    {
        EnsureCreated();
        var enabled = ReadPaths();
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var commonDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in enabled)
        {
            try
            {
                var fullPath = Path.GetFullPath(path);
                var parent = Path.GetDirectoryName(fullPath);
                if (!PathComparer.Equals(parent, desktop) && !PathComparer.Equals(parent, commonDesktop))
                    continue;

                var name = Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (!string.IsNullOrEmpty(name))
                    result[name] = fullPath;
            }
            catch (ArgumentException) { }
            catch (NotSupportedException) { }
            catch (PathTooLongException) { }
        }

        return result;
    }

    public static void Toggle(string path)
    {
        if (!Directory.Exists(path))
            return;

        EnsureCreated();
        WithConfigLock(() =>
        {
            var paths = ReadPaths();
            var fullPath = Path.GetFullPath(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var removed = paths.RemoveWhere(item => PathComparer.Equals(item, fullPath)) > 0;
            if (!removed)
                paths.Add(fullPath);
            WritePaths(paths);
        });
    }

    public static string? FindDesktopFolderPath(string folderName)
    {
        // The personal Desktop takes precedence for duplicate folder names.
        foreach (var desktop in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                     Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
                 })
        {
            if (string.IsNullOrWhiteSpace(desktop))
                continue;

            var path = Path.Combine(desktop, folderName);
            if (Directory.Exists(path))
                return path;
        }

        return null;
    }

    private static HashSet<string> ReadPaths()
    {
        EnsureDirectory();
        if (!File.Exists(ConfigPath))
            return new HashSet<string>(PathComparer);

        return File.ReadLines(ConfigPath, Encoding.UTF8)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToHashSet(PathComparer);
    }

    private static void WritePaths(IEnumerable<string> paths)
    {
        var lines = ConfigHeader
            .Concat(paths.OrderBy(path => path, PathComparer));
        WriteConfigContents(string.Join(Environment.NewLine, lines) + Environment.NewLine);
    }

    private static void EnsureDocumented()
    {
        var contents = File.ReadAllText(ConfigPath, Encoding.UTF8).TrimStart('\uFEFF');
        if (contents.Contains(ConfigTitle, StringComparison.Ordinal))
            return;

        var header = string.Join(Environment.NewLine, ConfigHeader);
        var separator = contents.Length == 0 ? string.Empty : Environment.NewLine + Environment.NewLine;
        WriteConfigContents(header + Environment.NewLine + separator + contents);
    }

    private static void WriteConfigContents(string contents)
    {
        EnsureDirectory();
        var temporaryPath = ConfigPath + "." + Environment.ProcessId + ".tmp";
        try
        {
            File.WriteAllText(
                temporaryPath,
                contents,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temporaryPath, ConfigPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static void EnsureDirectory()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
    }

    private static void WithConfigLock(Action action)
    {
        using var mutex = new Mutex(false, ConfigMutexName);
        var acquired = false;
        try
        {
            try
            {
                acquired = mutex.WaitOne(TimeSpan.FromSeconds(3));
            }
            catch (AbandonedMutexException)
            {
                acquired = true;
            }

            if (!acquired)
                throw new TimeoutException("Не удалось получить блокировку файла настроек.");

            action();
        }
        finally
        {
            if (acquired)
                mutex.ReleaseMutex();
        }
    }
}
