using System.Collections.Generic;
using System.Threading.Tasks;

namespace RewstAgent.Configuration
{
    /// <summary>
    /// Handles fetching configuration from remote endpoints with retry logic
    /// </summary>
    public interface IConfigurationFetcher
    {
        /// <summary>
        /// Fetches configuration from the specified endpoint with retry capabilities
        /// </summary>
        Task<ConfigurationData> FetchConfigurationAsync(
            string configUrl,
            string secret,
            string orgId,
            IEnumerable<(int interval, int maxRetries)> retryIntervals = null);
    }

    /// <summary>
    /// Provides system information and host details
    /// </summary>
    public interface IHostInfoProvider
    {
        /// <summary>
        /// Gets the host's MAC address
        /// </summary>
        string GetMacAddress();

        /// <summary>
        /// Checks if the current machine is a domain controller
        /// </summary>
        Task<bool> IsDomainController();

        /// <summary>
        /// Gets the Active Directory domain name
        /// </summary>
        Task<string> GetADDomainName();

        /// <summary>
        /// Gets the Entra (Azure AD) domain name
        /// </summary>
        Task<string> GetEntraDomain();

        /// <summary>
        /// Checks if the current machine is an Entra Connect server
        /// </summary>
        Task<bool> IsEntraConnectServer();

        /// <summary>
        /// Builds a complete set of host information tags
        /// </summary>
        Task<HostInformation> BuildHostTags(string orgId);
    }

    /// <summary>
    /// Represents detailed host system information
    /// </summary>
    public class HostInformation
    {
        public string AgentVersion { get; set; }
        public string AgentExecutablePath { get; set; }
        public string ServiceExecutablePath { get; set; }
        public string Hostname { get; set; }
        public string MacAddress { get; set; }
        public string OperatingSystem { get; set; }
        public string CpuModel { get; set; }
        public double RamGb { get; set; }
        public string AdDomain { get; set; }
        public bool IsAdDomainController { get; set; }
        public bool IsEntraConnectServer { get; set; }
        public string EntraDomain { get; set; }
        public string OrgId { get; set; }
    }
}