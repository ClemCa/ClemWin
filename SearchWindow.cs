using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.ComponentModel;


namespace ClemWin
{

    public class SearchWindow : Form
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vlc);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")]
        private static extern int ToUnicode(uint wVirtKey, uint wScanCode, byte[] lpKeyState, [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pwszBuff, int cchBuff, uint wFlags);
        [DllImport("user32.dll")]
        private static extern bool GetKeyboardState(byte[] lpKeyState);
        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);
        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll")]
        private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);
        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);
        // Modifiers
        private const int MOD_CONTROL = 0x0002;
        private const int MOD_WIN = 0x0008;
        private const int MOD_SHIFT = 0x0004;
        // Virtual key code for 'K'
        private const int VK_K = 0x4B;
        private const long WS_POPUP = 0x80000000L;
        private const long WS_CAPTION = 0x40000000L;
        private const long WS_SYSMENU = 0x10000000L;
        // SWP
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_FRAMECHANGED = 0x0020;
        // Layered window attributes
        private const uint LWA_COLORKEY = 0x00000001;
        private (float width, float height) boxSize = (0.4f, 0.2f); // default size for search box (in percentage)
        private const float cornerRadiusPercentage = 0.3f; // default corner radius percentage
        private System.Windows.Forms.Screen targetScreen;
        private BackgroundWindow? backgroundWindow;
        private Rectangle searchBoxRect;
        private RectangleF searchboxSubRect;
        private int cornerRadius;
        private Font font = new Font("Arial", 36, FontStyle.Regular);
        private string searchText = "";
        private bool searchMode = false;
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool SearchMode
        {
            get => searchMode;
            set
            {
                searchMode = value;
                if (searchMode)
                {
                    backgroundWindow?.Show();
                    this.BringToFront();
                    SetWindowLong(Handle, -20, CreateParams.ExStyle);
                }
                else
                    backgroundWindow?.Hide();
                Invalidate(); // trigger repaint when search mode changes
            }
        }
        public SearchWindow()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.TopMost = true;
            this.BackColor = Color.LimeGreen;
            this.TransparencyKey = Color.LimeGreen;
            this.AllowTransparency = true;
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.Manual;
            this.DoubleBuffered = true;

            backgroundWindow = new BackgroundWindow(this);
            this.Owner = backgroundWindow;
            backgroundWindow.Show();
            backgroundWindow.Hide();

            RepositionSearchBox();
            if (targetScreen == null)
            {
                throw new InvalidOperationException("No primary screen found.");
            }
            // register hotkey for toggling search mode
            if (!RegisterHotKey(Handle, 0, MOD_WIN | MOD_CONTROL, VK_K))
            {
                throw new InvalidOperationException("Failed to register hotkey for toggling search mode.");
            }
            RegisterHotKey(Handle, 1, MOD_WIN | MOD_SHIFT, VK_K); // Register another hotkey for future use if needed
        }
        ~SearchWindow()
        {
            // unregister hotkey
            UnregisterHotKey(Handle, 0); // 0 is the id for the hotkey
            UnregisterHotKey(Handle, 1); // 1 is the id for the second hotkey

            // Clean up background window
            if (backgroundWindow != null && !backgroundWindow.IsDisposed)
            {
                backgroundWindow.Dispose();
            }
        }
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style |= unchecked((int)(WS_POPUP | WS_CAPTION | WS_SYSMENU));
                if (searchMode)
                {
                    cp.ExStyle |= 0x80 | 0x80000; // WS_EX_TOOLWINDOW | WS_EX_LAYERED
                }
                else
                {
                    cp.ExStyle |= 0x80 | 0x80000 | 0x20; // WS_EX_TOOLWINDOW | WS_EX_LAYERED | WS_EX_TRANSPARENT
                }
                return cp;
            }
        }
        public CreateParams GetCreateParams()
        {
            return CreateParams;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            RepositionSearchBox();
            DrawSearchBox(e.Graphics);
            ManageKeyboardInput();
        }
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            RepositionSearchBox();
        }
        private void RepositionSearchBox()
        {
            // if not on primary screen, reposition to primary screen
            var primaryScreen = System.Windows.Forms.Screen.PrimaryScreen;
            if (targetScreen == primaryScreen)
            {
                return;
            }
            else if (primaryScreen == null) // could be disconnected
            {
                return;
            }
            else
            {
                int workspaceX = SystemInformation.VirtualScreen.X;
                int workspaceY = SystemInformation.VirtualScreen.Y;
                int workspaceWidth = SystemInformation.VirtualScreen.Width;
                int workspaceHeight = SystemInformation.VirtualScreen.Height;
                Console.WriteLine($"Workspace: {workspaceX}, {workspaceY}, {workspaceWidth}, {workspaceHeight}");
                Bounds = new Rectangle(workspaceX, workspaceY, workspaceWidth, workspaceHeight);

                targetScreen = primaryScreen;
                int XDiff = targetScreen.Bounds.X - workspaceX;
                int YDiff = targetScreen.Bounds.Y - workspaceY;
                float middleX = XDiff + targetScreen.Bounds.Width / 2f;
                float middleY = YDiff + targetScreen.Bounds.Height / 2f;
                searchBoxRect = new Rectangle(
                    (int)(middleX - (targetScreen.Bounds.Width * boxSize.width / 2)),
                    (int)(middleY - (targetScreen.Bounds.Height * boxSize.height / 2)),
                    (int)(targetScreen.Bounds.Width * boxSize.width),
                    (int)(targetScreen.Bounds.Height * boxSize.height)
                );
                cornerRadius = (int)(searchBoxRect.Height * cornerRadiusPercentage);
                searchboxSubRect = new RectangleF(
                    searchBoxRect.X,
                    searchBoxRect.Y + cornerRadius,
                    searchBoxRect.Width,
                    searchBoxRect.Height - cornerRadius
                );
                backgroundWindow?.SetBounds(Bounds);
            }
        }
        private void DrawSearchBox(Graphics g)
        {
            if (!SearchMode)
            {
                return;
            }
            // Draw the search box UI
            g.FillRoundedRectangle(Brushes.Black, searchBoxRect, new Size(cornerRadius, cornerRadius));
            g.FillRectangle(Brushes.Black, searchboxSubRect); // remove the rounded corners on the bottom
            g.DrawString(searchText, font, Brushes.White, new PointF(
                searchBoxRect.Left + 10,
                searchBoxRect.Top + (searchBoxRect.Height - font.Height) / 2f
            ));
        }

        private void ManageKeyboardInput()
        {
            if (SearchMode)
            {
                MouseDown += React_Click;
                // Make it clickable
                SetStyle(ControlStyles.Selectable, true);
                // Take focus
                Activate();
                Focus();
                // backgroundWindow?.Activate();
                // backgroundWindow?.Focus();
                // Show();
                // BringToFront();
                // Select();

            }
            else
            {
                MouseDown -= React_Click;
                // Make it not clickable
                SetStyle(ControlStyles.Selectable, false);
                // Remove focus
                if (ContainsFocus)
                {
                    ActiveControl = null; // remove focus from the search window
                }
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (!searchMode) return false;
            var keyCode = keyData & Keys.KeyCode;
            switch (keyCode)
            {
                case Keys.Escape:
                    Console.WriteLine("Exiting search mode.");
                    SearchMode = false;
                    return true;
                case Keys.Back:
                    if (searchText.Length > 0)
                    {
                        searchText = searchText.Substring(0, searchText.Length - 1);
                    }
                    Invalidate(); // trigger repaint to show updated search text
                    return true;
                case Keys.Enter:
                    PickFirstResult();
                    return true;
                default:
                    var keyChar = KeyToChar(keyCode, (keyData & Keys.Shift) != 0, (keyData & Keys.Control) != 0, (keyData & Keys.Alt) != 0);
                    if (keyChar != "noChar")
                    {
                        searchText += keyChar;
                        Invalidate(); // trigger repaint to show updated search text
                    }
                    break;
            }
            return true;
        }
        public void React_Click(object? sender, MouseEventArgs e)
        {
            Console.WriteLine("Exiting search mode from click.");
            SearchMode = false;
            return;
        }
        private void PickFirstResult()
        {
            //TODO
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0312)
            {
                int id = m.WParam.ToInt32();
                if (id != 0 && id != 1)
                {
                    Console.WriteLine($"Unknown hotkey id: {id}, still continuing.");
                }
                Console.WriteLine($"Toggling search mode: {(SearchMode ? "off" : "on")}"); SearchMode = !SearchMode;
                return;
            }
            base.WndProc(ref m);
        }
        public static string KeyToChar(Keys key, bool shift, bool control, bool alt, string fallback = "noChar")
        {
            byte[] keyboardState = new byte[256];
            GetKeyboardState(keyboardState);

            if (shift) keyboardState[(int)Keys.ShiftKey] = 0x80;
            if (control) keyboardState[(int)Keys.ControlKey] = 0x80;
            if (alt) keyboardState[(int)Keys.Menu] = 0x80;

            var buffer = new StringBuilder(2);
            uint virtualKey = (uint)key;
            uint scanCode = MapVirtualKey(virtualKey, 0);

            int result = ToUnicode(virtualKey, scanCode, keyboardState, buffer, buffer.Capacity, 0);
            if (result > 0)
                return buffer.ToString();
            else
                return fallback;
        }
    }
}