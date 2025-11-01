namespace SteamServerManager.Processing.Exceptions;

/// <summary>
/// Thrown when attempting to interact with a process that is not currently running.
/// </summary>
public class ProcessNotRunningException() 
	: InvalidOperationException("Process is not running!") { };
