using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RewstAgent.Configuration;

namespace RewstAgent.Services
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var rootCommand = new RootCommand("Rewst Agent Service Manager");

            // Define options
            var orgIdOption = new Option<string>(
                "--org-id",
                "Organization ID for the service");

            var commandOption = new Option<string>(
                "--command",
                "Service command (install, uninstall, start, stop, restart, status)");

            rootCommand.AddOption(orgIdOption);
            rootCommand.AddOption(commandOption);

            rootCommand.SetHandler(async (orgId, command) =>
            {
                try
                {
                    var host = CreateHostBuilder(args).Build();
                    var serviceManager = host.Services.GetRequiredService<ServiceManager>();

                    if (string.IsNullOrEmpty(orgId))
                    {
                        Console.WriteLine("Error: Organization ID is required");
                        Environment.Exit(1);
                    }

                    switch (command?.ToLower())
                    {
                        case "install":
                            await serviceManager.InstallService(orgId);
                            break;

                        case "uninstall":
                            await serviceManager.UninstallService(orgId);
                            break;

                        case "start":
                            await serviceManager.StartService(orgId);
                            break;

                        case "stop":
                            await serviceManager.StopService(orgId);
                            break;

                        case "restart":
                            await serviceManager.RestartService(orgId);
                            break;

                        case "status":
                            var status = await serviceManager.CheckServiceStatus(orgId);
                            Console.WriteLine(status);
                            break;

                        default:
                            if (!string.IsNullOrEmpty(command))
                            {
                                Console.WriteLine($"Unknown command: {command}");
                                Environment.Exit(1);
                            }
                            // If no command specified, run as a service
                            await host.RunAsync();
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    Environment.Exit(1);
                }
            }, orgIdOption, commandOption);

            return await rootCommand.InvokeAsync(args);
        }

        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseWindowsService()
                .UseSystemd()
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddLogging(builder =>
                    {
                        builder.AddConsole();
                        builder.AddEventLog();
                    });
                    services.AddSingleton<ConfigurationPaths>();
                    services.AddSingleton<IConfigurationManager, ConfigurationManager>();
                    services.AddSingleton<ServiceManager>();
                });
    }
}