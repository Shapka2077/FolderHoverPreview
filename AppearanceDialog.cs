using System.Drawing;
using Forms = System.Windows.Forms;

namespace FolderHoverPreview;

internal static class AppearanceDialog
{
    private sealed record PresetChoice(string Id, string Name)
    {
        public override string ToString() => Name;
    }

    private sealed record ColorField(
        string Label,
        Func<PreviewThemePreferences, string> Get,
        Action<PreviewThemePreferences, string> Set);

    public static PreviewThemePreferences? Show(PreviewThemePreferences current)
    {
        using var dialog = new Forms.Form
        {
            Text = "Скины и оформление FolderHoverPreview",
            StartPosition = Forms.FormStartPosition.CenterScreen,
            FormBorderStyle = Forms.FormBorderStyle.FixedDialog,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            ClientSize = new Size(820, 660),
            Font = new Font("Segoe UI", 9)
        };

        var draft = PreviewThemeConfig.Normalize(current);
        var savedTheme = draft.Clone();
        var isLoading = false;
        PreviewThemePreferences? result = null;
        var presetChoices = PreviewThemeConfig.Presets
            .Select(preset => new PresetChoice(preset.Id, preset.Name))
            .ToArray();

        var presetLabel = new Forms.Label
        {
            Text = "Готовый скин:",
            Left = 16,
            Top = 19,
            Width = 100,
            Height = 24,
            TextAlign = ContentAlignment.MiddleLeft
        };
        var presetCombo = new Forms.ComboBox
        {
            Left = 120,
            Top = 16,
            Width = 310,
            DropDownStyle = Forms.ComboBoxStyle.DropDownList
        };
        presetCombo.Items.AddRange(presetChoices);
        presetCombo.SelectedItem = presetChoices.FirstOrDefault(choice => choice.Id == draft.PresetId)
            ?? presetChoices[^1];
        dialog.Controls.AddRange([presetLabel, presetCombo]);

        var colorsGroup = new Forms.GroupBox
        {
            Text = "Цвета",
            Left = 12,
            Top = 55,
            Width = 390,
            Height = 525
        };
        var colorFields = new[]
        {
            new ColorField("Фон окна", theme => theme.WindowBackground, (theme, value) => theme.WindowBackground = value),
            new ColorField("Рамка окна", theme => theme.WindowBorder, (theme, value) => theme.WindowBorder = value),
            new ColorField("Цвет тени", theme => theme.ShadowColor, (theme, value) => theme.ShadowColor = value),
            new ColorField("Заголовок папки", theme => theme.HeadingText, (theme, value) => theme.HeadingText = value),
            new ColorField("Текст плиток", theme => theme.TileText, (theme, value) => theme.TileText = value),
            new ColorField("Фон плиток", theme => theme.TileBackground, (theme, value) => theme.TileBackground = value),
            new ColorField("Плитка при наведении", theme => theme.TileHoverBackground, (theme, value) => theme.TileHoverBackground = value),
            new ColorField("Плитка при нажатии", theme => theme.TilePressedBackground, (theme, value) => theme.TilePressedBackground = value),
            new ColorField("Акцентный текст", theme => theme.AccentText, (theme, value) => theme.AccentText = value),
            new ColorField("Сообщение пустой папки", theme => theme.EmptyText, (theme, value) => theme.EmptyText = value)
        };
        var colorTable = new Forms.TableLayoutPanel
        {
            Left = 12,
            Top = 24,
            Width = 360,
            Height = colorFields.Length * 48,
            ColumnCount = 2,
            RowCount = colorFields.Length,
            AutoSize = false
        };
        colorTable.ColumnStyles.Add(new Forms.ColumnStyle(Forms.SizeType.Percent, 62));
        colorTable.ColumnStyles.Add(new Forms.ColumnStyle(Forms.SizeType.Percent, 38));
        var colorButtons = new List<(Forms.Button Button, ColorField Field)>();
        var colorRow = 0;
        foreach (var field in colorFields)
        {
            var row = colorRow++;
            colorTable.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.Absolute, 48));
            var label = new Forms.Label
            {
                Text = field.Label,
                Dock = Forms.DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };
            var colorButton = new Forms.Button
            {
                Dock = Forms.DockStyle.Fill,
                Height = 32,
                Margin = new Forms.Padding(4, 7, 4, 7),
                FlatStyle = Forms.FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                Tag = field
            };
            colorButton.FlatAppearance.BorderColor = SystemColors.ControlDark;
            colorButton.Click += (_, _) =>
            {
                var selectedField = (ColorField)colorButton.Tag!;
                using var colorDialog = new Forms.ColorDialog
                {
                    FullOpen = true,
                    Color = ColorTranslator.FromHtml(selectedField.Get(draft))
                };
                if (colorDialog.ShowDialog(dialog) != Forms.DialogResult.OK)
                    return;

                selectedField.Set(draft, ColorTranslator.ToHtml(colorDialog.Color));
                MarkCustom();
                UpdateColorButton(colorButton, selectedField.Get(draft));
            };
            colorButtons.Add((colorButton, field));
            colorTable.Controls.Add(label, 0, row);
            colorTable.Controls.Add(colorButton, 1, row);
        }
        colorsGroup.Controls.Add(colorTable);

        var visualGroup = new Forms.GroupBox
        {
            Text = "Размеры и эффекты",
            Left = 410,
            Top = 55,
            Width = 398,
            Height = 525
        };
        var settingsPanel = new Forms.Panel
        {
            Left = 10,
            Top = 23,
            Width = 374,
            Height = 490,
            AutoScroll = true
        };
        var settingsTable = new Forms.TableLayoutPanel
        {
            Dock = Forms.DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = Forms.AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 0
        };
        settingsTable.ColumnStyles.Add(new Forms.ColumnStyle(Forms.SizeType.Percent, 67));
        settingsTable.ColumnStyles.Add(new Forms.ColumnStyle(Forms.SizeType.Percent, 33));

        var numericInputs = new Dictionary<string, Forms.NumericUpDown>(StringComparer.Ordinal);
        var fontCombo = AddFontSetting(settingsTable, draft.FontFamily, value => draft.FontFamily = value, MarkCustom);
        AddNumericSetting(settingsTable, numericInputs, "Прозрачность окна", "opacity", 45, 100, 5, 0, draft.WindowOpacity * 100, value => draft.WindowOpacity = (double)value / 100, "%", MarkCustom);
        AddNumericSetting(settingsTable, numericInputs, "Отступы окна", "padding", 4, 40, 1, 0, draft.WindowPadding, value => draft.WindowPadding = (double)value, "px", MarkCustom);
        AddNumericSetting(settingsTable, numericInputs, "Скругление окна", "windowRadius", 0, 28, 1, 0, draft.WindowCornerRadius, value => draft.WindowCornerRadius = (double)value, "px", MarkCustom);
        AddNumericSetting(settingsTable, numericInputs, "Толщина рамки", "border", 0, 4, 0.5m, 1, draft.BorderThickness, value => draft.BorderThickness = (double)value, "px", MarkCustom);
        AddNumericSetting(settingsTable, numericInputs, "Ширина плитки", "tileWidth", 78, 220, 4, 0, draft.TileWidth, value => draft.TileWidth = (double)value, "px", MarkCustom);
        AddNumericSetting(settingsTable, numericInputs, "Высота плитки", "tileHeight", 58, 180, 4, 0, draft.TileHeight, value => draft.TileHeight = (double)value, "px", MarkCustom);
        AddNumericSetting(settingsTable, numericInputs, "Скругление плиток", "tileRadius", 0, 30, 1, 0, draft.TileCornerRadius, value => draft.TileCornerRadius = (double)value, "px", MarkCustom);
        AddNumericSetting(settingsTable, numericInputs, "Расстояние между плитками", "tileSpacing", 0, 24, 1, 0, draft.TileSpacing, value => draft.TileSpacing = (double)value, "px", MarkCustom);
        AddNumericSetting(settingsTable, numericInputs, "Размер значка", "iconSize", 24, 96, 2, 0, draft.IconSize, value => draft.IconSize = (double)value, "px", MarkCustom);
        AddNumericSetting(settingsTable, numericInputs, "Размер заголовка", "headingFont", 10, 28, 1, 0, draft.HeadingFontSize, value => draft.HeadingFontSize = (double)value, "pt", MarkCustom);
        AddNumericSetting(settingsTable, numericInputs, "Размер подписи", "tileFont", 8, 24, 1, 0, draft.TileFontSize, value => draft.TileFontSize = (double)value, "pt", MarkCustom);
        AddNumericSetting(settingsTable, numericInputs, "Размер сообщения", "emptyFont", 8, 24, 1, 0, draft.EmptyFontSize, value => draft.EmptyFontSize = (double)value, "pt", MarkCustom);
        AddNumericSetting(settingsTable, numericInputs, "Колонок плиток", "columns", 1, 8, 1, 0, draft.MaxColumns, value => draft.MaxColumns = decimal.ToInt32(value), "", MarkCustom);
        AddCheckboxSetting(settingsTable, "Тень окна", draft.ShadowEnabled, value => draft.ShadowEnabled = value, MarkCustom);
        AddNumericSetting(settingsTable, numericInputs, "Размытие тени", "shadowBlur", 0, 40, 1, 0, draft.ShadowBlur, value => draft.ShadowBlur = (double)value, "px", MarkCustom);
        AddNumericSetting(settingsTable, numericInputs, "Непрозрачность тени", "shadowOpacity", 0, 80, 5, 0, draft.ShadowOpacity * 100, value => draft.ShadowOpacity = (double)value / 100, "%", MarkCustom);
        settingsPanel.Controls.Add(settingsTable);
        visualGroup.Controls.Add(settingsPanel);

        var hint = new Forms.Label
        {
            Text = "Скины, палитры и тень встроены — ничего скачивать не нужно.",
            Left = 18,
            Top = 594,
            Width = 520,
            Height = 24,
            ForeColor = SystemColors.GrayText,
            TextAlign = ContentAlignment.MiddleLeft
        };
        var saveButton = new Forms.Button
        {
            Text = "Сохранить",
            Left = 584,
            Top = 610,
            Width = 100,
            Height = 32
        };
        var cancelButton = new Forms.Button
        {
            Text = "Отмена",
            Left = 694,
            Top = 610,
            Width = 100,
            Height = 32,
            DialogResult = Forms.DialogResult.Cancel
        };
        saveButton.Click += (_, _) =>
        {
            result = PreviewThemeConfig.Normalize(draft);
            dialog.DialogResult = Forms.DialogResult.OK;
            dialog.Close();
        };
        dialog.Controls.AddRange([colorsGroup, visualGroup, hint, saveButton, cancelButton]);
        dialog.AcceptButton = saveButton;
        dialog.CancelButton = cancelButton;

        void MarkCustom()
        {
            if (isLoading)
                return;

            draft.PresetId = "custom";
            isLoading = true;
            presetCombo.SelectedItem = presetChoices[^1];
            isLoading = false;
        }

        void LoadTheme(PreviewThemePreferences theme)
        {
            isLoading = true;
            draft = PreviewThemeConfig.Normalize(theme);
            foreach (var (button, field) in colorButtons)
                UpdateColorButton(button, field.Get(draft));
            foreach (var (key, input) in numericInputs)
                SetNumericValue(input, (decimal)ReadNumericValue(key, draft));
            fontCombo.SelectedItem = draft.FontFamily;
            foreach (Forms.CheckBox checkBox in settingsTable.Controls.OfType<Forms.CheckBox>())
                checkBox.Checked = draft.ShadowEnabled;
            isLoading = false;
        }

        presetCombo.SelectedValueChanged += (_, _) =>
        {
            if (isLoading || presetCombo.SelectedItem is not PresetChoice choice)
                return;

            if (choice.Id == "custom")
            {
                LoadTheme(savedTheme);
                draft.PresetId = "custom";
                return;
            }

            var preset = PreviewThemeConfig.Presets.First(item => item.Id == choice.Id);
            LoadTheme(preset.Theme);
        };

        foreach (var (button, field) in colorButtons)
            UpdateColorButton(button, field.Get(draft));
        presetCombo.SelectedItem = presetChoices.FirstOrDefault(choice => choice.Id == draft.PresetId)
            ?? presetChoices[^1];

        return dialog.ShowDialog() == Forms.DialogResult.OK ? result : null;
    }

    private static void AddNumericSetting(
        Forms.TableLayoutPanel table,
        IDictionary<string, Forms.NumericUpDown> inputs,
        string label,
        string key,
        decimal minimum,
        decimal maximum,
        decimal increment,
        int decimals,
        double value,
        Action<decimal> changed,
        string unit,
        Action markCustom)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.Absolute, 31));
        table.Controls.Add(new Forms.Label
        {
            Text = label,
            Dock = Forms.DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        }, 0, row);

        var editor = new Forms.NumericUpDown
        {
            Minimum = minimum,
            Maximum = maximum,
            Increment = increment,
            DecimalPlaces = decimals,
            Width = string.IsNullOrEmpty(unit) ? 74 : 86,
            Anchor = Forms.AnchorStyles.Left,
            Margin = new Forms.Padding(0, 4, 0, 3)
        };
        SetNumericValue(editor, (decimal)value);
        editor.ValueChanged += (_, _) => changed(editor.Value);
        inputs.Add(key, editor);

        var cell = new Forms.FlowLayoutPanel
        {
            Dock = Forms.DockStyle.Fill,
            WrapContents = false,
            FlowDirection = Forms.FlowDirection.LeftToRight,
            Margin = Forms.Padding.Empty
        };
        cell.Controls.Add(editor);
        if (!string.IsNullOrEmpty(unit))
        {
            cell.Controls.Add(new Forms.Label
            {
                Text = unit,
                AutoSize = true,
                Margin = new Forms.Padding(4, 7, 0, 0)
            });
        }
        table.Controls.Add(cell, 1, row);
        editor.ValueChanged += (_, _) => markCustom();
    }

    private static void AddCheckboxSetting(
        Forms.TableLayoutPanel table,
        string label,
        bool isChecked,
        Action<bool> changed,
        Action markCustom)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.Absolute, 31));
        var checkBox = new Forms.CheckBox
        {
            Text = label,
            Checked = isChecked,
            AutoSize = true,
            Anchor = Forms.AnchorStyles.Left,
            Margin = new Forms.Padding(0, 6, 0, 0)
        };
        checkBox.CheckedChanged += (_, _) =>
        {
            changed(checkBox.Checked);
            markCustom();
        };
        table.Controls.Add(checkBox, 0, row);
        table.SetColumnSpan(checkBox, 2);
    }

    private static Forms.ComboBox AddFontSetting(
        Forms.TableLayoutPanel table,
        string selectedFont,
        Action<string> changed,
        Action markCustom)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.Absolute, 31));
        table.Controls.Add(new Forms.Label
        {
            Text = "Гарнитура шрифта",
            Dock = Forms.DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        }, 0, row);

        var comboBox = new Forms.ComboBox
        {
            DropDownStyle = Forms.ComboBoxStyle.DropDownList,
            Width = 120,
            Anchor = Forms.AnchorStyles.Left,
            Margin = new Forms.Padding(0, 3, 0, 3)
        };
        comboBox.Items.AddRange(PreviewThemeConfig.FontFamilies.Cast<object>().ToArray());
        comboBox.SelectedItem = selectedFont;
        comboBox.SelectedValueChanged += (_, _) =>
        {
            if (comboBox.SelectedItem is string fontFamily)
                changed(fontFamily);
            markCustom();
        };
        table.Controls.Add(comboBox, 1, row);
        return comboBox;
    }

    private static double ReadNumericValue(string key, PreviewThemePreferences theme) => key switch
    {
        "opacity" => theme.WindowOpacity * 100,
        "padding" => theme.WindowPadding,
        "windowRadius" => theme.WindowCornerRadius,
        "border" => theme.BorderThickness,
        "tileWidth" => theme.TileWidth,
        "tileHeight" => theme.TileHeight,
        "tileRadius" => theme.TileCornerRadius,
        "tileSpacing" => theme.TileSpacing,
        "iconSize" => theme.IconSize,
        "headingFont" => theme.HeadingFontSize,
        "tileFont" => theme.TileFontSize,
        "emptyFont" => theme.EmptyFontSize,
        "columns" => theme.MaxColumns,
        "shadowBlur" => theme.ShadowBlur,
        "shadowOpacity" => theme.ShadowOpacity * 100,
        _ => 0
    };

    private static void SetNumericValue(Forms.NumericUpDown input, decimal value) =>
        input.Value = Math.Clamp(value, input.Minimum, input.Maximum);

    private static void UpdateColorButton(Forms.Button button, string value)
    {
        var color = ColorTranslator.FromHtml(value);
        button.BackColor = color;
        button.ForeColor = color.GetBrightness() > 0.58f ? Color.Black : Color.White;
        button.Text = value.ToUpperInvariant();
    }
}
