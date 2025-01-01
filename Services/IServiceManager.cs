namespace RewstAgent.Services
{
    /// <summary>
    /// Manages the Windows/Linux service aspects of the Rewst Agent, including
    /// installation, starting, stopping, and status checking.
    /// </summary>
    public interface IServiceManager
    {
        /// <summary>
        /// Checks if the agent service is currently running.
        /// </summary>
        /// <param name="orgId">The organization identifier</param>
        /// <returns>True if the service is running, false otherwise</returns>
        Task<bool> IsServiceRunning(string orgId);

        /// <summary>
        /// Installs the agent service on the system.
        /// </summary>
        /// <param name="orgId">The organization identifier</param>
        /// <returns>True if installation was successful, false otherwise</returns>
        Task<bool> InstallService(string orgId);

        /// <summary>
        /// Starts the agent service.
        /// </summary>
        /// <param name="orgId">The organization identifier</param>
        /// <returns>True if the service was started successfully, false otherwise</returns>
        Task<bool> StartService(string orgId);
    }
}