using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using RewstAgent.Configuration;
using RewstAgent.IoTHub;

namespace RewstAgent.RemoteAgent
{
    /// <summary>
    /// Main entry point for the Rewst Remote Agent service
    /// </summary>
    public class RemoteAgentService : BackgroundService
    {
        private readonly ILogger<RemoteAgentService> _logger;
        private readonly IConfigurationManager _configManager;
        private readonly IConnectionManager _connectionManager;
        private readonly CancellationTokenSource _shutdownTokenSource;
        private readonly string _osType;

        public RemoteAgentService(
            ILogger<RemoteAgentService> logger,
            IConfigurationManager configManager,
            IConnectionManager connectionManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
            _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
            _shutdownTokenSource = new CancellationTokenSource();
            
            _osType = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) 
                ? "windows" 
                : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) 
                    ? "linux" 
                    : "darwin";
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation($"Version: {GetType().Assembly.GetName().Version}");
                _logger.LogInformation($"Running on {_osType}");

                // Load configuration
                _logger.LogInformation("Loading Configuration");
                var (orgId, configData) = await LoadConfigurationAsync();
                
                if (configData == null)
                {
                    throw new ConfigurationException("No configuration was found.");
                }

                _logger.LogInformation($"Running for Org ID {orgId}");

                // Set up file logging
                _logger.LogInformation("Setting up file logging");
                await SetupFileLogging(orgId);

                // Register for graceful shutdown
                using var combinedTokenSource = CancellationTokenSource
                    .CreateLinkedTokenSource(stoppingToken, _shutdownTokenSource.Token);

                // Start the IoT Hub connection loop
                await RunIoTHubConnectionLoop(configData, combinedTokenSource.Token);
            }
            catch (ConfigurationException ex)
            {
                _logger.LogError(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred in the Remote Agent");
            }
        }

        private async Task<(string OrgId, ConfigurationData Config)> LoadConfigurationAsync()
        {
            string configFile = null; // Could be passed as a parameter if needed
            string orgId;
            ConfigurationData configData;

            if (!string.IsNullOrEmpty(configFile))
            {
                _logger.LogInformation($"Using config file {configFile}");
                configData = await _configManager.LoadConfiguration(null, configFile);
                orgId = configData?.RewstOrgId;
            }
            else
            {
                orgId = GetOrgIdFromExecutableName();
                if (!string.IsNullOrEmpty(orgId))
                {
                    _logger.LogInformation($"Found Org ID {orgId} via executable name.");
                    configData = await _configManager.LoadConfiguration(orgId);
                }
                else
                {
                    _logger.LogWarning("Did not find guid in executable name.");
                    configData = null;
                }
            }

            return (orgId, configData);
        }

        private string GetOrgIdFromExecutableName()
        {
            // Implementation similar to Python's get_org_id_from_executable_name
            var executablePath = Environment.GetCommandLineArgs()[0];
            // Extract GUID from executable name using regex or parsing
            // Return the org ID or null if not found
            return null; // Placeholder - implement actual logic
        }

        private async Task SetupFileLogging(string orgId)
        {
            try
            {
                // Implement file logging setup
                // This would typically be done through ILoggingBuilder configuration
                // in Program.cs, but additional setup could be done here
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred setting up file-based logging.");
            }
        }

        private async Task RunIoTHubConnectionLoop(
            ConfigurationData configData, 
            CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await _connectionManager.Connect();
                    await _connectionManager.SetMessageHandler();

                    // Keep the connection alive until cancellation is requested
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Normal shutdown
                    _logger.LogInformation("Shutting down gracefully.");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in IoT Hub connection loop. Retrying...");
                    await Task.Delay(5000, cancellationToken); // Wait before retrying
                }
                finally
                {
                    await _connectionManager.Disconnect();
                }
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping Remote Agent service");
            _shutdownTokenSource.Cancel();
            await base.StopAsync(cancellationToken);
        }

        public override void Dispose()
        {
            _shutdownTokenSource.Dispose();
            base.Dispose();
        }
    }

    public class ConfigurationException : Exception
    {
        public ConfigurationException(string message) : base(message) { }
    }
}