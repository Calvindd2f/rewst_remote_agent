using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace RewstAgent.Configuration
{
    /// <summary>
    /// Implements remote configuration fetching with retry logic
    /// </summary>
    public class ConfigurationFetcher : IConfigurationFetcher
    {
        private readonly ILogger<ConfigurationFetcher> _logger;
        private readonly HttpClient _httpClient;
        private readonly IHostInfoProvider _hostInfoProvider;
        
        private static readonly IEnumerable<(int interval, int maxRetries)> DefaultRetryIntervals = new[]
        {
            (5, 12),    // 5 seconds, 12 times
            (60, 60),   // 1 minute, 60 times
            (300, -1)   // 5 minutes, infinite times
        };

        private static readonly string[] RequiredKeys = new[]
        {
            "azure_iot_hub_host",
            "device_id",
            "shared_access_key",
            "rewst_engine_host",
            "rewst_org_id"
        };

        public ConfigurationFetcher(
            ILogger<ConfigurationFetcher> logger,
            HttpClient httpClient,
            IHostInfoProvider hostInfoProvider)
        {
            _logger = logger;
            _httpClient = httpClient;
            _hostInfoProvider = hostInfoProvider;
        }

        public async Task<ConfigurationData> FetchConfigurationAsync(
            string configUrl,
            string secret,
            string orgId,
            IEnumerable<(int interval, int maxRetries)> retryIntervals = null)
        {
            retryIntervals ??= DefaultRetryIntervals;
            
            foreach (var (interval, maxRetries) in retryIntervals)
            {
                var retryCount = 0;
                while (maxRetries < 0 || retryCount < maxRetries)
                {
                    retryCount++;
                    try
                    {
                        var hostInfo = await _hostInfoProvider.BuildHostTags(orgId);
                        
                        using var request = new HttpRequestMessage(HttpMethod.Post, configUrl);
                        if (!string.IsNullOrEmpty(secret))
                        {
                            request.Headers.Add("x-rewst-secret", secret);
                        }
                        
                        request.Content = JsonContent.Create(hostInfo);
                        
                        _logger.LogDebug("Sending host information to {ConfigUrl}: {@HostInfo}", 
                            configUrl, hostInfo);

                        var response = await _httpClient.SendAsync(request);
                        
                        if (response.StatusCode == System.Net.HttpStatusCode.SeeOther)
                        {
                            _logger.LogInformation("Waiting while Rewst processes Agent Registration...");
                        }
                        else if (response.IsSuccessStatusCode)
                        {
                            var data = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
                            if (data?.TryGetValue("configuration", out var configObj) == true)
                            {
                                var config = System.Text.Json.JsonSerializer
                                    .Deserialize<ConfigurationData>(configObj.ToString());
                                
                                if (ValidateConfiguration(config))
                                {
                                    return config;
                                }
                                
                                _logger.LogWarning("Missing required keys in configuration data. Retrying...");
                            }
                        }
                        else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest ||
                                response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                        {
                            _logger.LogError("Not authorized. Check your config secret.");
                            return null;
                        }
                        else
                        {
                            _logger.LogWarning("Received status code {StatusCode}. Retrying...",
                                response.StatusCode);
                        }
                    }
                    catch (HttpRequestException ex)
                    {
                        _logger.LogWarning(ex, "Network error occurred. Retrying...");
                    }
                    catch (TaskCanceledException)
                    {
                        _logger.LogWarning("Request timed out. Retrying...");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Unexpected error occurred while fetching configuration");
                        return null;
                    }

                    _logger.LogInformation("Waiting {Interval}s before retrying...", interval);
                    await Task.Delay(TimeSpan.FromSeconds(interval));
                }
            }

            _logger.LogInformation("This process will end when the service is installed.");
            return null;
        }

        private bool ValidateConfiguration(ConfigurationData config)
        {
            if (config == null) return false;

            // Use reflection to check all required keys
            var type = typeof(ConfigurationData);
            foreach (var key in RequiredKeys)
            {
                var prop = type.GetProperty(key);
                if (prop == null || prop.GetValue(config) == null)
                {
                    return false;
                }
            }

            return true;
        }
    }
}