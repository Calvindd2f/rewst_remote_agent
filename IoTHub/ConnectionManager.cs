using System;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.Extensions.Logging;

namespace RewstAgent.IoTHub
{
    public class ConnectionManager : IConnectionManager, IAsyncDisposable
    {
        private readonly ILogger<ConnectionManager> _logger;
        private readonly string _connectionString;
        private DeviceClient _deviceClient;
        private bool _isConnected;

        public ConnectionManager(
            ILogger<ConnectionManager> logger,
            ConfigurationData config)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _connectionString = config?.IoTHubConnectionString 
                ?? throw new ArgumentNullException(nameof(config));
        }

        public async Task Connect()
        {
            try
            {
                if (_isConnected)
                {
                    _logger.LogWarning("Already connected to IoT Hub");
                    return;
                }

                _deviceClient = DeviceClient.CreateFromConnectionString(
                    _connectionString,
                    TransportType.Mqtt);

                await _deviceClient.OpenAsync();
                _isConnected = true;

                _logger.LogInformation("Successfully connected to IoT Hub");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to IoT Hub");
                throw;
            }
        }

        public async Task Disconnect()
        {
            if (!_isConnected || _deviceClient == null)
            {
                return;
            }

            try
            {
                await _deviceClient.CloseAsync();
                _isConnected = false;
                _logger.LogInformation("Disconnected from IoT Hub");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disconnecting from IoT Hub");
                throw;
            }
        }

        public async Task SetMessageHandler()
        {
            if (!_isConnected || _deviceClient == null)
            {
                throw new InvalidOperationException("Not connected to IoT Hub");
            }

            try
            {
                await _deviceClient.SetReceiveMessageHandlerAsync(
                    HandleMessage, 
                    null);

                _logger.LogInformation("Message handler set successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set message handler");
                throw;
            }
        }

        private async Task<MessageResponse> HandleMessage(
            Message message, 
            object userContext)
        {
            try
            {
                var messageData = System.Text.Encoding.UTF8.GetString(message.GetBytes());
                _logger.LogInformation("Received command message");

                var commandScript = messageData;
                await ExecuteConfigurationScript(commandScript);

                return MessageResponse.Completed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message");
                return MessageResponse.Abandoned;
            }
        }

        private async Task ExecuteConfigurationScript(string script)
        {
            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            var startInfo = new ProcessStartInfo
            {
                FileName = isWindows ? "powershell.exe" : "bash",
                Arguments = isWindows ? $"-Command {script}" : $"-c '{script}'",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();
            
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            
            await process.WaitForExitAsync();
            
            if (process.ExitCode != 0)
            {
                _logger.LogError("Script execution failed: {Error}", error);
                throw new Exception($"Script execution failed with exit code {process.ExitCode}");
            }
            
            _logger.LogInformation("Configuration script executed successfully");
        }

        public async ValueTask DisposeAsync()
        {
            if (_deviceClient != null)
            {
                await Disconnect();
                await _deviceClient.DisposeAsync();
            }
            
            GC.SuppressFinalize(this);
        }
    }
}