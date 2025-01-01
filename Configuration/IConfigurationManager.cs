namespace RewstAgent.Configuration
{
    /// <summary>
    /// Manages the configuration aspects of the Rewst Agent, including fetching and storing configuration data
    /// and managing configuration files on the system.
    /// </summary>
    public interface IConfigurationManager
    {
        /// <summary>
        /// Fetches the configuration from the remote endpoint using the provided credentials.
        /// </summary>
        /// <param name="configUrl">The URL to fetch configuration from</param>
        /// <param name="configSecret">The secret key for authentication</param>
        /// <param name="orgId">The organization identifier</param>
        /// <returns>The configuration data object or null if fetching fails</returns>
        Task<ConfigurationData> FetchConfiguration(string configUrl, string configSecret, string orgId);

        /// <summary>
        /// Archives existing configuration files by renaming them with _oldver suffix.
        /// </summary>
        /// <param name="orgId">The organization identifier</param>
        Task ArchiveOldFiles(string orgId);

        /// <summary>
        /// Checks if all required agent files are present on the system.
        /// </summary>
        /// <param name="orgId">The organization identifier</param>
        /// <returns>True if all required files are present, false otherwise</returns>
        Task<bool> AreAllFilesPresent(string orgId);
    }
}