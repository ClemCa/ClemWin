using System.Configuration;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ClemWin;

static class Program
{
    [STAThread]
    static void Main()
    {
        // already running?
        var mutex = new Mutex(false, "ClemWinWindowManagerMutex");
        if (!mutex.WaitOne(0, false))
        {
            MessageBox.Show("ClemWin Window Manager is already running.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        var hotkeyWindow = new HotkeyWindow();
        var whitelist = new WhiteList();
        var windowManager = new Windows(hotkeyWindow, whitelist);
        var overlay = new Overlay(windowManager);
        var searchWindow = new Search(windowManager, overlay, hotkeyWindow);
        var markers = new Markers(overlay, whitelist);
        var icon = new NotifyIcon
        {
            Visible = true,
            ContextMenuStrip = new ContextMenuStrip()
            {
                Items =
                {
                    new ToolStripMenuItem("Search (Win + [Shift / Ctrl] + K)", null, (s, e) => {
                        searchWindow.SearchMode = !searchWindow.SearchMode;
                    }),
                    new ToolStripMenuItem("Open Folder", null, (s, e) => {
                        OpenStorageFolder();
                    }),
                    new ToolStripMenuItem("Exit", null, (s, e) => {
                        Application.Exit();
                    })
                }
            },
            Icon = Utils.IconFromPNG("logo.png") ?? SystemIcons.Application,
            Text = "ClemWin Window Manager",
        };
        icon.Click += (s, e) =>
        {
            if (e is MouseEventArgs mouseEventArgs && mouseEventArgs.Button == MouseButtons.Left)
            {
                OpenStorageFolder();
            }
        };
        Application.Run(overlay);
        Application.ApplicationExit += (s, e) =>
        {
            icon.Dispose();
            mutex.ReleaseMutex();
            mutex.Dispose();
        };
    }
    private static void OpenStorageFolder()
    {
        // Open storage folder
        string storagePath = Storage.GetStoragePath();
        if (Directory.Exists(storagePath))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = storagePath,
                UseShellExecute = true
            });
        }
        else
        {
            MessageBox.Show("Storage folder does not exist.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}