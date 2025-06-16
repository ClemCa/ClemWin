namespace ClemWin
{
    public class WindowManager
    {
        public void SaveLayout(int id)
        {
        }

        public void RestoreLayout(int id)
        {
        }
    }

    enum MatchLevel
    {
        ExactMatch = 0, // Same ID, same process, same title
        ProcessMatch = 1, // Process match
        TitleMatch = 2, // Title and program match
        ProgramMatch = 3, // Only program match
        NoMatch = 4
    }
    class Layout
    {
        public int Id;
        public List<Tile> Tiles = [];
        public Layout(int id)
        {
            Id = id;
        }
        public Window? SearchWindow(string processID, string processName, string title)
        {
            Window? found = null;
            MatchLevel level = MatchLevel.NoMatch;
            foreach (var tile in Tiles)
            {
                MatchLevel found_level = tile.Search(processID, processName, title, out Window? result);
                if (found_level < level)
                {
                    found = result;
                    level = found_level;
                    if (level == MatchLevel.ExactMatch)
                    {
                        break; // Found an exact match, can't find better
                    }
                }
            }
            return found;
        }
    }

    class Window(string title, string processName, string processID)
    {
        public string Title = title;
        public string ProcessName = processName;
        public string ProcessID = processID;
    }

    class Tile(Bounds bounds)
    {
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

    class Screen(int width, int height) {
        public int Width = width;
        public int Height = height;
    }

    class Bounds(Screen screen, int left, int right, int top, int bottom)
    {
        public Screen Screen = screen;
        // left, right, top, bottom offset
        public int Left = left;
        public int Right = right;
        public int Top = top;
        public int Bottom = bottom;
    }
}