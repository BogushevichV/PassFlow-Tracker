using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PassFlow_Tracker.Infrastructure.Logging
{
    public enum LogLevel
    {
        Info,
        Warning,
        Error
    }

    public static class AppLogger
    {
        private static readonly string _logDir;

        private static readonly SemaphoreSlim _semaphore = new(1, 1);

        static AppLogger()
        {
            string appData = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);

            _logDir = Path.Combine(appData, "PassFlowTracker", "logs");

            if (!Directory.Exists(_logDir))
                Directory.CreateDirectory(_logDir);

            Console.WriteLine($"Логи сохраняются в: {_logDir}");
        }

        public static void Info(string message)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await WriteAsync(LogLevel.Info, message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Logger error: {ex.Message}");
                }
            });
        }

        public static void Warning(string message)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await WriteAsync(LogLevel.Warning, message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Logger error: {ex.Message}");
                }
            });
        }

        public static void Error(string message, Exception? ex = null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await WriteAsync(LogLevel.Error, message, ex);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Logger error: {ex.Message}");
                }
            });
        }

        private static string GetLogFilePath()
        {
            return Path.Combine(_logDir, $"log_{DateTime.Now:yyyy-MM-dd}.txt");
        }

        private static async Task WriteAsync(LogLevel level, string message, Exception? ex = null)
        {
            string log = BuildMessage(level, message, ex);
            string logFile = GetLogFilePath(); 

            await _semaphore.WaitAsync();
            try
            {
                await File.AppendAllTextAsync(logFile, log, Encoding.UTF8);
                Console.WriteLine(log);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private static string BuildMessage(LogLevel level, string message, Exception? ex)
        {
            var sb = new StringBuilder();

            sb.Append($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ");
            sb.Append($"[{level}] ");
            sb.AppendLine(message);

            if (ex != null)
            {
                sb.AppendLine($"Exception: {ex.Message}");
                sb.AppendLine(ex.StackTrace);
            }

            return sb.ToString();
        }
    }
}
