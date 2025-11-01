using Mono.Unix.Native;

namespace SteamServerManager.Processing;

using Delegates;
using Exceptions;
using Signaling;

/// <summary>
/// Represents a proxy for managing processes
/// </summary>
/// <param name="_info">The information used for starting the process</param>
/// <remarks>
/// <para>The following properties on <paramref name="_info"/> parameter will be overwritten:</para>
/// <para><see cref="ProcessStartInfo.CreateNoWindow"/> will always be <see langword="true"/></para>
/// <para><see cref="ProcessStartInfo.RedirectStandardError"/> will always be <see langword="true"/></para>
/// <para><see cref="ProcessStartInfo.RedirectStandardInput"/> will always be <see langword="true"/></para>
/// <para><see cref="ProcessStartInfo.RedirectStandardOutput"/> will always be <see langword="true"/></para>
/// <para><see cref="ProcessStartInfo.UseShellExecute"/> will always be <see langword="false"/></para>
/// <para><see cref="ProcessStartInfo.WindowStyle"/> will always be <see cref="ProcessWindowStyle.Hidden"/></para>
/// </remarks>
public class ProcessProxy(
	ProcessStartInfo _info) : TextWriter, IAsyncDisposable
{
	/// <summary>
	/// The file name for executing PowerShell scripts
	/// </summary>
	public const string POWERSHELL_FILENAME = "powershell.exe";
	/// <summary>
	/// Command arguments for executing PowerShell scripts
	/// </summary>
	public const string POWERSHELL_ARGUMENTS = "-NoProfile -ExecutionPolicy unrestricted";
	/// <summary>
	/// Command arguments for executing encoded in-line PowerShell scripts
	/// </summary>
	public const string POWERSHELL_ARGUMENTS_ENCODED = POWERSHELL_ARGUMENTS + " -EncodedCommand {0}";
	/// <summary>
	/// Command arguments for executing PowerShell script files
	/// </summary>
	public const string POWERSHELL_ARGUMENTS_FILE = POWERSHELL_ARGUMENTS + " -File {0}";
	/// <summary>
	/// The default encoding to use for all process proxies
	/// </summary>
	public static Encoding DefaultEncoding { get; set; } = Encoding.UTF8;

	private readonly Stopwatch _timer = new();
	private readonly CancellationTokenSource _cancel = new();
	private readonly SemaphoreSlim _accessControl = new(1, 1);
	private readonly StringBuilder _stdOut = new();
	private readonly StringBuilder _stdErr = new();
	private Process? _process;
	private Task? _task;
	private Encoding? _encoding;
	private string? _logName;
	private readonly FixedQueue<ProcessError> _errors = new(10);

	#region Events
	/// <summary>
	/// Triggered whenever a line is received from standard output
	/// </summary>
	public event ProcessOutputHandler OnStandardOutput = delegate { };
	/// <summary>
	/// Triggered whenever a line is received from standard error
	/// </summary>
	public event ProcessOutputHandler OnStandardError = delegate { };
	/// <summary>
	/// Triggered when the process has started
	/// </summary>
	public event ProcessVoidHandler OnStarted = delegate { };
	/// <summary>
	/// Triggered when the process has exited
	/// </summary>
	public event ProcessVoidHandler OnExited = delegate { };
	/// <summary>
	/// Triggered when an exception occurs during process execution
	/// </summary>
	public event ProcessErrorHandler OnException = delegate { };
	#endregion

	#region Auto Properties
	/// <summary>
	/// The amount of time the process has been running
	/// </summary>
	public TimeSpan Elapsed => _timer.Elapsed;
	/// <summary>
	/// Indicates whether or not cancellation has been requested for the process
	/// </summary>
	public bool Cancelled => _cancel.IsCancellationRequested;
	/// <summary>
	/// Everything that has been written to standard output
	/// </summary>
	/// <remarks>Only available if <see cref="TrackOutput"/> is enabled</remarks>
	public string StandardOutput => _stdOut.ToString();
	/// <summary>
	/// Gets the standard error output captured during the execution of the process.
	/// </summary>
	/// <remarks>Only available if <see cref="TrackError"/> is enabled</remarks>
	public string StandardError => _stdErr.ToString();
	/// <summary>
	/// The exit code of the process or -1 if the process has not exited
	/// </summary>
	public int ExitCode => _process?.ExitCode ?? -1;
	/// <summary>
	/// The writer to the standard input of the process
	/// </summary>
	/// <remarks>Only available if <see cref="Running"/> is <see langword="true"/></remarks>
	public StreamWriter Writer => _process?.StandardInput ?? throw new NotSupportedException("Cannot access a writer for a process that hasn't started");
	/// <summary>
	/// Whether or not the process is currently running
	/// </summary>
	public bool Running { get; private set; } = false;
	/// <summary>
	/// The exception that was thrown by the process
	/// </summary>
	public ProcessError? LastError => _errors.LastOrDefault();
	/// <summary>
	/// The last 10 errors that have occurred within the process proxy
	/// </summary>
	public IEnumerable<ProcessError> Errors => _errors;
	/// <summary>
	/// The result of the process at the current point in time
	/// </summary>
	/// <remarks>If the process hasn't exited, the <see cref="ProcessResult.ExitCode"/> will be -1</remarks>
	public ProcessResult Result => new (
		_process?.ExitCode ?? -1,
		StandardOutput,
		StandardError.ForceNull(),
		LastError?.Exception,
		Elapsed);
	/// <summary>
	/// The name of the file being executed
	/// </summary>
	public string FileName => _info.FileName;
	/// <summary>
	/// The arguments being passed to the executable
	/// </summary>
	public string Arguments => string.IsNullOrEmpty(_info.Arguments) ? string.Join(' ', _info.ArgumentList) : _info.Arguments;
	/// <inheritdoc />
	public override Encoding Encoding => _encoding ??= DefaultEncoding;
	#endregion

	#region Configuration Properties
	/// <summary>
	/// Whether or not to write everything from standard output to <see cref="StandardOutput"/>
	/// </summary>
	public bool TrackOutput { get; set; } = false;
	/// <summary>
	/// Whether or not to write everything from standard error to <see cref="StandardError"/>
	/// </summary>
	public bool TrackError { get; set; } = false;
	/// <summary>
	/// The name to use for logging purposes
	/// </summary>
	public string Name
	{
		get => _logName ??= FileName;
		set => _logName = value;
	}
    #endregion

    /// <summary>
    /// Represents a proxy for managing processes
    /// </summary>
    /// <param name="command">The executable to run in the new process</param>
    public ProcessProxy(string command) : this(new ProcessStartInfo
	{
		FileName = command,
	}) { }

    #region Configuration methods
    /// <summary>
    /// Sets the working directory of the process
    /// </summary>
    /// <param name="directory">The directory to use</param>
    /// <returns>The current proxy for method chaining</returns>
    public ProcessProxy WithWorkingDirectory(string? directory)
	{
		_info.WorkingDirectory = directory ?? string.Empty;
		return this;
	}

	/// <summary>
	/// Sets the command line arguments for the proxy
	/// </summary>
	/// <param name="arguments">The arguments to use</param>
	/// <returns>The current proxy for method chaining</returns>
	public ProcessProxy WithArgs(string? arguments)
	{
		_info.Arguments = arguments ?? string.Empty;
		return this;
	}

	/// <inheritdoc cref="WithArgs(string?)"/>
	public ProcessProxy WithArgs(params string?[] arguments)
	{
		foreach (var arg in arguments)
			if (!string.IsNullOrWhiteSpace(arg))
				_info.ArgumentList.Add(arg);
		return this;
	}

	/// <summary>
	/// Sets the username for the process
	/// </summary>
	/// <param name="username">The username to use for the process</param>
	/// <returns>The current proxy for method chaining</returns>
	public ProcessProxy WithUser(string? username)
	{
		_info.UserName = username ?? string.Empty;
		return this;
	}

	/// <summary>
	/// Sets the given environment variable for the process
	/// </summary>
	/// <param name="key">The key of the variable</param>
	/// <param name="value">The value of the variable</param>
	/// <returns>The current proxy for method chaining</returns>
	public ProcessProxy WithEnvArg(string key, string? value)
	{
		_info.Environment[key] = value;
		return this;
	}

	/// <summary>
	/// Sets the given environment variables for the process
	/// </summary>
	/// <param name="args">The environment variables to set</param>
	/// <returns>The current proxy for method chaining</returns>
	public ProcessProxy WithEnvArgs(params (string key, string? value)[] args)
	{
		foreach (var (key, value) in args)
			WithEnvArg(key, value);
		return this;
	}

	/// <summary>
	/// Sets the given environment variables for the process
	/// </summary>
	/// <param name="args">The environment variables to set</param>
	/// <returns>The current proxy for method chaining</returns>
	public ProcessProxy WithEnvArgs(IEnumerable<KeyValuePair<string, string?>> args)
	{
		foreach (var (key, value) in args)
			WithEnvArg(key, value);
		return this;
	}

    /// <summary>
    /// Registers a logger for the process events
    /// </summary>
    /// <param name="logger">The logger to log the events to</param>
    /// <param name="name">The name of the logger to use</param>
    /// <returns>The current proxy for method chaining</returns>
    [SuppressMessage("Performance", "CA1873:Avoid potentially expensive logging", Justification = "Stupid warning")]
    public ProcessProxy WithLogger(ILogger logger, string? name = null)
	{
		if (!string.IsNullOrEmpty(name))
			Name = name;
		OnStandardOutput += (line) => logger.LogInformation("[{ProcessName}::STDOUT] {Line}", Name, line);
		OnStandardError += (line) => logger.LogWarning("[{ProcessName}::STDERR] {Line}", Name, line);
		OnException += (code, exception) => logger.LogError(exception, "[{ProcessName}::EXCEPTION] An exception occurred: {ErrorCode}", Name, code);
		OnExited += () => logger.LogInformation("[{ProcessName}::EXITED] Process has exited.", Name);
		OnStarted += () => logger.LogInformation("[{ProcessName}::STARTED] Process has started.", Name);
		return this;
	}

	/// <summary>
	/// Enables or disables tracking standard output and error streams to <see cref="StandardOutput"/> and <see cref="StandardError"/>
	/// </summary>
	/// <param name="stdOut">Whether or not to track standard output and pipe the results to <see cref="StandardOutput"/></param>
	/// <param name="stdErr">Whether or not to track standard error and pipe the results to <see cref="StandardError"/></param>
	/// <returns>The current proxy for method chaining</returns>
	public ProcessProxy WithTracking(bool stdOut = true, bool stdErr = true)
	{
		TrackError = stdErr;
		TrackOutput = stdOut;
		return this;
	}
	#endregion

	#region Public Methods
	/// <summary>
	/// Attempts to start the process
	/// </summary>
	/// <param name="token">The cancellation token to use for the process</param>
	/// <returns>
	/// <para><see langword="true"/> if the process was started.</para>
	/// <para><see langword="false"/> if the process was already running or an exception occurred</para>
	/// </returns>
	public async Task<bool> Start(CancellationToken token = default)
	{
		var tsc = new TaskCompletionSource();
		token.Register(() => tsc.TrySetCanceled());
		void Started() => tsc.TrySetResult();

		try
		{
			await _accessControl.WaitAsync(token);

			if (Running) return false;

			token.Register(_cancel.Cancel);
			Running = true;

			EnsureStartArgs();
			_process = new Process
			{
				StartInfo = _info,
				EnableRaisingEvents = true,
			};
			_process.OutputDataReceived += (_, args) =>
			{
				if (string.IsNullOrEmpty(args.Data)) return;
                OnStandardOutput(args.Data);
				if (!TrackOutput) return;
				_stdOut.AppendLine(args.Data);
			};
			_process.ErrorDataReceived += (_, args) =>
			{
				if (string.IsNullOrEmpty(args.Data)) return;
				OnStandardError(args.Data);
				if (!TrackError) return;
				_stdErr.AppendLine(args.Data);
			};

			OnStarted += Started;
			_task = Task.Run(ExecuteThread, CancellationToken.None);
			await tsc.Task;
			OnStarted -= Started;
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

	/// <summary>
	/// Attempts to gracefully stop the process
	/// </summary>
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
		bool Send(Signal signal, ProcessErrorCode err)
		{
			try
			{
				return SendSignal(signal, out var code) && code == 0;
			}
			catch (Exception ex)
			{
				SetError(err, ex);
				return false;
			}
		}

		bool SendCloseMainWindow()
		{
			try { return _process!.CloseMainWindow(); }
			catch (Exception ex)
			{
				OnException(ProcessErrorCode.Stop_CloseMainWindow, ex);
				return false;
			}
		}

		try
		{
			await _accessControl.WaitAsync(token);
			if (_process is null || _process.HasExited)
				return true;

			Func<bool>[] tries =
			[
				() => Send(Signal.Interupt, ProcessErrorCode.Stop_Interupt),
				() => Send(Signal.Abort, ProcessErrorCode.Stop_Abort),
				() => Send(Signal.Terminate, ProcessErrorCode.Stop_Term),
				SendCloseMainWindow,
			];

			foreach (var action in tries)
			{
				if (action())
					return true;
			}

			return false;
		}
		catch (Exception ex)
		{
			OnException(ProcessErrorCode.Stop, ex);
			return false;
		}
		finally
		{
			_accessControl.Release();
		}
	}

	/// <summary>
	/// Attempts to forcefully kill the process
	/// </summary>
	/// <returns>
	/// <para><see langword="true"/> if the process was killed or is already stopped</para>
	/// <para><see langword="false"/> if the process threw an error while attempting to kill</para>
	/// </returns>
	public async Task<bool> Kill(CancellationToken token = default)
	{
		try
		{
			await _accessControl.WaitAsync(token);

			if (_process is null || _process.HasExited)
				return true;

			_process.Kill(true);
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

	/// <summary>
	/// Waits until the process is closed
	/// </summary>
	/// <param name="token">The cancellation token for the wait operation</param>
	/// <remarks>
	/// <para>Prefer hooking <see cref="OnExited"/></para>
	/// <para><see cref="OperationCanceledException"/> is not thrown when the <paramref name="token"/> is cancelled.</para>
	/// </remarks>
	public async Task WaitForExit(CancellationToken token = default)
	{
		if (_task is null) return;

		try
		{
			await Task.WhenAny(
				_task,
				Task.Delay(-1, token));
		}
		catch (OperationCanceledException) { }
	}

	/// <summary>
	/// Waits until the process is closed and returns the result
	/// </summary>
	/// <param name="token">The cancellation token for the wait operation</param>
	/// <returns>The result of the process</returns>
	/// <remarks>
	/// <para>if <see cref="TrackOutput"/> is <see langword="false"/> then <see cref="ProcessResult.OutputStream"/> will be <see cref="string.Empty"/></para>
	/// <para>if <see cref="TrackError"/> is <see langword="false"/> then <see cref="ProcessResult.ErrorStream"/> will by <see langword="null"/></para>
	/// <para><see cref="OperationCanceledException"/> is not thrown when the <paramref name="token"/> is cancelled.</para>
	/// </remarks>
	public async Task<ProcessResult> WaitForResult(CancellationToken token = default)
	{
		await WaitForExit(token);
		return Result;
	}

	/// <summary>
	/// Sends a signal to the process based on the current platform
	/// </summary>
	/// <param name="signal">The signal to send</param>
	/// <param name="code">The return code of the call, or -1 if something went wrong</param>
	/// <returns>Whether or not the signal was sent, regardless of the result of the siganl</returns>
	/// <remarks>If this returns <see langword="false"/> you can check <see cref="LastError"/> for more information.</remarks>
	/// <exception cref="NotSupportedException">Thrown if the signal isn't supported on the current platform</exception>
	public bool SendSignal(Signal signal, out int code)
	{
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			var ws = signal switch
			{
				Signal.Abort => WindowsSignal.CTRL_CLOSE_EVENT,
				Signal.Terminate => WindowsSignal.CTRL_SHUTDOWN_EVENT,
				Signal.Interupt => WindowsSignal.CTRL_C_EVENT,
				_ => throw new NotSupportedException($"Signal {signal} is not supported on Windows.")
			};
			return SendSignalWindows(ws, out code);
		}

		var ps = signal switch
		{
			Signal.Abort => Signum.SIGABRT,
			Signal.Terminate => Signum.SIGTERM,
			Signal.Interupt => Signum.SIGINT,
			_ => throw new NotSupportedException($"Signal {signal} is not supported on POSIX/UNIX.")
		};
		return SendSignalPosix(ps, out code);
	}

	/// <summary>
	/// Attempts to send a Windows signal to process
	/// </summary>
	/// <param name="signal">The signal to send to the process</param>
	/// <param name="code">The return code of the call, or -1 if something went wrong</param>
	/// <returns>Whether or not the signal was sent, regardless of the result of the siganl</returns>
	/// <remarks>
	/// <para>This does not work on POSIX/UNIX, use either <see cref="SendSignalPosix(Signum, out int)"/> or <see cref="SendSignal(Signal, out int)"/></para>
	/// <para>If this returns <see langword="false"/> you can check <see cref="LastError"/> for more information.</para>
	/// </remarks>
	public bool SendSignalWindows(WindowsSignal signal, out int code)
	{
		if (_process is null || _process.HasExited)
		{
			code = -1;
			SetError(ProcessErrorCode.Signal_ProcessNotRunning, new ProcessNotRunningException());
			return false;
		}

		if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			code = -1;
			SetError(ProcessErrorCode.Signal_NotSupported, new PlatformNotSupportedException("You cannot send Windows signals on POSIX/UNIX!"));
			return false;
		}

		code = WindowsConsoleCloser.SendSignal(_process, signal);
		return true;
	}

	/// <summary>
	/// Attempts to send a POSIX signal to process
	/// </summary>
	/// <param name="signal">The signal to send to the process</param>
	/// <param name="code">The return code of the call, or -1 if something went wrong</param>
	/// <returns>Whether or not the signal was sent, regardless of the result of the siganl</returns>
	/// <remarks>
	/// <para>This does not work on Windows, use either <see cref="SendSignalWindows(WindowsSignal, out int)"/> or <see cref="SendSignal(Signal, out int)"/></para>
	/// <para>If this returns <see langword="false"/> you can check <see cref="LastError"/> for more information.</para>
	/// </remarks>
	public bool SendSignalPosix(Signum signal, out int code)
	{
		if (_process is null || _process.HasExited)
		{
			code = -1;
			SetError(ProcessErrorCode.Signal_ProcessNotRunning, new ProcessNotRunningException());
			return false;
		}

		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			code = -1;
			SetError(ProcessErrorCode.Signal_NotSupported, new PlatformNotSupportedException("You cannot send POSIX/UNIX signals on Windows!"));
			return false;
		}

		code = Syscall.kill(_process.Id, signal);
		return true;
	}
	#endregion

	#region Text Writer Forwards
	/// <inheritdoc />
	public override void Write(char value)
	{
		if (!EnsureWrite()) return;
		Writer!.Write(value);
	}

	/// <inheritdoc />
	public override void Write(char[]? buffer)
	{
		if (!EnsureWrite() || buffer is null) return;
		Writer!.Write(buffer, 0, buffer.Length);
	}

	/// <inheritdoc />
	public override void Write(char[] buffer, int index, int count)
	{
		if (!EnsureWrite()) return;
		Writer!.Write(buffer, index, count);
	}

	/// <inheritdoc />
	public override void Write(ReadOnlySpan<char> buffer)
	{
		if (!EnsureWrite()) return;
		Writer!.Write(buffer);
	}

	/// <inheritdoc />
	public override void Write(bool value)
	{
		if (!EnsureWrite()) return;
		Writer!.Write(value);
	}

	/// <inheritdoc />
	public override void Write(int value)
	{
		if (!EnsureWrite()) return;
		Writer!.Write(value);
	}

	/// <inheritdoc />
	public override void Write(uint value)
	{
		if (!EnsureWrite()) return;
		Writer!.Write(value);
	}

	/// <inheritdoc />
	public override void Write(long value)
	{
		if (!EnsureWrite()) return;
		Writer!.Write(value);
	}

	/// <inheritdoc />
	public override void Write(ulong value)
	{
		if (!EnsureWrite()) return;
		Writer!.Write(value);
	}

	/// <inheritdoc />
	public override void Write(float value)
	{
		if (!EnsureWrite()) return;
		Writer!.Write(value);
	}

	/// <inheritdoc />
	public override void Write(double value)
	{
		if (!EnsureWrite()) return;
		Writer!.Write(value);
	}

	/// <inheritdoc />
	public override void Write(decimal value)
	{
		if (!EnsureWrite()) return;
		Writer!.Write(value);
	}

	/// <inheritdoc />
	public override void Write(string? value)
	{
		if (!EnsureWrite() || value is null) return;
		Writer!.Write(value);
	}

	/// <inheritdoc />
	public override void Write(object? value)
	{
		if (!EnsureWrite() || value is null) return;
		Writer!.Write(value);
	}

	/// <inheritdoc />
	public override void Write(StringBuilder? value)
	{
		if (!EnsureWrite() || value is null) return;
		Writer!.Write(value);
	}

	/// <inheritdoc />
	public override void Write(string format, object? arg0)
	{
		if (!EnsureWrite()) return;
		Writer!.Write(format, arg0);
	}

	/// <inheritdoc />
	public override void Write(string format, object? arg0, object? arg1)
	{
		if (!EnsureWrite()) return;
		Writer!.Write(format, arg0, arg1);
	}

	/// <inheritdoc />
	public override void Write(string format, object? arg0, object? arg1, object? arg2)
	{
		if (!EnsureWrite()) return;
		Writer!.Write(format, arg0, arg1, arg2);
	}

	/// <inheritdoc />
	public override void Write(string format, params object?[] arg)
	{
		if (!EnsureWrite()) return;
		Writer!.Write(format, arg);
	}

	/// <inheritdoc />
	public override void WriteLine()
	{
		if (!EnsureWrite()) return;
		Writer!.WriteLine();
	}

	/// <inheritdoc />
	public override void WriteLine(char value)
	{
		if (!EnsureWrite()) return;
		Writer!.WriteLine(value);
	}

	/// <inheritdoc />
	public override void WriteLine(char[]? buffer)
	{
		if (!EnsureWrite() || buffer is null) return;
		Writer!.WriteLine(buffer);
	}

	/// <inheritdoc />
	public override void WriteLine(char[] buffer, int index, int count)
	{
		if (!EnsureWrite()) return;
		Writer!.WriteLine(buffer, index, count);
	}

	/// <inheritdoc />
	public override void WriteLine(ReadOnlySpan<char> buffer)
	{
		if (!EnsureWrite()) return;
		Writer!.WriteLine(buffer);
	}

	/// <inheritdoc />
	public override void WriteLine(bool value)
	{
		if (!EnsureWrite()) return;
		Writer!.WriteLine(value);
	}

	/// <inheritdoc />
	public override void WriteLine(int value)
	{
		if (!EnsureWrite()) return;
		Writer!.WriteLine(value);
	}

	/// <inheritdoc />
	public override void WriteLine(uint value)
	{
		if (!EnsureWrite()) return;
		Writer!.WriteLine(value);
	}

	/// <inheritdoc />
	public override void WriteLine(long value)
	{
		if (!EnsureWrite()) return;
		Writer!.WriteLine(value);
	}

	/// <inheritdoc />
	public override void WriteLine(ulong value)
	{
		if (!EnsureWrite()) return;
		Writer!.WriteLine(value);
	}

	/// <inheritdoc />
	public override void WriteLine(float value)
	{
		if (!EnsureWrite()) return;
		Writer!.WriteLine(value);
	}

	/// <inheritdoc />
	public override void WriteLine(double value)
	{
		if (!EnsureWrite()) return;
		Writer!.WriteLine(value);
	}

	/// <inheritdoc />
	public override void WriteLine(decimal value)
	{
		if (!EnsureWrite()) return;
		Writer!.WriteLine(value);
	}

	/// <inheritdoc />
	public override void WriteLine(string? value)
	{
		if (!EnsureWrite() || value is null) return;
		Writer!.WriteLine(value);
	}

	/// <inheritdoc />
	public override void WriteLine(object? value)
	{
		if (!EnsureWrite() || value is null) return;
		Writer!.WriteLine(value);
	}

	/// <inheritdoc />
	public override void WriteLine(StringBuilder? value)
	{
		if (!EnsureWrite() || value is null) return;
		Writer!.WriteLine(value);
	}

	/// <inheritdoc />
	public override void WriteLine(string format, object? arg0)
	{
		if (!EnsureWrite()) return;
		Writer!.WriteLine(format, arg0);
	}

	/// <inheritdoc />
	public override void WriteLine(string format, object? arg0, object? arg1)
	{
		if (!EnsureWrite()) return;
		Writer!.WriteLine(format, arg0, arg1);
	}

	/// <inheritdoc />
	public override void WriteLine(string format, object? arg0, object? arg1, object? arg2)
	{
		if (!EnsureWrite()) return;
		Writer!.WriteLine(format, arg0, arg1, arg2);
	}

	/// <inheritdoc />
	public override void WriteLine(string format, params object?[] arg)
	{
		if (!EnsureWrite()) return;
		Writer!.WriteLine(format, arg);
	}

	/// <inheritdoc />
	public override void Flush()
	{
		if (!EnsureWrite()) return;
		Writer!.Flush();
	}

	/// <inheritdoc />
	public override Task FlushAsync()
	{
		if (!EnsureWrite()) return Task.CompletedTask;
		return Writer!.FlushAsync();
	}

	/// <inheritdoc />
	public override Task WriteAsync(char value)
	{
		if (!EnsureWrite()) return Task.CompletedTask;
		return Writer!.WriteAsync(value);
	}

	/// <inheritdoc />
	public override Task WriteAsync(char[] buffer, int index, int count)
	{
		if (!EnsureWrite()) return Task.CompletedTask;
		return Writer!.WriteAsync(buffer, index, count);
	}

	/// <inheritdoc />
	public override Task WriteAsync(string? value)
	{
		if (!EnsureWrite() || value is null) return Task.CompletedTask;
		return Writer!.WriteAsync(value);
	}

	/// <inheritdoc />
	public override Task WriteAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default)
	{
		if (!EnsureWrite()) return Task.CompletedTask;
		return Writer!.WriteAsync(buffer, cancellationToken);
	}

	/// <inheritdoc />
	public override Task WriteAsync(StringBuilder? value, CancellationToken cancellationToken = default)
	{
		if (!EnsureWrite() || value is null) return Task.CompletedTask;
		return Writer!.WriteAsync(value, cancellationToken);
	}

	/// <inheritdoc />
	public override Task WriteLineAsync()
	{
		if (!EnsureWrite()) return Task.CompletedTask;
		return Writer!.WriteLineAsync();
	}

	/// <inheritdoc />
	public override Task WriteLineAsync(char value)
	{
		if (!EnsureWrite()) return Task.CompletedTask;
		return Writer!.WriteLineAsync(value);
	}

	/// <inheritdoc />
	public override Task WriteLineAsync(char[] buffer, int index, int count)
	{
		if (!EnsureWrite()) return Task.CompletedTask;
		return Writer!.WriteLineAsync(buffer, index, count);
	}

	/// <inheritdoc />
	public override Task WriteLineAsync(string? value)
	{
		if (!EnsureWrite() || value is null) return Task.CompletedTask;
		return Writer!.WriteLineAsync(value);
	}

	/// <inheritdoc />
	public override Task WriteLineAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default)
	{
		if (!EnsureWrite()) return Task.CompletedTask;
		return Writer!.WriteLineAsync(buffer, cancellationToken);
	}

	/// <inheritdoc />
	public override Task WriteLineAsync(StringBuilder? value, CancellationToken cancellationToken = default)
	{
		if (!EnsureWrite() || value is null) return Task.CompletedTask;
		return Writer!.WriteLineAsync(value, cancellationToken);
	}
	#endregion

	#region Internal Methods
	/// <summary>
	/// Ensures the process is running before attempting to write to standard input
	/// </summary>
	/// <returns>
	/// <para><see langword="true"/> if the process can be written to</para>
	/// <para><see langword="false"/> if the process is not running</para>
	/// </returns>
	internal bool EnsureWrite()
	{
		if (_process is not null && !_process.HasExited)
			return true;

		SetError(ProcessErrorCode.Write, new ProcessNotRunningException());
		return false;
	}

	/// <summary>
	/// Sets <see cref="LastError"/> and triggers <see cref="OnException"/> for the given code and exception
	/// </summary>
	/// <param name="code">The code / reason the exception occurred</param>
	/// <param name="ex">The exception that was thrown</param>
	internal void SetError(ProcessErrorCode code, Exception ex)
	{
		_errors.Enqueue(new (code, ex));
		OnException(code, ex);
	}

	/// <summary>
	/// Ensures the required <see cref="ProcessStartInfo"/> properties are set
	/// </summary>
	internal void EnsureStartArgs()
	{
		_info.CreateNoWindow = true;
		_info.UseShellExecute = false;
		_info.RedirectStandardOutput = true;
		_info.RedirectStandardError = true;
		_info.RedirectStandardInput = true;
		_info.WindowStyle = ProcessWindowStyle.Hidden;
	}

	/// <summary>
	/// The main execution thread for the process
	/// </summary>
	internal async Task ExecuteThread()
	{
		if (_process is null)
			return;

		try
		{
			_timer.Start();
			_process.Start();
			_process.BeginErrorReadLine();
			_process.BeginOutputReadLine();

			OnStarted();
			await _process.WaitForExitAsync(_cancel.Token);
		}
		catch (OperationCanceledException) { }
		catch (Exception ex)
		{
			SetError(ProcessErrorCode.RunningProcess, ex);
		}
		finally
		{
			CleanProcess();
		}
	}

	/// <summary>
	/// Cleans up the resources associated with the process
	/// </summary>
	internal void CleanProcess()
	{
		if (_process is null) return;

		try
		{
			_process.CancelErrorRead();
			_process.CancelOutputRead();
		}
		catch { }

		if (!_process.HasExited)
		{
			try
			{
				_process.Kill(true);
			}
			catch { }
		}
		Running = false;
		_timer.Stop();
		_process.Dispose();
		_process = null;
		OnExited();
	}
	#endregion

	/// <inheritdoc />
	public new async ValueTask DisposeAsync()
	{
		_cancel.Cancel();
		if (_task != null)
			await _task;
		_accessControl.Dispose();
		_cancel.Dispose();
		await base.DisposeAsync();
		GC.SuppressFinalize(this);
	}

	#region Static helper methods
	/// <summary>
	/// Creates a process proxy for executing an inline PowerShell script
	/// </summary>
	/// <param name="script">The inline script to run</param>
	/// <param name="workingDirectory">The working directory for the script</param>
	/// <returns>The process proxy</returns>
	public static ProcessProxy PowerShellScript(string script, string? workingDirectory = null)
	{
		script = Convert.ToBase64String(DefaultEncoding.GetBytes(script));
		script = string.Format(POWERSHELL_ARGUMENTS_ENCODED, script);
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

		path = path.Trim();
		path = Path.GetFullPath(path);
		if (!(path.StartsWith('"') && path.EndsWith('"')))
			path = $"\"{path}\"";

		var args = string.Format(POWERSHELL_ARGUMENTS_FILE, path);
		return new ProcessProxy(POWERSHELL_FILENAME)
			.WithArgs(args)
			.WithWorkingDirectory(workingDirectory);
	}
	#endregion
}
