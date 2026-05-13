using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace SwExportAddin
{
    internal enum LogLevel
    {
        Debug,
        Information,
        Warning,
        Error
    }

    internal sealed class Logger
    {
        private static readonly Lazy<Logger> Instance = new Lazy<Logger>(() => new Logger());
        private readonly object syncRoot = new object();
        private readonly List<Action<string>> fallbackWriters = new List<Action<string>>();
        private string logFolder;
        private string currentLogFile;
        private bool initialized;

        public static Logger Current => Instance.Value;

        private Logger()
        {
        }

        public void Log(string message) => Information(message);

        public void Information(string message) => Write(LogLevel.Information, message);

        public void Warning(string message) => Write(LogLevel.Warning, message);

        public void Error(string message) => Write(LogLevel.Error, message);

        public void Error(Exception ex, string message) => Write(LogLevel.Error, message + " | " + ex);

        public void Debug(string message) => Write(LogLevel.Debug, message);

        private void Write(LogLevel level, string message)
        {
            string safeMessage = Format(level, message);

            try
            {
                EnsureInitialized();
                AppendToFile(safeMessage);
                return;
            }
            catch
            {
            }

            try
            {
                WriteFallback(safeMessage);
            }
            catch
            {
            }
        }

        private void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            lock (syncRoot)
            {
                if (initialized)
                {
                    return;
                }

                logFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SwExportAddin", "Logs");
                Directory.CreateDirectory(logFolder);
                currentLogFile = Path.Combine(logFolder, $"SwExportAddin-{DateTime.Now:yyyyMMdd}.log");
                fallbackWriters.Add(message => System.Diagnostics.Debug.WriteLine(message));
                fallbackWriters.Add(message => File.AppendAllText(Path.Combine(logFolder, "SwExportAddin-fallback.log"), message + Environment.NewLine));
                initialized = true;
            }
        }

        private void AppendToFile(string message)
        {
            if (string.IsNullOrWhiteSpace(currentLogFile))
            {
                return;
            }

            File.AppendAllText(currentLogFile, message + Environment.NewLine);
        }

        private static string Format(LogLevel level, string message)
        {
            return $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
        }

        private void WriteFallback(string message)
        {
            foreach (var writer in fallbackWriters)
            {
                try
                {
                    writer(message);
                }
                catch
                {
                }
            }
        }
    }
}
