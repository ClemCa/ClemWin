using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ClemWin
{
    public static class Fetcher
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetTopWindow(IntPtr hWnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        private const uint GW_HWNDNEXT = 2;
        public static List<(IntPtr handle, Process process, int zIndex)> GetWindowsOrdered()
        {
            List<(IntPtr handle, Process process, int zIndex)> allWindowsOrdered = new();
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
                    allWindowsOrdered.Add((process.MainWindowHandle, process, allWindowsOrdered.Count));
                }
                topWindow = GetWindow(topWindow, GW_HWNDNEXT); // next by Z order
            }
            return allWindowsOrdered;
        }

        public static List<(IntPtr handle, Process process)> GetWindows()
        {
            return Process.GetProcesses()
                .Where(p => p.MainWindowHandle != IntPtr.Zero && !string.IsNullOrEmpty(p.MainWindowTitle))
                .Select(p => (p.MainWindowHandle, p))
                .ToList();
        }

        public static (IntPtr handle, Process process) GetCurrentWindow()
        {
            IntPtr topWindow = GetTopWindow(IntPtr.Zero);
            GetWindowThreadProcessId(topWindow, out uint processId);
            Process process = Process.GetProcessById((int)processId);
            while (topWindow != IntPtr.Zero && (process.MainWindowHandle == IntPtr.Zero || string.IsNullOrEmpty(process.MainWindowTitle)))
            {
                topWindow = GetWindow(topWindow, GW_HWNDNEXT);
                if (topWindow != IntPtr.Zero)
                {
                    GetWindowThreadProcessId(topWindow, out processId);
                    process = Process.GetProcessById((int)processId);
                }
            }
            return (topWindow, process);
        }

        public static List<(Window window, Bounds bounds)> GetWindowsOnScreen(Windows manager)
        {
            var windows = GetWindowsOrdered();
            List<Bounds> occupiedBounds = new();
            List<(Window window, Bounds bounds)> windowsOnScreen = new();
            foreach (var w in windows)
            {
                Window window = new(w.process.MainWindowTitle, w.process.ProcessName, w.process.Id.ToString(), w.process.MainWindowHandle, w.zIndex);
                var bounds = manager.GetWindowBounds(w.handle);
                if (DoesWindowCollide(window, bounds, occupiedBounds))
                {
                    continue; // Skip windows that collide with others, only draw frontmost windows
                }
                occupiedBounds.Add(bounds);
                windowsOnScreen.Add((window, bounds));
            }
            return windowsOnScreen;
        }

        public static SearchResult[] GetBySearch(string searchText)
        {
            var windows = GetWindowsOrdered()
                .Select(w => new SearchResult(w.handle, w.process, w.zIndex))
                .Do(w => w.CalculateScore(searchText))
                .Where(w => w.Score > 0)
                .OrderByDescending(w => w.Score)
                .ToArray();
            return windows;
        }
        private static bool DoesWindowCollide(Window window, Bounds bounds, List<Bounds> occupiedBounds)
        {
            if (occupiedBounds.Count == 0) return false;
            foreach (var b in occupiedBounds)
            {
                if (bounds.IntersectsWith(b))
                {
                    return true;
                }
            }
            return false;
        }
    }
    public class SearchResult
        {
            public IntPtr Handle { get; }
            public Process Process { get; }
            public string Title { get; }
            public string ProcessName { get; }
            public int ZIndex { get; }
            public int Score { get; private set; }
            public SearchResult(IntPtr handle, Process process, int zIndex)
            {
                Handle = handle;
                Process = process;
                Title = process.MainWindowTitle;
                ProcessName = process.ProcessName;
                ZIndex = zIndex;
            }
            public void CalculateScore(string searchText)
            {
                Score = 0;
                Score += Searcher.ScoreSearch(ProcessName, searchText) * 5; // More valuable
                Score += Searcher.ScoreSearch(Title, searchText) * 4; // Less valuable
                if (Process.MainModule?.FileVersionInfo.ProductName != null)
                    Score += Searcher.ScoreSearch(Process.MainModule.FileVersionInfo.ProductName, searchText) * 3;
                if (Process.MainModule?.FileName != null)
                    Score += Searcher.ScoreSearch(Process.MainModule.FileName, searchText); // Least valuable
            }
        }
}