namespace SteamServerManager.GameServer.Verbs;

using Processing;

/// <summary>
/// Installs all of the game's files
/// </summary>
[Verb("install-game", HelpText = "Installs all of the game's files")]
public class InstallGameOptions
{
	internal const string DEFAULT_USER = "anonymous";

	/// <summary>
	/// The App ID for the game server
	/// </summary>
	[Option('i', "app-id", HelpText = "The App ID for the game server", Required = true)]
	public int AppId { get; set; }

	/// <summary>
	/// The steam username for login
	/// </summary>
	[Option('u', "steam-username", HelpText = "The steam username for login", Default = DEFAULT_USER)]
	public string Username { get; set; } = DEFAULT_USER;

	/// <summary>
	/// The steam password for login
	/// </summary>
	[Option('p', "steam-password", HelpText = "The steam password for login", Required = false)]
	public string? Password { get; set; }

	/// <summary>
	/// Whether or not to validate the game server files after installation
	/// </summary>
	[Option('v', "validate", HelpText = "Whether or not to validate the game server files after installation")]
	public bool? Validate { get; set; }

	/// <summary>
	/// Any app configs that should be set, this is everything after `+app_set_config`
	/// </summary>
	[Option('c', "app-config", HelpText = "Any app configs that should be set, this is everything after `+app_set_config `")]
	public IEnumerable<string> AppConfigs { get; set; } = [];

	/// <summary>
	/// The beta branch to install
	/// </summary>
	[Option('b', "beta-branch", HelpText = "The beta branch to install")]
	public string? BetaBranch { get; set; }

	/// <summary>
	/// The beta password for the branch
	/// </summary>
	[Option('w', "beta-password", HelpText = "The beta password for the branch")]
	public string? BetaPassword { get; set; }

	/// <summary>
	/// The platform to force steam to install as (One of: windows, linux, macos)
	/// </summary>
	[Option('o', "os-platform", HelpText = "The platform to force steam to install as (One of: windows, linux, macos)")]	
	public string? Platform { get; set; }
}

internal class InstallGameVerb(
	ISteamService _steam,
    ILogger<InstallGameVerb> logger) : BooleanVerb<InstallGameOptions>(logger)
{
	public string BuildCommand(string directory, InstallGameOptions options)
	{
		void CmdPlatform(StringBuilder builder)
		{
			if (string.IsNullOrEmpty(options.Platform))
				return;
			builder.Append("+@sSteamCmdForcePlatformType ");
			builder.Append(options.Platform);
			builder.Append(' ');
		}
		void CmdAppUpdate(StringBuilder builder)
		{
			builder.Append("+app_update ");
			builder.Append(options.AppId);
			builder.Append(' ');

			if (!string.IsNullOrEmpty(options.BetaBranch))
			{
				builder.Append("-beta ");
				builder.Append(options.BetaBranch);
				builder.Append(' ');

				if (!string.IsNullOrEmpty(options.BetaPassword))
				{
					builder.Append("-betapassword ");
					builder.Append(options.BetaPassword);
					builder.Append(' ');
				}
			}

			if (options.Validate ?? true)
				builder.Append("validate ");
		}
		void CmdForceInstallDir(StringBuilder builder)
		{
			builder.Append("+force_install_dir \"");
			builder.Append(directory);
			builder.Append("\" ");
		}
		void CmdLogin(StringBuilder builder)
		{
			builder.Append("+login ");
			builder.Append(options.Username?.ForceNull() ?? InstallGameOptions.DEFAULT_USER);
			builder.Append(' ');
			if (!string.IsNullOrEmpty(options.Password))
			{
				builder.Append(options.Password);
				builder.Append(' ');
			}
		}
		void CmdAppSetConfig(StringBuilder builder)
		{
			if (options.AppConfigs is null ||
				!options.AppConfigs.Any())
				return;

			foreach (var config in options.AppConfigs)
			{
				builder.Append("+app_set_config ");
				builder.Append(config);
				builder.Append(' ');
			}
		}

		//https://developer.valvesoftware.com/wiki/SteamCMD
		var builder = new StringBuilder();
		CmdPlatform(builder);
		CmdForceInstallDir(builder);
		CmdLogin(builder);
		CmdAppSetConfig(builder);
		CmdAppUpdate(builder);
		builder.Append("+quit");
		return builder.ToString();
	}

	public (string cmd, string args) GetCommand(InstallGameOptions options)
	{
		var args = BuildCommand(Path.GetRelativePath(_steam.SteamDir, _steam.GameDir), options);
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			return (_steam.SteamCmdExe, args);

		args = Path.GetFullPath(_steam.SteamCmdExe) + " " + args;
		return ("/bin/bash", args);
	}

    public override async Task<bool> Execute(InstallGameOptions options, CancellationToken token)
    {
        if (!_steam.SteamInstalled)
		{
			_logger.LogWarning("Steam is not installed >> {Exe}", _steam.SteamCmdExe);
			return false;
		}

		var absPath = Path.GetFullPath(_steam.SteamDir);
		var (cmd, args) = GetCommand(options);

		using var proxy = new ProcessProxy(cmd)
			.WithArgs(args)
			.WithLogger(_logger, "game-install")
			.WithWorkingDirectory(absPath);
		_logger.LogInformation("Installing game server >> {WorkDir} {Cmd} in ", _steam.SteamCmdExe, cmd);
		if (!await proxy.Start(token))
		{
			_logger.LogError("Failed to start steam CMD with command: {Cmd}", cmd);
			return false;
		}

		var result = await proxy.WaitForExit(token);
		if (!result.Success)
		{
			_logger.LogError(result.Exception, "Failed to install game server >> {Code} in {Span}", result.ExitCode, result.Elapsed);
			return false;
		}

		_logger.LogInformation("Game server installed >> {Code} in {Span}", result.ExitCode, result.Elapsed);
		return true;
    }
}
