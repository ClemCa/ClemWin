using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace ClemWin
{
    public class WindowManager
    {
        internal List<Layout> Layouts = [];
        internal List<Screen> Screens = [];
        internal Layout? GetLayout(int id)
        {
            return Layouts.FirstOrDefault(l => l.Id == id);
        }
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetTopWindow(IntPtr hWnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool BringWindowToTop(IntPtr hWnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("dwmapi.dll", CharSet = CharSet.Unicode)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, int attrValue, int attrSize);
        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
        [DllImport("user32.dll")]
        static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")]
        static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll")]
        static extern bool IsZoomed(IntPtr hWnd);
        [DllImport("user32.dll")]
        static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);
        [DllImport("user32.dll")]
        static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEXA lpmi);
        [DllImport("user32.dll")]
        static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);
        private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFOEXA
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
            public MONITORINFOEXA()
            {
                cbSize = Marshal.SizeOf<MONITORINFOEXA>();
                rcMonitor = new RECT();
                rcWork = new RECT();
                dwFlags = 0;
                szDevice = string.Empty;
            }
        }
        private static readonly IntPtr HWND_TOP = new IntPtr(0);
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private const uint GW_HWNDNEXT = 2;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_FRAMECHANGED = 0x0020;
        private const int SWP_SHOWWINDOW = 0x0040;
        private const int RDW_INVALIDATE = 0x0001;
        private const int RDW_UPDATENOW = 0x0100;
        private const int DWMWA_TRANSITIONS_FORCEDISABLED = 3;
        private const int SW_MAXIMIZE = 3;
        private const int SW_MINIMIZE = 6;
        private const int SW_RESTORE = 9;
        private const int SW_NORMAL = 1;
        private const int GWL_STYLE = -16;
        private const nint WS_OVERLAPPED = 0x00000000;
        private const nint WS_CAPTION = 0x00C00000;
        private const nint WS_SYSMENU = 0x00080000;
        private const nint WS_THICKFRAME = 0x00040000;
        private const nint WS_MINIMIZEBOX = 0x00020000;
        private const nint WS_MAXIMIZEBOX = 0x00010000;
        private const nint WS_OVERLAPPEDWINDOW = (WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX);
        private const long WS_POPUP = 0x80000000L;
        public void SaveLayout(int id)
        {
            Layout? layout = GetLayout(id);
            if (layout == null)
            {
                layout = new Layout(id);
                Layouts.Add(layout);
            }
            layout.Tiles.Clear();
            List<(IntPtr handle, Process process, Bounds bounds, int zIndex)> allWindowsOrdered = new();
            IntPtr topWindow = GetTopWindow(IntPtr.Zero);
            while (topWindow != IntPtr.Zero)
            {
                GetWindowThreadProcessId(topWindow, out uint processId);
                var process = Process.GetProcessById((int)processId);
                if (process.MainWindowHandle != IntPtr.Zero && !string.IsNullOrEmpty(process.MainWindowTitle) && process.MainWindowHandle == topWindow)
                {
                    var match = allWindowsOrdered.Any(w => w.handle == process.MainWindowHandle);
                    if (match)
                    {
                        Console.WriteLine($"!!! Duplicate window found: {process.MainWindowTitle} ({process.ProcessName})");
                    }
                    allWindowsOrdered.Add((process.MainWindowHandle, process, GetWindowBounds(process.MainWindowHandle), allWindowsOrdered.Count));
                }
                topWindow = GetWindow(topWindow, GW_HWNDNEXT); // next by Z order
            }
            foreach (var (handle, process, bounds, zIndex) in allWindowsOrdered)
            {
                TileMode mode = TileMode.Normal;
                // is it minimized?
                if (IsIconic(handle))
                {
                    mode = TileMode.Minimized;
                }
                else if ((GetWindowLongPtr(handle, GWL_STYLE) & WS_POPUP) != 0)
                {
                    mode = TileMode.Fullscreen;
                }
                else if (IsZoomed(handle))
                {
                    mode = TileMode.Maximized;
                }
                Tile tile = layout.GetMatchingTile(mode, bounds) ?? new(mode, bounds);
                Window window = new(process.MainWindowTitle, process.ProcessName, process.Id.ToString(), handle, zIndex);
                Console.WriteLine($"Window: {window.Title}, Process: {window.ProcessName}, Mode: {mode}, Bounds: {bounds.Left}, {bounds.Top}, {bounds.Right}, {bounds.Bottom}");
                tile.Windows.Add(window);
                layout.Tiles.Add(tile);
            }
        }

        public void RestoreLayout(int id)
        {
            Layout? layout = Layouts.FirstOrDefault(l => l.Id == id);
            if (layout == null)
            {
                return; // Layout not found
            }
            List<(Tile tile, Window window)?> allWindows = Process.GetProcesses().
                Where(p => p.MainWindowHandle != IntPtr.Zero && !string.IsNullOrEmpty(p.MainWindowTitle))
                .Select(p => layout.SearchWindow(p.Id.ToString(), p.ProcessName, p.MainWindowHandle, p.MainWindowTitle))
                .Where(p => p.HasValue)
                .OrderByDescending(p => p.Value.window.ZIndex)
                .ToList();
            foreach (var target in allWindows)
            {
                if (target == null)
                    continue;
                SetWindow((nint)target.Value.window.Handle, target.Value.tile, target.Value.window.Title);
            }
        }
        private Screen GetScreen(IntPtr handle)
        {
            MONITORINFOEXA monitorInfo = new();
            monitorInfo.cbSize = Marshal.SizeOf<MONITORINFOEXA>();
            IntPtr monitorHandle = MonitorFromWindow(handle, 0);
            _ = GetMonitorInfo(monitorHandle, ref monitorInfo);
            foreach (var screen in Screens)
            {
                if (screen.Name == monitorInfo.szDevice)
                {
                    return screen;
                }
            }
            var newScreen = new Screen(
                monitorInfo.szDevice,
                monitorInfo.rcMonitor.Left,
                monitorInfo.rcMonitor.Top,
                monitorInfo.rcMonitor.Right - monitorInfo.rcMonitor.Left,
                monitorInfo.rcMonitor.Bottom - monitorInfo.rcMonitor.Top
            );
            Screens.Add(newScreen);
            return newScreen;
        }
        private Bounds GetWindowBounds(IntPtr handle)
        {
            Space space = GetWindowSpace(handle);
            Screen screen = GetScreen(handle);
            return screen.FromDesktop(space);
        }
        private Space GetWindowSpace(IntPtr handle)
        {
            RECT rect;
            _ = GetWindowRect(handle, out rect);
            return new Space(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
        }
        private void SetWindow(IntPtr handle, Tile tile, string name)
        {
            // if it hasn't changed we don't do shit
            var currentBounds = GetWindowBounds(handle);
            bool isMinimized = IsIconic(handle);
            bool isMaximized = IsZoomed(handle);
            bool isFullscreen = (GetWindowLongPtr(handle, GWL_STYLE) & WS_POPUP) != 0;
            TileMode currentMode = TileMode.Normal;
            if (isMinimized)
                currentMode = TileMode.Minimized;
            else if (isFullscreen)
                currentMode = TileMode.Fullscreen;
            else if (isMaximized)
                currentMode = TileMode.Maximized;
            if (currentBounds == tile.Bounds && currentMode == tile.Mode)
            {
                // No need to change anything, to avoid unintended flickering
                // Just put forward
                // Console.WriteLine($"Skipping unchanged window: {name} {tile.Mode}, {tile.Bounds.Left}, {tile.Bounds.Top}, {tile.Bounds.Right}, {tile.Bounds.Bottom}");
                Console.WriteLine($"Skipping {name}");
                if (currentMode != TileMode.Minimized)
                {
                    _ = DwmSetWindowAttribute(handle, DWMWA_TRANSITIONS_FORCEDISABLED, 1, sizeof(int));
                    //! windows is fickle so we're using the entire bag of tricks
                    // Set topmost to force it up even when windows doesn't want to
                    _ = SetWindowPos(handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
                    _ = SetWindowPos(handle, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);

                    _ = SetForegroundWindow(handle);

                    if (currentMode == TileMode.Maximized)
                    {
                        _ = ShowWindow(handle, SW_MAXIMIZE);
                    }
                    _ = DwmSetWindowAttribute(handle, DWMWA_TRANSITIONS_FORCEDISABLED, 0, sizeof(int));
                }
                return;
            }
            Console.WriteLine($"Setting window: {name} {tile.Mode}, {tile.Bounds.Left}, {tile.Bounds.Top}, {tile.Bounds.Right}, {tile.Bounds.Bottom}");
            Space space = tile.Bounds.ToDesktop();
            if (tile.Mode != currentMode)
            {
                switch (tile.Mode)
                {
                    case TileMode.Normal:
                        if (isFullscreen)
                            _ = SetWindowLong(handle, GWL_STYLE, WS_OVERLAPPEDWINDOW);
                        _ = ShowWindow(handle, SW_RESTORE);
                        break;
                    case TileMode.Fullscreen:
                        _ = SetWindowLong(handle, GWL_STYLE, new IntPtr(GetWindowLongPtr(handle, GWL_STYLE) & ~WS_POPUP));
                        return;
                    case TileMode.Maximized:
                        _ = SetWindowLong(handle, GWL_STYLE, WS_OVERLAPPEDWINDOW);
                        _ = ShowWindow(handle, SW_MAXIMIZE);
                        break;
                    case TileMode.Minimized:
                        _ = ShowWindow(handle, SW_MINIMIZE);
                        break;
                }
            }
            bool shouldUnmaximize = isMaximized && currentMode == TileMode.Maximized;
            if (shouldUnmaximize)
            {
                _ = ShowWindow(handle, SW_RESTORE);
            }
            _ = SetWindowPos(handle, HWND_TOP,
                space.X, space.Y,
                space.Width, space.Height,
                SWP_FRAMECHANGED | SWP_SHOWWINDOW);

            //! windows is fickle so we're using the entire bag of tricks
            _ = SetWindowPos(handle, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
            // _ = BringWindowToTop(handle);
            _ = SetForegroundWindow(handle);
            if (shouldUnmaximize)
            {
                _ = ShowWindow(handle, SW_MAXIMIZE);
            }
            _ = RedrawWindow(handle, IntPtr.Zero, IntPtr.Zero, RDW_INVALIDATE | RDW_UPDATENOW);
        }

    }

    public class Layout
    {
        [JsonInclude]
        public int Id;
        [JsonInclude]
        public List<Tile> Tiles = [];
        public Layout(int id)
        {
            Id = id;
        }
        public Tile? GetMatchingTile(TileMode mode, Bounds bounds)
        {
            foreach (var tile in Tiles)
            {
                if (tile.Mode != mode)
                {
                    continue;
                }
                if (tile.Bounds == bounds)
                {
                    return tile;
                }
            }
            return null;
        }
        public Tile? SearchTile(string processID, string processName, nint handle, string title)
        {
            (Tile tile, Window window)? result = SearchWindow(processID, processName, handle, title);
            if (result != null)
            {
                return result.Value.tile;
            }
            return null;
        }
        internal (Tile tile, Window window)? SearchWindow(string processID, string processName, nint handle, string title)
        {
            Window? found = null;
            Tile? foundTile = null;
            MatchLevel level = MatchLevel.NoMatch;
            foreach (var tile in Tiles)
            {
                MatchLevel found_level = tile.Search(processID, processName, title, handle, out Window? result);
                if (found_level < level)
                {
                    found = result;
                    foundTile = tile;
                    level = found_level;
                    if (level == MatchLevel.ExactMatch)
                    {
                        break; // Found an exact match, can't find better
                    }
                }
            }
            if (found != null && level != MatchLevel.NoMatch && level != MatchLevel.ExactMatch)
            {
                // refresh window data with the latest info
                found.Title = title;
                found.ProcessID = processID;
                found.Handle = handle;
                //! not updating process name here
                // found.ProcessName = processName;
            }
            return found != null && foundTile != null ? (foundTile, found) : null;
        }
    }
    public class Window(string title, string processName, string processID, long handle, int zIndex)
    {
        [JsonInclude]
        public string Title = title;
        [JsonInclude]
        public string ProcessName = processName;
        [JsonInclude]
        public string ProcessID = processID;
        [JsonInclude]
        public long Handle = handle; // here mainly for same-session usage, for apps that change titles AND have multiple windows (i.e. browser)
        [JsonInclude]
        public int ZIndex = zIndex;
    }

    public class Tile(TileMode mode, Bounds bounds)
    {
        [JsonInclude]
        public TileMode Mode = mode;
        [JsonInclude]
        public Bounds Bounds = bounds;
        [JsonInclude]
        public List<Window> Windows = [];
        MatchLevel GetMatchLevel(string processID, string processName, string title, nint handle, Window window)
        {
            if (window.ProcessID == processID && window.ProcessName == processName && window.Handle == handle) // except title as it can change easily in the same window
            {
                return MatchLevel.ExactMatch;
            }
            if (window.ProcessID == processID && window.ProcessName == processName && window.Title == title) // the same process AND title can help if the same process has several windows
            {
                return MatchLevel.GreatMatch;
            }
            if (window.ProcessID == processID && window.ProcessName == processName)
            {
                return MatchLevel.ProcessMatch;
            }
            if (window.Title == title && window.ProcessName == processName)
            {
                return MatchLevel.TitleMatch;
            }
            if (window.ProcessName == processName)
            {
                return MatchLevel.ProgramMatch;
            }
            return MatchLevel.NoMatch;
        }
        internal MatchLevel Search(string processID, string processName, string title, nint handle, out Window? result)
        {
            result = null;
            if (Windows.Count == 0)
            {
                return MatchLevel.NoMatch; // No windows in this tile
            }
            MatchLevel match_level = MatchLevel.NoMatch;
            foreach (var window in Windows)
            {
                MatchLevel currentMatch = GetMatchLevel(processID, processName, title, handle, window);
                switch (currentMatch)
                {
                    case MatchLevel.ExactMatch:
                        result = window;
                        return MatchLevel.ExactMatch; // Found an exact match
                    case MatchLevel.NoMatch:
                        continue;
                    default:
                        if (currentMatch < match_level)
                        {
                            match_level = currentMatch;
                            result = window; // Update result with the best match found so far
                        }
                        break;
                }
            }
            if (result != null)
            {
                return match_level;
            }
            return MatchLevel.NoMatch;
        }
    }

    public class Screen(string name, int x, int y, int width, int height)
    {
        [JsonInclude]
        public string Name = name;
        [JsonInclude]
        public int X = x;
        [JsonInclude]
        public int Y = y;
        [JsonInclude]
        public int Width = width;
        [JsonInclude]
        public int Height = height;
        public Space ToDesktop(Bounds bounds)
        {
            return new Space(
                X + bounds.Left,
                Y + bounds.Top,
                bounds.Right - bounds.Left,
                bounds.Bottom - bounds.Top
            );
        }
        public Bounds FromDesktop(Space space)
        {
            return new Bounds(
                this,
                space.X - X,
                space.X - X + space.Width,
                space.Y - Y,
                space.Y - Y + space.Height
            );
        }
    }

    public class Bounds(Screen screen, int left, int right, int top, int bottom)
    {
        [JsonInclude]
        public Screen Screen = screen;
        // left, right, top, bottom offset
        [JsonInclude]
        public int Left = left;
        [JsonInclude]
        public int Right = right;
        [JsonInclude]
        public int Top = top;
        [JsonInclude]
        public int Bottom = bottom;
        public Space ToDesktop()
        {
            return Screen.ToDesktop(this);
        }
        public static bool operator ==(Bounds a, Bounds b)
        {
            return a.Screen.Name == b.Screen.Name &&
                a.Left == b.Left &&
                a.Right == b.Right &&
                a.Top == b.Top &&
                a.Bottom == b.Bottom;
        }
        public static bool operator !=(Bounds a, Bounds b)
        {
            return !(a == b);
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Screen.Name, Left, Right, Top, Bottom);
        }
    }
    public class Space(int x, int y, int width, int height)
    {
        public int X = x;
        public int Y = y;
        public int Width = width;
        public int Height = height;
    }
    public enum TileMode
    {
        Normal = 0,
        Fullscreen = 1,
        Maximized = 2,
        Minimized = 3,
    }
    enum MatchLevel
    {
        ExactMatch = 0, // Same handle, same ID, same process, same title
        GreatMatch = 1, // Same ID, same process, same title
        ProcessMatch = 2, // Process match
        TitleMatch = 3, // Title and program match
        ProgramMatch = 4, // Only program match
        NoMatch = 5
    }
}