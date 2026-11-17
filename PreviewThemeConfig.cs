using System.Text.Json;
using System.Threading;
using System.Windows.Media;

namespace FolderHoverPreview;

internal sealed class PreviewThemePreferences
{
    // PresetId is retained to read and write configs created by earlier builds.
    public string PresetId { get; set; } = "graphite";
    public string ColorPresetId { get; set; } = "";
    public string VisualPresetId { get; set; } = "";
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
    public bool HeadingEnabled { get; set; } = true;
    public double TileFontSize { get; set; } = 12;
    public double EmptyFontSize { get; set; } = 13;
    public string FontFamily { get; set; } = "Segoe UI";
    public int MaxColumns { get; set; } = 4;
    public bool ShadowEnabled { get; set; }
    public double ShadowBlur { get; set; } = 18;
    public double ShadowOpacity { get; set; } = 0.35;

    public PreviewThemePreferences Clone() => (PreviewThemePreferences)MemberwiseClone();
}

internal sealed record PreviewColorPreset(string Id, string Name, PreviewThemePreferences Theme);
internal sealed record PreviewVisualPreset(string Id, string Name, PreviewThemePreferences Theme);

internal static class PreviewThemeConfig
{
    private const string SettingsMutexName = @"Local\ShapkaFolderHoverPreviewThemeCSharp";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string SettingsPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FolderHoverPreview",
            "preview-theme.json");

    public static IReadOnlyList<PreviewColorPreset> ColorPresets { get; } =
    [
        new("graphite", "Графит", new PreviewThemePreferences { ColorPresetId = "graphite" }),
        new("midnight", "Полночь", new PreviewThemePreferences
        {
            ColorPresetId = "midnight",
            WindowBackground = "#121621",
            WindowBorder = "#343B4D",
            HeadingText = "#F4F6FF",
            EmptyText = "#ADB6CB",
            TileBackground = "#191F2D",
            TileHoverBackground = "#242E42",
            TilePressedBackground = "#303E59",
            TileText = "#F1F4FC",
            AccentText = "#A994FF",
        }),
        new("ocean", "Океан", new PreviewThemePreferences
        {
            ColorPresetId = "ocean",
            WindowBackground = "#122033",
            WindowBorder = "#355875",
            HeadingText = "#EAF7FF",
            EmptyText = "#B5CDDF",
            TileBackground = "#192B40",
            TileHoverBackground = "#21405B",
            TilePressedBackground = "#2B5475",
            TileText = "#F0F9FF",
            AccentText = "#62D5F2",
        }),
        new("forest", "Лес", new PreviewThemePreferences
        {
            ColorPresetId = "forest",
            WindowBackground = "#17211D",
            WindowBorder = "#3D5749",
            HeadingText = "#F1FFF5",
            EmptyText = "#B9D0C0",
            TileBackground = "#1D2B24",
            TileHoverBackground = "#294033",
            TilePressedBackground = "#355844",
            TileText = "#F1FFF5",
            AccentText = "#9BE7B0",
        }),
        new("rose", "Роза", new PreviewThemePreferences
        {
            ColorPresetId = "rose",
            WindowBackground = "#281D25",
            WindowBorder = "#674658",
            HeadingText = "#FFF4F8",
            EmptyText = "#D8BFCB",
            TileBackground = "#35232E",
            TileHoverBackground = "#4A2D3D",
            TilePressedBackground = "#603A50",
            TileText = "#FFF4F8",
            AccentText = "#FF9BC2",
        }),
        new("light", "Светлая", new PreviewThemePreferences
        {
            ColorPresetId = "light",
            WindowBackground = "#F2F4F8",
            WindowBorder = "#C7CDD8",
            HeadingText = "#202532",
            EmptyText = "#626B78",
            TileBackground = "#FFFFFF",
            TileHoverBackground = "#E7EDF6",
            TilePressedBackground = "#D7E1EF",
            TileText = "#232A35",
            AccentText = "#1769AA"
        }),
        new("cyberpunk", "Неон и графит", new PreviewThemePreferences
        {
            ColorPresetId = "cyberpunk",
            WindowBackground = "#17131F",
            WindowBorder = "#544064",
            ShadowColor = "#100C16",
            HeadingText = "#F7EDFF",
            EmptyText = "#BFB1CC",
            TileBackground = "#211B2B",
            TileHoverBackground = "#382B48",
            TilePressedBackground = "#4B3456",
            TileText = "#F8F0FF",
            AccentText = "#72F1D2"
        }),
        new("lagoon", "Лагуна и коралл", new PreviewThemePreferences
        {
            ColorPresetId = "lagoon",
            WindowBackground = "#102A2D",
            WindowBorder = "#4E7775",
            ShadowColor = "#071719",
            HeadingText = "#E8F7EE",
            EmptyText = "#A9C7BD",
            TileBackground = "#18383A",
            TileHoverBackground = "#28534D",
            TilePressedBackground = "#38675B",
            TileText = "#F2F4DF",
            AccentText = "#FFAA7A"
        }),
        new("amethyst", "Аметист и лайм", new PreviewThemePreferences
        {
            ColorPresetId = "amethyst",
            WindowBackground = "#20152E",
            WindowBorder = "#574276",
            ShadowColor = "#120B1B",
            HeadingText = "#F1E9FF",
            EmptyText = "#C1B3D5",
            TileBackground = "#2D2040",
            TileHoverBackground = "#46355C",
            TilePressedBackground = "#56466C",
            TileText = "#F2EAFE",
            AccentText = "#C9F264"
        }),
        new("terracotta", "Терракота и ваниль", new PreviewThemePreferences
        {
            ColorPresetId = "terracotta",
            WindowBackground = "#F4E8D8",
            WindowBorder = "#C58A5A",
            ShadowColor = "#76513A",
            HeadingText = "#4A2D22",
            EmptyText = "#81685A",
            TileBackground = "#FAF1E5",
            TileHoverBackground = "#EAD1AE",
            TilePressedBackground = "#D8B795",
            TileText = "#4A2D22",
            AccentText = "#AD4F3B"
        }),
        new("arctic", "Арктика и янтарь", new PreviewThemePreferences
        {
            ColorPresetId = "arctic",
            WindowBackground = "#E8F2F5",
            WindowBorder = "#8CAEB8",
            ShadowColor = "#385C68",
            HeadingText = "#17333C",
            EmptyText = "#57747D",
            TileBackground = "#F7FBFC",
            TileHoverBackground = "#C8E4E8",
            TilePressedBackground = "#A8CED5",
            TileText = "#17333C",
            AccentText = "#A95F18"
        }),
        new("sakura", "Сакура и индиго", new PreviewThemePreferences
        {
            ColorPresetId = "sakura",
            WindowBackground = "#201D32",
            WindowBorder = "#514969",
            ShadowColor = "#100E1C",
            HeadingText = "#F8F0FF",
            EmptyText = "#C3B8D0",
            TileBackground = "#2A263F",
            TileHoverBackground = "#3F3859",
            TilePressedBackground = "#514668",
            TileText = "#F7F1FF",
            AccentText = "#F2A6B8"
        }),
        new("citrus", "Цитрус и сланец", new PreviewThemePreferences
        {
            ColorPresetId = "citrus",
            WindowBackground = "#20292A",
            WindowBorder = "#52645A",
            ShadowColor = "#111718",
            HeadingText = "#F1F4E8",
            EmptyText = "#B5C0AA",
            TileBackground = "#2B3634",
            TileHoverBackground = "#45513C",
            TilePressedBackground = "#586148",
            TileText = "#F4F3E5",
            AccentText = "#E2E96B"
        }),
        new("custom", "Пользовательский", new PreviewThemePreferences
        {
            PresetId = "custom",
            ColorPresetId = "custom"
        })
    ];

    public static IReadOnlyList<PreviewVisualPreset> VisualPresets { get; } =
    [
        new("default", "По умолчанию", new PreviewThemePreferences { VisualPresetId = "default" }),
        new("compact", "Компактный", new PreviewThemePreferences
        {
            VisualPresetId = "compact",
            WindowPadding = 10,
            WindowCornerRadius = 6,
            BorderThickness = 1,
            TileWidth = 90,
            TileHeight = 64,
            TileCornerRadius = 4,
            TileSpacing = 5,
            IconSize = 32,
            HeadingFontSize = 14,
            TileFontSize = 11,
            EmptyFontSize = 12,
            MaxColumns = 5,
            ShadowEnabled = false,
            ShadowBlur = 12,
            ShadowOpacity = 0.2
        }),
        new("spacious", "Просторный", new PreviewThemePreferences
        {
            VisualPresetId = "spacious",
            WindowPadding = 22,
            WindowCornerRadius = 14,
            BorderThickness = 1.5,
            TileWidth = 132,
            TileHeight = 96,
            TileCornerRadius = 14,
            TileSpacing = 12,
            IconSize = 50,
            HeadingFontSize = 17,
            TileFontSize = 13,
            EmptyFontSize = 14,
            MaxColumns = 3,
            ShadowEnabled = true,
            ShadowBlur = 24,
            ShadowOpacity = 0.35
        }),
        new("touch", "Сенсорный / крупный", new PreviewThemePreferences
        {
            VisualPresetId = "touch",
            WindowPadding = 24,
            WindowCornerRadius = 18,
            BorderThickness = 1,
            TileWidth = 148,
            TileHeight = 120,
            TileCornerRadius = 18,
            TileSpacing = 14,
            IconSize = 64,
            HeadingFontSize = 18,
            TileFontSize = 14,
            EmptyFontSize = 16,
            MaxColumns = 3,
            ShadowEnabled = true,
            ShadowBlur = 28,
            ShadowOpacity = 0.4
        }),
        new("soft", "Мягкие карточки", new PreviewThemePreferences
        {
            VisualPresetId = "soft",
            WindowPadding = 18,
            WindowCornerRadius = 20,
            BorderThickness = 0.75,
            TileWidth = 118,
            TileHeight = 86,
            TileCornerRadius = 16,
            TileSpacing = 10,
            IconSize = 42,
            HeadingFontSize = 16,
            TileFontSize = 12,
            EmptyFontSize = 14,
            MaxColumns = 4,
            ShadowEnabled = true,
            ShadowBlur = 32,
            ShadowOpacity = 0.28
        }),
        new("minimal", "Минимализм без тени", new PreviewThemePreferences
        {
            VisualPresetId = "minimal",
            WindowPadding = 14,
            WindowCornerRadius = 2,
            BorderThickness = 1,
            TileWidth = 104,
            TileHeight = 70,
            TileCornerRadius = 2,
            TileSpacing = 6,
            IconSize = 36,
            HeadingFontSize = 15,
            TileFontSize = 12,
            EmptyFontSize = 13,
            MaxColumns = 4,
            ShadowEnabled = false,
            ShadowBlur = 0,
            ShadowOpacity = 0
        }),
        new("floating", "Парящая панель", new PreviewThemePreferences
        {
            VisualPresetId = "floating",
            WindowPadding = 20,
            WindowCornerRadius = 12,
            BorderThickness = 0,
            TileWidth = 116,
            TileHeight = 82,
            TileCornerRadius = 10,
            TileSpacing = 9,
            IconSize = 42,
            HeadingFontSize = 16,
            TileFontSize = 12,
            EmptyFontSize = 13,
            MaxColumns = 4,
            ShadowEnabled = true,
            ShadowBlur = 30,
            ShadowOpacity = 0.45
        }),
        new("custom", "Пользовательские значения", new PreviewThemePreferences
        {
            VisualPresetId = "custom"
        })
    ];

    public static PreviewThemePreferences Default => new()
    {
        PresetId = "graphite",
        ColorPresetId = "graphite",
        VisualPresetId = "default"
    };

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
        if (string.IsNullOrEmpty(normalized.ColorPresetId))
        {
            normalized.ColorPresetId = ColorPresets.Any(
                preset => preset.Id.Equals(normalized.PresetId, StringComparison.Ordinal))
                ? normalized.PresetId
                : "custom";
        }
        if (!ColorPresets.Any(preset => preset.Id.Equals(normalized.ColorPresetId, StringComparison.Ordinal)))
            normalized.ColorPresetId = "custom";

        if (string.IsNullOrEmpty(normalized.VisualPresetId))
            normalized.VisualPresetId = "custom";
        if (!VisualPresets.Any(preset => preset.Id.Equals(normalized.VisualPresetId, StringComparison.Ordinal)))
            normalized.VisualPresetId = "custom";
        normalized.PresetId = normalized.ColorPresetId;

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
        normalized.WindowPadding = Clamp(normalized.WindowPadding, 0, 100, 15);
        normalized.WindowCornerRadius = Clamp(normalized.WindowCornerRadius, 0, 120, 8);
        normalized.BorderThickness = Clamp(normalized.BorderThickness, 0, 12, 1);
        normalized.TileWidth = Clamp(normalized.TileWidth, 48, 360, 108);
        normalized.TileHeight = Clamp(normalized.TileHeight, 32, 300, 72);
        normalized.TileCornerRadius = Clamp(normalized.TileCornerRadius, 0, 120, 6);
        normalized.TileSpacing = Clamp(normalized.TileSpacing, 0, 80, 8);
        normalized.IconSize = Clamp(normalized.IconSize, 8, 192, 38);
        normalized.HeadingFontSize = Clamp(normalized.HeadingFontSize, 6, 48, 15);
        normalized.TileFontSize = Clamp(normalized.TileFontSize, 6, 40, 12);
        normalized.EmptyFontSize = Clamp(normalized.EmptyFontSize, 6, 40, 13);
        if (!FontFamilies.Contains(normalized.FontFamily, StringComparer.Ordinal))
            normalized.FontFamily = "Segoe UI";
        normalized.MaxColumns = Math.Clamp(normalized.MaxColumns, 1, 16);
        normalized.ShadowBlur = Clamp(normalized.ShadowBlur, 0, 100, 18);
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
