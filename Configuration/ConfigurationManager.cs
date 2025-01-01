using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace RewstAgent.Configuration
{
    public class ConfigurationManager : IConfigurationManager
    {
        private readonly ILogger<ConfigurationManager> _logger;
        private readonly HttpClient _httpClient;
        private readonly string _basePath;

        public ConfigurationManager(ILogger<ConfigurationManager> logger, IHttpClientFactory httpClientFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClient = httpClientFactory?.CreateClient("RewstConfig") 
                ?? throw new ArgumentNullException(nameof(httpClientFactory));
            
            // Determine base path based on OS
            _basePath = Environment.OSVersion.Platform == PlatformID.Unix 
                ? "/opt/rewst/agent" 
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Rewst", "Agent");
        }

        public async Task<ConfigurationData> FetchConfiguration(string configUrl, string configSecret, string orgId)
        {
            try
            {
                _logger.LogInformation("Fetching configuration from {Url}", configUrl);

                // Add authorization header
                _httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", configSecret);

                var response = await _httpClient.GetAsync(configUrl);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var config = JsonSerializer.Deserialize<ConfigurationData>(content);

                if (config.RewstOrgId != orgId)
                {
                    throw new InvalidOperationException("Configuration organization ID does not match provided ID");
                }

                // Save configuration to file
                await SaveConfigurationFile(content, orgId);

                return config;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch configuration");
                return null;
            }
        }

        public async Task ArchiveOldFiles(string orgId)
        {
            var filesToArchive = new[]
            {
                GetServiceManagerPath(orgId),
                GetAgentExecutablePath(orgId),
                GetServiceExecutablePath(orgId)
            };

            foreach (var filePath in filesToArchive)
            {
                if (File.Exists(filePath))
                {
                    try
                    {
                        var archivePath = $"{filePath}_oldver";
                        if (File.Exists(archivePath))
                        {
                            File.Delete(archivePath);
                        }
                        File.Move(filePath, archivePath);
                        _logger.LogInformation("Archived {File} to {ArchivePath}", filePath, archivePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to archive file {File}", filePath);
                        throw;
                    }
                }
            }
        }

        public async Task<bool> AreAllFilesPresent(string orgId)
        {
            var requiredFiles = new[]
            {
                GetServiceManagerPath(orgId),
                GetAgentExecutablePath(orgId)
            };

            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                requiredFiles = requiredFiles.Append(GetServiceExecutablePath(orgId)).ToArray();
            }

            return requiredFiles.All(File.Exists);
        }

        private async Task SaveConfigurationFile(string content, string orgId)
        {
            var configPath = Path.Combine(_basePath, orgId, "config.json");
            var configDir = Path.GetDirectoryName(configPath);

            if (!Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
            }

            await File.WriteAllTextAsync(configPath, content);
            _logger.LogInformation("Configuration saved to {Path}", configPath);
        }

        private string GetServiceManagerPath(string orgId)
            => Path.Combine(_basePath, orgId, 
                Environment.OSVersion.Platform == PlatformID.Win32NT 
                    ? "rewst_service_manager.exe" 
                    : "rewst_service_manager");

        private string GetAgentExecutablePath(string orgId)
            => Path.Combine(_basePath, orgId,
                Environment.OSVersion.Platform == PlatformID.Win32NT
                    ? "rewst_agent.exe"
                    : "rewst_agent");

        private string GetServiceExecutablePath(string orgId)
            => Path.Combine(_basePath, orgId, "rewst_service.exe");
    }
}