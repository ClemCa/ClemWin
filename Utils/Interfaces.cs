namespace ClemWin
{
    public class ClemWinForm : Form, IClemWinSender
    {
        public virtual void RegisterReceiver(IClemWinReceiver receiver)
        {
            throw new NotImplementedException();
        }
        public new void SetStyle(ControlStyles flag, bool value)
        {
            base.SetStyle(flag, value);
        }
    }

    public interface IClemWinSender
    {
        void RegisterReceiver(IClemWinReceiver receiver);
    }
    public interface IClemWinReceiver;
    public interface IDrawReceiver : IClemWinReceiver
    {
        void Draw(ClemWinForm form, Graphics graphics);
    }
    public interface IBoundsReceiver : IClemWinReceiver
    {
        void BoundsChanged(Rectangle workspace, Rectangle mainScreen);
    }
    public interface IKeyboardReceiver : IClemWinReceiver
    {
        bool KeyMessage(ClemWinForm form, ref Message msg, Keys keyData);
    }
    public interface IHotkeyReceiver : IClemWinReceiver
    {
        void HotkeyPressed(int id);
    }
}