namespace SteamServerManager.Processing.Exceptions;

/// <summary>
/// Represents an error that occurred within a <see cref="ProcessProxy"/>
/// </summary>
/// <param name="Code">The error code indicating where the error happened</param>
/// <param name="Exception">The exception that was thrown</param>
public record class ProcessError(
	ProcessErrorCode Code,
	Exception Exception);
