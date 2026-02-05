using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EZcashAutoUpdater
{
    public static class Logger
    {
        // Serilog handles thread-safety internally, so we don't need manual locking objects or queues.
        static Logger()
        {

            string serviceFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EZCashATMServiceAutoUpdater");

            string archiveFolder = Path.Combine(serviceFolder, "Archievedlogs");

            //var fileSize = string.IsNullOrEmpty(ServiceConfiguration.GetFileLocation("TraceFileSize")) ? 100 : Convert.ToInt16(ServiceConfiguration.GetFileLocation("TraceFileSize"));
            //var rollOutDays = string.IsNullOrEmpty(ServiceConfiguration.GetFileLocation("DeleteArchieved")) ? 60 : Convert.ToInt16(ServiceConfiguration.GetFileLocation("DeleteArchieved"));
            //rollOutDays = rollOutDays == 0 ? 60 : rollOutDays;
            //fileSize = fileSize == 0 ? 100 : fileSize;
            string outputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}";

            Log.Logger = new LoggerConfiguration()
                            .WriteTo.File(
                               path: Path.Combine(serviceFolder, "AutoUpdateLog.txt"),
                               outputTemplate: outputTemplate, // Apply the custom format here
                               rollingInterval: RollingInterval.Infinite,
                               rollOnFileSizeLimit: true, // MUST be true to trigger the hook
                               fileSizeLimitBytes: 100 * 1024 * 1024, // 1MB
                               hooks: new ArchiveFileHook(archiveFolder, 100),
                               retainedFileCountLimit: null
                            )
                            .CreateLogger();
        }

        public static void LogWithNoLock(string message)
        {
            Log.Information(message);
        }

        public static void LogExceptionWithNoLock(string message, Exception exception)
        {
            // Serilog automatically formats the message and the StackTrace
            Log.Error(exception, message);
        }

        public static void LogWarningWithNoLock(string message)
        {
            Log.Warning(message);
        }
    }
}
