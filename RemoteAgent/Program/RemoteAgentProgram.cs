// RemoteAgentProgram.cs
public class Program
{
    public static async Task Main(string[] args)
    {
        try
        {
            // Set up cancellation support
            using var cancellationTokenSource = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cancellationTokenSource.Cancel();
            };

            await CreateHostBuilder(args).Build().RunAsync(cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Application terminated unexpectedly");
            throw;
        }
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseWindowsService()
            .UseSystemd()
            .ConfigureServices((context, services) =>
            {
                services.AddHostedService<RemoteAgentService>();
                // Add other services...
            });
}