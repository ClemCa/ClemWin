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
        private int selectedIndex = -1;
        private IntPtr selectedHandle = IntPtr.Zero;
        private SolidBrush selectedResultBackground = new(Color.FromArgb(70, 255, 255, 255));
        private SolidBrush highlightBrush = new(Color.FromArgb(255, 221, 87));
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
                    lastSearch = "";
                    searchResults = Fetcher.GetBySearch(searchText);
                    ClearSelection();
                    overlay.SetSearchActive(true);
                    overlay.ShowBackground();
                    overlay.BringToFront();
                    SetWindowLong(overlay.Handle, -20, overlay.ExStyle);
                }
                else
                {
                    ClearSelection();
                    overlay.HideBackground();
                    overlay.SetSearchActive(false);
                }
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
            if (lastSearch == searchText)
            {
                return; // no need to fetch results if search text hasn't changed
            }
            int previousSelectedIndex = selectedIndex;
            IntPtr previousSelectedHandle = selectedHandle;
            lastSearch = searchText;
            searchResults = Fetcher.GetBySearch(searchText);
            RestoreSelection(previousSelectedHandle, previousSelectedIndex);
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
            if (!SearchMode)
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
            float y = resultRect.Top + 7.5f + (index * resultRect.Height); // can't explain why it's 7.5f and not 5 but welp
            if (index == selectedIndex)
            {
                g.FillRectangle(selectedResultBackground, resultRect.Left + 2, resultRect.Top + 2 + (index * resultRect.Height), resultRect.Width - 4, resultRect.Height + 4);
            }

            DrawHighlightedResultText(g, GetDisplayText(result), new PointF(resultRect.Left + 10, y));
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
                case Keys.Tab:
                    if (selectedIndex < 0)
                    {
                        if (searchResults.Length > 0)
                        {
                            SetSelection(0);
                            form.Invalidate();
                        }
                    }
                    else
                    {
                        PickSelectedResult();
                    }
                    return true;
                case Keys.Up:
                    MoveSelection(-1);
                    form.Invalidate();
                    return true;
                case Keys.Down:
                    MoveSelection(1);
                    form.Invalidate();
                    return true;
                case Keys.Enter:
                    PickSelectedResult();
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
        private void PickSelectedResult()
        {
            if (searchResults.Length == 0)
            {
                return;
            }

            int index = selectedIndex >= 0 ? selectedIndex : 0;
            var selectedResult = searchResults[index];
            Console.WriteLine($"Switching to: {selectedResult.Title} ({selectedResult.ProcessName})");
            windowManager.ShowWindow(selectedResult.Handle);
            SearchMode = false;
        }
        private void MoveSelection(int delta)
        {
            if (searchResults.Length == 0)
            {
                ClearSelection();
                return;
            }

            if (selectedIndex < 0)
            {
                SetSelection(delta < 0 ? searchResults.Length - 1 : 0);
                return;
            }

            SetSelection(Math.Clamp(selectedIndex + delta, 0, searchResults.Length - 1));
        }
        private void RestoreSelection(IntPtr previousSelectedHandle, int previousSelectedIndex)
        {
            if (searchResults.Length == 0)
            {
                ClearSelection();
                return;
            }

            if (previousSelectedHandle != IntPtr.Zero)
            {
                int matchingIndex = Array.FindIndex(searchResults, result => result.Handle == previousSelectedHandle);
                if (matchingIndex >= 0)
                {
                    SetSelection(matchingIndex);
                    return;
                }
            }

            if (previousSelectedIndex >= 0)
            {
                SetSelection(Math.Clamp(previousSelectedIndex - 1, 0, searchResults.Length - 1));
                return;
            }

            ClearSelection();
        }
        private void SetSelection(int index)
        {
            if (index < 0 || index >= searchResults.Length)
            {
                ClearSelection();
                return;
            }

            selectedIndex = index;
            selectedHandle = searchResults[index].Handle;
        }
        private void ClearSelection()
        {
            selectedIndex = -1;
            selectedHandle = IntPtr.Zero;
        }
        private string GetDisplayText(SearchResult result)
        {
            return $"[{result.ProcessName}] {result.Title}";
        }
        private void DrawHighlightedResultText(Graphics g, string text, PointF location)
        {
            var highlightRanges = GetHighlightRanges(text, searchText);
            using StringFormat format = StringFormat.GenericTypographic;
            if (highlightRanges.Count == 0)
            {
                DrawTextSegment(g, text, Brushes.White, location, format);
                return;
            }

            float x = location.X;
            int cursor = 0;

            foreach (var (start, length) in highlightRanges)
            {
                if (start > cursor)
                {
                    string segment = text.Substring(cursor, start - cursor);
                    x += DrawTextSegment(g, segment, Brushes.White, new PointF(x, location.Y), format);
                }

                string highlightedSegment = text.Substring(start, length);
                x += DrawTextSegment(g, highlightedSegment, highlightBrush, new PointF(x, location.Y), format);
                cursor = start + length;
            }

            if (cursor < text.Length)
            {
                string trailingSegment = text.Substring(cursor);
                DrawTextSegment(g, trailingSegment, Brushes.White, new PointF(x, location.Y), format);
            }
        }
        private float DrawTextSegment(Graphics g, string text, Brush brush, PointF location, StringFormat format)
        {
            float x = location.X;
            float spaceWidth = g.MeasureString("a a", resultFont, PointF.Empty, format).Width - g.MeasureString("aa", resultFont, PointF.Empty, format).Width;
            for (int i = 0; i < text.Length; i++)
            {
                string character = text[i].ToString();
                if (text[i] == ' ')
                {
                    x += spaceWidth;
                    continue;
                }

                float y = location.Y;
                if (text[i] == '[' || text[i] == ']')
                {
                    y -= 2f;
                }

                g.DrawString(character, resultFont, brush, new PointF(x, y), format);
                x += g.MeasureString(character, resultFont, PointF.Empty, format).Width;
            }

            return x - location.X;
        }
        private List<(int start, int length)> GetHighlightRanges(string text, string currentSearch)
        {
            List<(int start, int length)> ranges = new();
            if (string.IsNullOrWhiteSpace(currentSearch))
            {
                return ranges;
            }

            foreach (string token in currentSearch.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                int startIndex = 0;
                while (startIndex < text.Length)
                {
                    int index = text.IndexOf(token, startIndex, StringComparison.InvariantCultureIgnoreCase);
                    if (index < 0)
                    {
                        break;
                    }

                    ranges.Add((index, token.Length));
                    startIndex = index + token.Length;
                }
            }

            if (ranges.Count <= 1)
            {
                return ranges;
            }

            ranges = ranges
                .OrderBy(range => range.start)
                .ToList();

            List<(int start, int length)> mergedRanges = [ranges[0]];
            for (int i = 1; i < ranges.Count; i++)
            {
                var currentRange = ranges[i];
                var previousRange = mergedRanges[^1];
                int previousEnd = previousRange.start + previousRange.length;
                int currentEnd = currentRange.start + currentRange.length;

                if (currentRange.start <= previousEnd)
                {
                    mergedRanges[^1] = (previousRange.start, Math.Max(previousEnd, currentEnd) - previousRange.start);
                    continue;
                }

                mergedRanges.Add(currentRange);
            }

            return mergedRanges;
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