using CardboardBox.Json;

namespace SteamServerManager.Core;

/// <summary>
/// Extensions for registering dependency injection services
/// </summary>
public static class DiExtensions
{
	/// <summary>
	/// Adds all of the core services to the given service collection
	/// </summary>
	/// <param name="services">The service collection to register to</param>
	/// <returns>The service collection for fluent method chaining</returns>
	public static IServiceCollection AddCore(this IServiceCollection services)
	{
		return services
			.AddCardboardHttp()
			.AddJson()
			
			.AddTransient<ITokenParserService, TokenParserService>()
			.AddTransient<IFormatService, FormatService>();
	}
}
