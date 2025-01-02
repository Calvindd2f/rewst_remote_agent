// ServiceManagerProgram.cs
public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Rewst Service Manager")
        {
            new Option<string>(
                "--org-id",
                "Organization ID") { IsRequired = true },
            new Option<string>(
                "--config-file",
                "Path to configuration file"),
            new Option<bool>(
                "--install",
                "Install the service"),
            // Add other options...
        };

        rootCommand.SetHandler(async (context) =>
        {
            var orgId = context.ParseResult.GetValueForOption<string>("--org-id");
            // Handle commands...
        });

        return await rootCommand.InvokeAsync(args);
    }
}