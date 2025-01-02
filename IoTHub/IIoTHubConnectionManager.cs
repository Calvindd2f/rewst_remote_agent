using Microsoft.Azure.Devices.Client;

namespace RewstAgent.IoTHub
{
    /// <summary>
    /// Represents the contract for managing IoT Hub connections and message handling.
    /// </summary>
    public interface IIoTHubConnectionManager : IAsyncDisposable
    {
        Task ConnectAsync();
        Task DisconnectAsync();
        Task SendMessageAsync(Dictionary<string, object> messageData);
        Task SetupMessageHandlerAsync();
        Task ExecuteCommandsAsync(byte[] commands, string postUrl = null, string interpreterOverride = null);
        Task GetInstallationInfoAsync(string postUrl);
    }

    /// <summary>
    /// Configuration data structure for IoT Hub connection.
    /// </summary>
    public class IoTHubConfig
    {
        public string AzureIoTHubHost { get; set; }
        public string DeviceId { get; set; }
        public string SharedAccessKey { get; set; }
        public string RewstOrgId { get; set; }
        public string RewstEngineHost { get; set; }
    }

    /// <summary>
    /// Represents the result of a command execution.
    /// </summary>
    public class CommandExecutionResult
    {
        public string Output { get; set; }
        public string Error { get; set; }
    }

    /// <summary>
    /// Service for handling system commands execution.
    /// </summary>
    public interface ICommandExecutionService
    {
        Task<CommandExecutionResult> ExecuteCommandAsync(
            string command,
            string interpreter,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Service for managing temporary files.
    /// </summary>
    public interface ITempFileManager : IAsyncDisposable
    {
        Task<string> CreateTempFileAsync(string content, string extension);
        Task DeleteTempFileAsync(string filePath);
    }

    /// <summary>
    /// Service for making HTTP requests.
    /// </summary>
    public interface IHttpClientService
    {
        Task<HttpResponseMessage> PostAsync(string url, object content);
    }
}