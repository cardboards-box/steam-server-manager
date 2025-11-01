namespace SteamServerManager.Cli;

using Processing;

[Verb("process-test", isDefault: true, Hidden = true, HelpText = "Tests the process proxy")]
public class ProcessTestOptions
{
	[Value(0, Required = false, HelpText = "The method to execute")]
	public string? Method { get; set; }
}

internal class ProcessTestVerb(
    ILogger<ProcessTestVerb> logger) : BooleanVerb<ProcessTestOptions>(logger)
{
	public ProcessProxy? GenProxy(string verb)
	{
		var fileName = Assembly.GetEntryAssembly()?.Location;
		_logger.LogInformation("Entry assembly location: {FileName}", fileName);
		if (string.IsNullOrEmpty(fileName))
		{
			_logger.LogError("Could not determine entry assembly location.");
			return null;
		}

		return new ProcessProxy("dotnet")
			.WithArgs($"{fileName} {verb}")
			.WithLogger(_logger);
	}

	public async Task<bool> NestedTest(CancellationToken token)
	{
		var process = GenProxy($"process-test {nameof(WriteTest)}");
		if (process is null) return false;

		await process.Start(token);
		_logger.LogInformation("Nested test is running. Waiting for exit...");
		await process.WaitForExit(token);
		_logger.LogInformation("Nested test completed.");
		return true;
	}

	public async Task<bool> KillTest(CancellationToken token)
	{
		var process = GenProxy("long-running");
		if (process is null) return false;

		await process.Start(token);

		_logger.LogInformation("Process is running. Waiting for 2 seconds before stopping...");
		await Task.Delay(TimeSpan.FromSeconds(2), token);

		_logger.LogInformation("Process stop requesting...");
		await process.Kill(token);
		_logger.LogInformation("Process stop requested. Waiting for exit...");

		await process.WaitForExit(token);

		_logger.LogInformation("Process test completed.");
		return true;
	}

	public async Task<bool> StopTest(CancellationToken token)
    {
		var process = GenProxy("long-running");
		if (process is null) return false;

		await process.Start(token);

		_logger.LogInformation("Process is running. Waiting for 2 seconds before stopping...");
		await Task.Delay(TimeSpan.FromSeconds(2), token);

		_logger.LogInformation("Process stop requesting...");
		await process.Stop(token);
		_logger.LogInformation("Process stop requested. Waiting for exit...");

		await process.WaitForExit(token);

		_logger.LogInformation("Process test completed.");
		return true;
	}

	public async Task<bool> WriteTest(CancellationToken token)
	{
		var process = GenProxy("read-test");
		if (process is null) return false;

		await process.Start(token);

		_logger.LogInformation("Process is running. Writing input...");

		for (int i = 0; i < 5; i++)
		{
			var input = $"echo Hello World #{i}! I hope everything is fine?";
			_logger.LogInformation("Writing to process: {Input}", input);
			await process.WriteLineAsync(input);
			await Task.Delay(500, token);
		}

		await process.WriteLineAsync("exit");
		await process.WaitForExit(token);
		_logger.LogInformation("Process write test completed.");
		return true;
	}

    public override async Task<bool> Execute(ProcessTestOptions options, CancellationToken token)
    {
		const string defaultMethod = nameof(NestedTest);

		var name = options.Method?.ForceNull() ?? defaultMethod;
		_logger.LogInformation("Executing process test method: {Method}", name);

		var method = GetType().GetMethod(name);
		if (method is null)
		{
			_logger.LogError("Method '{Method}' not found.", name);
			return false;
		}

		if (method.ReturnType != typeof(Task<bool>) ||
			method.GetParameters().Length != 1 ||
			method.GetParameters()[0].ParameterType != typeof(CancellationToken))
		{
			_logger.LogError("Method '{Method}' has an invalid signature. Must be Task<bool> Name(CancellationToken)", name);
			return false;
		}

		var result = (Task<bool>)method.Invoke(this, [token ])!;
		return await result;
	}
}
