namespace SteamServerManager.GameServer;

/// <summary>
/// Represents an environment steam can run in
/// </summary>
public interface ISteamEnvironment
{
	/// <summary>
	/// The platform the environment is for
	/// </summary>
	OSPlatform Platform { get; }

	/// <summary>
	/// The URL to download the steamcmd tool from
	/// </summary>
	string SteamCmdUrl { get; }

	/// <summary>
	/// The name of the steamcmd executable file
	/// </summary>
	string SteamCmdExe { get; }
}

/// <summary>
/// The windows implementation of the <see cref="ISteamEnvironment"/>
/// </summary>
public class SteamEnvironmentWindows : ISteamEnvironment
{
	/// <inheritdoc/>
	public OSPlatform Platform => OSPlatform.Windows;
	/// <inheritdoc/>
	public string SteamCmdUrl => "https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip";
	/// <inheritdoc/>
	public string SteamCmdExe => "steamcmd.exe";
}

/// <summary>
/// The linux implementation of the <see cref="ISteamEnvironment"/>
/// </summary>
public class SteamEnvironmentLinux : ISteamEnvironment
{
	/// <inheritdoc/>
	public OSPlatform Platform => OSPlatform.Linux;
	/// <inheritdoc/>
	public string SteamCmdUrl => "https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz";
	/// <inheritdoc/>
	public string SteamCmdExe => "steamcmd.sh";
}

/// <summary>
/// The OSX/Mac implementation of the <see cref="ISteamEnvironment"/>
/// </summary>
public class SteamEnvironmentOSX : ISteamEnvironment
{
	/// <inheritdoc/>
	public OSPlatform Platform => OSPlatform.OSX;
	/// <inheritdoc/>
	public string SteamCmdUrl => "https://steamcdn-a.akamaihd.net/client/installer/steamcmd_osx.tar.gz";
	/// <inheritdoc/>
	public string SteamCmdExe => "steamcmd.sh";
}
