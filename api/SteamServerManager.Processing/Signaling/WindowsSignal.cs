namespace SteamServerManager.Processing.Signaling;

/// <summary>
/// The various signals that can be sent to a Windows process.
/// </summary>
public enum WindowsSignal : uint
{
	#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    CTRL_C_EVENT = 0,
    CTRL_BREAK_EVENT = 1,
	CTRL_CLOSE_EVENT = 2,
	CTRL_LOGOFF_EVENT = 5,
	CTRL_SHUTDOWN_EVENT = 6
	#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}
