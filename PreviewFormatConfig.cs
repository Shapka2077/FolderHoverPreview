using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace FolderHoverPreview;

internal sealed record PreviewFormatOption(string Label, string Extension, string Group);

internal sealed class PreviewFormatPreferences
{
    public List<string> EnabledExtensions { get; set; } = [];
    public List<string> CustomExtensions { get; set; } = [];
    public bool UseCustomExtensions { get; set; }

    public HashSet<string> GetEnabledExtensionSet()
    {
        var result = new HashSet<string>(EnabledExtensions, StringComparer.OrdinalIgnoreCase);
        if (UseCustomExtensions)
            result.UnionWith(CustomExtensions);
        return result;
    }
}

internal static class PreviewFormatConfig
{
    private const string SettingsMutexName = @"Local\ShapkaFolderHoverPreviewFormatsCSharp";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static IReadOnlyList<PreviewFormatOption> CommonFormats { get; } =
    [
        new("Ярлыки Windows (.lnk)", ".lnk", "Ярлыки и программы"),
        new("Интернет-ссылки (.url)", ".url", "Ярлыки и программы"),
        new("Программы (.exe)", ".exe", "Ярлыки и программы"),
        new("Установщики (.msi)", ".msi", "Ярлыки и программы"),
        new("Сценарии BAT (.bat)", ".bat", "Сценарии"),
        new("Командные файлы (.cmd)", ".cmd", "Сценарии"),
        new("PowerShell (.ps1)", ".ps1", "Сценарии"),
        new("PDF (.pdf)", ".pdf", "Документы"),
        new("Текст (.txt)", ".txt", "Документы"),
        new("Markdown (.md)", ".md", "Документы"),
        new("Word (.docx)", ".docx", "Документы"),
        new("Excel (.xlsx)", ".xlsx", "Документы"),
        new("CSV (.csv)", ".csv", "Документы"),
        new("PowerPoint (.pptx)", ".pptx", "Документы"),
        new("PNG (.png)", ".png", "Изображения"),
        new("JPEG (.jpg)", ".jpg", "Изображения"),
        new("JPEG (.jpeg)", ".jpeg", "Изображения"),
        new("GIF (.gif)", ".gif", "Изображения"),
        new("MP3 (.mp3)", ".mp3", "Медиа и архивы"),
        new("MP4 (.mp4)", ".mp4", "Медиа и архивы"),
        new("ZIP (.zip)", ".zip", "Медиа и архивы"),
        new("7-Zip (.7z)", ".7z", "Медиа и архивы"),
        new("RAR (.rar)", ".rar", "Медиа и архивы")
    ];

    public static string SettingsPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FolderHoverPreview",
            "preview-formats.json");

    public static PreviewFormatPreferences Load()
    {
        if (!File.Exists(SettingsPath))
            return DefaultPreferences();

        try
        {
            var preferences = JsonSerializer.Deserialize<PreviewFormatPreferences>(
                File.ReadAllText(SettingsPath),
                JsonOptions);
            return Normalize(preferences ?? DefaultPreferences());
        }
        catch (JsonException) { return DefaultPreferences(); }
        catch (IOException) { return DefaultPreferences(); }
        catch (UnauthorizedAccessException) { return DefaultPreferences(); }
    }

    public static void Save(PreviewFormatPreferences preferences)
    {
        var normalized = Normalize(preferences);
        var directory = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(directory);

        using var mutex = new Mutex(false, SettingsMutexName);
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
                throw new TimeoutException("Не удалось заблокировать настройки форматов.");

            var temporaryPath = SettingsPath + "." + Environment.ProcessId + ".tmp";
            try
            {
                File.WriteAllText(temporaryPath, JsonSerializer.Serialize(normalized, JsonOptions));
                File.Move(temporaryPath, SettingsPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }
        finally
        {
            if (acquired)
                mutex.ReleaseMutex();
        }
    }

    public static PreviewFormatPreferences Normalize(PreviewFormatPreferences preferences)
    {
        var common = new HashSet<string>(
            CommonFormats.Select(option => option.Extension),
            StringComparer.OrdinalIgnoreCase);

        return new PreviewFormatPreferences
        {
            EnabledExtensions = NormalizeExtensions(preferences.EnabledExtensions)
                .Where(common.Contains)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            CustomExtensions = NormalizeExtensions(preferences.CustomExtensions)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            UseCustomExtensions = preferences.UseCustomExtensions
        };
    }

    public static List<string> ParseCustomExtensions(string input)
    {
        var extensions = new List<string>();
        foreach (var token in input.Split(
                     [',', ';', '\r', '\n', '\t', ' '],
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var extension = NormalizeExtension(token);
            if (extension is null)
                throw new FormatException($"«{token}» — некорректное расширение. Пример: .json");
            extensions.Add(extension);
        }

        return extensions
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static PreviewFormatPreferences DefaultPreferences() => new()
    {
        EnabledExtensions = [".lnk", ".url", ".exe"]
    };

    private static IEnumerable<string> NormalizeExtensions(IEnumerable<string>? extensions)
    {
        if (extensions is null)
            yield break;

        foreach (var rawExtension in extensions)
        {
            var extension = NormalizeExtension(rawExtension);
            if (extension is not null)
                yield return extension;
        }
    }

    private static string? NormalizeExtension(string? rawExtension)
    {
        if (string.IsNullOrWhiteSpace(rawExtension))
            return null;

        var extension = rawExtension.Trim();
        if (extension.StartsWith("*.", StringComparison.Ordinal))
            extension = extension[1..];
        if (!extension.StartsWith(".", StringComparison.Ordinal))
            extension = "." + extension;

        if (extension.Length < 2
            || extension.AsSpan(1).IndexOfAnyExceptLettersDigitsAndAllowedSymbols() >= 0)
        {
            return null;
        }

        return extension.ToLowerInvariant();
    }

    private static int IndexOfAnyExceptLettersDigitsAndAllowedSymbols(this ReadOnlySpan<char> value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (!char.IsLetterOrDigit(character) && character is not ('_' or '-' or '+'))
                return index;
        }

        return -1;
    }
}
