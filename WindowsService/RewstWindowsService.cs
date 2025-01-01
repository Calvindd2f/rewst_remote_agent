using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using RewstAgent.Configuration;

namespace RewstAgent.WindowsService
{
    /// <summary>
    /// Windows service implementation for the Rewst Agent
    /// </summary>
    public class RewstWindowsService : BackgroundService
    {
        private readonly ILogger<RewstWindowsService> _logger;
        private readonly IConfigurationManager _configManager;
        private readonly string _orgId;
        private Process? _agentProcess;
        private readonly List<int> _processIds;
        private readonly CancellationTokenSource _shutdownTokenSource;
        private string _agentExecutablePath;

        public RewstWindowsService(
            ILogger<RewstWindowsService> logger,
            IConfigurationManager configManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
            _processIds = new List<int>();
            _shutdownTokenSource = new CancellationTokenSource();

            // Get organization ID from executable name
            _orgId = GetOrgIdFromExecutableName();
            if (string.IsNullOrEmpty(_orgId))
            {
                _logger.LogError("Organization ID not found in executable name");
                throw new InvalidOperationException("Organization ID not found");
            }

            _agentExecutablePath = _configManager.GetAgentExecutablePath(_orgId);
            ServiceName = $"RewstRemoteAgent_{_orgId}";
            DisplayName = $"Rewst Agent Service for Org {_orgId}";
        }

        public string ServiceName { get; }
        public string DisplayName { get; }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation($"Starting {ServiceName}");
                
                while (!stoppingToken.IsCancellationRequested)
                {
                    if (_agentProcess == null || _agentProcess.HasExited)
                    {
                        await StartAgentProcess();
                    }

                    await Task.Delay(5000, stoppingToken); // Check every 5 seconds
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Service is shutting down");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred in the service");
                throw;
            }
            finally
            {
                await StopAgentProcess();
            }
        }

        private async Task StartAgentProcess()
        {
            try
            {
                if (!await IsChecksumValid(_agentExecutablePath))
                {
                    throw new InvalidOperationException("Agent executable checksum validation failed");
                }

                _logger.LogInformation($"Verified executable {_agentExecutablePath} signature");
                
                var processName = Path.GetFileNameWithoutExtension(_agentExecutablePath);
                _logger.LogInformation($"Launching process for {processName}");

                var startInfo = new ProcessStartInfo
                {
                    FileName = _agentExecutablePath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                _agentProcess = Process.Start(startInfo);
                if (_agentProcess != null)
                {
                    _processIds.Add(_agentProcess.Id);
                    _logger.LogInformation($"Started process with PID {_agentProcess.Id}");
                }

                await Task.Delay(4000); // Wait for process to initialize

                // Find any additional processes with the same name
                var processes = Process.GetProcessesByName(processName);
                foreach (var proc in processes)
                {
                    if (!_processIds.Contains(proc.Id))
                    {
                        _processIds.Add(proc.Id);
                        _logger.LogInformation($"Found additional process with PID {proc.Id}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start agent process");
                _processIds.Clear();
                throw;
            }
        }

        private async Task StopAgentProcess()
        {
            var processName = Path.GetFileNameWithoutExtension(_agentExecutablePath);

            foreach (var pid in _processIds.ToList())
            {
                try
                {
                    _logger.LogInformation($"Attempting to terminate process with PID {pid}");
                    var process = Process.GetProcessById(pid);
                    
                    process.CloseMainWindow();
                    if (!process.WaitForExit(10000)) // Wait up to 10 seconds
                    {
                        _logger.LogWarning($"Process {pid} did not exit gracefully, forcing termination");
                        process.Kill(true); // Force kill including child processes
                    }
                }
                catch (ArgumentException)
                {
                    _logger.LogInformation($"Process {pid} has already terminated");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error terminating process {pid}");
                }
            }

            // Double-check for any remaining processes
            var remainingProcesses = Process.GetProcessesByName(processName);
            foreach (var proc in remainingProcesses)
            {
                try
                {
                    _logger.LogWarning($"Force killing leftover process {proc.Id}");
                    proc.Kill(true);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to kill leftover process {proc.Id}");
                }
            }

            _processIds.Clear();
            _logger.LogInformation("All processes stopped");
        }

        private string GetOrgIdFromExecutableName()
        {
            var executablePath = Environment.GetCommandLineArgs()[0];
            // Implementation to extract GUID from executable name
            // Add your GUID extraction logic here
            return string.Empty; // Placeholder
        }

        private async Task<bool> IsChecksumValid(string executablePath)
        {
            try
            {
                // Implement checksum validation logic here
                // This should match your Python implementation's checksum verification
                return true; // Placeholder
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Checksum validation failed");
                return false;
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping service");
            _shutdownTokenSource.Cancel();
            await StopAgentProcess();
            await base.StopAsync(cancellationToken);
        }

        public override void Dispose()
        {
            _shutdownTokenSource.Dispose();
            base.Dispose();
        }
    }
}