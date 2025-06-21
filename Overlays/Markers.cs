namespace ClemWin
{
    public class Markers : IDrawReceiver, IBoundsReceiver
    {
        private WhiteList whiteList;
        private bool markersVisible = true;
        private Brush visibleColor = new SolidBrush(Color.White);
        private Brush invisibleColor = new SolidBrush(Color.FromArgb(80, 0, 0));
        private int negativeX = 0;
        private Overlay overlay;
        public Markers(Overlay overlay, WhiteList whiteList)
        {
            this.overlay = overlay;
            this.whiteList = whiteList;
            WhiteList.OnWhitelistUpdated += () =>
            {
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
            if (isWhitelisted) return;
            var markerColor = isWhitelisted ? visibleColor : invisibleColor;
            var space = bounds.ToDesktop();
            //! I have no clue why tf I need to offset it by 1920 but windows multimonitor compensation ig
            graphics.FillEllipse(markerColor, new RectangleF(
                negativeX + space.X + space.Width - space.Height * 0.1f,
                space.Y + space.Height - space.Height * 0.1f,
                50,
                50));
        }
    }
}