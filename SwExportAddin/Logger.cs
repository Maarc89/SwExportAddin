using System;
using System.IO;

namespace SwExportAddin
{
    internal sealed class Logger
    {
        public void Log(string message)
        {
            try
            {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SwExportAddin");
                Directory.CreateDirectory(folder);
                string file = Path.Combine(folder, "SwExportAddin.log");
                File.AppendAllText(file, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch
            {
            }
        }
    }
}
