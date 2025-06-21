namespace ClemWin
{
    using System;
    using System.Drawing;
    using System.IO;
    using System.Reflection;
    using System.Windows.Forms;

    public static class Utils
    {
        public static Icon? IconFromPNG(string resourceName)
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                using Stream? stream = assembly.GetManifestResourceStream($"ClemWin.{resourceName}");
                if (stream != null)
                {
                    using Bitmap bitmap = new Bitmap(stream);
                    return Icon.FromHandle(bitmap.GetHicon());
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading icon: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return null;
        }
        public static Bitmap? BitmapFromPNG(string resourceName)
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                using Stream? stream = assembly.GetManifestResourceStream($"ClemWin.{resourceName}");
                if (stream != null)
                {
                    return new Bitmap(stream);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading bitmap: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return null;
        }

        public static KeyCode KeyCodeFrom(int number)
        {
            if (number < 0 || number > 9)
            {
                throw new ArgumentOutOfRangeException(nameof(number), "Invalid number for KeyCode.");
            }
            return KeyCode.Numpad0 + number;
        }
    }
}