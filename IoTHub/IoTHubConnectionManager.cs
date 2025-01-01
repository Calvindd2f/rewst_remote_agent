using Microsoft.Azure.Devices.Client;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using RewstAgent.Configuration;

namespace RewstAgent.IoTHub
{
    /// <summary>
    /// Manages connections and communications with Azure IoT Hub.
    /// </summary>
    public class IoTHubConnectionManager : IIoTHubConnectionManager
    {
        private readonly ILogger<IoTHubConnectionManager> _logger;
        private readonly IoTHubConfig _config;
        private readonly ICommandExecutionService _commandExecutionService;
        private readonly ITempFileManager _tempFileManager;
        private readonly IHttpClientService _httpClient;
        private readonly IConfigurationManager _configManager;
        private DeviceClient _deviceClient;
        private bool _isConnected;

        public IoTHubConnectionManager(
            ILogger<IoTHubConnectionManager> logger,
            IoTHubConfig config,
            ICommandExecutionService commandExecutionService,
            ITempFileManager tempFileManager,
            IHttpClientService httpClient,
            IConfigurationManager configManager)
        {
            _logger = logger;
            _config = config;
            _commandExecutionService = commandExecutionService;
            _tempFileManager = tempFileManager;
            _httpClient = httpClient;
            _configManager = configManager;
        }

        public async Task ConnectAsync()
        {
            try
            {
                if (_isConnected)
                {
                    _logger.LogWarning("Already connected to IoT Hub");
                    return;
                }

                var connectionString = GetConnectionString();
                _deviceClient = DeviceClient.CreateFromConnectionString(connectionString, TransportType.Mqtt);
                await _deviceClient.OpenAsync();
                _isConnected = true;

                await UpdateDeviceTwinStatusAsync("online");
                _logger.LogInformation("Successfully connected to IoT Hub");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to IoT Hub");
                throw;
            }
        }

        public async Task DisconnectAsync()
        {
            try
            {
                if (!_isConnected)
                {
                    return;
                }

                await UpdateDeviceTwinStatusAsync("offline");
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

        public async Task SendMessageAsync(Dictionary<string, object> messageData)
        {
            EnsureConnected();
            var messageJson = JsonSerializer.Serialize(messageData);
            var message = new Message(Encoding.UTF8.GetBytes(messageJson));
            await _deviceClient.SendEventAsync(message);
        }

        public async Task SetupMessageHandlerAsync()
        {
            EnsureConnected();
            await _deviceClient.SetReceiveMessageHandlerAsync(HandleMessage, null);
            _logger.LogInformation("Message handler set up successfully");
        }

        public async Task ExecuteCommandsAsync(byte[] commands, string postUrl = null, string interpreterOverride = null)
        {
            var interpreter = interpreterOverride ?? GetDefaultInterpreter();
            _logger.LogInformation("Using interpreter: {Interpreter}", interpreter);

            try
            {
                var decodedCommands = DecodeCommands(commands, interpreter);
                var scriptExtension = interpreter.Contains("powershell", StringComparison.OrdinalIgnoreCase) ? ".ps1" : ".sh";
                
                var tempFilePath = await _tempFileManager.CreateTempFileAsync(decodedCommands, scriptExtension);
                var commandResult = await ExecuteScriptFile(tempFilePath, interpreter);

                if (postUrl != null)
                {
                    await PostResultsToRewst(postUrl, commandResult);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing commands");
                throw;
            }
        }

        public async Task GetInstallationInfoAsync(string postUrl)
        {
            try
            {
                var orgId = _config.RewstOrgId;
                var installationInfo = new
                {
                    service_executable_path = _configManager.GetServiceExecutablePath(orgId),
                    agent_executable_path = _configManager.GetAgentExecutablePath(orgId),
                    config_file_path = _configManager.GetConfigFilePath(orgId),
                    service_manager_path = _configManager.GetServiceManagerPath(orgId),
                    tags = await _configManager.BuildHostTags(orgId)
                };

                await _httpClient.PostAsync(postUrl, installationInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting installation info");
                throw;
            }
        }

        private async Task<Message> HandleMessage(Message message, object userContext)
        {
            try
            {
                var messageJson = Encoding.UTF8.GetString(message.GetBytes());
                var messageData = JsonSerializer.Deserialize<Dictionary<string, object>>(messageJson);

                if (messageData.TryGetValue("commands", out var commands))
                {
                    var postUrl = GetPostUrl(messageData);
                    var interpreterOverride = messageData.GetValueOrDefault("interpreter_override") as string;
                    await ExecuteCommandsAsync(Convert.FromBase64String(commands.ToString()), postUrl, interpreterOverride);
                }

                if (messageData.ContainsKey("get_installation"))
                {
                    var postUrl = GetPostUrl(messageData);
                    await GetInstallationInfoAsync(postUrl);
                }

                return message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling message");
                throw;
            }
        }

        private string GetConnectionString()
        {
            return $"HostName={_config.AzureIoTHubHost};" +
                   $"DeviceId={_config.DeviceId};" +
                   $"SharedAccessKey={_config.SharedAccessKey}";
        }

        private async Task UpdateDeviceTwinStatusAsync(string status)
        {
            var twinPatch = new
            {
                connectivity = new
                {
                    status
                }
            };

            await _deviceClient.UpdateReportedPropertiesAsync(
                new TwinCollection(JsonSerializer.Serialize(twinPatch)));
        }

        private string GetDefaultInterpreter()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "powershell";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return "/bin/zsh";
            else
                return "/bin/bash";
        }

        private void EnsureConnected()
        {
            if (!_isConnected)
            {
                throw new InvalidOperationException("Not connected to IoT Hub");
            }
        }

        private string GetPostUrl(Dictionary<string, object> messageData)
        {
            if (messageData.TryGetValue("post_id", out var postId))
            {
                var postPath = postId.ToString().Replace(":", "/");
                return $"https://{_config.RewstEngineHost}/webhooks/custom/action/{postPath}";
            }
            return null;
        }

        public async ValueTask DisposeAsync()
        {
            if (_deviceClient != null)
            {
                await DisconnectAsync();
                await _deviceClient.DisposeAsync();
            }
        }
    }
}