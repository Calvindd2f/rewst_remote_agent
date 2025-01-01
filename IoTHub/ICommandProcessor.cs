namespace RewstAgent.IoTHub
{
    /// <summary>
    /// Defines the contract for processing system commands in a sequential and managed way.
    /// This interface handles the execution of commands received from IoT Hub, maintaining
    /// environment state and collecting results.
    /// </summary>
    public interface ICommandProcessor
    {
        /// <summary>
        /// Processes a list of commands sequentially, maintaining environment state between executions.
        /// Each command is executed in the context of previous commands, allowing for variable
        /// persistence and environment sharing.
        /// </summary>
        /// <param name="commands">The list of commands to execute in sequence</param>
        /// <param name="interpreter">The shell interpreter to use (e.g., powershell, bash)</param>
        /// <param name="cancellationToken">Token to cancel the operation if needed</param>
        /// <returns>A list of command results in the same order as the input commands</returns>
        Task<List<CommandResult>> ProcessCommandsAsync(
            List<string> commands,
            string interpreter,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes a single command and returns its result. This method maintains the same
        /// environment context as ProcessCommandsAsync but handles just one command at a time.
        /// </summary>
        /// <param name="command">The command to execute</param>
        /// <param name="interpreter">The shell interpreter to use</param>
        /// <param name="inheritedEnvironment">Optional environment variables from previous executions</param>
        /// <param name="cancellationToken">Token to cancel the operation if needed</param>
        /// <returns>The result of the command execution</returns>
        Task<CommandResult> ExecuteCommandAsync(
            string command,
            string interpreter,
            IDictionary<string, string> inheritedEnvironment = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the default interpreter for the current operating system.
        /// Windows will return PowerShell, Linux returns bash, and macOS returns zsh.
        /// </summary>
        /// <returns>The path or command to invoke the default shell interpreter</returns>
        string GetDefaultInterpreter();

        /// <summary>
        /// Validates if a given interpreter is available and executable on the current system.
        /// This helps prevent command execution failures due to missing interpreters.
        /// </summary>
        /// <param name="interpreter">The interpreter to validate</param>
        /// <returns>True if the interpreter is available, false otherwise</returns>
        Task<bool> ValidateInterpreterAsync(string interpreter);

        /// <summary>
        /// Cleans up any temporary resources or files created during command execution.
        /// This should be called when finished with a batch of commands to ensure
        /// proper resource management.
        /// </summary>
        Task CleanupAsync();
    }

    /// <summary>
    /// Represents the result of a command execution, including output, error information,
    /// and any environment variables that were exported during execution.
    /// </summary>
    public class CommandResult
    {
        /// <summary>
        /// The original command that was executed
        /// </summary>
        public string Command { get; set; }

        /// <summary>
        /// The standard output from the command execution
        /// </summary>
        public string Output { get; set; }

        /// <summary>
        /// Any error output from the command execution
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        /// The exit code from the command execution. 0 typically indicates success
        /// </summary>
        public int ExitCode { get; set; }

        /// <summary>
        /// Environment variables that were exported or modified during command execution
        /// These can be passed to subsequent commands to maintain state
        /// </summary>
        public IDictionary<string, string> ExportedVariables { get; set; }

        /// <summary>
        /// The timestamp when the command execution started
        /// </summary>
        public DateTimeOffset StartTime { get; set; }

        /// <summary>
        /// The timestamp when the command execution completed
        /// </summary>
        public DateTimeOffset EndTime { get; set; }

        /// <summary>
        /// Indicates whether the command executed successfully (ExitCode == 0)
        /// </summary>
        public bool IsSuccess => ExitCode == 0;

        /// <summary>
        /// The duration of the command execution
        /// </summary>
        public TimeSpan Duration => EndTime - StartTime;
    }
}