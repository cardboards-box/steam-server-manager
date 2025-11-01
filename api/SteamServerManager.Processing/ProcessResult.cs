namespace SteamServerManager.Processing;

/// <summary>
/// Represents the result of a process being run by the <see cref="IProcessService"/>
/// </summary>
/// <param name="ExitCode">The exit code of the process (this is a GNU exit code)</param>
/// <param name="Exception">The caught exception that occurred during the process execution</param>
/// <param name="Elapsed">How long it took to run the process</param>
public record class ProcessResult(
	int ExitCode,
	Exception? Exception,
	TimeSpan Elapsed)
{
	/// <summary>
	/// The exit code success range lower value
	/// </summary>
	public const int SUCCESS_EXIT_CODE_RANGE_START = 0;

	/// <summary>
	/// The exit code success range upper value
	/// </summary>
	public const int SUCCESS_EXIT_CODE_RANGE_END = 0;

	/// <summary>
	/// The content of the Standard Output stream if available
	/// </summary>
	public string? Output { get; set; }

	/// <summary>
	/// The content of the Standard Error stream if available
	/// </summary>
	public string? Error { get; set; }

	/// <summary>
	/// Whether or not the process exited successfully
	/// </summary>
	public bool Success => Exception is null &&
		IsSuccessExitCode(SUCCESS_EXIT_CODE_RANGE_START, SUCCESS_EXIT_CODE_RANGE_END);

	/// <summary>
	/// Whether or not the exit code is within the given range
	/// </summary>
	/// <param name="successStart">The lower range value</param>
	/// <param name="successEnd">The upper range value</param>
	/// <returns>Whether or not the exit code is within the range</returns>
	public bool IsSuccessExitCode(int successStart, int successEnd)
	{
		return ExitCode >= successStart &&
			ExitCode <= successEnd;
	}

	/// <summary>
	/// The default process result
	/// </summary>
	public static ProcessResult Default => new(
		ExitCode: -1,
		Exception: null,
		Elapsed: TimeSpan.Zero);
}
