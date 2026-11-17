using System.Drawing;
using System.Runtime.InteropServices;
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
        string Description,
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
            ClientSize = new Size(820, 550),
            Font = new Font("Segoe UI", 9)
        };
        using var toolTip = new Forms.ToolTip
        {
            InitialDelay = 2000,
            ReshowDelay = 400,
            AutoPopDelay = 10000,
            ShowAlways = true
        };

        var draft = PreviewThemeConfig.Normalize(current);
        var savedTheme = draft.Clone();
        var isLoading = false;
        PreviewThemePreferences? result = null;
        var colorPresetChoices = PreviewThemeConfig.ColorPresets
            .Select(preset => new PresetChoice(preset.Id, preset.Name))
            .ToArray();
        var visualPresetChoices = PreviewThemeConfig.VisualPresets
            .Select(preset => new PresetChoice(preset.Id, preset.Name))
            .ToArray();

        var colorPresetLabel = new Forms.Label
        {
            Text = "Цветовой скин:",
            Left = 16,
            Top = 19,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft
        };
        colorPresetLabel.Width = colorPresetLabel.PreferredSize.Width;
        var colorPresetCombo = new Forms.ComboBox
        {
            Left = colorPresetLabel.Right + 8,
            Top = 16,
            Width = 230,
            DropDownStyle = Forms.ComboBoxStyle.DropDownList
        };
        colorPresetCombo.Items.AddRange(colorPresetChoices);
        colorPresetCombo.SelectedItem = colorPresetChoices.FirstOrDefault(choice => choice.Id == draft.ColorPresetId)
            ?? colorPresetChoices[^1];
        toolTip.SetToolTip(
            colorPresetCombo,
            "Выберите только палитру цветов. Размеры, шрифт и эффекты задаются отдельно справа.");
        toolTip.SetToolTip(
            colorPresetLabel,
            "Цветовой скин меняет только палитру и не влияет на размеры или эффекты.");
        EnableThemeComboBox(colorPresetCombo, () => draft);

        var visualPresetLabel = new Forms.Label
        {
            Text = "Пресет размеров:",
            Left = colorPresetCombo.Right + 12,
            Top = 19,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft
        };
        visualPresetLabel.Width = visualPresetLabel.PreferredSize.Width;
        var visualPresetCombo = new Forms.ComboBox
        {
            Left = visualPresetLabel.Right + 8,
            Top = 16,
            Width = dialog.ClientSize.Width - visualPresetLabel.Right - 24,
            DropDownStyle = Forms.ComboBoxStyle.DropDownList
        };
        visualPresetCombo.Items.AddRange(visualPresetChoices);
        visualPresetCombo.SelectedItem = visualPresetChoices.FirstOrDefault(choice => choice.Id == draft.VisualPresetId)
            ?? visualPresetChoices[^1];
        toolTip.SetToolTip(
            visualPresetCombo,
            "Выберите набор размеров и эффектов. Он не меняет цвета выбранного скина.");
        toolTip.SetToolTip(
            visualPresetLabel,
            "Отдельный набор размеров и эффектов, независимый от цветового скина.");
        EnableThemeComboBox(visualPresetCombo, () => draft);
        dialog.Controls.AddRange(
        [
            colorPresetLabel,
            colorPresetCombo,
            visualPresetLabel,
            visualPresetCombo
        ]);

        var colorsGroup = new Forms.GroupBox
        {
            Text = "Цвета",
            Left = 12,
            Top = 55,
            Width = 390,
            Height = 430
        };
        var colorFields = new[]
        {
            new ColorField("Фон окна", "Основной цвет фона всплывающего окна превью.", theme => theme.WindowBackground, (theme, value) => theme.WindowBackground = value),
            new ColorField("Рамка окна", "Цвет контура окна превью и границ меню в трее.", theme => theme.WindowBorder, (theme, value) => theme.WindowBorder = value),
            new ColorField("Цвет тени", "Цвет встроенной тени вокруг окна превью.", theme => theme.ShadowColor, (theme, value) => theme.ShadowColor = value),
            new ColorField("Заголовок папки", "Цвет названия папки в верхней части превью.", theme => theme.HeadingText, (theme, value) => theme.HeadingText = value),
            new ColorField("Текст плиток", "Основной цвет подписей файлов в плитках.", theme => theme.TileText, (theme, value) => theme.TileText = value),
            new ColorField("Фон плиток", "Цвет обычной плитки файла без наведения.", theme => theme.TileBackground, (theme, value) => theme.TileBackground = value),
            new ColorField("Плитка при наведении", "Цвет фона плитки, когда курсор находится над ней.", theme => theme.TileHoverBackground, (theme, value) => theme.TileHoverBackground = value),
            new ColorField("Плитка при нажатии", "Цвет плитки в момент нажатия кнопки мыши.", theme => theme.TilePressedBackground, (theme, value) => theme.TilePressedBackground = value),
            new ColorField("Акцентный текст", "Цвет подписи плитки при наведении и акцентных элементов.", theme => theme.AccentText, (theme, value) => theme.AccentText = value),
            new ColorField("Сообщение пустой папки", "Цвет текста, который показывается, если подходящих файлов нет.", theme => theme.EmptyText, (theme, value) => theme.EmptyText = value)
        };
        var colorTable = new Forms.TableLayoutPanel
        {
            Left = 12,
            Top = 24,
            Width = 360,
            Height = colorFields.Length * 38,
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
            colorTable.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.Absolute, 38));
            var label = new Forms.Label
            {
                Text = field.Label,
                Dock = Forms.DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };
            toolTip.SetToolTip(label, field.Description);
            var colorButton = new Forms.Button
            {
                Dock = Forms.DockStyle.Fill,
                Height = 24,
                Margin = new Forms.Padding(4, 4, 4, 4),
                FlatStyle = Forms.FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                Tag = field
            };
            colorButton.FlatAppearance.BorderColor = SystemColors.ControlDark;
            toolTip.SetToolTip(colorButton, field.Description);
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
                MarkCustomColors();
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
            Height = 430
        };
        var settingsPanel = new Forms.Panel
        {
            Left = 10,
            Top = 23,
            Width = 374,
            Height = 395,
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
        var fontCombo = AddFontSetting(settingsTable, toolTip, draft.FontFamily, value => draft.FontFamily = value, MarkCustomVisual);
        EnableThemeComboBox(fontCombo, () => draft);
        AddNumericSetting(settingsTable, toolTip, numericInputs, "Прозрачность окна", "opacity", 45, 100, 5, 0, draft.WindowOpacity * 100, value => draft.WindowOpacity = (double)value / 100, "%", MarkCustomVisual);
        AddNumericSetting(settingsTable, toolTip, numericInputs, "Отступы окна", "padding", 4, 40, 1, 0, draft.WindowPadding, value => draft.WindowPadding = (double)value, "px", MarkCustomVisual);
        AddNumericSetting(settingsTable, toolTip, numericInputs, "Скругление окна", "windowRadius", 0, 28, 1, 0, draft.WindowCornerRadius, value => draft.WindowCornerRadius = (double)value, "px", MarkCustomVisual);
        AddNumericSetting(settingsTable, toolTip, numericInputs, "Толщина рамки", "border", 0, 4, 0.5m, 1, draft.BorderThickness, value => draft.BorderThickness = (double)value, "px", MarkCustomVisual);
        AddNumericSetting(settingsTable, toolTip, numericInputs, "Ширина плитки", "tileWidth", 78, 220, 4, 0, draft.TileWidth, value => draft.TileWidth = (double)value, "px", MarkCustomVisual);
        AddNumericSetting(settingsTable, toolTip, numericInputs, "Высота плитки", "tileHeight", 58, 180, 4, 0, draft.TileHeight, value => draft.TileHeight = (double)value, "px", MarkCustomVisual);
        AddNumericSetting(settingsTable, toolTip, numericInputs, "Скругление плиток", "tileRadius", 0, 30, 1, 0, draft.TileCornerRadius, value => draft.TileCornerRadius = (double)value, "px", MarkCustomVisual);
        AddNumericSetting(settingsTable, toolTip, numericInputs, "Расстояние между плитками", "tileSpacing", 0, 24, 1, 0, draft.TileSpacing, value => draft.TileSpacing = (double)value, "px", MarkCustomVisual);
        AddNumericSetting(settingsTable, toolTip, numericInputs, "Размер значка", "iconSize", 24, 96, 2, 0, draft.IconSize, value => draft.IconSize = (double)value, "px", MarkCustomVisual);
        AddNumericSetting(settingsTable, toolTip, numericInputs, "Размер заголовка", "headingFont", 10, 28, 1, 0, draft.HeadingFontSize, value => draft.HeadingFontSize = (double)value, "pt", MarkCustomVisual);
        AddNumericSetting(settingsTable, toolTip, numericInputs, "Размер подписи", "tileFont", 8, 24, 1, 0, draft.TileFontSize, value => draft.TileFontSize = (double)value, "pt", MarkCustomVisual);
        AddNumericSetting(settingsTable, toolTip, numericInputs, "Размер сообщения", "emptyFont", 8, 24, 1, 0, draft.EmptyFontSize, value => draft.EmptyFontSize = (double)value, "pt", MarkCustomVisual);
        AddNumericSetting(settingsTable, toolTip, numericInputs, "Колонок плиток", "columns", 1, 8, 1, 0, draft.MaxColumns, value => draft.MaxColumns = decimal.ToInt32(value), "", MarkCustomVisual);
        AddCheckboxSetting(settingsTable, toolTip, "Тень окна", draft.ShadowEnabled, value => draft.ShadowEnabled = value, MarkCustomVisual);
        AddNumericSetting(settingsTable, toolTip, numericInputs, "Размытие тени", "shadowBlur", 0, 40, 1, 0, draft.ShadowBlur, value => draft.ShadowBlur = (double)value, "px", MarkCustomVisual);
        AddNumericSetting(settingsTable, toolTip, numericInputs, "Непрозрачность тени", "shadowOpacity", 0, 80, 5, 0, draft.ShadowOpacity * 100, value => draft.ShadowOpacity = (double)value / 100, "%", MarkCustomVisual);
        settingsPanel.Controls.Add(settingsTable);
        visualGroup.Controls.Add(settingsPanel);

        var resetButton = new Forms.Button
        {
            Name = "RestoreDefaultsButton",
            Text = "Восстановить по умолчанию",
            Left = 18,
            Top = 500,
            Width = 210,
            Height = 32
        };
        toolTip.SetToolTip(
            resetButton,
            "Вернуть исходные цвета, размеры, шрифт и эффекты. Изменения применятся после нажатия «Сохранить».");
        var saveButton = new Forms.Button
        {
            Name = "SaveButton",
            Text = "Сохранить",
            Left = 584,
            Top = 500,
            Width = 100,
            Height = 32
        };
        var cancelButton = new Forms.Button
        {
            Name = "CancelButton",
            Text = "Отмена",
            Left = 694,
            Top = 500,
            Width = 100,
            Height = 32,
            DialogResult = Forms.DialogResult.Cancel
        };
        dialog.Controls.AddRange(
        [
            colorsGroup,
            visualGroup,
            resetButton,
            saveButton,
            cancelButton
        ]);
        dialog.AcceptButton = saveButton;
        dialog.CancelButton = cancelButton;
        dialog.Shown += (_, _) => ApplyNativeTitleBarTheme(dialog, draft);

        void UpdateColorControls()
        {
            foreach (var (button, field) in colorButtons)
                UpdateColorButton(button, field.Get(draft));
        }

        void UpdateVisualControls()
        {
            foreach (var (key, input) in numericInputs)
                SetNumericValue(input, (decimal)ReadNumericValue(key, draft));
            fontCombo.SelectedItem = draft.FontFamily;
            foreach (Forms.CheckBox checkBox in settingsTable.Controls.OfType<Forms.CheckBox>())
                checkBox.Checked = draft.ShadowEnabled;
        }

        void MarkCustomColors()
        {
            if (isLoading)
                return;

            draft.ColorPresetId = "custom";
            draft.PresetId = "custom";
            isLoading = true;
            colorPresetCombo.SelectedItem = colorPresetChoices[^1];
            isLoading = false;
            ApplyDialogTheme(dialog, draft, toolTip);
        }

        void MarkCustomVisual()
        {
            if (isLoading)
                return;

            draft.VisualPresetId = "custom";
            isLoading = true;
            visualPresetCombo.SelectedItem = visualPresetChoices[^1];
            isLoading = false;
        }

        void LoadColorPreset(string presetId)
        {
            isLoading = true;
            try
            {
                if (presetId == "custom")
                {
                    CopyColorSettings(draft, savedTheme);
                    draft.ColorPresetId = "custom";
                }
                else
                {
                    var preset = PreviewThemeConfig.ColorPresets.First(item => item.Id == presetId);
                    CopyColorSettings(draft, preset.Theme);
                    draft.ColorPresetId = preset.Id;
                }

                draft.PresetId = draft.ColorPresetId;
                colorPresetCombo.SelectedItem = colorPresetChoices.First(choice => choice.Id == presetId);
                UpdateColorControls();
            }
            finally
            {
                isLoading = false;
            }

            ApplyDialogTheme(dialog, draft, toolTip);
        }

        void LoadVisualPreset(string presetId)
        {
            isLoading = true;
            try
            {
                if (presetId == "custom")
                {
                    CopyVisualSettings(draft, savedTheme);
                    draft.VisualPresetId = "custom";
                }
                else
                {
                    var preset = PreviewThemeConfig.VisualPresets.First(item => item.Id == presetId);
                    CopyVisualSettings(draft, preset.Theme);
                    draft.VisualPresetId = preset.Id;
                }

                visualPresetCombo.SelectedItem = visualPresetChoices.First(choice => choice.Id == presetId);
                UpdateVisualControls();
            }
            finally
            {
                isLoading = false;
            }

        }

        void LoadFullTheme(PreviewThemePreferences theme)
        {
            isLoading = true;
            try
            {
                draft = PreviewThemeConfig.Normalize(theme);
                colorPresetCombo.SelectedItem = colorPresetChoices.FirstOrDefault(
                    choice => choice.Id == draft.ColorPresetId) ?? colorPresetChoices[^1];
                visualPresetCombo.SelectedItem = visualPresetChoices.FirstOrDefault(
                    choice => choice.Id == draft.VisualPresetId) ?? visualPresetChoices[^1];
                UpdateColorControls();
                UpdateVisualControls();
            }
            finally
            {
                isLoading = false;
            }

            ApplyDialogTheme(dialog, draft, toolTip);
        }

        colorPresetCombo.SelectedValueChanged += (_, _) =>
        {
            if (!isLoading && colorPresetCombo.SelectedItem is PresetChoice choice)
                LoadColorPreset(choice.Id);
        };
        visualPresetCombo.SelectedValueChanged += (_, _) =>
        {
            if (!isLoading && visualPresetCombo.SelectedItem is PresetChoice choice)
                LoadVisualPreset(choice.Id);
        };
        resetButton.Click += (_, _) => LoadFullTheme(PreviewThemeConfig.Default);
        saveButton.Click += (_, _) =>
        {
            result = PreviewThemeConfig.Normalize(draft);
            dialog.DialogResult = Forms.DialogResult.OK;
            dialog.Close();
        };

        UpdateColorControls();
        UpdateVisualControls();
        ApplyDialogTheme(dialog, draft, toolTip);

        return dialog.ShowDialog() == Forms.DialogResult.OK ? result : null;
    }

    private static void AddNumericSetting(
        Forms.TableLayoutPanel table,
        Forms.ToolTip toolTip,
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
        var labelControl = new Forms.Label
        {
            Text = label,
            Dock = Forms.DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };
        toolTip.SetToolTip(labelControl, GetNumericTooltip(key));
        table.Controls.Add(labelControl, 0, row);

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
        toolTip.SetToolTip(editor, GetNumericTooltip(key));
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
        Forms.ToolTip toolTip,
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
        toolTip.SetToolTip(
            checkBox,
            "Включает или выключает встроенную тень окна превью. Её размытие и насыщенность настраиваются ниже.");
        table.Controls.Add(checkBox, 0, row);
        table.SetColumnSpan(checkBox, 2);
    }

    private static Forms.ComboBox AddFontSetting(
        Forms.TableLayoutPanel table,
        Forms.ToolTip toolTip,
        string selectedFont,
        Action<string> changed,
        Action markCustom)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.Absolute, 31));
        var fontLabel = new Forms.Label
        {
            Text = "Гарнитура шрифта",
            Dock = Forms.DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };
        toolTip.SetToolTip(
            fontLabel,
            "Выберите гарнитуру для заголовка папки, подписей файлов и сообщения о пустой папке.");
        table.Controls.Add(fontLabel, 0, row);

        var comboBox = new Forms.ComboBox
        {
            DropDownStyle = Forms.ComboBoxStyle.DropDownList,
            Width = 120,
            Anchor = Forms.AnchorStyles.Left,
            Margin = new Forms.Padding(0, 3, 0, 3)
        };
        comboBox.Items.AddRange(PreviewThemeConfig.FontFamilies.Cast<object>().ToArray());
        comboBox.SelectedItem = selectedFont;
        toolTip.SetToolTip(
            comboBox,
            "Выберите гарнитуру для заголовка папки, подписей файлов и сообщения о пустой папке.");
        comboBox.SelectedValueChanged += (_, _) =>
        {
            if (comboBox.SelectedItem is string fontFamily)
                changed(fontFamily);
            markCustom();
        };
        table.Controls.Add(comboBox, 1, row);
        return comboBox;
    }

    private static void EnableThemeComboBox(
        Forms.ComboBox comboBox,
        Func<PreviewThemePreferences> getTheme)
    {
        comboBox.DrawMode = Forms.DrawMode.OwnerDrawFixed;
        comboBox.DrawItem += (_, args) =>
        {
            var theme = getTheme();
            var selected = (args.State & Forms.DrawItemState.Selected) != 0;
            var background = ColorTranslator.FromHtml(
                selected ? theme.TileHoverBackground : theme.TileBackground);
            var foreground = selected
                ? GetContrastColor(background)
                : ColorTranslator.FromHtml(theme.TileText);
            using var brush = new SolidBrush(background);
            args.Graphics.FillRectangle(brush, args.Bounds);

            var text = args.Index >= 0
                ? comboBox.Items[args.Index]?.ToString() ?? string.Empty
                : comboBox.Text;
            var textBounds = args.Bounds;
            textBounds.Inflate(-4, 0);
            Forms.TextRenderer.DrawText(
                args.Graphics,
                text,
                args.Font,
                textBounds,
                foreground,
                Forms.TextFormatFlags.Left
                    | Forms.TextFormatFlags.VerticalCenter
                    | Forms.TextFormatFlags.EndEllipsis
                    | Forms.TextFormatFlags.NoPrefix);
            if (selected && args.Bounds.Width > 1 && args.Bounds.Height > 1)
            {
                using var border = new Pen(ColorTranslator.FromHtml(theme.AccentText));
                args.Graphics.DrawRectangle(
                    border,
                    args.Bounds.X,
                    args.Bounds.Y,
                    args.Bounds.Width - 1,
                    args.Bounds.Height - 1);
            }

            args.DrawFocusRectangle();
        };
    }

    private static string GetNumericTooltip(string key) => key switch
    {
        "opacity" => "Насколько непрозрачным будет всё окно превью: 100% — полностью непрозрачное.",
        "padding" => "Внутреннее расстояние от рамки окна до заголовка и плиток.",
        "windowRadius" => "Радиус скругления внешних углов окна превью.",
        "border" => "Толщина контура вокруг окна превью; 0 отключает рамку.",
        "tileWidth" => "Ширина каждой плитки файла.",
        "tileHeight" => "Минимальная высота плитки файла.",
        "tileRadius" => "Радиус скругления углов плиток.",
        "tileSpacing" => "Расстояние между плитками и минимальный отступ между значком и подписью.",
        "iconSize" => "Размер значка файла внутри плитки.",
        "headingFont" => "Размер названия папки над списком файлов.",
        "tileFont" => "Размер подписей файлов на плитках.",
        "emptyFont" => "Размер сообщения, которое показывается, если подходящих файлов нет.",
        "columns" => "Максимальное число плиток в одной строке до переноса на следующую.",
        "shadowBlur" => "Радиус размытия встроенной тени окна.",
        "shadowOpacity" => "Насыщенность тени: 0% делает её невидимой, большее значение — темнее.",
        _ => "Изменяет внешний вид окна превью."
    };

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

    private static void CopyColorSettings(
        PreviewThemePreferences destination,
        PreviewThemePreferences source)
    {
        destination.WindowBackground = source.WindowBackground;
        destination.WindowBorder = source.WindowBorder;
        destination.ShadowColor = source.ShadowColor;
        destination.HeadingText = source.HeadingText;
        destination.EmptyText = source.EmptyText;
        destination.TileBackground = source.TileBackground;
        destination.TileHoverBackground = source.TileHoverBackground;
        destination.TilePressedBackground = source.TilePressedBackground;
        destination.TileText = source.TileText;
        destination.AccentText = source.AccentText;
    }

    private static void CopyVisualSettings(
        PreviewThemePreferences destination,
        PreviewThemePreferences source)
    {
        destination.WindowOpacity = source.WindowOpacity;
        destination.WindowPadding = source.WindowPadding;
        destination.WindowCornerRadius = source.WindowCornerRadius;
        destination.BorderThickness = source.BorderThickness;
        destination.TileWidth = source.TileWidth;
        destination.TileHeight = source.TileHeight;
        destination.TileCornerRadius = source.TileCornerRadius;
        destination.TileSpacing = source.TileSpacing;
        destination.IconSize = source.IconSize;
        destination.HeadingFontSize = source.HeadingFontSize;
        destination.TileFontSize = source.TileFontSize;
        destination.EmptyFontSize = source.EmptyFontSize;
        destination.FontFamily = source.FontFamily;
        destination.MaxColumns = source.MaxColumns;
        destination.ShadowEnabled = source.ShadowEnabled;
        destination.ShadowBlur = source.ShadowBlur;
        destination.ShadowOpacity = source.ShadowOpacity;
    }

    private static void ApplyDialogTheme(
        Forms.Form dialog,
        PreviewThemePreferences theme,
        Forms.ToolTip toolTip)
    {
        var background = ColorTranslator.FromHtml(theme.WindowBackground);
        var text = ColorTranslator.FromHtml(theme.TileText);
        dialog.BackColor = background;
        dialog.ForeColor = text;

        toolTip.BackColor = ColorTranslator.FromHtml(theme.TileHoverBackground);
        toolTip.ForeColor = GetContrastColor(toolTip.BackColor);
        foreach (Forms.Control child in dialog.Controls)
            ApplyDialogTheme(child, theme);
        ApplyNativeTitleBarTheme(dialog, theme);
    }

    private static void ApplyNativeTitleBarTheme(
        Forms.Form dialog,
        PreviewThemePreferences theme)
    {
        if (!dialog.IsHandleCreated)
            return;

        try
        {
            var handle = dialog.Handle;
            var caption = ColorTranslator.FromHtml(theme.WindowBackground);
            var isDark = GetContrastColor(caption) == Color.White ? 1 : 0;
            var darkModeResult = SetDwmAttribute(handle, 20, isDark);
            if (darkModeResult != 0)
                _ = SetDwmAttribute(handle, 19, isDark);

            _ = SetDwmAttribute(handle, 35, ColorTranslator.ToWin32(caption));
            _ = SetDwmAttribute(
                handle,
                36,
                ColorTranslator.ToWin32(GetContrastColor(caption)));
            _ = SetDwmAttribute(
                handle,
                34,
                ColorTranslator.ToWin32(ColorTranslator.FromHtml(theme.WindowBorder)));
        }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
        catch (ExternalException) { }
    }

    private static int SetDwmAttribute(IntPtr handle, uint attribute, int value) =>
        DwmSetWindowAttribute(handle, attribute, ref value, sizeof(int));

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(
        IntPtr windowHandle,
        uint attribute,
        ref int value,
        int valueSize);

    private static void ApplyDialogTheme(Forms.Control control, PreviewThemePreferences theme)
    {
        if (control is Forms.Button swatch && swatch.Tag is ColorField)
        {
            return;
        }

        var window = ColorTranslator.FromHtml(theme.WindowBackground);
        var normalTile = ColorTranslator.FromHtml(theme.TileBackground);
        var foreground = ColorTranslator.FromHtml(theme.TileText);
        var border = ColorTranslator.FromHtml(theme.WindowBorder);

        switch (control)
        {
            case Forms.GroupBox group:
                group.BackColor = window;
                group.ForeColor = ColorTranslator.FromHtml(theme.HeadingText);
                break;
            case Forms.Label label:
                label.BackColor = Color.Transparent;
                label.ForeColor = foreground;
                break;
            case Forms.Button button:
                button.FlatStyle = Forms.FlatStyle.Flat;
                button.UseVisualStyleBackColor = false;
                button.BackColor = button.Name switch
                {
                    "SaveButton" => ColorTranslator.FromHtml(theme.AccentText),
                    "RestoreDefaultsButton" => ColorTranslator.FromHtml(theme.TileHoverBackground),
                    _ => normalTile
                };
                button.ForeColor = GetContrastColor(button.BackColor);
                button.FlatAppearance.BorderColor = border;
                break;
            case Forms.ComboBox combo:
                combo.BackColor = normalTile;
                combo.ForeColor = foreground;
                break;
            case Forms.NumericUpDown numeric:
                numeric.BackColor = normalTile;
                numeric.ForeColor = foreground;
                break;
            case Forms.CheckBox checkBox:
                checkBox.BackColor = window;
                checkBox.ForeColor = foreground;
                break;
            case Forms.Panel panel:
                panel.BackColor = window;
                panel.ForeColor = foreground;
                break;
            default:
                control.BackColor = window;
                control.ForeColor = foreground;
                break;
        }

        foreach (Forms.Control child in control.Controls)
            ApplyDialogTheme(child, theme);
    }

    private static Color GetContrastColor(Color background)
    {
        static double Linearize(byte channel)
        {
            var value = channel / 255.0;
            return value <= 0.04045
                ? value / 12.92
                : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        var luminance =
            0.2126 * Linearize(background.R)
            + 0.7152 * Linearize(background.G)
            + 0.0722 * Linearize(background.B);
        return (luminance + 0.05) / 0.05 >= 1.05 / (luminance + 0.05)
            ? Color.Black
            : Color.White;
    }

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
