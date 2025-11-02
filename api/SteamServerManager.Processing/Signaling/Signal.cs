using Mono.Unix.Native;

namespace SteamServerManager.Processing.Signaling;

/// <summary>
/// The various signals that can be sent to a process that are supported across all platforms.
/// </summary>
public enum Signal
{
	/// <summary>
	/// Represents an unspecified or unrecognized value.
	/// </summary>
	Unknown = 0,
	/// <summary>
	/// <see cref="Signum.SIGTERM"/> or <see cref="WindowsSignal.CTRL_SHUTDOWN_EVENT"/>
	/// </summary>
	Terminate = 1,
	/// <summary>
	/// <see cref="Signum.SIGABRT"/> or <see cref="WindowsSignal.CTRL_CLOSE_EVENT"/>
	/// </summary>
	Abort = 2,
	/// <summary>
	/// <see cref="Signum.SIGINT"/> or <see cref="WindowsSignal.CTRL_C_EVENT"/>
	/// </summary>
	Interupt = 3
}
