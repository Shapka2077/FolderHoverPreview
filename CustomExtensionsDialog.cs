using Forms = System.Windows.Forms;

namespace FolderHoverPreview;

internal static class CustomExtensionsDialog
{
    public static List<string>? Show(IEnumerable<string> currentExtensions)
    {
        using var dialog = new Forms.Form
        {
            Text = "Свои расширения",
            StartPosition = Forms.FormStartPosition.CenterScreen,
            FormBorderStyle = Forms.FormBorderStyle.FixedDialog,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            ClientSize = new System.Drawing.Size(440, 205)
        };

        var instructions = new Forms.Label
        {
            Text = "Введите расширения файлов через запятую, пробел или новую строку:",
            AutoSize = false,
            Left = 14,
            Top = 14,
            Width = 412,
            Height = 36
        };
        var input = new Forms.TextBox
        {
            Left = 14,
            Top = 54,
            Width = 412,
            Height = 78,
            Multiline = true,
            AcceptsReturn = true,
            ScrollBars = Forms.ScrollBars.Vertical,
            Text = string.Join("; ", currentExtensions)
        };
        var example = new Forms.Label
        {
            Text = "Например: .json; .cfg; .log  (точку можно не писать)",
            AutoSize = true,
            Left = 14,
            Top = 139
        };
        var save = new Forms.Button
        {
            Text = "Сохранить",
            DialogResult = Forms.DialogResult.None,
            Width = 96,
            Height = 30,
            Left = 224,
            Top = 163
        };
        var cancel = new Forms.Button
        {
            Text = "Отмена",
            DialogResult = Forms.DialogResult.Cancel,
            Width = 96,
            Height = 30,
            Left = 330,
            Top = 163
        };

        List<string>? result = null;
        save.Click += (_, _) =>
        {
            try
            {
                result = PreviewFormatConfig.ParseCustomExtensions(input.Text);
                dialog.DialogResult = Forms.DialogResult.OK;
                dialog.Close();
            }
            catch (FormatException error)
            {
                Forms.MessageBox.Show(
                    dialog,
                    error.Message,
                    "FolderHoverPreview",
                    Forms.MessageBoxButtons.OK,
                    Forms.MessageBoxIcon.Warning);
            }
        };

        dialog.Controls.AddRange([instructions, input, example, save, cancel]);
        dialog.AcceptButton = save;
        dialog.CancelButton = cancel;
        return dialog.ShowDialog() == Forms.DialogResult.OK ? result : null;
    }
}
