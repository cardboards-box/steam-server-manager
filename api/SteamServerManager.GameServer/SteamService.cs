namespace SteamServerManager.GameServer;

/// <summary>
/// A service for interacting with Steam
/// </summary>
public interface ISteamService
{
	/// <summary>
	/// The steam environment for the current platform
	/// </summary>
	ISteamEnvironment Environment { get; }

	/// <summary>
	/// The path to the steamcmd executable (or shell file)
	/// </summary>
	string SteamCmdExe { get; }

	/// <summary>
	/// Whether or not the <see cref="SteamCmdExe"/> exists
	/// </summary>
	bool SteamInstalled { get; }

	/// <summary>
	/// The path to the steam-cmd directory
	/// </summary>
	string SteamDir { get; }

	/// <summary>
	/// The path to where the game files should be stored
	/// </summary>
	string GameDir { get; }

	/// <summary>
	/// The path to where the mods are stored
	/// </summary>
	string ModDir { get; }
}

internal class SteamService(IConfiguration _config) : ISteamService
{
	private ISteamEnvironment? _environment;
	private string? _root, _steam, _steamCmdPath, _gameFiles, _mods;

	public ISteamEnvironment Environment => _environment ??= 
		RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? new SteamEnvironmentWindows() :
		RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? new SteamEnvironmentLinux() :
		RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? new SteamEnvironmentOSX() :
		throw new PlatformNotSupportedException("Unsupported OS platform for SteamCMD installation.");

	public string SteamCmdExe => _steamCmdPath ??= Path.Combine(SteamDir, Environment.SteamCmdExe);

	public bool SteamInstalled => File.Exists(SteamCmdExe);

	public string RootDirectory => _root ??= GetRootDirectory();

	public string SteamDir => _steam ??= FormatPath("steam");

	public string GameDir => _gameFiles ??= FormatPath("game");

	public string ModDir => _mods ??= FormatPath("mods");

	public string GetRootDirectory()
	{
		var option = _config["RootDirectory"]?.ForceNull();
		if (option is null) return "system-files";

		if (!Directory.Exists(option))
			return option;

		return Path.GetRelativePath(Directory.GetCurrentDirectory(), option);
	}

	public string FormatPath(string path)
	{
		if (!Directory.Exists(RootDirectory))
			Directory.CreateDirectory(RootDirectory);

		if (!path.StartsWithIc(RootDirectory))
			path = Path.Combine(RootDirectory, path);

		path = Path.Combine(path.Split(['\\', '/'], StringSplitOptions.TrimEntries));
		if (!Directory.Exists(path))
			Directory.CreateDirectory(path);

		return path;
	}
}
