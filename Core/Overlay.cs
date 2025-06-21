using System.Runtime.InteropServices;
using System.Text;
using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Diagnostics;

namespace ClemWin
{
    public class Overlay : Form, IClemWinSender
    {
        private const long WS_POPUP = 0x80000000L;
        private const long WS_CAPTION = 0x40000000L;
        private const long WS_SYSMENU = 0x10000000L;
        private System.Windows.Forms.Screen targetScreen;
        private List<IClemWinReceiver> receivers = [];
        private Background backgroundWindow;
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Intangible { get; set; } = true;
        public Overlay()
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

            UpdateTargetScreen();
            if (targetScreen == null) // could check the returned boolean but we need the compiler to shut up
            {
                throw new InvalidOperationException("No primary screen found.");
            }

            backgroundWindow = new Background(this);
            this.Owner = backgroundWindow;
            backgroundWindow.Show();
            backgroundWindow.Hide();

            foreach (var receiver in receivers)
            {
                if (receiver is IBoundsReceiver boundsReceiver)
                {
                    boundsReceiver.BoundsChanged(SystemInformation.VirtualScreen, targetScreen.Bounds);
                }
            }
        }
        ~Overlay()
        {
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
                if (Intangible)
                {
                    cp.ExStyle |= 0x80 | 0x80000 | 0x20; // WS_EX_TOOLWINDOW | WS_EX_LAYERED | WS_EX_TRANSPARENT
                }
                else
                {
                    cp.ExStyle |= 0x80 | 0x80000; // WS_EX_TOOLWINDOW | WS_EX_LAYERED
                }
                return cp;
            }
        }
        public int ExStyle
        {
            get
            {
                return CreateParams.ExStyle;
            }
        }
        public bool UpdateTargetScreen()
        {
            var mainScreen = System.Windows.Forms.Screen.PrimaryScreen;
            if (mainScreen == null)
            {
                if (targetScreen == null)
                {
                    throw new InvalidOperationException("No primary screen found.");
                }
                // it'll pass
                return false; // No change in target screen
            }
            if (targetScreen != null && targetScreen.Bounds == mainScreen.Bounds)
            {
                return false; // No change in target screen
            }
            targetScreen = mainScreen;
            Bounds = SystemInformation.VirtualScreen;
            return true; // Target screen updated
        }
        public void RegisterReceiver(IClemWinReceiver receiver)
        {
            ArgumentNullException.ThrowIfNull(receiver);
            if (!receivers.Contains(receiver))
            {
                receivers.Add(receiver);
                if (receiver is IBoundsReceiver boundsReceiver)
                {
                    boundsReceiver.BoundsChanged(SystemInformation.VirtualScreen, targetScreen.Bounds);
                }
                if (receiver is IDrawReceiver)
                {
                    Invalidate(); // trigger repaint to show the new receiver
                }
            }
            Console.WriteLine($"Receiver registered: {receiver.GetType().Name}");
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            bool boundsChanged = UpdateTargetScreen();
            foreach (var receiver in receivers)
            {
                if (boundsChanged && receiver is IBoundsReceiver boundsReceiver)
                {
                    boundsReceiver.BoundsChanged(SystemInformation.VirtualScreen, targetScreen.Bounds);
                }
                if (receiver is IDrawReceiver drawReceiver)
                {
                    drawReceiver.Draw(this, e.Graphics);
                }
            }
        }
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateTargetScreen();
            foreach (var receiver in receivers) // no matter what send the event as they may depend on the virtual screen and not just the main screen
            {
                if (receiver is IBoundsReceiver boundsReceiver)
                {
                    boundsReceiver.BoundsChanged(SystemInformation.VirtualScreen, targetScreen.Bounds);
                }
            }
        }
        public new void SetStyle(ControlStyles flag, bool value)
        {
            base.SetStyle(flag, value);
        }
        public void ShowBackground()
        {
            backgroundWindow.Show();
        }
        public void HideBackground()
        {
            backgroundWindow.Hide();
        }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            bool anyHandled = false;
            foreach (var receiver in receivers)
            {
                if (receiver is IKeyboardReceiver keyboardReceiver)
                {
                    anyHandled |= keyboardReceiver.KeyMessage(this, ref msg, keyData);
                }
            }
            return anyHandled || base.ProcessCmdKey(ref msg, keyData);
        }
    }
}