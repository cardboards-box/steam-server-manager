namespace SteamServerManager.Processing;

/// <summary>
/// A service for executing third party processes or commands
/// </summary>
public interface IProcessService
{
	/// <summary>
	/// Executes the given command via the command shell for the current operating system
	/// </summary>
	/// <param name="command">The command or file to execute</param>
	/// <param name="arguments">The arguments to pass to the command or file</param>
	/// <param name="workingDirectory">The optional working directory for the file or command</param>
	/// <param name="token">The cancellation token for cancelling the execution of the process</param>
	/// <param name="logEvents">Whether or not to log process events</param>
	/// <returns>The result of the process execution</returns>
	Task<ProcessResult> Run(string command, string arguments, string? workingDirectory = null, bool logEvents = true, CancellationToken token = default);

	/// <summary>
	/// Executes the given <see cref="ProcessStartInfo"/> via the command shell for the current operating system
	/// </summary>
	/// <param name="process">The process to execute</param>
	/// <param name="token">The cancellation token for cancelling the execution of the process</param>
	/// <param name="logEvents">Whether or not to log process events</param>
	/// <returns>The result of the process execution</returns>
	Task<ProcessResult> Run(ProcessStartInfo process, bool logEvents = true, CancellationToken token = default);

	/// <summary>
	/// Executes an inline PowerShell script via the command shell for the current operating system
	/// </summary>
	/// <param name="script">The inline PowerShell script to run</param>
	/// <param name="workingDirectory">The optional working directory for the script</param>
	/// <param name="token">The cancellation token for cancelling the execution of the script</param>
	/// <param name="logEvents">Whether or not to log process events</param>
	/// <returns>The result of the script execution</returns>
	/// <remarks>This will only work on windows as it uses "powershell.exe" as the command</remarks>
	Task<ProcessResult> PowerShellScript(string script, string? workingDirectory = null, bool logEvents = true, CancellationToken token = default);

	/// <summary>
	/// Executes a PowerShell script file via the command shell for the current operating system
	/// </summary>
	/// <param name="path">The path to the PowerShell script file</param>
	/// <param name="workingDirectory">The optional working directory for the script</param>
	/// <param name="token">The cancellation token for cancelling the execution of the script</param>
	/// <param name="logEvents">Whether or not to log process events</param>
	/// <returns>The result of the script execution</returns>
	/// <remarks>This will only work on windows as it uses "powershell.exe" as the command</remarks>
	Task<ProcessResult> PowerShellFile(string path, string? workingDirectory = null, bool logEvents = true, CancellationToken token = default);

	/// <summary>
	/// Open a folder (or file) in windows explorer
	/// </summary>
	/// <param name="path">The file or folder to open</param>
	/// <param name="token">The cancellation token for cancelling the execution of the process</param>
	/// <param name="logEvents">Whether or not to log process events</param>
	/// <returns>The result of the script execution</returns>
	/// <remarks>This will only work on windows as it uses "explorer.exe" as the command</remarks>
	Task<ProcessResult> OpenFolder(string path, bool logEvents = true, CancellationToken token = default);

	/// <summary>
	/// Clears read-only flags for all of the files in the given directory
	/// </summary>
	/// <param name="path">The path to set the flags</param>
	/// <param name="token">The cancellation token for cancelling the execution of the process</param>
	/// <param name="logEvents">Whether or not to log process events</param>
	/// <returns>The result of the script execution</returns>
	/// <remarks>This will only work on windows as it uses "cmd.exe" as the command</remarks>
	Task<ProcessResult> SetFolderFlags(string path, bool logEvents = true, CancellationToken token = default);
}

internal class ProcessService(
	ILogger<ProcessService> _logger) : IProcessService
{
	public Task<ProcessResult> Run(string command, string arguments, string? workingDirectory = null, bool logEvents = true, CancellationToken token = default)
	{
		var info = new ProcessStartInfo
		{
			FileName = command,
			Arguments = arguments,
			WorkingDirectory = workingDirectory,
		};
		return Run(info, logEvents, token);
	}

	public Task<ProcessResult> Run(ProcessStartInfo info, bool logEvents = true, CancellationToken token = default)
	{
		var proxy = new ProcessProxy(info);
		return Run(proxy, logEvents, token);
	}

	public async Task<ProcessResult> Run(ProcessProxy proxy, bool logEvents = true, CancellationToken token = default)
	{
		if (logEvents) proxy.WithLogger(_logger);
		await proxy.WithTracking().Start(token);
		return await proxy.WaitForResult(CancellationToken.None);
	}

	public Task<ProcessResult> PowerShellScript(string script, string? workingDirectory = null, bool logEvents = true, CancellationToken token = default)
	{
		var proxy = ProcessProxy.PowerShellScript(script, workingDirectory);
		return Run(proxy, logEvents, token);
	}

	public Task<ProcessResult> PowerShellFile(string path, string? workingDirectory = null, bool logEvents = true, CancellationToken token = default)
	{
		var proxy = ProcessProxy.PowerShellFile(path, workingDirectory);
		return Run(proxy, logEvents, token);
	}

	public Task<ProcessResult> OpenFolder(string path, bool logEvents = true, CancellationToken token = default)
	{
		var info = new ProcessStartInfo
		{
			FileName = "explorer.exe",
			Arguments = Path.GetFullPath(path),
		};
		return Run(info, logEvents, token);
	}

	public Task<ProcessResult> SetFolderFlags(string path, bool logEvents = true, CancellationToken token = default)
	{
		return Run("cmd.exe", $" /C ATTRIB -R  \"{path}\\*.*\" /S /D", null, logEvents, token);
	}
}