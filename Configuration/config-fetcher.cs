using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace RewstAgent.Configuration
{
    /// <summary>
    /// Handles fetching and updating configuration from remote endpoints
    /// </summary>
    public class ConfigFetcher : IConfigFetcher
    {
        private readonly ILogger<ConfigFetcher> _logger;
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;

        public ConfigFetcher(ILogger<ConfigFetcher> logger, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient();
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        public async Task<ConfigurationData> FetchRemoteConfigurationAsync(string configUrl, string configSecret, string orgId)
        {
            try
            {
                _logger.LogInformation("Fetching remote configuration from {ConfigUrl}", configUrl);

                var request = new HttpRequestMessage(HttpMethod.Post, configUrl);
                var payload = new
                {
                    secret = configSecret,
                    orgId = orgId
                };

                var jsonContent = JsonSerializer.Serialize(payload);
                request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var config = JsonSerializer.Deserialize<ConfigurationData>(responseContent, _jsonOptions);

                _logger.LogInformation("Successfully fetched remote configuration");
                return config;
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is JsonException)
            {
                _logger.LogError(ex, "Error fetching remote configuration: {Message}", ex.Message);
                throw new ConfigurationException("Failed to fetch remote configuration", ex);
            }
        }

        public async Task<ConfigurationData> UpdateConfigurationAsync(ConfigurationData localConfig)
        {
            try
            {
                _logger.LogInformation("Updating configuration for organization {OrgId}", localConfig.RewstOrgId);

                // Construct the update URL using the engine host
                var updateUrl = $"{localConfig.RewstEngineHost}/api/v1/agent/config/update";
                
                var request = new HttpRequestMessage(HttpMethod.Post, updateUrl);
                var jsonContent = JsonSerializer.Serialize(localConfig, _jsonOptions);
                request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var updatedConfig = JsonSerializer.Deserialize<ConfigurationData>(responseContent, _jsonOptions);

                _logger.LogInformation("Successfully updated configuration");
                return updatedConfig;
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is JsonException)
            {
                _logger.LogError(ex, "Error updating configuration: {Message}", ex.Message);
                throw new ConfigurationException("Failed to update configuration", ex);
            }
        }
    }
}