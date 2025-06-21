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
    }
}