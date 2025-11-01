namespace SteamServerManager.Processing.Delegates;

using Exceptions;

/// <summary>
/// Represents an event that handles errors occurring during a process.
/// </summary>
/// <param name="code">The error code indicating the type of error that occurred during the process.</param>
/// <param name="exception">The error that occurred</param>
public delegate void ProcessErrorHandler(ProcessErrorCode code, Exception exception);