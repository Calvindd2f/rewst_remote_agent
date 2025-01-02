public class IoTHubConnectionMaintainer
{
    private readonly ILogger<IoTHubConnectionMaintainer> _logger;
    private readonly IIoTHubConnectionManager _connectionManager;
    private readonly TimeSpan _reconnectInterval = TimeSpan.FromSeconds(30);
    private readonly CancellationTokenSource _shutdownTokenSource = new();
    private Task _maintenanceTask;

    public IoTHubConnectionMaintainer(
        ILogger<IoTHubConnectionMaintainer> logger,
        IIoTHubConnectionManager connectionManager)
    {
        _logger = logger;
        _connectionManager = connectionManager;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Link the shutdown token with the provided cancellation token
        using var linkedTokenSource = CancellationTokenSource
            .CreateLinkedTokenSource(_shutdownTokenSource.Token, cancellationToken);

        _maintenanceTask = Task.Run(async () =>
        {
            while (!linkedTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    await _connectionManager.ConnectAsync();
                    await _connectionManager.SetupMessageHandlerAsync();

                    // Keep running until cancellation is requested
                    try
                    {
                        await Task.Delay(Timeout.Infinite, linkedTokenSource.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        // Normal shutdown, exit gracefully
                        break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Connection lost. Attempting to reconnect...");
                    await Task.Delay(_reconnectInterval, linkedTokenSource.Token);
                }
            }
        }, linkedTokenSource.Token);

        await Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        _shutdownTokenSource.Cancel();
        if (_maintenanceTask != null)
        {
            await _maintenanceTask;
        }
    }
}