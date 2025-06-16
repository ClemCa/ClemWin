using System.Runtime.InteropServices;

namespace ClemWin;

static class Program
{
    [STAThread]
    static void Main()
    {
        var icon = new NotifyIcon
        {
            Visible = true,
            ContextMenuStrip = new ContextMenuStrip()
            {
                Items =
                {
                    new ToolStripMenuItem("Exit", null, (s, e) => {
                        Application.Exit();
                    })
                }
            },
            Icon = SystemIcons.Application,
            Text = "ClemWin Window Manager",
        };
        var hotkeyWindow = new HotkeyWindow();
        Application.Run();
    }


}
public class HotkeyWindow : NativeWindow
{
    private WindowManager windowManager;
    private const int MOD_ALT = 0x0001;
    private const int MOD_CONTROL = 0x0002;
    private const int MOD_SHIFT = 0x0004;
    private const int MOD_WIN = 0x0008;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vlc);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    static readonly Dictionary<string, int> hotkeys = new Dictionary<string, int>
    {
        //numpad numbers
        { "1", 0x60 },
        { "2", 0x61 },
        { "3", 0x62 },
        { "4", 0x63 },
        { "5", 0x64 },
        { "6", 0x65 },
        { "7", 0x66 },
        { "8", 0x67 },
        { "9", 0x68 },
        { "0", 0x69 },
    };
    public HotkeyWindow()
    {
        CreateHandle(new CreateParams());
        int size = hotkeys.Count;
        var keys = hotkeys.Keys.ToArray();
        for (int i = 0; i < size; i++)
        {
            RegisterHotKey(Handle, i + size, MOD_WIN | MOD_ALT, hotkeys[keys[i]]); // Save
            RegisterHotKey(Handle, i, MOD_WIN | MOD_CONTROL, hotkeys[keys[i]]); // Go
        }
        windowManager = new WindowManager();
        for (int i = 0; i < size; i++)
        {
            var layout = Storage.LoadData(i, ref windowManager.Screens);
            if (layout == null)
                continue;
            windowManager.Layouts.Add(layout);
        }
    }
    // Destructor
    ~HotkeyWindow()
    {
        int size = hotkeys.Count;
        for (int i = 0; i < size * 2; i++)
        {
            UnregisterHotKey(Handle, i);
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0312)
        {
            int id = m.WParam.ToInt32();
            if (id >= hotkeys.Count)
            {
                id -= hotkeys.Count;
                Console.WriteLine($"Saving layout {id}");
                windowManager.SaveLayout(id);
                var layout = windowManager.GetLayout(id);
                if (layout != null)
                {
                    Storage.SaveData(layout);
                }
            }
            else
            {
                Console.WriteLine($"Restoring layout {id}");
                windowManager.RestoreLayout(id);
                var layout = windowManager.GetLayout(id);
                if (layout != null)
                {
                    Storage.SaveData(layout);
                }
            }
        }
        base.WndProc(ref m);
    }
}