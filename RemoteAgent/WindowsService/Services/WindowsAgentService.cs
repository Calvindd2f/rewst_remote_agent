// WindowsAgentService.cs
public class WindowsAgentService : BackgroundService
{
    private readonly string _orgId;
    private readonly ILogger<WindowsAgentService> _logger;
    private readonly List<Process> _managedProcesses = new();
    private readonly CancellationTokenSource _shutdownTokenSource = new();

    public WindowsAgentService(ILogger<WindowsAgentService> logger)
    {
        _logger = logger;
        _orgId = GetOrgIdFromExecutableName();
        ServiceName = $"RewstRemoteAgent_{_orgId}";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var linkedTokenSource = 
                CancellationTokenSource.CreateLinkedTokenSource(
                    stoppingToken, 
                    _shutdownTokenSource.Token);

            while (!linkedTokenSource.Token.IsCancellationRequested)
            {
                await ManageAgentProcess(linkedTokenSource.Token);
                await Task.Delay(5000, linkedTokenSource.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Service encountered an error");
            throw;
        }
    }

    // Process management methods...
}