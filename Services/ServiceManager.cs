using System.Diagnostics;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using Microsoft.Extensions.Logging;
using RewstAgent.Configuration;

namespace RewstAgent.Services
{
    public class ServiceManager
    {
        private readonly ILogger<ServiceManager> _logger;
        private readonly IConfigurationManager _configManager;
        private readonly string _osType;

        public ServiceManager(ILogger<ServiceManager> logger, IConfigurationManager configManager)
        {
            _logger = logger;
            _configManager = configManager;
            _osType = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) 
                ? "windows" 
                : RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                    ? "linux"
                    : "darwin";
        }

        public string GetServiceName(string orgId)
        {
            return $"RewstRemoteAgent_{orgId}";
        }

        public bool IsServiceInstalled(string orgId)
        {
            var serviceName = GetServiceName(orgId);

            try
            {
                if (_osType == "windows")
                {
                    using var serviceController = new ServiceController(serviceName);
                    var status = serviceController.Status; // This will throw if service doesn't exist
                    _logger.LogInformation($"Service {serviceName} is installed.");
                    return true;
                }
                else if (_osType == "linux")
                {
                    var servicePath = $"/etc/systemd/system/{serviceName}.service";
                    var exists = File.Exists(servicePath);
                    _logger.LogInformation($"Service {serviceName} is {(exists ? "" : "not ")}installed.");
                    return exists;
                }
                else if (_osType == "darwin")
                {
                    var plistPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        "Library/LaunchAgents",
                        $"{serviceName}.plist");
                    var exists = File.Exists(plistPath);
                    _logger.LogInformation($"Service {serviceName} is {(exists ? "" : "not ")}installed.");
                    return exists;
                }

                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        public bool IsServiceRunning(string orgId)
        {
            var executablePath = _configManager.GetAgentExecutablePath(orgId);
            var executableName = Path.GetFileName(executablePath);

            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    if (process.ProcessName.Equals(
                        Path.GetFileNameWithoutExtension(executableName),
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                catch (Exception)
                {
                    // Process may have exited between enumeration and access
                    continue;
                }
            }

            return false;
        }

        public async Task InstallService(string orgId)
        {
            var serviceName = GetServiceName(orgId);
            var executablePath = _configManager.GetAgentExecutablePath(orgId);
            var configFilePath = _configManager.GetConfigFilePath(orgId);

            _logger.LogInformation($"Installing {serviceName} Service...");

            if (IsServiceInstalled(orgId))
            {
                _logger.LogInformation("Service is already installed.");
                return;
            }

            if (_osType == "windows")
            {
                var serviceExePath = _configManager.GetServiceExecutablePath(orgId);
                await RunProcessAsync(serviceExePath, "install");
            }
            else if (_osType == "linux")
            {
                var serviceContent = $@"
[Unit]
Description={serviceName}

[Service]
ExecStart={executablePath} --config-file {configFilePath}
Restart=always

[Install]
WantedBy=multi-user.target
";
                var servicePath = $"/etc/systemd/system/{serviceName}.service";
                await File.WriteAllTextAsync(servicePath, serviceContent);
                
                await RunProcessAsync("systemctl", "daemon-reload");
                await RunProcessAsync("systemctl", $"enable {serviceName}");
            }
            else if (_osType == "darwin")
            {
                var plistContent = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0"">
<dict>
    <key>Label</key>
    <string>{serviceName}</string>
    <key>ProgramArguments</key>
    <array>
        <string>{executablePath}</string>
        <string>--config-file</string>
        <string>{configFilePath}</string>
    </array>
    <key>RunAtLoad</key>
    <true/>
</dict>
</plist>";
                var plistPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Library/LaunchAgents",
                    $"{serviceName}.plist");
                
                await File.WriteAllTextAsync(plistPath, plistContent);
                await RunProcessAsync("launchctl", $"load {serviceName}");
            }
        }

        public async Task UninstallService(string orgId)
        {
            var serviceName = GetServiceName(orgId);
            _logger.LogInformation($"Uninstalling service {serviceName}");

            try
            {
                await StopService(orgId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to stop service");
            }

            if (_osType == "windows")
            {
                try
                {
                    using var serviceController = new ServiceController(serviceName);
                    if (serviceController.Status != ServiceControllerStatus.Stopped)
                    {
                        serviceController.Stop();
                        serviceController.WaitForStatus(ServiceControllerStatus.Stopped);
                    }
                    await RunProcessAsync("sc", $"delete {serviceName}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Exception removing service");
                }
            }
            else if (_osType == "linux")
            {
                await RunProcessAsync("systemctl", $"disable {serviceName}");
                await RunProcessAsync("rm", $"/etc/systemd/system/{serviceName}.service");
                await RunProcessAsync("systemctl", "daemon-reload");
            }
            else if (_osType == "darwin")
            {
                var plistPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Library/LaunchAgents",
                    $"{serviceName}.plist");
                
                await RunProcessAsync("launchctl", $"unload {plistPath}");
                if (File.Exists(plistPath))
                {
                    File.Delete(plistPath);
                }
            }
        }

        public async Task<string> CheckServiceStatus(string orgId)
        {
            var serviceName = GetServiceName(orgId);

            try
            {
                if (_osType == "windows")
                {
                    using var serviceController = new ServiceController(serviceName);
                    return $"Service status: {serviceController.Status}";
                }
                else if (_osType == "linux")
                {
                    var result = await RunProcessAsync("systemctl", $"is-active {serviceName}");
                    return $"Service status: {result}";
                }
                else if (_osType == "darwin")
                {
                    var result = await RunProcessAsync("launchctl", $"list {serviceName}");
                    return result.Contains(serviceName) 
                        ? "Service status: Running"
                        : "Service status: Not Running";
                }

                return $"Unsupported OS type: {_osType}";
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        public async Task StartService(string orgId)
        {
            var serviceName = GetServiceName(orgId);
            _logger.LogInformation($"Starting Service {serviceName} for {_osType}");

            if (_osType == "windows")
            {
                using var serviceController = new ServiceController(serviceName);
                serviceController.Start();
                serviceController.WaitForStatus(ServiceControllerStatus.Running);
            }
            else if (_osType == "linux")
            {
                await RunProcessAsync("systemctl", $"start {serviceName}");
            }
            else if (_osType == "darwin")
            {
                await RunProcessAsync("launchctl", $"start {serviceName}");
            }
        }

        public async Task StopService(string orgId)
        {
            var serviceName = GetServiceName(orgId);

            if (_osType == "windows")
            {
                try
                {
                    using var serviceController = new ServiceController(serviceName);
                    if (serviceController.Status != ServiceControllerStatus.Stopped)
                    {
                        serviceController.Stop();
                        serviceController.WaitForStatus(ServiceControllerStatus.Stopped);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to stop service");
                }
            }
            else if (_osType == "linux")
            {
                await RunProcessAsync("systemctl", $"stop {serviceName}");
            }
            else if (_osType == "darwin")
            {
                await RunProcessAsync("launchctl", $"stop {serviceName}");
            }
        }

        public async Task RestartService(string orgId)
        {
            await StopService(orgId);
            await StartService(orgId);
        }

        private async Task<string> RunProcessAsync(string fileName, string arguments)
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            try
            {
                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    var error = await process.StandardError.ReadToEndAsync();
                    throw new Exception($"Process exited with code {process.ExitCode}: {error}");
                }

                return output.Trim();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error running process {fileName} {arguments}");
                throw;
            }
        }
    }
}