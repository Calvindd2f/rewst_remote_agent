using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace RewstAgent.IoTHub.Logging
{
    /// <summary>
    /// Configures logging for the IoT Hub module.
    /// </summary>
    public static class IoTHubLoggingSetup
    {
        public static ILoggingBuilder ConfigureIoTHubLogging(
            this ILoggingBuilder builder,
            string applicationName)
        {
            builder.ClearProviders();

            // Add console logging
            builder.AddConsole();

            // Configure platform-specific logging
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                builder.AddEventLog(settings =>
                {
                    settings.SourceName = applicationName;
                    settings.LogName = "Application";
                });
            }
            else
            {
                // For non-Windows platforms, add file logging
                builder.AddFile($"logs/{applicationName}-.log", options =>
                {
                    options.FileSizeLimitBytes = 10 * 1024 * 1024; // 10MB
                    options.RetainedFileCountLimit = 5;
                });
            }

            return builder;
        }
    }

    /// <summary>
    /// Provides structured exception handling for IoT Hub operations.
    /// </summary>
    public class IoTHubExceptionHandler
    {
        private readonly ILogger _logger;

        public IoTHubExceptionHandler(ILogger logger)
        {
            _logger = logger;
        }

        public async Task<T> ExecuteWithRetryAsync<T>(
            Func<Task<T>> operation,
            int maxRetries = 3,
            int retryDelayMs = 1000)
        {
            for (int i = 0; i <= maxRetries; i++)
            {
                try
                {
                    return await operation();
                }
                catch (Exception ex) when (i < maxRetries)
                {
                    _logger.LogWarning(ex,
                        "Operation failed (attempt {Attempt} of {MaxRetries}). Retrying in {Delay}ms",
                        i + 1, maxRetries, retryDelayMs);
                    
                    await Task.Delay(retryDelayMs * (i + 1));
                }
            }

            // If we get here, all retries have failed
            throw new IoTHubOperationException("Operation failed after maximum retries");
        }

        public async Task ExecuteWithRetryAsync(
            Func<Task> operation,
            int maxRetries = 3,
            int retryDelayMs = 1000)
        {
            await ExecuteWithRetryAsync<object>(async () =>
            {
                await operation();
                return null;
            }, maxRetries, retryDelayMs);
        }
    }

    /// <summary>
    /// Custom exception for IoT Hub operations.
    /// </summary>
    public class IoTHubOperationException : Exception
    {
        public IoTHubOperationException(string message) : base(message) { }
        public IoTHubOperationException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}