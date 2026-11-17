using System.Text.Json;
using System.Threading;
using System.Windows.Media;

namespace FolderHoverPreview;

internal sealed class PreviewThemePreferences
{
    public string PresetId { get; set; } = "graphite";
    public string WindowBackground { get; set; } = "#202124";
    public string WindowBorder { get; set; } = "#505050";
    public string ShadowColor { get; set; } = "#000000";
    public string HeadingText { get; set; } = "#FFFFFF";
    public string EmptyText { get; set; } = "#CCCCCC";
    public string TileBackground { get; set; } = "#202124";
    public string TileHoverBackground { get; set; } = "#2A2E34";
    public string TilePressedBackground { get; set; } = "#313740";
    public string TileText { get; set; } = "#FFFFFF";
    public string AccentText { get; set; } = "#FFD166";
    public double WindowOpacity { get; set; } = 1.0;
    public double WindowPadding { get; set; } = 15;
    public double WindowCornerRadius { get; set; } = 8;
    public double BorderThickness { get; set; } = 1;
    public double TileWidth { get; set; } = 108;
    public double TileHeight { get; set; } = 72;
    public double TileCornerRadius { get; set; } = 6;
    public double TileSpacing { get; set; } = 8;
    public double IconSize { get; set; } = 38;
    public double HeadingFontSize { get; set; } = 15;
    public double TileFontSize { get; set; } = 12;
    public double EmptyFontSize { get; set; } = 13;
    public string FontFamily { get; set; } = "Segoe UI";
    public int MaxColumns { get; set; } = 4;
    public bool ShadowEnabled { get; set; }
    public double ShadowBlur { get; set; } = 18;
    public double ShadowOpacity { get; set; } = 0.35;

    public PreviewThemePreferences Clone() => (PreviewThemePreferences)MemberwiseClone();
}

internal sealed record PreviewThemePreset(string Id, string Name, PreviewThemePreferences Theme);

internal static class PreviewThemeConfig
{
    private const string SettingsMutexName = @"Local\ShapkaFolderHoverPreviewThemeCSharp";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string SettingsPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FolderHoverPreview",
            "preview-theme.json");

    public static IReadOnlyList<PreviewThemePreset> Presets { get; } =
    [
        new("graphite", "Графит", new PreviewThemePreferences()),
        new("midnight", "Полночь", new PreviewThemePreferences
        {
            PresetId = "midnight",
            WindowBackground = "#121621",
            WindowBorder = "#343B4D",
            HeadingText = "#F4F6FF",
            EmptyText = "#ADB6CB",
            TileBackground = "#191F2D",
            TileHoverBackground = "#242E42",
            TilePressedBackground = "#303E59",
            TileText = "#F1F4FC",
            AccentText = "#A994FF",
            ShadowEnabled = true
        }),
        new("ocean", "Океан", new PreviewThemePreferences
        {
            PresetId = "ocean",
            WindowBackground = "#122033",
            WindowBorder = "#355875",
            HeadingText = "#EAF7FF",
            EmptyText = "#B5CDDF",
            TileBackground = "#192B40",
            TileHoverBackground = "#21405B",
            TilePressedBackground = "#2B5475",
            TileText = "#F0F9FF",
            AccentText = "#62D5F2",
            ShadowEnabled = true
        }),
        new("forest", "Лес", new PreviewThemePreferences
        {
            PresetId = "forest",
            WindowBackground = "#17211D",
            WindowBorder = "#3D5749",
            HeadingText = "#F1FFF5",
            EmptyText = "#B9D0C0",
            TileBackground = "#1D2B24",
            TileHoverBackground = "#294033",
            TilePressedBackground = "#355844",
            TileText = "#F1FFF5",
            AccentText = "#9BE7B0",
            ShadowEnabled = true
        }),
        new("rose", "Роза", new PreviewThemePreferences
        {
            PresetId = "rose",
            WindowBackground = "#281D25",
            WindowBorder = "#674658",
            HeadingText = "#FFF4F8",
            EmptyText = "#D8BFCB",
            TileBackground = "#35232E",
            TileHoverBackground = "#4A2D3D",
            TilePressedBackground = "#603A50",
            TileText = "#FFF4F8",
            AccentText = "#FF9BC2",
            ShadowEnabled = true
        }),
        new("light", "Светлая", new PreviewThemePreferences
        {
            PresetId = "light",
            WindowBackground = "#F2F4F8",
            WindowBorder = "#C7CDD8",
            HeadingText = "#202532",
            EmptyText = "#626B78",
            TileBackground = "#FFFFFF",
            TileHoverBackground = "#E7EDF6",
            TilePressedBackground = "#D7E1EF",
            TileText = "#232A35",
            AccentText = "#1769AA",
            ShadowEnabled = true,
            ShadowOpacity = 0.2
        }),
        new("custom", "Пользовательская", new PreviewThemePreferences { PresetId = "custom" })
    ];

    public static PreviewThemePreferences Default => Presets[0].Theme.Clone();

    public static PreviewThemePreferences Load()
    {
        if (!File.Exists(SettingsPath))
            return Default;

        try
        {
            var theme = JsonSerializer.Deserialize<PreviewThemePreferences>(
                File.ReadAllText(SettingsPath),
                JsonOptions);
            return Normalize(theme ?? Default);
        }
        catch (JsonException) { return Default; }
        catch (IOException) { return Default; }
        catch (UnauthorizedAccessException) { return Default; }
    }

    public static void Save(PreviewThemePreferences theme)
    {
        var normalized = Normalize(theme);
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
                throw new TimeoutException("Не удалось заблокировать настройки оформления.");

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

    public static PreviewThemePreferences Normalize(PreviewThemePreferences theme)
    {
        var normalized = theme.Clone();
        if (string.IsNullOrEmpty(normalized.PresetId)
            || !Presets.Any(preset => preset.Id.Equals(normalized.PresetId, StringComparison.Ordinal)))
            normalized.PresetId = "custom";

        normalized.WindowBackground = NormalizeColor(normalized.WindowBackground, "#202124");
        normalized.WindowBorder = NormalizeColor(normalized.WindowBorder, "#505050");
        normalized.ShadowColor = NormalizeColor(normalized.ShadowColor, "#000000");
        normalized.HeadingText = NormalizeColor(normalized.HeadingText, "#FFFFFF");
        normalized.EmptyText = NormalizeColor(normalized.EmptyText, "#CCCCCC");
        normalized.TileBackground = NormalizeColor(normalized.TileBackground, "#202124");
        normalized.TileHoverBackground = NormalizeColor(normalized.TileHoverBackground, "#2A2E34");
        normalized.TilePressedBackground = NormalizeColor(normalized.TilePressedBackground, "#313740");
        normalized.TileText = NormalizeColor(normalized.TileText, "#FFFFFF");
        normalized.AccentText = NormalizeColor(normalized.AccentText, "#FFD166");
        normalized.WindowOpacity = Clamp(normalized.WindowOpacity, 0.45, 1.0, 1.0);
        normalized.WindowPadding = Clamp(normalized.WindowPadding, 4, 40, 15);
        normalized.WindowCornerRadius = Clamp(normalized.WindowCornerRadius, 0, 28, 8);
        normalized.BorderThickness = Clamp(normalized.BorderThickness, 0, 4, 1);
        normalized.TileWidth = Clamp(normalized.TileWidth, 78, 220, 108);
        normalized.TileHeight = Clamp(normalized.TileHeight, 58, 180, 72);
        normalized.TileCornerRadius = Clamp(normalized.TileCornerRadius, 0, 30, 6);
        normalized.TileSpacing = Clamp(normalized.TileSpacing, 0, 24, 8);
        normalized.IconSize = Clamp(normalized.IconSize, 24, 96, 38);
        normalized.HeadingFontSize = Clamp(normalized.HeadingFontSize, 10, 28, 15);
        normalized.TileFontSize = Clamp(normalized.TileFontSize, 8, 24, 12);
        normalized.EmptyFontSize = Clamp(normalized.EmptyFontSize, 8, 24, 13);
        if (!FontFamilies.Contains(normalized.FontFamily, StringComparer.Ordinal))
            normalized.FontFamily = "Segoe UI";
        normalized.MaxColumns = Math.Clamp(normalized.MaxColumns, 1, 8);
        normalized.ShadowBlur = Clamp(normalized.ShadowBlur, 0, 40, 18);
        normalized.ShadowOpacity = Clamp(normalized.ShadowOpacity, 0, 0.8, 0.35);
        return normalized;
    }

    public static Color ParseColor(string value)
    {
        var color = ColorConverter.ConvertFromString(value);
        return color is Color parsed ? parsed : Colors.White;
    }

    private static string NormalizeColor(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        try
        {
            var color = ParseColor(value);
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }
        catch (FormatException)
        {
            return fallback;
        }
        catch (ArgumentException)
        {
            return fallback;
        }
    }

    private static double Clamp(double value, double min, double max, double fallback) =>
        double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;

    public static IReadOnlyList<string> FontFamilies { get; } =
        ["Segoe UI", "Arial", "Consolas", "Tahoma", "Verdana", "Georgia"];
}
