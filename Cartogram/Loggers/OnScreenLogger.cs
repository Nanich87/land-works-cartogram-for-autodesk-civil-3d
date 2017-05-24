namespace Cartogram.Loggers
{
    using System.Windows.Forms;
    using Cartogram.Contracts;

    public class OnScreenLogger : ILogger
    {
        public void WriteLog(string message)
        {
            MessageBox.Show(message, "Error:", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}