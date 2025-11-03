namespace SteamServerManager.Processing;

/// <summary>
/// Methods related to configuring the process proxy
/// </summary>
public partial class ProcessProxy
{
	/// <summary>
	/// Sets the working directory of the process
	/// </summary>
	/// <param name="directory">The directory to use</param>
	/// <returns>The current proxy for method chaining</returns>
	public ProcessProxy WithWorkingDirectory(string? directory)
	{
		startInfo.WorkingDirectory = directory ?? string.Empty;
		return this;
	}

	/// <summary>
	/// Sets the command line arguments for the proxy
	/// </summary>
	/// <param name="arguments">The arguments to use</param>
	/// <returns>The current proxy for method chaining</returns>
	public ProcessProxy WithArgs(string? arguments)
	{
		startInfo.Arguments = arguments ?? string.Empty;
		return this;
	}

	/// <inheritdoc cref="WithArgs(string?)"/>
	public ProcessProxy WithArgs(params string?[] arguments)
	{
		foreach (var arg in arguments)
			if (!string.IsNullOrWhiteSpace(arg))
				startInfo.ArgumentList.Add(arg);
		return this;
	}

	/// <summary>
	/// Sets the username for the process
	/// </summary>
	/// <param name="username">The username to use for the process</param>
	/// <returns>The current proxy for method chaining</returns>
	public ProcessProxy WithUser(string? username)
	{
		startInfo.UserName = username ?? string.Empty;
		return this;
	}

	/// <summary>
	/// Sets the given environment variable for the process
	/// </summary>
	/// <param name="key">The key of the variable</param>
	/// <param name="value">The value of the variable</param>
	/// <returns>The current proxy for method chaining</returns>
	public ProcessProxy WithEnvArg(string key, string? value)
	{
		startInfo.Environment[key] = value;
		return this;
	}

	/// <summary>
	/// Sets the given environment variables for the process
	/// </summary>
	/// <param name="args">The environment variables to set</param>
	/// <returns>The current proxy for method chaining</returns>
	public ProcessProxy WithEnvArgs(params (string key, string? value)[] args)
	{
		foreach (var (key, value) in args)
			WithEnvArg(key, value);
		return this;
	}

	/// <summary>
	/// Sets the given environment variables for the process
	/// </summary>
	/// <param name="args">The environment variables to set</param>
	/// <returns>The current proxy for method chaining</returns>
	public ProcessProxy WithEnvArgs(IEnumerable<KeyValuePair<string, string?>> args)
	{
		foreach (var (key, value) in args)
			WithEnvArg(key, value);
		return this;
	}

	/// <summary>
	/// Registers a logger for the process events
	/// </summary>
	/// <param name="logger">The logger to log the events to</param>
	/// <param name="name">The name of the logger to use</param>
	/// <returns>The current proxy for method chaining</returns>
	[SuppressMessage("Performance", "CA1873:Avoid potentially expensive logging", Justification = "Stupid warning")]
	public ProcessProxy WithLogger(ILogger logger, string? name = null)
	{
		name ??= Path.GetFileNameWithoutExtension(startInfo.FileName);
		OnStandardOutput.Subscribe((line) => logger.LogInformation("[{ProcessName}::STDOUT] {Line}", name, line));
		OnStandardError.Subscribe((line) => logger.LogWarning("[{ProcessName}::STDERR] {Line}", name, line));
		OnException.Subscribe((error) => logger.LogError(error.Exception, "[{ProcessName}::EXCEPTION] An exception occurred: {ErrorCode}", name, error.Code));
		OnExited.Subscribe((res) => logger.LogInformation("[{ProcessName}::EXITED] Process has exited >> {Code} ({Success})", name, res.ExitCode, res.Success ? "Success" : "Failed"));
		OnStarted.Subscribe((id) => logger.LogInformation("[{ProcessName}::STARTED] Process has started.", name));
		return this;
	}

	/// <summary>
	/// Writes everything from <see cref="OnStandardOutput"/> to the given <see cref="StringBuilder"/>
	/// </summary>
	/// <param name="builder">The string builder to write to</param>
	/// <param name="disposer">The subscription's disposable instance</param>
	/// <returns>The current proxy for method chaining</returns>
	public ProcessProxy WithStandardOutput(StringBuilder builder, out IDisposable disposer)
	{
		disposer = OnStandardOutput.Subscribe((line) => builder.AppendLine(line));
		return this;
	}

	/// <summary>
	/// Writes everything from <see cref="OnStandardOutput"/> to the given <see cref="TextWriter"/>
	/// </summary>
	/// <param name="writer">The <see cref="TextWriter"/> to write to</param>
	/// <param name="disposer">The subscription's disposable instance</param>
	/// <returns>The current proxy for method chaining</returns>
	public ProcessProxy WithStandardOutput(TextWriter writer, out IDisposable disposer)
	{
		disposer = OnStandardOutput.Subscribe(writer.WriteLine);
		return this;
	}

	/// <summary>
	/// Writes everything from <see cref="OnStandardError"/> to the given <see cref="StringBuilder"/>
	/// </summary>
	/// <param name="builder">The string builder to write to</param>
	/// <param name="disposer">The subscription's disposable instance</param>
	/// <returns>The current proxy for method chaining</returns>
	public ProcessProxy WithStandardError(StringBuilder builder, out IDisposable disposer)
	{
		disposer = OnStandardError.Subscribe((line) => builder.AppendLine(line));
		return this;
	}

	/// <summary>
	/// Writes everything from <see cref="OnStandardError"/> to the given <see cref="TextWriter"/>
	/// </summary>
	/// <param name="writer">The <see cref="TextWriter"/> to write to</param>
	/// <param name="disposer">The subscription's disposable instance</param>
	/// <returns>The current proxy for method chaining</returns>
	public ProcessProxy WithStandardError(TextWriter writer, out IDisposable disposer)
	{
		disposer = OnStandardError.Subscribe(writer.WriteLine);
		return this;
	}
}
