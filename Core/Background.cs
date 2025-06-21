using System.Runtime.InteropServices;

namespace ClemWin
{
    public class Background : ClemWinForm, IBoundsReceiver
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
        private List<IClemWinReceiver> receivers = [];
        public Windows WindowManager { get; private set; }
        public Background(Overlay overlay)
        {
            this.Bounds = overlay.Bounds;
            this.FormBorderStyle = FormBorderStyle.None;
            this.TopMost = true;
            this.BackColor = Color.Black;
            this.AllowTransparency = true;
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.Manual;
            this.Opacity = 0.5;
            this.DoubleBuffered = true;
            this.WindowManager = overlay.WindowManager;
            overlay.RegisterReceiver(this);
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
        public void BoundsChanged(Rectangle workspace, Rectangle mainScreen)
        {
            Bounds = workspace;
            foreach (var receiver in receivers)
            {
                if (receiver is IBoundsReceiver boundsReceiver)
                {
                    boundsReceiver.BoundsChanged(workspace, mainScreen);
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            foreach (var receiver in receivers)
            {
                if (receiver is IDrawReceiver drawReceiver)
                {
                    drawReceiver.Draw(this, e.Graphics);
                }
            }
        }
        public override void RegisterReceiver(IClemWinReceiver receiver)
        {
            receivers.Add(receiver);
        }
    }
}
