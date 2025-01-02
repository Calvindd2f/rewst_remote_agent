using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RewstAgent.Configuration;

namespace RewstAgent.WindowsService
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();
            
            if (args.Length > 0)
            {
                // Handle service installation/uninstallation commands
                await HandleServiceCommands(args);
                return;
            }

            await host.RunAsync();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseWindowsService(options =>
                {
                    // Service name and display name will be set dynamically
                    // based on the organization ID in the RewstWindowsService constructor
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddSingleton<IHostInfoProvider, HostInfoProvider>();
                    services.AddSingleton<ConfigurationPaths>();
                    services.AddSingleton<IConfigurationManager, ConfigurationManager, ChecksumValidator>();
                    services.AddHostedService<RewstWindowsService>();
                })
                .ConfigureLogging((hostContext, logging) =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                    
                    // Add file logging
                    var logPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                        "Rewst", "Agent", "logs");
                    
                    logging.AddFile(Path.Combine(logPath, "rewst-service-{Date}.log"));
                });

        private static async Task HandleServiceCommands(string[] args)
        {
            // Implement service installation/uninstallation logic here
            // This would use the Windows Service APIs or sc.exe commands
            throw new NotImplementedException("Service command handling to be implemented");
        }
    }
}