namespace SteamServerManager.Core.Tokening;

using Resolvers = Dictionary<string, Func<object?>>;

/// <summary>
/// The settings for the <see cref="IFormatService"/>
/// </summary>
/// <param name="_comparer">The comparer to use for the argument key</param>
public class FormatSettings(
	StringComparer? _comparer = null)
{
	/// <summary>
	/// The default arguments to use for transfers
	/// </summary>
	internal static Resolvers? _defaults;

	private readonly Resolvers _arguments = new(_comparer ?? StringComparer.InvariantCultureIgnoreCase);
	private TokenParserConfig _config = new("{", "}", "\\");
	private bool _hasDefaults = false;

	internal string FormatToken = ":";
	internal int MaxPasses = 99;
	internal bool ThrowOnMaxPasses = false;

	/// <summary>
	/// Sets the max number of times the formatter should run on a single string
	/// </summary>
	/// <param name="count">The number of iterations</param>
	/// <returns>The current settings for fluent method chaining</returns>
	public FormatSettings WithMaxPasses(int count)
	{
		MaxPasses = count;
		return this;
	}

	/// <summary>
	/// Indicates whether or not to throw an error if the max passes are reached
	/// </summary>
	/// <param name="throw">Whether or not to throw an error if the max passes are reached</param>
	/// <returns>The current settings for fluent method chaining</returns>
	public FormatSettings WithThrowOnMaxPasses(bool @throw = true)
	{
		ThrowOnMaxPasses = @throw;
		return this;
	}

	/// <summary>
	/// Sets the parser configuration
	/// </summary>
	/// <param name="config">The configuration to use</param>
	/// <returns>The current settings for fluent method chaining</returns>
	public FormatSettings WithConfig(TokenParserConfig config)
	{
		_config = config;
		return this;
	}

	/// <summary>
	/// Sets the parser configuration
	/// </summary>
	/// <param name="config">The configuration action</param>
	/// <returns>The current settings for fluent method chaining</returns>
	public FormatSettings WithConfig(Action<TokenParserConfig> config)
	{
		config(_config);
		return this;
	}

	/// <summary>
	/// Sets the token to use for fetching the formatter for the token
	/// </summary>
	/// <param name="token">The token character to look for</param>
	/// <returns>The current settings for fluent method chaining</returns>
	public FormatSettings WithFormatToken(string token)
	{
		FormatToken = token;
		return this;
	}

	/// <summary>
	/// Includes the given arguments in the settings
	/// </summary>
	/// <param name="args">The arguments to use</param>
	/// <returns>The current settings for fluent method chaining</returns>
	public FormatSettings With(Resolvers args)
	{
		foreach (var (key, val) in args)
			_arguments[key] = val;
		return this;
	}

	/// <summary>
	/// Includes the given arguments in the settings
	/// </summary>
	/// <param name="args">The arguments to use</param>
	/// <returns>The current settings for fluent method chaining</returns>
	public FormatSettings With(Dictionary<string, object?> args)
	{
		return With(ToArgFormat(args));
	}

	/// <summary>
	/// Includes the given arguments in the settings
	/// </summary>
	/// <param name="args">The arguments to use</param>
	/// <returns>The current settings for fluent method chaining</returns>
	public FormatSettings With(Dictionary<string, string?> args)
	{
		return With(ToArgFormat(args));
	}

	/// <summary>
	/// Adds the given variable to the settings
	/// </summary>
	/// <param name="key">The name of the variable</param>
	/// <param name="arg">The argument resolver</param>
	/// <returns>The current settings for fluent method chaining</returns>
	public FormatSettings Add(string key, Func<object?> arg)
	{
		_arguments.Add(key, arg);
		return this;
	}

	/// <summary>
	/// Adds the given variable to the settings
	/// </summary>
	/// <param name="key">The name of the variable</param>
	/// <param name="arg">The argument</param>
	/// <returns>The current settings for fluent method chaining</returns>
	public FormatSettings Add(string key, object? arg)
	{
		return Add(key, () => arg);
	}

	/// <summary>
	/// Fetches the settings
	/// </summary>
	/// <returns>The resolver settings and the parser configuration</returns>
	internal (Resolvers args, TokenParserConfig config, string format) Resolve()
	{
		if (!_hasDefaults)
		{
			With(GetDefaults());
			_hasDefaults = true;
		}

		return (_arguments, _config, FormatToken);
	}

	/// <summary>
	/// Lazily load the default resolvers for settings
	/// </summary>
	/// <returns>The default resolvers</returns>
	internal static Resolvers GetDefaults()
	{
		if (_defaults != null) return _defaults;

		var dic = new Resolvers()
		{
			["now"] = () => DateTime.Now,
			["now.utc"] = () => DateTime.UtcNow,

			//Environment.*
			["env.Is64BitOperatingSystem"] = () => Environment.Is64BitOperatingSystem,
			["env.Is64BitProcess"] = () => Environment.Is64BitProcess,
			["env.OSVersion"] = () => Environment.OSVersion,
			["env.HasShutdownStarted"] = () => Environment.HasShutdownStarted,
			["env.ExitCode"] = () => Environment.ExitCode,
			["env.CurrentManagedThreadId"] = () => Environment.CurrentManagedThreadId,
			["env.CurrentDirectory"] = () => Environment.CurrentDirectory,
			["env.CommandLine"] = () => Environment.CommandLine,
			["env.MachineName"] = () => Environment.MachineName,
			["env.ProcessorCount"] = () => Environment.ProcessorCount,
			["env.SystemDirectory"] = () => Environment.SystemDirectory,
			["env.SystemPageSize"] = () => Environment.SystemPageSize,
			["env.TickCount"] = () => Environment.TickCount,
			["env.TickCount64"] = () => Environment.TickCount64,
			["env.UserDomainName"] = () => Environment.UserDomainName,
			["env.UserInteractive"] = () => Environment.UserInteractive,
			["env.UserName"] = () => Environment.UserName,
			["env.Version"] = () => Environment.Version,
			["env.WorkingSet"] = () => Environment.WorkingSet,
			["env.StackTrace"] = () => Environment.StackTrace,
			["env.NewLine"] = () => Environment.NewLine,
		};

		var allFlags = (Environment.SpecialFolder[])Enum.GetValues(typeof(Environment.SpecialFolder));
		var flags = allFlags ?? [];

		foreach (var flag in flags)
		{
			var key = "folder." + flag.ToString();
			if (dic.ContainsKey(key)) continue;

			dic.Add(key, () => Environment.GetFolderPath(flag, Environment.SpecialFolderOption.None));
		}
		return _defaults = dic;
	}

	/// <summary>
	/// Formats the given dictionary of arguments as a resolver dictionary
	/// </summary>
	/// <param name="dic">The dictionary to convert</param>
	/// <returns>The formatted version</returns>
	internal static Resolvers ToArgFormat(Dictionary<string, object?> dic)
	{
		return dic.ToDictionary(t => t.Key, t =>
		{
			object? val() => t.Value;
			return (Func<object?>)val;
		});
	}

	/// <summary>
	/// Formats the given dictionary of arguments as a resolver dictionary
	/// </summary>
	/// <param name="dic">The dictionary to convert</param>
	/// <returns>The formatted version</returns>
	internal static Resolvers ToArgFormat(Dictionary<string, string?> dic)
	{
		return dic.ToDictionary(t => t.Key, t =>
		{
			object? val() => t.Value;
			return (Func<object?>)val;
		});
	}
}
