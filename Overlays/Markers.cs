using System.Drawing.Drawing2D;

namespace ClemWin
{
    public class Markers : IDrawReceiver, IBoundsReceiver
    {
        private WhiteList whiteList;
        private bool markersVisible = true;
        private CancellationTokenSource endpointTask = new CancellationTokenSource();
        private Brush visibleColor = new SolidBrush(Color.White);
        private Brush invisibleColor = new SolidBrush(Color.Black);
        private int negativeX = 0;
        private Rectangle whitelistBounds;
        private LinearGradientBrush whitelistBrush = new LinearGradientBrush(new PointF(0, 0.5f), new PointF(1, 1f), Color.FromArgb(8, 51, 69), Color.FromArgb(54, 0, 0));
        private Pen whitelistPen = new Pen(Color.White, 4);
        private Font whitelistFont = new Font("Arial", 15, FontStyle.Bold);
        private Font whitelistSecondaryFont = new Font("Arial", 15, FontStyle.Regular);
        private Font whitelistTernaryFont = new Font("Arial", 10, FontStyle.Italic);
        private float whitelistLineHeight;
        private const float halfOffset = 7.5f;
        private const string ternaryText = $"Ctrl + Shift + Enter to toggle, Ctrl + Alt + Enter to clear";
        private float ternarySize;
        public Markers(Overlay overlay, WhiteList whiteList)
        {
            this.whiteList = whiteList;
            WhiteList.OnWhitelistUpdated += () =>
            {
                overlay.TopLevel = true;
                overlay.TopMost = true;
                markersVisible = whiteList.WhiteListMode;
                if (markersVisible)
                {
                    endpointTask.Cancel();
                    endpointTask = new CancellationTokenSource();
                    Task.Delay(2000, endpointTask.Token).ContinueWith(t =>
                    {
                        if (t.IsCanceled)
                        {
                            return; // Task was canceled, do not proceed
                        }
                        markersVisible = false;
                        overlay.Invalidate();
                    }, TaskScheduler.FromCurrentSynchronizationContext());
                }
                overlay.Invalidate();
            };
            overlay.RegisterReceiver(this);
            overlay.RegisterLayer(this, () => markersVisible);
        }

        public void BoundsChanged(Rectangle workspace, Rectangle mainScreen)
        {
            negativeX = Math.Max(0 - workspace.X, 0);
            whitelistBounds = new Rectangle(
                negativeX + mainScreen.Right - 50,
                mainScreen.Bottom - 200,
                50,
                50);
            whitelistLineHeight = whitelistFont.Height + halfOffset * 2;
            whitelistBrush = new LinearGradientBrush(new PointF(mainScreen.Right, mainScreen.Bottom), new PointF(mainScreen.Right, mainScreen.Top + mainScreen.Height * 0.5f),
            whitelistBrush.LinearColors[0],
            whitelistBrush.LinearColors[1]);
        }

        public void Draw(ClemWinForm form, Graphics graphics)
        {
            if (!markersVisible || !whiteList.WhiteListMode)
            {
                return;
            }
            if (form is not Overlay overlay)
            {
                return; // Ensure we are drawing on the correct form type
            }
            DrawWhitelist(graphics, overlay);
            var windowsOnScreen = Fetcher.GetWindowsOnScreen(overlay.WindowManager);
            foreach (var (window, bounds) in windowsOnScreen)
            {
                DrawMarker(graphics, window, bounds, whiteList.InWhitelist(window));
            }
        }

        private void DrawMarker(Graphics graphics, Window window, Bounds bounds, bool isWhitelisted)
        {
            var markerColor = isWhitelisted ? visibleColor : invisibleColor;
            var space = bounds.ToDesktop();
            Console.WriteLine($"Drawing marker for {window.ProcessName}");
            //! I have no clue why tf I need to offset it by the absolute of the workspace's coordinates but windows multimonitor compensation ig
            graphics.FillEllipse(markerColor, new RectangleF(
                negativeX + space.X + space.Width - space.Height * 0.1f,
                space.Y + space.Height - space.Height * 0.1f,
                50,
                50));
        }

        private void DrawWhitelist(Graphics graphics, Overlay overlay)
        {
            if (!whiteList.WhiteListMode || !markersVisible)
            {
                return;
            }
            var whitelist = whiteList.GetWhitelist();
            int whitelistSize = Math.Max(1, whitelist.Count) + 2;

            if (ternarySize == 0) ternarySize = graphics.MeasureString(ternaryText, whitelistTernaryFont).Width;
            float longestStringSize = ternarySize;
            if (whitelist.Count > 0)
            {
                var target = whitelist.MaxBy(static w => w.Title.Length + w.ProcessName.Length);
                string longestString = $"[{target?.ProcessName}] {target?.Title}";
                longestStringSize = Math.Max(graphics.MeasureString(longestString, whitelistSecondaryFont).Width, ternarySize);
            }

            float targetWidth = longestStringSize + 20;
            float targetX = whitelistBounds.X - targetWidth;

            graphics.FillRoundedRectangle(whitelistBrush,
            new RectangleF(targetX, whitelistBounds.Y - whitelistLineHeight * whitelistSize, targetWidth, whitelistLineHeight * whitelistSize),
            new Size(25, 25));
            var format = new StringFormat
            {
                Alignment = StringAlignment.Far,
            };
            float topPos = whitelistBounds.Y - whitelistLineHeight * whitelistSize + halfOffset;
            int rightPos = whitelistBounds.X - 10;
            graphics.DrawString("Whitelist", whitelistFont, Brushes.White, rightPos, topPos, format);
            if (whitelist.Count == 0)
            {
                graphics.DrawString("Empty", whitelistSecondaryFont, Brushes.White, rightPos, topPos + whitelistLineHeight, format);
            }
            else
            {
                for (int i = 0; i < whitelist.Count; i++)
                {
                    graphics.DrawString($"[{whitelist[i].ProcessName}] {whitelist[i].Title}", whitelistSecondaryFont, Brushes.White, rightPos, topPos + whitelistLineHeight * (i + 1), format);
                }
            }
            graphics.DrawString(ternaryText, whitelistTernaryFont, Brushes.White, rightPos, topPos + whitelistLineHeight * (whitelistSize - 1), format);

        }
    }
}