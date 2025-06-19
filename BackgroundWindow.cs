using System.Runtime.InteropServices;

namespace ClemWin
{
    public class BackgroundWindow : Form
    {
        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll")]
        private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        private const long WS_POPUP = 0x80000000L;
        private const long WS_CAPTION = 0x40000000L;
        private const long WS_SYSMENU = 0x10000000L;
        private SearchWindow? searchWindow;
        public BackgroundWindow(SearchWindow searchWindow)
        {
            this.searchWindow = searchWindow;
            this.Bounds = searchWindow.Bounds;
            this.FormBorderStyle = FormBorderStyle.None;
            this.TopMost = true;
            this.BackColor = Color.Black;
            this.AllowTransparency = true;
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.Manual;
            this.Opacity = 0.5;
            this.DoubleBuffered = true;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style |= unchecked((int)(WS_POPUP | WS_CAPTION | WS_SYSMENU));
                cp.ExStyle |= 0x80 | 0x80000;
                return cp;
            }
        }
        public void SetBounds(Rectangle bounds)
        {
            this.Bounds = bounds;
        }
    }
}
