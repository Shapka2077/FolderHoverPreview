using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;

namespace FolderHoverPreview;

internal readonly record struct DesktopHit(string Name, double Left, double Top, double Right, double Bottom);

internal sealed class DesktopInspector
{
    private const int DwmwaCloaked = 14;
    private const uint SmtoAbortIfHung = 0x0002;

    private DesktopHit[] _items = [];
    private long _refreshedAt = -2000;
    private IntPtr _cachedListView;
    private int _cachedRevision = -1;
    private IntPtr _tooltip;
    private readonly HashSet<IntPtr> _decoratedMenus = [];
    private HashSet<uint> _snippingProcessIds = [];
    private long _snippingProcessIdsCheckedAt = -2000;

    public DesktopHit? FolderAt(NativeMethods.Point point, IReadOnlyDictionary<string, string> folders, int layoutRevision)
    {
        var item = ItemAt(point, layoutRevision);
        return item is { } hit && folders.ContainsKey(hit.Name) ? hit : null;
    }

    public string? ItemNameAt(NativeMethods.Point point, int layoutRevision) =>
        ItemAt(point, layoutRevision)?.Name;

    private DesktopHit? ItemAt(NativeMethods.Point point, int layoutRevision)
    {
        var listView = DesktopListViewHandle();
        var now = Environment.TickCount64;
        if (now - _refreshedAt > 2000 || listView != _cachedListView || layoutRevision != _cachedRevision)
            RefreshItems(listView, layoutRevision, now);

        foreach (var item in _items)
        {
            if (point.X >= item.Left && point.X <= item.Right
                && point.Y >= item.Top && point.Y <= item.Bottom)
            {
                return item;
            }
        }

        return null;
    }

    public bool IsObscured(NativeMethods.Point point, IntPtr ignoredWindow)
    {
        var window = NativeMethods.GetTopWindow(IntPtr.Zero);
        for (var count = 0; window != IntPtr.Zero && count < 4096; count++)
        {
            var current = window;
            window = NativeMethods.GetWindow(current, NativeMethods.GwHwndNext);

            if (current == ignoredWindow || !NativeMethods.IsWindowVisible(current))
                continue;

            var className = GetClassName(current);
            if (className is "Progman" or "WorkerW")
                return false;

            if (NativeMethods.IsIconic(current)
                || !NativeMethods.GetWindowRect(current, out var rect)
                || point.X < rect.Left || point.X >= rect.Right
                || point.Y < rect.Top || point.Y >= rect.Bottom)
            {
                continue;
            }

            if ((NativeMethods.GetWindowLongPtr(current, NativeMethods.GwlExStyle) & NativeMethods.WsExTransparent) != 0)
                continue;

            if (GetWindowText(current).Contains("NVIDIA GeForce Overlay", StringComparison.OrdinalIgnoreCase))
                continue;

            if (NativeMethods.DwmGetWindowAttribute(current, DwmwaCloaked, out var cloaked, sizeof(int)) == 0
                && cloaked != 0)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    public bool IsContextMenuOpen() =>
        IsVisibleWindow("#32768")
        || IsVisibleWindow("Xaml_WindowedPopupClass")
        || IsVisibleWindow("Microsoft.UI.Content.PopupWindowSiteBridge");

    public string? UpdateContextMenuCheckbox(
        string? contextFolderPath,
        IReadOnlyDictionary<string, string> enabledFolders)
    {
        if (contextFolderPath is null)
        {
            var selectedName = SelectedDesktopFolderName();
            if (selectedName is not null)
                contextFolderPath = PreviewConfig.FindDesktopFolderPath(selectedName);
        }

        var enabled = contextFolderPath is not null
            && enabledFolders.Values.Any(path => string.Equals(
                path,
                contextFolderPath,
                StringComparison.OrdinalIgnoreCase));

        var window = NativeMethods.GetTopWindow(IntPtr.Zero);
        for (var count = 0; window != IntPtr.Zero && count < 4096; count++)
        {
            var current = window;
            window = NativeMethods.GetWindow(current, NativeMethods.GwHwndNext);
            if (!NativeMethods.IsWindowVisible(current) || GetClassName(current) != "#32768")
                continue;

            var sent = NativeMethods.SendMessageTimeout(
                current,
                NativeMethods.MnGetHMenu,
                IntPtr.Zero,
                IntPtr.Zero,
                SmtoAbortIfHung,
                50,
                out var menuHandle);
            if (sent == IntPtr.Zero || menuHandle == IntPtr.Zero || _decoratedMenus.Contains(menuHandle))
                continue;

            if (ApplyPreviewMenuCheckbox(menuHandle, enabled))
            {
                NativeMethods.RedrawWindow(
                    current,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    NativeMethods.RedrawInvalidateNow);
                _decoratedMenus.Add(menuHandle);
            }
        }

        return contextFolderPath;
    }

    public void ResetDecoratedMenus() => _decoratedMenus.Clear();

    public bool IsSnippingUiActive()
    {
        if (IsVisibleWindow("Microsoft-Windows-SnipperToolbar"))
            return true;

        RefreshSnippingProcessIds();
        if (_snippingProcessIds.Count == 0)
            return false;

        var window = NativeMethods.GetTopWindow(IntPtr.Zero);
        for (var count = 0; window != IntPtr.Zero && count < 4096; count++)
        {
            var current = window;
            window = NativeMethods.GetWindow(current, NativeMethods.GwHwndNext);
            if (!NativeMethods.IsWindowVisible(current))
                continue;

            NativeMethods.GetWindowThreadProcessId(current, out var processId);
            if (_snippingProcessIds.Contains(processId))
                return true;
        }

        return false;
    }

    private void RefreshSnippingProcessIds()
    {
        var now = Environment.TickCount64;
        if (now - _snippingProcessIdsCheckedAt < 2000)
            return;

        _snippingProcessIdsCheckedAt = now;
        var processIds = new HashSet<uint>();
        try
        {
            foreach (var process in Process.GetProcessesByName("ScreenClippingHost"))
            {
                using (process)
                    processIds.Add((uint)process.Id);
            }
        }
        catch (ArgumentException) { }
        catch (InvalidOperationException) { }
        catch (Win32Exception) { }

        _snippingProcessIds = processIds;
    }

    public void SetDesktopTooltipSuppressed(bool suppress)
    {
        if (!suppress)
        {
            if (_tooltip != IntPtr.Zero && NativeMethods.IsWindow(_tooltip))
                SendTooltipMessage(_tooltip, NativeMethods.TtmActivate, new IntPtr(1));
            _tooltip = IntPtr.Zero;
            return;
        }

        if (_tooltip != IntPtr.Zero && NativeMethods.IsWindow(_tooltip))
            return;

        var listView = DesktopListViewHandle();
        if (listView == IntPtr.Zero)
            return;

        var success = NativeMethods.SendMessageTimeout(
            listView,
            NativeMethods.LvmGetToolTips,
            IntPtr.Zero,
            IntPtr.Zero,
            SmtoAbortIfHung,
            100,
            out var tooltip);
        if (success == IntPtr.Zero || tooltip == IntPtr.Zero)
            return;

        _tooltip = tooltip;
        SendTooltipMessage(tooltip, NativeMethods.TtmActivate, IntPtr.Zero);
        SendTooltipMessage(tooltip, NativeMethods.TtmPop, IntPtr.Zero);
    }

    private void RefreshItems(IntPtr listView, int layoutRevision, long now)
    {
        _refreshedAt = now;
        _cachedListView = listView;
        _cachedRevision = layoutRevision;
        var items = new List<DesktopHit>();

        if (listView != IntPtr.Zero)
        {
            try
            {
                var root = AutomationElement.FromHandle(listView);
                var condition = new PropertyCondition(
                    AutomationElement.ControlTypeProperty,
                    ControlType.ListItem);
                var elements = root.FindAll(TreeScope.Children, condition);
                if (elements.Count == 0)
                    elements = root.FindAll(TreeScope.Descendants, condition);

                foreach (AutomationElement element in elements)
                {
                    try
                    {
                        var name = element.Current.Name.Trim();
                        var rect = element.Current.BoundingRectangle;
                        if (name.Length > 0 && !rect.IsEmpty)
                            items.Add(new DesktopHit(name, rect.Left, rect.Top, rect.Right, rect.Bottom));
                    }
                    catch (ElementNotAvailableException) { }
                    catch (InvalidOperationException) { }
                    catch (COMException) { }
                }
            }
            catch (ElementNotAvailableException) { }
            catch (InvalidOperationException) { }
            catch (COMException) { }
        }

        _items = items.ToArray();
    }

    private static string? SelectedDesktopFolderName()
    {
        var listView = DesktopListViewHandle();
        if (listView == IntPtr.Zero)
            return null;

        try
        {
            var root = AutomationElement.FromHandle(listView);
            var condition = new PropertyCondition(
                AutomationElement.ControlTypeProperty,
                ControlType.ListItem);
            var elements = root.FindAll(TreeScope.Children, condition);
            if (elements.Count == 0)
                elements = root.FindAll(TreeScope.Descendants, condition);

            foreach (AutomationElement element in elements)
            {
                try
                {
                    var selected = element.GetCurrentPropertyValue(SelectionItemPattern.IsSelectedProperty);
                    if (selected is true)
                        return element.Current.Name.Trim();
                }
                catch (ElementNotAvailableException) { }
                catch (InvalidOperationException) { }
                catch (COMException) { }
            }
        }
        catch (ElementNotAvailableException) { }
        catch (InvalidOperationException) { }
        catch (COMException) { }

        return null;
    }

    private static bool ApplyPreviewMenuCheckbox(IntPtr menuHandle, bool enabled)
    {
        var itemCount = NativeMethods.GetMenuItemCount(menuHandle);
        if (itemCount < 1)
            return false;

        for (var position = 0; position < itemCount; position++)
        {
            var label = new StringBuilder(512);
            var length = NativeMethods.GetMenuString(
                menuHandle,
                (uint)position,
                label,
                label.Capacity,
                NativeMethods.MfByPosition);
            if (length <= 0
                || !label.ToString().Replace("&", string.Empty, StringComparison.Ordinal)
                    .Equals("Превью при наведении", StringComparison.Ordinal))
            {
                continue;
            }

            var flags = NativeMethods.MfByPosition | (enabled ? NativeMethods.MfChecked : 0);
            return NativeMethods.CheckMenuItem(menuHandle, (uint)position, flags) != uint.MaxValue;
        }

        return false;
    }

    private static IntPtr DesktopListViewHandle()
    {
        var progman = NativeMethods.FindWindow("Progman", null);
        var listView = FindDesktopListView(progman);
        if (listView != IntPtr.Zero)
            return listView;

        var worker = IntPtr.Zero;
        for (var count = 0; count < 128; count++)
        {
            worker = NativeMethods.FindWindowEx(IntPtr.Zero, worker, "WorkerW", null);
            if (worker == IntPtr.Zero)
                break;

            listView = FindDesktopListView(worker);
            if (listView != IntPtr.Zero)
                return listView;
        }

        return IntPtr.Zero;
    }

    private static IntPtr FindDesktopListView(IntPtr host)
    {
        if (host == IntPtr.Zero)
            return IntPtr.Zero;

        var view = NativeMethods.FindWindowEx(host, IntPtr.Zero, "SHELLDLL_DefView", null);
        return view == IntPtr.Zero
            ? IntPtr.Zero
            : NativeMethods.FindWindowEx(view, IntPtr.Zero, "SysListView32", null);
    }

    private static string GetClassName(IntPtr window)
    {
        var name = new StringBuilder(256);
        return NativeMethods.GetClassName(window, name, name.Capacity) > 0 ? name.ToString() : string.Empty;
    }

    private static string GetWindowText(IntPtr window)
    {
        var text = new StringBuilder(512);
        return NativeMethods.GetWindowText(window, text, text.Capacity) > 0 ? text.ToString() : string.Empty;
    }

    private static bool IsVisibleWindow(string className)
    {
        var window = NativeMethods.FindWindow(className, null);
        return window != IntPtr.Zero && NativeMethods.IsWindowVisible(window);
    }

    private static void SendTooltipMessage(IntPtr tooltip, uint message, IntPtr wParam)
    {
        NativeMethods.SendMessageTimeout(
            tooltip,
            message,
            wParam,
            IntPtr.Zero,
            SmtoAbortIfHung,
            100,
            out _);
    }
}
