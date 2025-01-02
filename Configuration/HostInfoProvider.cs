using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Management;
using System.ServiceProcess;

namespace RewstAgent.Configuration
{
    /// <summary>
    /// Provides host system information and checks for various Windows-specific features
    /// </summary>
    public class HostInfoProvider : IHostInfoProvider
    {
        private readonly ILogger<HostInfoProvider> _logger;
        private readonly ConfigurationPaths _configPaths;
        private readonly bool _isWindows;

        public HostInfoProvider(
            ILogger<HostInfoProvider> logger,
            ConfigurationPaths configPaths)
        {
            _logger = logger;
            _configPaths = configPaths;
            _isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        }

        public string GetMacAddress()
        {
            try
            {
                return NetworkInterface
                    .GetAllNetworkInterfaces()
                    .Where(nic => nic.OperationalStatus == OperationalStatus.Up &&
                           nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .Select(nic => string.Join("", nic.GetPhysicalAddress()
                        .GetAddressBytes()
                        .Select(b => b.ToString("X2"))))
                    .FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting MAC address");
                return null;
            }
        }

        public async Task<bool> IsDomainController()
        {
            if (!_isWindows) return false;

            try
            {
                var output = await RunPowerShellCommand(@"
                    $domainStatus = (Get-WmiObject Win32_ComputerSystem).DomainRole
                    if ($domainStatus -eq 4 -or $domainStatus -eq 5) {
                        return $true
                    } else {
                        return $false
                    }");

                _logger.LogInformation("Is domain controller?: {Output}", output);
                return output?.Contains("True") ?? false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking domain controller status");
                return false;
            }
        }

        public async Task<string> GetADDomainName()
        {
            if (!_isWindows) return null;

            try
            {
                var output = await RunPowerShellCommand(@"
                    $domainInfo = (Get-WmiObject Win32_ComputerSystem).Domain
                    if ($domainInfo -and $domainInfo -ne 'WORKGROUP') {
                        return $domainInfo
                    } else {
                        return $null
                    }");

                _logger.LogInformation("AD domain name: {Output}", output);
                return string.IsNullOrEmpty(output) ? null : output;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting AD domain name");
                return null;
            }
        }

        public async Task<string> GetEntraDomain()
        {
            if (!_isWindows) return null;

            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "dsregcmd",
                        Arguments = "/status",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                var lines = output.Split('\n');
                if (lines.Any(l => l.Contains("AzureAdJoined") && l.Contains("YES")))
                {
                    var domainLine = lines.FirstOrDefault(l => l.Contains("DomainName"));
                    if (domainLine != null)
                    {
                        return domainLine.Split(':')[1].Trim();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unexpected issue querying for Entra Domain");
            }

            return null;
        }

        public async Task<bool> IsEntraConnectServer()
        {
            if (!_isWindows) return false;

            var serviceNames = new[]
            {
                "ADSync",
                "Azure AD Sync",
                "EntraConnectSync",
                "OtherFutureName"
            };

            foreach (var serviceName in serviceNames)
            {
                if (await IsServiceRunning(serviceName))
                {
                    return true;
                }
            }

            return false;
        }

        public async Task<HostInformation> BuildHostTags(string orgId)
        {
            var adDomain = await GetADDomainName();
            var isDc = adDomain != null && await IsDomainController();

            return new HostInformation
            {
                AgentVersion = GetType().Assembly.GetName().Version.ToString(),
                AgentExecutablePath = _configPaths.GetAgentExecutablePath(orgId),
                ServiceExecutablePath = _configPaths.GetServiceExecutablePath(orgId),
                Hostname = Environment.MachineName,
                MacAddress = GetMacAddress(),
                OperatingSystem = RuntimeInformation.OSDescription,
                CpuModel = GetProcessorInfo(),
                RamGb = GetTotalRamGb(),
                AdDomain = adDomain,
                IsAdDomainController = isDc,
                IsEntraConnectServer = await IsEntraConnectServer(),
                EntraDomain = await GetEntraDomain(),
                OrgId = orgId
            };
        }

        private async Task<bool> IsServiceRunning(string serviceName)
        {
            if (_isWindows)
            {
                try
                {
                    using var service = new ServiceController(serviceName);
                    return service.Status == ServiceControllerStatus.Running;
                }
                catch
                {
                    return false;
                }
            }
            
            // For non-Windows platforms, check process list
            var processes = Process.GetProcesses();
            return processes.Any(p => p.ProcessName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));
        }

        private async Task<string> RunPowerShellCommand(string command)
        {
            if (!_isWindows) return null;

            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powershell",
                        Arguments = $"-Command {command}",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    var error = await process.StandardError.ReadToEndAsync();
                    _logger.LogError("PowerShell command failed: {Error}", error);
                    return null;
                }

                return output.Trim();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing PowerShell command");
                return null;
            }
        }

        private string GetProcessorInfo()
        {
            try
            {
                if (_isWindows)
                {
                    // Use WMI to get processor information on Windows
                    using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
                    using var collection = searcher.Get();
                    var processor = collection.Cast<ManagementObject>().FirstOrDefault();
                    return processor?["Name"]?.ToString() ?? "Unknown";
                }
                else
                {
                    // On Unix systems, try to read from /proc/cpuinfo
                    var cpuInfo = System.IO.File.ReadAllLines("/proc/cpuinfo");
                    var modelLine = cpuInfo.FirstOrDefault(l => l.StartsWith("model name"));
                    if (modelLine != null)
                    {
                        return modelLine.Split(':').LastOrDefault()?.Trim() ?? "Unknown";
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting processor information");
            }
            
            return RuntimeInformation.ProcessArchitecture.ToString();
        }

        private double GetTotalRamGb()
        {
            try
            {
                if (_isWindows)
                {
                    // Use WMI to get memory information on Windows
                    using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystem");
                    using var collection = searcher.Get();
                    var system = collection.Cast<ManagementObject>().FirstOrDefault();
                    if (system != null)
                    {
                        var totalMemoryBytes = Convert.ToInt64(system["TotalPhysicalMemory"]);
                        return Math.Round(totalMemoryBytes / (1024.0 * 1024.0 * 1024.0), 1);
                    }
                }
                else
                {
                    // On Unix systems, try to read from /proc/meminfo
                    var memInfo = System.IO.File.ReadAllLines("/proc/meminfo");
                    var totalLine = memInfo.FirstOrDefault(l => l.StartsWith("MemTotal"));
                    if (totalLine != null)
                    {
                        var memKb = long.Parse(new string(totalLine.Where(char.IsDigit).ToArray()));
                        return Math.Round(memKb / (1024.0 * 1024.0), 1);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting total RAM information");
            }

            return 0.0;
        }
    }
}