using Mono.Unix.Native;

namespace SteamServerManager.Processing;

using Exceptions;
using Signaling;

/// <summary>Represents a proxy for managing processes</summary>
/// <param name="startInfo">The information used for starting the process</param>
/// <param name="maxErrors">The maximum number of errors to track</param>
/// <remarks>
/// <para>The following properties on the <see cref="ProcessStartInfo"/> parameter (<paramref name="startInfo"/>) will be overwritten:</para>
/// <para><see cref="ProcessStartInfo.CreateNoWindow"/> will always be <see langword="true"/></para>
/// <para><see cref="ProcessStartInfo.RedirectStandardError"/> will always be <see langword="true"/></para>
/// <para><see cref="ProcessStartInfo.RedirectStandardInput"/> will always be <see langword="true"/></para>
/// <para><see cref="ProcessStartInfo.RedirectStandardOutput"/> will always be <see langword="true"/></para>
/// <para><see cref="ProcessStartInfo.UseShellExecute"/> will always be <see langword="false"/></para>
/// <para><see cref="ProcessStartInfo.WindowStyle"/> will always be <see cref="ProcessWindowStyle.Hidden"/></para>
/// </remarks>
public partial class ProcessProxy(
	ProcessStartInfo startInfo,
	int maxErrors = ProcessProxy.MAX_ERRORS_TRACKED) : TextWriter
{
	#region Constants
	/// <summary>The maximum number of errors to track by default</summary>
	public const int MAX_ERRORS_TRACKED = 10;
	/// <summary>The file name for executing PowerShell scripts</summary>
	public const string POWERSHELL_FILENAME = "powershell.exe";
	/// <summary>Command arguments for executing PowerShell scripts</summary>
	public const string POWERSHELL_ARGUMENTS = "-NoProfile -ExecutionPolicy unrestricted";
	/// <summary>Command arguments for executing encoded in-line PowerShell scripts</summary>
	public const string POWERSHELL_ARGUMENTS_ENCODED = POWERSHELL_ARGUMENTS + " -EncodedCommand {0}";
	/// <summary>Command arguments for executing PowerShell script files</summary>
	public const string POWERSHELL_ARGUMENTS_FILE = POWERSHELL_ARGUMENTS + " -File {0}";
	/// <summary>The default encoding to use for all process proxies</summary>
	public static Encoding DefaultEncoding { get; set; } = Encoding.UTF8;
	#endregion

	#region Fields
	private readonly Stopwatch _timer = new();
	private readonly SemaphoreSlim _accessControl = new(1, 1);
	private readonly FixedQueue<ProcessError> _errors = new(maxErrors);
	private Process? _process;
	private Encoding? _encoding;
	private ProcessResult? _lastResult;
	#endregion

	#region Properties
	/// <summary>The amount of time the process has been running</summary>
	public TimeSpan Elapsed => _timer.Elapsed;
	/// <summary>The writer to the standard input of the process</summary>
	/// <remarks>Only available if <see cref="Running"/> is <see langword="true"/></remarks>
	public StreamWriter? Writer => _process?.StandardInput;
	/// <summary>Whether or not the process is currently running</summary>
	public bool Running { get; private set; } = false;
	/// <summary>The exception that was thrown by the process</summary>
	public ProcessError? LastError => _errors.LastOrDefault();
	/// <summary>The last 10 errors that have occurred within the process proxy</summary>
	public IEnumerable<ProcessError> Errors => _errors;
	/// <summary>The name of the file being executed</summary>
	public string FileName => startInfo.FileName;
	/// <summary>The arguments being passed to the executable</summary>
	public string Arguments => string.IsNullOrEmpty(startInfo.Arguments) ? string.Join(' ', startInfo.ArgumentList) : startInfo.Arguments;
	/// <inheritdoc />
	public override Encoding Encoding => _encoding ??= DefaultEncoding;
	#endregion

	/// <summary>Represents a proxy for managing processes</summary>
	/// <param name="command">The executable to run in the new process</param>
	/// <param name="maxErrors">The maximum number of errors to track</param>
	public ProcessProxy(string command,int maxErrors = MAX_ERRORS_TRACKED) 
		: this(new ProcessStartInfo { FileName = command }, maxErrors) { }

	#region Public Methods
	/// <summary>Attempts to start the process</summary>
	/// <param name="token">The cancellation token to use for the process</param>
	/// <returns>
	/// <para><see langword="true"/> if the process was started.</para>
	/// <para><see langword="false"/> if the process was already running or an exception occurred</para>
	/// </returns>
	public async Task<bool> Start(CancellationToken token = default)
	{

		try
		{
			await _accessControl.WaitAsync(token);
			if (Running) return false;

			var tsc = new TaskCompletionSource();
			token.Register(() => tsc.TrySetCanceled());
			using var sub = OnStarted.Subscribe((_) => tsc.TrySetResult());

			_ = Task.Run(ExecuteThread, CancellationToken.None);

			await tsc.Task;
			return true;
		}
		catch (Exception ex)
		{
			SetError(ProcessErrorCode.StartFailed, ex);
			return false;
		}
		finally
		{
			_accessControl.Release();
		}
	}

	/// <summary>Attempts to gracefully stop the process</summary>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>
	/// <para><see langword="true"/> if the process was asked to gracefully stop or the process is already stopped</para>
	/// <para><see langword="false"/> if no appropriate termination was available (use <see cref="Kill"/> to forcefully kill the process)</para>
	/// </returns>
	/// <remarks>
	/// <para>This method will attempt the following in order: SIG-INTERUPT, SIG-ABORT, SIG-TERM, <see cref="Process.CloseMainWindow"/></para>
	/// <para>If all attempts fail, the method will return <see langword="false"/></para>
	/// <para>This method returning does not mean the process has exited, be sure to await <see cref="WaitForExit(CancellationToken)"/> afterward for that.</para>
	/// </remarks>
	public async Task<bool> Stop(CancellationToken token = default)
	{
		bool Send(Signal signal) => SendSignal(signal, out var code) && code == 0;

		try
		{
			await _accessControl.WaitAsync(token);
			if (!Running) return true;

			(ProcessErrorCode, Func<bool>)[] tries =
			[
				(ProcessErrorCode.Stop_Interupt, () => Send(Signal.Interupt)),
				(ProcessErrorCode.Stop_Abort, () => Send(Signal.Abort)),
				(ProcessErrorCode.Stop_Term, () => Send(Signal.Terminate)),
				(ProcessErrorCode.Stop_CloseMainWindow, () => _process!.CloseMainWindow()),
			];

			foreach (var (code, action) in tries)
			{
				try
				{
					if (action()) return true;
				}
				catch (Exception ex)
				{
					SetError(code, ex);
				}
			}

			return false;
		}
		catch (Exception ex)
		{
			SetError(ProcessErrorCode.Stop, ex);
			return false;
		}
		finally
		{
			_accessControl.Release();
		}
	}

	/// <summary>Attempts to forcefully kill the process</summary>
	/// <returns>
	/// <para><see langword="true"/> if the process was killed or is already stopped</para>
	/// <para><see langword="false"/> if the process threw an error while attempting to kill</para>
	/// </returns>
	public async Task<bool> Kill(CancellationToken token = default)
	{
		try
		{
			await _accessControl.WaitAsync(token);
			if (!Running) return true;

			_process!.Kill(true);
			return true;
		}
		catch (Exception ex)
		{
			SetError(ProcessErrorCode.Kill, ex);
			return false;
		}
		finally
		{
			_accessControl.Release();
		}
	}

	/// <summary>Waits until the process is closed</summary>
	/// <param name="token">The cancellation token for the wait operation</param>
	/// <returns>The result of the process</returns>
	/// <remarks>
	/// <para>Prefer hooking <see cref="OnExited"/></para>
	/// <para><see cref="OperationCanceledException"/> is not thrown when the <paramref name="token"/> is cancelled.</para>
	/// </remarks>
	public async Task<ProcessResult> WaitForExit(CancellationToken token = default)
	{
		if (!Running) 
			return _lastResult ?? ProcessResult.Default;

		var tsc = new TaskCompletionSource<ProcessResult>();
		token.Register(() => tsc.TrySetCanceled());
		using var sub = OnExited.Subscribe(tsc.SetResult);
		return await tsc.Task;
	}

	/// <summary>Sends a signal to the process based on the current platform</summary>
	/// <param name="signal">The signal to send</param>
	/// <param name="code">The return code of the call, or -1 if something went wrong</param>
	/// <returns>Whether or not the signal was sent, regardless of the result of the siganl</returns>
	/// <remarks>If this returns <see langword="false"/> you can check <see cref="LastError"/> for more information.</remarks>
	/// <exception cref="NotSupportedException">Thrown if the signal isn't supported on the current platform</exception>
	public bool SendSignal(Signal signal, out int code)
	{
		code = -1;
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			WindowsSignal? ws = signal switch
			{
				Signal.Abort => WindowsSignal.CTRL_CLOSE_EVENT,
				Signal.Terminate => WindowsSignal.CTRL_SHUTDOWN_EVENT,
				Signal.Interupt => WindowsSignal.CTRL_C_EVENT,
				_ => null
			};

			if (ws is not null)
				return SendSignalWindows(ws.Value, out code);

			SetError(ProcessErrorCode.Signal_NotSupported, new NotSupportedWindowsException($"Signal {signal}"));
			return false;
		}

		Signum? ps = signal switch
		{
			Signal.Abort => Signum.SIGABRT,
			Signal.Terminate => Signum.SIGTERM,
			Signal.Interupt => Signum.SIGINT,
			_ => null
		};

		if (ps is not null)
			return SendSignalPosix(ps.Value, out code);

		SetError(ProcessErrorCode.Signal_NotSupported, new NotSupportedPosixException($"Signal {signal}"));
		return false;
	}

	/// <summary>Attempts to send a Windows signal to process</summary>
	/// <param name="signal">The signal to send to the process</param>
	/// <param name="code">The return code of the call, or -1 if something went wrong</param>
	/// <returns>Whether or not the signal was sent, regardless of the result of the siganl</returns>
	/// <remarks>
	/// <para>This does not work on POSIX/UNIX, use either <see cref="SendSignalPosix(Signum, out int)"/> or <see cref="SendSignal(Signal, out int)"/></para>
	/// <para>If this returns <see langword="false"/> you can check <see cref="LastError"/> for more information.</para>
	/// </remarks>
	public bool SendSignalWindows(WindowsSignal signal, out int code)
	{
		if (!Running)
		{
			code = -1;
			SetError(ProcessErrorCode.Signal_ProcessNotRunning, new ProcessNotRunningException());
			return false;
		}

		if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			code = -1;
			SetError(ProcessErrorCode.Signal_NotSupported, new NotSupportedPosixException("Windows signals"));
			return false;
		}

		code = WindowsConsoleInterop.SendSignal(_process!, signal);
		return true;
	}

	/// <summary>Attempts to send a POSIX signal to process</summary>
	/// <param name="signal">The signal to send to the process</param>
	/// <param name="code">The return code of the call, or -1 if something went wrong</param>
	/// <returns>Whether or not the signal was sent, regardless of the result of the siganl</returns>
	/// <remarks>
	/// <para>This does not work on Windows, use either <see cref="SendSignalWindows(WindowsSignal, out int)"/> or <see cref="SendSignal(Signal, out int)"/></para>
	/// <para>If this returns <see langword="false"/> you can check <see cref="LastError"/> for more information.</para>
	/// </remarks>
	public bool SendSignalPosix(Signum signal, out int code)
	{
		if (!Running)
		{
			code = -1;
			SetError(ProcessErrorCode.Signal_ProcessNotRunning, new ProcessNotRunningException());
			return false;
		}

		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			code = -1;
			SetError(ProcessErrorCode.Signal_NotSupported, new NotSupportedWindowsException("POSIX/UNIX signals"));
			return false;
		}

		code = Syscall.kill(_process!.Id, signal);
		return true;
	}

	/// <summary>Reads all lines from the <see cref="Process.StandardOutput"/></summary>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>All of the lines read from standard output</returns>
	public IAsyncEnumerable<string> ReadAllOutputLines(CancellationToken token = default)
	{
		var (channel, subs) = Channelable<string>();
		subs.Add(OnStandardOutput.Subscribe(async t => await channel.Writer.WriteAsync(t, token)));
		return channel.Reader.ReadAllAsync(token);
	}

	/// <summary>Reads all lines from the <see cref="Process.StandardError"/></summary>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>All of the lines read from standard error</returns>
	public IAsyncEnumerable<string> ReadAllErrorLines(CancellationToken token = default)
	{
		var (channel, subs) = Channelable<string>();
		subs.Add(OnStandardError.Subscribe(async t => await channel.Writer.WriteAsync(t, token)));
		return channel.Reader.ReadAllAsync(token);
	}
	#endregion

	#region Internal Methods
	/// <summary>The main execution thread for the process</summary>
	internal async Task ExecuteThread()
	{
		var code = -1;
		try
		{
			Running = true;
			_lastResult = null;
			startInfo.CreateNoWindow = true;
			startInfo.RedirectStandardOutput = true;
			startInfo.RedirectStandardError = true;
			startInfo.RedirectStandardInput = true;
			startInfo.UseShellExecute = false;
			startInfo.WindowStyle = ProcessWindowStyle.Hidden;
			_process = new Process 
			{ 
				StartInfo = startInfo, 
				EnableRaisingEvents = true, 
			};
			_process.OutputDataReceived += (_, args) =>
			{
				if (args.Data is null) return;
				_standardOutput.OnNext(args.Data);
			};
			_process.ErrorDataReceived += (_, args) =>
			{
				if (args.Data is null) return;
				_standardError.OnNext(args.Data);
			};
			_timer.Start();
			_process.Start();
			_process.BeginErrorReadLine();
			_process.BeginOutputReadLine();

			_started.OnNext(_process.Id);
			await _process.WaitForExitAsync();
			code = _process.ExitCode;

			_process.CancelErrorRead();
			_process.CancelOutputRead();
		}
		catch (OperationCanceledException) { }
		catch (Exception ex)
		{
			SetError(ProcessErrorCode.RunningProcess, ex);
		}

		Running = false;
		_timer.Stop();
		_lastResult = new(code, LastError?.Exception, Elapsed);
		_exited.OnNext(_lastResult);
		_process?.Dispose();
		_process = null;
	}

	/// <summary>Creates a channel that completes when the process exits</summary>
	/// <typeparam name="T">The type of channel</typeparam>
	/// <param name="options">The channel options</param>
	/// <returns>The channel and the subscriptions collection</returns>
	internal (Channel<T> channel, List<IDisposable> subs) Channelable<T>(UnboundedChannelOptions? options = null)
	{
		options ??= new UnboundedChannelOptions
		{
			SingleReader = false,
			SingleWriter = true,
		};
		var channel = Channel.CreateUnbounded<T>(options);
		List<IDisposable> subs = [];
		subs.Add(OnExited.Subscribe(_ =>
		{
			channel.Writer.Complete();
			subs.ForEach(s => s.Dispose());
			subs.Clear();
		}));
		return (channel, subs);
	}
	#endregion

	#region Static helper methods
	/// <summary>
	/// Creates a process proxy for executing an inline PowerShell script
	/// </summary>
	/// <param name="script">The inline script to run</param>
	/// <param name="workingDirectory">The working directory for the script</param>
	/// <returns>The process proxy</returns>
	public static ProcessProxy PowerShellScript(string script, string? workingDirectory = null)
	{
		script = string.Format(POWERSHELL_ARGUMENTS_ENCODED, 
			Convert.ToBase64String(DefaultEncoding.GetBytes(script)));
		return new ProcessProxy(POWERSHELL_FILENAME)
			.WithArgs(script)
			.WithWorkingDirectory(workingDirectory);
	}

	/// <summary>
	/// Creates a process proxy for executing a PowerShell script file
	/// </summary>
	/// <param name="path">The path to the powershell script</param>
	/// <param name="workingDirectory">The working directory for the script</param>
	/// <returns>The process proxy</returns>
	/// <exception cref="FileNotFoundException">Thrown if the path could not be found</exception>
	public static ProcessProxy PowerShellFile(string path, string? workingDirectory = null)
	{
		if (!File.Exists(path))
			throw new FileNotFoundException("The specified PowerShell script file was not found.", path);

		path = Path.GetFullPath(path.Trim());
		if (!(path.StartsWith('"') && path.EndsWith('"')))
			path = $"\"{path}\"";

		return new ProcessProxy(POWERSHELL_FILENAME)
			.WithArgs(string.Format(POWERSHELL_ARGUMENTS_FILE, path))
			.WithWorkingDirectory(workingDirectory);
	}
	#endregion
}