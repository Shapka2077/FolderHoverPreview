using System.Diagnostics;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace FolderHoverPreview;

internal sealed class PreviewController : IDisposable
{
    private readonly DesktopInspector _desktop = new();
    private readonly PreviewWindow _preview = new();
    private readonly DispatcherTimer _hoverTimer;
    private readonly DispatcherTimer _configTimer;
    private readonly DispatcherTimer _rightButtonTimer;
    private readonly Forms.NotifyIcon _trayIcon;
    private readonly System.Drawing.Icon _trayImage;
    private readonly List<(Forms.ToolStripMenuItem Item, string Extension)> _formatMenuItems = [];
    private Dictionary<string, string> _folders;
    private PreviewFormatPreferences _formatPreferences;
    private string _configSnapshot;
    private Forms.ToolStripMenuItem? _customFormatsEnabledItem;
    private Forms.ToolStripMenuItem? _configureCustomFormatsItem;
    private string? _currentFolder;
    private long _lastHover;
    private long _lastPreferenceCheck = -1000;
    private long _lastDesktopPointCheck = -1000;
    private long _contextCooldown;
    private long _dragCooldown;
    private int _lastDesktopX = int.MinValue;
    private int _lastDesktopY = int.MinValue;
    private IntPtr _lastDesktopForeground;
    private bool _contextWasOpen;
    private bool _desktopPointerDown;
    private bool _rightButtonDown;
    private bool _customExtensionsDialogOpen;
    private string? _contextFolderPath;
    private int _desktopLayoutRevision;
    private bool _disposed;

    public PreviewController(Dictionary<string, string> folders)
    {
        _folders = folders;
        _formatPreferences = PreviewFormatConfig.Load();
        _configSnapshot = ReadConfigSnapshot();

        _hoverTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        _hoverTimer.Tick += (_, _) =>
        {
            try
            {
                CheckHover();
            }
            catch (Exception error)
            {
                Debug.WriteLine(error);
                HidePreview();
            }
        };
        _configTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
        _configTimer.Tick += (_, _) => RefreshPreferences();
        _rightButtonTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(25) };
        _rightButtonTimer.Tick += (_, _) => CaptureContextFolderOnRightClick();

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Открыть файл настроек", null, (_, _) => OpenConfig());

        var formatsMenu = new Forms.ToolStripMenuItem("Форматы превью");
        string? previousFormatGroup = null;
        foreach (var option in PreviewFormatConfig.CommonFormats)
        {
            if (previousFormatGroup is not null && previousFormatGroup != option.Group)
                formatsMenu.DropDownItems.Add(new Forms.ToolStripSeparator());
            previousFormatGroup = option.Group;

            var formatItem = new Forms.ToolStripMenuItem(option.Label)
            {
                CheckOnClick = true,
                Checked = _formatPreferences.EnabledExtensions.Contains(
                    option.Extension,
                    StringComparer.OrdinalIgnoreCase)
            };
            _formatMenuItems.Add((formatItem, option.Extension));
            formatItem.Click += (_, _) => SaveFormatMenuSelection();
            formatsMenu.DropDownItems.Add(formatItem);
        }

        formatsMenu.DropDownItems.Add(new Forms.ToolStripSeparator());
        _customFormatsEnabledItem = new Forms.ToolStripMenuItem("Использовать свои расширения")
        {
            CheckOnClick = true,
            Checked = _formatPreferences.UseCustomExtensions
        };
        _customFormatsEnabledItem.Click += (_, _) => ToggleCustomFormats();
        formatsMenu.DropDownItems.Add(_customFormatsEnabledItem);

        _configureCustomFormatsItem = new Forms.ToolStripMenuItem();
        _configureCustomFormatsItem.Click += (_, _) => ConfigureCustomFormats();
        formatsMenu.DropDownItems.Add(_configureCustomFormatsItem);
        UpdateFormatMenuChecks();
        menu.Items.Add(formatsMenu);

        var startupItem = new Forms.ToolStripMenuItem("Автозагрузка")
        {
            CheckOnClick = true,
            Checked = StartupRegistration.IsEnabled
        };
        startupItem.Click += (_, _) =>
        {
            try
            {
                StartupRegistration.SetEnabled(startupItem.Checked);
            }
            catch (Exception error)
            {
                startupItem.Checked = StartupRegistration.IsEnabled;
                Forms.MessageBox.Show(
                    $"Не удалось изменить автозагрузку:\n{error.Message}",
                    "FolderHoverPreview",
                    Forms.MessageBoxButtons.OK,
                    Forms.MessageBoxIcon.Error);
            }
        };
        menu.Items.Add(startupItem);
        var removeContextMenuItem = new Forms.ToolStripMenuItem(
            "Удалить пункт «Превью при наведении» из меню папок");
        removeContextMenuItem.Click += (_, _) =>
        {
            try
            {
                App.RemoveContextMenu();
            }
            catch (Exception error)
            {
                Forms.MessageBox.Show(
                    $"Не удалось удалить пункт меню:\n{error.Message}",
                    "FolderHoverPreview",
                    Forms.MessageBoxButtons.OK,
                    Forms.MessageBoxIcon.Error);
            }
        };
        menu.Items.Add(removeContextMenuItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Выход", null, (_, _) => System.Windows.Application.Current.Shutdown());
        menu.Opening += (_, _) =>
        {
            RefreshPreferences(force: true);
            startupItem.Checked = StartupRegistration.IsEnabled;
            UpdateFormatMenuChecks();
        };
        _trayImage = TrayIconFactory.Create();
        _trayIcon = new Forms.NotifyIcon
        {
            Icon = _trayImage,
            Text = "FolderHoverPreview",
            ContextMenuStrip = menu,
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) => OpenConfig();
    }

    public void Start()
    {
        _hoverTimer.Start();
        _configTimer.Start();
        _rightButtonTimer.Start();
    }

    private void CaptureContextFolderOnRightClick()
    {
        var isDown = (NativeMethods.GetAsyncKeyState(0x02) & 0x8000) != 0;
        if (isDown && !_rightButtonDown)
        {
            _contextFolderPath = null;
            NativeMethods.GetCursorPos(out var point);
            var previewHandle = new System.Windows.Interop.WindowInteropHelper(_preview).Handle;
            if (!_desktop.IsObscured(point, previewHandle))
            {
                var name = _desktop.ItemNameAt(point, _desktopLayoutRevision);
                if (name is not null)
                    _contextFolderPath = PreviewConfig.FindDesktopFolderPath(name);
            }
        }

        _rightButtonDown = isDown;
    }

    private void CheckHover()
    {
        if (_disposed)
            return;

        RefreshPreferences();
        var now = Environment.TickCount64;
        var leftButtonDown = (NativeMethods.GetAsyncKeyState(0x01) & 0x8000) != 0;

        if (_desktopPointerDown)
        {
            if (leftButtonDown)
            {
                if (_currentFolder is not null)
                    HidePreview();
                return;
            }

            _desktopPointerDown = false;
            _desktopLayoutRevision++;
            _dragCooldown = now + 300;
            _lastDesktopPointCheck = 0;
        }

        if (now < _dragCooldown)
            return;

        if (_desktop.IsContextMenuOpen())
        {
            if (!_contextWasOpen)
                _contextCooldown = now + 600;
            _contextWasOpen = true;
            _contextFolderPath = _desktop.UpdateContextMenuCheckbox(_contextFolderPath, _folders);
            if (_currentFolder is not null)
                HidePreview();
            return;
        }

        if (_contextWasOpen)
        {
            _contextWasOpen = false;
            _contextFolderPath = null;
            _desktop.ResetDecoratedMenus();
        }
        if (now < _contextCooldown)
            return;

        NativeMethods.GetCursorPos(out var point);

        // Do not hide the popup when the user clicks one of its launch tiles.
        if (_preview.IsVisible && _preview.ContainsScreenPoint(point))
        {
            _desktop.SetDesktopTooltipSuppressed(true);
            _lastHover = now;
            return;
        }

        if (_desktop.IsSnippingUiActive())
            return;

        var foreground = NativeMethods.GetForegroundWindow();
        if (!leftButtonDown
            && _currentFolder is null
            && point.X == _lastDesktopX
            && point.Y == _lastDesktopY
            && foreground == _lastDesktopForeground
            && now - _lastDesktopPointCheck < 900)
        {
            return;
        }

        _lastDesktopX = point.X;
        _lastDesktopY = point.Y;
        _lastDesktopForeground = foreground;
        _lastDesktopPointCheck = now;

        if (leftButtonDown && !_desktop.IsObscured(point, IntPtr.Zero))
        {
            _desktopPointerDown = true;
            HidePreview();
            return;
        }

        var hit = _desktop.IsObscured(point, new System.Windows.Interop.WindowInteropHelper(_preview).Handle)
            ? null
            : _desktop.FolderAt(point, _folders, _desktopLayoutRevision);
        _desktop.SetDesktopTooltipSuppressed(hit is not null);

        if (hit is { } folderHit)
        {
            var found = folderHit.Name;
            _lastHover = now;
            if (!StringComparer.OrdinalIgnoreCase.Equals(found, _currentFolder))
            {
                try
                {
                    _preview.ShowFolder(
                        found,
                        _folders[found],
                        point,
                        folderHit,
                        _formatPreferences.GetEnabledExtensionSet());
                    _currentFolder = found;
                }
                catch (Exception error)
                {
                    Debug.WriteLine(error);
                    HidePreview();
                }
            }
        }
        else if (_currentFolder is not null && now - _lastHover > 350)
        {
            HidePreview();
        }
    }

    private void RefreshPreferences(bool force = false)
    {
        var now = Environment.TickCount64;
        if (!force && now - _lastPreferenceCheck < 900)
            return;
        _lastPreferenceCheck = now;

        try
        {
            var snapshot = ReadConfigSnapshot();
            if (snapshot == _configSnapshot)
                return;

            _folders = PreviewConfig.LoadEnabledDesktopFolders();
            _formatPreferences = PreviewFormatConfig.Load();
            _configSnapshot = snapshot;
            _lastDesktopPointCheck = 0;
            HidePreview();
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static string ReadConfigSnapshot()
    {
        var folders = File.Exists(PreviewConfig.ConfigPath)
            ? File.ReadAllText(PreviewConfig.ConfigPath)
            : string.Empty;
        var formats = File.Exists(PreviewFormatConfig.SettingsPath)
            ? File.ReadAllText(PreviewFormatConfig.SettingsPath)
            : string.Empty;
        return $"{folders.Length}:{folders}{formats.Length}:{formats}";
    }

    private void SaveFormatMenuSelection()
    {
        SaveFormatPreferences(new PreviewFormatPreferences
        {
            EnabledExtensions = _formatMenuItems
                .Where(entry => entry.Item.Checked)
                .Select(entry => entry.Extension)
                .ToList(),
            CustomExtensions = _formatPreferences.CustomExtensions,
            UseCustomExtensions = _formatPreferences.UseCustomExtensions
        });
    }

    private void ToggleCustomFormats()
    {
        SaveFormatPreferences(new PreviewFormatPreferences
        {
            EnabledExtensions = _formatPreferences.EnabledExtensions,
            CustomExtensions = _formatPreferences.CustomExtensions,
            UseCustomExtensions = _customFormatsEnabledItem?.Checked == true
        });
    }

    private void ConfigureCustomFormats()
    {
        if (_customExtensionsDialogOpen)
            return;

        _customExtensionsDialogOpen = true;
        try
        {
            var customExtensions = CustomExtensionsDialog.Show(_formatPreferences.CustomExtensions);
            if (customExtensions is null)
                return;

            SaveFormatPreferences(new PreviewFormatPreferences
            {
                EnabledExtensions = _formatPreferences.EnabledExtensions,
                CustomExtensions = customExtensions,
                UseCustomExtensions = _formatPreferences.UseCustomExtensions && customExtensions.Count > 0
            });
        }
        finally
        {
            _customExtensionsDialogOpen = false;
        }
    }

    private void SaveFormatPreferences(PreviewFormatPreferences preferences)
    {
        try
        {
            var normalized = PreviewFormatConfig.Normalize(preferences);
            PreviewFormatConfig.Save(normalized);
            _formatPreferences = normalized;
            _configSnapshot = ReadConfigSnapshot();
            _lastDesktopPointCheck = 0;
            HidePreview();
            UpdateFormatMenuChecks();
        }
        catch (Exception error)
        {
            UpdateFormatMenuChecks();
            Forms.MessageBox.Show(
                $"Не удалось сохранить настройки форматов:\n{error.Message}",
                "FolderHoverPreview",
                Forms.MessageBoxButtons.OK,
                Forms.MessageBoxIcon.Error);
        }
    }

    private void UpdateFormatMenuChecks()
    {
        foreach (var (item, extension) in _formatMenuItems)
        {
            item.Checked = _formatPreferences.EnabledExtensions.Contains(
                extension,
                StringComparer.OrdinalIgnoreCase);
        }

        if (_customFormatsEnabledItem is not null)
        {
            _customFormatsEnabledItem.Enabled = _formatPreferences.CustomExtensions.Count > 0;
            _customFormatsEnabledItem.Checked = _formatPreferences.UseCustomExtensions;
        }
        if (_configureCustomFormatsItem is not null)
        {
            var count = _formatPreferences.CustomExtensions.Count;
            _configureCustomFormatsItem.Text = count == 0
                ? "Настроить свои расширения…"
                : $"Настроить свои расширения… ({count})";
        }
    }

    private void HidePreview()
    {
        if (_preview.IsVisible)
            _preview.Hide();
        _currentFolder = null;
        _desktop.SetDesktopTooltipSuppressed(false);
    }

    private static void OpenConfig()
    {
        try
        {
            PreviewConfig.EnsureCreated();
            Process.Start(new ProcessStartInfo("notepad.exe", $"\"{PreviewConfig.ConfigPath}\"")
            {
                UseShellExecute = true
            });
        }
        catch (Exception) { }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _hoverTimer.Stop();
        _configTimer.Stop();
        _rightButtonTimer.Stop();
        _desktop.SetDesktopTooltipSuppressed(false);
        _preview.Close();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _trayIcon.ContextMenuStrip?.Dispose();
        _trayImage.Dispose();
    }
}
