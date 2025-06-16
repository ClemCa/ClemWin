using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ClemWin
{
    public class WindowManager
    {
        internal List<Layout> Layouts = [];
        internal List<Screen> Screens = [];
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
        private const uint GW_HWNDNEXT = 2;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const int SW_MAXIMIZE = 3;
        private const int SW_MINIMIZE = 6;
        private const int SW_RESTORE = 9;
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
            Layout? layout = Layouts.FirstOrDefault(l => l.Id == id);
            if (layout == null)
            {
                layout = new Layout(id);
                Layouts.Add(layout);
            }
            layout.Tiles.Clear();
            var allWindows = Process.GetProcesses()
                .Where(p => p.MainWindowHandle != IntPtr.Zero && !string.IsNullOrEmpty(p.MainWindowTitle))
                .Select(p => (handle: p.MainWindowHandle, process: p, bounds: GetWindowBounds(p.MainWindowHandle)));
            foreach (var (handle, process, bounds) in allWindows)
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
                Window window = new(process.MainWindowTitle, process.ProcessName, process.Id.ToString());
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
            List<(IntPtr handle, Tile? tile)> allWindowsOrdered = new();
            IntPtr topWindow = GetTopWindow(IntPtr.Zero);
            while (topWindow != IntPtr.Zero)
            {
                GetWindowThreadProcessId(topWindow, out uint processId);
                var process = Process.GetProcessById((int)processId);
                if (process.MainWindowHandle != IntPtr.Zero && !string.IsNullOrEmpty(process.MainWindowTitle))
                {
                    var tile = layout.SearchTile(process.Id.ToString(), process.ProcessName, process.MainWindowTitle);
                    allWindowsOrdered.Add((process.MainWindowHandle, tile));
                }
                topWindow = GetWindow(topWindow, GW_HWNDNEXT); // next by Z order
            }
            // var allWindows = Process.GetProcesses()
            //     .Where(p => p.MainWindowHandle != IntPtr.Zero)
            //     .OrderBy(p => p.)
            //     .Select(p => (handle: p.Handle, tile: layout.SearchTile(p.Id.ToString(), p.ProcessName, p.MainWindowTitle)))
            foreach (var (handle, tile) in allWindowsOrdered)
            {
                if (tile == null)
                    continue;
                SetWindow(handle, tile);
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
        private void SetWindow(IntPtr handle, Tile tile)
        {
            // if it hasn't changed we don't do shit
            var currentBounds = GetWindowBounds(handle);
            bool isMinimized = IsIconic(handle);
            bool isMaximized = IsZoomed(handle);
            bool isFullscreen = (GetWindowLongPtr(handle, GWL_STYLE) & WS_POPUP) != 0;
            if (currentBounds == tile.Bounds && isMinimized == (tile.Mode == TileMode.Minimized)
            && isMaximized == (tile.Mode == TileMode.Maximized) && isFullscreen == (tile.Mode == TileMode.Fullscreen))
            {
                return; // No need to change anything, to avoid unintended flickering
            }
            Console.WriteLine($"Setting window: {tile.Mode}, {tile.Bounds.Left}, {tile.Bounds.Top}, {tile.Bounds.Right}, {tile.Bounds.Bottom}");
            Space space = tile.Bounds.ToDesktop();
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
            uint flags = SWP_NOACTIVATE | tile.Mode switch
            {
                TileMode.Maximized => SWP_NOZORDER | SWP_NOSIZE | SWP_NOMOVE,
                TileMode.Minimized => SWP_NOZORDER | SWP_NOSIZE | SWP_NOMOVE,
                _ => 0
            };
            _ = SetWindowPos(handle, HWND_TOP,
                space.X, space.Y,
                space.Width, space.Height,
                flags);
        }

    }

    class Layout
    {
        public int Id;
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
        public Tile? SearchTile(string processID, string processName, string title)
        {
            (Tile tile, Window window)? result = SearchWindow(processID, processName, title);
            if (result != null)
            {
                return result.Value.tile;
            }
            return null;
        }
        public (Tile tile, Window window)? SearchWindow(string processID, string processName, string title)
        {
            Window? found = null;
            Tile? foundTile = null;
            MatchLevel level = MatchLevel.NoMatch;
            foreach (var tile in Tiles)
            {
                MatchLevel found_level = tile.Search(processID, processName, title, out Window? result);
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
            return found != null && foundTile != null ? (foundTile, found) : null;
        }
    }
    class Window(string title, string processName, string processID)
    {
        public string Title = title;
        public string ProcessName = processName;
        public string ProcessID = processID;
    }

    class Tile(TileMode mode, Bounds bounds)
    {
        public TileMode Mode = mode;
        public Bounds Bounds = bounds;
        public List<Window> Windows = [];
        MatchLevel GetMatchLevel(string processID, string processName, string title, Window window)
        {
            if (window.ProcessID == processID && window.ProcessName == processName && window.Title == title)
            {
                return MatchLevel.ExactMatch;
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
        public MatchLevel Search(string processID, string processName, string title, out Window? result)
        {
            result = null;
            if (Windows.Count == 0)
            {
                return MatchLevel.NoMatch; // No windows in this tile
            }
            MatchLevel match_level = MatchLevel.NoMatch;
            foreach (var window in Windows)
            {
                MatchLevel currentMatch = GetMatchLevel(processID, processName, title, window);
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

    class Screen(string name, int x, int y, int width, int height)
    {
        public string Name = name;
        public int ID = 0;
        public int X = x;
        public int Y = y;
        public int Width = width;
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

    class Bounds(Screen screen, int left, int right, int top, int bottom)
    {
        public Screen Screen = screen;
        // left, right, top, bottom offset
        public int Left = left;
        public int Right = right;
        public int Top = top;
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
    class Space(int x, int y, int width, int height)
    {
        public int X = x;
        public int Y = y;
        public int Width = width;
        public int Height = height;
    }
    enum TileMode
    {
        Normal = 0,
        Fullscreen = 1,
        Maximized = 2,
        Minimized = 3,
    }
    enum MatchLevel
    {
        ExactMatch = 0, // Same ID, same process, same title
        ProcessMatch = 1, // Process match
        TitleMatch = 2, // Title and program match
        ProgramMatch = 3, // Only program match
        NoMatch = 4
    }
}