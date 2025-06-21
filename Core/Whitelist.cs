namespace ClemWin
{
    public class WhiteListManager
    {
        private List<Window> _whitelist = new();
        public bool WhiteListMode = false;

        public void Toggle(Window window)
        {
            if (!WhiteListMode)
            {
                WhiteListMode = true;
            }
            if (_whitelist == null)
            {
                _whitelist = [];
            }
            if (_whitelist.RemoveAll((w) => w.Handle == window.Handle) > 0)
            {
                return;
            }
            else
            {
                _whitelist.Add(window);
            }
        }
        public bool InWhitelist(long handle)
        {
            if (!WhiteListMode)
            {
                return true;
            }
            if (_whitelist == null || _whitelist.Count == 0)
            {
                return false;
            }
            return _whitelist.Any((w) => w.Handle == handle);
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
            return result;
        }
    }
}