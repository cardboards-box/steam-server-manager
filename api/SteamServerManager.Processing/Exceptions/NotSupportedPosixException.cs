namespace SteamServerManager.Processing.Exceptions;

/// <summary>
/// Thrown if something isn't supported on POSIX/UNIX
/// </summary>
/// <param name="component">The component that isn't supported</param>
public class NotSupportedPosixException(string component)
	: NotSupportedException($"{component} is/are not supported on POSIX/UNIX.");
