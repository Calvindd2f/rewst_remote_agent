using Microsoft.Extensions.Logging;
using System;
using System.CommandLine;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using RewstAgent.Configuration;
using RewstAgent.IoTHub;
using RewstAgent.Services;

namespace RewstAgent
{
    /// <summary>
    /// Main configuration class for the Rewst Agent, handling setup and initialization
    /// </summary>
    public class RewstAgentConfig
    {
        private readonly ILogger<RewstAgentConfig> _logger;
        private readonly IConnectionManager _connectionManager;
        private readonly IServiceManager _serviceManager;
        private readonly IConfigurationManager _configManager;
        
        // Track the current operating system for platform-specific operations
        private static readonly string OsType = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) 
            ? "windows" 
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) 
                ? "linux" 
                : "darwin";

        public RewstAgentConfig(
            ILogger<RewstAgentConfig> logger,
            IConnectionManager connectionManager,
            IServiceManager serviceManager,
            IConfigurationManager configManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
            _serviceManager = serviceManager ?? throw new ArgumentNullException(nameof(serviceManager));
            _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
        }

        /// <summary>
        /// Outputs environment information including OS and version details
        /// </summary>
        private void OutputEnvironmentInfo()
        {
            _logger.LogInformation($"Running on {RuntimeInformation.OSDescription}");
            _logger.LogInformation($"Rewst Agent Configuration Tool v{GetType().Assembly.GetName().Version}");
        }

        /// <summary>
        /// Validates if the provided string is a valid URL
        /// </summary>
        private bool IsValidUrl(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var uriResult)
                && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        }

        /// <summary>
        /// Validates if the provided string is a valid base64 string
        /// </summary>
        private bool IsBase64(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;

            input = input.Trim();
            return Regex.IsMatch(input, @"^[A-Za-z0-9+/]*={0,2}$");
        }

        /// <summary>
        /// Removes old versions of agent files and renames them with _oldver suffix
        /// </summary>
        private async Task RemoveOldFiles(string orgId)
        {
            try
            {
                await _configManager.ArchiveOldFiles(orgId);
                _logger.LogInformation("Successfully archived old files");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while archiving old files");
                throw;
            }
        }

        /// <summary>
        /// Waits for all required files to be written to the system
        /// </summary>
        private async Task<bool> WaitForFiles(string orgId, int timeout = 3600)
        {
            _logger.LogInformation("Waiting for files to be written...");
            
            var startTime = DateTime.UtcNow;
            while (true)
            {
                if (await _configManager.AreAllFilesPresent(orgId))
                {
                    await Task.Delay(TimeSpan.FromSeconds(20));
                    _logger.LogInformation("All files have been written.");
                    return true;
                }

                if ((DateTime.UtcNow - startTime).TotalSeconds > timeout)
                {
                    _logger.LogWarning("Timeout reached while waiting for files.");
                    return false;
                }

                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }

        /// <summary>
        /// Main configuration workflow
        /// </summary>
        public async Task ConfigureAsync(string configUrl, string configSecret, string orgId)
        {
            try
            {
                OutputEnvironmentInfo();

                // Validate input parameters
                if (!IsValidUrl(configUrl))
                {
                    _logger.LogError("The config URL provided is not valid.");
                    EndProgram(1);
                }

                if (!IsBase64(configSecret))
                {
                    _logger.LogError("The config secret provided is not a valid base64 string.");
                    EndProgram(1);
                }

                // Fetch and save configuration
                _logger.LogInformation("Fetching configuration from Rewst...");
                var config = await _configManager.FetchConfiguration(configUrl, configSecret, orgId);
                if (config == null)
                {
                    _logger.LogError("Failed to fetch configuration.");
                    EndProgram(2);
                }

                // Connect to IoT Hub
                _logger.LogInformation("Connecting to IoT Hub...");
                await _connectionManager.Connect();
                await _connectionManager.SetMessageHandler();

                // Handle file management
                await RemoveOldFiles(orgId);
                await WaitForFiles(orgId);

                // Disconnect from IoT Hub
                _logger.LogInformation("Disconnecting from IoT Hub...");
                await _connectionManager.Disconnect();
                await Task.Delay(TimeSpan.FromSeconds(4));
                _logger.LogInformation("Disconnected from IoT Hub.");

                // Wait for service to start
                while (!await _serviceManager.IsServiceRunning(orgId))
                {
                    _logger.LogInformation("Waiting for the service to start...");
                    await Task.Delay(TimeSpan.FromSeconds(5));
                }

                EndProgram(0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during configuration");
                EndProgram(1);
            }
        }

        /// <summary>
        /// Ends the program with the specified exit code
        /// </summary>
        private void EndProgram(int exitCode)
        {
            _logger.LogInformation($"Agent configuration is exiting with exit level {exitCode}.");
            Environment.Exit(exitCode);
        }

        /// <summary>
        /// Entry point for the configuration process
        /// </summary>
        public static async Task<int> Main(string[] args)
        {
            var rootCommand = new RootCommand("Rewst Agent Configuration Tool");
            
            var configSecretOption = new Option<string>(
                "--config-secret",
                "Secret Key for configuration access");
            
            var configUrlOption = new Option<string>(
                "--config-url",
                "URL to fetch the configuration from");
            
            var orgIdOption = new Option<string>(
                "--org-id",
                "Organization ID to register agent within");

            rootCommand.AddOption(configSecretOption);
            rootCommand.AddOption(configUrlOption);
            rootCommand.AddOption(orgIdOption);

            rootCommand.SetHandler(async (configSecret, configUrl, orgId) =>
            {
                // Here you would set up your DI container and resolve RewstAgentConfig
                // For now, we'll use a simple creation method
                var serviceProvider = CreateServiceProvider();
                var agentConfig = serviceProvider.GetRequiredService<RewstAgentConfig>();
                
                await agentConfig.ConfigureAsync(configUrl, configSecret, orgId);
            },
            configSecretOption, configUrlOption, orgIdOption);

            return await rootCommand.InvokeAsync(args);
        }

        private static IServiceProvider CreateServiceProvider()
        {
            // Set up your dependency injection container here
            var services = new ServiceCollection()
                .AddLogging(builder => builder.AddConsole())
                .AddSingleton<IConnectionManager, ConnectionManager>()
                .AddSingleton<IServiceManager, ServiceManager>()
                .AddSingleton<IConfigurationManager, ConfigurationManager>()
                .AddSingleton<RewstAgentConfig>();

            return services.BuildServiceProvider();
        }
    }
}