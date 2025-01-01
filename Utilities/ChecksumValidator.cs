using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace RewstAgent.Utilities
{
    public class ChecksumValidator
    {
        private readonly ILogger<ChecksumValidator> _logger;
        private readonly HttpClient _httpClient;
        private const string GithubApiBaseUrl = "https://api.github.com";
        private const string Repository = "rewstapp/rewst_remote_agent";

        public ChecksumValidator(ILogger<ChecksumValidator> logger, HttpClient httpClient)
        {
            _logger = logger;
            _httpClient = httpClient;
            // Set GitHub API headers
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Rewst-Agent");
            _httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        }

        /// <summary>
        /// Validates the checksum of an executable by comparing it with the checksum from GitHub.
        /// </summary>
        public async Task<bool> IsChecksumValid(string executablePath)
        {
            try
            {
                var executableName = Path.GetFileName(executablePath);
                var checksumFileName = GetChecksumFileName(executableName);

                var githubChecksum = (await FetchChecksumFromGithub(checksumFileName))?.ToLowerInvariant();
                _logger.LogInformation($"GitHub Checksum: {githubChecksum}");

                var localChecksum = await CalculateLocalFileChecksum(executablePath);
                _logger.LogInformation($"Local Checksum: {localChecksum}");

                if (string.IsNullOrEmpty(githubChecksum) || string.IsNullOrEmpty(localChecksum))
                {
                    _logger.LogError("Failed to get one or both of the checksums.");
                    return false;
                }

                return githubChecksum == localChecksum;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating checksum");
                return false;
            }
        }

        private string GetChecksumFileName(string executableName)
        {
            // Remove GUID from filename if present
            var pattern = @"_[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}";
            var checksumFileName = Regex.Replace(executableName, pattern, "", RegexOptions.IgnoreCase);
            return $"{checksumFileName}.sha256";
        }

        private async Task<ReleaseInfo> GetReleaseInfoByTag(string tag)
        {
            var url = $"{GithubApiBaseUrl}/repos/{Repository}/releases/tags/{tag}";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<ReleaseInfo>(content);
        }

        private async Task<string> GetChecksumFileUrl(string tag, string fileName)
        {
            var releaseInfo = await GetReleaseInfoByTag(tag);
            return releaseInfo?.Assets
                ?.FirstOrDefault(a => a.Name == fileName)
                ?.BrowserDownloadUrl;
        }

        private async Task<string> FetchChecksumFromGithub(string checksumFileName)
        {
            try
            {
                var versionTag = $"v{GetType().Assembly.GetName().Version}";
                var checksumFileUrl = await GetChecksumFileUrl(versionTag, checksumFileName);

                if (string.IsNullOrEmpty(checksumFileUrl))
                {
                    _logger.LogError($"Checksum file URL not found for {checksumFileName}");
                    return null;
                }

                var response = await _httpClient.GetAsync(checksumFileUrl);
                response.EnsureSuccessStatusCode();
                
                var checksumData = await response.Content.ReadAsStringAsync();
                
                // Parse the checksum data
                foreach (var line in checksumData.Split('\n'))
                {
                    if (line.StartsWith("Hash"))
                    {
                        return line.Split(':')[1].Trim();
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch checksum from GitHub");
                return null;
            }
        }

        private async Task<string> CalculateLocalFileChecksum(string filePath)
        {
            try
            {
                using var sha256 = SHA256.Create();
                using var stream = File.OpenRead(filePath);
                var hash = await sha256.ComputeHashAsync(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to calculate local file checksum");
                return null;
            }
        }

        private class ReleaseInfo
        {
            public List<Asset> Assets { get; set; }
        }

        private class Asset
        {
            public string Name { get; set; }
            public string BrowserDownloadUrl { get; set; }
        }
    }
}