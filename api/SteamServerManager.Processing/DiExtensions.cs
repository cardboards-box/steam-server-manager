namespace SteamServerManager.Processing;

/// <summary>
/// Extensions for registering dependency injection services
/// </summary>
public static class DiExtensions
{
	/// <summary>
	/// Adds all of the processing services to the given service collection
	/// </summary>
	/// <param name="services">The service collection to register to</param>
	/// <returns>The service collection for fluent method chaining</returns>
	public static IServiceCollection AddProcessing(this IServiceCollection services)
	{
		return services
			.AddTransient<IProcessService, ProcessService>();
	}
}
