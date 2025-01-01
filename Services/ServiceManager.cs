using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace RewstAgent.Services
{
    public class ServiceManager : IServiceManager
    {
        private readonly ILogger<ServiceManager> _logger;
        private readonly string _basePath;
        private readonly bool _isWindows;

        public ServiceManager(ILogger<ServiceManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            _basePath = _isWindows
                ? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Rewst", "Agent")
                : "/opt/rewst/agent";
        }

        public async Task<bool> IsServiceRunning(string orgId)
        {
            try
            {
                if (_isWindows)
                {
                    return IsWindowsServiceRunning(GetServiceName(orgId));
                }
                else
                {
                    return await IsLinuxServiceRunning(GetServiceName(orgId));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking service status");
                return false;
            }
        }

        public async Task<bool> InstallService(string orgId)
        {
            try
            {
                var serviceManagerPath = GetServiceManagerPath(orgId);
                var startInfo = new ProcessStartInfo
                {
                    FileName = serviceManagerPath,
                    Arguments = $"--org-id {orgId} --install",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = startInfo };
                process.Start();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    var error = await process.StandardError.ReadToEndAsync();
                    _logger.LogError("Service installation failed: {Error}", error);
                    return false;
                }

                _logger.LogInformation("Service installed successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error installing service");
                return false;
            }
        }

        public async Task<bool> StartService(string orgId)
        {
            try
            {
                var serviceManagerPath = GetServiceManagerPath(orgId);
                var startInfo = new ProcessStartInfo
                {
                    FileName = serviceManagerPath,
                    Arguments = $"--org-id {orgId} --start",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = startInfo };
                process.Start();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    var error = await process.StandardError.ReadToEndAsync();
                    _logger.LogError("Service start failed: {Error}", error);
                    return false;
                }

                _logger.LogInformation("Service started successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting service");
                return false;
            }
        }

        private bool IsWindowsServiceRunning(string serviceName)
        {
            using var serviceController = new ServiceController(serviceName);
            try
            {
                return serviceController.Status == ServiceControllerStatus.Running;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private async Task<bool> IsLinuxServiceRunning(string serviceName)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "systemctl",
                Arguments = $"is-active {serviceName}",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();
            await process.WaitForExitAsync();

            return process.ExitCode == 0;
        }

        private string GetServiceName(string orgId) 
            => $"rewst-agent-{orgId}";

        private string GetServiceManagerPath(string orgId)
            => System.IO.Path.Combine(_basePath, orgId,
                _isWindows ? "rewst_service_manager.exe" : "rewst_service_manager");
    }
}