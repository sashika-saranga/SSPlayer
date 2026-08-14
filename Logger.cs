using System;
using System.IO;
using System.Text;

namespace Mp3Player
{
    public static class Logger
    {
        private static readonly object lockObj = new object();
        private static string logFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");

        public static void Initialize()
        {
            try
            {
                if (!Directory.Exists(logFolder)) Directory.CreateDirectory(logFolder);

                // cleanup files older than 2 days
                var files = Directory.GetFiles(logFolder, "log-*.txt");
                var threshold = DateTime.Now.Date.AddDays(-2);
                foreach (var f in files)
                {
                    try
                    {
                        var info = new FileInfo(f);
                        if (info.CreationTime < threshold)
                        {
                            info.Delete();
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        private static string GetLogPath()
        {
            try
            {
                var name = $"log-{DateTime.Now:yyyy-MM-dd}.txt";
                return Path.Combine(logFolder, name);
            }
            catch
            {
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"log-{DateTime.Now:yyyy-MM-dd}.txt");
            }
        }

        public static void Log(string message)
        {
            try
            {
                var path = GetLogPath();
                var sb = new StringBuilder();
                sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] INFO: {message}");
                lock (lockObj)
                {
                    File.AppendAllText(path, sb.ToString());
                }
            }
            catch { }
        }

        public static void Log(Exception ex, string? message = null)
        {
            try
            {
                var path = GetLogPath();
                var sb = new StringBuilder();
                sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] ERROR: {message}");
                sb.AppendLine(ex.ToString());
                sb.AppendLine(new string('-', 80));
                lock (lockObj)
                {
                    File.AppendAllText(path, sb.ToString());
                }
            }
            catch { }
        }
    }
}
