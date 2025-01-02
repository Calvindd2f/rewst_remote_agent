using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RewstAgent.Configuration;
using RewstAgent.IoTHub;

namespace RewstAgent.RemoteAgent
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            await CreateHostBuilder(args).Build().RunAsync();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseWindowsService() // Enables running as a Windows Service
                .UseSystemd()       // Enables running as a Linux systemd service
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHttpClient();
                    services.AddSingleton<IConfigurationManager, ConfigurationManager>();
                    services.AddSingleton<IConnectionManager, ConnectionManager>();
                    services.AddHostedService<RemoteAgentService>();
                    services.AddSingleton<IoTHubConnectionMaintainer>();
                    services.AddSingleton<CommandProcessor>();
                    services.AddHostedService<IoTHubBackgroundService>();
                    services.AddSingleton<IHostInfoProvider, HostInfoProvider>();
                })
                .ConfigureLogging((hostContext, logging) =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                    
                    // Add file logging with rotation
                    logging.AddFile("logs/rewst-agent-{Date}.log", fileSizeLimitBytes: 10 * 1024 * 1024);
                    
                    logging.SetMinimumLevel(LogLevel.Information);
                });
    }
}