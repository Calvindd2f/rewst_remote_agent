public class CommandProcessor
{
    private readonly ILogger<CommandProcessor> _logger;
    private readonly ITempFileManager _tempFileManager;
    private readonly IHttpClientService _httpClient;
    private readonly string _osType;

    public CommandProcessor(
        ILogger<CommandProcessor> logger,
        ITempFileManager tempFileManager,
        IHttpClientService httpClient)
    {
        _logger = logger;
        _tempFileManager = tempFileManager;
        _httpClient = httpClient;
        _osType = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows" : 
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux" : "darwin";
    }

    public async Task<List<CommandResult>> ProcessCommandsAsync(
        List<string> commands,
        string interpreter,
        CancellationToken cancellationToken)
    {
        var results = new List<CommandResult>();
        var environment = new Dictionary<string, string>();

        foreach (var command in commands)
        {
            try
            {
                var result = await ExecuteCommandInEnvironment(command, interpreter, environment, cancellationToken);
                results.Add(result);

                // Update environment with any exported variables
                if (result.ExportedVariables != null)
                {
                    foreach (var (key, value) in result.ExportedVariables)
                    {
                        environment[key] = value;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing command: {Command}", command);
                results.Add(new CommandResult 
                { 
                    Command = command,
                    Error = ex.Message,
                    ExitCode = -1
                });
                
                // Depending on requirements, we might want to break here
                break;
            }
        }

        return results;
    }

    private async Task<CommandResult> ExecuteCommandInEnvironment(
        string command,
        string interpreter,
        Dictionary<string, string> environment,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = interpreter,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Apply environment variables
        foreach (var (key, value) in environment)
        {
            startInfo.EnvironmentVariables[key] = value;
        }

        using var process = new Process { StartInfo = startInfo };
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (sender, e) => 
        {
            if (e.Data != null)
            {
                outputBuilder.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (sender, e) => 
        {
            if (e.Data != null)
            {
                errorBuilder.AppendLine(e.Data);
            }
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.StandardInput.WriteLineAsync(command);
            await process.StandardInput.FlushAsync();
            process.StandardInput.Close();

            await process.WaitForExitAsync(cancellationToken);

            return new CommandResult
            {
                Command = command,
                Output = outputBuilder.ToString(),
                Error = errorBuilder.ToString(),
                ExitCode = process.ExitCode,
                ExportedVariables = ExtractExportedVariables(outputBuilder.ToString())
            };
        }
        catch (Exception ex)
        {
            throw new CommandExecutionException($"Failed to execute command: {command}", ex);
        }
    }

    private Dictionary<string, string> ExtractExportedVariables(string output)
    {
        var exports = new Dictionary<string, string>();
        // Implement logic to extract exported variables from command output
        // This would be shell-specific (bash vs powershell)
        return exports;
    }
}

public class CommandResult
{
    public string Command { get; set; }
    public string Output { get; set; }
    public string Error { get; set; }
    public int ExitCode { get; set; }
    public Dictionary<string, string> ExportedVariables { get; set; }
}

public class CommandExecutionException : Exception
{
    public CommandExecutionException(string message, Exception inner) 
        : base(message, inner) { }
}