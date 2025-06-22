using System.Runtime.InteropServices;
using System.Text;
using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Diagnostics;

namespace ClemWin
{
    public class Search : IClemWinReceiver, IDrawReceiver, IBoundsReceiver, IKeyboardReceiver, IHotkeyReceiver
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

        private (float width, float height) boxSize = (0.4f, 0.2f); // default size for search box (in percentage)
        private const float cornerRadiusPercentage = 0.3f; // default corner radius percentage
        private Windows windowManager;
        private Rectangle searchBoxRect;
        private RectangleF searchboxSubRect;
        private Rectangle resultRect;
        private int cornerRadius;
        private Bitmap? logo;
        private Font font = new Font("Arial", 36, FontStyle.Regular);
        private Font resultFont = new Font("Arial", 16, FontStyle.Bold);
        private LinearGradientBrush mainBackground = new LinearGradientBrush(new PointF(0, 0.5f), new PointF(1, 1f), Color.FromArgb(0, 0, 0), Color.FromArgb(54, 0, 0));
        private LinearGradientBrush resultBackground = new LinearGradientBrush(new PointF(0, 0.5f), new PointF(1, 1f), Color.FromArgb(8, 51, 69), Color.FromArgb(54, 0, 0));
        private string searchText = "";
        private bool searchMode = false;
        private string lastSearch = "";
        private Overlay overlay;
        private SearchResult[] searchResults = [];
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool SearchMode
        {
            get => searchMode;
            set
            {
                searchMode = value;
                overlay.Intangible = !searchMode; // make the overlay intangible when search mode is inactive
                if (searchMode)
                {
                    searchText = "";
                    overlay.ShowBackground();
                    overlay.BringToFront();
                    SetWindowLong(overlay.Handle, -20, overlay.ExStyle);
                }
                else
                    overlay.HideBackground();
                overlay.Invalidate(); // trigger repaint when search mode changes
            }
        }
        public Search(Windows windowManager, Overlay overlay, HotkeyWindow hotkeyWindow)
        {
            this.windowManager = windowManager;
            this.overlay = overlay;
            overlay.RegisterReceiver(this);
            overlay.RegisterLayer(this, () => SearchMode);
            hotkeyWindow.RegisterHotkey(HotkeyModifiers.Win | HotkeyModifiers.Ctrl, KeyCode.K, this);
            hotkeyWindow.RegisterHotkey(HotkeyModifiers.Win | HotkeyModifiers.Shift, KeyCode.K, this);
            hotkeyWindow.RegisterHotkey(HotkeyModifiers.Win, KeyCode.K, this);
        }
        public void BoundsChanged(Rectangle workspace, Rectangle mainScreen)
        {
            RepositionSearchBox(workspace, mainScreen);
        }
        public void Draw(ClemWinForm form, Graphics graphics)
        {
            ManageKeyboardInput(form);
            FetchResults();
            DrawSearchBox(graphics);
            DrawResults(graphics);
        }
        private void RepositionSearchBox(Rectangle workspace, Rectangle mainScreen)
        {
            int workspaceX = SystemInformation.VirtualScreen.X;
            int workspaceY = SystemInformation.VirtualScreen.Y;
            int workspaceWidth = SystemInformation.VirtualScreen.Width;
            int workspaceHeight = SystemInformation.VirtualScreen.Height;
            Console.WriteLine($"Workspace: {workspaceX}, {workspaceY}, {workspaceWidth}, {workspaceHeight}");

            int XDiff = mainScreen.X - workspaceX;
            int YDiff = mainScreen.Y - workspaceY;
            float middleX = XDiff + mainScreen.Width / 2f;
            float middleY = YDiff + mainScreen.Height / 2f;
            searchBoxRect = new Rectangle(
                (int)(middleX - (mainScreen.Width * boxSize.width / 2)),
                (int)(middleY - (mainScreen.Height * boxSize.height / 2)),
                (int)(mainScreen.Width * boxSize.width),
                (int)(mainScreen.Height * boxSize.height)
            );
            cornerRadius = (int)(searchBoxRect.Height * cornerRadiusPercentage);
            searchboxSubRect = new RectangleF(
                searchBoxRect.X,
                searchBoxRect.Y + cornerRadius,
                searchBoxRect.Width,
                searchBoxRect.Height - cornerRadius
            );
            resultRect = new Rectangle(
                searchBoxRect.X,
                (int)(searchBoxRect.Bottom + searchBoxRect.Height * 0.1f),
                searchBoxRect.Width,
                5 + resultFont.Height // height will be calculated dynamically based on the number of results
            );

            mainBackground = new LinearGradientBrush(
                new PointF(searchBoxRect.Left - searchBoxRect.Height, searchBoxRect.Top + searchBoxRect.Height / 2f),
                new PointF(searchBoxRect.Right + searchBoxRect.Height * 2, searchBoxRect.Bottom),
                mainBackground.LinearColors[0],
                mainBackground.LinearColors[1]
            );

            resultBackground = new LinearGradientBrush(
                new PointF(resultRect.Left, mainScreen.Bottom),
                new PointF(resultRect.Right + resultRect.Width / 2f, searchBoxRect.Bottom),
                resultBackground.LinearColors[0],
                resultBackground.LinearColors[1]
            );
        }
        private void FetchResults()
        {
            if (!SearchMode)
            {
                return;
            }
            if (string.IsNullOrEmpty(searchText))
            {
                if (lastSearch != searchText)
                {
                    lastSearch = searchText;
                    searchResults = [];
                }
                return; // no need to fetch results if search text is empty or hasn't changed
            }
            if (lastSearch == searchText)
            {
                return; // no need to fetch results if search text hasn't changed
            }
            lastSearch = searchText;
            searchResults = Fetcher.GetBySearch(searchText);
        }
        private void DrawSearchBox(Graphics g)
        {
            if (!SearchMode)
            {
                return;
            }
            // Draw the search box UI
            g.FillRoundedRectangle(mainBackground, searchBoxRect, new Size(cornerRadius, cornerRadius));
            g.FillRectangle(mainBackground, searchboxSubRect); // remove the rounded corners on the bottom
            logo ??= Utils.BitmapFromPNG("logo.png") ?? SystemIcons.Application.ToBitmap();
            int logoSize = (int)(searchBoxRect.Height * 0.25f);
            g.DrawImage(logo,
                new Rectangle(
                    searchBoxRect.Left + 10,
                    searchBoxRect.Top + (searchBoxRect.Height - logoSize) / 2,
                    logoSize,
                    logoSize
                )
            );
            g.DrawString(searchText, font, Brushes.White, new PointF(
                searchBoxRect.Left + logoSize + 10,
                searchBoxRect.Top + (searchBoxRect.Height - font.Height) / 2f
            ));
        }
        private void DrawResults(Graphics g)
        {
            if (!SearchMode || string.IsNullOrEmpty(searchText))
            {
                return;
            }
            // Draw the search area
            g.FillRectangle(resultBackground, resultRect.X, resultRect.Y, resultRect.Width, 10 + (searchResults.Length * resultRect.Height));
            for (int i = 0; i < searchResults.Length; i++)
            {
                DrawResult(g, searchResults[i], i);
            }
        }

        private void DrawResult(Graphics g, SearchResult result, int index)
        {
            g.DrawString($"[{result.ProcessName}] {result.Title}", resultFont, Brushes.White, new PointF(
                resultRect.Left + 10,
                resultRect.Top + 7.5f + (index * resultRect.Height) // can't explain why it's 7.5f and not 5 but welp
            ));
        }

        private void ManageKeyboardInput(ClemWinForm form)
        {
            if (SearchMode)
            {
                form.MouseDown += React_Click;
                // Make it clickable
                form.SetStyle(ControlStyles.Selectable, true);
                // Take focus
                form.Activate();
                form.Focus();
                // backgroundWindow?.Activate();
                // backgroundWindow?.Focus();
                // Show();
                // BringToFront();
                // Select();

            }
            else
            {
                form.MouseDown -= React_Click;
                // Make it not clickable
                form.SetStyle(ControlStyles.Selectable, false);
                // Remove focus
                if (form.ContainsFocus)
                {
                    form.ActiveControl = null; // remove focus from the search window
                }
            }
        }
        public bool KeyMessage(ClemWinForm form, ref Message msg, Keys keyData)
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
                    form.Invalidate(); // trigger repaint to show updated search text
                    return true;
                case Keys.Enter:
                    PickFirstResult();
                    return true;
                default:
                    var keyChar = KeyToChar(keyCode, (keyData & Keys.Shift) != 0, (keyData & Keys.Control) != 0, (keyData & Keys.Alt) != 0);
                    if (keyChar != "noChar")
                    {
                        searchText += keyChar;
                        form.Invalidate(); // trigger repaint to show updated search text
                    }
                    break;
            }
            return true;
        }
        public void HotkeyPressed(int id)
        {
            Console.WriteLine($"Hotkey {id} pressed, toggling search mode.");
            SearchMode = !SearchMode;
        }
        public void React_Click(object? sender, MouseEventArgs e)
        {
            Console.WriteLine("Exiting search mode from click.");
            SearchMode = false;
            return;
        }
        private void PickFirstResult()
        {
            if (searchResults.Length == 0)
            {
                return;
            }
            var firstResult = searchResults[0];
            Console.WriteLine($"Switching to: {firstResult.Title} ({firstResult.ProcessName})");
            windowManager.ShowWindow(firstResult.Handle);
            SearchMode = false;
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