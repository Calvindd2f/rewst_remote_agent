using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace RewstAgent.Configuration
{
    /// <summary>
    /// Manages configuration and executable paths across different operating systems,
    /// mirroring the Python platformdirs functionality
    /// </summary>
    public class ConfigurationPaths
    {
        private readonly ILogger<ConfigurationPaths> _logger;
        private readonly string _osType;

        public ConfigurationPaths(ILogger<ConfigurationPaths> logger)
        {
            _logger = logger;
            _osType = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows" :
                     RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux" :
                     RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "darwin" : "unknown";
        }

        /// <summary>
        /// Gets the base configuration directory for the current platform
        /// </summary>
        public string GetBaseConfigDirectory()
        {
            return _osType switch
            {
                "windows" => Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "RewstRemoteAgent"),
                "linux" => "/etc/rewst_remote_agent",
                "darwin" => Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Library/Application Support/RewstRemoteAgent"),
                _ => throw new PlatformNotSupportedException($"Unsupported OS type: {_osType}")
            };
        }

        /// <summary>
        /// Gets the executable folder path for the organization
        /// </summary>
        public string GetExecutableFolder(string orgId)
        {
            return _osType switch
            {
                "windows" => Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "RewstRemoteAgent", orgId),
                "linux" => "/usr/local/bin",
                "darwin" => Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Library/Application Support/RewstRemoteAgent", orgId),
                _ => throw new PlatformNotSupportedException($"Unsupported OS type: {_osType}")
            };
        }

        /// <summary>
        /// Gets the path for the service manager executable
        /// </summary>
        public string GetServiceManagerPath(string orgId)
        {
            var executableName = _osType switch
            {
                "windows" => $"rewst_service_manager.win_{orgId}.exe",
                "linux" => $"rewst_service_manager.linux_{orgId}.bin",
                "darwin" => $"rewst_service_manager.macos_{orgId}.bin",
                _ => throw new PlatformNotSupportedException($"Unsupported OS type: {_osType}")
            };

            return Path.Combine(GetExecutableFolder(orgId), executableName);
        }

        /// <summary>
        /// Gets the path for the agent executable
        /// </summary>
        public string GetAgentExecutablePath(string orgId)
        {
            var executableName = _osType switch
            {
                "windows" => $"rewst_remote_agent_{orgId}.win.exe",
                "linux" => $"rewst_remote_agent_{orgId}.linux.bin",
                "darwin" => $"rewst_remote_agent_{orgId}.macos.bin",
                _ => throw new PlatformNotSupportedException($"Unsupported OS type: {_osType}")
            };

            return Path.Combine(GetExecutableFolder(orgId), executableName);
        }

        /// <summary>
        /// Gets the path for the service executable (Windows-specific)
        /// </summary>
        public string GetServiceExecutablePath(string orgId)
        {
            if (_osType != "windows")
            {
                _logger.LogInformation("Windows Service executable is only necessary for Windows, not {OsType}", _osType);
                return null;
            }

            var executableName = $"rewst_windows_service_{orgId}.win.exe";
            return Path.Combine(GetExecutableFolder(orgId), executableName);
        }

        /// <summary>
        /// Gets the path for log files
        /// </summary>
        public string GetLoggingPath(string orgId)
        {
            var logFileName = "rewst_agent.log";
            return _osType switch
            {
                "windows" => Path.Combine(GetBaseConfigDirectory(), orgId, "logs", logFileName),
                "linux" => Path.Combine("/var/log/rewst_remote_agent", orgId, logFileName),
                "darwin" => Path.Combine("/var/log/rewst_remote_agent", orgId, logFileName),
                _ => throw new PlatformNotSupportedException($"Unsupported OS type: {_osType}")
            };
        }

        /// <summary>
        /// Gets the path for the configuration file
        /// </summary>
        public string GetConfigFilePath(string orgId)
        {
            var configDir = _osType switch
            {
                "windows" => Path.Combine(GetBaseConfigDirectory(), orgId),
                "linux" => Path.Combine("/etc/rewst_remote_agent", orgId),
                "darwin" => Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Library/Application Support/RewstRemoteAgent",
                    orgId),
                _ => throw new PlatformNotSupportedException($"Unsupported OS type: {_osType}")
            };

            // Ensure the directory exists
            if (!Directory.Exists(configDir))
            {
                try
                {
                    Directory.CreateDirectory(configDir);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create directory {ConfigDir}", configDir);
                    throw;
                }
            }

            var configPath = Path.Combine(configDir, "config.json");
            _logger.LogInformation("Config File Path: {ConfigPath}", configPath);
            return configPath;
        }
    }
}