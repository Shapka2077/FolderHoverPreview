using System.Reflection;
using System.Threading;
using System.Windows;
using Microsoft.Win32;

namespace FolderHoverPreview;

public partial class App : Application
{
    private const string InstanceMutexName = @"Local\ShapkaFolderHoverPreviewCSharp";
    private const string ContextMenuRegistryPath = @"Software\Classes\Directory\shell\FolderHoverPreview";
    private Mutex? _instanceMutex;
    private bool _ownsInstanceMutex;
    private PreviewController? _controller;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        if (e.Args.Length >= 2 && e.Args[0].Equals("--toggle", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                PreviewConfig.Toggle(e.Args[1]);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Не удалось изменить настройку предпросмотра:\n{ex.Message}",
                    "FolderHoverPreview",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }

            Shutdown();
            return;
        }

        _instanceMutex = new Mutex(true, InstanceMutexName, out _ownsInstanceMutex);
        if (!_ownsInstanceMutex)
        {
            _instanceMutex.Dispose();
            _instanceMutex = null;
            Shutdown();
            return;
        }

        try
        {
            PreviewConfig.EnsureCreated();
            InstallContextMenu();
            _controller = new PreviewController(PreviewConfig.LoadEnabledDesktopFolders());
            _controller.Start();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Не удалось запустить FolderHoverPreview:\n{ex.Message}",
                "FolderHoverPreview",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _controller?.Dispose();
        if (_ownsInstanceMutex)
        {
            try { _instanceMutex?.ReleaseMutex(); }
            catch (ApplicationException) { }
        }
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }

    private static void InstallContextMenu()
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var commonDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);

        using (var verb = Registry.CurrentUser.CreateSubKey(ContextMenuRegistryPath))
        {
            verb.SetValue(null, "Превью при наведении");
            verb.SetValue(
                "AppliesTo",
                $"System.ParsingPath:~<\"{desktop.TrimEnd('\\')}\\\" OR " +
                $"System.ParsingPath:~<\"{commonDesktop.TrimEnd('\\')}\\\"");
        }

        using (var command = Registry.CurrentUser.CreateSubKey(ContextMenuRegistryPath + @"\command"))
            command.SetValue(null, $"{GetLaunchCommand()} --toggle \"%1\"");

        NativeMethods.SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
    }

    internal static void RemoveContextMenu()
    {
        Registry.CurrentUser.DeleteSubKeyTree(ContextMenuRegistryPath, throwOnMissingSubKey: false);
        NativeMethods.SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
    }

    internal static string GetLaunchCommand()
    {
        var processPath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Не удалось определить путь к приложению.");

        // Also support starting from `dotnet run`; the context-menu verb then
        // invokes the host with this app's assembly.
        if (Path.GetFileNameWithoutExtension(processPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            var entryAssembly = Assembly.GetEntryAssembly();
            var assemblyPath = entryAssembly is null
                ? null
                : Path.Combine(AppContext.BaseDirectory, $"{entryAssembly.GetName().Name}.dll");
            if (assemblyPath is not null && File.Exists(assemblyPath))
                return $"\"{processPath}\" \"{assemblyPath}\"";
        }

        return $"\"{processPath}\"";
    }
}
