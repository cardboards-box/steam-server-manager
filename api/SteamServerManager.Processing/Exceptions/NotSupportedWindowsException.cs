namespace SteamServerManager.Processing.Exceptions;

/// <summary>
/// Thrown if something isn't supported on windows
/// </summary>
/// <param name="component">The component that isn't supported</param>
public class NotSupportedWindowsException(string component) 
	: NotSupportedException($"{component} is/are not supported on Windows.");
