namespace SteamServerManager.Processing.Exceptions;

/// <summary>
/// The various error codes for operations on <see cref="ProcessProxy"/>.
/// </summary>
public enum ProcessErrorCode
{
	/// <summary>
	/// An unknown error occurred
	/// </summary>
	Unknown = 0,
	/// <summary>
	/// Error occurred while running the process in execution thread
	/// </summary>
	RunningProcess,
	/// <summary>
	/// Error occurred while starting the process in <see cref="ProcessProxy.Start(CancellationToken)"/>
	/// </summary>
	StartFailed,
	/// <summary>
	/// Error occurred while trying to stop the process in <see cref="ProcessProxy.Stop(CancellationToken)"/>
	/// </summary>
	Stop,
	/// <summary>
	/// Error occurred while sending interupt signal to the process in <see cref="ProcessProxy.Stop(CancellationToken)"/>
	/// </summary>
	Stop_Interupt,
	/// <summary>
	/// Error occurred while sending abort signal to the process in <see cref="ProcessProxy.Stop(CancellationToken)"/>
	/// </summary>
	Stop_Abort,
	/// <summary>
	/// Error occurred while sending term signal to the process in <see cref="ProcessProxy.Stop(CancellationToken)"/>
	/// </summary>
	Stop_Term,
	/// <summary>
	/// Error occurred while trying to close main window for the process in <see cref="ProcessProxy.Stop(CancellationToken)"/>
	/// </summary>
	Stop_CloseMainWindow,
	/// <summary>
	/// Error occurred while attempting to kill the process in <see cref="ProcessProxy.Kill(CancellationToken)"/>
	/// </summary>
	Kill,
	/// <summary>
	/// Indicates that the requested signal is not supported on the current platform
	/// </summary>
	Signal_NotSupported,
	/// <summary>
	/// Indicates that the signal could not be sent because the target process is not running.
	/// </summary>
	Signal_ProcessNotRunning,
	/// <summary>
	/// Indicates that an error occurred while trying to write to the standard input stream of the process.
	/// </summary>
	Write,
}
