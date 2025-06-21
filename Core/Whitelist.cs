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
            if (_whitelist.RemoveAll((w) => w.Handle == window.Handle && w.ProcessID == window.ProcessID) > 0)
            {
                OnWhitelistUpdated?.Invoke();
                return;
            }
            else
            {
                _whitelist.Add(window);
                OnWhitelistUpdated?.Invoke();
            }
        }
        public bool InWhitelist(Window window)
        {
            return InWhitelist(window.Handle, window.ProcessID);
        }
        public bool InWhitelist(long handle, string processId)
        {
            if (!WhiteListMode)
            {
                return true;
            }
            if (_whitelist == null || _whitelist.Count == 0)
            {
                return false;
            }
            return _whitelist.Any((w) => w.Handle == handle && w.ProcessID == processId);
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