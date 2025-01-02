using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace RewstAgent.Configuration
{
    /// <summary>
    /// Implements configuration file I/O operations with error handling and logging
    /// </summary>
    public class ConfigIO : IConfigIO
    {
        private readonly ILogger<ConfigIO> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public ConfigIO(ILogger<ConfigIO> logger)
        {
            _logger = logger;
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            };
        }

        public async Task<ConfigurationData> ReadConfigurationAsync(string filePath)
        {
            try
            {
                _logger.LogInformation("Reading configuration from {FilePath}", filePath);

                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("Configuration file not found at {FilePath}", filePath);
                    return null;
                }

                var jsonContent = await File.ReadAllTextAsync(filePath);
                var config = JsonSerializer.Deserialize<ConfigurationData>(jsonContent, _jsonOptions);

                _logger.LogInformation("Successfully read configuration file");
                return config;
            }
            catch (Exception ex) when (ex is JsonException || ex is IOException)
            {
                _logger.LogError(ex, "Error reading configuration file: {Message}", ex.Message);
                throw new ConfigurationException("Failed to read configuration file", ex);
            }
        }

        public async Task WriteConfigurationAsync(string filePath, ConfigurationData configuration)
        {
            try
            {
                _logger.LogInformation("Writing configuration to {FilePath}", filePath);

                // Ensure directory exists
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var jsonContent = JsonSerializer.Serialize(configuration, _jsonOptions);
                await File.WriteAllTextAsync(filePath, jsonContent);

                _logger.LogInformation("Successfully wrote configuration file");
            }
            catch (Exception ex) when (ex is JsonException || ex is IOException)
            {
                _logger.LogError(ex, "Error writing configuration file: {Message}", ex.Message);
                throw new ConfigurationException("Failed to write configuration file", ex);
            }
        }

        public async Task<bool> ValidateConfigurationAsync(ConfigurationData configuration)
        {
            try
            {
                _logger.LogInformation("Validating configuration");

                if (configuration == null)
                {
                    _logger.LogError("Configuration is null");
                    return false;
                }

                // Validate required fields
                var isValid = !string.IsNullOrEmpty(configuration.RewstOrgId) &&
                            !string.IsNullOrEmpty(configuration.RewstEngineHost) &&
                            !string.IsNullOrEmpty(configuration.AzureIoTHubHost) &&
                            !string.IsNullOrEmpty(configuration.DeviceId) &&
                            !string.IsNullOrEmpty(configuration.SharedAccessKey);

                if (!isValid)
                {
                    _logger.LogError("Configuration validation failed: missing required fields");
                    return false;
                }

                _logger.LogInformation("Configuration validation successful");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating configuration: {Message}", ex.Message);
                throw new ConfigurationException("Failed to validate configuration", ex);
            }
        }
    }

    public class ConfigurationException : Exception
    {
        public ConfigurationException(string message) : base(message) { }
        public ConfigurationException(string message, Exception innerException) : base(message, innerException) { }
    }
}