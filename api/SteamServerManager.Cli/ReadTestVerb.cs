namespace SteamServerManager.Cli;

[Verb("read-test", Hidden = true, HelpText = "A test verb for reading input")]
public class ReadTestOptions
{
}

internal class ReadTestVerb(
    ILogger<ReadTestVerb> logger) : BooleanVerb<ReadTestOptions>(logger)
{
    public override Task<bool> Execute(ReadTestOptions options, CancellationToken token)
    {
        Dictionary<string, Func<string, bool>> actions = new(StringComparer.InvariantCultureIgnoreCase)
        {
            ["exit"] = (_) => { _logger.LogInformation("Exiting..."); return false; },
            ["echo"] = (i) => { _logger.LogInformation("{Input}", i); return true; },
        };

        while(true)
        {
            _logger.LogInformation("Enter an action ({Actions}):", string.Join(", ", actions.Keys));
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input)) continue;

            var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0) continue;

            var key = parts.First();
            var value = string.Join(" ", parts.Skip(1));
            if (!actions.TryGetValue(key, out var action))
            {
                _logger.LogInformation("Could not determine action from: {Input}", input);
                continue;
            }

            var result = action(value);
            if (!result) break;
        }

        return Task.FromResult(true);
    }
}
