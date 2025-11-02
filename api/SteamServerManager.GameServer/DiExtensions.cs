namespace SteamServerManager.GameServer;

using Verbs;

/// <summary>
/// Extensions for registering dependency injection services
/// </summary>
public static class DiExtensions
{
	/// <summary>
	/// Adds all of the game services to the given service collection
	/// </summary>
	/// <param name="services">The service collection to register to</param>
	/// <returns>The service collection for fluent method chaining</returns>
	public static IServiceCollection AddGame(this IServiceCollection services)
	{
		return services
			.AddTransient<ISteamService, SteamService>();
	}

	/// <summary>
	/// Adds all of the game-related command line verbs to the given builder
	/// </summary>
	/// <param name="builder">The command line builder to register to</param>
	/// <returns>The command line builder for fluent method chaining</returns>
	public static ICommandLineBuilder AddGame(this ICommandLineBuilder builder)
	{
		return builder
			.Add<InstallSteamVerb>()
			.Add<InstallGameVerb>();
	}
}
