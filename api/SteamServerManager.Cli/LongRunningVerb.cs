namespace SteamServerManager.Cli;

[Verb("long-running", Hidden = true, HelpText = "A task that keeps running until it gets a sigterm")]
public class LongRunningOptions
{
}

internal class LongRunningVerb(
    ILogger<LongRunningVerb> logger) : BooleanVerb<LongRunningOptions>(logger)
{
    public override async Task<bool> Execute(LongRunningOptions options, CancellationToken token)
    {
        _logger.LogInformation("Starting long-running task. Press Ctrl+C to cancel.");
        try
        {
            await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
		_logger.LogInformation("Long-running task has been cancelled.");
        return true;
	}
}
