namespace RewstAgent.IoTHub
{
    /// <summary>
    /// Manages connections to the IoT hub, including establishing connections, 
    /// handling messages, and managing the connection lifecycle.
    /// </summary>
    public interface IConnectionManager
    {
        /// <summary>
        /// Establishes a connection to the IoT hub.
        /// </summary>
        Task Connect();

        /// <summary>
        /// Disconnects from the IoT hub.
        /// </summary>
        Task Disconnect();

        /// <summary>
        /// Sets up the message handler for processing incoming IoT hub messages.
        /// </summary>
        Task SetMessageHandler();
    }
}