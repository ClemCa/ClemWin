namespace ClemWin
{
    public interface IClemWinSender
    {
        void RegisterReceiver(IClemWinReceiver receiver);
    }
    public interface IClemWinReceiver;
    public interface IDrawReceiver : IClemWinReceiver
    {
        void Draw(Overlay form, Graphics graphics);
    }
    public interface IBoundsReceiver : IClemWinReceiver
    {
        void BoundsChanged(Rectangle workspace, Rectangle mainScreen);
    }
    public interface IKeyboardReceiver : IClemWinReceiver
    {
        bool KeyMessage(Overlay form, ref Message msg, Keys keyData);
    }
    public interface IHotkeyReceiver : IClemWinReceiver
    {
        void HotkeyPressed(int id);
    }
}