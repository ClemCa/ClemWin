using System.Runtime.InteropServices;

namespace ClemWin;
public enum HotkeyModifiers
{
    Alt = 1,
    Ctrl = 2,
    Shift = 4,
    Win = 8
}
public enum KeyCode
{
    Numpad0 = 0x60,
    Numpad1 = 0x61,
    Numpad2 = 0x62,
    Numpad3 = 0x63,
    Numpad4 = 0x64,
    Numpad5 = 0x65,
    Numpad6 = 0x66,
    Numpad7 = 0x67,
    Numpad8 = 0x68,
    Numpad9 = 0x69,
    Space = 0x20,
    Delete = 0x2E,
    Enter = 0x0D,
    K = 0x4B,
}

public class HotkeyWindow : NativeWindow
{    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vlc);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    private Dictionary<int, IHotkeyReceiver> hotkeyReceivers = new Dictionary<int, IHotkeyReceiver>();
    private Dictionary<int, Action<int>> hotkeyActions = new Dictionary<int, Action<int>>();
    private int cursor = 0;
    public HotkeyWindow()
    {
        CreateHandle(new CreateParams());
    }
    // Destructor
    ~HotkeyWindow()
    {
        foreach (var key in hotkeyReceivers.Keys)
        {
            UnregisterHotKey(Handle, key);
        }
        foreach (var key in hotkeyActions.Keys)
        {
            UnregisterHotKey(Handle, key);
        }
    }
    public int RegisterHotkey(HotkeyModifiers modifiers, KeyCode key, IHotkeyReceiver receiver)
    {
        RegisterHotKey(Handle, cursor, (int)modifiers, (int)key);
        hotkeyReceivers.Add(cursor, receiver);
        return cursor++;
    }
    public int RegisterHotkey(HotkeyModifiers modifiers, KeyCode key, Action<int> action)
    {
        RegisterHotKey(Handle, cursor, (int)modifiers, (int)key);
        hotkeyActions.Add(cursor, action);
        return cursor++;
    }
    public int RegisterHotkey(HotkeyModifiers modifiers, KeyCode key, Action action)
    {
        return RegisterHotkey(modifiers, key, (id) => action());
    }
    public void SimulateHotkey(int id)
    {
        Console.WriteLine($"Simulating hotkey: {id}");
        var message = new Message
        {
            Msg = 0x0312, // WM_HOTKEY
            WParam = new IntPtr(id),
            LParam = IntPtr.Zero,
            HWnd = Handle
        };
        WndProc(ref message);
    }
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0312)
        {
            int id = m.WParam.ToInt32();
            hotkeyReceivers.TryGetValue(id, out var receiver);
            if (receiver != null)
            {
                receiver.HotkeyPressed(id);
                return;
            }
            hotkeyActions.TryGetValue(id, out var action);
            if (action != null)
            {
                action(id);
                return;
            }
            Console.WriteLine($"Hotkey {id} pressed, no receiver or action found.");
        }
        base.WndProc(ref m);
    }
}