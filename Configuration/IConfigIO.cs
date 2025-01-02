using System.Threading.Tasks;

namespace RewstAgent.Configuration
{
    /// <summary>
    /// Handles configuration file I/O operations, including reading, writing, and validation
    /// </summary>
    public interface IConfigIO
    {
        /// <summary>
        /// Reads configuration from the specified file path
        /// </summary>
        /// <param name="filePath">Path to the configuration file</param>
        /// <returns>Configuration data object</returns>
        Task<ConfigurationData> ReadConfigurationAsync(string filePath);

        /// <summary>
        /// Writes configuration to the specified file path
        /// </summary>
        /// <param name="filePath">Path to write the configuration file</param>
        /// <param name="configuration">Configuration data to write</param>
        Task WriteConfigurationAsync(string filePath, ConfigurationData configuration);

        /// <summary>
        /// Validates the configuration file structure and required fields
        /// </summary>
        /// <param name="configuration">Configuration data to validate</param>
        /// <returns>True if configuration is valid, false otherwise</returns>
        Task<bool> ValidateConfigurationAsync(ConfigurationData configuration);
    }

    /// <summary>
    /// Handles remote configuration operations, including fetching and updating
    /// </summary>
    public interface IConfigFetcher
    {
        /// <summary>
        /// Fetches configuration from remote endpoint using provided credentials
        /// </summary>
        /// <param name="configUrl">URL to fetch configuration from</param>
        /// <param name="configSecret">Secret key for authentication</param>
        /// <param name="orgId">Organization identifier</param>
        /// <returns>Configuration data from remote source</returns>
        Task<ConfigurationData> FetchRemoteConfigurationAsync(string configUrl, string configSecret, string orgId);

        /// <summary>
        /// Updates local configuration with remote changes
        /// </summary>
        /// <param name="localConfig">Current local configuration</param>
        /// <returns>Updated configuration data</returns>
        Task<ConfigurationData> UpdateConfigurationAsync(ConfigurationData localConfig);
    }

    /// <summary>
    /// Collects and manages host system information
    /// </summary>
    public interface IHostInfoProvider
    {
        /// <summary>
        /// Retrieves current system information
        /// </summary>
        /// <returns>Host system information object</returns>
        Task<HostSystemInfo> GetHostInfoAsync();

        /// <summary>
        /// Validates system requirements for the agent
        /// </summary>
        /// <returns>True if system meets requirements, false otherwise</returns>
        Task<bool> ValidateSystemRequirementsAsync();

        /// <summary>
        /// Gets the agent's installation directory based on the operating system
        /// </summary>
        /// <returns>Full path to the installation directory</returns>
        string GetAgentInstallationDirectory();
    }

    /// <summary>
    /// Represents host system information
    /// </summary>
    public class HostSystemInfo
    {
        public string OsType { get; set; }
        public string OsVersion { get; set; }
        public string Architecture { get; set; }
        public string MachineName { get; set; }
        public string Username { get; set; }
        public long AvailableMemory { get; set; }
        public long TotalMemory { get; set; }
        public string AgentVersion { get; set; }
        public Dictionary<string, string> EnvironmentVariables { get; set; }
    }
}