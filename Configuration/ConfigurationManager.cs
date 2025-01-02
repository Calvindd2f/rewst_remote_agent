using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RewstAgent.Configuration
{
    /// <summary>
    /// Enhanced configuration manager that handles loading, saving, and managing configuration
    /// across different platforms, mirroring the Python implementation's functionality
    /// </summary>
    public class ConfigurationManager : IConfigurationManager
    {
        private readonly ILogger<ConfigurationManager> _logger;
        private readonly ConfigurationPaths _configPaths;
        private readonly JsonSerializerOptions _jsonOptions;

        public ConfigurationManager(
            ILogger<ConfigurationManager> logger,
            ConfigurationPaths configPaths)
        {
            _logger = logger;
            _configPaths = configPaths;
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            };
        }

        /// <summary>
        /// Loads configuration from the specified file or uses the default location
        /// </summary>
        public async Task<ConfigurationData> LoadConfigurationAsync(string orgId, string configFilePath = null)
        {
            try
            {
                var path = configFilePath ?? _configPaths.GetConfigFilePath(orgId);
                _logger.LogInformation("Loading configuration from {Path}", path);

                if (!File.Exists(path))
                {
                    _logger.LogWarning("Configuration file not found at {Path}", path);
                    return null;
                }

                var jsonContent = await File.ReadAllTextAsync(path);
                var config = JsonSerializer.Deserialize<ConfigurationData>(jsonContent, _jsonOptions);
                
                return config;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading configuration");
                return null;
            }
        }

        /// <summary>
        /// Saves configuration to the specified file
        /// </summary>
        public async Task SaveConfigurationAsync(ConfigurationData configData, string configFile = null)
        {
            try
            {
                var path = configFile ?? _configPaths.GetConfigFilePath(configData.RewstOrgId);
                _logger.LogInformation("Saving configuration to {Path}", path);

                var jsonContent = JsonSerializer.Serialize(configData, _jsonOptions);
                await File.WriteAllTextAsync(path, jsonContent);

                _logger.LogInformation("Configuration saved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving configuration");
                throw new ConfigurationException("Failed to save configuration", ex);
            }
        }

        /// <summary>
        /// Extracts organization ID from the executable name
        /// </summary>
        public string GetOrgIdFromExecutableName(string[] commandLineArgs)
        {
            if (commandLineArgs == null || commandLineArgs.Length == 0)
            {
                return null;
            }

            var executablePath = commandLineArgs[0];
            var pattern = new Regex(@"rewst_.*_(.+?)\.");
            var match = pattern.Match(executablePath);

            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            return null;
        }

        /// <summary>
        /// Sets up file logging for the specified organization
        /// </summary>
        public async Task<bool> SetupFileLoggingAsync(string orgId)
        {
            try
            {
                var logPath = _configPaths.GetLoggingPath(orgId);
                _logger.LogInformation("Configuring logging to file: {LogPath}", logPath);

                var logDir = Path.GetDirectoryName(logPath);
                if (!Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }

                // Note: The actual log configuration should be done through the logging framework
                // This method just ensures the directory exists and is accessible
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting up file logging");
                return false;
            }
        }

        public async Task<bool> AreAllFilesPresent(string orgId)
        {
            var agentPath = _configPaths.GetAgentExecutablePath(orgId);
            var servicePath = _configPaths.GetServiceExecutablePath(orgId);
            var configPath = _configPaths.GetConfigFilePath(orgId);

            var files = new[] { agentPath, configPath };
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                files = new[] { agentPath, servicePath, configPath };
            }

            return files.All(f => File.Exists(f));
        }

        public async Task ArchiveOldFiles(string orgId)
        {
            var files = new[]
            {
                _configPaths.GetAgentExecutablePath(orgId),
                _configPaths.GetServiceExecutablePath(orgId),
                _configPaths.GetServiceManagerPath(orgId)
            };

            foreach (var file in files.Where(f => !string.IsNullOrEmpty(f) && File.Exists(f)))
            {
                try
                {
                    var backupPath = $"{file}_oldver";
                    if (File.Exists(backupPath))
                    {
                        File.Delete(backupPath);
                    }
                    File.Move(file, backupPath);
                    _logger.LogInformation("Archived {File} to {BackupPath}", file, backupPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error archiving file {File}", file);
                }
            }
        }
    }
}