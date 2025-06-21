namespace ClemWin
{
    public class WhiteList
    {
        private List<Window> _whitelist = new();
        public bool WhiteListMode = false;
        public static event Action OnWhitelistUpdated = delegate { };
        public void Toggle(Window window)
        {
            WhiteListMode = true;
            _whitelist ??= [];
            if (_whitelist.RemoveAll((w) => w.ProcessName == window.ProcessName && w.ProcessID == window.ProcessID) > 0)
            {
                OnWhitelistUpdated?.Invoke();
                return;
            }
            else
            {
                Console.WriteLine($"Adding {window.ProcessName} ({window.ProcessID}) to whitelist");
                _whitelist.Add(window);
                OnWhitelistUpdated?.Invoke();
            }
        }
        public bool InWhitelist(Window window)
        {
            return InWhitelist(window.ProcessID, window.ProcessName);
        }
        public bool InWhitelist(string processId, string processName)
        {
            if (!WhiteListMode)
            {
                return true;
            }
            if (_whitelist == null || _whitelist.Count == 0)
            {
                return false;
            }
            Console.WriteLine($"Checking whitelist for {processName} ({processId})");
            return _whitelist.Any((w) => w.ProcessName == processName && w.ProcessID == processId);
        }
        public List<Window> GetWhitelist()
        {
            if (!WhiteListMode || _whitelist == null || _whitelist.Count == 0)
            {
                return [];
            }
            return _whitelist;
        }
        public List<Window> Consume()
        {
            WhiteListMode = false;
            var result = _whitelist;
            _whitelist = new List<Window>();
            OnWhitelistUpdated?.Invoke();
            return result;
        }
    }
}